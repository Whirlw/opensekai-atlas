using System;
using System.IO;
using System.IO.Compression;

namespace OpenSekai.Infrastructure.Packages
{
    /// <summary>Extract only into a new, private staging directory. Never trust archive paths.</summary>
    public static class SafeZipExtractor
    {
        public const long MAX_TOTAL_BYTES = 2L * 1024 * 1024 * 1024;
        public const long MAX_FILE_BYTES = 512L * 1024 * 1024;
        public const int MAX_FILES = 4096;
        public const long FREE_SPACE_RESERVE_BYTES = 32L * 1024 * 1024;

        /// <summary>Call on Unity's thread before dispatching an Android import to a worker.</summary>
        public static long? GetAvailableBytes(string destination)
        {
            string directory = Path.GetFullPath(destination);
            Directory.CreateDirectory(directory);
#if UNITY_ANDROID && !UNITY_EDITOR
            // DriveInfo reaches unsupported IL2CPP internal calls on Android (including IsReady).
            // Query the actual destination filesystem, not the root mount's available space.
            using var stat = new UnityEngine.AndroidJavaObject("android.os.StatFs", directory);
            return stat.Call<long>("getAvailableBytes");
#elif UNITY_EDITOR || (UNITY_STANDALONE && !ENABLE_IL2CPP)
            return new DriveInfo(Path.GetPathRoot(directory)).AvailableFreeSpace;
#else
            // Other players still report write failures; never invoke unsupported DriveInfo icalls.
            return null;
#endif
        }

        public static void Extract(string archivePath, string destination, Action<float> progress = null, long? availableBytes = null)
        {
            using var archive = ZipFile.OpenRead(archivePath);
            Extract(archive, destination, progress, availableBytes);
        }

        internal static void Extract(ZipArchive archive, string destination, Action<float> progress, long? availableBytes)
        {
            string root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(root);
            if (archive.Entries.Count > MAX_FILES) throw new InvalidDataException("曲包文件数量超过限制。");
            long expected = 0;
            foreach (var entry in archive.Entries)
            {
                if (entry.Length > MAX_FILE_BYTES || entry.Length < 0 || entry.Length > MAX_TOTAL_BYTES - expected)
                    throw new InvalidDataException("曲包解压大小超过限制。");
                expected += entry.Length;
            }
            long? freeBytes = availableBytes ?? GetAvailableBytes(root);
            if (freeBytes.HasValue && freeBytes.Value < expected + FREE_SPACE_RESERVE_BYTES)
                throw new IOException("存储空间不足。");
            long total = 0;
            byte[] buffer = new byte[81920];
            foreach (var entry in archive.Entries)
            {
                string name = entry.FullName.Replace('\\', '/');
                if (name.Length > 512 || name.Contains(":") || name.StartsWith("/") ||
                    Array.Exists(name.Split('/'), part => part == "..") ||
                    ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                    throw new InvalidDataException("曲包包含不安全的路径。");
                string path = Path.GetFullPath(Path.Combine(root, name));
                if (!path.StartsWith(root, StringComparison.Ordinal)) throw new InvalidDataException("曲包路径越界。");
                if (name.EndsWith("/")) { Directory.CreateDirectory(path); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using var input = entry.Open();
                using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                long written = 0;
                int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    written += count;
                    total += count;
                    if (written > entry.Length || written > MAX_FILE_BYTES || total > MAX_TOTAL_BYTES)
                        throw new InvalidDataException("曲包实际解压大小超过限制。");
                    output.Write(buffer, 0, count);
                    progress?.Invoke(expected == 0 ? 1f : (float)total / expected);
                }
                if (written != entry.Length) throw new InvalidDataException("曲包文件不完整。");
            }
            progress?.Invoke(1f);
        }
    }
}
