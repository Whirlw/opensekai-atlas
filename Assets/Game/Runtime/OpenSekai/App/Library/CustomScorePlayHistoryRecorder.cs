using OpenSekai.Shared.Playback;
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
        public RankingSettlement Finish(
            FreeLiveBootData bootData,
            LiveScore score,
            PlayResultKind kind,
            long musicTimeMs,
            long musicLengthMs,
            string sessionId,
            string chartHash,
            int rulesVersion)
        {
            return CustomMusicScorePlayHistoryStorage.Finish(
                bootData, score, kind, musicTimeMs, musicLengthMs, sessionId, chartHash, rulesVersion);
        }
    }
}
