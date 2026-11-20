using System;
using System.Collections.Generic;
using OpenSekai.App.Library;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Packages;

namespace OpenSekai.App.UI
{
    internal enum LibrarySortKey
    {
        Title,
        Level,
        RecentlyPlayed,
        Imported,
        BestScore,
        Charter
    }

    internal enum LibraryGroupKey
    {
        None,
        Difficulty,
        Level,
        Charter,
        Singer,
        ClearStatus
    }

    internal enum LibraryViewMode
    {
        List,
        Grid
    }

    /// <summary>One chip of the group bar: a group name and how many packages it holds.</summary>
    internal readonly struct LibraryGroup
    {
        internal LibraryGroup(string name, int count)
        {
            Name = name;
            Count = count;
        }

        internal string Name { get; }
        internal int Count { get; }
    }

    /// <summary>What the list shows after search, grouping and sorting.</summary>
    internal sealed class LibraryListing
    {
        internal LibraryListing(
            int totalCount, int matchCount, IReadOnlyList<LibraryGroup> groups, string selectedGroup,
            IReadOnlyList<SongPackageItem> items)
        {
            TotalCount = totalCount;
            MatchCount = matchCount;
            Groups = groups;
            SelectedGroup = selectedGroup;
            Items = items;
        }

        /// <summary>Packages in the library, before any filter.</summary>
        internal int TotalCount { get; }

        /// <summary>Packages that match the search text, across all groups.</summary>
        internal int MatchCount { get; }

        /// <summary>Groups in display order; empty when grouping is off.</summary>
        internal IReadOnlyList<LibraryGroup> Groups { get; }

        /// <summary>The group the list is narrowed to; null shows every group.</summary>
        internal string SelectedGroup { get; }

        internal IReadOnlyList<SongPackageItem> Items { get; }
    }

    /// <summary>
    /// Search, grouping and sorting of the library, with no Unity objects involved. The presenter
    /// owns one instance, changes its options from user input and asks it for a listing.
    /// </summary>
    internal sealed class LibraryModel
    {
        private const string UNKNOWN_DIFFICULTY = "未知";
        private const string UNSET_FIELD = "未填写";
        private const string LEVEL_LOW = "Lv 1–19";
        private const string LEVEL_MID = "Lv 20–29";
        private const string LEVEL_HIGH = "Lv 30+";
        private const string LEVEL_NONE = "未定级";
        private const string CLEAR_ALL_PERFECT = "ALL PERFECT";
        private const string CLEAR_FULL_COMBO = "FULL COMBO";
        private const string CLEAR_CLEARED = "已通关";
        private const string CLEAR_FAILED = "未通关";
        private const string CLEAR_UNPLAYED = "未游玩";

        private static readonly string[] DifficultyOrder = { "EASY", "NORMAL", "HARD", "EXPERT", "MASTER", "APPEND" };
        private static readonly string[] LevelOrder = { LEVEL_LOW, LEVEL_MID, LEVEL_HIGH, LEVEL_NONE };
        private static readonly string[] ClearOrder =
        {
            CLEAR_ALL_PERFECT, CLEAR_FULL_COMBO, CLEAR_CLEARED, CLEAR_FAILED, CLEAR_UNPLAYED
        };

        private static readonly IReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary> NoHistory =
            new Dictionary<string, CustomMusicScorePlayHistorySummary>();

        internal static readonly IReadOnlyList<LibrarySortKey> SortKeys = new[]
        {
            LibrarySortKey.Title, LibrarySortKey.Level, LibrarySortKey.RecentlyPlayed,
            LibrarySortKey.Imported, LibrarySortKey.BestScore, LibrarySortKey.Charter
        };

        internal static readonly IReadOnlyList<LibraryGroupKey> GroupKeys = new[]
        {
            LibraryGroupKey.None, LibraryGroupKey.Difficulty, LibraryGroupKey.Level,
            LibraryGroupKey.Charter, LibraryGroupKey.Singer, LibraryGroupKey.ClearStatus
        };

        internal LibrarySortKey SortKey { get; set; } = LibrarySortKey.Title;

        internal LibraryGroupKey GroupKey { get; private set; } = LibraryGroupKey.None;

        /// <summary>Group the list is narrowed to; null shows every group.</summary>
        internal string SelectedGroup { get; set; }

        internal string SearchText { get; set; } = string.Empty;

        /// <summary>Switches the grouping and goes back to showing every group.</summary>
        internal void SetGroupKey(LibraryGroupKey key)
        {
            GroupKey = key;
            SelectedGroup = null;
        }

        internal static string Label(LibrarySortKey key)
        {
            switch (key)
            {
                case LibrarySortKey.Level: return "等级";
                case LibrarySortKey.RecentlyPlayed: return "最近游玩";
                case LibrarySortKey.Imported: return "导入时间";
                case LibrarySortKey.BestScore: return "最高分";
                case LibrarySortKey.Charter: return "谱面作者";
                default: return "标题";
            }
        }

