using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OpenSekai.App.Library;
using OpenSekai.App.Playback;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Infrastructure.Sonolus;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Playback;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// What the pages know about one song of a 段位 before it is played: the link, the server and
    /// level it names and, when the chart is on this device, its title, difficulty and jacket.
    /// </summary>
    internal sealed class DanSongInfo
    {
        private DanSongInfo(string link)
        {
            Link = link;
        }

        internal string Link { get; }
        internal string Host { get; private set; }
        internal string LevelName { get; private set; }
        internal string PageUrl { get; private set; }
        internal bool RequiresLocalPackage { get; private set; }
        internal string Availability => Installed ? "本机已有" : PageUrl != null ? "手动下载"
            : RequiresLocalPackage ? "需要导入曲包" : "开打前下载";
        internal string ManualDownloadPage => Installed ? null : PageUrl;

        /// <summary>The song's title: the installed chart's, then one a past attempt saved, then the level's name.</summary>
        internal string Title { get; private set; }

        /// <summary>"MASTER 30", or null when the chart is not on this device.</summary>
        internal string Difficulty { get; private set; }

        internal string DifficultyType { get; private set; }
        internal string Charter { get; private set; }
        internal bool Installed { get; private set; }
        internal bool Valid { get; private set; }
        internal string JacketPath { get; private set; }
        internal long JacketVersion { get; private set; }

        /// <param name="savedTitle">A title an attempt saved for this song, used when the chart is not installed.</param>
        internal static DanSongInfo Resolve(string link, string savedTitle = null)
        {
            var info = new DanSongInfo(link);
            SonolusServerAddress server = null;
            string level = null;
            bool local = DanLibrarySong.TryParse(link, out var librarySong);
            if (!local && (!SonolusServerAddress.TryParse(link, out server, out level, out _) || string.IsNullOrEmpty(level)))
            {
                info.Title = string.IsNullOrWhiteSpace(link) ? "未填写链接" : link.Trim();
                return info;
            }

            info.Valid = true;
            info.RequiresLocalPackage = local;
            info.PageUrl = librarySong?.PageUrl;
            info.Host = SongPackageSource.TryHttps(info.PageUrl, out Uri page) ? page.Host : server?.Host ?? "曲库";
            info.LevelName = level;
            info.Title = !string.IsNullOrWhiteSpace(savedTitle) ? savedTitle : librarySong?.Title ?? level;
            try
            {
                SongPackage package = local ? librarySong.FindInstalled(SongPackageManager.Shared)?.Entry
                    : SonolusLevelInstaller.Shared.FindPlayableInstalled(server, level);
                if (package?.Manifest != null)
                {
                    info.Installed = true;
                    info.Fill(package);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
                || exception is InvalidDataException || exception is FormatException)
            {
                Debug.LogWarning("[Dan] Could not look up an installed chart: " + exception.Message);
            }

            return info;
        }

        /// <summary>The song from the copy a running course pinned; <paramref name="link"/> names it when that copy is gone.</summary>
        internal static DanSongInfo FromDirectory(string directory, string link)
        {
            DanSongInfo info = Resolve(link);
            if (string.IsNullOrEmpty(directory)) return info;
            try
            {
                SongPackage package = SongPackageStorage.LoadEntry(directory);
                if (package?.Manifest != null) info.Fill(package);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
                || exception is InvalidDataException || exception is FormatException)
            {
                Debug.LogWarning("[Dan] Could not read the next song: " + exception.Message);
            }

            return info;
        }

        private void Fill(SongPackage package)
        {
            var manifest = package.Manifest;
            string title = string.IsNullOrWhiteSpace(manifest.title) ? manifest.scoreTitle : manifest.title;
            if (!string.IsNullOrWhiteSpace(title)) Title = title;
            DifficultyType = manifest.musicDifficultyType;
            string name = string.IsNullOrWhiteSpace(manifest.musicDifficultyType) ? null : manifest.musicDifficultyType.Trim().ToUpperInvariant();
            string number = manifest.playLevel > 0 ? manifest.playLevel.ToString(CultureInfo.InvariantCulture) : null;
            Difficulty = name != null && number != null ? name + " " + number : name ?? (number != null ? "Lv." + number : null);
            Charter = string.IsNullOrWhiteSpace(manifest.userName) ? null : manifest.userName.Trim();
            if (File.Exists(package.JacketPath))
            {
                JacketPath = package.JacketPath;
                JacketVersion = File.GetLastWriteTimeUtc(package.JacketPath).Ticks;
            }
        }
    }

    /// <summary>One saved 段位 with what the player has done with it, newest attempt first.</summary>
    internal sealed class DanRecord
    {
        internal DanRecord(DanDefinition definition, IReadOnlyList<DanAttempt> attempts, bool earned, bool equipped)
        {
            Definition = definition;
            Attempts = attempts;
            Earned = earned;
            Equipped = equipped;
            foreach (DanAttempt attempt in attempts)
            {
                int cleared = DanCopy.Cleared(attempt);
                if (cleared > FurthestCleared) FurthestCleared = cleared;
                if (attempt.Outcome == DanOutcome.Passed)
                {
                    Passes++;
                    int life = attempt.RemainingLife(definition);
                    if (life > BestLife) BestLife = life;
                }
            }
        }

        internal DanDefinition Definition { get; }
        internal IReadOnlyList<DanAttempt> Attempts { get; }
        internal bool Earned { get; }
        internal bool Equipped { get; }
        internal int Passes { get; }

        /// <summary>Most songs one attempt finished.</summary>
        internal int FurthestCleared { get; }

        /// <summary>Most life a passing attempt kept; -1 before the first pass.</summary>
        internal int BestLife { get; } = -1;

        internal DanAttempt Latest => Attempts.Count > 0 ? Attempts[0] : null;
    }

    /// <summary>The words, numbers and colors of the 段位 pages, kept apart from their layout.</summary>
    internal static class DanCopy
    {
        internal static readonly string[] JudgeNames = { "PERFECT", "GREAT", "GOOD", "BAD", "MISS" };

        internal static readonly Color32[] JudgeColors =
        {
            new Color32(255, 206, 120, 255),
            new Color32(255, 140, 186, 255),
            new Color32(122, 182, 255, 255),
            new Color32(123, 216, 143, 255),
            new Color32(154, 166, 181, 255)
        };

        internal static readonly Color32 Pass = new Color32(255, 206, 120, 255);
        internal static readonly Color32 Life = new Color32(123, 216, 143, 255);
        internal static readonly Color32 LifeLow = new Color32(245, 128, 128, 255);

        internal static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

        internal static int Damage(DanDamage damage, int judge)
        {
            switch (judge)
            {
                case 0: return damage.Perfect;
                case 1: return damage.Great;
                case 2: return damage.Good;
                case 3: return damage.Bad;
                default: return damage.Miss;
            }
        }

        /// <summary>A judgment's cost as the damage table shows it: "−3", or "0" when it costs nothing.</summary>
        internal static string Cost(int cost) => cost > 0 ? "−" + Number(cost) : "0";

        internal static int Judgments(DanSongResult song, int judge)
        {
            switch (judge)
            {
                case 0: return song.Score.perfectCount;
                case 1: return song.Score.greatCount;
                case 2: return song.Score.goodCount;
                case 3: return song.Score.badCount;
                default: return song.Score.missCount;
            }
        }

        /// <summary>Songs of the attempt that ended with the song completed.</summary>
        internal static int Cleared(DanAttempt attempt)
        {
            int cleared = 0;
            foreach (DanSongResult song in attempt.Songs)
            {
                if (song.Kind == PlayResultKind.Completed) cleared++;
            }

            return cleared;
        }

        internal static string Outcome(DanOutcome outcome)
        {
            switch (outcome)
            {
                case DanOutcome.Passed: return "通过";
                case DanOutcome.Failed: return "未通过";
                default: return "未完成";
            }
        }

        internal static string OutcomeLatin(DanOutcome outcome)
        {
            switch (outcome)
            {
                case DanOutcome.Passed: return "PASSED";
                case DanOutcome.Failed: return "FAILED";
                default: return "INCOMPLETE";
            }
        }

        internal static Color32 OutcomeColor(DanOutcome outcome)
        {
            switch (outcome)
            {
                case DanOutcome.Passed: return Pass;
                case DanOutcome.Failed: return LibraryTheme.Colors.Danger;
                default: return LibraryTheme.Colors.TextSecondary;
            }
        }

        /// <summary>How one song of an attempt ended.</summary>
        internal static string SongEnd(PlayResultKind kind)
        {
            switch (kind)
            {
                case PlayResultKind.Completed: return "完成";
                case PlayResultKind.Failed: return "生命耗尽";
                default: return "中途退出";
            }
        }

        internal static Color32 SongEndColor(PlayResultKind kind)
        {
            switch (kind)
            {
                case PlayResultKind.Completed: return Life;
                case PlayResultKind.Failed: return LibraryTheme.Colors.Danger;
                default: return LibraryTheme.Colors.Warning;
            }
        }

        internal static Color32 LifeColor(int life, int initial) =>
            initial > 0 && life * 5 <= initial ? LifeLow : Life;

        /// <summary>"1:23" for a position in a song.</summary>
        internal static string Clock(long milliseconds)
        {
            long seconds = Math.Max(0L, milliseconds) / 1000L;
            return (seconds / 60L).ToString(CultureInfo.InvariantCulture) + ":" + (seconds % 60L).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>"3 首  ·  生命 100".</summary>
        internal static string Shape(DanDefinition definition) =>
            definition.Songs.Count + " 首  ·  生命 " + Number(definition.InitialLife);

        /// <summary>What the player has done with a 段位, for its list row.</summary>
        internal static string Status(DanRecord record)
        {
            int count = record.Definition.Songs.Count;
            if (record.Earned)
            {
                return record.BestLife >= 0
                    ? "已通关  ·  最多剩余生命 " + Number(record.BestLife)
                    : "已通关";
            }

            if (record.Attempts.Count == 0) return "尚未挑战";
            return "最远完成 " + record.FurthestCleared + " / " + count + " 首  ·  挑战 " + record.Attempts.Count + " 次";
        }

        /// <summary>One attempt in a line: how far it got and what was left.</summary>
        internal static string Attempt(DanAttempt attempt, DanDefinition definition)
        {
            int count = definition.Songs.Count;
            int life = attempt.RemainingLife(definition);
            switch (attempt.Outcome)
            {
                case DanOutcome.Passed:
                    return "完成全部 " + count + " 首  ·  剩余生命 " + Number(life) + " / " + Number(definition.InitialLife);
                case DanOutcome.Failed:
                    return "第 " + attempt.Songs.Count + " 首生命耗尽";
                default:
                    return attempt.Songs.Count == 0 || attempt.Songs[attempt.Songs.Count - 1].Kind == PlayResultKind.Completed
                        ? "完成 " + Cleared(attempt) + " / " + count + " 首后中断"
                        : "第 " + attempt.Songs.Count + " 首中途退出";
            }
        }

        internal static string When(DateTimeOffset when) =>
            when.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);

        /// <summary>A plain-language reason for a field the definition refused.</summary>
        internal static string Field(DanFieldException exception, int songCount = 0)
        {
            string field = exception.Field ?? string.Empty;
            if (field.StartsWith("songs[", StringComparison.Ordinal))
            {
                int close = field.IndexOf(']');
                if (close > 6 && int.TryParse(field.Substring(6, close - 6), NumberStyles.None, CultureInfo.InvariantCulture, out int index))
                {
                    return "第 " + (index + 1) + " 首的链接无法识别，请使用谱面的分享链接";
                }
            }

            switch (field)
            {
                case "version": return "无法识别 OpenSekai 段位分享文本的版本";
                case "json": return "分享文本不完整或已损坏，请重新复制";
                case "initialLife": return "初始生命需要是大于 0 的整数";
                case "damage": return "缺少扣血表";
                case "songs": return songCount < 2 ? "段位至少需要两首谱面" : "曲目列表无效";
                case "style": return "缺少段位牌样式";
                case "style.label": return "段位名不能为空，也不能包含控制字符";
                case "style.emblem": return "段位牌图案无效";
                case "style.color": return "颜色需要写成 #RRGGBB，例如 #526DC8";
                case "style.tier": return "段位牌的阶位无效";
                case "style.grade": return "段位牌的等级需要是 I 到 X 或 α 到 ω";
            }

            if (field.StartsWith("damage.", StringComparison.Ordinal))
            {
                string judge = field.Substring(7);
                int at = Array.IndexOf(JudgeNames, judge.ToUpperInvariant());
                return at >= 0
                    ? JudgeNames[at] + " 的扣血需要是不小于 0 的整数"
                    : "扣血表里有无法识别的项目：" + judge;
            }

            return "分享文本里有无法识别的字段：" + field;
        }

        /// <summary>The lines of a pasted text that name a level, in order: for adding many songs at once.</summary>
        internal static List<string> LevelLinks(string text)
        {
            var links = new List<string>();
            if (string.IsNullOrEmpty(text)) return links;
            foreach (string token in text.Split(new[] { ' ', '\n', '\r', '\t', ',', '，' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (SonolusServerAddress.TryParse(token, out _, out string level, out _) && !string.IsNullOrEmpty(level))
                {
                    links.Add(token.Trim());
                }
            }

            return links;
        }

        /// <summary>True when the text looks like a shared 段位 (its first line is the format prefix).</summary>
        internal static bool IsShareText(string text) =>
            text != null && text.TrimStart().StartsWith(DanDefinition.PREFIX, StringComparison.Ordinal);
    }
}
