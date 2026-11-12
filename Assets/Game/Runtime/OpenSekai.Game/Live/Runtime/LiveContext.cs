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
            int rulesVersion)
        {
            BootData = bootData;
            PlayMode = playMode;
            SessionId = sessionId ?? string.Empty;
            ChartHash = chartHash ?? string.Empty;
            RulesVersion = rulesVersion;
        }

        public LiveBootDataBase BootData { get; }

        public PlayMode PlayMode { get; }

        public string SessionId { get; }

        public string ChartHash { get; }

        public int RulesVersion { get; }

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
                PlaySessionContext.RULES_VERSION);
        }
    }
}
