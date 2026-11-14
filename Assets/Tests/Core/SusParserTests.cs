using System.IO;
using NUnit.Framework;
using OpenSekai.Shared.Conversion;
using Sekai.Live;
using Sekai.SUS;

namespace OpenSekai.Tests.Core
{
    /// <summary>
    /// The SUS parser is the pure half of the importer: text in, DTO tables out, no engine involved.
    /// These tests pin the parts of the format the game actually relies on, so the runtime side can be
    /// changed without wondering whether the tables still mean the same thing.
    /// </summary>
    public class SusParserTests
    {
        private const string FixtureDirectory = "Tools/fixtures/sus-p2-6";

        private static SusChartData ParseFixture(string name)
        {
            string path = Path.Combine(FixtureDirectory, name + ".sus");
            Assert.That(File.Exists(path), Is.True, "fixture not found: " + path);
            return new SusParser().Parse(File.ReadAllText(path));
        }

        [Test]
        public void TicksPerBeatComesFromTheRequestRow()
        {
            SusChartData chart = ParseFixture("timing");

            Assert.That(chart.TicksPerBeat, Is.EqualTo(240));
        }

        [Test]
        public void TimeSignatureAndBpmRowsLandInTheirOwnTables()
        {
            SusChartData chart = ParseFixture("timing");

            Assert.That(chart.TimeSignatures, Has.Count.EqualTo(2));
            Assert.That(chart.TimeSignatures[0].StartBarIndex, Is.EqualTo(0));
            Assert.That(chart.TimeSignatures[0].TimeSignature, Is.EqualTo(4f).Within(0.001f));
            Assert.That(chart.TimeSignatures[1].StartBarIndex, Is.EqualTo(1));
            Assert.That(chart.TimeSignatures[1].TimeSignature, Is.EqualTo(3f).Within(0.001f));

            Assert.That(chart.BpmTable["01"], Is.EqualTo(120f).Within(0.001f));
            Assert.That(chart.BpmTable["02"], Is.EqualTo(180f).Within(0.001f));

            Assert.That(chart.BpmInfos, Has.Count.EqualTo(2));
            Assert.That(chart.BpmInfos[1].bar, Is.EqualTo(1));
            Assert.That(chart.BpmInfos[1].barProgress, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(chart.BpmInfos[1].bpm, Is.EqualTo(180f).Within(0.001f));
        }

        [Test]
        public void HighSpeedAndVolumeRowsKeepTheirTickOffsets()
        {
            SusChartData chart = ParseFixture("timing");

            Assert.That(chart.HighSpeedInfos, Has.Count.EqualTo(2));
            Assert.That(chart.HighSpeedInfos[0].BarIndex, Is.EqualTo(0));
            Assert.That(chart.HighSpeedInfos[0].TickOffset, Is.EqualTo(120));
            Assert.That(chart.HighSpeedInfos[0].SpeedRatio, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(chart.HighSpeedInfos[1].BarIndex, Is.EqualTo(1));
            Assert.That(chart.HighSpeedInfos[1].TickOffset, Is.EqualTo(360));
            Assert.That(chart.HighSpeedInfos[1].SpeedRatio, Is.EqualTo(-0.5f).Within(0.001f));

            Assert.That(chart.VolumeInfos, Has.Count.EqualTo(2));
            Assert.That(chart.VolumeInfos[0].BarIndex, Is.EqualTo(0));
            Assert.That(chart.VolumeInfos[0].TickOffset, Is.EqualTo(240));
            Assert.That(chart.VolumeInfos[0].Volume, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(chart.VolumeInfos[1].Volume, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void FeverAndSkillRowsBecomeEvents()
        {
            SusChartData chart = ParseFixture("timing");

            var kinds = new System.Collections.Generic.List<EventType>();
            foreach (var list in chart.EventInfos.Values)
            {
                foreach (EventInfo info in list)
                {
                    kinds.Add(info.EventType);
                }
            }

            Assert.That(kinds, Does.Contain(EventType.Skill));
            Assert.That(kinds, Does.Contain(EventType.FeverBegin));
            Assert.That(kinds, Does.Contain(EventType.FeverStart));
        }

        /// <summary>
        /// A second row on the same bar position and lane overlays the first instead of adding a note:
        /// the category latches, while the long number is taken from the later row. Official charts
        /// depend on this to turn a plain flick into the tail of a hold.
        /// </summary>
        [Test]
        public void ASecondRowOnTheSamePositionOverlaysTheFirst()
        {
            SusChartData chart = ParseFixture("linked");

            NoteInfo note = chart.NoteInfos[2f][4];

            Assert.That(note.Category, Is.EqualTo(NoteCategory.Flick), "the flick category latches");
            Assert.That(note.Direction, Is.EqualTo(NoteDirection.Right), "its direction survives the overlay");
            Assert.That(note.LongNo, Is.EqualTo(1), "the later row attaches it to the hold");
            Assert.That(note.Width, Is.EqualTo(2));
        }

        /// <summary>
        /// A guide row carries no shape of its own; it takes whatever the note row it decorates asked
        /// for, so a guide over a right flick stays a right-facing decoration.
        /// </summary>
        /// <remarks>
        /// The order matters and is worth knowing: a guide row that comes first is filled in by the note
        /// row afterwards through <see cref="NoteInfo.Update"/>, which also takes the note's category
        /// over. Charts put the note row first, which is the case pinned here.
        /// </remarks>
        [Test]
        public void GuideRowsInheritShapeFromTheNoteAtTheSamePosition()
        {
            const string text = "#BPM01:120\n#00052:42\n#000921:11\n";

            SusChartData chart = new SusParser().Parse(text);

            NoteInfo guide = chart.GuideInfos[0f][0];
            Assert.That(guide.Category, Is.EqualTo(NoteCategory.Guide));
            Assert.That(guide.Direction, Is.EqualTo(NoteDirection.Right), "inherited from the flick row");
        }

        [Test]
        public void EmptyRowsAndLanesOutsideTheBoardAreDropped()
        {
            // First row is all "00" placeholders; second puts a width-2 note on lane d, which would run
            // past the 13th column.
            const string text = "#BPM01:120\n#00012:0000\n#0001d:12\n";

            SusChartData chart = new SusParser().Parse(text);

            Assert.That(chart.NoteInfos, Is.Empty, "no note row here describes a playable note");
        }

        [Test]
        public void AnEmptyChartParsesToEmptyTables()
        {
            SusChartData chart = new SusParser().Parse(string.Empty);

            Assert.That(chart.NoteInfos, Is.Empty);
            Assert.That(chart.EventInfos, Is.Empty);
            Assert.That(chart.TicksPerBeat, Is.EqualTo(SusChartData.DefaultTicksPerBeat));
        }
    }
}
