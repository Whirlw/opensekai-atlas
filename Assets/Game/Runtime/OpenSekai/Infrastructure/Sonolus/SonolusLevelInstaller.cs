using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSekai.Infrastructure.Charts;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Packages;
using UnityEngine;

namespace OpenSekai.Infrastructure.Sonolus
{
    /// <summary>Where a level stands against the local library.</summary>
    public enum SonolusInstallState
    {
        /// <summary>Not in the library.</summary>
        Missing,

        /// <summary>In the library, with the same chart data the server has now.</summary>
        Installed,

        /// <summary>In the library, but the server's chart data has changed since.</summary>
        Outdated
    }

    /// <summary>
    /// Turns a Sonolus level into a native NextRUSH+ package in the library: downloads the chart
    /// data, music and cover, checks each against its SHA-1 when the server gives one, checks the
    /// chart parses, writes manifest.json and sonolus-source.json into a staging folder and hands
    /// it to <see cref="SongPackageManager.ImportFolder"/>. Nothing reaches the library unless every
    /// step passed. A small index (server + level name → package folder) answers "已下载" and
    /// "可更新" without scanning the library.
    /// </summary>
    public sealed class SonolusLevelInstaller
    {
        /// <summary>The one engine whose charts the game can load natively.</summary>
        public const string SUPPORTED_ENGINE = "NextRUSH_P";

        public const string SOURCE_FILE_NAME = "sonolus-source.json";
        public const string DATA_FILE_NAME = "level-data.json.gz";
        private const string INDEX_FILE_NAME = "sonolus-downloads.json";
        private const int IMPORTER_VERSION = 1;

        private const long MAX_DATA_BYTES = 32L * 1024 * 1024;
        private const long MAX_AUDIO_BYTES = 256L * 1024 * 1024;
        private const long MAX_COVER_BYTES = 16L * 1024 * 1024;

        private static readonly string[] DifficultyNames = { "easy", "normal", "hard", "expert", "master", "append" };
        private static SonolusLevelInstaller _shared;

        private readonly SongPackageManager _packages;
        private readonly string _indexPath;
        private readonly string _stagingRoot;
        private Dictionary<string, IndexEntry> _index;

        public SonolusLevelInstaller(SongPackageManager packages, string indexDirectory, string stagingRoot)
        {
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _indexPath = Path.Combine(indexDirectory, INDEX_FILE_NAME);
            _stagingRoot = stagingRoot;
        }

        public static SonolusLevelInstaller Shared => _shared ?? (_shared = new SonolusLevelInstaller(
            SongPackageManager.Shared, Application.persistentDataPath, Path.Combine(Application.temporaryCachePath, "SonolusStaging")));

        /// <summary>Whether the game can load this level's chart; false explains why in <paramref name="reason"/>.</summary>
        public static bool IsSupported(SonolusLevel level, out string reason)
        {
            reason = null;
            if (level == null)
            {
                reason = "谱面信息缺失。";
                return false;
            }

            if (!string.Equals(level.EngineName, SUPPORTED_ENGINE, StringComparison.Ordinal))
            {
                reason = "暂不支持 " + (level.EngineTitle ?? level.EngineName ?? "未知") + " 引擎的谱面，仅支持 NextRUSH+。";
                return false;
            }

            if (level.Data == null || level.Bgm == null)
            {
                reason = "服务器没有提供谱面数据或音乐。";
                return false;
            }

            return true;
        }

        /// <summary>The library folder holding this level, or null when it is not downloaded (or was deleted since).</summary>
        public string FindInstalled(SonolusServerAddress server, string levelName)
        {
            return TryGetEntry(server, levelName, out IndexEntry entry) ? entry.Directory : null;
        }

        public SonolusInstallState StateOf(SonolusServerAddress server, SonolusLevel level)
        {
            if (level == null || !TryGetEntry(server, level.Name, out IndexEntry entry))
            {
                return SonolusInstallState.Missing;
            }

            string hash = level.Data?.Hash;
            bool same = string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(entry.DataHash)
                ? entry.Version == level.Version
                : string.Equals(hash, entry.DataHash, StringComparison.OrdinalIgnoreCase);
            return same ? SonolusInstallState.Installed : SonolusInstallState.Outdated;
        }