        internal static string Label(LibraryGroupKey key)
        {
            switch (key)
            {
                case LibraryGroupKey.Difficulty: return "难度";
                case LibraryGroupKey.Level: return "等级";
                case LibraryGroupKey.Charter: return "谱面作者";
                case LibraryGroupKey.Singer: return "演唱";
                case LibraryGroupKey.ClearStatus: return "通关状态";
                default: return "不分组";
            }
        }

        internal LibraryListing Build(
            IReadOnlyList<SongPackageItem> items,
            IReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary> history = null)
        {
            history = history ?? NoHistory;
            var matches = new List<SongPackageItem>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                if (MatchesSearch(items[i]))
                {
                    matches.Add(items[i]);
                }
            }

            IReadOnlyList<LibraryGroup> groups = BuildGroups(matches, history);
            string selectedGroup = null;
            for (int i = 0; i < groups.Count; i++)
            {
                if (string.Equals(groups[i].Name, SelectedGroup, StringComparison.Ordinal))
                {
                    selectedGroup = SelectedGroup;
                    break;
                }
            }

            // A group that no longer exists (search narrowed, package deleted) falls back to "all".
            SelectedGroup = selectedGroup;

            var visible = new List<SongPackageItem>(matches.Count);
            for (int i = 0; i < matches.Count; i++)
            {
                if (selectedGroup == null || GroupOf(matches[i], history) == selectedGroup)
                {
                    visible.Add(matches[i]);
                }
            }

            visible.Sort((a, b) => Compare(a, b, history));
            return new LibraryListing(items.Count, matches.Count, groups, selectedGroup, visible);
        }

        internal static string BuildTitle(SongPackageItem item)
        {
            SongPackageManifest manifest = item.Entry?.Manifest;
            if (manifest == null)
            {
                return "未命名曲包";
            }
            return string.IsNullOrWhiteSpace(manifest.title) ? (manifest.scoreTitle ?? "未命名曲包") : manifest.title;
        }

        /// <summary>Second line of a row: who sings and who charted, or what is missing.</summary>
        internal static string BuildMeta(SongPackageItem item)
        {
            if (!IsPlayable(item))
            {
                return item.StatusText + "，无法演奏";
            }

            SongPackageManifest manifest = item.Entry?.Manifest;
            string singer = manifest?.singer;
            string charter = manifest?.userName;
            if (string.IsNullOrWhiteSpace(singer) && string.IsNullOrWhiteSpace(charter))
            {
                return string.Empty;
            }
            if (string.IsNullOrWhiteSpace(charter))
            {
                return singer;
            }
            return string.IsNullOrWhiteSpace(singer) ? "谱面 " + charter : singer + " · 谱面 " + charter;
        }

        internal static bool IsPlayable(SongPackageItem item)
        {
            return item != null && item.HasManifest && item.HasScore && item.HasAudio;
        }

        internal static string DifficultyLabel(SongPackageItem item)
        {
            string type = item.Entry?.Manifest?.musicDifficultyType;
            return string.IsNullOrWhiteSpace(type) ? UNKNOWN_DIFFICULTY : type.ToUpperInvariant();
        }

        internal static int PlayLevelOf(SongPackageItem item)
        {
            return item?.Entry?.Manifest != null ? item.Entry.Manifest.playLevel : 0;
        }

        /// <summary>Short clear badge for a cell: AP, FC or nothing.</summary>
        internal static string ClearBadge(CustomMusicScorePlayHistorySummary summary)
        {
            CustomMusicScorePlayHistoryRecord best = summary?.Best;
            if (best == null)
            {
                return string.Empty;
            }
            return best.isAllPerfect ? "AP" : best.isFullCombo ? "FC" : string.Empty;
        }

        internal static string ClearLabel(CustomMusicScorePlayHistorySummary summary)
        {
            CustomMusicScorePlayHistoryRecord best = summary?.Best;
            if (best == null)
            {
                return CLEAR_UNPLAYED;
            }
            if (best.isAllPerfect)
            {
                return CLEAR_ALL_PERFECT;
            }
            if (best.isFullCombo)
            {
                return CLEAR_FULL_COMBO;
            }
            return string.Equals(best.clearType, "life_zero", StringComparison.Ordinal) ? CLEAR_FAILED : CLEAR_CLEARED;
        }

        internal static CustomMusicScorePlayHistorySummary SummaryOf(
            SongPackageItem item, IReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary> history)
        {
            string key = item?.ScoreKey;
            if (history == null || string.IsNullOrEmpty(key))
            {
                return null;
            }
            return history.TryGetValue(key, out CustomMusicScorePlayHistorySummary summary) ? summary : null;
        }

        private bool MatchesSearch(SongPackageItem item)
        {
            string query = SearchText?.Trim();
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }

