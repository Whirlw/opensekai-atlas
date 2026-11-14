using System;
using UnityEngine;

namespace OpenSekai.Shared.Playback
{
    /// <summary>
    /// Per-session facts that are decided when the chart is loaded and consumed when the result is
    /// written: a session id for correlating logs/history, the hash of the chart file that was read,
    /// and the rules version that produced the score.
    /// </summary>
    public static class PlaySessionContext
    {
        /// <summary>
        /// Bump when judgement windows or scoring coefficients change.
        /// 2 = 1,000,000 改为按音符累积摊分（满判严格等于 1,000,000）；1 = 旧的 floor(1e6/n) 取整。
        /// </summary>
        public const int RULES_VERSION = 2;

        public static string SessionId { get; private set; } = string.Empty;
        public static string ChartHash { get; private set; } = string.Empty;

        public static void Begin(string chartHash)
        {
            SessionId = Guid.NewGuid().ToString("N").Substring(0, 12);
            ChartHash = chartHash ?? string.Empty;
        }

        public static void Clear()
        {
            SessionId = string.Empty;
            ChartHash = string.Empty;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewRun()
        {
            Clear();
        }
    }
}
