using System;
using System.IO;
using OpenSekai.Charts;
using OpenSekai.Charts.Serialization;
using Sekai;
using Sekai.Live;
using UnityEngine;
using OpenSekai.Shared.Conversion;
using OpenSekai.Shared.Packages;
using OpenSekai.Live.Conversion;

namespace OpenSekai.Infrastructure.Charts
{
    public static class ChartFileReader
    {
        public static ChartDocument Read(SongPackage package)
        {
            if (package == null || !File.Exists(package.ScorePath)) return null;
            return ReadFile(package.ScorePath, package.MusicId);
        }

        public static ChartDocument ReadFile(string path, int musicId)
        {
            string text = File.ReadAllText(path);
            string extension = Path.GetExtension(path);
            if (string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)) return ChartJson.Read(text, musicId);
            if (!string.Equals(extension, ".sus", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("不支持的谱面格式：" + extension);

            // Match the old import: never bake mirror or generated hold-combo notes into source JSON.
            var config = ResourceCatalog.LiveBundleConfig.Load();
            var score = new SusRuntimeConverter().Convert(text, config, isNeedCombo: false, _isMirror: false);
            var document = RuntimeChartImporter.FromMusicScore(score);
            document.MusicId = musicId;
            return document;
        }

        public static void Write(SongPackage package, ChartDocument document)
        {
            if (package == null || document == null) return;
            var copy = document.Copy();
            copy.MusicId = package.MusicId;
            Directory.CreateDirectory(package.RootDirectory);
            File.WriteAllText(package.ScorePath, ChartJson.Write(copy));
        }
    }
}
