using System.Linq;
using CriWare;
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
        public static CustomMusicScoreEntry LoadEntry(string path) => CustomMusicScoreEntry.FromPackage(SongPackageStorage.LoadEntry(path));
        public static CustomMusicScoreEntry[] LoadAllEntries() => SongPackageStorage.LoadAllEntries().Select(CustomMusicScoreEntry.FromPackage).ToArray();
        public static CustomMusicScoreEntry LoadFirstEntry() => CustomMusicScoreEntry.FromPackage(SongPackageStorage.LoadFirstEntry());
        public static string GenerateShortId() => SongPackageStorage.GenerateShortId();
        public static string CreateFolderName(CustomMusicScoreManifest manifest) => SongPackageStorage.CreateFolderName(manifest);
    }
}
