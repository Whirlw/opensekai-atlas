using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OpenSekai.App.Library;
using OpenSekai.Shared.Playback;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>The words, numbers and tag colors of the 个人信息 page, kept apart from its layout.</summary>
    internal static class ProfileCopy
    {
        internal const int NICKNAME_LIMIT = 24;

        internal static readonly Color32 AllPerfect = new Color32(214, 160, 255, 255);
        internal static readonly Color32 FullCombo = new Color32(255, 196, 106, 255);

        internal static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

        internal static string NextTier(long total)
        {
            int? threshold = RankingRules.NextTierThreshold(total);
            return threshold.HasValue ? "距下一评级还差 " + Number(threshold.Value - total) + " 分" : "已达最高评级";
        }

        /// <summary>The worn 段位 plate's text; null while none is worn.</summary>
        internal static string DanLabel(PlayerProfileSnapshot snapshot) => snapshot?.EquippedDan?.Label;

        /// <summary>The song's name as the run saved it.</summary>
        internal static string Title(CustomMusicScorePlayHistoryRecord play)
        {
            if (!string.IsNullOrWhiteSpace(play?.Title)) return play.Title;
            if (!string.IsNullOrWhiteSpace(play?.ScoreTitle)) return play.ScoreTitle;
            return "未命名曲目";
        }

        /// <summary>"MASTER 30", or just the level when the difficulty has no name.</summary>
        internal static string Difficulty(CustomMusicScorePlayHistoryRecord play)
        {
            string name = string.IsNullOrWhiteSpace(play.Difficulty) ? null : play.Difficulty.Trim().ToUpperInvariant();
            string level = play.PlayLevel > 0 ? play.PlayLevel.ToString(CultureInfo.InvariantCulture) : null;
            if (name != null && level != null) return name + " " + level;
            if (name != null) return name;
            return level != null ? "Lv." + level : "—";
        }

        internal static Color32 DifficultyColor(CustomMusicScorePlayHistoryRecord play) =>
            LibraryTheme.Colors.Difficulty(play.Difficulty);

        internal static bool IsAuto(CustomMusicScorePlayHistoryRecord play) => play.AutoCount > 0;

        /// <summary>The mark of a Best 30 entry: AP over FC over a plain clear.</summary>
        internal static string AwardTag(ClearAward award)
        {
            switch (award)
            {
                case ClearAward.AllPerfect: return "AP";
                case ClearAward.FullCombo: return "FC";
                default: return "CLEAR";
            }
        }

        internal static Color32 AwardColor(ClearAward award)
        {
            switch (award)
            {
                case ClearAward.AllPerfect: return AllPerfect;
                case ClearAward.FullCombo: return FullCombo;
                default: return LibraryTheme.Colors.TextSecondary;
            }
        }

        /// <summary>A history row's tag: how the run ended, or what it achieved when it was completed by hand.</summary>
        internal static string EndTag(CustomMusicScorePlayHistoryRecord play, out Color32 color)
        {
            if (IsAuto(play))
            {
                color = HomeTheme.Colors.Periwinkle;
                return "AUTO";
            }

            if (!play.EndReason.HasValue)
            {
                color = LibraryTheme.Colors.TextMuted;
                if (play.IsAllPerfect)
                {
                    color = AllPerfect;
                    return "AP";
                }

                if (play.IsFullCombo)
                {
                    color = FullCombo;
                    return "FC";
                }

                return "旧版";
            }

            switch (play.EndReason.Value)
            {
                case PlayResultKind.Failed:
                    color = LibraryTheme.Colors.Danger;
                    return "FAILED";
                case PlayResultKind.Retired:
                    color = LibraryTheme.Colors.TextMuted;
                    return "RETIRED";
                case PlayResultKind.Completed:
                    if (play.IsAllPerfect)
                    {
                        color = AllPerfect;
                        return "AP";
                    }

                    if (play.IsFullCombo)
                    {
                        color = FullCombo;
                        return "FC";
                    }

                    color = LibraryTheme.Colors.TextSecondary;
                    return "CLEAR";
                default:
                    color = LibraryTheme.Colors.TextMuted;
                    return "—";
            }
        }

        /// <summary>Why a history row adds nothing, when it does not; null for a run that could take part.</summary>
        internal static string HistoryNote(CustomMusicScorePlayHistoryRecord play)
        {
            if (play.SchemaVersion < 3 || !play.EndReason.HasValue) return "旧版记录 · 不计入";
            if (IsAuto(play)) return "自动演奏 · 不计入";
            if (play.EndReason == PlayResultKind.Failed) return "未完成 · 不计入";
            if (play.EndReason == PlayResultKind.Retired) return "中途退出 · 不计入";
            return null;
        }

        /// <summary>Played-at time, local: "11-22" and "21:45", with the year when it is not this year's.</summary>
        internal static bool TryWhen(CustomMusicScorePlayHistoryRecord play, out string date, out string time)
        {
            if (DateTimeOffset.TryParse(play.PlayedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset when))
            {
                DateTimeOffset local = when.ToLocalTime();
                date = local.Year == DateTimeOffset.Now.Year
                    ? local.ToString("MM-dd", CultureInfo.InvariantCulture)
                    : local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                time = local.ToString("HH:mm", CultureInfo.InvariantCulture);
                return true;
            }

            date = "—";
            time = string.Empty;
            return false;
        }

        // ---------------------------------------------------------------- backup

        /// <summary>What went wrong with a backup, for a toast.</summary>
        internal static string DescribeBackupFailure(Exception exception, bool importing)
        {
            string action = importing ? "导入" : "导出";
            switch (exception)
            {
                case InvalidDataException _:
                    return importing ? "备份文件无效或已损坏，没有导入任何内容" : "游玩记录超过备份上限，无法导出";
                case PlatformNotSupportedException _:
                    return "当前平台不支持" + action + "个人档案";
                case UnauthorizedAccessException _:
                    return "没有读写权限，" + action + "失败";
                case InvalidOperationException _:
                    return "另一项备份操作尚未结束，请稍后再试";
                case IOException _:
                    return action + "失败：文件无法读写";
                default:
                    return action + "失败，请检查运行日志";
            }
        }

        /// <summary>"第 3、7 行" for the first few line numbers, with how many more when there are more.</summary>
        internal static string Lines(IReadOnlyList<int> lines, int shown = 5)
        {
            var text = new StringBuilder("第 ");
            for (int i = 0; i < lines.Count && i < shown; i++)
            {
                if (i > 0) text.Append('、');
                text.Append(lines[i].ToString(CultureInfo.InvariantCulture));
            }

            text.Append(" 行");
            if (lines.Count > shown) text.Append(" 等 ").Append(lines.Count.ToString(CultureInfo.InvariantCulture)).Append(" 行");
            return text.ToString();
        }

        /// <summary>The first few play ids, shortened, with how many more when there are more.</summary>
        internal static string Ids(IReadOnlyList<string> ids, int shown = 3)
        {
            var text = new StringBuilder();
            for (int i = 0; i < ids.Count && i < shown; i++)
            {
                if (i > 0) text.Append('、');
                string id = ids[i] ?? string.Empty;
                text.Append(id.Length > 10 ? id.Substring(0, 8) + "…" : id);
            }

            if (ids.Count > shown) text.Append(" 等 ").Append(ids.Count.ToString(CultureInfo.InvariantCulture)).Append(" 局");
            return text.ToString();
        }
    }
}
