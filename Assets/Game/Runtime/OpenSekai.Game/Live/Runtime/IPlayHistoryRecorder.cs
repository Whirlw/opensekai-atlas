namespace Sekai.Core.Live
{
    /// <summary>
    /// The one thing a finished run still needs from the player library: writing the result down.
    /// The storage moved to the App layer in P3-3, so Game keeps only this shape and the live hands
    /// over the facts it already has instead of reading the package store itself.
    /// </summary>
    public interface IPlayHistoryRecorder
    {
        void AppendLiveResult(
            FreeLiveBootData bootData,
            LiveScore score,
            long musicTimeMs,
            long musicLengthMs,
            string sessionId,
            string chartHash,
            int rulesVersion);
    }

    /// <summary>
    /// Slot the App layer fills before a live scene loads, on the same static boundary it already
    /// writes for <see cref="Sekai.Core.EntryPoint.PlayMode"/> and the play session facts. Game never
    /// references App: App registers one implementation per run, and the live reads it back through
    /// <see cref="LiveContext.PlayHistory"/>.
    /// </summary>
    public static class PlayHistoryRecorder
    {
        public static IPlayHistoryRecorder Current { get; set; }
    }
}