        /// <summary>
        /// Downloads the level and adds it to the library. An outdated copy is replaced once the
        /// new one is in; an up-to-date copy is returned as is.
        /// </summary>
        /// <param name="progress">Overall progress 0–1 and what is happening, for the UI.</param>
        /// <returns>The package now in the library.</returns>
        public async UniTask<SongPackage> InstallAsync(
            SonolusClient client, SonolusLevelDetails details, Action<float, string> progress, CancellationToken cancellationToken)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            SonolusLevel level = details?.Level ?? throw new ArgumentNullException(nameof(details));
            if (!IsSupported(level, out string reason))
            {
                throw new SonolusException(reason);
            }

            SonolusServerAddress server = client.Address;
            string existing = FindInstalled(server, level.Name);
            if (existing != null && StateOf(server, level) == SonolusInstallState.Installed)
            {
                SongPackage current = SongPackageStorage.LoadEntry(existing);
                if (current != null)
                {
                    progress?.Invoke(1f, "已在曲库中");
                    return current;
                }
            }

            // Weights roughly follow typical sizes: the music dominates.
            progress?.Invoke(0f, "正在下载谱面…");
            byte[] data = await client.DownloadAsync(level.Data, MAX_DATA_BYTES,
                value => progress?.Invoke(0.1f * Known(value), "正在下载谱面…"), cancellationToken);
            await VerifyAsync(data, level.Data.Hash, "谱面", cancellationToken);

            progress?.Invoke(0.1f, "正在下载音乐…");
            byte[] audio = await client.DownloadAsync(level.Bgm, MAX_AUDIO_BYTES,
                value => progress?.Invoke(0.1f + 0.75f * Known(value), "正在下载音乐…"), cancellationToken);
            await VerifyAsync(audio, level.Bgm.Hash, "音乐", cancellationToken);
            string audioExtension = AudioExtension(audio) ?? throw new SonolusException("音乐格式无法识别（仅支持 MP3 / OGG / WAV）。");

            byte[] cover = null;
            string coverExtension = null;
            if (level.Cover != null)
            {
                progress?.Invoke(0.85f, "正在下载封面…");
                try
                {
                    cover = await client.DownloadAsync(level.Cover, MAX_COVER_BYTES,
                        value => progress?.Invoke(0.85f + 0.1f * Known(value), "正在下载封面…"), cancellationToken);
                    await VerifyAsync(cover, level.Cover.Hash, "封面", cancellationToken);
                    coverExtension = ImageExtension(cover);
                }
                catch (SonolusException exception)
                {
                    // A chart without its cover still plays; the library shows a placeholder.
                    Debug.LogWarning("[Sonolus] Cover skipped: " + exception.Message);
                    cover = null;
                }

                if (coverExtension == null)
                {
                    cover = null;
                }
            }

            progress?.Invoke(0.95f, "正在检查谱面…");
            string staging = Path.Combine(_stagingRoot, Guid.NewGuid().ToString("N"));
            try
            {
                string manifestJson = BuildManifest(details, audioExtension, coverExtension);
                string sourceJson = BuildSource(server, details);
                await UniTask.RunOnThreadPool(() =>
                {
                    Directory.CreateDirectory(staging);
                    File.WriteAllBytes(Path.Combine(staging, DATA_FILE_NAME), data);
                    // Parse once before anything reaches the library: a corrupt file fails here.
                    NextRushChartLoader.ReadFile(Path.Combine(staging, DATA_FILE_NAME));
                    File.WriteAllBytes(Path.Combine(staging, "audio" + audioExtension), audio);
                    if (cover != null)
                    {
                        File.WriteAllBytes(Path.Combine(staging, "jacket" + coverExtension), cover);
                    }

                    File.WriteAllText(Path.Combine(staging, SongPackageStorage.MANIFEST_FILE_NAME), manifestJson, new UTF8Encoding(false));
                    File.WriteAllText(Path.Combine(staging, SOURCE_FILE_NAME), sourceJson, new UTF8Encoding(false));
                }, cancellationToken: cancellationToken);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException) && !(exception is SonolusException))
            {
                TryDelete(staging);
                throw new SonolusException("谱面数据无效：" + exception.Message);
            }

