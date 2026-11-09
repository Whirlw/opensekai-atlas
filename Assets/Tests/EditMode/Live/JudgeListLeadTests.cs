using NUnit.Framework;
using OpenSekai.Shared.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    [TestFixture]
    public class JudgeListLeadTests
    {
        [Test]
        public void TheLeadIsLongerThanTheWidestJudgeWindow()
        {
            // LiveConfig.JudgeTimeAfter is the widest bad window and sits around 125-142 ms at 60 fps.
            Assert.That(JudgeListLead.Seconds, Is.GreaterThan(0.2f));
        }

        [Test]
        public void ANoteExactlyOneLeadAwayIsStillTooFar()
        {
            Assert.That(JudgeListLead.IsStillTooFarAhead(0f, JudgeListLead.Seconds), Is.True);
        }

        [Test]
        public void ANoteInsideTheLeadIsScannable()
        {
            Assert.That(JudgeListLead.IsStillTooFarAhead(0f, JudgeListLead.Seconds - 0.01f), Is.False);
        }

        [Test]
        public void ANoteInThePastIsNeverTooFar()
        {
            Assert.That(JudgeListLead.IsStillTooFarAhead(10f, 9f), Is.False);
        }

        [Test]
        public void NaNTimesAreTreatedAsNotTooFar()
        {
            Assert.That(JudgeListLead.IsStillTooFarAhead(float.NaN, 1f), Is.False);
            Assert.That(JudgeListLead.IsStillTooFarAhead(1f, float.NaN), Is.False);
        }

        [Test]
        public void TheEntryMomentDoesNotDependOnTheScrollSpeed()
        {
            // The regression this guards: the scanner used the display lead, which divides by the
            // speed ratio, so a note on a fast section entered the judgment list much later than an
            // identical note on a slow one. After the split, entry depends on time alone.
            const float noteTime = 2f;
            const float displayWindow = 0.5f;

            bool negativeSlow = false;
            bool negativeFast = false;
            float slowDisplayLead = NoteDisplayOffset.Resolve(displayWindow, 0.5f, 1f, ref negativeSlow);
            float fastDisplayLead = NoteDisplayOffset.Resolve(displayWindow, 4f, 1f, ref negativeFast);

            float slowEntryWithDisplayLead = EntryTime(noteTime, slowDisplayLead);
            float fastEntryWithDisplayLead = EntryTime(noteTime, fastDisplayLead);
            Assert.That(fastEntryWithDisplayLead, Is.GreaterThan(slowEntryWithDisplayLead + 0.1f),
                "the display lead really does shift entry by a lot at high speed");

            float fastEntryWithJudgeableLead = EntryTime(noteTime, JudgeListLead.Seconds);
            float slowEntryWithJudgeableLead = EntryTime(noteTime, JudgeListLead.Seconds);
            Assert.That(fastEntryWithJudgeableLead, Is.EqualTo(slowEntryWithJudgeableLead));
            Assert.That(fastEntryWithJudgeableLead, Is.LessThan(fastEntryWithDisplayLead),
                "a fast note must enter the list earlier than its display lead would allow");
        }

        /// <summary>
        /// The first frame time at which the scan would accept a note at <paramref name="noteTime"/>,
        /// stepping forward the way the per-frame scan does.
        /// </summary>
        private static float EntryTime(float noteTime, float leadSeconds)
        {
            for (float now = noteTime - 1f; now <= noteTime; now += 0.005f)
            {
                if (!JudgeListLead.IsStillTooFarAhead(now, noteTime, leadSeconds))
                {
                    return now;
                }
            }

            return float.PositiveInfinity;
        }
    }
}
