using System.Collections.Generic;
using System.Linq;
using OpenSekai.App.Library;
using OpenSekai.Shared.Packages;
using CriWare;
using OpenSekai.Infrastructure.Packages;
namespace Sekai.CustomMusicScoreManager
{
    /// <summary>Compatibility facade for the retained song-manager UI and editor.</summary>
    public static class CustomMusicScoreManagerService
    {
        public static IReadOnlyList<CustomMusicScoreManagerItem> LoadItems() => SongPackageManager.Shared.LoadItems().Select(item =>
            new CustomMusicScoreManagerItem(CustomMusicScoreEntry.FromPackage(item.Entry),item.LastWriteTime,
                item.HasManifest,item.HasScore,item.HasAudio,item.HasJacket)).ToArray();
        public static CustomMusicScoreEntry CreateNewEntry() => Wrap(SongPackageManager.Shared.CreateNewEntry());
        public static CustomMusicScoreEntry DuplicateEntry(CustomMusicScoreEntry source) => Wrap(SongPackageManager.Shared.DuplicateEntry(source?.Package));
        public static void DeleteEntry(CustomMusicScoreEntry entry) => SongPackageManager.Shared.DeleteEntry(entry?.Package);
        public static CustomMusicScoreEntry ImportFolder(string path) => Wrap(SongPackageManager.Shared.ImportFolder(path));
        public static CustomMusicScoreEntry ImportZip(string path) => Wrap(SongPackageManager.Shared.ImportZip(path));
        public static string ExportZip(CustomMusicScoreEntry entry,string destinationPath = null) => SongPackageManager.Shared.ExportZip(entry?.Package,destinationPath);
        public static CustomMusicScoreEntry SaveManifest(CustomMusicScoreEntry entry,CustomMusicScoreManifest manifest) => Wrap(SongPackageManager.Shared.SaveManifest(entry?.Package,manifest));
        public static CustomMusicScoreEntry ReplaceAudioFile(CustomMusicScoreEntry entry,string path) => Wrap(SongPackageManager.Shared.ReplaceAudioFile(entry?.Package,path));
        public static CustomMusicScoreEntry ReplaceJacketFile(CustomMusicScoreEntry entry,string path) => Wrap(SongPackageManager.Shared.ReplaceJacketFile(entry?.Package,path));
        public static CustomMusicScoreEntry ReplaceScoreFile(CustomMusicScoreEntry entry,string path) => Wrap(SongPackageManager.Shared.ReplaceScoreFile(entry?.Package,path));
        public static CustomMusicScoreEntry ReplaceVideoFile(CustomMusicScoreEntry entry,string path) => Wrap(SongPackageManager.Shared.ReplaceVideoFile(entry?.Package,path));
        private static CustomMusicScoreEntry Wrap(SongPackage package) => CustomMusicScoreEntry.FromPackage(package);
    }
}
