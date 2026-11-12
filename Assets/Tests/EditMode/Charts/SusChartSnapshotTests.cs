using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using OpenSekai.Shared.Conversion;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Charts
{
    /// <summary>
    /// Freezes what the SUS importer produces for a committed pair of charts. P2-6 splits the one
    /// converter class into a parser, a timing stage, a note stage and an event stage; the only way to
    /// call that split behaviour-preserving is to compare the produced score line by line — the frames,
    /// their timestamps, the event order, the concrete note classes and the long-note links.
    /// </summary>
    /// <remarks>
    /// The expectation is a committed text file rather than an inline hash so a reviewer can read the
    /// diff. <c>Tools/fixtures/sus-p2-6/README.md</c> documents how to regenerate it after an intended
    /// change.
    /// </remarks>
    public class SusChartSnapshotTests
    {
        private const string FixtureDirectory = "Tools/fixtures/sus-p2-6";
        private const string ConfigResourcePath = "live/config/LiveBundleBuildData";

        private LiveBundleBuildData _config;

        [SetUp]
        public void SetUp()
        {
            _config = Resources.Load<LiveBundleBuildData>(ConfigResourcePath);
            Assert.That(_config, Is.Not.Null, "the live bundle config the importer reads lane offsets from");
        }

        [TearDown]
        public void TearDown()
        {
            _config = null;
        }

        private static string Timing(MusicScoreInfo value) => string.Format(
            CultureInfo.InvariantCulture,
            "{0}|{1:R}|{2:R}|{3:R}|{4:R}|{5:R}|{6:R}|{7}",
            value.bar, value.barProgress, value.time, value.bpm, value.timeSignature,
            value.speedRatio, value.seVolume, value.guideColor);

        private static string Note(NoteBase note) => string.Format(
            CultureInfo.InvariantCulture,
            "{0}|{1}|{2}|{3}|{4}|{5}|{6:R}..{7:R}|{8:R}|{9}|{10}|{11}|{12}|{13}|{14}|{15}",
            note.Id, note.GetType().Name, note.Category, note.Type, note.Direction, note.LineType,
            note.LaneStartF, note.LaneEndF, note.speedRatio, note.IsSkip, note.IsDecoration,
            note.ParentRole, note.ParentNote?.Id, note.ChildNote?.Id, note.PairNote?.Id,
            Timing(note.MusicScoreInfo));

        /// <summary>Reproduces <c>TestResults/restructure-p2-6-capture-sus.cs</c> line for line.</summary>
        private List<string> Capture()
        {
            var rows = new List<string>();
            foreach (string name in new[] { "timing", "linked", "empty" })
            foreach (bool mirror in new[] { false, true })
            foreach (bool combine in new[] { false, true })
            {
                string text = name == "empty"
                    ? string.Empty
                    : Normalize(File.ReadAllText(Path.Combine(FixtureDirectory, name + ".sus")));
                MusicScore score = new SusRuntimeConverter().Convert(
                    text, _config, isNeedCombo: combine, _isMirror: mirror, showPair: combine);

                rows.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "CASE {0} mirror={1} combo={2} pair={3}", name, mirror, combine, combine));
                foreach (MusicScoreInfo frame in score.musicScoreInfoArray)
                {
                    rows.Add("FRAME " + Timing(frame));
                }
                foreach (EventBase musicEvent in score.EventArray)
                {
                    rows.Add("EVENT " + musicEvent.GetType().Name + "|" + Timing(musicEvent.MusicScoreInfo));
                }
                foreach (NoteBase root in score.NoteArray)
                {
                    rows.Add("ROOT " + Note(root));
                    if (root.NoteList == null)
                    {
                        continue;
                    }
                    foreach (NoteBase child in root.NoteList)
                    {
                        if (child != null && !ReferenceEquals(child, root))
                        {
                            rows.Add("CHILD " + Note(child));
                        }
                    }
                }
            }

            return rows;
        }

        /// <summary>
        /// The fixtures are committed with LF, but a Windows checkout may hand them over as CRLF. Line
        /// endings are not part of a chart, so both are fed to the importer as LF and the transcript
        /// stays comparable across checkouts.
        /// </summary>
        private static string Normalize(string text)
        {
            return text.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        [Test]
        public void TheImporterStillProducesTheCommittedTranscript()
        {
            List<string> actual = Capture();
            string expectedPath = Path.Combine(FixtureDirectory, "expected.txt");
            Assert.That(File.Exists(expectedPath), Is.True, expectedPath);
            string[] expected = File.ReadAllLines(expectedPath);

            string difference = DescribeFirstDifference(expected, actual);
            Assert.That(difference, Is.Null, difference);
            Assert.That(actual, Has.Count.EqualTo(expected.Length), "line count");
        }

        /// <summary>
        /// Returns null when both transcripts agree, otherwise the first differing line with a few
        /// lines of context so the failure names a concrete frame or note.
        /// </summary>
        private static string DescribeFirstDifference(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
        {
            int limit = Math.Min(expected.Count, actual.Count);
            for (int i = 0; i < limit; i++)
            {
                if (string.Equals(expected[i], actual[i], StringComparison.Ordinal))
                {
                    continue;
                }

                var report = new StringBuilder();
                report.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "SUS transcript differs at line {0} (1-based).", i + 1));
                report.AppendLine("expected: " + expected[i]);
                report.AppendLine("actual  : " + actual[i]);
                AppendContext(report, "expected", expected, i);
                AppendContext(report, "actual  ", actual, i);
                return report.ToString();
            }

            if (expected.Count == actual.Count)
            {
                return null;
            }

            var lengthReport = new StringBuilder();
            lengthReport.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "SUS transcript has {0} lines but {1} were expected.", actual.Count, expected.Count));
            if (actual.Count > limit)
            {
                lengthReport.AppendLine("first extra line: " + actual[limit]);
            }
            if (expected.Count > limit)
            {
                lengthReport.AppendLine("first missing line: " + expected[limit]);
            }
            return lengthReport.ToString();
        }

        private static void AppendContext(StringBuilder report, string label, IReadOnlyList<string> rows, int index)
        {
            for (int i = Math.Max(0, index - 3); i < index; i++)
            {
                report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}[{1}]: {2}", label, i + 1, rows[i]));
            }
        }
    }
}
