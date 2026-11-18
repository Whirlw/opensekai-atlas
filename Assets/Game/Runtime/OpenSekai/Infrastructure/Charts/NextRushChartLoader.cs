using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using OpenSekai.Charts;
using OpenSekai.Live.Conversion;
using OpenSekai.Shared.Scoring;
using Sekai;
using Sekai.Live;

namespace OpenSekai.Infrastructure.Charts
{
    /// <summary>Builds live notes directly from NextRUSH entities, without a ChartDocument round trip.</summary>
    public static class NextRushChartLoader
    {
        public const string Format = "next-rush";
        private const int MaxFileBytes = 32 * 1024 * 1024;
        private static readonly HashSet<string> NoteFields = new HashSet<string>(new[] {
            "#BEAT", "#TIMESCALE_GROUP", "lane", "size", "style", "direction", "isAttached", "isSeparator",
            "connectorEase", "segmentKind", "segmentAlpha", "segmentLayer", "effectKind", "segmentThroughJudgeLine",
            "segmentPresentation", "next", "prev", "activeHead", "elevation"
        }, StringComparer.Ordinal);

        public static bool IsNativeFileName(string path) =>
            path.EndsWith(".next-rush.json", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".next-rush.json.gz", StringComparison.OrdinalIgnoreCase);

        public static NextRushChart ReadFile(string path)
        {
            using var file = File.OpenRead(path);
            if (file.Length > MaxFileBytes) throw new InvalidDataException("NextRUSH+ 谱面文件超过大小限制。");
            int first = file.ReadByte(), second = file.ReadByte();
            file.Position = 0;
            bool gzip = first == 0x1f && second == 0x8b;
            using Stream input = gzip ? new GZipStream(file, CompressionMode.Decompress) : (Stream)file;
            using var reader = new StreamReader(input, new UTF8Encoding(false, true));
            var text = new StringBuilder();
            var buffer = new char[8192];
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (text.Length + read > NextRushChart.MaxJsonCharacters)
                    throw new InvalidDataException("NextRUSH+ 解压后的谱面超过大小限制。");
                text.Append(buffer, 0, read);
            }
            return NextRushChart.Parse(text.ToString());
        }

