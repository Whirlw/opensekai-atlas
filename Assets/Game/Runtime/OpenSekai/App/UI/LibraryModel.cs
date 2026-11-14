using System;
using System.Collections.Generic;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Packages;

namespace OpenSekai.App.UI
{
    internal sealed class LibraryModel
    {
        private int _sortMode;

        internal string SortLabel => _sortMode switch
        {
            1 => "排序：等级",
            2 => "排序：最近",
            _ => "排序：标题"
        };

        internal void CycleSort() => _sortMode = (_sortMode + 1) % 3;

        internal IReadOnlyList<SongPackageItem> Order(IReadOnlyList<SongPackageItem> items)
        {
            var ordered = new List<SongPackageItem>(items);
            ordered.Sort(CompareItems);
            return ordered;
        }

        private int CompareItems(SongPackageItem a, SongPackageItem b)
        {
            string left = BuildTitle(a);
            string right = BuildTitle(b);
            switch (_sortMode)
            {
                case 1:
                {
                    int levelCompare = PlayLevelOf(b).CompareTo(PlayLevelOf(a));
                    return levelCompare != 0
                        ? levelCompare
                        : string.Compare(left, right, StringComparison.CurrentCulture);
                }
                case 2:
                {
                    int timeCompare = b.LastWriteTime.CompareTo(a.LastWriteTime);
                    return timeCompare != 0
                        ? timeCompare
                        : string.Compare(left, right, StringComparison.CurrentCulture);
                }
                default:
                    return string.Compare(left, right, StringComparison.CurrentCulture);
            }
        }

        private static int PlayLevelOf(SongPackageItem item)
        {
            try
            {
                return item?.Entry?.Manifest != null ? item.Entry.Manifest.playLevel : 0;
            }
            catch
            {
                return 0;
            }
        }

        internal static string BuildTitle(SongPackageItem item)
        {
            SongPackageManifest manifest = item.Entry?.Manifest;
            if (manifest == null)
            {
                return "未命名曲包";
            }
            return string.IsNullOrWhiteSpace(manifest.title) ? (manifest.scoreTitle ?? "未命名曲包") : manifest.title;
        }

        internal static string BuildMeta(SongPackageItem item)
        {
            SongPackageManifest manifest = item.Entry?.Manifest;
            string difficulty = manifest != null && !string.IsNullOrWhiteSpace(manifest.musicDifficultyType)
                ? manifest.musicDifficultyType.ToUpperInvariant()
                : "—";
            string level = manifest != null && manifest.playLevel > 0 ? " Lv." + manifest.playLevel : string.Empty;
            return difficulty + level + "  ·  " + item.StatusText;
        }
    }
}
