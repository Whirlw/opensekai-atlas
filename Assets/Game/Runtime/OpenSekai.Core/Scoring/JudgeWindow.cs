using Sekai.Live;

namespace OpenSekai.Shared.Scoring
{
    /// <summary>
    /// The judge ladder: one offset and one window table in, one <see cref="NoteResult"/> out.
    ///
    /// Offsets are positive when the note is still ahead of the input (the player is early) and negative
    /// when it has passed (late), which is why the "after" bounds are compared with a negated value.
    ///
    /// The order of the branches is the behaviour: JustPerfect is a global window that applies to every
    /// note type, then the per-type Perfect/Great/Good/Bad windows, then Miss.
    /// </summary>
    public static class JudgeWindow
    {
        // Input admission uses open outer bounds. Grade bands include their endpoints.
        public static bool Accepts(JudgeTimeData data, float noteMinusInput) =>
            noteMinusInput < data.BadBeforeJudgeTime && noteMinusInput > -data.BadAfterJudgeTime;

        public static bool IsPast(JudgeTimeData data, float noteMinusNow) =>
            noteMinusNow < -data.BadAfterJudgeTime;

        public static (NoteResult, NoteResultDescription) ResultFor(
            JudgeTimeData data, float offset, bool isDirection, float justPerfectHalf)
        {
            if (isDirection)
            {
                if (-justPerfectHalf <= offset && justPerfectHalf >= offset)
                {
                    return (NoteResult.JustPerfect, DescriptionFor(offset, justPerfectHalf));
                }

                if (data.PerfectBeforeJudgeTime >= offset && -data.PerfectAfterJudgeTime <= offset)
                {
                    return (NoteResult.Perfect, DescriptionFor(offset, justPerfectHalf));
                }
            }
            else if (data.PerfectBeforeJudgeTime >= offset && -data.PerfectAfterJudgeTime <= offset)
            {
                // A flick whose direction was wrong still lands inside the perfect window, but it is not a
                // perfect: the result reports it as a direction miss so the effect can say so.
                return (NoteResult.Great, NoteResultDescription.FlickMiss);
            }

            if (data.GreatBeforeJudgeTime >= offset && -data.GreatAfterJudgeTime <= offset)
            {
                return (NoteResult.Great, DescriptionFor(offset, justPerfectHalf));
            }

            if (data.GoodBeforeJudgeTime >= offset && -data.GoodAfterJudgeTime <= offset)
            {
                return (NoteResult.Good, DescriptionFor(offset, justPerfectHalf));
            }

            if (data.BadBeforeJudgeTime >= offset && -data.BadAfterJudgeTime <= offset)
            {
                return (NoteResult.Bad, DescriptionFor(offset, justPerfectHalf));
            }

            return (NoteResult.Miss, DescriptionFor(offset, justPerfectHalf));
        }

        /// <summary>Whether an accepted input was early, late, or inside the just-perfect window.</summary>
        public static NoteResultDescription DescriptionFor(float offset, float justPerfectHalf)
        {
            if (-justPerfectHalf <= offset && justPerfectHalf >= offset)
            {
                return NoteResultDescription.Just;
            }

            return offset > justPerfectHalf ? NoteResultDescription.Fast : NoteResultDescription.Late;
        }
    }
}
