using Sekai;
using Sekai.Core.Live;

namespace OpenSekai.App.Library
{
    /// <summary>
    /// Implements the Game-owned recorder port with the App-owned storage. The live only hands over
    /// the facts it already holds; building the record, its schema and the file layout stay on this
    /// side, which is where the player library lives.
    /// </summary>
    internal sealed class CustomScorePlayHistoryRecorder : IPlayHistoryRecorder
    {
        public void AppendLiveResult(
            FreeLiveBootData bootData,
            LiveScore score,
            long musicTimeMs,
            long musicLengthMs,
            string sessionId,
            string chartHash,
            int rulesVersion)
        {
            CustomMusicScorePlayHistoryStorage.AppendLiveResult(
                bootData, score, musicTimeMs, musicLengthMs, sessionId, chartHash, rulesVersion);
        }
    }
}
