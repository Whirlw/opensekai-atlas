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
        private const double MaxAnchorEdge = 4096;
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
            private readonly HashSet<NextRushEntity> _geometryNodes = new HashSet<NextRushEntity>();
            private readonly Dictionary<NextRushEntity, ConnectorEdge> _edges = new Dictionary<NextRushEntity, ConnectorEdge>();
            private readonly Dictionary<(NextRushEntity head, NextRushEntity tail), NextRushConnectorSegment> _crossChainGuides =
                new Dictionary<(NextRushEntity head, NextRushEntity tail), NextRushConnectorSegment>();
            private readonly HashSet<NextRushEntity> _crossChainPoints = new HashSet<NextRushEntity>();
            private readonly Dictionary<NextRushEntity, Attachment> _attachments = new Dictionary<NextRushEntity, Attachment>();
            private readonly Dictionary<NextRushEntity, List<NextRushEntity>> _attachmentSlices =
                new Dictionary<NextRushEntity, List<NextRushEntity>>();
            private readonly Dictionary<NextRushEntity, LongNote> _activeSections = new Dictionary<NextRushEntity, LongNote>();
            private readonly Dictionary<NextRushEntity, LongNote> _outgoingBars = new Dictionary<NextRushEntity, LongNote>();
            private readonly Dictionary<NextRushEntity, LongNote> _incomingBars = new Dictionary<NextRushEntity, LongNote>();
            private readonly HashSet<NextRushEntity> _endpointTicks = new HashSet<NextRushEntity>();
            private readonly List<NextRushEntity> _hidden = new List<NextRushEntity>();
            private readonly List<(double beat, double time, double bpm)> _bpm = new List<(double, double, double)>();
            private Dictionary<NextRushEntity, NextRushScrollTimeline> _groups;
            private NextRushScrollTimeline _defaultScroll;
            private MusicScoreInfo[] _timing;
            private int _nextId;

            private readonly struct ConnectorEdge
            {
                internal readonly NextRushEntity tail, activeHead, activeTail;
                internal readonly NextRushConnectorSegment segment;

                internal ConnectorEdge(NextRushEntity tail, NextRushEntity activeHead, NextRushEntity activeTail,
                    NextRushConnectorSegment segment)
                {
                    this.tail = tail;
                    this.activeHead = activeHead;
                    this.activeTail = activeTail;
                    this.segment = segment;
                }
            }

            private readonly struct Attachment
            {
                internal readonly NextRushEntity head, tail;
                internal readonly float fraction;
                internal readonly int ease;

                internal Attachment(NextRushEntity head, NextRushEntity tail, float fraction, int ease)
                {
                    this.head = head;
                    this.tail = tail;
                    this.fraction = fraction;
                    this.ease = ease;
                }
            }

            public Builder(NextRushChart source, LiveBundleBuildData config, bool showPair)
            { _source = source; _config = config; _showPair = showPair; }

            public RuntimeChartDefinition Build(NextRushCompatibilityReport compatibility)
            {
                BuildTiming();
                _groups = NextRushScrollCompiler.Compile(_source, TimingAt);
                if (_groups.Count != 0 || _source.Entities.Any(e => e.Archetype == "Connector"))
                    _defaultScroll = new NextRushScrollTimeline(Array.Empty<NextRushScrollTimeline.Change>(), 0);
                foreach (var entity in _source.Entities)
                {
                    if (NextRushCompatibility.Classify(entity.Archetype) == NextRushEntityStatus.IgnoredByProject) continue;
                    switch (entity.Archetype)
                    {
                        case "#BPM_CHANGE": case "Initialization": case "#TIMESCALE_GROUP": case "#TIMESCALE_CHANGE": break;
                        case "Connector": case "SimLine": break;
                        case "TransientHiddenTickNote": _hidden.Add(entity); break;
                        default: _notes.Add(entity, CreateNote(entity)); break;
                    }
                }
                foreach (var entry in _notes)
                {
                    if (entry.Key.Has("next") && !_notes.ContainsKey(Resolve(entry.Key, "next", true)))
                        throw Invalid("长条 next 未指向音符");
                    if (entry.Key.Number("isAttached") != 1) continue;
                    if (entry.Key.Has("next") || entry.Key.Has("prev")) throw Unsupported("附着节点包含 next 或 prev");
                    var attachment = ResolveAttachment(entry.Key);
                    _attachments.Add(entry.Key, attachment);
                    SetAttachmentGeometry(entry.Value, attachment);
                    SetNativeScroll(entry.Key, entry.Value, Resolve(entry.Key, "#TIMESCALE_GROUP", false));
                    entry.Value.SetNextRushAttachment(_notes[attachment.head], _notes[attachment.tail]);
                }
                ReadConnectors();
                var incoming = new HashSet<NextRushEntity>();
                foreach (var edge in _edges.Values)
                    if (!incoming.Add(edge.tail)) throw Invalid("长条连接存在共享节点");
                var roots = new List<NoteBase>();
                foreach (var entry in _notes)
                {
                    var entity = entry.Key;
                    string kind = Kind(entity);
                    if (_edges.ContainsKey(entity) && !incoming.Contains(entity))
                        BindChain(entity, roots);
                    if (!IsChainNode(kind) || kind is "HeadTraceNote" or "HeadReleaseNote" or "TailTapNote")
                        roots.Add(entry.Value);
                }
                if (_outgoingBars.Count != _edges.Count) throw Invalid("长条连接存在循环");
                BindCrossChainGuides(roots);
                foreach (var entry in _notes)
                {
                    bool onPath = _geometryNodes.Contains(entry.Key);
                    if (_attachments.TryGetValue(entry.Key, out var attachment))
                    {
                        RequireAttachmentPath(attachment);
                        if (!onPath)
                        {
                            BindIndependentAttachment(entry.Key, entry.Value, attachment);
                            continue;
                        }
                    }
                    string kind = Kind(entry.Key);
                    bool anchor = kind == "AnchorNote";
                    bool tail = IsBoundTailKind(kind);
                    bool member = IsHoldHead(kind) || tail || kind == "TickNote"
                        || anchor && (entry.Key.Has("next") || entry.Key.Has("prev") || entry.Key.Has("activeHead"));
                    bool endpointTick = _endpointTicks.Contains(entry.Key);
                    bool independentBridgeTail = tail && !onPath && _crossChainPoints.Contains(entry.Key);
                    bool standaloneTick = kind == "TickNote" && !onPath && !endpointTick
                        && !entry.Key.Has("next") && !entry.Key.Has("prev") && !entry.Key.Has("activeHead");
                    if (member && !onPath && !endpointTick && !independentBridgeTail && !standaloneTick)
                        throw Invalid("孤立的长条节点 " + entry.Key.Name);
                    if (entry.Key.Has("next") && !onPath) throw Unsupported("缺少 Connector 的长条片段");
                    _incomingBars.TryGetValue(entry.Key, out var before);
                    _outgoingBars.TryGetValue(entry.Key, out var after);
                    LongNote activeSection = null;
                    if ((onPath || endpointTick || independentBridgeTail) && entry.Key.Has("activeHead"))
                    {
                        var active = Resolve(entry.Key, "activeHead", true);
                        if (!_activeSections.TryGetValue(active, out activeSection)
                            || activeSection != before && activeSection != after)
                            throw Invalid("长条 activeHead 与相邻判定区间不一致");
                    }
                    if (endpointTick)
                    {
                        var tick = (ConnectionNote)entry.Value;
                        tick.SetNextRushEndpointSection(activeSection
                            ?? (after != null && after is not GuideNote ? after : before is GuideNote ? null : before));
                        roots.Add(tick);
                    }
                    if (standaloneTick)
                    {
                        ((ConnectionNote)entry.Value).SetNextRushContactWindow();
                        roots.Add(entry.Value);
                    }
                    if (independentBridgeTail)
                        BindIndependentTail(entry.Key, roots);
                    if (!member && !onPath
                        && (entry.Key.Has("next") || entry.Key.Has("prev") || entry.Key.Has("activeHead")))
                        throw Unsupported("独立音符的长条连接");
                }
                ValidateSimLines();
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

            private NoteBase CreateNote(NextRushEntity entity)
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
                if (alpha < 0 || alpha > float.MaxValue) throw Invalid("长条透明度超出范围");
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
                if (!attached)
                {
                    if (!entity.Has("lane") || size < 0) throw Invalid("音符位置或宽度无效");
                    if (kind == "AnchorNote")
                    {
                        if (Math.Abs(lane) + size > MaxAnchorEdge) throw Unsupported("Anchor 超出几何范围");
                    }
                    else if (size < 0.5 || Math.Abs(lane) + size > 6)
                        throw Unsupported("超出 12 轨或宽度小于一轨的音符");
                }
                if (attached) { lane = 0; size = 0; }
                float left = (float)(lane - size + 6), right = (float)(lane + size + 5);
                var info = At(Beat(entity));
                int id = ++_nextId, a = (int)Math.Floor(left), b = (int)Math.Ceiling(right);
                NoteBase note;
                var direction = (NoteDirection)(int)entity.Number("direction");
                float flick = LiveUtility.ScreenDpiToInch(_config.FlickDistance);
                switch (kind)
                {
                    case "TapNote": case "TailTapNote": case "TailReleaseNote":
                        note = new NormalNote(info, id, a, b, NoteCategory.Normal, 1, type, Offset(NoteCategory.Normal)); break;
                    case "ReleaseNote": case "HeadReleaseNote":
                        var release = new NormalNote(info, id, a, b, NoteCategory.Long, 1, type, Offset(NoteCategory.Long));
                        release.SetNextRushIndependentRelease();
                        note = release;
                        break;
                    case "FlickNote": case "TailFlickNote":
                        note = new FlickNote(info, id, a, b, NoteCategory.Flick, 1, type, direction, Offset(NoteCategory.Flick), flick); break;
                    case "TraceNote": case "TailTraceNote": case "HeadTraceNote":
                        note = new FrictionNote(info, id, a, b, NoteCategory.Friction, _config, type, 1, NoteLineType.Linear); break;
                    case "TraceFlickNote": case "TailTraceFlickNote":
                        note = new FrictionFlickNote(info, id, a, b, NoteCategory.FrictionFlick, 1, type, direction, flick, Offset(NoteCategory.FrictionFlick)); break;
                    case "HeadTapNote":
                        note = new LongNote(info, id, a, b, NoteCategory.Long, type, 1, Offset(NoteCategory.Long)); break;
                    case "TickNote":
                        note = new ConnectionNote(info, id, a, b, NoteCategory.Connection, type, 1, Offset(NoteCategory.Connection)); break;
                    case "AnchorNote":
                        note = new HiddenConnectionNote(info, id, a, b, NoteCategory.Hidden, type, 1, Offset(NoteCategory.Hidden));
                        break;
                    default: throw Unsupported(entity.Archetype);
                }
                if (kind == "HeadTraceNote") ((FrictionNote)note).SetNextRushContactWindow();
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

            private void BindChain(NextRushEntity head, List<NoteBase> roots)
            {
                _geometryNodes.Add(head);
                NextRushEntity lastUnattached = null;
                CheckNextLink(head, ref lastUnattached);
                var current = head;
                while (_edges.TryGetValue(current, out var first))
                {
                    var start = current;
                    var bar = CreateSection(start, first.activeHead);
                    if (first.activeHead != null) _activeSections.Add(start, bar);
                    roots.Add(bar);

                    var point = (NoteBase)bar;
                    var edge = first;
                    while (true)
                    {
                        var next = edge.tail;
                        if (!_geometryNodes.Add(next)) throw Invalid("长条连接存在循环或共享节点");
                        CheckNextLink(next, ref lastUnattached,
                            allowChainBreak: edge.activeHead != null && current == lastUnattached);
                        _outgoingBars.Add(current, bar);
                        _incomingBars.Add(next, bar);
                        point.NativeConnector = edge.segment;

                        bool continues = _edges.TryGetValue(next, out var following);
                        bool sameSection = continues && edge.activeHead == following.activeHead && edge.activeTail == following.activeTail;
                        if (!sameSection)
                        {
                            if (first.activeTail != null && first.activeTail != next)
                                throw Unsupported("Connector 的判定区间须与连续几何片段对齐");
                            BindSectionTail(bar, next, continues ? following.activeHead : null, roots);
                            current = next;
                            break;
                        }

                        string kind = Kind(next);
                        if (kind == "TickNote" && bar is GuideNote)
                        {
                            _endpointTicks.Add(next);
                            point = BorrowEndpoint(next);
                        }
                        else if (kind == "TickNote" || kind == "AnchorNote")
                        {
                            point = _notes[next];
                        }
                        else if (!IsChainNode(kind))
                        {
                            point = BorrowEndpoint(next);
                        }
                        else throw Unsupported("长条中间节点 " + next.Archetype);
                        bar.AddConnectionNote(point);
                        RestoreNode(next, point);
                        current = next;
                        edge = following;
                    }
                }
                if (lastUnattached != null && lastUnattached.Has("next"))
                    throw Unsupported("缺少 Connector 的长条片段");
            }

            private void CheckNextLink(NextRushEntity point, ref NextRushEntity previous, bool allowChainBreak = false)
            {
                // LevelData keeps attached separators out of the next/prev chain.
                if (_attachments.ContainsKey(point)) return;
                var declaredNext = previous != null ? Resolve(previous, "next", false) : null;
                bool connectsChainEnds = allowChainBreak && declaredNext == null && !point.Has("prev");
                if ((!connectsChainEnds && previous != null && declaredNext != point)
                    || (point.Has("prev") && Resolve(point, "prev", true) != previous))
                    throw Invalid("Connector 的未附着节点与 next/prev 不一致");
                previous = point;
            }

            private LongNote CreateSection(NextRushEntity start, NextRushEntity activeHead)
            {
                var source = _notes[start];
                string kind = Kind(start);
                if (kind == "TickNote") _endpointTicks.Add(start);
                if (activeHead != null)
                {
                    if (activeHead != start) throw Unsupported("Connector 的判定区间须与连续几何片段对齐");
                    if (source is LongNote scoredHead) return scoredHead;
                    if (kind is not ("AnchorNote" or "TickNote" or "HeadTraceNote" or "HeadReleaseNote"))
                        throw Unsupported("长条判定起点 " + start.Archetype);
                    var carrier = new FrictionHideLongNote(source.MusicScoreInfo, ++_nextId,
                        source.LaneStart, source.LaneEnd, NoteCategory.FrictionHideLong, source.Type, _config, 1);
                    if (source is FrictionNote traceHead) carrier.SetNextRushTraceHead(traceHead);
                    return CopyGeometry(start, carrier);
                }
                if (IsHoldHead(kind)) throw Unsupported("长条头缺少从该节点开始的判定区间");
                return CreateGuide(start);
            }

            private GuideNote CreateGuide(NextRushEntity start)
            {
                var source = _notes[start];
                return CopyGeometry(start, new GuideNote(source.MusicScoreInfo, ++_nextId, source.LaneStart,
                    source.LaneEnd, NoteCategory.Guide, source.Type, _config, 1));
            }

            private void BindCrossChainGuides(List<NoteBase> roots)
            {
                if (_crossChainGuides.Count == 0) return;
                ValidateSameBeatGeometry();
                foreach (var entry in _crossChainGuides)
                {
                    var (head, tail) = entry.Key;
                    _crossChainPoints.Add(head);
                    _crossChainPoints.Add(tail);
                    if (Kind(head) == "TickNote" && !_geometryNodes.Contains(head)) _endpointTicks.Add(head);
                    if (Kind(tail) == "TickNote" && !_geometryNodes.Contains(tail)) _endpointTicks.Add(tail);
                    var guide = CreateGuide(head);
                    guide.NativeConnector = entry.Value;
                    guide.AddNoteListAndSetupChildNote(BorrowEndpoint(tail));
                    roots.Add(guide);
                }
            }

            private void ValidateSameBeatGeometry()
            {
                // A cycle can only use same-beat edges, because all Connector edges move forward in beat.
                var outgoing = new Dictionary<NextRushEntity, List<NextRushEntity>>();
                var incoming = new Dictionary<NextRushEntity, int>();
                foreach (var entry in _edges) AddSameBeat(entry.Key, entry.Value.tail);
                foreach (var (head, tail) in _crossChainGuides.Keys) AddSameBeat(head, tail);
                var ready = new Queue<NextRushEntity>(outgoing.Keys.Where(point => !incoming.ContainsKey(point)));
                while (ready.Count > 0)
                {
                    if (!outgoing.TryGetValue(ready.Dequeue(), out var edges)) continue;
                    foreach (var tail in edges)
                    {
                        int remaining = --incoming[tail];
                        if (remaining == 0)
                        {
                            incoming.Remove(tail);
                            ready.Enqueue(tail);
                        }
                    }
                }
                if (incoming.Count != 0) throw Invalid("跨链 Connector 存在同拍循环");

                void AddSameBeat(NextRushEntity head, NextRushEntity tail)
                {
                    if (Beat(head) != Beat(tail)) return;
                    if (!outgoing.TryGetValue(head, out var edges))
                        outgoing.Add(head, edges = new List<NextRushEntity>());
                    edges.Add(tail);
                    incoming.TryGetValue(tail, out int count);
                    incoming[tail] = count + 1;
                }
            }

            private void BindSectionTail(LongNote bar, NextRushEntity end, NextRushEntity nextActiveHead, List<NoteBase> roots)
            {
                var note = _notes[end];
                string kind = Kind(end);
                if (kind == "TickNote") _endpointTicks.Add(end);
                bool realTail = IsBoundTailKind(kind);
                if (realTail && bar is not GuideNote)
                {
                    bar.AddNoteListAndSetupChildNote(note);
                    RestoreNode(end, note);
                    return;
                }
                if (IsHoldHead(kind) && nextActiveHead != end)
                    throw Unsupported("长条头缺少从该节点开始的判定区间");
                if (realTail) BindIndependentTail(end, roots);
                if (kind == "AnchorNote" && !_edges.ContainsKey(end))
                {
                    bar.AddNoteListAndSetupChildNote(note);
                    RestoreNode(end, note);
                    return;
                }

                // Adjacent sections share geometry but never share keys or their runtime state.
                bar.AddNoteListAndSetupChildNote(BorrowEndpoint(end));
            }

            private void BindIndependentTail(NextRushEntity entity, List<NoteBase> roots)
            {
                var note = _notes[entity];
                if (Kind(entity) == "TailReleaseNote") ((NormalNote)note).SetNextRushIndependentRelease();
                RestoreNode(entity, note);
                roots.Add(note);
            }

            private HiddenConnectionNote BorrowEndpoint(NextRushEntity entity)
            {
                var note = _notes[entity];
                return CopyGeometry(entity, new HiddenConnectionNote(note.MusicScoreInfo, ++_nextId,
                    note.LaneStart, note.LaneEnd, NoteCategory.Hidden, note.Type, 1, Offset(NoteCategory.Hidden)));
            }

            private T CopyGeometry<T>(NextRushEntity entity, T copy) where T : NoteBase
            {
                var note = _notes[entity];
                SetGeometry(copy, note.LaneStartF, note.LaneEndF);
                SetNativeScroll(entity, copy, Resolve(entity, "#TIMESCALE_GROUP", false));
                if (_attachments.TryGetValue(entity, out var attachment))
                    copy.SetNextRushAttachment(_notes[attachment.head], _notes[attachment.tail]);
                return copy;
            }

            private void RestoreNode(NextRushEntity entity, NoteBase note)
            {
                // Legacy binding may promote a tail to Critical. Native entities declare their own type.
                note.Type = TypeOf(entity);
                SetGeometry(note, note.LaneStartF, note.LaneEndF);
            }

            private void BindHiddenTick(NextRushEntity entity)
            {
                Fields(entity, "#BEAT", "isAttached", "attachHead", "attachTail", "activeHead");
                if (entity.Number("isAttached") != 1) throw Unsupported("未附着的隐藏判定点");
                var attachment = ResolveAttachment(entity);
                RequireAttachmentPath(attachment);
                var root = AttachmentHold(entity, attachment.head, attachment.tail);
                var tick = new LongHoldCombo(At(Beat(entity)), root.Type, ++_nextId, 1, Offset(NoteCategory.Combo));
                root.AddHoldCombo(tick);
                SetAttachmentGeometry(tick, attachment);
            }

            private void BindIndependentAttachment(NextRushEntity entity, NoteBase note, Attachment attachment)
            {
                string kind = Kind(entity);
                if (IsHoldHead(kind) || IsBoundTailKind(kind))
                    throw Invalid("孤立的附着长条节点");
                if (kind == "TickNote")
                {
                    AttachmentHold(entity, attachment.head, attachment.tail).AddConnectionNote(note, shapesLine: false);
                    note.Type = TypeOf(entity);
                }
                else if (entity.Has("activeHead"))
                    throw Unsupported("独立附着音符的判定区间");
            }

            private Attachment ResolveAttachment(NextRushEntity entity)
            {
                var head = Resolve(entity, "attachHead", true);
                var tail = Resolve(entity, "attachTail", true);
                if (!_notes.ContainsKey(head) || !_notes.ContainsKey(tail)
                    || head.Number("isAttached") != 0 || tail.Number("isAttached") != 0
                    || (head != tail && Resolve(head, "next", false) != tail)
                    || Beat(entity) < Beat(head) || Beat(entity) > Beat(tail))
                    throw Invalid("音符的附着连接无效");
                double start = TimingAt(Beat(head)).time;
                double end = TimingAt(Beat(tail)).time;
                double time = TimingAt(Beat(entity)).time;
                return new Attachment(head, tail, start == end ? 0.5f : (float)((time - start) / (end - start)),
                    (int)head.Number("connectorEase", 1));
            }

            private void RequireAttachmentPath(Attachment attachment)
            {
                if (attachment.head == attachment.tail && _crossChainPoints.Contains(attachment.head)) return;
                if (!_geometryNodes.Contains(attachment.head) || !_geometryNodes.Contains(attachment.tail))
                    throw Invalid("音符的附着引用不属于连接路径");
            }

            private LongNote AttachmentHold(NextRushEntity entity, NextRushEntity head, NextRushEntity tail)
            {
                var active = Resolve(entity, "activeHead", false);
                _outgoingBars.TryGetValue(head, out var outgoing);
                LongNote incoming = null;
                if (head == tail) _incomingBars.TryGetValue(head, out incoming);
                else if (_edges.TryGetValue(head, out var edge) && edge.tail != tail)
                {
                    var slices = AttachmentSlices(head, tail);
                    double at = Beat(entity);
                    int low = 1;
                    int high = slices.Count - 1;
                    while (low < high)
                    {
                        int middle = (low + high) / 2;
                        if (Beat(slices[middle]) < at) low = middle + 1;
                        else high = middle;
                    }
                    outgoing = _outgoingBars[slices[low - 1]];
                    if (Beat(slices[low]) == at) _outgoingBars.TryGetValue(slices[low], out incoming);
                }
                var root = outgoing ?? incoming;
                if (active != null && (!_activeSections.TryGetValue(active, out root)
                    || root != outgoing && root != incoming))
                    throw Unsupported("附着判定点的独立判定区间");
                if (root == null || root is GuideNote) throw Unsupported("装饰段上的附着判定点");
                return root;
            }

            private List<NextRushEntity> AttachmentSlices(NextRushEntity head, NextRushEntity tail)
            {
                if (_attachmentSlices.TryGetValue(head, out var slices)) return slices;
                slices = new List<NextRushEntity> { head };
                var current = head;
                while (current != tail)
                {
                    current = _edges[current].tail;
                    slices.Add(current);
                }
                _attachmentSlices.Add(head, slices);
                return slices;
            }

            private void SetAttachmentGeometry(NoteBase note, Attachment attachment)
            {
                var a = _notes[attachment.head];
                var b = _notes[attachment.tail];
                float progress = NextRushConnectorSegment.Interpolate(attachment.ease, attachment.fraction);
                float left = a.LaneStartF + (b.LaneStartF - a.LaneStartF) * progress;
                float right = a.LaneEndF + (b.LaneEndF - a.LaneEndF) * progress;
                if (right - left < -1f)
                {
                    // Overshooting a narrowing attachment can collapse its authored half-width to zero.
                    float lane = (left + right + 1f) / 2f;
                    left = lane;
                    right = lane - 1f;
                }
                SetGeometry(note, left, right);
            }

            private void ReadConnectors()
            {
                foreach (var entity in _source.Entities)
                {
                    if (entity.Archetype != "Connector") continue;
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
                    if (!_notes.ContainsKey(head) || !_notes.ContainsKey(tail) || head == tail || Beat(tail) < Beat(head))
                        throw Invalid("Connector 的几何端点无效");
                    if (!_notes.TryGetValue(a, out var styleHead) || !_notes.ContainsKey(b)
                        || Beat(a) > Beat(head) || Beat(b) < Beat(tail))
                        throw Invalid("Connector 的样式区间没有覆盖连接");
                    if ((activeHead == null) != (activeTail == null)
                        || activeHead != null && (!_notes.ContainsKey(activeHead) || !_notes.ContainsKey(activeTail)
                            || Beat(activeHead) > Beat(head) || Beat(activeTail) < Beat(tail)))
                        throw Invalid("Connector 的判定区间没有覆盖连接");
                    double defaultKind = activeHead == null ? 101 : TypeOf(a) == NoteType.Critical ? 2 : 1;
                    bool headAttached = _attachments.TryGetValue(head, out var headAttachment);
                    bool tailAttached = _attachments.TryGetValue(tail, out var tailAttachment);
                    bool sameBeat = Beat(head) == Beat(tail);
                    var segment = new NextRushConnectorSegment(
                        (int)a.Number("segmentKind", defaultKind),
                        headAttached ? headAttachment.ease : (int)head.Number("connectorEase", 1),
                        AlphaAt(head, a, b), AlphaAt(tail, a, b), styleHead, a.Number("segmentThroughJudgeLine") == 1,
                        (NextRushConnectorLayer)(int)a.Number("segmentLayer"), a.Number("segmentPresentation") == 1,
                        headAttached && !sameBeat ? headAttachment.fraction : 0f,
                        tailAttached && !sameBeat ? tailAttachment.fraction : 1f);
                    if (activeHead == null && !headAttached && !tailAttached && Resolve(head, "next", false) != tail)
                    {
                        if (!_crossChainGuides.TryAdd((head, tail), segment))
                            throw Invalid("重复的跨链 Connector");
                    }
                    else if (!_edges.TryAdd(head, new ConnectorEdge(tail, activeHead, activeTail, segment)))
                        throw Invalid("重复的长条连接片段");
                }
            }

            private void ValidateSimLines()
            {
                foreach (var entity in _source.Entities)
                {
                    if (entity.Archetype != "SimLine") continue;
                    Fields(entity, "left", "right");
                    var a = Resolve(entity, "left", true); var b = Resolve(entity, "right", true);
                    if (!_notes.TryGetValue(a, out var left) || !left.HasJudgment
                        || !_notes.TryGetValue(b, out var right) || !right.HasJudgment
                        || Beat(a) != Beat(b)) throw Invalid("SimLine 引用无效");
                }
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
            private static bool IsHoldHead(string kind) => kind.StartsWith("Head", StringComparison.Ordinal);

            // A Tap tail shares the endpoint's geometry but keeps its own press and timeout.
            private static bool IsBoundTailKind(string kind) => kind != "TailTapNote"
                && kind.StartsWith("Tail", StringComparison.Ordinal);

            private static bool IsChainNode(string kind) => kind == "TickNote" || kind == "AnchorNote"
                || IsHoldHead(kind) || kind.StartsWith("Tail", StringComparison.Ordinal);
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
