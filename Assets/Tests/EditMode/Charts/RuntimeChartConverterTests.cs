using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenSekai.Charts;
using OpenSekai.Live.Conversion;
using Sekai;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Charts
{
    /// <summary>
    /// The converter is where the authored note categories turn into the concrete note classes the
    /// live scene ticks, so the mapping is asserted class by class instead of by note count alone.
    /// </summary>
    public class RuntimeChartConverterTests
    {
        private LiveBundleBuildData _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<LiveBundleBuildData>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
            {
                Object.DestroyImmediate(_config);
            }
        }

        private static ChartDocument Document(params ChartNote[] notes)
        {
            var document = new ChartDocument { MusicScoreTicksMax = 3840 };
            document.MusicScoreEventDataList.Add(new ChartEvent
            {
                id = 0, ticks = 0, eventType = ChartEventType.BPM, changeValue = 120f
            });
            document.NoteList.AddRange(notes);
            return document;
        }

        private static ChartNote Tap(int id, NoteCategory category, long ticks,
            ChartNote.NoteBaseType baseType = ChartNote.NoteBaseType.Normal)
        {
            return new ChartNote(id, ticks, 0, 0, category, NoteType.Default, 1f,
                NoteLineType.Linear, baseType);
        }

        private MusicScore Convert(ChartDocument document) => RuntimeChartConverter.Convert(document, _config, true);

        [Test]
        public void NormalTapBecomesNormalNote()
        {
            MusicScore score = Convert(Document(Tap(1, NoteCategory.Normal, 0)));

            Assert.That(score.NoteArray, Has.Length.EqualTo(1));
            Assert.That(score.NoteArray[0], Is.TypeOf<NormalNote>());
            Assert.That(score.NoteArray[0].Category, Is.EqualTo(NoteCategory.Normal));
        }

        [Test]
        public void FlickBecomesFlickNote()
        {
            MusicScore score = Convert(Document(
                Tap(1, NoteCategory.Flick, 0, ChartNote.NoteBaseType.Flick)));

            Assert.That(score.NoteArray[0], Is.TypeOf<FlickNote>());
        }

        [Test]
        public void FrictionBecomesFrictionNote()
        {
            MusicScore score = Convert(Document(
                Tap(1, NoteCategory.Friction, 0, ChartNote.NoteBaseType.Friction)));

            Assert.That(score.NoteArray[0], Is.TypeOf<FrictionNote>());
        }

        [Test]
        public void FrictionFlickStaysAFrictionFlickNote()
        {
            MusicScore score = Convert(Document(
                Tap(1, NoteCategory.FrictionFlick, 0, ChartNote.NoteBaseType.FrictionFlick)));

            Assert.That(score.NoteArray[0], Is.TypeOf<FrictionFlickNote>());
        }

        [Test]
        public void PairedLongBecomesOneRootWithAHoldChild()
        {
            var start = new ChartNote(1, 0, 0, 0, NoteCategory.Long, NoteType.Default, 1f,
                NoteLineType.Linear, ChartNote.NoteBaseType.Long, isSkip: false,
                direction: NoteDirection.Default, previousConnectionId: -1, nextConnectionId: 2);
            var end = new ChartNote(2, 1920, 0, 0, NoteCategory.Long, NoteType.Default, 1f,
                NoteLineType.Linear, ChartNote.NoteBaseType.Long, isSkip: false,
                direction: NoteDirection.Default, previousConnectionId: 1, nextConnectionId: -1);

            MusicScore score = Convert(Document(start, end));

            Assert.That(score.NoteArray, Has.Length.EqualTo(1), "a long note is one root");
            NoteBase root = score.NoteArray[0];
            Assert.That(root, Is.TypeOf<LongNote>());
            Assert.That(root.NoteList.Count, Is.GreaterThanOrEqualTo(2), "root plus the hold body");
        }

        [Test]
        public void ConnectionNotesJoinTheLongRoot()
        {
            var start = new ChartNote(1, 0, 0, 0, NoteCategory.Long, NoteType.Default, 1f,
                NoteLineType.Linear, ChartNote.NoteBaseType.Long, isSkip: false,
                direction: NoteDirection.Default, previousConnectionId: -1, nextConnectionId: 2);
            var connection = new ChartNote(2, 960, 0, 0, NoteCategory.Connection, NoteType.Default, 1f,
                NoteLineType.Linear, ChartNote.NoteBaseType.Connection, isSkip: false,
                direction: NoteDirection.Default, previousConnectionId: 1, nextConnectionId: 3);
            var end = new ChartNote(3, 1920, 0, 0, NoteCategory.Long, NoteType.Default, 1f,
                NoteLineType.Linear, ChartNote.NoteBaseType.Long, isSkip: false,
                direction: NoteDirection.Default, previousConnectionId: 2, nextConnectionId: -1);

            MusicScore score = Convert(Document(start, connection, end));

            Assert.That(score.NoteArray, Has.Length.EqualTo(1));
            Assert.That(score.NoteArray[0].NoteList.Count, Is.GreaterThanOrEqualTo(3),
                "start, connection and end belong to one root");
        }

        [Test]
        public void DecorationNotesDoNotCountTowardsTheCombo()
        {
            var decoration = new ChartNote(1, 0, 0, 0, NoteCategory.Normal, NoteType.Default, 1f,
                NoteLineType.Linear, ChartNote.NoteBaseType.Normal, isSkip: true,
                direction: NoteDirection.Default, previousConnectionId: -1, nextConnectionId: -1,
                isDecoration: true);
            MusicScore score = Convert(Document(decoration, Tap(2, NoteCategory.Normal, 480)));

            Assert.That(LiveUtility.CalculateTotalComboCount(score), Is.EqualTo(1),
                "only the plain tap is judged");
        }

        [Test]
        public void CriticalNotesKeepTheirType()
        {
            var critical = new ChartNote(1, 0, 0, 0, NoteCategory.Normal, NoteType.Critical);

            MusicScore score = Convert(Document(critical));

            Assert.That(score.NoteArray[0].Type, Is.EqualTo(NoteType.Critical));
        }

        [Test]
        public void TimelineCarriesTheBpmEvent()
        {
            MusicScore score = Convert(Document(Tap(1, NoteCategory.Normal, 0)));

            Assert.That(score.musicScoreInfoArray, Is.Not.Null.And.Length.GreaterThanOrEqualTo(1));
            Assert.That(score.musicScoreInfoArray[0].bpm, Is.EqualTo(120f).Within(0.001f));
        }

        [Test]
        public void EmptyChartProducesAnEmptyScore()
        {
            MusicScore score = Convert(new ChartDocument());

            Assert.That(LiveUtility.CalculateTotalComboCount(score), Is.EqualTo(0));
        }

        [Test]
        public void OnlyTheHeadOfAChainBecomesARoot()
        {
            var head = new ChartNote(1, 0, 0, 0, NoteCategory.Long, NoteType.Default, 1f,
                NoteLineType.Linear, ChartNote.NoteBaseType.Long, isSkip: false,
                direction: NoteDirection.Default, previousConnectionId: -1, nextConnectionId: 2);
            var middle = new ChartNote(2, 960, 0, 0, NoteCategory.Connection, NoteType.Default, 1f,
                NoteLineType.Linear, ChartNote.NoteBaseType.Connection, isSkip: false,
                direction: NoteDirection.Default, previousConnectionId: 1, nextConnectionId: 3);
            var tail = new ChartNote(3, 1920, 0, 0, NoteCategory.Long, NoteType.Default, 1f,
                NoteLineType.Linear, ChartNote.NoteBaseType.Long, isSkip: false,
                direction: NoteDirection.Default, previousConnectionId: 2, nextConnectionId: -1);

            MusicScore score = Convert(Document(head, middle, tail));

            Assert.That(score.NoteArray, Has.Length.EqualTo(1), "the chain has exactly one root");
            Assert.That(score.NoteArray[0].NoteList, Has.Count.EqualTo(3),
                "head, connection and tail all hang off that root");
        }

        [Test]
        public void ChildrenFollowTheirTicks()
        {
            var head = new ChartNote(1, 0, 0, 0, NoteCategory.Long, NoteType.Default, 1f,
                NoteLineType.Linear, ChartNote.NoteBaseType.Long, isSkip: false,
                direction: NoteDirection.Default, previousConnectionId: -1, nextConnectionId: 2);
            var tail = new ChartNote(2, 1920, 0, 0, NoteCategory.Long, NoteType.Default, 1f,
                NoteLineType.Linear, ChartNote.NoteBaseType.Long, isSkip: false,
                direction: NoteDirection.Default, previousConnectionId: 1, nextConnectionId: -1);

            MusicScore score = Convert(Document(head, tail));

            NoteBase root = score.NoteArray[0];
            Assert.That(root.NoteList[0].MusicScoreInfo.time, Is.LessThan(root.NoteList[1].MusicScoreInfo.time),
                "the hold body comes after the head");
        }

        [Test]
        public void AnOrphanedTailNeverBecomesARoot()
        {
            var orphan = new ChartNote(2, 960, 0, 0, NoteCategory.Long, NoteType.Default, 1f,
                NoteLineType.Linear, ChartNote.NoteBaseType.Long, isSkip: false,
                direction: NoteDirection.Default, previousConnectionId: 1, nextConnectionId: -1);

            MusicScore score = Convert(Document(orphan));

            Assert.That(score.NoteArray, Is.Empty,
                "a note whose previousConnectionId points at nothing is not a head and must not tick");
        }

        [Test]
        public void ADanglingNextIdStillProducesARoot()
        {
            var head = new ChartNote(1, 0, 0, 0, NoteCategory.Long, NoteType.Default, 1f,
                NoteLineType.Linear, ChartNote.NoteBaseType.Long, isSkip: false,
                direction: NoteDirection.Default, previousConnectionId: -1, nextConnectionId: 99);

            MusicScore score = Convert(Document(head));

            Assert.That(score.NoteArray, Has.Length.EqualTo(1), "a missing link ends the chain, it is not fatal");
            Assert.That(score.NoteArray[0].NoteList, Has.Count.EqualTo(1));
        }

        [Test]
        public void ACyclicChainIsRejected()
        {
            var first = new ChartNote(1, 0, 0, 0, NoteCategory.Long, NoteType.Default, 1f,
                NoteLineType.Linear, ChartNote.NoteBaseType.Long, isSkip: false,
                direction: NoteDirection.Default, previousConnectionId: -1, nextConnectionId: 2);
            var second = new ChartNote(2, 960, 0, 0, NoteCategory.Long, NoteType.Default, 1f,
                NoteLineType.Linear, ChartNote.NoteBaseType.Long, isSkip: false,
                direction: NoteDirection.Default, previousConnectionId: 1, nextConnectionId: 1);

            InvalidDataException error = Assert.Throws<InvalidDataException>(() => Convert(Document(first, second)));

            Assert.That(error.Message, Does.Contain("循环"), "a loop in the note graph is rejected");
        }

        [Test]
        public void AFrictionLongPairStaysAnAudibleRub()
        {
            var head = new ChartNote(1, 0, 0, 0, NoteCategory.FrictionLong, NoteType.Default, 1f,
                NoteLineType.Linear, ChartNote.NoteBaseType.FrictionLong, isSkip: false,
                direction: NoteDirection.Default, previousConnectionId: -1, nextConnectionId: 2);
            var tail = new ChartNote(2, 1920, 0, 0, NoteCategory.FrictionLong, NoteType.Default, 1f,
                NoteLineType.Linear, ChartNote.NoteBaseType.FrictionLong, isSkip: false,
                direction: NoteDirection.Default, previousConnectionId: 1, nextConnectionId: -1);

            MusicScore score = Convert(Document(head, tail));

            // A rub is meant to be felt and heard: it keeps its own judgement. Collapsing it into the
            // hidden carrier erased the difference between a 摩擦 bar and a 隐藏 bar and left the rub
            // with nothing to publish, so no sound and no tail key.
            Assert.That(score.NoteArray.OfType<FrictionHideLongNote>(), Is.Empty, "a plain rub is not a hidden carrier");
            var rub = score.NoteArray.OfType<FrictionLongNote>().FirstOrDefault();
            Assert.That(rub, Is.Not.Null, "the friction long pair stays a friction long");
            Assert.That(rub.HasJudgment, Is.True, "the rub answers input");
            Assert.That(rub.ChildNote, Is.Not.Null);
            Assert.That(((NoteBase)rub.ChildNote).HasJudgment, Is.True, "and its end is a note the player answers");
        }

        [Test]
        public void AHiddenFrictionPairStaysASilentCarrier()
        {
            var head = new ChartNote(1, 0, 0, 0, NoteCategory.FrictionHideLong, NoteType.Default, 1f,
                NoteLineType.Linear, ChartNote.NoteBaseType.FrictionHideLong, isSkip: false,
                direction: NoteDirection.Default, previousConnectionId: -1, nextConnectionId: 2);
            var tail = new ChartNote(2, 1920, 0, 0, NoteCategory.FrictionHideLong, NoteType.Default, 1f,
                NoteLineType.Linear, ChartNote.NoteBaseType.FrictionHideLong, isSkip: false,
                direction: NoteDirection.Default, previousConnectionId: 1, nextConnectionId: -1);

            MusicScore score = Convert(Document(head, tail));

            var carrier = score.NoteArray.OfType<FrictionHideLongNote>().FirstOrDefault();
            Assert.That(carrier, Is.Not.Null, "the hidden pair keeps its carrier");
            Assert.That(carrier.HasJudgment, Is.False, "a hidden carrier grades nothing");
        }
    }
}
