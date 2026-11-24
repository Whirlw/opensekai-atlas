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
        private const int JSON_LIMIT = 16 * 1024;
        internal static readonly UTF8Encoding Utf8 = new(false, true);

        internal static void Write(string destination, string historyPath, ProfileAppearance appearance, DanArchive dan,
            AchievementClaims claims, CancellationToken cancel = default)
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
                    if (File.Exists(historyPath))
                        foreach (string line in LocalLines(historyPath))
                        {
                            cancel.ThrowIfCancellationRequested();
                            if (TryLocalRow(line) == null) continue;
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
                using (var target = zip.CreateEntry(PlayerProfileStore.ACHIEVEMENTS_FILE).Open()) target.Write(unlocks, 0, unlocks.Length);
                var manifest = new JObject
                {
                    ["format"] = FORMAT, ["version"] = 3, ["danSha256"] = Hash(challenges), ["achievementsSha256"] = Hash(unlocks),
                    ["profileSha256"] = Hash(profile), ["historySha256"] = Convert.ToBase64String(digest.Hash)
                };
                using var metadata = new StreamWriter(zip.CreateEntry(MANIFEST).Open(), Utf8);
                metadata.Write(manifest.ToString(Formatting.None));
                cancel.ThrowIfCancellationRequested();
            });
        }

        internal static (ProfileAppearance, DanArchive, AchievementClaims) Unpack(string path, string stagedHistory)
        {
            using var file = File.OpenRead(path);
            if (file.Length > HISTORY_LIMIT + DAN_LIMIT + 1024 * 1024) throw new InvalidDataException("Backup exceeds size limit.");
            using var zip = new ZipArchive(file, ZipArchiveMode.Read);
            using var json = new MemoryStream();
            Copy(zip, MANIFEST, json, JSON_LIMIT);
            var manifest = Parse(Utf8.GetString(json.ToArray()));
            if ((string)manifest["format"] != FORMAT || manifest["version"]?.Type != JTokenType.Integer ||
                (int)manifest["version"] is not (1 or 2 or 3)) throw new InvalidDataException("Unsupported player backup.");
            // Version 2 added the challenges, version 3 the unlocked achievements.
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
                Copy(zip, PlayerProfileStore.ACHIEVEMENTS_FILE, json, JSON_LIMIT);
                byte[] bytes = json.ToArray();
                if (Hash(bytes) != (string)manifest["achievementsSha256"]) throw new InvalidDataException("Achievement checksum mismatch.");
                claims = AchievementClaims.Decode(Utf8.GetString(bytes));
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
            return (new ProfileAppearance((string)fields["nickname"], (string)fields["avatarId"]), dan, claims);
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

        internal static JObject TryLocalRow(string line)
        {
            try { return Parse(line); }
            catch (JsonException) { return null; }
            catch (InvalidDataException) { return null; }
        }
    }
}
