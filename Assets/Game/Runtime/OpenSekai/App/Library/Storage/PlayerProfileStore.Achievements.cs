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
        private AchievementClaims ReadClaims() => _cachedClaims ??= LoadClaims();
        private AchievementClaims LoadClaims() => File.Exists(ClaimsPath)
            ? AchievementClaims.Decode(File.ReadAllText(ClaimsPath)) : new AchievementClaims();

        /// <summary>Every goal with its progress, and which of the reached ones the player has unlocked.</summary>
        public IReadOnlyList<AchievementProgress> ReadAchievements() => ReadClaims().Apply(History.ReadAchievements());

        /// <summary>
        /// Unlocks a reached goal, which hands over its medal and avatar. Levels are unlocked in
        /// order. One already unlocked stays as it is.
        /// </summary>
        public void UnlockAchievement(string id)
        {
            RequireIdle();
            var series = AchievementSeries.Group(ReadAchievements()).FirstOrDefault(item => item.IndexOf(id) >= 0)
                ?? throw new InvalidOperationException("没有这项成就。");
            int step = series.IndexOf(id);
            var state = series.Steps[step].State;
            if (state == AchievementState.Unlocked) return;
            if (step != series.UnlockedCount || state != AchievementState.Claimable)
                throw new InvalidOperationException("该成就还不能解锁。");
            WriteClaims(ReadClaims().With(id, DateTimeOffset.UtcNow));
        }

        /// <summary>False for an avatar that an achievement gives and the player has not unlocked.</summary>
        public bool OwnsAvatar(string avatarId)
        {
            var goal = AchievementGoals.ForAvatar(avatarId);
            return goal == null || ReadAchievements().Any(item => item.Id == goal.Id && item.State == AchievementState.Unlocked);
        }

        /// <summary>
        /// Forgets the unlocks whose goals deleted records no longer reach, so reaching one again
        /// makes it claimable, the way it is announced.
        /// </summary>
        private void ForgetUnreachedClaims()
        {
            var claims = ReadClaims().WithoutUnreached(History.ReadAchievements());
            if (claims.Count != ReadClaims().Count) WriteClaims(claims);
        }

        private void WriteClaims(AchievementClaims claims)
        {
            AtomicFile.WriteAllText(ClaimsPath, JsonConvert.SerializeObject(claims));
            _cachedClaims = claims;
            CancelPendingPreview();
        }
    }
}
