using System;
using System.IO;
using OpenSekai.Charts;
using OpenSekai.Live.Conversion;
using OpenSekai.Shared.Packages;
using Sekai;
using Sekai.Live;

namespace OpenSekai.Infrastructure.Charts
{
    /// <summary>Dispatches source formats into the common live boundary.</summary>
    public static class RuntimeChartLoader
    {
        public static RuntimeChartDefinition Load(SongPackage package, LiveBundleBuildData config, bool showPair = true,
            bool isMirror = false)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            if (IsNextRushChart(package))
                return NextRushChartLoader.Load(NextRushChartLoader.ReadFile(package.ScorePath), config, showPair, isMirror);
            var document = ChartFileReader.Read(package);
            if (document == null) throw new InvalidDataException("无法读取谱面文件。");
            return RuntimeChartDefinition.Create(document, RuntimeChartConverter.Convert(document, config, showPair, isMirror));
        }

        internal static bool IsNextRushChart(SongPackage package)
        {
            string format = package.Manifest.chartFormat;
            if (string.IsNullOrEmpty(format)) return NextRushChartLoader.IsNativeFileName(package.ScorePath);
            if (string.Equals(format, NextRushChartLoader.Format, StringComparison.OrdinalIgnoreCase)) return true;
            throw new InvalidDataException("不支持的谱面格式：" + format);
        }
    }
}
