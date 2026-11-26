using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using OpenSekai.Shared.Storage;

namespace OpenSekai.App.Library
{
    public sealed partial class PlayerProfileStore
    {
        internal const string ACHIEVEMENTS_FILE = "achievements.json";
        private string ClaimsPath => Path.Combine(_directory, ACHIEVEMENTS_FILE);
        private AchievementClaims _cachedClaims;
        internal DateTimeOffset? SampleClaimTime { get; set; }
        private AchievementClaims ReadClaims() => _cachedClaims ??= LoadClaims();
        private AchievementClaims LoadClaims() => File.Exists(ClaimsPath)
            ? AchievementClaims.Decode(File.ReadAllText(ClaimsPath)) : new AchievementClaims();

        /// <summary>Every goal with its progress, and which of the reached ones the player has unlocked.</summary>
        public IReadOnlyList<AchievementProgress> ReadAchievements() => ReadClaims().Apply(History);

        /// <summary>
        /// Unlocks a reached goal, which hands over its medal and avatar. Levels are unlocked in
        /// order. One already unlocked stays as it is.
        /// </summary>
        public void UnlockAchievement(string id) => ClaimAchievement(id);

        public (bool NewlyClaimed, string MedalKey, string AvatarId) ClaimAchievement(string id)
        {
            RequireIdle();
            var series = AchievementSeries.Group(ReadAchievements()).FirstOrDefault(item => item.IndexOf(id) >= 0)
                ?? throw new InvalidOperationException("没有这项成就。");
            int step = series.IndexOf(id);
            var state = series.Steps[step].State;
            if (state == AchievementState.Unlocked) return (false, null, null);
            if (step != series.UnlockedCount || state != AchievementState.Claimable)
                throw new InvalidOperationException("该成就还不能解锁。");
            var goal = series.Steps[step].Goal;
            var avatar = goal.AvatarId != null && !OwnsAvatar(goal.AvatarId) ? goal.AvatarId : null;
            WriteClaims(ReadClaims().With(id, SampleClaimTime ?? DateTimeOffset.UtcNow));
            return (true, goal.GivesMedal ? Medal.AchievementKey(id) : null, avatar);
        }

        /// <summary>False for an avatar that an achievement gives and the player has not unlocked.</summary>
        public bool OwnsAvatar(string avatarId)
        {
            var goal = AchievementGoals.ForAvatar(avatarId);
            return goal == null || RewardAchievements().Any(item => item.Goal.AvatarId == avatarId && item.State == AchievementState.Unlocked);
        }

        private IReadOnlyList<AchievementProgress> RewardAchievements() => ReadClaims().Rewards(History);

        private void ForgetUnreachedClaims()
        {
            var claims = ReadClaims().WithoutUnreached(History);
            if (claims.Count != ReadClaims().Count) WriteClaims(claims);
        }

        internal void RecordRetryStart(string id, string scoreKey, string sourceId, string source)
        {
            if (string.IsNullOrEmpty(scoreKey) || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(sourceId)) return;
            var start = new AchievementClaims.RetryStart { Time = DateTimeOffset.UtcNow, ScoreKey = scoreKey, SourceId = sourceId, Source = source };
            try
            {
                RequireIdle();
                var current = ReadClaims();
                var next = current.WithRetry(id, start);
                if (ReferenceEquals(current, next)) return;
                WriteClaims(next);
                History.AppendAchievement(new AchievementEvent { Time = start.Time, Id = id, SongNumber = -1, RetryKey = scoreKey });
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // The settled record also carries the start evidence if this write fails.
                UnityEngine.Debug.LogWarning("[Achievement] " + error.Message);
            }
        }

        private void WriteClaims(AchievementClaims claims)
        {
            AtomicFile.WriteAllText(ClaimsPath, JsonConvert.SerializeObject(claims));
            _cachedClaims = claims;
            CancelPendingPreview();
        }
    }
}
