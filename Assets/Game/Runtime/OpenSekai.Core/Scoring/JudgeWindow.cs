using Sekai.Live;

namespace OpenSekai.Shared.Scoring
{
    /// <summary>
    /// The judge ladder: one offset and one window table in, one <see cref="NoteResult"/> out.
    ///
    /// Offsets are positive when the note is still ahead of the input (the player is early) and negative
    /// when it has passed (late), which is why the "after" bounds are compared with a negated value.
    ///
    /// The note type's timing table determines the grade. The center of the timing window only
    /// determines the timing description.
    /// </summary>
    public static class JudgeWindow
    {
        // Input admission uses open outer bounds. Grade bands include their endpoints.
        public static bool Accepts(JudgeTimeData data, float noteMinusInput) =>
            noteMinusInput < data.BadBeforeJudgeTime && noteMinusInput > -data.BadAfterJudgeTime;

        public static bool IsPast(JudgeTimeData data, float noteMinusNow) =>
            noteMinusNow < -data.BadAfterJudgeTime;

        public static (NoteResult, NoteResultDescription) ResultFor(
            JudgeTimeData data, float offset, bool isDirection, float onTimeWindowHalf)
        {
            var description = DescriptionFor(offset, onTimeWindowHalf);
            if (data.PerfectBeforeJudgeTime >= offset && -data.PerfectAfterJudgeTime <= offset)
            {
                return isDirection
                    ? (NoteResult.Perfect, description)
                    : (NoteResult.Great, NoteResultDescription.FlickMiss);
            }

            if (data.GreatBeforeJudgeTime >= offset && -data.GreatAfterJudgeTime <= offset)
            {
                return (NoteResult.Great, description);
            }

            if (data.GoodBeforeJudgeTime >= offset && -data.GoodAfterJudgeTime <= offset)
            {
                return (NoteResult.Good, description);
            }

            if (data.BadBeforeJudgeTime >= offset && -data.BadAfterJudgeTime <= offset)
            {
                return (NoteResult.Bad, description);
            }

            return (NoteResult.Miss, description);
        }

        /// <summary>Classifies an input's timing independently of the grade.</summary>
        public static NoteResultDescription DescriptionFor(float offset, float onTimeWindowHalf)
        {
            if (-onTimeWindowHalf <= offset && offset <= onTimeWindowHalf)
            {
                return NoteResultDescription.Just;
            }

            return offset > onTimeWindowHalf ? NoteResultDescription.Fast : NoteResultDescription.Late;
        }
    }
}
