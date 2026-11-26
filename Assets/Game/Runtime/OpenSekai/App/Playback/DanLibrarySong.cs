using System;
using System.Linq;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Infrastructure.Sonolus;

namespace OpenSekai.App.Playback
{
    /// <summary>A package reference that works across installs, independently of the local package directory.</summary>
    internal sealed class DanLibrarySong
    {
        private const string PREFIX = "opensekai-chart:";

        internal string ScoreKey { get; }
        internal string AudioHash { get; }
        internal string Title { get; }
        internal string PageUrl { get; }
        internal string Reference => PREFIX + ScoreKey + "/" + AudioHash + "/" + Uri.EscapeDataString(Title)
            + "/" + Uri.EscapeDataString(PageUrl ?? string.Empty);

        private DanLibrarySong(string scoreKey, string audioHash, string title, string pageUrl)
            => (ScoreKey, AudioHash, Title, PageUrl) = (scoreKey.ToLowerInvariant(), audioHash.ToLowerInvariant(), title, pageUrl);

        internal static bool TryParse(string reference, out DanLibrarySong song)
        {
            song = null;
            if (reference == null || !reference.StartsWith(PREFIX, StringComparison.Ordinal)) return false;
            string[] fields = reference.Substring(PREFIX.Length).Split('/');
            if (fields.Length != 4 || !IsHash(fields[0]) || !IsHash(fields[1])) return false;
            string title = Uri.UnescapeDataString(fields[2]);
            string page = Uri.UnescapeDataString(fields[3]);
            if (string.IsNullOrWhiteSpace(title) || title.Any(char.IsControl)
                || (page.Length > 0 && !SongPackageSource.TryHttps(page, out _))) return false;
            song = new DanLibrarySong(fields[0], fields[1], title, page.Length > 0 ? page : null);
            return true;
        }

        internal static bool IsCandidate(SongPackageItem item) => item.HasManifest && item.HasScore && item.HasAudio
            && IsHash(item.ScoreKey);

        internal static string ReferenceFor(SongPackageItem item)
        {
            SongPackageSource source = SongPackageSource.Read(item.Entry);
            if (source?.SonolusLevelUrl != null
                && SonolusServerAddress.TryParse(source.SonolusLevelUrl, out var server, out var level, out _)
                && SonolusLevelInstaller.Shared.FindPlayableInstalled(server, level)?.RootDirectory == item.Entry.RootDirectory)
                return source.SonolusLevelUrl;

            string audioHash = SongPackageHash.Compute(item.Entry.AudioPath);
            if (!IsHash(audioHash)) return null;
            var manifest = item.Entry.Manifest;
            string title = string.IsNullOrWhiteSpace(manifest.title) ? manifest.scoreTitle : manifest.title;
            title = string.IsNullOrWhiteSpace(title) ? "未命名谱面" : new string(title.Trim().Where(c => !char.IsControl(c)).ToArray());
            if (string.IsNullOrWhiteSpace(title)) title = "未命名谱面";
            return new DanLibrarySong(item.ScoreKey, audioHash, title, source?.PageUrl).Reference;
        }

        internal SongPackageItem FindInstalled(SongPackageManager manager)
            => manager.LoadItems().FirstOrDefault(item => IsCandidate(item) && item.ScoreKey == ScoreKey
                && MatchesAudio(item.Entry.AudioPath));

        internal bool MatchesAudio(string path) => SongPackageHash.Compute(path) == AudioHash;

        private static bool IsHash(string value) => value?.Length == 64 && value.All(Uri.IsHexDigit);
    }
}
