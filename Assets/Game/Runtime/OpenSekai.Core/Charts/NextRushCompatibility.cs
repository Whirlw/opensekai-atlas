using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenSekai.Charts
{
    public enum NextRushRevision
    {
        Base = 0,
        Sonolus110 = 1,
        ApparentElevation = 2
    }

    public enum NextRushEntityStatus
    {
        CompilerCandidate,
        IgnoredByProject,
        Unsupported,
        RuntimeOnly,
        Unknown
    }

    public sealed class NextRushCompatibilityReport
    {
        public NextRushRevision? Revision { get; }
        public IReadOnlyList<(string Archetype, NextRushEntityStatus Status, int Count)> Archetypes { get; }
        public IReadOnlyList<string> UnsupportedFeatures { get; }
        public IReadOnlyList<string> InvalidData { get; }
        public bool PassesPreflight => UnsupportedFeatures.Count == 0 && InvalidData.Count == 0;

        internal NextRushCompatibilityReport(NextRushRevision? revision,
            List<(string, NextRushEntityStatus, int)> archetypes, List<string> unsupported, List<string> invalid)
        {
            Revision = revision;
            Archetypes = archetypes.AsReadOnly();
            UnsupportedFeatures = unsupported.AsReadOnly();
            InvalidData = invalid.AsReadOnly();
        }
    }

    /// <summary>Classifies authored entities against the pinned play-mode input. The compiler validates note fields and graphs.</summary>
    public static class NextRushCompatibility
    {
        public const string UpstreamCommit = "1f086fb6fcc56fcb38cbf9eba6b98dbefc630316";

        private static readonly HashSet<string> NoteKinds = new HashSet<string>(StringComparer.Ordinal)
        {
            "TapNote", "FlickNote", "TraceNote", "TraceFlickNote", "ReleaseNote",
            "HeadTapNote", "HeadFlickNote", "HeadTraceNote", "HeadTraceFlickNote", "HeadReleaseNote",
            "TailTapNote", "TailFlickNote", "TailTraceNote", "TailTraceFlickNote", "TailReleaseNote", "TickNote"
        };

        public static NextRushEntityStatus Classify(string archetype)
        {
            switch (archetype)
            {
                case "Skill": case "FeverChance": case "FeverStart":
                    return NextRushEntityStatus.IgnoredByProject;
                case "Initialization": case "#BPM_CHANGE": case "#TIMESCALE_GROUP": case "#TIMESCALE_CHANGE":
                case "Connector": case "SimLine": case "TransientHiddenTickNote": case "AnchorNote":
                    return NextRushEntityStatus.CompilerCandidate;
                case "Stage": case "CameraChange":
                case "StageTransformChange": case "StageMaskChange": case "StagePivotChange": case "StageStyleChange":
                case "DamageNote": case "TransientHiddenDamageTickNote":
                case "FakeAnchorNote": case "FakeDamageNote":
                case "FakeTransientHiddenTickNote": case "FakeTransientHiddenDamageTickNote":
                    return NextRushEntityStatus.Unsupported;
                case "StaticStage": case "_InputManager": case "ParticleManager":
                case "SlideManager": case "ConnectorSfxManager":
                case "SlotGlowEffect": case "SlotEffect": case "ComboJudge": case "JudgmentAccuracy": case "DamageFlash":
                    return NextRushEntityStatus.RuntimeOnly;
            }

            if (archetype == null) return NextRushEntityStatus.Unknown;
            bool fake = archetype.StartsWith("Fake", StringComparison.Ordinal);
            string kind = GetNoteKind(fake ? archetype.Substring(4) : archetype);
            if (kind == null || !NoteKinds.Contains(kind)) return NextRushEntityStatus.Unknown;
            if (fake) return NextRushEntityStatus.Unsupported;
            switch (kind)
            {
                case "ReleaseNote": case "HeadFlickNote": case "HeadTraceFlickNote":
                case "HeadReleaseNote": case "TailTapNote":
                    return NextRushEntityStatus.Unsupported;
                default:
                    return NextRushEntityStatus.CompilerCandidate;
            }
        }

        public static string GetNoteKind(string archetype)
        {
            if (archetype.StartsWith("Normal", StringComparison.Ordinal)) return archetype.Substring(6);
            if (archetype.StartsWith("Critical", StringComparison.Ordinal)) return archetype.Substring(8);
            return null;
        }

        public static NextRushCompatibilityReport Analyze(NextRushChart source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var archetypes = new List<(string, NextRushEntityStatus, int)>();
            var unsupported = new List<string>();
            var invalid = new List<string>();
            if (Math.Abs(source.BgmOffset) > NextRushChart.MaxAbsoluteTimeSeconds)
                invalid.Add("bgmOffset 超出播放时间范围");

            foreach (var group in source.Entities.GroupBy(e => e.Archetype, StringComparer.Ordinal))
            {
                var status = Classify(group.Key);
                archetypes.Add((group.Key, status, group.Count()));
                switch (status)
                {
                    case NextRushEntityStatus.RuntimeOnly:
                        invalid.Add(group.Key + " 是引擎内部实体");
                        break;
                    case NextRushEntityStatus.Unknown:
                        unsupported.Add("未知实体 " + group.Key);
                        break;
                    case NextRushEntityStatus.Unsupported:
                        unsupported.Add(group.Key);
                        break;
                }
            }

            NextRushRevision? revision = null;
            var initializations = source.Entities.Where(e => e.Archetype == "Initialization").Take(2).ToArray();
            if (initializations.Length != 1)
                invalid.Add("需要且只能有一个 Initialization");
            else
                revision = AnalyzeInitialization(source, initializations[0], unsupported, invalid);

            return new NextRushCompatibilityReport(revision, archetypes, unsupported, invalid);
        }

        private static NextRushRevision? AnalyzeInitialization(NextRushChart source, NextRushEntity entity,
            List<string> unsupported, List<string> invalid)
        {
            foreach (var field in entity.Fields)
                if (field != "revision" && field != "initialLife" && field != "firstCamera")
                    unsupported.Add("Initialization." + field);

            NextRushRevision? revision = null;
            try
            {
                double value = entity.Number("revision", (int)NextRushRevision.Sonolus110);
                if (value == (int)NextRushRevision.Base || value == (int)NextRushRevision.Sonolus110
                    || value == (int)NextRushRevision.ApparentElevation)
                    revision = (NextRushRevision)(int)value;
                else
                    unsupported.Add(FormattableString.Invariant($"Initialization.revision={value}"));
            }
            catch (InvalidDataException) { invalid.Add("Initialization.revision 需要数值"); }

            try
            {
                if (entity.Number("initialLife", 1000) != 1000)
                    unsupported.Add("Initialization.initialLife（自定义初始生命值）");
            }
            catch (InvalidDataException) { invalid.Add("Initialization.initialLife 需要数值"); }

            try
            {
                var camera = entity.Reference("firstCamera");
                if (camera != null)
                {
                    if (source.NamedEntities.TryGetValue(camera, out var target) && target.Archetype == "CameraChange")
                        unsupported.Add("Initialization.firstCamera");
                    else
                        invalid.Add("Initialization.firstCamera 未指向 CameraChange");
                }
            }
            catch (InvalidDataException) { invalid.Add("Initialization.firstCamera 需要实体引用"); }

            return revision;
        }
    }
}
