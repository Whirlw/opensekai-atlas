using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSekai.App.Library
{
    public sealed partial class PlayerProfileStore
    {
        /// <summary>Every medal the player owns: passed 段位, newest saved first, then the ones unlocked achievements gave.</summary>
        public IReadOnlyList<Medal> ReadMedals() => Medals(ReadDanArchive(), RewardAchievements());

        /// <summary>
        /// Wears the medal in <paramref name="slot"/>; null leaves the slot empty. A medal already
        /// worn in the other slot trades places with this one's.
        /// </summary>
        public void WearMedal(int slot, string key)
        {
            RequireIdle();
            if (slot < 0 || slot >= Medal.SLOTS) throw new ArgumentOutOfRangeException(nameof(slot));
            if (key != null && ReadMedals().All(medal => medal.Key != key)) throw new InvalidOperationException("尚未取得该奖牌。");
            var archive = ReadDanArchive().Copy();
            int held = key == null ? -1 : Array.IndexOf(archive.Worn, key);
            if (held >= 0) archive.Worn[held] = archive.Worn[slot];
            archive.Worn[slot] = key;
            WriteDan(archive);
        }

        /// <summary>
        /// Wears the medal without naming a slot: in the first free one, else in place of the last.
        /// Returns the slot it is worn in; one already worn stays where it is.
        /// </summary>
        public int WearMedal(string key)
        {
            RequireIdle();
            if (key == null) throw new ArgumentNullException(nameof(key));
            // A slot whose medal is gone (its record was deleted) counts as free.
            var worn = WornMedals(ReadDanArchive(), RewardAchievements);
            int slot = Array.FindIndex(worn, medal => medal?.Key == key);
            if (slot >= 0) return slot;
            slot = Array.IndexOf(worn, null);
            if (slot < 0) slot = Medal.SLOTS - 1;
            WearMedal(slot, key);
            return slot;
        }

        /// <summary>Takes the medal off, wherever it is worn.</summary>
        public void RemoveMedal(string key)
        {
            RequireIdle();
            var archive = ReadDanArchive().Copy();
            if (key != null && archive.TakeOff(key)) WriteDan(archive);
        }

        /// <summary>The medal in each slot, null where none is worn or the worn one is no longer owned.</summary>
        internal static Medal[] WornMedals(DanArchive archive, Func<IReadOnlyList<AchievementProgress>> achievements)
        {
            // Achievements are only worked out for a player who wears one.
            var owned = Medals(archive, archive.Worn.Any(Medal.IsAchievementKey) ? achievements() : Array.Empty<AchievementProgress>());
            return archive.Worn.Select(key => key == null ? null : owned.FirstOrDefault(medal => medal.Key == key)).ToArray();
        }

        private static IReadOnlyList<Medal> Medals(DanArchive archive, IReadOnlyList<AchievementProgress> achievements)
        {
            var earned = new HashSet<string>(archive.Earned(), StringComparer.Ordinal);
            return archive.DecodeDefinitions().Values.Where(definition => earned.Contains(definition.Id)).Reverse().Select(Medal.Of)
                .Concat(achievements.Where(progress => progress.Goal.GivesMedal && progress.State == AchievementState.Unlocked).Select(Medal.Of))
                .ToArray();
        }
    }
}
