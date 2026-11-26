using System;
using Sekai;
using Sekai.Core.Live;
using UnityEngine;

namespace OpenSekai.Shared.Playback
{
    /// <summary>Why the live session ended. LoadError never reaches the live scene: PlayService
    /// reports it before the transition, so it is not part of this enum.</summary>
    public enum PlayResultKind
    {
        Completed,
        Failed,
        Retired,
        /// <summary>The session never started (missing package, bad chart, missing audio).</summary>
        LoadError
    }

    /// <summary>Single immutable snapshot of one finished session, shared by the result panel and
    /// (later) history writing. It owns no Unity objects and holds no mutable note state.</summary>
    public sealed class PlayResult
    {
        public PlayResultKind Kind { get; }
        public bool IsAuto { get; }
        public string PackageDirectory { get; }
        public string Title { get; }
        public int PlayLevel { get; }
        public string Difficulty { get; }
        public int Score { get; }
        public int MaxCombo { get; }
        public int TotalCombo { get; }
        public ScoreRank Rank { get; }
        public int Life { get; }
        public ClearAward Award { get; }
        public RankingSettlement Settlement { get; }
        public int Perfect { get; }
        public int Great { get; }
        public int Good { get; }
        public int Bad { get; }
        public int Miss { get; }
        public int AutoCount { get; }
        public bool IsAllPerfect => Award == ClearAward.AllPerfect;

        /// <summary>User-facing failure text; only set for <see cref="PlayResultKind.LoadError"/>.</summary>
        public string Message { get; }

        /// <summary>True when no score was produced (load error), so the panel hides score details.</summary>
        public bool HasScore => Kind != PlayResultKind.LoadError;

        /// <summary>True only for a finished manual run with every note in the Perfect window.</summary>
        public bool IsAllPerfectClear => Award == ClearAward.AllPerfect;

        public string KindText
        {
            get
            {
                switch (Kind)
                {
                    case PlayResultKind.Completed: return "完成";
                    case PlayResultKind.Retired: return "中途退出";
                    case PlayResultKind.LoadError: return "加载失败";
                    default: return "失败";
                }
            }
        }

        public PlayResult(
            PlayResultKind kind,
            bool isAuto,
            string packageDirectory,
            string title,
            int playLevel,
            LiveScore score,
            string message = null, RankingSettlement settlement = null, string difficulty = null)
        {
            Kind = kind;
            IsAuto = isAuto;
            PackageDirectory = packageDirectory ?? string.Empty;
            Title = string.IsNullOrWhiteSpace(title) ? "未命名曲目" : title;
            PlayLevel = playLevel;
            Difficulty = difficulty ?? string.Empty;
            Message = message ?? string.Empty;
            Score = score.totalScore;
            MaxCombo = score.maxCombo;
            TotalCombo = score.totalComboCount;
            Rank = score.rank;
            Life = score.life;
            Award = RankingRules.Award(kind, isAuto, score);
            Settlement = settlement;
            Perfect = score.perfectCount;
            Great = score.greatCount;
            Good = score.goodCount;
            Bad = score.badCount;
            Miss = score.missCount;
            AutoCount = score.autoCount;
        }

        public static PlayResult FromLoadError(string title, bool isAuto, string packageDirectory, string message)
        {
            return new PlayResult(PlayResultKind.LoadError, isAuto, packageDirectory, title, 0, default, message);
        }

        public static PlayResult FromBootData(Sekai.FreeLiveBootData boot, PlayResultKind kind, LiveScore score,
            RankingSettlement settlement = null)
        {
            if (boot == null)
            {
                return new PlayResult(kind, false, string.Empty, null, 0, score);
            }

            string title = boot.CustomMusicScoreTitle;
            if (string.IsNullOrWhiteSpace(title) && boot.MusicData != null && boot.MusicData.Music != null)
            {
                title = boot.MusicData.Music.title;
            }

            return new PlayResult(
                kind,
                boot.IsAuto,
                boot.CustomMusicScorePath,
                title,
                boot.MusicData != null ? boot.MusicData.PlayLevel : 0,
                score, settlement: settlement, difficulty: boot.MusicData?.DifficultyString);
        }
    }

    /// <summary>
    /// One-slot handoff from the live scene (which is about to unload) to the library scene.
    /// The library consumes it on start; nothing here survives more than one scene transition.
    /// </summary>
    public static class PlayResultHandoff
    {
        public static PlayResult Pending { get; private set; }

        public static void Publish(PlayResult result)
        {
            Pending = result;
        }

        public static PlayResult Take()
        {
            PlayResult current = Pending;
            Pending = null;
            return current;
        }

        public static void Clear()
        {
            Pending = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewRun()
        {
            Pending = null;
        }
    }
}
