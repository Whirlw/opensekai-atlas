using OpenSekai.Shared.Playback;
using Sekai.Live;

namespace Sekai.Core.Live
{
    /// <summary>
    /// What one live run receives from the scene that started it: the boot data, the play mode, and the
    /// session facts written when the chart was loaded. The Game layer owns this type and reads it as a
    /// value; the App layer puts the values there through its own ports, so no Game type has to reach
    /// for <c>UserDataManager</c>, <c>EntryPoint.PlayMode</c> or <c>PlaySessionContext</c> again.
    /// </summary>
    public sealed class LiveContext
    {
        public LiveContext(
            LiveBootDataBase bootData,
            PlayMode playMode,
            string sessionId,
            string chartHash,
            int rulesVersion,
            IPlayHistoryRecorder playHistory = null)
        {
            BootData = bootData;
            PlayMode = playMode;
            SessionId = sessionId ?? string.Empty;
            ChartHash = chartHash ?? string.Empty;
            RulesVersion = rulesVersion;
            PlayHistory = playHistory;
        }

        public LiveBootDataBase BootData { get; }

        public PlayMode PlayMode { get; }

        public string SessionId { get; }

        public string ChartHash { get; }

        public int RulesVersion { get; }

        /// <summary>
        /// The player library's recorder, handed in by the App layer: the live writes a finished run
        /// down through it instead of reaching for the package store it no longer owns.
        /// </summary>
        public IPlayHistoryRecorder PlayHistory { get; }

        /// <summary>
        /// Reads the retained boot boundary once, at the scene entry point, so the live controller can
        /// be handed a context instead of reading three statics during its own Awake.
        /// </summary>
        public static LiveContext FromRetainedBoot()
        {
            return new LiveContext(
                UserDataManager.Instance.FreeLiveBootData,
                EntryPoint.PlayMode,
                PlaySessionContext.SessionId,
                PlaySessionContext.ChartHash,
                PlaySessionContext.RULES_VERSION,
                PlayHistoryRecorder.Current);
        }
    }
}