            SongPackageManifest manifest = item.Entry?.Manifest;
            return Contains(BuildTitle(item), query)
                || Contains(manifest?.scoreTitle, query)
                || Contains(manifest?.singer, query)
                || Contains(manifest?.composer, query)
                || Contains(manifest?.userName, query);
        }

        private static bool Contains(string text, string query)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private IReadOnlyList<LibraryGroup> BuildGroups(
            List<SongPackageItem> items, IReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary> history)
        {
            if (GroupKey == LibraryGroupKey.None)
            {
                return Array.Empty<LibraryGroup>();
            }

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < items.Count; i++)
            {
                string name = GroupOf(items[i], history);
                counts.TryGetValue(name, out int count);
                counts[name] = count + 1;
            }

            var names = new List<string>(counts.Keys);
            string[] order = FixedOrder();
            names.Sort((a, b) => CompareGroupNames(a, b, order));

            var groups = new List<LibraryGroup>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                groups.Add(new LibraryGroup(names[i], counts[names[i]]));
            }
            return groups;
        }

        private string[] FixedOrder()
        {
            switch (GroupKey)
            {
                case LibraryGroupKey.Difficulty: return DifficultyOrder;
                case LibraryGroupKey.Level: return LevelOrder;
                case LibraryGroupKey.ClearStatus: return ClearOrder;
                default: return Array.Empty<string>();
            }
        }

        /// <summary>Names with a fixed place come first in that order; the rest follow alphabetically, placeholders last.</summary>
        private static int CompareGroupNames(string a, string b, string[] order)
        {
            int rankA = Array.IndexOf(order, a);
            int rankB = Array.IndexOf(order, b);
            if (rankA >= 0 || rankB >= 0)
            {
                if (rankA < 0)
                {
                    return 1;
                }
                if (rankB < 0)
                {
                    return -1;
                }
                return rankA.CompareTo(rankB);
            }

            bool placeholderA = a == UNSET_FIELD || a == UNKNOWN_DIFFICULTY;
            bool placeholderB = b == UNSET_FIELD || b == UNKNOWN_DIFFICULTY;
            if (placeholderA != placeholderB)
            {
                return placeholderA ? 1 : -1;
            }
            return string.Compare(a, b, StringComparison.CurrentCulture);
        }

        private string GroupOf(
            SongPackageItem item, IReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary> history)
        {
            SongPackageManifest manifest = item.Entry?.Manifest;
            switch (GroupKey)
            {
                case LibraryGroupKey.Difficulty:
                    return DifficultyLabel(item);
                case LibraryGroupKey.Level:
                    int level = PlayLevelOf(item);
                    return level <= 0 ? LEVEL_NONE : level < 20 ? LEVEL_LOW : level < 30 ? LEVEL_MID : LEVEL_HIGH;
                case LibraryGroupKey.Charter:
                    return string.IsNullOrWhiteSpace(manifest?.userName) ? UNSET_FIELD : manifest.userName.Trim();
                case LibraryGroupKey.Singer:
                    return string.IsNullOrWhiteSpace(manifest?.singer) ? UNSET_FIELD : manifest.singer.Trim();
                case LibraryGroupKey.ClearStatus:
                    return ClearLabel(SummaryOf(item, history));
                default:
                    return string.Empty;
            }
        }

        private int Compare(
            SongPackageItem a, SongPackageItem b,
            IReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary> history)
        {
            int result = 0;
            switch (SortKey)
            {
                case LibrarySortKey.Level:
                    result = PlayLevelOf(b).CompareTo(PlayLevelOf(a));
                    break;
                case LibrarySortKey.RecentlyPlayed:
                    result = string.CompareOrdinal(LastPlayedOf(b, history), LastPlayedOf(a, history));
                    break;
                case LibrarySortKey.Imported:
                    result = b.ImportedAt.CompareTo(a.ImportedAt);
                    break;
                case LibrarySortKey.BestScore:
                    result = BestScoreOf(b, history).CompareTo(BestScoreOf(a, history));
                    break;
                case LibrarySortKey.Charter:
                    result = CompareGroupNames(CharterOf(a), CharterOf(b), Array.Empty<string>());
                    break;
            }

            return result != 0
                ? result
                : string.Compare(BuildTitle(a), BuildTitle(b), StringComparison.CurrentCulture);
        }

        private static string CharterOf(SongPackageItem item)
        {
            string charter = item.Entry?.Manifest?.userName;
            return string.IsNullOrWhiteSpace(charter) ? UNSET_FIELD : charter.Trim();
        }

        private static string LastPlayedOf(
            SongPackageItem item, IReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary> history)
        {
            return SummaryOf(item, history)?.LastPlayedAtUtc ?? string.Empty;
        }

        private static int BestScoreOf(
            SongPackageItem item, IReadOnlyDictionary<string, CustomMusicScorePlayHistorySummary> history)
        {
            CustomMusicScorePlayHistoryRecord best = SummaryOf(item, history)?.Best;
            return best != null ? best.totalScore : -1;
        }
    }
}
