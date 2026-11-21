using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using OpenSekai.MaterialPacks;

namespace OpenSekai.Infrastructure.Packages
{
    internal static class MaterialPackArchive
    {
        // Provisional limits for the audio milestone. Decoded, combined selection is bounded separately.
        internal const int MAX_FILE_BYTES = 8 * 1024 * 1024;
        private const int MAX_ARCHIVE_BYTES = 32 * 1024 * 1024;
        private static readonly UTF8Encoding _utf8 = new(false, true);

        internal static InstalledMaterialPack Unpack(string zipPath, string stage, long? available, CancellationToken token)
        {
            using var file = File.OpenRead(zipPath);
            if (file.Length > MAX_ARCHIVE_BYTES + 1024 * 1024) throw new InvalidDataException("资源包 ZIP 过大。");
            using var zip = new ZipArchive(file, ZipArchiveMode.Read);
            if (zip.Entries.Count > 256) throw new InvalidDataException("资源包最多支持 256 项。");
            var files = new SortedSet<string>(StringComparer.Ordinal);
            var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var entry in zip.Entries)
            {
                token.ThrowIfCancellationRequested();
                string name = entry.FullName;
                bool directory = name.EndsWith("/", StringComparison.Ordinal);
                if (directory) name = name.Substring(0, name.Length - 1);
                MaterialPackManifest.CheckPath(name);
                if (!entries.Add(name)) throw new InvalidDataException("重复 ZIP 路径 " + name);
                string prefix = "";
                foreach (string part in name.Split('/'))
                {
                    prefix = prefix.Length == 0 ? part : prefix + "/" + part;
                    if (paths.TryGetValue(prefix, out string previous) && prefix != previous)
                        throw new InvalidDataException("ZIP 路径大小写冲突 " + prefix);
                    paths[prefix] = prefix;
                }
                total += entry.Length;
                if (entry.Length > MAX_FILE_BYTES || total > MAX_ARCHIVE_BYTES || directory && entry.Length != 0
                    || name == "pack.json" && entry.Length > 256 * 1024)
                    throw new InvalidDataException("资源包文件超过大小限制 " + name);
                if (!directory) files.Add(name);
            }
            if (!files.Contains("pack.json")) throw new InvalidDataException("包根缺少 pack.json。");
            SafeZipExtractor.Extract(zip, stage, _ => token.ThrowIfCancellationRequested(), available);
            using var digestBytes = new MemoryStream();
            using var digest = new BinaryWriter(digestBytes, _utf8, true);
            var media = new Dictionary<string, (string hash, MaterialPackWave wave)>(StringComparer.Ordinal);
            foreach (string name in files)
            {
                token.ThrowIfCancellationRequested();
                byte[] bytes = File.ReadAllBytes(Path.Combine(stage, name));
                byte[] hash = Hash(bytes);
                byte[] pathBytes = _utf8.GetBytes(name);
                digest.Write(pathBytes.Length);
                digest.Write(pathBytes);
                digest.Write(hash.Length);
                digest.Write(hash);
                if (name == "pack.json") continue;
                if (!name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("当前音效包仅支持 WAV：" + name);
                try { media.Add(name, (Hex(hash), MaterialPackWave.Read(bytes))); }
                catch (InvalidDataException e) { throw new InvalidDataException(name + "：" + e.Message, e); }
            }
            string json = _utf8.GetString(File.ReadAllBytes(Path.Combine(stage, "pack.json"))).TrimStart('\uFEFF');
            return MaterialPackManifest.Parse(json, Hex(Hash(digestBytes.ToArray())), media);
        }

        internal static byte[] Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(bytes);
        }

        internal static string Hex(byte[] hash) => BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

    }
}
