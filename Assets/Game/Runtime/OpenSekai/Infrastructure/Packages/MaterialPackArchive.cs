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
        // Archive limits apply before decoding. Each selected category also has a decoded budget.
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
            return Inspect(stage, files, token);
        }

        /// <summary>
        /// Re-reads an installed revision's own files and manifest, for an index written before
        /// the manifest's extensions were read. Null when the files no longer match the digest.
        /// </summary>
        internal static InstalledMaterialPack Reinspect(string versionRoot, string expectedDigest, CancellationToken token)
        {
            var root = Path.GetFullPath(versionRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var files = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                string full = Path.GetFullPath(path);
                if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return null;
                if (new FileInfo(full).Length > MAX_FILE_BYTES) return null;
                files.Add(full.Substring(root.Length + 1).Replace('\\', '/'));
            }
            if (!files.Contains("pack.json")) return null;
            var pack = Inspect(root, files, token);
            return pack.Digest == expectedDigest ? pack : null;
        }

        /// <summary>Reads a file of an installed revision and checks it still has the hash the index recorded.</summary>
        internal static byte[] ReadVerified(string path, string expectedHash)
        {
            using var stream = File.OpenRead(path);
            if (stream.Length > MAX_FILE_BYTES) throw new InvalidDataException("文件大小已改变。");
            using var reader = new BinaryReader(stream);
            var bytes = reader.ReadBytes((int)stream.Length);
            if (Hex(Hash(bytes)) != expectedHash) throw new InvalidDataException("文件内容与安装修订不符。");
            return bytes;
        }

        private static InstalledMaterialPack Inspect(string stage, SortedSet<string> files, CancellationToken token)
        {
            using var digestBytes = new MemoryStream();
            using var digest = new BinaryWriter(digestBytes, _utf8, true);
            var media = new Dictionary<string, (string hash, MaterialPackWave wave)>(StringComparer.Ordinal);
            var images = new Dictionary<string, (string hash, int width, int height)>(StringComparer.Ordinal);
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
                if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    var (width, height) = MaterialPackTexture.Dimensions(bytes, name);
                    images.Add(name, (Hex(hash), width, height));
                    continue;
                }
                if (!name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("需要 WAV 或 PNG：" + name);
                try { media.Add(name, (Hex(hash), MaterialPackWave.Read(bytes))); }
                catch (InvalidDataException e) { throw new InvalidDataException(name + "：" + e.Message, e); }
            }
            string json = _utf8.GetString(File.ReadAllBytes(Path.Combine(stage, "pack.json"))).TrimStart('\uFEFF');
            return MaterialPackManifest.Parse(json, Hex(Hash(digestBytes.ToArray())), media, images);
        }

        internal static byte[] Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(bytes);
        }

        internal static string Hex(byte[] hash) => BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

    }
}
