using Sekai.Live;

namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// Several notes can be judged on the same frame (a chord, or a bar tail together with the note that
    /// sits on top of it). The popup can only show one label, and the worst result of the frame is the one
    /// the player has to see: showing the Perfect that happened to be judged last hides the Miss.
    ///
    /// <see cref="NoteResult"/> is ordered from worst (Miss) to best (JustPerfect), with two entries that
    /// are not part of that ladder: None means "not judged yet" and Auto means the note was never in the
    /// player's hands, so neither may outrank a real judgement.
    /// </summary>
    public static class JudgmentAggregator
    {
        public static NoteResult Worse(NoteResult current, NoteResult candidate)
        {
            if (!IsRealResult(candidate))
            {
                return current;
            }

            if (!IsRealResult(current))
            {
                return candidate;
            }

            return candidate < current ? candidate : current;
        }

        public static bool IsRealResult(NoteResult result)
        {
            return result != NoteResult.None && result != NoteResult.Auto;
        }
    }
}
