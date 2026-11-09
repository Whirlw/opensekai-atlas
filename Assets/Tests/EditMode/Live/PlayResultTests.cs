using NUnit.Framework;
using OpenSekai.Shared.Playback;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>
    /// The result panel decides what to show from these flags, so the rules for "all perfect" and
    /// "has a score at all" are pinned here rather than in the UI.
    /// </summary>
    public class PlayResultTests
    {
        private static LiveScore Score(int totalCombo, int jp = 0, int perfect = 0, int great = 0,
            int good = 0, int bad = 0, int miss = 0, int auto = 0, int totalScore = 0,
            ScoreRank rank = ScoreRank.D, int maxCombo = 0, int life = 0)
        {
            return new LiveScore
            {
                totalComboCount = totalCombo,
                justPerfectCount = jp,
                perfectCount = perfect,
                greatCount = great,
                goodCount = good,
                badCount = bad,
                missCount = miss,
                autoCount = auto,
                totalScore = totalScore,
                rank = rank,
                maxCombo = maxCombo,
                life = life
            };
        }

        private static PlayResult Completed(LiveScore score) =>
            new PlayResult(PlayResultKind.Completed, false, "dir", "Song", 30, score);

        [Test]
        public void AllPerfectRequiresACompletedManualRun()
        {
            LiveScore perfect = Score(totalCombo: 100, jp: 100, totalScore: 1000000, rank: ScoreRank.S);

            Assert.That(Completed(perfect).IsAllPerfectClear, Is.True);
            Assert.That(new PlayResult(PlayResultKind.Retired, false, "dir", "Song", 30, perfect).IsAllPerfectClear,
                Is.False, "retiring forfeits all-perfect even with a full combo");
        }

        [Test]
        public void AllPerfectAcceptsPerfectSplitsAcrossBothWindows()
        {
            LiveScore score = Score(totalCombo: 100, jp: 40, perfect: 60);

            Assert.That(Completed(score).IsAllPerfectClear, Is.True);
        }

        [Test]
        public void AnyLowerJudgementBreaksAllPerfect()
        {
            Assert.That(Completed(Score(totalCombo: 10, jp: 9, great: 1)).IsAllPerfectClear, Is.False);
            Assert.That(Completed(Score(totalCombo: 10, jp: 9, good: 1)).IsAllPerfectClear, Is.False);
            Assert.That(Completed(Score(totalCombo: 10, jp: 9, bad: 1)).IsAllPerfectClear, Is.False);
            Assert.That(Completed(Score(totalCombo: 10, jp: 9, miss: 1)).IsAllPerfectClear, Is.False);
        }

        [Test]
        public void AllAutoRunIsNotAnAllPerfectClear()
        {
            // Auto play never fills the perfect counters, so it must not be reported as all perfect.
            LiveScore auto = Score(totalCombo: 64, auto: 64, totalScore: 1000000, rank: ScoreRank.S);

            Assert.That(Completed(auto).IsAllPerfectClear, Is.False);
        }

        [Test]
        public void EmptyChartIsNotAllPerfect()
        {
            Assert.That(Completed(Score(totalCombo: 0)).IsAllPerfectClear, Is.False);
        }

        [Test]
        public void LoadErrorHidesScoreDetails()
        {
            PlayResult result = PlayResult.FromLoadError("Missing", isAuto: true, "dir", "无法加载曲包信息。");

            Assert.That(result.Kind, Is.EqualTo(PlayResultKind.LoadError));
            Assert.That(result.HasScore, Is.False);
            Assert.That(result.KindText, Is.EqualTo("加载失败"));
            Assert.That(result.Message, Is.EqualTo("无法加载曲包信息。"));
        }

        [Test]
        public void KindTextIsUserFacing()
        {
            Assert.That(Completed(Score(1)).KindText, Is.EqualTo("完成"));
            Assert.That(new PlayResult(PlayResultKind.Retired, false, "d", "t", 1, Score(1)).KindText,
                Is.EqualTo("中途退出"));
            Assert.That(new PlayResult(PlayResultKind.Failed, false, "d", "t", 1, Score(1)).KindText,
                Is.EqualTo("失败"));
        }

        [Test]
        public void BlankTitleFallsBackToAPlaceholder()
        {
            var result = new PlayResult(PlayResultKind.Completed, false, null, "   ", 0, Score(1));

            Assert.That(result.Title, Is.EqualTo("未命名曲目"));
            Assert.That(result.PackageDirectory, Is.Empty);
        }

        [Test]
        public void ScoreFieldsAreCopiedFromTheLiveScore()
        {
            PlayResult result = Completed(Score(totalCombo: 221, jp: 200, perfect: 21, totalScore: 1000000,
                rank: ScoreRank.S, maxCombo: 221, life: 850));

            Assert.That(result.Score, Is.EqualTo(1000000));
            Assert.That(result.TotalCombo, Is.EqualTo(221));
            Assert.That(result.MaxCombo, Is.EqualTo(221));
            Assert.That(result.Rank, Is.EqualTo(ScoreRank.S));
            Assert.That(result.Life, Is.EqualTo(850));
            Assert.That(result.JustPerfect + result.Perfect, Is.EqualTo(221));
        }

        [Test]
        public void HandoffIsASingleSlot()
        {
            PlayResultHandoff.Clear();
            PlayResult first = Completed(Score(1));
            PlayResult second = new PlayResult(PlayResultKind.Retired, false, "d", "t", 1, Score(1));

            PlayResultHandoff.Publish(first);
            PlayResultHandoff.Publish(second);

            Assert.That(PlayResultHandoff.Take(), Is.SameAs(second), "the last publish wins");
            Assert.That(PlayResultHandoff.Take(), Is.Null, "taking empties the slot");
        }
    }
}
