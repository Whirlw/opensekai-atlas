using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using OpenSekai.Infrastructure.Sonolus;
using OpenSekai.Shared.Storage;
using UnityEngine;

namespace OpenSekai.Infrastructure.Packages
{
    // Records live outside imported packages and are keyed by the publisher's chart identity.
    internal static class ChartRankCache
    {
        internal sealed class Verdict
        {
            public string ScoreKey;
            public int Level;
            public bool Ranked;
            public bool UnreadableResponse;
            public string DataHash;

            internal bool Matches(string hash, int level) => Ranked && Level == level && level > 0
                && !string.IsNullOrEmpty(hash) && ScoreKey == hash;
        }

        private static readonly Dictionary<string, Verdict> _loaded = new(StringComparer.Ordinal);
        internal static event Action<string> Changed;
        private static readonly Regex _id = new(@"^[1-9][0-9]{0,15}$");
        private static readonly Regex _name = new(@"^UnCh-[0-9a-f]{32}$");

        internal static string Key(SongPackageSource source)
        {
            if (source?.IsWeb == true)
            {
                string page = WebKey(source.PageUrl);
                string download = WebKey(source.DownloadUrl);
                return page != null && download != null && page != download ? null : page ?? download;
            }
            return source?.SonolusLevelUrl != null
                && SonolusServerAddress.TryParse(source.SonolusLevelUrl, out var server, out var name, out _)
                ? SonolusKey(server, name) : null;
        }

        private static string WebKey(string url)
        {
            if (!SongPackageSource.TryHttps(url, out var uri) || uri.Host != "opensekai.com" || !uri.IsDefaultPort
                || (uri.AbsolutePath != "/chart" && uri.AbsolutePath != "/download")) return null;
            var ids = uri.Query.TrimStart('?').Split('&').Where(x => x.StartsWith("id=", StringComparison.Ordinal)).ToArray();
            return ids.Length == 1 && _id.IsMatch(ids[0].Substring(3)) ? "os-" + ids[0].Substring(3) : null;
        }

#if UNITY_EDITOR
        /// <summary>Base URL of the editor's sample server, whose charts are looked up like UntitledCharts'. Null in normal use.</summary>
        internal static string CaptureServer { get; set; }
#endif

        internal static string UntitledKey(SonolusServerAddress server, string name) =>
            IsUntitled(server) && name != null && _name.IsMatch(name) ? name : null;

        private static bool IsUntitled(SonolusServerAddress server)
        {
            string url = server?.BaseUrl;
#if UNITY_EDITOR
            if (url != null && url == CaptureServer) return true;
#endif
            return url == "https://untitledcharts.com";
        }

        internal static bool IsSbuga(SonolusServerAddress server) =>
            server?.Host is "sonolus.sbuga.com" or "sonolus.sbuga.com:443";

        internal static string SonolusKey(SonolusServerAddress server, string name)
        {
            if (!IsSbuga(server)) return UntitledKey(server, name);
            if (string.IsNullOrWhiteSpace(name)) return null;
            using var sha = SHA256.Create();
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(server.Prefix + "/levels/" + Uri.EscapeDataString(name)));
            return "sbuga-" + BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant();
        }

        internal static bool IsRankedLevel(SonolusServerAddress server, SonolusLevel level) =>
            level != null && (IsSbuga(server) || (UntitledKey(server, level.Name) != null && IsStaffPick(level)));

        internal static bool IsStaffPick(SonolusLevel level) => level?.Tags?.Any(tag =>
            tag.Icon == "trophy" && (string.IsNullOrEmpty(tag.Title)
                || string.Equals(tag.Title, "Staff Pick", StringComparison.OrdinalIgnoreCase))) == true;

        internal static Verdict FromLevel(SonolusServerAddress server, SonolusLevel level, string scoreKey) => new()
        {
            Ranked = IsRankedLevel(server, level), Level = (int)Math.Round(level.Rating), DataHash = level.Data?.Hash,
            ScoreKey = scoreKey
        };

        internal static Verdict FromWeb(string zone, int level, string hash)
        {
            if (level <= 0 || (zone != "quality" && zone != "abstract" && zone != "beginner")) return null;
            return new Verdict { Ranked = zone == "quality", Level = level, ScoreKey = hash };
        }

        internal static Verdict FromUnreadableResponse(string hash, int level) =>
            new() { ScoreKey = hash, Level = level, UnreadableResponse = true };

        internal static void RecordWebDownload(string key, string zone, int publishedLevel, string hash, int installedLevel) =>
            Save(key, FromWeb(zone, publishedLevel, hash) ?? FromUnreadableResponse(hash, installedLevel));

        internal static Verdict ParseDetail(string html, string id)
        {
            if (string.IsNullOrEmpty(html) || !_id.IsMatch(id)
                || !html.Contains("href=\"/download?id=" + id + "\"")) return null;
            var labels = Regex.Match(html, @"<div class=""detail-labels"">(?<body>.*?)</div>", RegexOptions.Singleline);
            var body = labels.Groups["body"].Value;
            var zone = Regex.Match(body, @"<span class=""zone-badge (quality|abstract|beginner)"">");
            var level = Regex.Match(body, @"<span class=""badge [a-z]+""[^>]*>[^<]*·\s*([0-9]+)\s*</span>");
            return zone.Success && int.TryParse(level.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
                ? FromWeb(zone.Groups[1].Value, value, null) : null;
        }

        private static string PathOf(string key) => Path.Combine(Application.persistentDataPath, "ChartRanks", key + ".json");

        internal static Verdict Read(string key)
        {
            if (key == null) return null;
            if (_loaded.TryGetValue(key, out var known)) return known;
            try
            {
                string path = PathOf(key);
                known = File.Exists(path) ? JsonConvert.DeserializeObject<Verdict>(File.ReadAllText(path)) : null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                Debug.LogWarning("[Ranked] " + e.Message);
            }
            return _loaded[key] = known;
        }

        internal static void Save(string key, Verdict verdict)
        {
            if (key == null || verdict == null) return;
            _loaded[key] = verdict;
            try { AtomicFile.WriteAllText(PathOf(key), JsonConvert.SerializeObject(verdict)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Debug.LogWarning("[Ranked] " + e.Message); }
            Changed?.Invoke(key);
        }
    }
}
