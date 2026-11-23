using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSekai.Infrastructure.Sonolus;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Storage;

namespace OpenSekai.Infrastructure.Packages
{
    public sealed class SongPackageSource
    {
        internal const string WEB_FILE_NAME = "web-source.json";

        public string PageUrl { get; }
        public string DownloadUrl { get; }
        public string SonolusServerUrl { get; }
        public string SonolusLevelUrl { get; }
        internal string SonolusLevelName { get; }

        private SongPackageSource(string pageUrl, string downloadUrl, SonolusServerAddress server, string name)
        {
            PageUrl = pageUrl;
            DownloadUrl = downloadUrl;
            SonolusServerUrl = server?.BaseUrl;
            SonolusLevelUrl = server?.LevelPage(name);
            SonolusLevelName = name;
        }

        internal static bool TryHttps(string value, out Uri uri)
        {
            uri = null;
            return !string.IsNullOrEmpty(value) && !value.Any(char.IsControl) && !value.Contains("\\")
                && Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps
                && uri.Host.Length > 0 && uri.UserInfo.Length == 0;
        }

        internal static SongPackageSource FromWeb(string pageUrl, string downloadUrl)
            => TryHttps(pageUrl, out _) && TryHttps(downloadUrl, out _)
                ? new SongPackageSource(pageUrl, downloadUrl, null, null) : null;

        internal static SongPackageSource FromSonolus(string serverUrl, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "." || name == ".." || name.Any(char.IsControl)
                || !SonolusServerAddress.TryParse(serverUrl, out var server, out var level, out _)
                || level != null) return null;
            return new SongPackageSource(null, null, server, name);
        }

        public static SongPackageSource Read(SongPackage package)
        {
            if (package == null) return null;
            JObject web = ReadRecord(package, WEB_FILE_NAME);
            SongPackageSource source = FromWeb(Text(web, "pageUrl"), Text(web, "downloadUrl"));
            if (source != null) return source;

            JObject sonolus = ReadRecord(package, SonolusLevelInstaller.SOURCE_FILE_NAME);
            return FromSonolus(Text(sonolus, "server"), Text(sonolus, "name"));
        }

        internal static SonolusLevel ReadSavedLevel(SongPackage package)
        {
            var details = ReadRecord(package, SonolusLevelInstaller.SOURCE_FILE_NAME)?["details"] as JObject;
            return SonolusLevel.From(details?["item"]);
        }

        private static string Text(JObject record, string field)
            => record?[field]?.Type == JTokenType.String ? (string)record[field] : null;

        private static JObject ReadRecord(SongPackage package, string fileName)
        {
            string path = Path.Combine(package.RootDirectory, fileName);
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length > 16 * 1024 * 1024) return null;
                SongPackageStorage.RejectReparsePoint(path);
                return JObject.Parse(File.ReadAllText(path));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
            {
                return null;
            }
        }

        internal void WriteWeb(string directory)
        {
            var record = new JObject { ["pageUrl"] = PageUrl, ["downloadUrl"] = DownloadUrl };
            string path = Path.Combine(directory, WEB_FILE_NAME);
            SongPackageStorage.RejectReparsePoint(path);
            AtomicFile.WriteAllText(path, record.ToString(Formatting.Indented));
        }
    }
}