        public static RuntimeChartDefinition Load(NextRushChart source, LiveBundleBuildData config, bool showPair = true)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (config == null) throw new ArgumentNullException(nameof(config));
            var compatibility = NextRushCompatibility.Analyze(source);
            if (compatibility.InvalidData.Count > 0) throw Invalid(string.Join("、", compatibility.InvalidData.Take(8)));
            if (compatibility.UnsupportedFeatures.Count > 0) throw Unsupported(string.Join("、", compatibility.UnsupportedFeatures.Take(8)));
            return new Builder(source, config, showPair).Build(compatibility);
        }

        private sealed class Builder
        {
            private readonly NextRushChart _source;
            private readonly LiveBundleBuildData _config;
            private readonly bool _showPair;
            private readonly Dictionary<NextRushEntity, NoteBase> _notes = new Dictionary<NextRushEntity, NoteBase>();
            private readonly Dictionary<NextRushEntity, NextRushEntity> _owners = new Dictionary<NextRushEntity, NextRushEntity>();
            private readonly List<NextRushEntity> _hidden = new List<NextRushEntity>();
            private readonly List<(double beat, double time, double bpm)> _bpm = new List<(double, double, double)>();
            private MusicScoreInfo[] _timing;
            private int _nextId;

            public Builder(NextRushChart source, LiveBundleBuildData config, bool showPair)
            { _source = source; _config = config; _showPair = showPair; }

            public RuntimeChartDefinition Build(NextRushCompatibilityReport compatibility)
            {
                BuildTiming();
                foreach (var entity in _source.Entities)
                {
                    if (NextRushCompatibility.Classify(entity.Archetype) == NextRushEntityStatus.IgnoredByProject) continue;
                    switch (entity.Archetype)
                    {
                        case "#BPM_CHANGE": case "Initialization": break;
                        case "#TIMESCALE_GROUP":
                            Fields(entity, "editorName");
                            if (entity.Has("editorName")) entity.Reference("editorName");
                            break;
                        case "Connector": case "SimLine": break;
                        case "TransientHiddenTickNote": _hidden.Add(entity); break;
                        default: _notes.Add(entity, CreateNote(entity)); break;
                    }
                }
                var roots = new List<NoteBase>();
                foreach (var entry in _notes)
                {
                    var entity = entry.Key;
                    if (Kind(entity).StartsWith("Head", StringComparison.Ordinal))
                    {
                        if (entity.Has("prev") || entity.Has("activeHead")) throw Unsupported("嵌套的长条起点");
                        BindChain(entity, (LongNote)entry.Value);
                        roots.Add(entry.Value);
                    }
                    else if (!Kind(entity).StartsWith("Tail", StringComparison.Ordinal) && !Kind(entity).Equals("TickNote"))
                        roots.Add(entry.Value);
                }
                foreach (var entry in _notes)
                {
                    bool member = Kind(entry.Key).StartsWith("Tail", StringComparison.Ordinal) || Kind(entry.Key) == "TickNote";
                    if (member && !_owners.ContainsKey(entry.Key)) throw Invalid("孤立的长条节点 " + entry.Key.Name);
                    if (!member && !Kind(entry.Key).StartsWith("Head", StringComparison.Ordinal)
                        && (entry.Key.Has("next") || entry.Key.Has("prev") || entry.Key.Has("activeHead")))
                        throw Unsupported("独立音符的长条连接");
                }
                ValidateVisualReferences();
                foreach (var tick in _hidden) BindHiddenTick(tick);
                foreach (var root in roots.OfType<LongNote>()) root.SortNoteList();
                if (_showPair) SetPairs(roots);
                var score = new MusicScore(_timing, Array.Empty<EventBase>(), roots.OrderBy(n => n.MusicScoreInfo.time).ToArray());
                double lastBeat = _notes.Keys.Select(e => Beat(e)).Concat(_hidden.Select(e => Beat(e))).DefaultIfEmpty(0).Max();
                return RuntimeChartDefinition.CreateNative(_source, score, checked((long)Math.Round(lastBeat * ChartTimeline.TICKS_PER_BEAT)), compatibility);
            }

            private void BuildTiming()
            {
                var changes = _source.Entities.Where(e => e.Archetype == "#BPM_CHANGE").OrderBy(Beat).ToArray();
                if (changes.Length == 0 || Beat(changes[0]) != 0) throw Invalid("需要位于第 0 拍的 BPM");
                var timing = new List<MusicScoreInfo>();
                double time = 0, previousBeat = 0, bpm = 120;
                for (int i = 0; i < changes.Length; i++)
                {
                    var entity = changes[i];
                    Fields(entity, "#BEAT", "#BPM", "meter");
                    double beat = Beat(entity), nextBpm = entity.Number("#BPM");
                    if (i > 0 && beat == previousBeat) throw Invalid("同一拍有重复 BPM");
                    if (nextBpm <= 0 || nextBpm > 10000) throw Invalid("BPM 超出有效范围");
                    if (entity.Number("meter", 4) != 4) throw Unsupported("非 4/4 拍号");
                    time += (beat - previousBeat) * 60 / bpm;
                    bpm = nextBpm;
                    timing.Add(Info(beat, time, bpm));
                    _bpm.Add((beat, time, bpm));
                    previousBeat = beat;
                }
                _timing = timing.ToArray();
            }

            private MusicScoreInfo At(double beat)
            {
                int low = 0, high = _bpm.Count - 1;
                while (low < high)
                {
                    int mid = (low + high + 1) / 2;
                    double at = _bpm[mid].beat;
                    if (at <= beat) low = mid; else high = mid - 1;
                }
                var start = _bpm[low];
                return Info(beat, start.time + (beat - start.beat) * 60 / start.bpm, start.bpm);
            }

            private static MusicScoreInfo Info(double beat, double time, double bpm)
            {
                if (double.IsNaN(time) || Math.Abs(time) > NextRushChart.MaxAbsoluteTimeSeconds)
                    throw Invalid("谱面时间超出播放范围");
                int bar = (int)Math.Floor(beat / 4);
                return new MusicScoreInfo(bar, (float)(beat / 4 - bar), (float)time, (float)bpm, 4f, 1f, 1f);
            }

            private NoteBase CreateNote(NextRushEntity entity)
            {
                Fields(entity, NoteFields.ToArray());
                string kind = Kind(entity);
                var type = TypeOf(entity);
                var group = Resolve(entity, "#TIMESCALE_GROUP", false);
                if (group != null && group.Archetype != "#TIMESCALE_GROUP") throw Invalid("音符引用的变速组无效");
                foreach (string field in new[] { "style", "isAttached", "isSeparator", "segmentLayer", "effectKind", "segmentThroughJudgeLine", "segmentPresentation", "elevation" })
                    if (entity.Number(field) != 0) throw Unsupported(entity.Archetype + "." + field);
                if (entity.Number("segmentAlpha", 1) != 1) throw Unsupported("长条透明度");
                if (entity.Number("connectorEase", 1) != 1) throw Unsupported("非线性长条");
                double segment = entity.Number("segmentKind", type == NoteType.Critical ? 2 : 1);
                if (segment != 1 && segment != 2) throw Unsupported("segmentKind=" + segment);
                if ((kind.StartsWith("Head", StringComparison.Ordinal) || kind == "TickNote")
                    && segment != (type == NoteType.Critical ? 2 : 1)) throw Unsupported("与长条音符类型不一致的 segmentKind");
                if (entity.Number("direction") < 0 || entity.Number("direction") > 2 || entity.Number("direction") % 1 != 0)
                    throw Unsupported("向下划键方向");
                if (!kind.Contains("Flick") && entity.Number("direction") != 0) throw Unsupported("非划键方向");
                double size = entity.Number("size"), lane = entity.Number("lane");
                if (!entity.Has("lane") || size < 0.5 || lane - size < -6 || lane + size > 6)
                    throw Unsupported("超出 12 轨或宽度小于一轨的音符");
                float left = (float)(lane - size + 6), right = (float)(lane + size + 5);
                var info = At(Beat(entity));
                int id = ++_nextId, a = (int)Math.Floor(left), b = (int)Math.Ceiling(right);
                NoteBase note;
                var direction = (NoteDirection)(int)entity.Number("direction");
                float flick = LiveUtility.ScreenDpiToInch(_config.FlickDistance);
                switch (kind)
                {
                    case "TapNote": case "TailReleaseNote":
                        note = new NormalNote(info, id, a, b, NoteCategory.Normal, 1, type, Offset(NoteCategory.Normal)); break;
                    case "FlickNote": case "TailFlickNote":
                        note = new FlickNote(info, id, a, b, NoteCategory.Flick, 1, type, direction, Offset(NoteCategory.Flick), flick); break;
                    case "TraceNote": case "TailTraceNote":
                        note = new FrictionNote(info, id, a, b, NoteCategory.Friction, _config, type, 1, NoteLineType.Linear); break;
                    case "TraceFlickNote": case "TailTraceFlickNote":
                        note = new FrictionFlickNote(info, id, a, b, NoteCategory.FrictionFlick, 1, type, direction, flick, Offset(NoteCategory.FrictionFlick)); break;
                    case "HeadTapNote":
                        note = new LongNote(info, id, a, b, NoteCategory.Long, type, 1, Offset(NoteCategory.Long)); break;
                    case "TickNote":
                        note = new ConnectionNote(info, id, a, b, NoteCategory.Connection, type, 1, Offset(NoteCategory.Connection)); break;
                    default: throw Unsupported(entity.Archetype);
                }
                SetGeometry(note, left, right);
                return note;
            }

            private void BindChain(NextRushEntity head, LongNote root)
            {
                if (head.Name == null) throw Invalid("长条起点缺少 name");
                var current = head;
                var visited = new HashSet<NextRushEntity> { head };
                _owners.Add(head, head);
                while (true)
                {
                    var next = Resolve(current, "next", true);
                    if (!_notes.TryGetValue(next, out var note)) throw Invalid("长条 next 未指向音符");
                    if (!visited.Add(next) || _owners.ContainsKey(next)) throw Invalid("长条连接存在循环或共享节点");
                    if (Beat(next) <= Beat(current)) throw Invalid("长条节点时间必须递增");
                    if (next.Has("prev") && Resolve(next, "prev", true) != current) throw Invalid("长条 prev 与 next 不一致");
                    if (next.Has("activeHead") && Resolve(next, "activeHead", true) != head) throw Invalid("长条 activeHead 不一致");
                    _owners.Add(next, head);
                    string kind = Kind(next);
                    bool isTail = kind.StartsWith("Tail", StringComparison.Ordinal);
                    if (kind == "TickNote") root.AddConnectionNote(note);
                    else if (isTail)
                    {
                        if (next.Has("next")) throw Invalid("长条尾部仍有 next");
                        root.AddNoteListAndSetupChildNote(note);
                    }
                    else throw Unsupported("长条中间节点 " + next.Archetype);
                    // Legacy parenting promotes a tail to its head's critical type. Native entities
                    // specify their own type and geometry; restore those after binding the graph.
                    note.Type = TypeOf(next);
                    SetGeometry(note, note.LaneStartF, note.LaneEndF);
                    if (isTail) break;
                    current = next;
                }
            }

            private void BindHiddenTick(NextRushEntity entity)
            {
                Fields(entity, "#BEAT", "isAttached", "attachHead", "attachTail");
                if (entity.Number("isAttached") != 1) throw Unsupported("未附着的隐藏判定点");
                var head = Resolve(entity, "attachHead", true);
                var tail = Resolve(entity, "attachTail", true);
                if (!_owners.TryGetValue(head, out var rootEntity) || !_owners.TryGetValue(tail, out var tailOwner)
                    || rootEntity != tailOwner || !(Beat(head) < Beat(entity) && Beat(entity) < Beat(tail)))
                    throw Invalid("隐藏判定点的附着连接无效");
                var root = (LongNote)_notes[rootEntity];
                var a = _notes[head]; var b = _notes[tail];
                var info = At(Beat(entity));
                float progress = (info.time - a.MusicScoreInfo.time) / (b.MusicScoreInfo.time - a.MusicScoreInfo.time);
                var tick = new LongHoldCombo(info, root.Type, ++_nextId, 1, Offset(NoteCategory.Combo));
                root.AddHoldCombo(tick);
                SetGeometry(tick, a.LaneStartF + (b.LaneStartF - a.LaneStartF) * progress, a.LaneEndF + (b.LaneEndF - a.LaneEndF) * progress);
            }

            private void ValidateVisualReferences()
            {
                var segments = new HashSet<NextRushEntity>();
                foreach (var entity in _source.Entities)
                {
                    if (entity.Archetype == "Connector")
                    {
                        Fields(entity, "head", "tail", "segmentHead", "segmentTail", "activeHead", "activeTail");
                        var head = Resolve(entity, "head", true); var tail = Resolve(entity, "tail", true);
                        var a = Resolve(entity, "segmentHead", true); var b = Resolve(entity, "segmentTail", true);
                        if (!_owners.TryGetValue(head, out var owner) || owner != head
                            || !_owners.TryGetValue(tail, out var tailOwner) || tailOwner != head
                            || !_owners.TryGetValue(a, out var aOwner) || aOwner != head
                            || !_owners.TryGetValue(b, out var bOwner) || bOwner != head
                            || Resolve(a, "next", true) != b || !Kind(tail).StartsWith("Tail", StringComparison.Ordinal))
                            throw Unsupported("独立或装饰 Connector");
                        if (Resolve(entity, "activeHead", true) != head || Resolve(entity, "activeTail", true) != tail)
                            throw Unsupported("Connector 的独立判定区间");
                        if (!segments.Add(a)) throw Unsupported("重复的长条连接片段");
                    }
                    else if (entity.Archetype == "SimLine")
                    {
                        Fields(entity, "left", "right");
                        var a = Resolve(entity, "left", true); var b = Resolve(entity, "right", true);
                        if (!_notes.ContainsKey(a) || !_notes.ContainsKey(b) || Beat(a) != Beat(b)) throw Invalid("SimLine 引用无效");
                    }
                }
                foreach (var entity in _owners.Keys)
                    if (entity.Has("next") && !segments.Contains(entity)) throw Unsupported("缺少 Connector 的长条片段");
            }

            private NextRushEntity Resolve(NextRushEntity entity, string field, bool required)
            {
                string name = entity.Reference(field);
                if (name == null && !required) return null;
                if (name == null || !_source.NamedEntities.TryGetValue(name, out var result)) throw Invalid("找不到实体引用 " + field + ": " + name);
                return result;
            }
            private float Offset(NoteCategory category) => LiveUtility.GetLaneOffset(category, _config);
            private void SetGeometry(NoteBase note, float left, float right)
            {
                note.LaneStartF = left; note.LaneEndF = right;
                note.LaneStart = note.DefaultLeftLane = (int)Math.Floor(left);
                note.LaneEnd = note.DefaultRightLane = (int)Math.Ceiling(right);
                JudgeTuning.WidenLaneRange(left, right, Offset(note.Category), out float a, out float b);
                note.JudgeLaneStart = a; note.JudgeLaneEnd = b;
            }
            private static void SetPairs(IEnumerable<NoteBase> roots)
            {
                var notes = roots.SelectMany(n => n.NoteList ?? new List<NoteBase> { n })
                    .Where(n => !RuntimeChartConverter.IsNotPairNoteTarget(n) && n is not ConnectionNote);
                foreach (var group in notes.GroupBy(n => n.MusicScoreInfo.time))
                {
                    var ordered = group.OrderBy(n => n.LaneStartF).ToArray();
                    if (ordered.Length < 2) continue;
                    ordered[0].SetPairNote(ordered[ordered.Length - 1]);
                    ordered[ordered.Length - 1].SetPairNote(ordered[0]);
                }
            }
            private static string Kind(NextRushEntity entity) => NextRushCompatibility.GetNoteKind(entity.Archetype);
            private static NoteType TypeOf(NextRushEntity entity) => entity.Archetype.StartsWith("Critical", StringComparison.Ordinal) ? NoteType.Critical : NoteType.Default;
            private static double Beat(NextRushEntity entity)
            {
                double beat = entity.Number("#BEAT");
                if (!entity.Has("#BEAT") || Math.Abs(beat) > 1000000) throw Invalid("拍数缺失或超出范围");
                return beat;
            }
            private static void Fields(NextRushEntity entity, params string[] allowed)
            {
                foreach (var field in entity.Fields)
                    if (Array.IndexOf(allowed, field) < 0) throw Unsupported(entity.Archetype + "." + field);
            }
        }
        private static InvalidDataException Unsupported(string feature) => new InvalidDataException("NextRUSH+ 暂不支持：" + feature + "。");
        private static InvalidDataException Invalid(string reason) => new InvalidDataException("NextRUSH+ 谱面无效：" + reason + "。");
    }
}
