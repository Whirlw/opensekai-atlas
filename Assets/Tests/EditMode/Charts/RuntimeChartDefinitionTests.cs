using NUnit.Framework;
using OpenSekai.Charts;
using OpenSekai.Live.Conversion;
using Sekai;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Charts
{
    /// <summary>
    /// The contract that sits between the authored chart and the live scene: it owns the converted
    /// score, the judged note count and the "is there anything to play" flag the library relies on.
    /// </summary>
    public class RuntimeChartDefinitionTests
    {
        private static MusicScoreInfo Info() => new MusicScoreInfo(0, 0f, 0f, 120f, 4f, 1f, 1f);

        private static ChartDocument DocumentWithNotes(int noteCount, bool hasTicksMax = false)
        {
            var document = new ChartDocument();
            document.MusicScoreEventDataList.Add(new ChartEvent
            {
                id = 0, ticks = 0, eventType = ChartEventType.BPM, changeValue = 120f
            });
            for (int i = 0; i < noteCount; i++)
            {
                document.NoteList.Add(new ChartNote(i + 1, i * 480L, 0, 0, NoteCategory.Normal));
            }

            if (hasTicksMax)
            {
                document.MusicScoreTicksMax = 1920;
            }

            return document;
        }

        private static MusicScore ScoreWithNotes(int noteCount)
        {
            MusicScoreInfo info = Info();
            var roots = new NoteBase[noteCount];
            for (int i = 0; i < noteCount; i++)
            {
                roots[i] = new NoteBase(info, i + 1, 0, 0, NoteCategory.Normal, 1f, 0f);
            }

            return new MusicScore(new[] { info }, new EventBase[0], roots);
        }

        [Test]
        public void NullDocumentAndScoreProduceAnEmptyDefinition()
        {
            RuntimeChartDefinition definition = RuntimeChartDefinition.Create(null, null);

            Assert.That(definition.Document, Is.Null);
            Assert.That(definition.Score, Is.Null);
            Assert.That(definition.TotalNoteCount, Is.EqualTo(0));
            Assert.That(definition.HasPlayableNotes, Is.False);
            Assert.That(definition.TicksMax, Is.EqualTo(0L));
        }

        [Test]
        public void DocumentWithoutScoreHasNoPlayableNotesButKeepsTicksMax()
        {
            RuntimeChartDefinition definition =
                RuntimeChartDefinition.Create(DocumentWithNotes(2, hasTicksMax: true), null);

            Assert.That(definition.TotalNoteCount, Is.EqualTo(0));
            Assert.That(definition.HasPlayableNotes, Is.False);
            Assert.That(definition.TicksMax, Is.EqualTo(1920L));
        }

        [Test]
        public void ScoreWithoutDocumentIsNotPlayable()
        {
            RuntimeChartDefinition definition = RuntimeChartDefinition.Create(null, ScoreWithNotes(3));

            Assert.That(definition.TotalNoteCount, Is.EqualTo(3));
            Assert.That(definition.HasPlayableNotes, Is.False, "a chart with no document cannot be played");
        }

        [Test]
        public void DocumentWithoutNotesIsNotPlayable()
        {
            RuntimeChartDefinition definition = RuntimeChartDefinition.Create(DocumentWithNotes(0), ScoreWithNotes(0));

            Assert.That(definition.TotalNoteCount, Is.EqualTo(0));
            Assert.That(definition.HasPlayableNotes, Is.False);
        }

        [Test]
        public void MatchingDocumentAndScoreArePlayable()
        {
            RuntimeChartDefinition definition = RuntimeChartDefinition.Create(DocumentWithNotes(4), ScoreWithNotes(4));

            Assert.That(definition.TotalNoteCount, Is.EqualTo(4));
            Assert.That(definition.HasPlayableNotes, Is.True);
        }

        [Test]
        public void ConverterProducesTheSameNoteCountAsTheDocument()
        {
            ChartDocument document = DocumentWithNotes(5);
            LiveBundleBuildData config = ScriptableObject.CreateInstance<LiveBundleBuildData>();

            try
            {
                MusicScore score = RuntimeChartConverter.Convert(document, config, true);
                RuntimeChartDefinition definition = RuntimeChartDefinition.Create(document, score);

                Assert.That(score, Is.Not.Null);
                Assert.That(definition.TotalNoteCount, Is.EqualTo(5));
                Assert.That(definition.HasPlayableNotes, Is.True);
                Assert.That(definition.Document, Is.SameAs(document));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
