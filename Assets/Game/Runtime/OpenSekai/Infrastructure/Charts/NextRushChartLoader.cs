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
            "segmentPresentation", "next", "prev", "activeHead", "attachHead", "attachTail", "elevation"
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
            private readonly Dictionary<NextRushEntity, NextRushEntity> _chainEnds = new Dictionary<NextRushEntity, NextRushEntity>();
            private readonly List<NextRushEntity> _hidden = new List<NextRushEntity>();
            private readonly List<(double beat, double time, double bpm)> _bpm = new List<(double, double, double)>();
            private Dictionary<NextRushEntity, NextRushScrollTimeline> _groups;
            private NextRushScrollTimeline _defaultScroll;
            private MusicScoreInfo[] _timing;
            private int _nextId;

            public Builder(NextRushChart source, LiveBundleBuildData config, bool showPair)
            { _source = source; _config = config; _showPair = showPair; }

            public RuntimeChartDefinition Build(NextRushCompatibilityReport compatibility)
            {
                BuildTiming();
                _groups = NextRushScrollCompiler.Compile(_source, TimingAt);
                if (_groups.Count != 0 || _source.Entities.Any(e => e.Archetype == "Connector"))
                    _defaultScroll = new NextRushScrollTimeline(Array.Empty<NextRushScrollTimeline.Change>(), 0);
                var incoming = new HashSet<NextRushEntity>();
                foreach (var entity in _source.Entities)
                    if (IsChainNode(Kind(entity)) && entity.Has("next"))
                        incoming.Add(Resolve(entity, "next", true));
                foreach (var entity in _source.Entities)
                {
                    if (NextRushCompatibility.Classify(entity.Archetype) == NextRushEntityStatus.IgnoredByProject) continue;
                    switch (entity.Archetype)
                    {
                        case "#BPM_CHANGE": case "Initialization": case "#TIMESCALE_GROUP": case "#TIMESCALE_CHANGE": break;
                        case "Connector": case "SimLine": break;
                        case "TransientHiddenTickNote": _hidden.Add(entity); break;
                        default:
                            bool guide = Kind(entity) == "AnchorNote" && entity.Has("next") && !incoming.Contains(entity);
                            _notes.Add(entity, CreateNote(entity, guide));
                            break;
                    }
                }
                var roots = new List<NoteBase>();
                foreach (var entry in _notes)
                {
                    var entity = entry.Key;
                    if (entry.Value is LongNote bar)
                    {
                        if (entity.Has("prev") || entity.Has("activeHead")) throw Unsupported("嵌套的长条起点");
                        BindChain(entity, bar, roots);
                        roots.Add(entry.Value);
                    }
                    else if (!IsChainNode(Kind(entity)))
                        roots.Add(entry.Value);
                }
                foreach (var entry in _notes)
                {
                    if (entry.Key.Number("isAttached") == 1)
                    {
                        BindVisibleAttachment(entry.Key, entry.Value);
                        continue;
                    }
                    bool anchor = Kind(entry.Key) == "AnchorNote";
                    bool member = Kind(entry.Key).StartsWith("Tail", StringComparison.Ordinal) || Kind(entry.Key) == "TickNote"
                        || anchor && (entry.Key.Has("next") || entry.Key.Has("prev") || entry.Key.Has("activeHead"));
                    if (member && !_owners.ContainsKey(entry.Key)) throw Invalid("孤立的长条节点 " + entry.Key.Name);
                    if (!member && !IsChainNode(Kind(entry.Key)) && !_owners.ContainsKey(entry.Key)
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
                var timing = TimingAt(beat);
                return Info(beat, timing.time, timing.bpm);
            }

            private (double time, double bpm) TimingAt(double beat)
            {
                int low = 0, high = _bpm.Count - 1;
                while (low < high)
                {
                    int mid = (low + high + 1) / 2;
                    double at = _bpm[mid].beat;
                    if (at <= beat) low = mid; else high = mid - 1;
                }
                var start = _bpm[low];
                return (start.time + (beat - start.beat) * 60 / start.bpm, start.bpm);
            }

            private static MusicScoreInfo Info(double beat, double time, double bpm)
            {
                if (double.IsNaN(time) || Math.Abs(time) > NextRushChart.MaxAbsoluteTimeSeconds)
                    throw Invalid("谱面时间超出播放范围");
                int bar = (int)Math.Floor(beat / 4);
                return new MusicScoreInfo(bar, (float)(beat / 4 - bar), (float)time, (float)bpm, 4f, 1f, 1f);
            }

            private NoteBase CreateNote(NextRushEntity entity, bool guide)
            {
                Fields(entity, NoteFields.ToArray());
                string kind = Kind(entity);
                var type = TypeOf(entity);
                var group = Resolve(entity, "#TIMESCALE_GROUP", false);
                if (group != null && group.Archetype != "#TIMESCALE_GROUP") throw Invalid("音符引用的变速组无效");
                foreach (string field in new[] { "style", "effectKind", "elevation" })
                    if (entity.Number(field) != 0) throw Unsupported(entity.Archetype + "." + field);
                double layer = entity.Number("segmentLayer");
                if (layer < 0 || layer > 3 || layer % 1 != 0) throw Unsupported("segmentLayer=" + layer);
                double presentation = entity.Number("segmentPresentation");
                if (presentation != 0 && presentation != 1) throw Unsupported("segmentPresentation=" + presentation);
                double through = entity.Number("segmentThroughJudgeLine");
                if (through != 0 && through != 1) throw Invalid("穿判定线标记无效");
                double attachment = entity.Number("isAttached");
                double separator = entity.Number("isSeparator");
                if ((attachment != 0 && attachment != 1) || (separator != 0 && separator != 1))
                    throw Invalid("附着或连接分隔标记无效");
                bool attached = attachment == 1;
                if (!attached && (entity.Has("attachHead") || entity.Has("attachTail")))
                    throw Invalid("没有附着标记的音符包含附着引用");
                double alpha = entity.Number("segmentAlpha", 1);
                if (alpha < 0 || alpha > 1) throw Invalid("长条透明度超出范围");
                double ease = entity.Number("connectorEase", 1);
                if (ease < 0 || ease > 41 || ease % 1 != 0)
                    throw Unsupported("connectorEase=" + ease);
                double segment = entity.Number("segmentKind", type == NoteType.Critical ? 2 : 1);
                if (segment % 1 != 0 || !(segment == 1 || segment == 2 || segment >= 11 && segment <= 18
                    || segment >= 21 && segment <= 28 || segment >= 101 && segment <= 108))
                    throw Unsupported("segmentKind=" + segment);
                if (entity.Number("direction") < 0 || entity.Number("direction") > 2 || entity.Number("direction") % 1 != 0)
                    throw Unsupported("向下划键方向");
                if (!kind.Contains("Flick") && entity.Number("direction") != 0) throw Unsupported("非划键方向");
                double size = entity.Number("size"), lane = entity.Number("lane");
                if (!attached && (!entity.Has("lane") || size < (kind == "AnchorNote" ? 0 : 0.5) || lane - size < -6 || lane + size > 6))
                    throw Unsupported("超出 12 轨或宽度小于一轨的音符");
                if (attached) { lane = 0; size = 0; }
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
                    case "AnchorNote":
                        note = guide
                            ? (NoteBase)new GuideNote(info, id, a, b, NoteCategory.Guide, type, _config, 1)
                            : new HiddenConnectionNote(info, id, a, b, NoteCategory.Hidden, type, 1, Offset(NoteCategory.Hidden));
                        break;
                    default: throw Unsupported(entity.Archetype);
                }
                SetGeometry(note, left, right);
                SetNativeScroll(entity, note, group);
                return note;
            }

            private void SetNativeScroll(NextRushEntity entity, NoteBase note, NextRushEntity group)
            {
                if (_defaultScroll != null)
                {
                    var scroll = group != null && _groups.TryGetValue(group, out var timeline) ? timeline : _defaultScroll;
                    note.SetNextRushScroll(scroll, TimingAt(Beat(entity)).time);
                }
            }

            private void BindChain(NextRushEntity head, LongNote root, List<NoteBase> roots)
            {
                if (head.Name == null) throw Invalid("长条起点缺少 name");
                var current = head;
                _owners.Add(head, head);
                while (true)
                {
                    var next = Resolve(current, "next", current == head || Kind(current) != "AnchorNote");
                    if (next == null) break;
                    if (!_notes.TryGetValue(next, out var note)) throw Invalid("长条 next 未指向音符");
                    if (next.Number("isAttached") == 1) throw Unsupported("附着的连接端点");
                    if (_owners.ContainsKey(next)) throw Invalid("长条连接存在循环或共享节点");
                    if (Beat(next) < Beat(current)) throw Invalid("长条节点时间必须递增");
                    if (next.Has("prev") && Resolve(next, "prev", true) != current) throw Invalid("长条 prev 与 next 不一致");
                    if (next.Has("activeHead") && Resolve(next, "activeHead", true) != head) throw Invalid("长条 activeHead 不一致");
                    _owners.Add(next, head);
                    string kind = Kind(next);
                    bool realTail = kind.StartsWith("Tail", StringComparison.Ordinal);
                    bool independentGuideTail = root is GuideNote
                        && (kind == "TapNote" || kind == "FlickNote" || kind == "TraceNote" || kind == "TraceFlickNote"
                            || realTail);
                    bool isTail = independentGuideTail || realTail || kind == "AnchorNote" && !next.Has("next");
                    if (kind == "TickNote" || kind == "AnchorNote" && !isTail) root.AddConnectionNote(note);
                    else if (isTail)
                    {
                        if (next.Has("next")) throw Invalid("长条尾部仍有 next");
                        if (independentGuideTail)
                        {
                            if (next.Has("activeHead")) throw Unsupported("装饰条尾键的独立判定区间");
                            if (kind == "TailReleaseNote") ((NormalNote)note).SetNextRushIndependentRelease();
                            if (realTail) roots.Add(note);
                            // The endpoint's key has its own input lifecycle. The guide only borrows its geometry.
                            var endpoint = new HiddenConnectionNote(note.MusicScoreInfo, ++_nextId, note.LaneStart,
                                note.LaneEnd, NoteCategory.Hidden, note.Type, 1, Offset(NoteCategory.Hidden));
                            SetGeometry(endpoint, note.LaneStartF, note.LaneEndF);
                            SetNativeScroll(next, endpoint, Resolve(next, "#TIMESCALE_GROUP", false));
                            root.AddNoteListAndSetupChildNote(endpoint);
                        }
                        else
                            root.AddNoteListAndSetupChildNote(note);
                    }
                    else throw Unsupported("长条中间节点 " + next.Archetype);
                    // Legacy parenting promotes a tail to its head's critical type. Native entities
                    // specify their own type and geometry; restore those after binding the graph.
                    note.Type = TypeOf(next);
                    SetGeometry(note, note.LaneStartF, note.LaneEndF);
                    current = next;
                    if (isTail) break;
                }
                _chainEnds.Add(head, current);
            }

            private void BindHiddenTick(NextRushEntity entity)
            {
                Fields(entity, "#BEAT", "isAttached", "attachHead", "attachTail", "activeHead");
                if (entity.Number("isAttached") != 1) throw Unsupported("未附着的隐藏判定点");
                var attachment = ResolveAttachment(entity);
                var root = AttachmentHold(entity, attachment.owner);
                var tick = new LongHoldCombo(At(Beat(entity)), root.Type, ++_nextId, 1, Offset(NoteCategory.Combo));
                root.AddHoldCombo(tick);
                SetAttachmentGeometry(entity, tick, attachment.head, attachment.tail);
            }

            private void BindVisibleAttachment(NextRushEntity entity, NoteBase note)
            {
                string kind = Kind(entity);
                if (note is LongNote || kind.StartsWith("Tail", StringComparison.Ordinal)
                    || entity.Has("next") || entity.Has("prev") || entity.Number("isSeparator") != 0)
                    throw Unsupported("附着的连接端点或分隔点");
                var attachment = ResolveAttachment(entity);
                if (kind == "TickNote")
                {
                    AttachmentHold(entity, attachment.owner).AddConnectionNote(note, shapesLine: false);
                    note.Type = TypeOf(entity);
                }
                else if (entity.Has("activeHead"))
                    throw Unsupported("独立附着音符的判定区间");
                SetAttachmentGeometry(entity, note, attachment.head, attachment.tail);
                SetNativeScroll(entity, note, Resolve(entity, "#TIMESCALE_GROUP", false));
                note.SetNextRushAttachment(_notes[attachment.head], _notes[attachment.tail]);
            }

            private (NextRushEntity head, NextRushEntity tail, NextRushEntity owner) ResolveAttachment(NextRushEntity entity)
            {
                var head = Resolve(entity, "attachHead", true);
                var tail = Resolve(entity, "attachTail", true);
                if (!_owners.TryGetValue(head, out var owner) || !_owners.TryGetValue(tail, out var tailOwner)
                    || owner != tailOwner || head.Number("isAttached") != 0 || tail.Number("isAttached") != 0
                    || (head != tail && Resolve(head, "next", false) != tail)
                    || Beat(entity) < Beat(head) || Beat(entity) > Beat(tail))
                    throw Invalid("音符的附着连接无效");
                return (head, tail, owner);
            }

            private LongNote AttachmentHold(NextRushEntity entity, NextRushEntity owner)
            {
                var root = (LongNote)_notes[owner];
                var active = Resolve(entity, "activeHead", false);
                if (root is GuideNote || (active != null && active != owner))
                    throw Unsupported("附着判定点的独立判定区间");
                return root;
            }

            private void SetAttachmentGeometry(NextRushEntity entity, NoteBase note, NextRushEntity head, NextRushEntity tail)
            {
                var a = _notes[head]; var b = _notes[tail];
                var timing = TimingAt(Beat(entity)).time;
                var start = TimingAt(Beat(head)).time;
                var end = TimingAt(Beat(tail)).time;
                float progress = NextRushConnectorSegment.Interpolate((int)head.Number("connectorEase", 1),
                    start == end ? 0.5f : (float)((timing - start) / (end - start)));
                SetGeometry(note, a.LaneStartF + (b.LaneStartF - a.LaneStartF) * progress,
                    a.LaneEndF + (b.LaneEndF - a.LaneEndF) * progress);
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
                        var activeHead = Resolve(entity, "activeHead", false);
                        var activeTail = Resolve(entity, "activeTail", false);
                        // Preserve the initial native loader's accepted layout, where the active span
                        // was repeated in head/tail and segmentHead/segmentTail described the edge.
                        if (head == activeHead && tail == activeTail && Resolve(head, "next", false) != tail
                            && Resolve(a, "next", false) == b)
                        {
                            head = a;
                            tail = b;
                        }
                        if (!_owners.TryGetValue(head, out var owner) || !_owners.TryGetValue(tail, out var tailOwner)
                            || owner != tailOwner || Resolve(head, "next", true) != tail)
                            throw Invalid("Connector 的几何端点与 next 不一致");
                        if (!_notes.TryGetValue(a, out var styleHead) || !_notes.ContainsKey(b)
                            || Beat(a) > Beat(head) || Beat(b) < Beat(tail))
                            throw Invalid("Connector 的样式区间没有覆盖连接");
                        bool guide = _notes[owner] is GuideNote;
                        if (guide ? activeHead != null || activeTail != null
                            : activeHead != owner || activeTail != _chainEnds[owner])
                            throw Unsupported("Connector 的独立判定区间");
                        if (!segments.Add(head)) throw Invalid("重复的长条连接片段");
                        double defaultKind = guide ? 101 : TypeOf(a) == NoteType.Critical ? 2 : 1;
                        _notes[head].NativeConnector = new NextRushConnectorSegment(
                            (int)a.Number("segmentKind", defaultKind), (int)head.Number("connectorEase", 1),
                            AlphaAt(head, a, b), AlphaAt(tail, a, b), styleHead, a.Number("segmentThroughJudgeLine") == 1,
                            (NextRushConnectorLayer)(int)a.Number("segmentLayer"), a.Number("segmentPresentation") == 1);
                    }
                    else if (entity.Archetype == "SimLine")
                    {
                        Fields(entity, "left", "right");
                        var a = Resolve(entity, "left", true); var b = Resolve(entity, "right", true);
                        if (!_notes.TryGetValue(a, out var left) || !left.HasJudgment
                            || !_notes.TryGetValue(b, out var right) || !right.HasJudgment
                            || Beat(a) != Beat(b)) throw Invalid("SimLine 引用无效");
                    }
                }
                foreach (var entity in _owners.Keys)
                    if (entity.Has("next") && !segments.Contains(entity)) throw Unsupported("缺少 Connector 的长条片段");
            }

            private float AlphaAt(NextRushEntity point, NextRushEntity start, NextRushEntity end)
            {
                double first = TimingAt(Beat(start)).time, last = TimingAt(Beat(end)).time;
                double progress = point == start ? 0 : point == end ? 1 : first == last ? 0.5
                    : Math.Clamp((TimingAt(Beat(point)).time - first) / (last - first), 0, 1);
                double alpha = start.Number("segmentAlpha", 1);
                return (float)(alpha + (end.Number("segmentAlpha", 1) - alpha) * progress);
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
                note.LaneStart = note.DefaultLeftLane = (int)Math.Floor(left);
                note.LaneEnd = note.DefaultRightLane = (int)Math.Ceiling(right);
                note.LaneStartF = left; note.LaneEndF = right;
                JudgeTuning.WidenLaneRange(left, right, Offset(note.Category), out float a, out float b);
                note.JudgeLaneStart = a; note.JudgeLaneEnd = b;
            }
            private static void SetPairs(IEnumerable<NoteBase> roots)
            {
                var notes = roots.SelectMany(n => n.NoteList ?? new List<NoteBase> { n })
                    .Where(n => n.HasJudgment && !RuntimeChartConverter.IsNotPairNoteTarget(n) && n is not ConnectionNote);
                foreach (var group in notes.GroupBy(n => n.MusicScoreInfo.time))
                {
                    var ordered = group.OrderBy(n => n.LaneStartF).ToArray();
                    if (ordered.Length < 2) continue;
                    ordered[0].SetPairNote(ordered[ordered.Length - 1]);
                    ordered[ordered.Length - 1].SetPairNote(ordered[0]);
                }
            }
            private static string Kind(NextRushEntity entity) => entity.Archetype == "AnchorNote" ? "AnchorNote"
                : NextRushCompatibility.GetNoteKind(entity.Archetype);
            private static bool IsChainNode(string kind) => kind == "TickNote" || kind == "AnchorNote"
                || kind != null && (kind.StartsWith("Head", StringComparison.Ordinal) || kind.StartsWith("Tail", StringComparison.Ordinal));
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
