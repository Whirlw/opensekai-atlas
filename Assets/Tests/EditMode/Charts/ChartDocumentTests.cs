using System.Collections.Generic;
using NUnit.Framework;
using OpenSekai.Charts;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Charts
{
    /// <summary>
    /// Locks down the hand-authored chart model: what survives a copy, what a legacy document looks
    /// like after migration, and when a chart counts as playable.
    /// </summary>
    public class ChartDocumentTests
    {
        private static ChartNote Note(int id, long ticks) =>
            new ChartNote(id, ticks, 0, 0, NoteCategory.Normal);

        private static ChartDocument DocumentWithTwoNotes()
        {
            var document = new ChartDocument { MusicId = 7, MusicScoreTicksMax = 3840 };
            document.NoteList.Add(Note(1, 960));
            document.NoteList.Add(Note(2, 1920));
            document.MusicScoreEventDataList.Add(new ChartEvent
            {
                ticks = 960, eventType = ChartEventType.BPM, changeValue = 120f
            });
            return document;
        }

        [Test]
        public void CopyProducesAnIndependentDocument()
        {
            ChartDocument source = DocumentWithTwoNotes();

            ChartDocument copy = source.Copy();

            Assert.That(copy, Is.Not.SameAs(source));
            Assert.That(copy.NoteList, Is.Not.SameAs(source.NoteList));
            Assert.That(copy.NoteList[0], Is.Not.SameAs(source.NoteList[0]));
            Assert.That(copy.MusicScoreEventDataList, Is.Not.SameAs(source.MusicScoreEventDataList));
            Assert.That(copy.MusicScoreEventDataList[0], Is.Not.SameAs(source.MusicScoreEventDataList[0]));
        }

        [Test]
        public void CopyPreservesScalarsAndNoteValues()
        {
            ChartDocument source = DocumentWithTwoNotes();

            ChartDocument copy = source.Copy();

            Assert.That(copy.MusicId, Is.EqualTo(7));
            Assert.That(copy.MusicScoreTicksMax, Is.EqualTo(3840));
            Assert.That(copy.VersionCode, Is.EqualTo(ChartDocument.CURRENT_VERSION));
            Assert.That(copy.NoteList.Count, Is.EqualTo(2));
            Assert.That(copy.NoteList[1].id, Is.EqualTo(2));
            Assert.That(copy.NoteList[1].ticks, Is.EqualTo(1920));
        }

        [Test]
        public void MutatingTheCopyLeavesTheSourceAlone()
        {
            ChartDocument source = DocumentWithTwoNotes();
            ChartDocument copy = source.Copy();

            copy.NoteList[0].ticks = 1;
            copy.NoteList.Add(Note(3, 2880));

            Assert.That(source.NoteList[0].ticks, Is.EqualTo(960));
            Assert.That(source.NoteList.Count, Is.EqualTo(2));
        }

        [Test]
        public void CopyToleratesNullLists()
        {
            var source = new ChartDocument { NoteList = null, MusicScoreEventDataList = null, EventArray = null };

            ChartDocument copy = source.Copy();

            Assert.That(copy.NoteList, Is.Not.Null.And.Empty);
            Assert.That(copy.MusicScoreEventDataList, Is.Not.Null.And.Empty);
            Assert.That(copy.EventArray, Is.Not.Null.And.Empty);
        }

        [Test]
        public void HasPlayableNotesNeedsAtLeastOneNote()
        {
            Assert.That(new ChartDocument().HasPlayableNotes, Is.False);

            var document = new ChartDocument();
            document.NoteList.Add(null);
            Assert.That(document.HasPlayableNotes, Is.False, "a null entry is not a note");

            document.NoteList.Add(Note(1, 0));
            Assert.That(document.HasPlayableNotes, Is.True);
        }

        [Test]
        public void MigrateStampsTheCurrentVersion()
        {
            var document = new ChartDocument { VersionCode = 0 };

            document.Migrate();

            Assert.That(document.VersionCode, Is.EqualTo(ChartDocument.CURRENT_VERSION));
        }

        [Test]
        public void MigrateDropsNullEvents()
        {
            var document = new ChartDocument();
            document.EventArray = new ChartPlaybackEvent[]
            {
                new ChartPlaybackEvent { MusicScoreInfo = new MusicScoreInfo(0, 0f, 0f, 120f, 4f, 1f, 1f) },
                null,
                new ChartPlaybackEvent { MusicScoreInfo = new MusicScoreInfo(1, 0f, 2f, 120f, 4f, 1f, 1f) }
            };

            document.Migrate();

            Assert.That(document.EventArray.Length, Is.EqualTo(2));
            Assert.That(document.EventArray[0].IsSkillOrFever, Is.False);
            Assert.That(document.EventArray[0].MusicScoreInfo.time, Is.EqualTo(0f).Within(0.001f));
            Assert.That(document.EventArray[1].MusicScoreInfo.time, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void MigrateIsIdempotent()
        {
            ChartDocument document = DocumentWithTwoNotes();

            document.Migrate();
            string firstPass = ChartDocumentSnapshot(document);
            document.Migrate();

            Assert.That(ChartDocumentSnapshot(document), Is.EqualTo(firstPass));
        }

        private static string ChartDocumentSnapshot(ChartDocument document)
        {
            var parts = new List<string> { document.VersionCode.ToString(), document.NoteList.Count.ToString() };
            foreach (ChartNote note in document.NoteList)
            {
                parts.Add($"{note.id}@{note.ticks}");
            }

            return string.Join("|", parts);
        }
    }
}
