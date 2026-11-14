using NUnit.Framework;
using OpenSekai.Charts;
using OpenSekai.Charts.Serialization;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Charts
{
    /// <summary>
    /// The wire format is shared with charts authored by the old editor, so these tests pin both
    /// directions: what we write today, and the legacy names a stored chart may still carry.
    /// </summary>
    public class ChartJsonTests
    {
        private const string LegacyDocumentType = "Sekai.MusicScoreMaker.Ingame.Models.MusicScoreMakerData";
        private const string LegacyNoteType = "Sekai.MusicScoreMaker.Ingame.Models.MusicScoreNoteBase";

        private static ChartDocument SampleDocument()
        {
            var document = new ChartDocument { MusicId = 42, MusicScoreTicksMax = 3840 };
            document.NoteList.Add(new ChartNote(1, 480, 0, 0, NoteCategory.Normal));
            document.NoteList.Add(new ChartNote(2, 960, 1, 3, NoteCategory.Flick, NoteType.Critical));
            document.NoteList.Add(new ChartNote(3, 1440, 2, 2, NoteCategory.FrictionLong, NoteType.Default,
                noteBaseType: ChartNote.NoteBaseType.FrictionLong, nextConnectionId: 4));
            return document;
        }

        [Test]
        public void ReadReturnsNullForMissingPayload()
        {
            Assert.That(ChartJson.Read(null), Is.Null);
            Assert.That(ChartJson.Read(string.Empty), Is.Null);
        }

        [Test]
        public void RoundTripKeepsNotesAndTheirFields()
        {
            ChartDocument source = SampleDocument();

            string json = ChartJson.Write(source);
            ChartDocument loaded = ChartJson.Read(json);

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.NoteList.Count, Is.EqualTo(3));
            Assert.That(loaded.MusicScoreTicksMax, Is.EqualTo(3840));
            Assert.That(loaded.NoteList[1].category, Is.EqualTo(NoteCategory.Flick));
            Assert.That(loaded.NoteList[1].type, Is.EqualTo(NoteType.Critical));
            Assert.That(loaded.NoteList[2].noteBaseType, Is.EqualTo(ChartNote.NoteBaseType.FrictionLong));
            Assert.That(loaded.NoteList[2].nextConnectionId, Is.EqualTo(4));
        }

        [Test]
        public void ReadAppliesMusicIdOverride()
        {
            string json = ChartJson.Write(SampleDocument());

            ChartDocument loaded = ChartJson.Read(json, musicId: 999);

            Assert.That(loaded.MusicId, Is.EqualTo(999));
        }

        [Test]
        public void ReadMigratesWhenLoading()
        {
            var document = new ChartDocument { VersionCode = 0 };
            document.NoteList.Add(new ChartNote(1, 0, 0, 0, NoteCategory.Normal));

            ChartDocument loaded = ChartJson.Read(ChartJson.Write(document));

            Assert.That(loaded.VersionCode, Is.EqualTo(ChartDocument.CURRENT_VERSION));
        }

        [Test]
        public void LegacyDocumentTypeNameIsAccepted()
        {
            const string json = "{\"$type\":\"" + LegacyDocumentType + "\",\"VersionCode\":1,\"MusicId\":5," +
                                "\"NoteList\":[{\"id\":1,\"ticks\":480,\"laneStart\":0,\"laneEnd\":0}]}";

            ChartDocument loaded = ChartJson.Read(json);

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.MusicId, Is.EqualTo(5));
            Assert.That(loaded.NoteList.Count, Is.EqualTo(1));
            Assert.That(loaded.NoteList[0].ticks, Is.EqualTo(480));
        }

        [Test]
        public void LegacyNoteTypeNameIsAccepted()
        {
            const string json = "{\"VersionCode\":1,\"NoteList\":[{\"$id\":\"note\",\"$type\":\"" + LegacyNoteType +
                                ", Assembly-CSharp\",\"id\":9,\"ticks\":960,\"laneStart\":1,\"laneEnd\":1,\"category\":3}," +
                                "{\"$ref\":\"note\"}]}";
            ChartDocument loaded = ChartJson.Read(json);
            Assert.That(loaded.NoteList.Count, Is.EqualTo(2));
            Assert.That(loaded.NoteList[0].id, Is.EqualTo(9));
            Assert.That(loaded.NoteList[0].category, Is.EqualTo(NoteCategory.Flick));
            Assert.That(loaded.NoteList[1], Is.SameAs(loaded.NoteList[0]));

            string written = ChartJson.Write(loaded);
            Assert.That(written, Does.Contain("\"$id\"").And.Contain("\"$ref\""));
            ChartDocument reread = ChartJson.Read(written);
            Assert.That(reread.NoteList[1], Is.SameAs(reread.NoteList[0]));
        }
    }
}
