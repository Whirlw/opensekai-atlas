using System;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using OpenSekai.App.Library;

namespace OpenSekai.Tests.EditMode.Packages
{
    /// <summary>
    /// A new run is folded into its package's summary in memory and saved as a snapshot: the history
    /// file is only folded in full when there is no snapshot, or on a reload (the refresh button).
    /// </summary>
    public class PlayHistorySummaryCacheTests
    {
        private string _root;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "opensekai-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _path = Path.Combine(_root, "PlayHistory.jsonl");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private static CustomMusicScorePlayHistoryRecord Run(string key, int score, string playedAt)
        {
            return new CustomMusicScorePlayHistoryRecord { scoreKey = key, totalScore = score, playedAtUtc = playedAt };
        }

        private void WriteExisting(params CustomMusicScorePlayHistoryRecord[] records)
        {
            foreach (CustomMusicScorePlayHistoryRecord record in records)
            {
                File.AppendAllText(_path, JsonConvert.SerializeObject(record) + Environment.NewLine);
            }
        }

        [Test]
        public void ALowerRunKeepsTheBestButMovesLastPlayedAndCount()
        {
            WriteExisting(Run("a", 300, "2026-11-01T00:00:00Z"), Run("b", 100, "2026-11-02T00:00:00Z"));
            var cache = new PlayHistorySummaryCache();

            CustomMusicScorePlayHistorySummary summary = cache.Append(_path, Run("a", 200, "2026-11-16T00:00:00Z"));

            Assert.That(summary.Best.totalScore, Is.EqualTo(300));
            Assert.That(summary.LastPlayedAtUtc, Is.EqualTo("2026-11-16T00:00:00Z"));
            Assert.That(summary.PlayCount, Is.EqualTo(2));
            Assert.That(cache.Load(_path)["b"].PlayCount, Is.EqualTo(1), "other packages are untouched");
        }

        [Test]
        public void AHigherRunBecomesTheBest()
        {
            WriteExisting(Run("a", 300, "2026-11-01T00:00:00Z"));
            var cache = new PlayHistorySummaryCache();

            CustomMusicScorePlayHistorySummary summary = cache.Append(_path, Run("a", 400, "2026-11-16T00:00:00Z"));

            Assert.That(summary.Best.totalScore, Is.EqualTo(400));
            Assert.That(summary.PlayCount, Is.EqualTo(2));
        }

        [Test]
        public void AppendsAreFoldedWithoutReadingTheFileAgain()
        {
            var cache = new PlayHistorySummaryCache();
            var loaded = cache.Load(_path);

            cache.Append(_path, Run("a", 100, "2026-11-16T00:00:00Z"));
            cache.Append(_path, Run("a", 250, "2026-11-16T00:01:00Z"));

            Assert.That(cache.Load(_path), Is.SameAs(loaded));
            var fromFile = CustomMusicScorePlayHistoryStorage.Summarize(File.ReadLines(_path));
            Assert.That(loaded["a"].Best.totalScore, Is.EqualTo(fromFile["a"].Best.totalScore));
            Assert.That(loaded["a"].PlayCount, Is.EqualTo(fromFile["a"].PlayCount));
        }

        [Test]
        public void AFileChangedElsewhereOnlyShowsAfterAReload()
        {
            WriteExisting(Run("a", 100, "2026-11-01T00:00:00Z"));
            var cache = new PlayHistorySummaryCache();
            cache.Load(_path);

            WriteExisting(Run("c", 900, "2026-11-02T00:00:00Z"));

            Assert.That(cache.Load(_path).ContainsKey("c"), Is.False);
            Assert.That(cache.Reload(_path).ContainsKey("c"), Is.True);
            Assert.That(new PlayHistorySummaryCache().Load(_path).ContainsKey("c"), Is.True, "the reload saved the snapshot");
        }

        [Test]
        public void ACurrentSnapshotCanLoadWithoutOpeningTheHistory()
        {
            var cache = new PlayHistorySummaryCache();
            cache.Append(_path, Run("a", 100, "2026-11-01T00:00:00Z"));
            using (File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var relaunched = new PlayHistorySummaryCache().Load(_path);
                Assert.That(relaunched["a"].PlayCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void ANewLaunchIncludesRunsWrittenAfterTheSnapshot()
        {
            var cache = new PlayHistorySummaryCache();
            cache.Append(_path, Run("a", 100, "2026-11-01T00:00:00Z"));
            WriteExisting(Run("a", 250, "2026-11-02T00:00:00Z"));

            var relaunched = new PlayHistorySummaryCache().Load(_path);

            Assert.That(File.Exists(PlayHistorySummaryCache.SnapshotPathOf(_path)), Is.True);
            Assert.That(relaunched["a"].Best.totalScore, Is.EqualTo(250));
            Assert.That(relaunched["a"].PlayCount, Is.EqualTo(2));
            Assert.That(relaunched["a"].LastPlayedAtUtc, Is.EqualTo("2026-11-02T00:00:00Z"));
        }

        [Test]
        public void AnInterruptedLineDoesNotSwallowTheNextCompletedRun()
        {
            var cache = new PlayHistorySummaryCache();
            cache.Append(_path, Run("a", 100, "2026-11-01T00:00:00Z"));
            File.AppendAllText(_path, "{\"scoreKey\":");
            var summary = cache.Append(_path, Run("a", 250, "2026-11-02T00:00:00Z"));

            Assert.That(summary.PlayCount, Is.EqualTo(2));
            var rebuilt = new PlayHistorySummaryCache().Reload(_path)["a"];
            Assert.That(rebuilt.PlayCount, Is.EqualTo(2));
            Assert.That(rebuilt.Best.totalScore, Is.EqualTo(250));
        }
    }
}
