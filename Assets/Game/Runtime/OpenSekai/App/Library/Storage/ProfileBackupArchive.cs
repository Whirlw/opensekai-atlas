using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSekai.Shared.Storage;
using OpenSekai.Shared.Playback;

namespace OpenSekai.App.Library
{
    internal static class ProfileBackupArchive
    {
        private const string FORMAT = "OpenSekai-Player";
        internal const string HISTORY = "PlayHistory.jsonl";
        private const string PROFILE = "profile.json";
        private const string MANIFEST = "manifest.json";
        private const int HISTORY_LIMIT = 64 * 1024 * 1024;
        private const int DAN_LIMIT = 64 * 1024 * 1024;
        internal const long MAX_ARCHIVE_BYTES = 3L * HISTORY_LIMIT + DAN_LIMIT + 1024 * 1024;
        private const int JSON_LIMIT = 16 * 1024;
        internal static readonly UTF8Encoding Utf8 = new(false, true);

        internal static void Write(string destination, string historyPath, ProfileAppearance appearance, DanArchive dan,
            AchievementClaims claims, CoinSpending spending, CancellationToken cancel = default)
        {
            AtomicFile.Write(destination, temp =>
            {
                using var file = File.Create(temp);
                using var zip = new ZipArchive(file, ZipArchiveMode.Create);
                byte[] profile = Utf8.GetBytes(JsonConvert.SerializeObject(appearance));
                using (var target = zip.CreateEntry(PROFILE).Open()) target.Write(profile, 0, profile.Length);
                using var digest = SHA256.Create();
                using (var target = zip.CreateEntry(HISTORY).Open())
                using (var hashed = new CryptoStream(target, digest, CryptoStreamMode.Write))
                using (var writer = new StreamWriter(hashed, Utf8))
                {
                    long bytes = 0;
                    int lineNumber = 0;
                    if (File.Exists(historyPath))
                        foreach (string line in LocalLines(historyPath))
                        {
                            cancel.ThrowIfCancellationRequested();
                            ReadHistoryRow(line, ++lineNumber);
                            bytes += Utf8.GetByteCount(line) + 1;
                            if (bytes > HISTORY_LIMIT) throw new InvalidDataException("History exceeds backup size limit.");
                            writer.Write(line);
                            writer.Write('\n');
                        }
                }
                byte[] challenges = Utf8.GetBytes(JsonConvert.SerializeObject(dan));
                if (challenges.Length > DAN_LIMIT) throw new InvalidDataException("Challenge archive exceeds backup size limit.");
                using (var target = zip.CreateEntry(PlayerProfileStore.DAN_FILE).Open()) target.Write(challenges, 0, challenges.Length);
                byte[] unlocks = Utf8.GetBytes(JsonConvert.SerializeObject(claims));
                if (unlocks.Length > HISTORY_LIMIT) throw new InvalidDataException("Achievement archive exceeds backup size limit.");
                using (var target = zip.CreateEntry(PlayerProfileStore.ACHIEVEMENTS_FILE).Open()) target.Write(unlocks, 0, unlocks.Length);
                var manifest = new JObject
                {
                    ["format"] = FORMAT, ["version"] = 3, ["danSha256"] = Hash(challenges), ["achievementsSha256"] = Hash(unlocks),
                    ["profileSha256"] = Hash(profile), ["historySha256"] = Convert.ToBase64String(digest.Hash)
                };
                if (spending.Count > 0)
                {
                    byte[] purchases = Utf8.GetBytes(JsonConvert.SerializeObject(spending));
                    if (purchases.Length > HISTORY_LIMIT) throw new InvalidDataException("Spending history exceeds backup size limit.");
                    using var target = zip.CreateEntry(PlayerProfileStore.SPENDING_FILE).Open();
                    target.Write(purchases, 0, purchases.Length);
                    manifest["version"] = 4;
                    manifest["spendingSha256"] = Hash(purchases);
                }
                using var metadata = new StreamWriter(zip.CreateEntry(MANIFEST).Open(), Utf8);
                metadata.Write(manifest.ToString(Formatting.None));
                cancel.ThrowIfCancellationRequested();
            });
        }

