using System.Linq;
using CriWare;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Packages;
namespace OpenSekai.App.Library
{
    public static class CustomMusicScoreStorage
    {
        public const string DIRECTORY_NAME = SongPackageStorage.DIRECTORY_NAME;
        public const string MANIFEST_FILE_NAME = SongPackageStorage.MANIFEST_FILE_NAME;
        public const string PLAY_HISTORY_FILE_NAME = SongPackageStorage.PLAY_HISTORY_FILE_NAME;
        public static string RootDirectory => SongPackageStorage.RootDirectory;
        public static string PlayHistoryPath => SongPackageStorage.PlayHistoryPath;
        public static CustomMusicScoreEntry LoadEntry(string path) => CustomMusicScoreEntry.FromPackage(
            SongPackageManager.Shared.FindCachedItem(path)?.Entry ?? SongPackageStorage.LoadEntry(path));
        // The package list is served from the library cache, never by walking the folders again.
        public static CustomMusicScoreEntry[] LoadAllEntries() => SongPackageManager.Shared.LoadItems().Select(item => CustomMusicScoreEntry.FromPackage(item.Entry)).ToArray();
        public static CustomMusicScoreEntry LoadFirstEntry() => CustomMusicScoreEntry.FromPackage(SongPackageManager.Shared.LoadItems().FirstOrDefault()?.Entry);
        public static string GenerateShortId() => SongPackageStorage.GenerateShortId();
        public static string CreateFolderName(CustomMusicScoreManifest manifest) => SongPackageStorage.CreateFolderName(manifest);
    }
}
