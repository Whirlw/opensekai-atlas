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
        public const string FORMAT = "next-rush";
        private const int MAX_FILE_BYTES = 32 * 1024 * 1024;
        private static readonly HashSet<string> _noteFields = new HashSet<string>(new[] {
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
            return Read(file);
        }

        internal static NextRushChart Read(Stream file)
        {
            if (file.Length > MAX_FILE_BYTES) throw new InvalidDataException("NextRUSH+ 谱面文件超过大小限制。");
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
                if (text.Length + read > NextRushChart.MAX_JSON_CHARACTERS)
                    throw new InvalidDataException("NextRUSH+ 解压后的谱面超过大小限制。");
                text.Append(buffer, 0, read);
            }
            return NextRushChart.Parse(text.ToString());
        }

        public static RuntimeChartDefinition Load(NextRushChart source, LiveBundleBuildData config, bool showPair = true,
            bool isMirror = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (config == null) throw new ArgumentNullException(nameof(config));
            return Load(source, new ChartConversionSettings(config), showPair, isMirror);
        }

        internal static RuntimeChartDefinition Load(NextRushChart source, ChartConversionSettings config, bool showPair, bool isMirror)
        {
            var compatibility = NextRushCompatibility.Analyze(source);
            if (compatibility.InvalidData.Count > 0) throw Invalid(string.Join("、", compatibility.InvalidData.Take(8)));
            if (compatibility.UnsupportedFeatures.Count > 0) throw Unsupported(string.Join("、", compatibility.UnsupportedFeatures.Take(8)));
            return new Builder(source, config, showPair, isMirror).Build(compatibility);
        }

        private sealed class Builder
        {
            private readonly NextRushChart _source;
            private readonly ChartConversionSettings _config;
            private readonly bool _showPair;
            private readonly bool _isMirror;
            private readonly Dictionary<NextRushEntity, NoteBase> _notes = new Dictionary<NextRushEntity, NoteBase>();
            private readonly Dictionary<NextRushEntity, NoteBase> _standaloneKeys = new Dictionary<NextRushEntity, NoteBase>();
            private Dictionary<NextRushEntity, double> _playableEndBeats;
            private readonly List<(NextRushConnectorTickNote tick, NextRushEntity owner)> _independentTicks = new();
            private readonly List<(NextRushEntity head, NextRushEntity tail, NextRushEntity firstEnd, NextRushEntity style, int kind, bool legacy)> _soundStarts = new();
            private readonly List<(NextRushEntity head, NextRushEntity tail, NextRushEntity owner, NextRushEntity end)> _soundEdges = new();
            private readonly HashSet<NextRushEntity> _geometryNodes = new HashSet<NextRushEntity>();
            private readonly Dictionary<NextRushEntity, ConnectorEdge> _edges = new Dictionary<NextRushEntity, ConnectorEdge>();
            private readonly Dictionary<(NextRushEntity head, NextRushEntity tail), ConnectorEdge> _graphEdges = new();
            private readonly Dictionary<(NextRushEntity head, NextRushEntity tail), LongNote> _graphBars = new();
            private ILookup<NextRushEntity, (NextRushEntity head, NextRushEntity tail)> _junctionEdges;
            private ILookup<NextRushEntity, (NextRushEntity head, NextRushEntity tail)> JunctionEdges =>
                _junctionEdges ??= _graphBars.Keys.SelectMany(pair => new[] { (pair.head, pair), (pair.tail, pair) })
                    .ToLookup(entry => entry.Item1, entry => entry.pair);
            private readonly HashSet<NextRushEntity> _graphNodes = new();
            private readonly HashSet<(NextRushEntity node, NextRushEntity activeHead)> _graphOwners = new();
            private readonly Dictionary<(NextRushEntity head, NextRushEntity tail), NextRushConnectorSegment> _crossChainGuides =
                new Dictionary<(NextRushEntity head, NextRushEntity tail), NextRushConnectorSegment>();
            private readonly HashSet<NextRushEntity> _crossChainPoints = new HashSet<NextRushEntity>();
            private readonly Dictionary<NextRushEntity, Attachment> _attachments = new Dictionary<NextRushEntity, Attachment>();
            private readonly Dictionary<(NextRushEntity point, bool start), NextRushEntity> _pathEndpoints = new();
            private readonly Dictionary<NextRushEntity, (List<NextRushEntity> path, int index)> _linearPositions = new();
            private ILookup<NextRushEntity, NextRushEntity> _attachmentPredecessors;
            private readonly Dictionary<NextRushEntity, LongNote> _activeSections = new Dictionary<NextRushEntity, LongNote>();
            private readonly Dictionary<NextRushEntity, LongNote> _outgoingBars = new Dictionary<NextRushEntity, LongNote>();
            private readonly Dictionary<NextRushEntity, LongNote> _incomingBars = new Dictionary<NextRushEntity, LongNote>();
            private readonly HashSet<NextRushEntity> _endpointTicks = new HashSet<NextRushEntity>();
            private Dictionary<NextRushEntity, NextRushScrollTimeline> _groups;
            private NextRushScrollTimeline _defaultScroll;
            private TimingMap _timing;
            private int _nextId;

            private readonly struct ConnectorEdge
            {
                internal readonly NextRushEntity tail, activeHead, activeTail;
                internal readonly NextRushConnectorSegment segment;
                internal (NextRushEntity head, NextRushEntity tail, bool damage, bool fake) Section =>
                    (activeHead, activeTail, segment.IsDamage, segment.IsFake);

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

            public Builder(NextRushChart source, ChartConversionSettings config, bool showPair, bool isMirror)
            { _source = source; _config = config; _showPair = showPair; _isMirror = isMirror; }

            public RuntimeChartDefinition Build(NextRushCompatibilityReport compatibility)
            {
                _timing = new TimingMap(_source);
                _groups = NextRushScrollCompiler.Compile(_source, _timing.TimingAt);
                if (_groups.Count != 0 || _source.Entities.Any(e => e.Archetype is
                    "Connector" or "DamageNote" or "TransientHiddenDamageTickNote"
                    || e.Archetype == "SimLine" && NeedsSimLineTimeline(e)
                    || e.Archetype.StartsWith("Fake", StringComparison.Ordinal)
                    || e.Archetype.EndsWith("Note", StringComparison.Ordinal) && e.Has("lane")
                        && (e.Number("size") < 0.5 || Math.Abs(e.Number("lane")) + e.Number("size") > 6)))
                    _defaultScroll = new NextRushScrollTimeline(Array.Empty<NextRushScrollTimeline.Change>(), 0);
                var activeHeads = _source.Entities.Where(e => e.Archetype == "Connector")
                    .Select(e => Resolve(e, "activeHead", false)).ToHashSet();
                foreach (var entity in _source.Entities)
                {
                    if (NextRushCompatibility.Classify(entity.Archetype) == NextRushEntityStatus.IgnoredByProject) continue;
                    switch (entity.Archetype)
                    {
                        case "#BPM_CHANGE": case "Initialization": case "#TIMESCALE_GROUP": case "#TIMESCALE_CHANGE": break;
                        case "Connector": case "SimLine": break;
                        default: _notes.Add(entity, CreateNote(entity, activeHead: activeHeads.Contains(entity))); break;
                    }
                }
                BindAttachments();
                ReadConnectors();
                SeparateBranchedSections();
                var incoming = new HashSet<NextRushEntity>();
                foreach (var edge in _edges.Values)
                    if (!incoming.Add(edge.tail)) throw Invalid("长条连接存在共享节点");
                var roots = new List<NoteBase>();
                BindGraphEdges(roots);
                foreach (var entry in _notes)
                {
                    var entity = entry.Key;
                    string kind = Kind(entity);
                    if (_edges.ContainsKey(entity) && !incoming.Contains(entity))
                        BindChain(entity, roots);
                    // Its geometry is ready for connectors. Its judgment needs the completed graph.
                    if (entry.Value is NextRushHiddenTickNote && _attachments.ContainsKey(entity)) continue;
                    if (entry.Value.IsDecoration || !IsChainNode(kind) || IsIndependentHoldHead(kind) || IsStandaloneTail(kind, entry.Value))
                        roots.Add(entry.Value);
                }
                if (_outgoingBars.Count != _edges.Count) throw Invalid("长条连接存在循环");
                BindCrossChainGuides(roots);
                foreach (var entry in _notes)
                {
                    if (entry.Value is NextRushHiddenTickNote) continue;
                    bool onPath = _geometryNodes.Contains(entry.Key);
                    if (entry.Value.IsDecoration && entry.Key.Has("activeHead"))
                    {
                        var start = Resolve(entry.Key, "activeHead", true);
                        if (!_notes.ContainsKey(start) || Beat(start) > Beat(entry.Key))
                            throw Invalid("Fake 音符的 activeHead 无效");
                    }
                    if (_attachments.TryGetValue(entry.Key, out var attachment))
                    {
                        // A scored connector tick needs a hold. Other attachments only borrow geometry.
                        if (entry.Value is NextRushConnectorTickNote) RequireAttachmentPath(attachment);
                        if (!entry.Value.IsDecoration && entry.Value.ParentNote == null && entry.Value is NextRushConnectorTickNote tickNote
                            && TryIndependentTickOwner(entry.Key, attachment, out var owner))
                        {
                            _independentTicks.Add((tickNote, owner));
                            roots.Add(tickNote);
                            continue;
                        }
                        if (!onPath)
                        {
                            if (!entry.Value.IsDecoration) BindIndependentAttachment(entry.Key, entry.Value, attachment, roots);
                            continue;
                        }
                    }
                    string kind = Kind(entry.Key);
                    if (_graphNodes.Contains(entry.Key))
                    {
                        BindGraphNode(entry.Key, roots);
                        continue;
                    }
                    bool anchor = kind == "AnchorNote";
                    bool tail = IsBoundTailKind(kind, entry.Value);
                    bool member = IsHoldHead(kind) || tail || kind == "TickNote"
                        || anchor && (entry.Key.Has("next") || entry.Key.Has("prev") || entry.Key.Has("activeHead"));
                    bool endpointTick = _endpointTicks.Contains(entry.Key);
                    bool independentBridgeTail = tail && !onPath && _crossChainPoints.Contains(entry.Key);
                    bool standaloneTick = kind == "TickNote" && !onPath && !endpointTick
                        && !entry.Key.Has("next") && !entry.Key.Has("prev") && !entry.Key.Has("activeHead");
                    if (!entry.Value.IsDecoration && member && !onPath && !endpointTick && !independentBridgeTail && !standaloneTick)
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
                    if (entry.Value.IsDecoration) continue;
                    if (endpointTick)
                    {
                        var tick = (ConnectionNote)entry.Value;
                        tick.SetNextRushEndpointSection(activeSection
                            ?? (IsTickHold(after) ? after : IsTickHold(before) ? before : null));
                        roots.Add(tick);
                    }
                    if (standaloneTick) roots.Add(entry.Value);
                    if (independentBridgeTail)
                        BindIndependentTail(entry.Key, roots);
                    if (kind != "TransientHiddenDamageTickNote" && !member && !onPath
                        && (entry.Key.Has("next") || entry.Key.Has("prev") || entry.Key.Has("activeHead")))
                        throw Unsupported("独立音符的长条连接");
                }
                foreach (var (entity, note) in _notes)
                {
                    if (note is not NormalNote release || !(release.IsTailReleaseOwner ||
                        Kind(entity) == "ReleaseNote" && _attachments.ContainsKey(entity) && !_geometryNodes.Contains(entity)
                        && !note.IsDecoration)) continue;
                    var prior = Resolve(entity, "activeHead", false);
                    if (prior == null || prior == entity) continue;
                    var owner = _standaloneKeys.TryGetValue(prior, out var key) ? key : _notes[prior];
                    if (owner.HasJudgment) release.SetNextRushIndependentRelease(owner);
                }
                foreach (var (entity, attachment) in _attachments)
                    if (_notes[entity] is NextRushHiddenTickNote)
                        BindAttachedHiddenTick(entity, attachment, roots);
                foreach (var (head, edge) in _edges)
                    BindComboTail(_outgoingBars[head], edge.activeTail);
                foreach (var (endpoints, edge) in _graphEdges)
                    if (_graphBars.TryGetValue(endpoints, out var branch))
                        BindComboTail(branch, edge.activeTail);
                foreach (var entry in _notes)
                    if (entry.Value is NextRushHiddenTickNote hidden && !_attachments.ContainsKey(entry.Key))
                        BindUnattachedHiddenTick(entry.Key, hidden);
                foreach (var (tick, owner) in _independentTicks)
                    tick.SetConnectorOwner(_standaloneKeys.TryGetValue(owner, out var key) ? key : _notes[owner], isAttached: true);
                BindDamageTicks();
                BindConnectorTickInputPaths();
                var hiddenLines = new List<(NoteBase left, NoteBase right)>();
                var authoredPairTimes = BindSimLines(hiddenLines);
                foreach (var root in roots.OfType<LongNote>()) root.SortNoteList();
                if (_showPair) SetPairs(roots, authoredPairTimes);
                foreach (var root in roots)
                    foreach (var note in root.NoteList)
                        note.UsesNextRushJudgment = true;
                var score = new MusicScore(_timing.Points, Array.Empty<EventBase>(),
                    roots.OrderBy(n => n.MusicScoreInfo.time).ToArray(), compatibility.InitialLife, useExtendedLanes: true,
                    connectorSounds: BuildConnectorSounds(), hiddenSimLines: hiddenLines.ToArray(), scoringHoldChains: ScoringHoldChains());
                double lastBeat = _notes.Keys.Select(e => Beat(e)).DefaultIfEmpty(0).Max();
                return RuntimeChartDefinition.CreateNative(_source, score, checked((long)Math.Round(lastBeat * ChartTimeline.TICKS_PER_BEAT)), compatibility);
            }

            // One authored interval can span several runtime bars. A tick may also judge on its own
            // root while borrowing geometry from a different interval.
            private NoteBase[][] ScoringHoldChains()
            {
                var sections = new Dictionary<LongNote, (NextRushEntity head, NextRushEntity tail)>();
                void Register(LongNote bar, ConnectorEdge edge)
                {
                    if (edge.activeHead != null && edge.activeTail != null && bar is not GuideNote &&
                        !edge.segment.IsFake && !edge.segment.IsDamage && edge.segment.Kind < 101)
                        sections[bar] = (edge.activeHead, edge.activeTail);
                }
                foreach (var (entity, edge) in _edges) Register(_outgoingBars[entity], edge);
                foreach (var (pair, edge) in _graphEdges) Register(_graphBars[pair], edge);
                if (sections.Count == 0) return Array.Empty<NoteBase[]>();
                var byHead = sections.Values.Distinct().ToLookup(section => section.head);
                var chains = new Dictionary<(NextRushEntity head, NextRushEntity tail), HashSet<NoteBase>>();
                foreach (var (entity, point) in _notes)
                {
                    if (point is not HoldPointNote || !point.HasJudgment ||
                        Kind(entity) != "TickNote" && entity.Archetype != "TransientHiddenTickNote") continue;
                    void Add((NextRushEntity head, NextRushEntity tail) section)
                    {
                        if (entity == section.head || entity == section.tail || Beat(entity) < Beat(section.head) ||
                            Beat(entity) > Beat(section.tail)) return;
                        if (!chains.TryGetValue(section, out var points)) chains.Add(section, points = new HashSet<NoteBase>());
                        points.Add(point);
                    }

                    var owner = Resolve(entity, "activeHead", false);
                    if (point.ParentNote is LongNote parent && sections.TryGetValue(parent, out var bound))
                        Add(bound);
                    else if (owner != null)
                        foreach (var section in byHead[owner]) Add(section);
                    else if (_incomingBars.TryGetValue(entity, out var incoming) && sections.TryGetValue(incoming, out var preceding))
                        Add(preceding);
                    else if (_graphNodes.Contains(entity))
                        foreach (var edge in JunctionEdges[entity])
                            if (sections.TryGetValue(_graphBars[edge], out var branch)) Add(branch);
                }
                return chains.Values.Select(points => points.ToArray()).ToArray();
            }

            private sealed class TimingMap
            {
                private readonly List<(double beat, double time, double bpm)> _bpm = new List<(double, double, double)>();
                internal MusicScoreInfo[] Points { get; }

                internal TimingMap(NextRushChart source)
                {
                    var changes = source.Entities.Where(e => e.Archetype == "#BPM_CHANGE").OrderBy(Beat).ToArray();
                    int origin = Array.FindLastIndex(changes, entity => Beat(entity) <= 0);
                    if (changes.Length == 0) throw Invalid("谱面缺少 BPM 事件");
                    for (int i = 0; i < changes.Length; i++)
                    {
                        var entity = changes[i];
                        Fields(entity, "#BEAT", "#BPM", "meter");
                        double beat = Beat(entity), nextBpm = entity.Number("#BPM");
                        if (i > 0 && beat == _bpm[i - 1].beat) throw Invalid("同一拍有重复 BPM");
                        if (nextBpm <= 0 || nextBpm > 10000) throw Invalid("BPM 超出有效范围");
                        // The upstream play archetype times beats using BPM alone. Validate
                        // the optional numeric meter metadata without rescaling the beat.
                        entity.Number("meter", 4);
                        _bpm.Add((beat, 0, nextBpm));
                    }
                    // Anchor beat zero before accumulating in either direction. A distant
                    // negative marker must not cost precision on the positive timeline.
                    if (origin < 0)
                    {
                        _bpm.Insert(0, (0, 0, _bpm[0].bpm));
                        origin = 0;
                    }
                    var anchor = _bpm[origin];
                    _bpm[origin] = (anchor.beat, anchor.beat * 60 / anchor.bpm, anchor.bpm);
                    for (int i = origin - 1; i >= 0; i--)
                    {
                        var here = _bpm[i];
                        var next = _bpm[i + 1];
                        _bpm[i] = (here.beat, next.time - (next.beat - here.beat) * 60 / here.bpm, here.bpm);
                    }
                    for (int i = origin + 1; i < _bpm.Count; i++)
                    {
                        var here = _bpm[i];
                        var prev = _bpm[i - 1];
                        _bpm[i] = (here.beat, prev.time + (here.beat - prev.beat) * 60 / prev.bpm, here.bpm);
                    }
                    Points = _bpm.Select(point => Info(point.beat, point.time, point.bpm)).ToArray();
                }

                internal MusicScoreInfo At(double beat)
                {
                    var timing = TimingAt(beat);
                    return Info(beat, timing.time, timing.bpm);
                }

                internal (double time, double bpm) TimingAt(double beat)
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
                    if (double.IsNaN(time) || Math.Abs(time) > NextRushChart.MAX_ABSOLUTE_TIME_SECONDS)
                        throw Invalid("谱面时间超出播放范围");
                    int bar = (int)Math.Floor(beat / 4);
                    return new MusicScoreInfo(bar, (float)(beat / 4 - bar), (float)time, (float)bpm, 4f, 1f, 1f);
                }
            }

            private NoteBase CreateNote(NextRushEntity entity, bool activeHead = false, bool connectorHeadView = false)
            {
                Fields(entity, _noteFields.ToArray());
                string kind = Kind(entity);
                bool fake = entity.Archetype.StartsWith("Fake", StringComparison.Ordinal);
                var type = TypeOf(entity);
                var group = Resolve(entity, "#TIMESCALE_GROUP", false);
                if (group != null && group.Archetype != "#TIMESCALE_GROUP") throw Invalid("音符引用的变速组无效");
                if (entity.Number("elevation") != 0) throw Unsupported(entity.Archetype + ".elevation");
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
                if (segment % 1 != 0 || !(segment >= 0 && segment <= 3 || segment >= 11 && segment <= 18
                    || segment >= 21 && segment <= 28 || segment >= 31 && segment <= 38
                    || segment >= 51 && segment <= 53 || segment >= 61 && segment <= 68
                    || segment >= 71 && segment <= 78 || segment >= 81 && segment <= 88
                    || segment >= 101 && segment <= 108))
                    throw Unsupported("segmentKind=" + segment);
                double rawDirection = entity.Number("direction");
                if (rawDirection < 0 || rawDirection > (int)NoteDirection.DownRight || rawDirection % 1 != 0)
                    throw Unsupported("direction=" + rawDirection);
                var direction = (NoteDirection)(int)rawDirection;
                if (!kind.Contains("Flick") && direction != NoteDirection.Default) throw Unsupported("非划键方向");
                double size = entity.Number("size"), lane = entity.Number("lane");
                if (!attached)
                {
                    if (!entity.Has("lane") && entity.Archetype != "TransientHiddenTickNote" || size < 0)
                        throw Invalid("音符位置或宽度无效");
                }
                if (attached) { lane = 0; size = 0; }
                if (_isMirror)
                {
                    lane = -lane;
                    direction = direction.Mirrored();
                }
                float left = (float)(lane - size + 6), right = (float)(lane + size + 5);
                // Runtime endpoints encode a zero-width key with right == left - 1.
                if (size == 0)
                {
                    if (Math.Abs(right) > Math.Abs(left)) left = right + 1f;
                    else right = left - 1f;
                    if ((double)right - left != -1)
                        throw Unsupported("零宽音符的位置超出轨道坐标精度");
                }
                var info = _timing.At(Beat(entity));
                int id = ++_nextId;
                var (a, b) = LaneIndices(left, right);
                NoteBase note;
                float flick = _config.FlickPixels;
                switch (kind)
                {
                    case "TapNote":
                        note = new NormalNote(info, id, a, b, NoteCategory.Normal, 1, type, Offset(NoteCategory.Normal)); break;
                    case "TailTapNote":
                        note = new NormalNote(info, id, a, b, NoteCategory.Long, 1, type, Offset(NoteCategory.Normal),
                            isNextRushTap: true); break;
                    case "TailReleaseNote" when !activeHead && !connectorHeadView || fake:
                        note = new NormalNote(info, id, a, b, NoteCategory.Long, 1, type, Offset(NoteCategory.Normal)); break;
                    case "ReleaseNote": case "HeadReleaseNote": case "TailReleaseNote":
                        var release = new NormalNote(info, id, a, b, NoteCategory.Long, 1, type, Offset(NoteCategory.Long),
                            isTailReleaseOwner: kind == "TailReleaseNote" && activeHead);
                        release.SetNextRushIndependentRelease();
                        note = release;
                        break;
                    case "TailFlickNote" when activeHead && !fake:
                        note = new FrictionFlickNote(info, id, a, b, NoteCategory.Flick, 1, type, direction,
                            flick, Offset(NoteCategory.Flick), isTraceFlickOwner: true); break;
                    case "FlickNote": case "TailFlickNote": case "HeadFlickNote":
                        note = new FlickNote(info, id, a, b, NoteCategory.Flick, 1, type, direction,
                            Offset(NoteCategory.Flick), flick,
                            isNextRushHead: kind == "HeadFlickNote" || activeHead && !fake && kind == "FlickNote"); break;
                    case "DamageNote":
                        note = new NextRushDamageNote(info, id, a, b); break;
                    case "TransientHiddenDamageTickNote":
                        note = new NextRushDamageNote(info, id, a, b, hidden: true); break;
                    case "TraceNote": case "TailTraceNote": case "HeadTraceNote":
                        note = new FrictionNote(info, id, a, b, NoteCategory.Friction, Offset(NoteCategory.Friction), type, 1,
                            isTailTraceOwner: activeHead && !fake && kind == "TailTraceNote", isTraceTail: kind == "TailTraceNote"); break;
                    case "TraceFlickNote": case "TailTraceFlickNote": case "HeadTraceFlickNote":
                        note = new FrictionFlickNote(info, id, a, b, NoteCategory.FrictionFlick, 1, type, direction,
                            flick, Offset(NoteCategory.FrictionFlick),
                            isTraceFlickOwner: activeHead && !fake && kind == "TraceFlickNote",
                            isTailTraceFlickOwner: activeHead && !fake && kind == "TailTraceFlickNote",
                            isTraceTail: kind == "TailTraceFlickNote"); break;
                    case "HeadTapNote":
                        note = fake
                            ? new NormalNote(info, id, a, b, NoteCategory.Long, 1, type, Offset(NoteCategory.Long))
                            : new LongNote(info, id, a, b, NoteCategory.Long, type, 1, Offset(NoteCategory.Long));
                        break;
                    case "TickNote":
                        var tick = attached && !fake
                            ? new NextRushConnectorTickNote(info, id, a, b, type, Offset(NoteCategory.Connection))
                            : new ConnectionNote(info, id, a, b, NoteCategory.Connection, type, 1, Offset(NoteCategory.Connection));
                        if (!fake && UsesTickContactWindow(entity)) tick.SetNextRushContactWindow();
                        note = tick;
                        break;
                    case "TransientHiddenTickNote" when !fake:
                        note = new NextRushHiddenTickNote(info, id, a, b, Offset(NoteCategory.Connection)); break;
                    case "AnchorNote": case "TransientHiddenTickNote":
                        note = new HiddenConnectionNote(info, id, a, b, NoteCategory.Hidden, type, 1, Offset(NoteCategory.Hidden));
                        break;
                    default: throw Unsupported(entity.Archetype);
                }
                if (fake) note.SetDecoration(true);
                note.IsNextRushHeadKey = IsHoldHead(kind);
                SetPresentation(entity, note);
                SetGeometry(note, left, right);
                SetNativeScroll(entity, note, group);
                return note;
            }

            private static void SetPresentation(NextRushEntity entity, NoteBase note)
            {
                double style = entity.Number("style"), effect = entity.Number("effectKind");
                if (style < 0 || style > 8 || style % 1 != 0) throw Unsupported("style=" + style);
                if (effect < 0 || effect > 10 || effect % 1 != 0) throw Unsupported("effectKind=" + effect);
                note.NativeStyle = (int)style;
                var kind = Kind(entity);
                bool critical = TypeOf(entity) == NoteType.Critical;
                note.NativeEffectKind = effect != 0 ? (NextRushEffectKind)effect : kind switch
                {
                    "AnchorNote" or "TransientHiddenTickNote" or "TransientHiddenDamageTickNote" => NextRushEffectKind.None,
                    "DamageNote" => NextRushEffectKind.Damage,
                    "TickNote" => critical ? NextRushEffectKind.CriticalTick : NextRushEffectKind.Tick,
                    "TraceNote" or "HeadTraceNote" or "TailTraceNote" => critical ? NextRushEffectKind.CriticalTrace : NextRushEffectKind.Trace,
                    _ when kind.Contains("Flick") => critical ? NextRushEffectKind.CriticalFlick : NextRushEffectKind.Flick,
                    "TapNote" when critical => NextRushEffectKind.CriticalBasic,
                    _ => NextRushEffectKind.Basic
                };
            }

            private void SetNativeScroll(NextRushEntity entity, NoteBase note, NextRushEntity group)
            {
                if (_defaultScroll != null)
                {
                    var scroll = group != null && _groups.TryGetValue(group, out var timeline) ? timeline : _defaultScroll;
                    note.SetNextRushScroll(scroll, _timing.TimingAt(Beat(entity)).time);
                }
            }

            private void BindChain(NextRushEntity head, List<NoteBase> roots)
            {
                _geometryNodes.Add(head);
                var path = new List<NextRushEntity> { head };
                _linearPositions.Add(head, (path, 0));
                NextRushEntity lastUnattached = null;
                CheckNextLink(head, ref lastUnattached);
                var current = head;
                while (_edges.TryGetValue(current, out var first))
                {
                    var start = current;
                    var bar = CreateSection(start, first);
                    if (_standaloneKeys.TryGetValue(start, out var key)) roots.Add(key);
                    if (first.activeHead != null) _activeSections.Add(start, bar);
                    roots.Add(bar);

                    var point = (NoteBase)bar;
                    var edge = first;
                    while (true)
                    {
                        var next = edge.tail;
                        if (!_geometryNodes.Add(next)) throw Invalid("长条连接存在循环或共享节点");
                        _linearPositions.Add(next, (path, path.Count));
                        path.Add(next);
                        CheckNextLink(next, ref lastUnattached,
                            allowChainBreak: edge.activeHead != null && current == lastUnattached);
                        _outgoingBars.Add(current, bar);
                        _incomingBars.Add(next, bar);
                        point.NativeConnector = edge.segment;

                        bool continues = _edges.TryGetValue(next, out var following);
                        bool sameSection = continues && edge.Section == following.Section;
                        if (!sameSection)
                        {
                            if (first.activeTail != null && first.activeTail != next)
                                throw Unsupported("Connector 的判定区间须与连续几何片段对齐");
                            BindSectionTail(bar, next, continues ? following.activeHead : null, roots);
                            current = next;
                            break;
                        }

                        string kind = Kind(next);
                        if (_notes[next].IsDecoration)
                        {
                            point = BorrowEndpoint(next);
                        }
                        else if (kind == "TickNote" && bar is GuideNote)
                        {
                            _endpointTicks.Add(next);
                            point = BorrowEndpoint(next);
                        }
                        else if (kind == "TickNote" && UsesTickContactWindow(next))
                        {
                            // The tick keeps its own input window and still follows the section's tail result.
                            var tick = (ConnectionNote)_notes[next];
                            tick.SetNextRushEndpointSection(bar);
                            roots.Add(tick);
                            point = BorrowEndpoint(next);
                        }
                        else if (kind == "TickNote" || kind == "AnchorNote")
                        {
                            point = _notes[next];
                        }
                        else
                        {
                            // Only geometry belongs to the bar here. Head and tail actions score separately.
                            if (_notes[next] is LongNote) roots.Add(RegisterStandaloneHead(next));
                            else if (kind is "TailFlickNote" or "TailTraceFlickNote" && !IsStandaloneTail(kind, _notes[next]))
                            {
                                // Both upstream tail swipes accept a finger already holding the bar.
                                var source = _notes[next];
                                var swipe = new FrictionFlickNote(source.MusicScoreInfo, ++_nextId, source.LaneStart,
                                    source.LaneEnd, source.Category, 1, source.Type, source.Direction,
                                    _config.FlickPixels, Offset(source.Category),
                                    isTraceTail: kind == "TailTraceFlickNote");
                                _standaloneKeys.Add(next, CopyGeometry(next, swipe));
                                roots.Add(swipe);
                            }
                            else if (IsBoundTailKind(kind, _notes[next])) BindIndependentTail(next, roots);
                            point = BorrowEndpoint(next);
                        }
                        bar.AddConnectionNote(point);
                        RestoreNode(next, point);
                        current = next;
                        edge = following;
                    }
                }
                if (lastUnattached != null && Resolve(lastUnattached, "next", false) is { } lastNext
                    && !IsAttachedLinkPoint(lastNext))
                    throw Unsupported("缺少 Connector 的长条片段");
            }

            private void CheckNextLink(NextRushEntity point, ref NextRushEntity previous, bool allowChainBreak = false)
            {
                // Exporters may link the surrounding notes directly or route either
                // link through an attached point. Connectors still define the geometry.
                if (_attachments.ContainsKey(point)) return;
                var declaredNext = previous != null ? Resolve(previous, "next", false) : null;
                var declaredPrev = Resolve(point, "prev", false);
                bool viaAttachedNext = declaredNext != null && IsAttachedLinkPoint(declaredNext)
                    && Beat(declaredNext) <= Beat(point);
                bool viaAttachedPrev = declaredPrev != null && IsAttachedLinkPoint(declaredPrev)
                    && (previous == null || Beat(declaredPrev) >= Beat(previous));
                bool connectsChainEnds = allowChainBreak && declaredNext == null && declaredPrev == null;
                if (previous != null && declaredNext != point && !viaAttachedNext && !connectsChainEnds
                        && !(declaredNext == null && (viaAttachedPrev || HasAttachedNeighborLink(previous, point)))
                    || declaredPrev != null && declaredPrev != previous && !viaAttachedPrev)
                    throw Invalid("Connector 的未附着节点与 next/prev 不一致");
                previous = point;
            }

            private bool HasAttachedNeighborLink(NextRushEntity from, NextRushEntity to)
            {
                var (path, start) = _linearPositions[from];
                int end = _linearPositions[to].index;
                for (int i = start + 1; i < end; i++)
                {
                    if (Resolve(path[i], "prev", false) == from || Resolve(path[i], "next", false) == to)
                        return true;
                }
                return false;
            }

            // Hidden ticks resolve their owning hold after the connector graph is built.
            private bool IsAttachedLinkPoint(NextRushEntity point) =>
                point.Archetype == "TransientHiddenTickNote" || _attachments.ContainsKey(point);

            private bool IsLinkPoint(NextRushEntity point) =>
                point.Archetype == "TransientHiddenTickNote" || _notes.ContainsKey(point);

            private LongNote CreateSection(NextRushEntity start, ConnectorEdge edge)
            {
                var source = _notes[start];
                string kind = Kind(start);
                var activeHead = edge.activeHead;
                if (kind == "TickNote") _endpointTicks.Add(start);
                if (activeHead != null && activeHead != start)
                    throw Unsupported("Connector 的判定区间须与连续几何片段对齐");
                if (edge.segment.IsFake || edge.segment.IsDamage && activeHead != null)
                {
                    if (source is LongNote) RegisterStandaloneHead(start);
                    return CreateGuide(start, edge.segment, activeHead);
                }
                if (activeHead != null)
                {
                    if (source is LongNote scoredHead)
                    {
                        SetConnectorHeadView(scoredHead, start);
                        return scoredHead;
                    }
                    if (!source.IsDecoration && !IsIndependentHoldHead(kind) && !HasStandaloneConnectorHead(kind)
                        && kind is not ("AnchorNote" or "TickNote" or "TransientHiddenTickNote" or "TransientHiddenDamageTickNote"))
                        throw Unsupported("长条判定起点 " + start.Archetype);
                    return CreatePlayableCarrier(start, activeHead);
                }
                if (IsHoldHead(kind) && !source.IsDecoration) throw Unsupported("长条头缺少从该节点开始的判定区间");
                return CreateGuide(start);
            }

            private FrictionHideLongNote CreatePlayableCarrier(NextRushEntity start, NextRushEntity activeHead)
            {
                var source = _notes[start];
                var carrier = new FrictionHideLongNote(source.MusicScoreInfo, ++_nextId, source.LaneStart,
                    source.LaneEnd, NoteCategory.FrictionHideLong, source.Type, Offset(NoteCategory.FrictionHideLong), 1);
                var owner = _notes[activeHead];
                var kind = Kind(activeHead);
                if (!owner.IsDecoration && owner is FlickNote flick && kind is "FlickNote" or "HeadFlickNote")
                    carrier.SetNextRushFlickHead(flick);
                if (!owner.IsDecoration && owner is FrictionNote trace && kind is "TraceNote" or "HeadTraceNote" or "TailTraceNote")
                    carrier.SetNextRushTraceHead(trace);
                if (owner is FrictionFlickNote traceFlick && (traceFlick.IsTraceFlickOwner || traceFlick.IsTailTraceFlickOwner))
                    carrier.SetNextRushTraceFlickHead(traceFlick);
                if (owner is NextRushDamageNote && !owner.IsDecoration)
                    carrier.SetNextRushReleaseOwner(owner);
                if (!owner.IsDecoration && owner is NormalNote && HasStandaloneConnectorHead(kind))
                    carrier.SetNextRushReleaseOwner(owner);
                SetConnectorHeadView(carrier, activeHead);
                return CopyGeometry(start, carrier);
            }

            private NormalNote CreateTapHeadKey(NextRushEntity entity)
            {
                var source = _notes[entity];
                return new NormalNote(source.MusicScoreInfo, ++_nextId, source.LaneStart, source.LaneEnd,
                    NoteCategory.Long, 1, source.Type, Offset(NoteCategory.Long), isNextRushTap: true)
                { IsNextRushHeadKey = true };
            }

            private NoteBase RegisterStandaloneHead(NextRushEntity entity)
            {
                var key = CopyGeometry(entity, CreateTapHeadKey(entity));
                _standaloneKeys.Add(entity, key);
                return key;
            }

            private GuideNote CreateGuide(NextRushEntity start, NextRushConnectorSegment segment = null,
                NextRushEntity activeHead = null)
            {
                var source = _notes[start];
                var guide = CopyGeometry(start, new GuideNote(source.MusicScoreInfo, ++_nextId, source.LaneStart,
                    source.LaneEnd, NoteCategory.Guide, source.Type, Offset(NoteCategory.Guide), 1));
                if (activeHead != null)
                    SetConnectorHeadView(guide, activeHead);
                else if (segment is { IsFake: true, IsDamage: true })
                    SetConnectorHeadView(guide, start);
                return guide;
            }

            private void SetConnectorHeadView(LongNote bar, NextRushEntity entity)
            {
                var source = _notes[entity];
                if (!source.IsVisible) return;
                var marker = Kind(entity) == "HeadTapNote" ? CreateTapHeadKey(entity)
                    : CreateNote(entity, connectorHeadView: true);
                marker.SetDecoration(true);
                bar.SetNextRushConnectorHeadViewNote(CopyGeometry(entity, marker));
            }

            private void BindCrossChainGuides(List<NoteBase> roots)
            {
                ValidateSameBeatGeometry();
                foreach (var entry in _crossChainGuides)
                {
                    var (head, tail) = entry.Key;
                    _crossChainPoints.Add(head);
                    _crossChainPoints.Add(tail);
                    if (Kind(head) == "TickNote" && !_geometryNodes.Contains(head)) _endpointTicks.Add(head);
                    if (Kind(tail) == "TickNote" && !_geometryNodes.Contains(tail)) _endpointTicks.Add(tail);
                    var guide = CreateGuide(head, entry.Value);
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
                foreach (var (head, tail) in _graphEdges.Keys) AddSameBeat(head, tail);
                if (_attachments.Count > 0 || _notes.Values.Any(note => note is NextRushHiddenTickNote))
                {
                    foreach (var point in _notes.Keys)
                    {
                        if (Resolve(point, "next", false) is { } next) CheckAttachedLink(point, next);
                        if (Resolve(point, "prev", false) is { } prev) CheckAttachedLink(prev, point);
                    }
                }
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

                void CheckAttachedLink(NextRushEntity head, NextRushEntity tail)
                {
                    if (!IsAttachedLinkPoint(head) && !IsAttachedLinkPoint(tail)) return;
                    if (!IsLinkPoint(head) || !IsLinkPoint(tail) || Beat(head) > Beat(tail))
                        throw Invalid("附着节点的 next/prev 引用无效");
                    AddSameBeat(head, tail);
                }

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
                bool realTail = !note.IsDecoration && IsBoundTailKind(kind, note);
                if (realTail && bar is not GuideNote)
                {
                    bar.AddNoteListAndSetupChildNote(note);
                    RestoreNode(end, note);
                    return;
                }
                if (!note.IsDecoration && IsHoldHead(kind) && nextActiveHead != end
                    && (!_edges.TryGetValue(end, out var next) || !next.segment.IsFake))
                    throw Unsupported("长条头缺少从该节点开始的判定区间");
                if (realTail) BindIndependentTail(end, roots);
                if (!note.IsDecoration && kind == "AnchorNote" && !_edges.ContainsKey(end))
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
                if (Kind(entity) == "TailReleaseNote")
                {
                    var active = Resolve(entity, "activeHead", false);
                    var releaseOwner = active != null && _notes.TryGetValue(active, out var owner)
                        && (active.Archetype is "NormalReleaseNote" or "CriticalReleaseNote"
                            || owner is NextRushDamageNote && !owner.IsDecoration
                            || owner is NormalNote { IsTailReleaseOwner: true }
                            || IsStandaloneTail(Kind(active), owner) && owner is FrictionFlickNote or FrictionNote) ? owner : null;
                    ((NormalNote)note).SetNextRushIndependentRelease(releaseOwner);
                }
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
                copy.NativeStyle = note.NativeStyle;
                copy.NativeEffectKind = note.NativeEffectKind;
                SetGeometry(copy, note.LaneStartF, note.LaneEndF);
                SetNativeScroll(entity, copy, Resolve(entity, "#TIMESCALE_GROUP", false));
                if (_attachments.ContainsKey(entity)) BindVisualAttachment(entity, copy);
                return copy;
            }

            private void RestoreNode(NextRushEntity entity, NoteBase note)
            {
                // Legacy binding may promote a tail to Critical. Native entities declare their own type.
                note.Type = TypeOf(entity);
                SetGeometry(note, note.LaneStartF, note.LaneEndF);
            }

            private void BindUnattachedHiddenTick(NextRushEntity entity, NextRushHiddenTickNote tick)
            {
                var active = Resolve(entity, "activeHead", false);
                if (active != null)
                {
                    if (!_notes.TryGetValue(active, out var owner) || Beat(active) > Beat(entity))
                        throw Invalid("隐藏判定点的 activeHead 无效");
                    if (_standaloneKeys.TryGetValue(active, out var key)) owner = key;
                    tick.SetConnectorOwner(owner, isAttached: false);
                }
                if (_edges.TryGetValue(entity, out var edge) && edge.activeHead != null)
                    tick.AddOutgoingHold(_outgoingBars[entity]);
                if (_graphNodes.Contains(entity))
                {
                    foreach (var pair in JunctionEdges[entity])
                        if (pair.head == entity && _graphEdges[pair].activeHead != null)
                            tick.AddOutgoingHold(_graphBars[pair]);
                }
            }

            private void BindAttachedHiddenTick(NextRushEntity entity, Attachment attachment, List<NoteBase> roots)
            {
                RequireAttachmentPath(attachment);
                if (TryIndependentTickOwner(entity, attachment, out var owner))
                {
                    var independent = (NextRushHiddenTickNote)_notes[entity];
                    _independentTicks.Add((independent, owner));
                    roots.Add(independent);
                    return;
                }
                var root = AttachmentHold(entity, attachment.head, attachment.tail);
                bool alwaysHeld = root is GuideNote { IsFakeHold: true };
                var tick = new LongHoldCombo(_timing.At(Beat(entity)), root.Type, ++_nextId, 1, Offset(NoteCategory.Combo), alwaysHeld);
                if (alwaysHeld) roots.Add(tick);
                else root.AddHoldCombo(tick);
                // Adding a combo assigns the hold's lanes. Restore the tick's authored attachment.
                _notes[entity] = CopyGeometry(entity, tick);
            }

            private void BindComboTail(LongNote bar, NextRushEntity entity)
            {
                if (entity == null || !Kind(entity).StartsWith("Tail", StringComparison.Ordinal)) return;
                var tail = _standaloneKeys.TryGetValue(entity, out var key) ? key : _notes[entity];
                if (tail.HasJudgment) bar.SetComboTail(tail);
            }

            private void BindDamageTicks()
            {
                var ticks = _notes.Where(entry => entry.Key.Archetype == "TransientHiddenDamageTickNote").ToArray();
                if (ticks.Length == 0) return;

                var path = new Dictionary<NextRushEntity, NextRushDamageNote.PathPoint>();
                foreach (var entry in _notes)
                {
                    if (!_geometryNodes.Contains(entry.Key) && !_attachments.ContainsKey(entry.Key)) continue;
                    path.Add(entry.Key, new NextRushDamageNote.PathPoint(entry.Value.MusicScoreInfo.time,
                        entry.Value.LaneStartF - 0.5f, entry.Value.LaneEndF + 0.5f,
                        _attachments.TryGetValue(entry.Key, out var geometry) ? geometry.ease : (int)entry.Key.Number("connectorEase", 1)));
                }
                // Older charts omit prev. Infer it only where next identifies a unique predecessor.
                var incoming = new Dictionary<NextRushEntity, NextRushDamageNote.PathPoint>();
                foreach (var entry in path)
                {
                    var next = Resolve(entry.Key, "next", false);
                    if (next != null && !incoming.TryAdd(next, entry.Value)) incoming[next] = null;
                }
                foreach (var entry in path)
                {
                    var prev = Resolve(entry.Key, "prev", false);
                    var next = Resolve(entry.Key, "next", false);
                    incoming.TryGetValue(entry.Key, out var previous);
                    if (prev != null) path.TryGetValue(prev, out previous);
                    entry.Value.SetNeighbors(previous, next != null && path.TryGetValue(next, out var tail) ? tail : null);
                }
                foreach (var entry in ticks)
                {
                    var entity = entry.Key;
                    double beat = Beat(entity);
                    double start = (Math.Ceiling(beat * 2 - 1e-6) - 1) / 2;
                    var active = Resolve(entity, "activeHead", false);
                    if (active != null)
                    {
                        if (!_notes.ContainsKey(active) || Beat(active) > beat)
                            throw Invalid("隐藏伤害点的 activeHead 无效");
                        start = Math.Max(start, Beat(active));
                    }
                    var head = entity.Number("isAttached") == 1 ? Resolve(entity, "attachHead", true) : entity;
                    path.TryGetValue(head, out var point);
                    ((NextRushDamageNote)entry.Value).SetTickWindow(_timing.At(start).time, point);
                }
            }

            private void BindConnectorTickInputPaths()
            {
                // Same-hold hidden ticks were already converted to LongHoldCombo by this point.
                var ticks = _notes.Where(entry => entry.Value is ConnectionNote { HasJudgment: true }
                    && (entry.Value is NextRushHiddenTickNote || Kind(entry.Key) == "TickNote")
                    && !UsesTickContactWindow(entry.Key)).ToArray();
                if (ticks.Length == 0) return;

                var visibleTicks = ticks.Where(entry => entry.Value.IsVisible).Select(entry => entry.Key).ToHashSet();
                var points = new Dictionary<NextRushEntity, NextRushTickPathPoint>(_notes.Count);
                foreach (var (entity, note) in _notes)
                {
                    bool attached = _attachments.TryGetValue(entity, out var attachment);
                    points.Add(entity, new NextRushTickPathPoint(note,
                        attached ? attachment.ease : (int)entity.Number("connectorEase", 1),
                        attached ? attachment.fraction : null, visibleTicks.Contains(entity)));
                }

                // The authored links select the path at a junction. Older charts may rely on a
                // single Connector edge when a link is absent; never choose an arbitrary branch.
                var outgoing = new Dictionary<NextRushEntity, NextRushEntity>();
                foreach (var (head, edge) in _edges) AddUnique(outgoing, head, edge.tail);
                foreach (var (head, tail) in _graphEdges.Keys) AddUnique(outgoing, head, tail);
                foreach (var (head, tail) in _crossChainGuides.Keys) AddUnique(outgoing, head, tail);

                var nextLinks = new Dictionary<NextRushEntity, NextRushEntity>();
                var previousLinks = new Dictionary<NextRushEntity, NextRushEntity>();
                foreach (var entity in points.Keys)
                {
                    var next = Resolve(entity, "next", false) ?? outgoing.GetValueOrDefault(entity);
                    if (next == null || !points.ContainsKey(next)) continue;
                    nextLinks.Add(entity, next);
                    AddUnique(previousLinks, next, entity);
                }
                foreach (var (entity, point) in points)
                {
                    var previous = Resolve(entity, "prev", false) ?? previousLinks.GetValueOrDefault(entity);
                    point.SetNeighbors(PointFor(previous), PointFor(nextLinks.GetValueOrDefault(entity)));
                }
                foreach (var (entity, note) in ticks)
                {
                    var head = Resolve(entity, "attachHead", false) ?? entity;
                    var tail = Resolve(entity, "attachTail", false) ?? nextLinks.GetValueOrDefault(entity);
                    ((ConnectionNote)note).SetConnectorInputPath(points[head], PointFor(tail));
                }

                NextRushTickPathPoint PointFor(NextRushEntity entity) =>
                    entity == null ? null : points.GetValueOrDefault(entity);

                static void AddUnique(Dictionary<NextRushEntity, NextRushEntity> links, NextRushEntity from, NextRushEntity to)
                {
                    if (!links.TryAdd(from, to) && links[from] != to) links[from] = null;
                }
            }

            private void BindIndependentAttachment(NextRushEntity entity, NoteBase note, Attachment attachment, List<NoteBase> roots)
            {
                string kind = Kind(entity);
                if (IsHoldHead(kind) || IsBoundTailKind(kind, note))
                    throw Invalid("孤立的附着长条节点");
                if (kind == "TickNote")
                {
                    var hold = AttachmentHold(entity, attachment.head, attachment.tail);
                    // A visible tick at a graph arm's end must still receive input after the body ends.
                    if (hold is GuideNote { IsFakeHold: true }
                        || attachment.head == attachment.tail && _graphNodes.Contains(attachment.head)
                            && note.MusicScoreInfo.time == hold.ChildNote.MusicScoreInfo.time)
                    {
                        ((ConnectionNote)note).SetNextRushEndpointSection(hold);
                        roots.Add(note);
                    }
                    else
                    {
                        hold.AddConnectionNote(note, shapesLine: false);
                        note.Type = TypeOf(entity);
                    }
                }
                else if (kind != "TransientHiddenDamageTickNote" && entity.Has("activeHead"))
                {
                    var owner = Resolve(entity, "activeHead", true);
                    if (kind is not ("TapNote" or "FlickNote" or "TraceNote" or "TraceFlickNote" or "ReleaseNote" or "DamageNote")
                        || !PlayableSectionCovers(owner, Beat(entity)))
                        throw Unsupported("独立附着音符的判定区间");
                }
            }

            private void BindAttachments()
            {
                // Evaluate the geometry of both immediate references before their child.
                var visiting = new HashSet<NextRushEntity>();
                var pending = new Stack<(NextRushEntity point, bool finish)>();
                foreach (var point in _notes.Keys)
                {
                    if (point.Has("next") && !IsLinkPoint(Resolve(point, "next", true)))
                        throw Invalid("长条 next 未指向音符");
                    if (point.Number("isAttached") != 1 || _attachments.ContainsKey(point)) continue;
                    pending.Push((point, false));
                    while (pending.Count != 0)
                    {
                        var (current, finish) = pending.Pop();
                        if (_attachments.ContainsKey(current)) continue;
                        var head = Resolve(current, "attachHead", true);
                        var tail = Resolve(current, "attachTail", true);
                        if (!_notes.ContainsKey(head) || !_notes.ContainsKey(tail))
                            throw Invalid("音符的附着连接无效");
                        if (finish)
                        {
                            var attachment = ResolveAttachment(current, head, tail);
                            _attachments.Add(current, attachment);
                            BindAttachment(current, attachment);
                            visiting.Remove(current);
                            continue;
                        }
                        if (!visiting.Add(current)) throw Invalid("音符附着引用存在循环");
                        pending.Push((current, true));
                        if (tail != head && tail.Number("isAttached") == 1) pending.Push((tail, false));
                        if (head.Number("isAttached") == 1) pending.Push((head, false));
                    }
                }
            }

            private void BindAttachment(NextRushEntity entity, Attachment attachment)
            {
                var note = _notes[entity];
                SetAttachmentGeometry(note, attachment);
                SetNativeScroll(entity, note, Resolve(entity, "#TIMESCALE_GROUP", false));
                BindVisualAttachment(entity, note);
            }

            // Lane geometry can follow a chain of aliases, but visual progress samples
            // the authored groups of the two immediate references.
            private void BindVisualAttachment(NextRushEntity entity, NoteBase note) =>
                note.SetNextRushAttachment(_notes[Resolve(entity, "attachHead", true)], _notes[Resolve(entity, "attachTail", true)]);

            private Attachment ResolveAttachment(NextRushEntity entity, NextRushEntity head, NextRushEntity tail)
            {
                if (!_notes.ContainsKey(head) || !_notes.ContainsKey(tail)
                    || Beat(entity) < Beat(head) || Beat(entity) > Beat(tail))
                    throw Invalid("音符的附着连接无效");
                bool attachedHead = _attachments.TryGetValue(head, out var parent);
                if (head == tail && attachedHead) return parent;
                int ease = attachedHead ? parent.ease : (int)head.Number("connectorEase", 1);
                double start = _timing.TimingAt(Beat(head)).time;
                double end = _timing.TimingAt(Beat(tail)).time;
                double time = _timing.TimingAt(Beat(entity)).time;
                return new Attachment(head, tail, start == end ? 0.5f : (float)((time - start) / (end - start)),
                    ease);
            }

            private void RequireAttachmentPath(Attachment attachment)
            {
                var head = PathEndpoint(attachment.head, true);
                var tail = PathEndpoint(attachment.tail, false);
                if (head == tail || DirectEdge(head, tail) || Resolve(head, "next", false) == tail) return;
                if (!TryLinearSpan(head, tail, out _, out _, out _))
                    GraphSpan(head, tail);
            }

            private NextRushEntity PathEndpoint(NextRushEntity point, bool start)
            {
                // An attached marker can supply geometry without itself splitting a connector.
                var initial = point;
                while (!_geometryNodes.Contains(point) && !_crossChainPoints.Contains(point))
                {
                    if (_pathEndpoints.TryGetValue((point, start), out var cached))
                    {
                        point = cached;
                        break;
                    }
                    if (!_attachments.TryGetValue(point, out var source)) throw Invalid("音符的附着引用不属于连接路径");
                    point = start ? source.head : source.tail;
                }
                if (point != initial) _pathEndpoints[(initial, start)] = point;
                return point;
            }

            private bool DirectEdge(NextRushEntity head, NextRushEntity tail) =>
                _graphBars.ContainsKey((head, tail)) || _crossChainGuides.ContainsKey((head, tail))
                || _edges.TryGetValue(head, out var edge) && edge.tail == tail;

            private bool TryIndependentTickOwner(NextRushEntity entity, Attachment attachment, out NextRushEntity owner)
            {
                owner = Resolve(entity, "activeHead", false);
                if (owner == null) return false;
                var declaredOwner = owner;
                var head = PathEndpoint(attachment.head, true);
                var tail = PathEndpoint(attachment.tail, false);
                double beat = Beat(entity);
                bool UsesDifferentOwner(NextRushEntity a, NextRushEntity b)
                {
                    if (_crossChainGuides.ContainsKey((a, b))) return true;
                    if (!_graphEdges.TryGetValue((a, b), out var edge)
                        && (!_edges.TryGetValue(a, out edge) || edge.tail != b)) return false;
                    return edge.activeHead != declaredOwner;
                }

                if (head == tail)
                {
                    if (_graphNodes.Contains(head))
                    {
                        if (!JunctionEdges[head].All(pair => UsesDifferentOwner(pair.head, pair.tail))) return false;
                    }
                    else if (_edges.TryGetValue(head, out var next))
                    {
                        if (!UsesDifferentOwner(head, next.tail)) return false;
                    }
                    else if (!_crossChainPoints.Contains(head) && (!_linearPositions.TryGetValue(head, out var position)
                        || position.index == 0 || !UsesDifferentOwner(position.path[position.index - 1], head))) return false;
                }
                else if (DirectEdge(head, tail))
                {
                    if (!UsesDifferentOwner(head, tail)) return false;
                }
                else if (TryLinearSpan(head, tail, out var path, out int first, out int last))
                {
                    int i = EdgeEndAt(path, first, last, beat);
                    if (Beat(path[i]) == beat && i < last && EdgeOwner(path[i], path[i + 1]) == owner) return false;
                    if (!UsesDifferentOwner(path[i - 1], path[i])) return false;
                }
                else
                {
                    bool found = false;
                    foreach (var edge in GraphEdgesAt(head, tail, beat))
                    {
                        if (!UsesDifferentOwner(edge.head, edge.tail)) return false;
                        found = true;
                    }
                    if (!found) return false;
                }
                return PlayableSectionCovers(owner, beat);
            }

            private bool PlayableSectionCovers(NextRushEntity owner, double beat)
            {
                _playableEndBeats ??= _edges.Values.Concat(_graphEdges.Values)
                    .Where(e => e.activeHead != null && !e.segment.IsFake && !e.segment.IsDamage && e.segment.Kind < 101)
                    .GroupBy(e => e.activeHead).ToDictionary(g => g.Key, g => g.Max(e => Beat(e.activeTail)));
                return _playableEndBeats.TryGetValue(owner, out double end) && Beat(owner) <= beat && beat <= end;
            }

            private static int EdgeEndAt(List<NextRushEntity> path, int first, int last, double beat)
            {
                int low = first + 1;
                while (low < last)
                {
                    int middle = low + (last - low) / 2;
                    if (Beat(path[middle]) < beat) low = middle + 1;
                    else last = middle;
                }
                return low;
            }

            private LongNote AttachmentHold(NextRushEntity entity, NextRushEntity head, NextRushEntity tail)
            {
                var active = Resolve(entity, "activeHead", false);
                head = PathEndpoint(head, true);
                tail = PathEndpoint(tail, false);
                if (head == tail && _graphNodes.Contains(head))
                    return JunctionHold(head, active ?? Resolve(head, "activeHead", false));
                if (head != tail && !DirectEdge(head, tail))
                {
                    double at = Beat(entity);
                    if (!TryLinearSpan(head, tail, out var path, out int first, out int last))
                        return GraphSpanHold(head, tail, at, active);
                    int low = EdgeEndAt(path, first, last, at);
                    head = path[low - 1];
                    tail = path[low];
                    if (active != null && Beat(tail) == at && low < last
                        && EdgeOwner(head, tail) != active && EdgeOwner(tail, path[low + 1]) == active)
                        (head, tail) = (tail, path[low + 1]);
                }
                if (_graphBars.TryGetValue((head, tail), out var branch))
                {
                    if (!IsTickHold(branch) || active != null && _graphEdges[(head, tail)].activeHead != active)
                        throw Invalid("附着判定点与分支判定区间不一致");
                    return branch;
                }
                if (_crossChainGuides.ContainsKey((head, tail))) throw Unsupported("装饰段上的附着判定点");
                _outgoingBars.TryGetValue(head, out var outgoing);
                LongNote incoming = null;
                if (head == tail) _incomingBars.TryGetValue(head, out incoming);
                var root = outgoing ?? incoming;
                if (active != null && (!_activeSections.TryGetValue(active, out root)
                    || root != outgoing && root != incoming))
                    throw Unsupported("附着判定点的独立判定区间");
                if (!IsTickHold(root)) throw Unsupported("装饰段上的附着判定点");
                return root;
            }

            private static bool IsTickHold(LongNote bar) => bar != null
                && (bar is not GuideNote guide || guide.IsFakeHold);

            private LongNote JunctionHold(NextRushEntity node, NextRushEntity active)
            {
                var next = Resolve(node, "next", false);
                var prev = Resolve(node, "prev", false);
                LongNote selected = null;
                int best = 0;
                foreach (var pair in JunctionEdges[node])
                {
                    var bar = _graphBars[pair];
                    if (!IsTickHold(bar) || active != null && _graphEdges[pair].activeHead != active) continue;
                    int priority = pair.head == node && pair.tail == next ? 3
                        : pair.tail == node && pair.head == prev ? 2 : 1;
                    if (priority > best)
                    {
                        best = priority;
                        selected = bar;
                    }
                    else if (priority == best) selected = null;
                }
                return selected ?? throw Invalid("分支同点附着缺少可确定的判定段");
            }

            private NextRushEntity EdgeOwner(NextRushEntity head, NextRushEntity tail)
            {
                if (_graphEdges.TryGetValue((head, tail), out var branch)) return branch.activeHead;
                return _edges.TryGetValue(head, out var edge) && edge.tail == tail ? edge.activeHead : null;
            }

            private bool TryLinearSpan(NextRushEntity head, NextRushEntity tail, out List<NextRushEntity> path,
                out int first, out int last)
            {
                _linearPositions.TryGetValue(head, out var start);
                _linearPositions.TryGetValue(tail, out var end);
                (path, first, last) = (start.path, start.index, end.index);
                return path != null && path == end.path && first < last;
            }

            private Dictionary<NextRushEntity, List<NextRushEntity>> GraphSpan(NextRushEntity head, NextRushEntity tail)
            {
                // Walk backwards to exclude branches that never reach the authored tail.
                _attachmentPredecessors ??= _edges.Select(e => (head: e.Key, tail: e.Value.tail))
                    .Concat(_graphEdges.Keys).Concat(_crossChainGuides.Keys).ToLookup(pair => pair.tail, pair => pair.head);
                var successors = new Dictionary<NextRushEntity, List<NextRushEntity>>();
                var pending = new Stack<NextRushEntity>();
                pending.Push(tail);
                while (pending.Count != 0)
                {
                    var current = pending.Pop();
                    if (current == head) continue;
                    foreach (var prev in _attachmentPredecessors[current])
                    {
                        if (Beat(prev) < Beat(head)) continue;
                        if (!successors.TryGetValue(prev, out var next))
                        {
                            successors.Add(prev, next = new List<NextRushEntity>());
                            pending.Push(prev);
                        }
                        next.Add(current);
                    }
                }
                if (!successors.ContainsKey(head)) throw Invalid("附着端点之间缺少连接路径");
                return successors;
            }

            private IEnumerable<(NextRushEntity head, NextRushEntity tail)> GraphEdgesAt(NextRushEntity head, NextRushEntity tail, double beat)
            {
                var successors = GraphSpan(head, tail);
                var pending = new Stack<NextRushEntity>();
                var visited = new HashSet<NextRushEntity>();
                pending.Push(head);
                while (pending.Count != 0)
                {
                    var current = pending.Pop();
                    if (!visited.Add(current) || !successors.TryGetValue(current, out var edges)) continue;
                    foreach (var next in edges)
                    {
                        if (Beat(next) <= beat) pending.Push(next);
                        if (Beat(next) >= beat) yield return (current, next);
                    }
                }
            }

            private LongNote GraphSpanHold(NextRushEntity head, NextRushEntity tail, double beat, NextRushEntity active)
            {
                LongNote selected = null;
                int best = 0;
                foreach (var (current, next) in GraphEdgesAt(head, tail, beat))
                {
                    if (active != null && EdgeOwner(current, next) != active) continue;
                    if (!_graphBars.TryGetValue((current, next), out var bar)
                        && _edges.TryGetValue(current, out var edge) && edge.tail == next)
                        _outgoingBars.TryGetValue(current, out bar);
                    if (!IsTickHold(bar)) continue;
                    int priority = Beat(current) < beat ? 2 : 1;
                    if (priority > best)
                    {
                        selected = bar;
                        best = priority;
                    }
                    else if (priority == best && selected != bar) selected = null;
                }
                return selected ?? throw Invalid("跨边附着判定点缺少可确定的判定段");
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
                    Fields(entity, "head", "tail", "segmentHead", "segmentTail", "activeHead", "activeTail", "legacyHiddenPop");
                    double legacyPop = entity.Number("legacyHiddenPop");
                    if (legacyPop != 0 && legacyPop != 1) throw Invalid("Connector.legacyHiddenPop 必须为 0 或 1");
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
                    double defaultKind = activeHead == null ? 101 : TypeOf(a) == NoteType.Critical ? 2 : 1;
                    int kind = (int)a.Number("segmentKind", defaultKind);
                    // A guide ending at the next hold head can already reference that hold.
                    bool guideLeadIn = kind is >= 101 and <= 108 && tail == activeHead && Beat(head) < Beat(tail);
                    if ((activeHead == null) != (activeTail == null)
                        || activeHead != null && (!_notes.ContainsKey(activeHead) || !_notes.ContainsKey(activeTail)
                            || Beat(activeHead) > Beat(head) && !guideLeadIn || Beat(activeTail) < Beat(tail)))
                        throw Invalid("Connector 的判定区间没有覆盖连接");
                    if (guideLeadIn)
                    {
                        activeHead = null;
                        activeTail = null;
                    }
                    bool headAttached = _attachments.TryGetValue(head, out var headAttachment);
                    bool tailAttached = _attachments.TryGetValue(tail, out var tailAttachment);
                    bool sameBeat = Beat(head) == Beat(tail);
                    if (activeHead != null && Beat(activeHead) < Beat(activeTail))
                    {
                        _soundEdges.Add((head, tail, activeHead, activeTail));
                        if (head == activeHead) _soundStarts.Add((head, activeTail, tail, a, kind, legacyPop == 1));
                    }
                    if (kind == 0) throw Invalid("Connector 的 segmentKind 为空");
                    var segment = new NextRushConnectorSegment(
                        kind,
                        headAttached ? headAttachment.ease : (int)head.Number("connectorEase", 1),
                        AlphaAt(head, a, b), AlphaAt(tail, a, b), styleHead, a.Number("segmentThroughJudgeLine") == 1,
                        (NextRushConnectorLayer)(int)a.Number("segmentLayer"), a.Number("segmentPresentation") == 1,
                        headAttached && !sameBeat ? headAttachment.fraction : 0f,
                        tailAttached && !sameBeat ? tailAttachment.fraction : 1f, activeHead != null, legacyPop == 1);
                    if (activeHead == null && !headAttached && !tailAttached && !FollowsNoteChain(head, tail))
                    {
                        if (_graphEdges.ContainsKey((head, tail)) || !_crossChainGuides.TryAdd((head, tail), segment))
                            throw Invalid("重复的跨链 Connector");
                    }
                    else if (_crossChainGuides.ContainsKey((head, tail))
                        || !_graphEdges.TryAdd((head, tail), new ConnectorEdge(tail, activeHead, activeTail, segment)))
                        throw Invalid("重复的长条连接片段");
                }
            }

            private bool FollowsNoteChain(NextRushEntity head, NextRushEntity tail)
            {
                // The note chain may visit keys attached to this edge without splitting its geometry.
                var next = Resolve(head, "next", false);
                int visited = 0;
                while (next != null && _attachments.TryGetValue(next, out var attachment)
                    && attachment.head == head && attachment.tail == tail)
                {
                    if (++visited > _attachments.Count) throw Invalid("附着节点的 next 引用存在循环");
                    next = Resolve(next, "next", false);
                }
                return next == tail;
            }

            private NextRushConnectorSound[] BuildConnectorSounds()
            {
                var paths = _soundEdges.ToLookup(e => (e.owner, e.end), e =>
                    _graphBars.TryGetValue((e.head, e.tail), out var bar) ? bar : _outgoingBars[e.head]);
                var sounds = new List<NextRushConnectorSound>();
                foreach (var (head, tail, firstEnd, style, autoKind, legacy) in _soundStarts)
                {
                    int manualKind = (int)head.Number("segmentKind", TypeOf(head) == NoteType.Critical ? 2 : 1);
                    sounds.Add(new NextRushConnectorSound(
                        _standaloneKeys.TryGetValue(head, out var key) ? key : _notes[head],
                        _notes[tail], _notes[firstEnd], _notes[style], legacy, manualKind, autoKind, paths[(head, tail)].Distinct().ToArray()));
                }
                return sounds.OrderBy(s => s.Start).ToArray();
            }

            private void SeparateBranchedSections()
            {
                var outgoing = _graphEdges.Keys.ToLookup(pair => pair.head);
                var incoming = _graphEdges.Keys.ToLookup(pair => pair.tail);
                // A change of carrier inside one active interval needs separate bars sharing the same owner.
                var activeEdges = _graphEdges.Where(pair => pair.Value.activeHead != null);
                var starts = activeEdges.Select(pair => (pair.Key.head, pair.Value.Section)).ToHashSet();
                var ends = activeEdges.Select(pair => (pair.Key.tail, pair.Value.Section)).ToHashSet();
                var neighbors = new Dictionary<NextRushEntity, List<NextRushEntity>>();
                var pending = new Queue<NextRushEntity>();
                void Link(NextRushEntity a, NextRushEntity b)
                {
                    if (a == b) return;
                    if (!neighbors.TryGetValue(a, out var list)) neighbors.Add(a, list = new());
                    list.Add(b);
                }
                foreach (var (pair, edge) in _graphEdges)
                {
                    if (outgoing[pair.head].Count() > 1 || incoming[pair.tail].Count() > 1
                        || edge.activeHead != null && (
                            pair.head != edge.activeHead && !ends.Contains((pair.head, edge.Section))
                            || pair.tail != edge.activeTail && !starts.Contains((pair.tail, edge.Section))))
                        pending.Enqueue(pair.head);
                }
                // Two complete spans can also be linked in next/prev while the chart
                // deliberately omits the Connector between their boundary nodes.
                foreach (var end in incoming)
                {
                    if (outgoing.Contains(end.Key)) continue;
                    var next = Resolve(end.Key, "next", false);
                    if (next == null || !outgoing.Contains(next) || incoming.Contains(next)) continue;
                    pending.Enqueue(end.Key);
                    pending.Enqueue(next);
                }
                if (pending.Count != 0)
                {
                    foreach (var (pair, edge) in _graphEdges)
                    {
                        Link(pair.head, pair.tail);
                        Link(pair.tail, pair.head);
                        if (edge.activeHead == null) continue;
                        Link(pair.head, edge.activeHead);
                        Link(edge.activeHead, pair.head);
                        Link(pair.tail, edge.activeTail);
                        Link(edge.activeTail, pair.tail);
                    }
                }
                while (pending.Count > 0)
                {
                    var node = pending.Dequeue();
                    if (!_graphNodes.Add(node)) continue;
                    foreach (var other in neighbors[node]) pending.Enqueue(other);
                }
                foreach (var pair in _graphEdges.Keys.Where(pair => !_graphNodes.Contains(pair.head)).ToArray())
                {
                    _edges.Add(pair.head, _graphEdges[pair]);
                    _graphEdges.Remove(pair);
                }
            }

            private void BindGraphEdges(List<NoteBase> roots)
            {
                foreach (var (pair, edge) in _graphEdges)
                {
                    LongNote bar;
                    if (edge.activeHead == null || edge.segment.IsFake || edge.segment.IsDamage)
                        bar = CreateGuide(pair.head, edge.segment, edge.activeHead);
                    else
                        bar = CreatePlayableCarrier(pair.head, edge.activeHead);
                    if (edge.activeHead != null)
                    {
                        _graphOwners.Add((pair.head, edge.activeHead));
                        _graphOwners.Add((pair.tail, edge.activeHead));
                        _graphOwners.Add((edge.activeHead, edge.activeHead));
                        _graphOwners.Add((edge.activeTail, edge.activeHead));
                    }
                    _geometryNodes.Add(pair.head);
                    _geometryNodes.Add(pair.tail);
                    bar.NativeConnector = edge.segment;
                    bar.AddNoteListAndSetupChildNote(BorrowEndpoint(pair.tail));
                    _graphBars.Add(pair, bar);
                    roots.Add(bar);
                }
            }

            private void BindGraphNode(NextRushEntity entity, List<NoteBase> roots)
            {
                var note = _notes[entity];
                var next = Resolve(entity, "next", false);
                var prev = Resolve(entity, "prev", false);
                if (!_geometryNodes.Contains(entity) && (next != null || prev != null))
                    throw Invalid("判定区间端点缺少 Connector");
                if (next != null && !ValidBranchLink(entity, next) || prev != null && !ValidBranchLink(prev, entity))
                    throw Invalid("分支节点的 next/prev 无效");
                var active = Resolve(entity, "activeHead", false);
                if (active != null && !_graphOwners.Contains((entity, active))) throw Invalid("分支节点的 activeHead 无效");
                if (note.IsDecoration) return;
                if (note is LongNote)
                {
                    roots.Add(RegisterStandaloneHead(entity));
                }
                else if (Kind(entity) == "TickNote")
                {
                    // The shared vertex is a single contact target whichever edge reaches it.
                    roots.Add(note);
                }
                else if (IsBoundTailKind(Kind(entity), note)) BindIndependentTail(entity, roots);
            }

            private bool ValidBranchLink(NextRushEntity head, NextRushEntity tail)
            {
                if ((!_geometryNodes.Contains(head) && !IsAttachedLinkPoint(head))
                    || (!_geometryNodes.Contains(tail) && !IsAttachedLinkPoint(tail))) return false;
                if (IsAttachedLinkPoint(head) || IsAttachedLinkPoint(tail)) return true; // Already checked with the attachment links.
                return head != tail && (Beat(head) < Beat(tail) || _graphEdges.ContainsKey((head, tail)));
            }

            private bool NeedsSimLineTimeline(NextRushEntity line)
            {
                var left = Resolve(line, "left", true);
                var right = Resolve(line, "right", true);
                if (left == right) return false;
                return Beat(left) != Beat(right) || Kind(left) is "AnchorNote" or "TransientHiddenTickNote"
                    || Kind(right) is "AnchorNote" or "TransientHiddenTickNote";
            }

            private HashSet<float> BindSimLines(List<(NoteBase left, NoteBase right)> hiddenLines)
            {
                var authoredPairTimes = new HashSet<float>();
                foreach (var entity in _source.Entities)
                {
                    if (entity.Archetype != "SimLine") continue;
                    Fields(entity, "left", "right");
                    var a = Resolve(entity, "left", true); var b = Resolve(entity, "right", true);
                    if (!_notes.TryGetValue(a, out var left)
                        || !_notes.TryGetValue(b, out var right)) throw Invalid("SimLine 引用无效");
                    if (!_showPair) continue;
                    if (_standaloneKeys.TryGetValue(a, out var leftKey)) left = leftKey;
                    if (_standaloneKeys.TryGetValue(b, out var rightKey)) right = rightKey;
                    authoredPairTimes.Add(left.MusicScoreInfo.time);
                    // Coincident endpoints have zero area, but the beat still uses authored lines.
                    if (a == b) continue;
                    authoredPairTimes.Add(right.MusicScoreInfo.time);
                    if (!left.IsVisible || !right.IsVisible) hiddenLines.Add((left, right));
                    else left.AddNextRushSimLine(right);
                }
                return authoredPairTimes;
            }

            private float AlphaAt(NextRushEntity point, NextRushEntity start, NextRushEntity end)
            {
                double first = _timing.TimingAt(Beat(start)).time, last = _timing.TimingAt(Beat(end)).time;
                double progress = point == start ? 0 : point == end ? 1 : first == last ? 0.5
                    : Math.Clamp((_timing.TimingAt(Beat(point)).time - first) / (last - first), 0, 1);
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
            private float Offset(NoteCategory category) => _config.LaneOffsetFor(category);
            private static (int start, int end) LaneIndices(float left, float right)
            {
                double start = Math.Floor(left), end = Math.Ceiling(right);
                // Legacy view data stores both indices and their difference as signed integers.
                if (!(start >= int.MinValue && start <= int.MaxValue && end >= int.MinValue
                    && end <= int.MaxValue && end - start <= int.MaxValue))
                    throw Unsupported("音符边界或宽度超出运行时轨道索引范围");
                return ((int)start, (int)end);
            }
            private void SetGeometry(NoteBase note, float left, float right)
            {
                var (start, end) = LaneIndices(left, right);
                note.LaneStart = note.DefaultLeftLane = start;
                note.LaneEnd = note.DefaultRightLane = end;
                note.LaneStartF = left; note.LaneEndF = right;
                JudgeTuning.WidenLaneRange(left, right, Offset(note.Category), out float a, out float b);
                note.JudgeLaneStart = a; note.JudgeLaneEnd = b;
            }
            private static void SetPairs(IEnumerable<NoteBase> roots, HashSet<float> authoredPairTimes)
            {
                var notes = roots.SelectMany(n => n.NoteList ?? new List<NoteBase> { n })
                    .Where(n => n.HasJudgment && !authoredPairTimes.Contains(n.MusicScoreInfo.time)
                        && n is not (ConnectionNote or NextRushDamageNote)
                        && !RuntimeChartConverter.IsNotPairNoteTarget(n));
                foreach (var group in notes.GroupBy(n => n.MusicScoreInfo.time))
                {
                    var ordered = group.OrderBy(n => n.LaneStartF).ToArray();
                    if (ordered.Length < 2) continue;
                    ordered[0].SetPairNote(ordered[ordered.Length - 1]);
                    ordered[ordered.Length - 1].SetPairNote(ordered[0]);
                }
            }
            private static string Kind(NextRushEntity entity)
            {
                string archetype = NextRushCompatibility.WithoutFakePrefix(entity.Archetype);
                return NextRushCompatibility.GetNoteKind(archetype) ?? archetype;
            }
            private static bool IsHoldHead(string kind) => kind.StartsWith("Head", StringComparison.Ordinal);
            private static bool HasStandaloneConnectorHead(string kind) =>
                kind is "TapNote" or "FlickNote" or "TraceNote" or "TraceFlickNote" or "ReleaseNote" or "DamageNote" or "TailTapNote" or "TailFlickNote" or "TailTraceNote" or "TailTraceFlickNote" or "TailReleaseNote";
            private static bool IsIndependentHoldHead(string kind) =>
                kind is "HeadTraceNote" or "HeadReleaseNote" or "HeadFlickNote" or "HeadTraceFlickNote";

            private static bool IsStandaloneTail(string kind, NoteBase note) =>
                kind == "TailTapNote" || note is NormalNote { IsTailReleaseOwner: true } or FrictionFlickNote { IsTailFlickOwner: true }
                    or FrictionFlickNote { IsTailTraceFlickOwner: true } or FrictionNote { IsTailTraceOwner: true };

            private static bool IsBoundTailKind(string kind, NoteBase note) =>
                kind.StartsWith("Tail", StringComparison.Ordinal) && !IsStandaloneTail(kind, note);

            private static bool UsesTickContactWindow(NextRushEntity tick) =>
                !tick.Has("activeHead") && tick.Number("isAttached") != 1;

            private static bool IsChainNode(string kind) => kind == "TickNote" || kind == "AnchorNote"
                || IsHoldHead(kind) || kind.StartsWith("Tail", StringComparison.Ordinal);
            private static NoteType TypeOf(NextRushEntity entity) =>
                NextRushCompatibility.WithoutFakePrefix(entity.Archetype).StartsWith("Critical", StringComparison.Ordinal)
                    ? NoteType.Critical : NoteType.Default;
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