        internal static (ProfileAppearance, DanArchive, AchievementClaims, CoinSpending) Unpack(string path, string stagedHistory)
        {
            using var file = File.OpenRead(path);
            if (file.Length > MAX_ARCHIVE_BYTES) throw new InvalidDataException("Backup exceeds size limit.");
            using var zip = new ZipArchive(file, ZipArchiveMode.Read);
            using var json = new MemoryStream();
            Copy(zip, MANIFEST, json, JSON_LIMIT);
            var manifest = Parse(Utf8.GetString(json.ToArray()));
            if ((string)manifest["format"] != FORMAT || manifest["version"]?.Type != JTokenType.Integer ||
                (int)manifest["version"] is not (1 or 2 or 3 or 4)) throw new InvalidDataException("Unsupported player backup.");
            // Each version adds one entry: challenges, then unlocks, then spending.
            int version = (int)manifest["version"];
            if (zip.Entries.Count != version + 2) throw new InvalidDataException("Unexpected player backup entries.");
            var dan = new DanArchive();
            if (version >= 2)
            {
                json.SetLength(0);
                Copy(zip, PlayerProfileStore.DAN_FILE, json, DAN_LIMIT);
                byte[] bytes = json.ToArray();
                if (Hash(bytes) != (string)manifest["danSha256"]) throw new InvalidDataException("Challenge checksum mismatch.");
                dan = DanArchive.Decode(Utf8.GetString(bytes));
            }
            var claims = new AchievementClaims();
            if (version >= 3)
            {
                json.SetLength(0);
                Copy(zip, PlayerProfileStore.ACHIEVEMENTS_FILE, json, HISTORY_LIMIT);
                byte[] bytes = json.ToArray();
                if (Hash(bytes) != (string)manifest["achievementsSha256"]) throw new InvalidDataException("Achievement checksum mismatch.");
                claims = AchievementClaims.Decode(Utf8.GetString(bytes));
            }
            var spending = new CoinSpending();
            if (version >= 4)
            {
                json.SetLength(0);
                Copy(zip, PlayerProfileStore.SPENDING_FILE, json, HISTORY_LIMIT);
                byte[] bytes = json.ToArray();
                if (Hash(bytes) != (string)manifest["spendingSha256"]) throw new InvalidDataException("Spending checksum mismatch.");
                spending = CoinSpending.Decode(Utf8.GetString(bytes));
            }
            json.SetLength(0);
            Copy(zip, PROFILE, json, JSON_LIMIT);
            byte[] profile = json.ToArray();
            if (Hash(profile) != (string)manifest["profileSha256"]) throw new InvalidDataException("Profile checksum mismatch.");
            using var output = File.Create(stagedHistory);
            using var digest = SHA256.Create();
            using (var hashed = new CryptoStream(output, digest, CryptoStreamMode.Write))
                Copy(zip, HISTORY, hashed, HISTORY_LIMIT);
            if (Convert.ToBase64String(digest.Hash) != (string)manifest["historySha256"])
                throw new InvalidDataException("History checksum mismatch.");
            var fields = Parse(Utf8.GetString(profile));
            if (fields["nickname"]?.Type != JTokenType.String || fields["avatarId"]?.Type != JTokenType.String)
                throw new InvalidDataException("Invalid player appearance.");
            return (new ProfileAppearance((string)fields["nickname"], (string)fields["avatarId"]), dan, claims, spending);
        }

        internal static IEnumerable<string> LocalLines(string path)
        {
            // A background preview/export must allow a live session to append to the journal.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Utf8);
            string line;
            while ((line = reader.ReadLine()) != null) yield return line;
        }

        private static void Copy(ZipArchive zip, string name, Stream output, int limit)
        {
            var entry = zip.GetEntry(name) ?? throw new InvalidDataException("Missing " + name);
            if (entry.Length > limit) throw new InvalidDataException("Oversized " + name);
            using var source = entry.Open();
            var buffer = new byte[8192];
            long total = 0;
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) != 0)
            {
                total += read;
                if (total > entry.Length) throw new InvalidDataException("Invalid size of " + name);
                output.Write(buffer, 0, read);
            }
            if (total != entry.Length) throw new InvalidDataException("Truncated " + name);
        }

        private static string Hash(byte[] data)
        {
            using var digest = SHA256.Create();
            return Convert.ToBase64String(digest.ComputeHash(data));
        }

        internal static JObject Parse(string line)
        {
            using var reader = new JsonTextReader(new StringReader(line)) { DateParseHandling = DateParseHandling.None };
            var row = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new InvalidDataException("Trailing JSON content.");
            return row;
        }

        // An export authorizes clearing the journal. Prove that every exported row can use
        // the same import path before the atomic writer replaces the last good backup.
        internal static (JObject, CustomMusicScorePlayHistoryRecord) ReadHistoryRow(string line, int lineNumber)
        {
            try
            {
                var row = Parse(line);
                var record = row.ToObject<CustomMusicScorePlayHistoryRecord>();
                ValidateRecord(row, record, lineNumber);
                return (row, record);
            }
            catch (Exception e) when (e is JsonException or FormatException or OverflowException or InvalidCastException)
            {
                throw new InvalidDataException("Invalid history record at line " + lineNumber, e);
            }
        }

        private static void ValidateRecord(JObject row, CustomMusicScorePlayHistoryRecord record, int line)
        {
            if (row["schemaVersion"]?.Type != JTokenType.Integer || record.SchemaVersion < 1 || record.SchemaVersion > 3 ||
                row["recordId"] != null && row["recordId"].Type != JTokenType.Null && row["recordId"].Type != JTokenType.String)
                throw new InvalidDataException("Invalid history schema at line " + line);
            if (record.SchemaVersion != 3) return;
            if (!record.EndReason.HasValue || !Enum.IsDefined(typeof(PlayResultKind), record.EndReason.Value) ||
                record.EndReason == PlayResultKind.LoadError || record.MaximumScore <= 0 ||
                !RankingRules.SupportsFormula(record.FormulaVersion) || string.IsNullOrWhiteSpace(record.PlayedAtUtc))
                throw new InvalidDataException("Invalid play at line " + line);
            foreach (string field in _judgementFields)
                if (row[field]?.Type != JTokenType.Integer || (long)row[field] < 0)
                    throw new InvalidDataException("Invalid " + field + " at line " + line);
        }

        private static readonly string[] _judgementFields =
            { "perfectCount", "greatCount", "goodCount", "badCount", "missCount", "totalScore", "totalComboCount" };

        internal static JObject TryLocalRow(string line)
        {
            try { return Parse(line); }
            catch (JsonException) { return null; }
            catch (InvalidDataException) { return null; }
        }
    }
}
