namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// Why a note sits under another note. A long note carries three kinds of children, and only one
    /// of them is allowed to end it: the tail. Connection notes follow the bar's state and combo
    /// markers are graded on their own beat, so a bar that reacted to them would finish early — the
    /// exact failure that made a bar vanish before its own start time.
    /// </summary>
    public enum NoteParentRole
    {
        None = 0,

        /// <summary>The bar's end point. Finishing it finishes the bar.</summary>
        Tail = 1,

        /// <summary>A slide note riding the bar. Finishing it must not finish the bar.</summary>
        Connection = 2,

        /// <summary>A combo marker inside the bar. Graded on its own, never finishes the bar.</summary>
        Combo = 3,
    }
}