            SongPackage installed;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Invoke(0.98f, "正在加入曲库…");
                installed = _packages.ImportFolder(staging) ?? throw new SonolusException("无法加入曲库。");
            }
            finally
            {
                TryDelete(staging);
            }

            // The new copy is in; the old one (an update) goes.
            if (existing != null && !string.Equals(Path.GetFullPath(existing), Path.GetFullPath(installed.RootDirectory), StringComparison.OrdinalIgnoreCase))
            {
                SongPackage old = SongPackageStorage.LoadEntry(existing);
                try
                {
                    if (old != null)
                    {
                        _packages.DeleteEntry(old);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[Sonolus] Could not remove the replaced package: " + exception.Message);
                }
            }

            Remember(server, level, installed.RootDirectory);
            progress?.Invoke(1f, "已加入曲库");
            return installed;
        }

        /// <summary>The manifest of the package, mapped from the level's fields.</summary>
        internal static string BuildManifest(SonolusLevelDetails details, string audioExtension, string coverExtension)
        {
            SonolusLevel level = details.Level;
            var manifest = new SongPackageManifest
            {
                formatVersion = 1,
                id = SongPackageStorage.GenerateShortId(),
                chartFormat = NextRushChartLoader.Format,
                title = level.Title,
                scoreTitle = level.Title,
                composer = level.Artists,
                userName = level.Author,
                description = details.Description ?? string.Empty,
                playLevel = (int)Math.Round(level.Rating),
                musicDifficultyType = DifficultyOf(level),
                audioFileName = "audio" + audioExtension,
                jacketFileName = coverExtension != null ? "jacket" + coverExtension : string.Empty,
                scoreFileName = DATA_FILE_NAME,
                videoFileName = string.Empty,
                fillerSec = 0f,
                secForMusicScoreMaker = 0
            };
            manifest.Normalize();
            if (coverExtension == null)
            {
                manifest.jacketFileName = string.Empty;
            }

            return JsonConvert.SerializeObject(manifest, Formatting.Indented);
        }

        /// <summary>
        /// The difficulty class a tag names (UntitledCharts tags levels "Expert", "Master"...);
        /// master when no tag names one, since the protocol has no such field.
        /// </summary>
        internal static string DifficultyOf(SonolusLevel level)
        {
            if (level?.Tags != null)
            {
                foreach (SonolusTag tag in level.Tags)
                {
                    string title = tag.Title?.Trim();
                    foreach (string name in DifficultyNames)
                    {
                        if (string.Equals(title, name, StringComparison.OrdinalIgnoreCase))
                        {
                            return name;
                        }
                    }
                }
            }

            return "master";
        }

        private static string BuildSource(SonolusServerAddress server, SonolusLevelDetails details)
        {
            SonolusLevel level = details.Level;
            var source = new JObject
            {
                ["server"] = server.BaseUrl,
                ["name"] = level.Name,
                ["version"] = level.Version,
                ["engine"] = level.EngineName,
                ["engineVersion"] = level.EngineVersion,
                ["dataHash"] = level.Data?.Hash,
                ["bgmHash"] = level.Bgm?.Hash,
                ["coverHash"] = level.Cover?.Hash,
                ["importer"] = "OpenSekaiAtlas/" + IMPORTER_VERSION,
                ["downloadedAtUtc"] = DateTimeOffset.UtcNow.ToString("o"),
                ["details"] = details.Raw
            };
            return source.ToString(Formatting.Indented);
        }

        internal static string AudioExtension(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 12) return null;
            if (bytes[0] == (byte)'I' && bytes[1] == (byte)'D' && bytes[2] == (byte)'3') return ".mp3";
            if (bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0) return ".mp3";
            if (bytes[0] == (byte)'O' && bytes[1] == (byte)'g' && bytes[2] == (byte)'g' && bytes[3] == (byte)'S') return ".ogg";
            if (bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
                && bytes[8] == (byte)'W' && bytes[9] == (byte)'A' && bytes[10] == (byte)'V' && bytes[11] == (byte)'E') return ".wav";
            return null;
        }

        internal static string ImageExtension(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 8) return null;
            if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return ".png";
            if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return ".jpg";
            return null;
        }

        internal static string Sha1Hex(byte[] bytes)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    text.Append(b.ToString("x2"));
                }

                return text.ToString();
            }
        }

        private static async UniTask VerifyAsync(byte[] bytes, string expected, string what, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(expected))
            {
                return;
            }

            string actual = await UniTask.RunOnThreadPool(() => Sha1Hex(bytes), cancellationToken: cancellationToken);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new SonolusException(what + "校验失败（SHA-1 不一致），请重试。");
            }
        }

        private static float Known(float value) => value < 0f ? 0f : value;

        private static void TryDelete(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Sonolus] Could not clean up " + directory + ": " + exception.Message);
            }
        }

        // ---------------------------------------------------------------- index

        private static string KeyOf(SonolusServerAddress server, string levelName) => server.Key + "|" + levelName;

        private bool TryGetEntry(SonolusServerAddress server, string levelName, out IndexEntry entry)
        {
            entry = null;
            if (server == null || string.IsNullOrEmpty(levelName))
            {
                return false;
            }

            Dictionary<string, IndexEntry> index = LoadIndex();
            if (!index.TryGetValue(KeyOf(server, levelName), out entry) || entry == null || string.IsNullOrEmpty(entry.Directory))
            {
                return false;
            }

            // Deleted from the library since: forget it.
            if (!File.Exists(Path.Combine(entry.Directory, SongPackageStorage.MANIFEST_FILE_NAME)))
            {
                index.Remove(KeyOf(server, levelName));
                SaveIndex();
                entry = null;
                return false;
            }

            return true;
        }

        private void Remember(SonolusServerAddress server, SonolusLevel level, string directory)
        {
            LoadIndex()[KeyOf(server, level.Name)] = new IndexEntry
            {
                Directory = directory,
                DataHash = level.Data?.Hash,
                Version = level.Version,
                Title = level.Title
            };
            SaveIndex();
        }

        private Dictionary<string, IndexEntry> LoadIndex()
        {
            if (_index != null)
            {
                return _index;
            }

            _index = new Dictionary<string, IndexEntry>(StringComparer.Ordinal);
            try
            {
                if (File.Exists(_indexPath) && JToken.Parse(File.ReadAllText(_indexPath)) is JObject loaded)
                {
                    foreach (KeyValuePair<string, JToken> pair in loaded)
                    {
                        if (pair.Value is JObject value)
                        {
                            _index[pair.Key] = new IndexEntry
                            {
                                Directory = Json.String(value, "directory"),
                                DataHash = Json.String(value, "dataHash"),
                                Version = (int)Json.Number(value, "version"),
                                Title = Json.String(value, "title")
                            };
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Sonolus] Download index unreadable, starting over: " + exception.Message);
            }

            return _index;
        }

        private void SaveIndex()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_indexPath));
                string temporary = _indexPath + ".tmp";
                var json = new JObject();
                foreach (KeyValuePair<string, IndexEntry> pair in _index)
                {
                    json[pair.Key] = new JObject
                    {
                        ["directory"] = pair.Value.Directory,
                        ["dataHash"] = pair.Value.DataHash,
                        ["version"] = pair.Value.Version,
                        ["title"] = pair.Value.Title
                    };
                }

                File.WriteAllText(temporary, json.ToString(Formatting.Indented), new UTF8Encoding(false));
                if (File.Exists(_indexPath))
                {
                    File.Delete(_indexPath);
                }

                File.Move(temporary, _indexPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Sonolus] Could not save the download index: " + exception.Message);
            }
        }

        private sealed class IndexEntry
        {
            internal string Directory;
            internal string DataHash;
            internal int Version;
            internal string Title;
        }
    }
}
