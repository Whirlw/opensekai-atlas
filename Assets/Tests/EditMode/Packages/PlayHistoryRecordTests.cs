using NUnit.Framework;
using Newtonsoft.Json;
using OpenSekai.App.Library;

namespace OpenSekai.Tests.EditMode.Packages
{
    /// <summary>
    /// History records written by older builds predate chartHash / rulesVersion / sessionId. The
    /// three fields were appended, so an old line must still deserialise with sensible defaults
    /// instead of failing the whole history file.
    /// </summary>
    public class PlayHistoryRecordTests
    {
        // A record as it looked before the three fields existed (only the fields we assert on).
        private const string LEGACY_JSON = @"{
            ""schemaVersion"": 1,
            ""recordId"": ""legacy-record"",
            ""playedAtUtc"": ""2026-10-20T12:00:00Z"",
            ""appVersion"": ""1.0.0"",
            ""customMusicScoreId"": ""codexbaseline20261101tap"",
            ""customMusicScorePath"": ""/data/CustomMusicScores/Baseline"",
            ""title"": ""Baseline Tap 120 BPM"",
            ""livePlayMode"": ""auto"",
            ""totalScore"": 1000000,
            ""maxCombo"": 221,
            ""totalComboCount"": 221,
            ""clearType"": ""all_perfect""
        }";

        [Test]
        public void LegacyRecordStillDeserialises()
        {
            CustomMusicScorePlayHistoryRecord record =
                JsonConvert.DeserializeObject<CustomMusicScorePlayHistoryRecord>(LEGACY_JSON);

            Assert.That(record, Is.Not.Null);
            Assert.That(record.RecordId, Is.EqualTo("legacy-record"));
            Assert.That(record.Title, Is.EqualTo("Baseline Tap 120 BPM"));
            Assert.That(record.TotalScore, Is.EqualTo(1000000));
            Assert.That(record.TotalComboCount, Is.EqualTo(221));
        }

        [Test]
        public void MissingNewFieldsFallBackToDefaults()
        {
            CustomMusicScorePlayHistoryRecord record =
                JsonConvert.DeserializeObject<CustomMusicScorePlayHistoryRecord>(LEGACY_JSON);

            Assert.That(record.ChartHash, Is.Null.Or.Empty);
            Assert.That(record.RulesVersion, Is.EqualTo(0), "0 means 'written before rules were versioned'");
            Assert.That(record.SessionId, Is.Null.Or.Empty);
        }

        [Test]
        public void NewFieldsRoundTrip()
        {
            var record = new CustomMusicScorePlayHistoryRecord
            {
                SchemaVersion = 2,
                RecordId = "new-record",
                ChartHash = "abc123",
                RulesVersion = 2,
                SessionId = "session000001",
                PlayedAtUtc = "2026-11-03T00:00:00Z",
                CustomMusicScoreId = "id",
                Title = "T",
                TotalScore = 1000000,
                TotalComboCount = 221
            };

            string json = JsonConvert.SerializeObject(record);
            CustomMusicScorePlayHistoryRecord back =
                JsonConvert.DeserializeObject<CustomMusicScorePlayHistoryRecord>(json);

            Assert.That(back.ChartHash, Is.EqualTo("abc123"));
            Assert.That(back.RulesVersion, Is.EqualTo(2));
            Assert.That(back.SessionId, Is.EqualTo("session000001"));
            Assert.That(back.TotalScore, Is.EqualTo(1000000));
        }

        [Test]
        public void UnknownFieldsInNewerRecordsAreIgnored()
        {
            const string FUTURE_JSON = @"{
                ""recordId"": ""future"",
                ""totalScore"": 500,
                ""someFieldFromTheFuture"": ""ignored""
            }";

            CustomMusicScorePlayHistoryRecord record =
                JsonConvert.DeserializeObject<CustomMusicScorePlayHistoryRecord>(FUTURE_JSON);

            Assert.That(record.RecordId, Is.EqualTo("future"));
            Assert.That(record.TotalScore, Is.EqualTo(500));
        }
    }
}
