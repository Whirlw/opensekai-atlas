using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSekai.App.Playback;
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

        /// <summary>In the library, with the same chart, music and cover the server has now.</summary>
        Installed,

        /// <summary>In the library, but the server's assets have changed since.</summary>
        Outdated
    }

    /// <summary>
    /// Turns a Sonolus level into a native NextRUSH package in the library: downloads the chart
    /// data, music and cover, checks each against its SHA-1 when the server gives one, checks the
    /// chart parses, writes manifest.json and sonolus-source.json into a staging folder and hands
    /// it to <see cref="SongPackageManager.ImportFolder"/>. Nothing reaches the library unless every
    /// step passed. A small index (server + level name → package folder) answers "已下载" and
    /// "可更新" without scanning the library.
    /// </summary>
    public sealed class SonolusLevelInstaller
    {
        /// <summary>Sonolus engine identifier for NextRUSH+.</summary>
        public const string SUPPORTED_ENGINE = "NextRUSH_P";

        public const string SOURCE_FILE_NAME = "sonolus-source.json";
        public const string DATA_FILE_NAME = "level-data.json.gz";
        private const string INDEX_FILE_NAME = "sonolus-downloads.json";
        private const string UPDATE_MARKER_PREFIX = ".sonolus-update-";
        private const int IMPORTER_VERSION = 1;

        private const long MAX_DATA_BYTES = 32L * 1024 * 1024;
        private const long MAX_AUDIO_BYTES = 256L * 1024 * 1024;
        private const long MAX_COVER_BYTES = 16L * 1024 * 1024;

        private static readonly string[] DifficultyNames = { "easy", "normal", "hard", "expert", "master", "append" };
        private static SonolusLevelInstaller _shared;
        private const long TemporaryPackageBudget = 128L * 1024 * 1024;
        private const int TemporaryPackageLimit = 3;
        private static readonly LinkedList<(string Key, SonolusLevel Level, TemporarySongPackage Package)> TemporaryPackages =
            new LinkedList<(string, SonolusLevel, TemporarySongPackage)>();

        private readonly SongPackageManager _packages;
        private readonly string _indexPath;
        private readonly string _stagingRoot;
        private Dictionary<string, IndexEntry> _index;

        public SonolusLevelInstaller(SongPackageManager packages, string indexDirectory, string stagingRoot)
        {
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _indexPath = Path.Combine(indexDirectory, INDEX_FILE_NAME);
            _stagingRoot = stagingRoot;
            TryRecoverPublication();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearTemporaryPackages()
        {
            foreach (var entry in TemporaryPackages)
                if (entry.Package.Audio != null) UnityEngine.Object.Destroy(entry.Package.Audio);
            TemporaryPackages.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RecoverOnStartup() => _ = Shared;

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

            if (level.EngineName != SUPPORTED_ENGINE && level.EngineName != "next-rush")
            {
                reason = "暂不支持 " + (level.EngineTitle ?? level.EngineName ?? "未知") + " 引擎的谱面，仅支持 NextRUSH 和 NextRUSH+。";
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

            bool sameVersion = entry.Version == level.Version;
            bool same = MatchesResource(entry.Data, level.Data, sameVersion)
                && MatchesResource(entry.Bgm, level.Bgm, sameVersion)
                && MatchesResource(entry.Cover, level.Cover, sameVersion);
            return same ? SonolusInstallState.Installed : SonolusInstallState.Outdated;
        }

        private static bool MatchesResource(SonolusSrl installed, SonolusSrl remote, bool sameVersion)
        {
            if (installed == null || remote == null) return installed == remote;
            if (!string.IsNullOrEmpty(installed.Hash) || !string.IsNullOrEmpty(remote.Hash))
                return string.Equals(installed.Hash, remote.Hash, StringComparison.OrdinalIgnoreCase);
            return sameVersion && string.Equals(installed.Url, remote.Url, StringComparison.Ordinal);
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
            SongPackage current = FindCurrentPackage(server, level);
            if (current != null)
            {
                progress?.Invoke(1f, "已在曲库中");
                return current;
            }

            var cached = FindTemporary(server, level);
            if (cached != null)
            {
                SongPackage installed = await SaveDownloadedAsync(server, details, cached.ChartData, cached.AudioData,
                    cached.CoverData, progress, cancellationToken, chartValidated: true);
                ForgetTemporary(cached);
                return installed;
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
            if (AudioExtension(audio) == null) throw new SonolusException("音乐格式无法识别（仅支持 MP3 / OGG / WAV）。");

            byte[] cover = await DownloadCoverAsync(client, level, progress, cancellationToken);
            return await SaveDownloadedAsync(server, details, data, audio, cover, progress, cancellationToken);
        }

        private static async UniTask<byte[]> DownloadCoverAsync(
            SonolusClient client, SonolusLevel level, Action<float, string> progress, CancellationToken token)
        {
            if (level.Cover == null) return null;
            try
            {
                byte[] cover = await client.DownloadAsync(level.Cover, MAX_COVER_BYTES,
                    value => progress?.Invoke(0.85f + 0.1f * Known(value), "正在下载封面…"), token);
                await VerifyAsync(cover, level.Cover.Hash, "封面", token);
                return ImageExtension(cover) != null ? cover : null;
            }
            catch (SonolusException exception)
            {
                Debug.LogWarning("[Sonolus] Cover skipped: " + exception.Message);
                return null;
            }
        }

        private async UniTask<SongPackage> SaveDownloadedAsync(SonolusServerAddress server, SonolusLevelDetails details,
            byte[] data, byte[] audio, byte[] cover, Action<float, string> progress, CancellationToken token,
            bool chartValidated = false)
        {
            string audioExtension = AudioExtension(audio);
            string coverExtension = cover == null ? null : ImageExtension(cover);
            progress?.Invoke(0.95f, chartValidated ? "正在保存已缓存的谱面…" : "正在检查谱面…");
            string manifestJson = BuildManifest(details, audioExtension, coverExtension);
            string sourceJson = BuildSource(server, details);
            return await StageAndPublishAsync(server, details.Level, staging =>
            {
                string chartPath = Path.Combine(staging, DATA_FILE_NAME);
                File.WriteAllBytes(chartPath, data);
                if (!chartValidated) NextRushChartLoader.ReadFile(chartPath);
                File.WriteAllBytes(Path.Combine(staging, "audio" + audioExtension), audio);
                if (coverExtension != null)
                    File.WriteAllBytes(Path.Combine(staging, "jacket" + coverExtension), cover);
                File.WriteAllText(Path.Combine(staging, SongPackageStorage.MANIFEST_FILE_NAME), manifestJson, new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(staging, SOURCE_FILE_NAME), sourceJson, new UTF8Encoding(false));
            }, progress, token);
        }

        private static void ForgetTemporary(TemporarySongPackage package)
        {
            for (var node = TemporaryPackages.First; node != null; node = node.Next)
            {
                if (node.Value.Package != package) continue;
                TemporaryPackages.Remove(node);
                if (package.Audio != null) UnityEngine.Object.Destroy(package.Audio);
                return;
            }
        }

        internal static TemporarySongPackage FindTemporary(SonolusServerAddress server, SonolusLevel level)
        {
            string key = KeyOf(server, level.Name);
            for (var node = TemporaryPackages.Last; node != null; node = node.Previous)
            {
                var cached = node.Value;
                if (cached.Key != key || cached.Package.Audio == null) continue;
                bool sameVersion = cached.Level.Version == level.Version;
                if (!MatchesResource(cached.Level.Data, level.Data, sameVersion)
                    || !MatchesResource(cached.Level.Bgm, level.Bgm, sameVersion)
                    || !MatchesResource(cached.Level.Cover, level.Cover, sameVersion)) continue;

                TemporaryPackages.Remove(node);
                TemporaryPackages.AddLast(node);
                return cached.Package;
            }

            return null;
        }

        internal async UniTask<TemporarySongPackage> LoadTemporaryAsync(
            SonolusClient client, SonolusLevelDetails details, Action<float, string> progress, CancellationToken token)
        {
            SonolusLevel level = details?.Level;
            if (!IsSupported(level, out string reason)) throw new SonolusException(reason);
            token.ThrowIfCancellationRequested();

            var cached = FindTemporary(client.Address, level);
            if (cached != null) return cached;

            progress?.Invoke(0f, "正在读取临时谱面…");
            byte[] data = await client.DownloadAsync(level.Data, MAX_DATA_BYTES,
                value => progress?.Invoke(0.2f * Known(value), "正在读取临时谱面…"), token);
            await VerifyAsync(data, level.Data.Hash, "谱面", token);
            var chart = await UniTask.RunOnThreadPool(() =>
            {
                using var stream = new MemoryStream(data, writable: false);
                return NextRushChartLoader.Read(stream);
            }, cancellationToken: token);

            progress?.Invoke(0.2f, "正在读取临时音乐…");
            var audio = await client.DownloadAudioAsync(level.Bgm, MAX_AUDIO_BYTES,
                value => progress?.Invoke(0.2f + 0.65f * Known(value), "正在读取临时音乐…"), token);
            try
            {
                await VerifyAsync(audio.Data, level.Bgm.Hash, "音乐", token);
                byte[] cover = await DownloadCoverAsync(client, level, progress, token);
                token.ThrowIfCancellationRequested();
                string extension = AudioExtension(audio.Data);
                var metadata = new SongPackage(null, CreateManifest(details, extension, null));
                using var sha = SHA256.Create();
                string chartHash = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
                var package = new TemporarySongPackage(metadata, chart, chartHash, audio.Clip, data, audio.Data, cover);
                RememberTemporary(client.Address, level, package);
                return package;
            }
            catch
            {
                UnityEngine.Object.Destroy(audio.Clip);
                throw;
            }
        }

        internal static void RememberTemporary(SonolusServerAddress server, SonolusLevel level, TemporarySongPackage package)
        {
            TemporaryPackages.AddLast((KeyOf(server, level.Name), level, package));

            long retainedBytes = 0;
            foreach (var entry in TemporaryPackages)
                retainedBytes += entry.Package.RetainedBytes;

            while (TemporaryPackages.Count > TemporaryPackageLimit
                || (TemporaryPackages.Count > 1 && retainedBytes > TemporaryPackageBudget))
            {
                var oldest = TemporaryPackages.First.Value.Package;
                TemporaryPackages.RemoveFirst();
                retainedBytes -= oldest.RetainedBytes;
                if (oldest.Audio != null) UnityEngine.Object.Destroy(oldest.Audio);
            }
        }

        internal async UniTask<SongPackage> StageAndPublishAsync(SonolusServerAddress server, SonolusLevel level,
            Action<string> writePackage, Action<float, string> progress, CancellationToken cancellationToken)
        {
            string staging = Path.Combine(_stagingRoot, Guid.NewGuid().ToString("N"));
            try
            {
                try
                {
                    await UniTask.RunOnThreadPool(() =>
                    {
                        Directory.CreateDirectory(staging);
                        writePackage(staging);
                    }, cancellationToken: cancellationToken);
                }
                catch (Exception exception) when (!(exception is OperationCanceledException) && !(exception is SonolusException))
                {
                    throw new SonolusException("谱面数据无效：" + exception.Message);
                }
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Invoke(0.98f, "正在加入曲库…");
                cancellationToken.ThrowIfCancellationRequested();
                SongPackage installed = FindCurrentPackage(server, level) ?? PublishPackage(staging, server, level, ownsDownloadStage: true);
                progress?.Invoke(1f, "已加入曲库");
                return installed;
            }
            finally
            {
                TryDelete(staging);
            }
        }

        private SongPackage FindCurrentPackage(SonolusServerAddress server, SonolusLevel level)
        {
            return StateOf(server, level) == SonolusInstallState.Installed
                ? SongPackageStorage.LoadEntry(FindInstalled(server, level.Name)) : null;
        }

        internal SongPackage PublishPackage(string staging, SonolusServerAddress server, SonolusLevel level, bool ownsDownloadStage = false)
        {
            SongPackage installed;
            try
            {
                installed = PreparePublish(staging, server, level, ownsDownloadStage);
                CommitPublication(server, level, installed);
            }
            catch (Exception exception)
            {
                TryRecoverPublication();
                throw new SonolusException("无法保存下载记录：" + exception.Message);
            }

            TryRecoverPublication();
            return installed;
        }

        private string PendingPath => _indexPath + ".pending";

        // Phase one publishes the files while the durable index still selects the old edition.
        internal SongPackage PreparePublish(string staging, SonolusServerAddress server, SonolusLevel level, bool ownsDownloadStage = false)
        {
            RecoverPublication();
            if (File.Exists(_indexPath)) _index = ReadIndexFile();
            string previous = FindInstalled(server, level.Name);
            if (previous != null) previous = _packages.RequirePackageDirectory(previous);
            string key = KeyOf(server, level.Name);
            if (previous == null && LoadIndex().ContainsKey(key))
            {
                // A package removed from the library has no recoverable old edition.
                var remaining = new Dictionary<string, IndexEntry>(_index, StringComparer.Ordinal);
                remaining.Remove(key);
                SaveIndex(remaining);
                _index = remaining;
            }
            SongPackage planned = _packages.PlanFolderImport(staging) ?? throw new SonolusException("无法加入曲库。");
            Guid operation = Guid.NewGuid();
            var pending = new Publication
            {
                Id = operation.ToString("N"),
                Key = key,
                Previous = previous == null ? null : Path.GetFileName(previous),
                Incoming = Path.GetFileName(planned.RootDirectory)
            };
            if (ownsDownloadStage) pending.DownloadStage = Path.GetFileName(staging);
            AtomicFile.WriteAllText(PendingPath, JsonConvert.SerializeObject(pending));
            if (previous != null) Stamp(previous, pending.Id);
            return _packages.PublishFolder(staging, planned, operation, copy => Stamp(copy, pending.Id));
        }

        private static void Stamp(string directory, string id)
        {
            string marker = Path.Combine(directory, UPDATE_MARKER_PREFIX + id);
            SongPackageStorage.RejectReparsePoint(marker);
            AtomicFile.WriteAllText(marker, id);
        }

        private static bool HasStamp(string directory, string id)
        {
            if (directory == null) return false;
            string marker = Path.Combine(directory, UPDATE_MARKER_PREFIX + id);
            SongPackageStorage.RejectReparsePoint(marker);
            return File.Exists(marker) && File.ReadAllText(marker) == id;
        }

        private void TryRecoverPublication()
        {
            try
            {
                RecoverPublication();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Sonolus] Update cleanup will be retried: " + exception.Message);
            }
        }

        private string PackagePath(string name)
        {
            SongPackageStorage.ValidatePathComponent(name, nameof(name));
            return _packages.RequirePackageDirectory(Path.Combine(_packages.RootDirectory, name));
        }

        private void RecoverPublication()
        {
            if (!File.Exists(PendingPath)) return;
            var pending = JsonConvert.DeserializeObject<Publication>(File.ReadAllText(PendingPath));
            if (pending == null || !Guid.TryParseExact(pending.Id, "N", out Guid operation) || string.IsNullOrEmpty(pending.Key))
                throw new InvalidDataException("更新记录无效。");
            string incoming = PackagePath(pending.Incoming);
            string previous = pending.Previous == null ? null : PackagePath(pending.Previous);
            if (previous != null && _packages.PathsEqual(previous, incoming))
                throw new InvalidDataException("更新记录中的曲包目录重复。");

            // Read strictly from disk. A corrupt or unavailable index cannot authorize deletion.
            var index = ReadIndexFile();
            string active = index.TryGetValue(pending.Key, out var selected) ? selected.Directory : null;
            bool committed = active != null && _packages.PathsEqual(active, incoming);
            bool previousSelected = active == null ? previous == null : previous != null && _packages.PathsEqual(active, previous);
            if (!committed && !previousSelected)
                throw new InvalidDataException("下载索引与更新记录不一致。");
            string keep = committed ? incoming : previous;
            string discard = committed ? previous : incoming;
            if (keep != null && SongPackageStorage.LoadEntry(keep) == null)
                throw new InvalidDataException("索引指向的曲包无法读取。");

            string retired = _packages.ImportStagingPath(operation);
            if (discard != null && HasStamp(discard, pending.Id))
            {
                foreach (var record in index.Values)
                {
                    if (_packages.PathsEqual(record.Directory, discard))
                        throw new InvalidDataException("待清理曲包仍被下载索引引用。");
                }
                // Rename first so interruption during recursive cleanup never loses ownership evidence.
                Directory.Move(discard, retired);
            }
            SongPackageManager.DeleteOwnedTree(retired);
            if (discard != null) _packages.RefreshCachedDirectory(discard);
            if (keep != null) _packages.RefreshCachedDirectory(keep);
            if (HasStamp(keep, pending.Id)) File.Delete(Path.Combine(keep, UPDATE_MARKER_PREFIX + pending.Id));
            if (pending.DownloadStage != null)
            {
                if (!Guid.TryParseExact(pending.DownloadStage, "N", out _)) throw new InvalidDataException("无效的暂存目录。");
                SongPackageStorage.RejectReparsePoint(_stagingRoot);
                SongPackageManager.DeleteOwnedTree(Path.Combine(_stagingRoot, pending.DownloadStage));
            }
            File.Delete(PendingPath);
            _index = index;
        }

        private sealed class Publication
        {
            public string Id, Key, Previous, Incoming, DownloadStage;
        }

        /// <summary>The manifest of the package, mapped from the level's fields.</summary>
        internal static string BuildManifest(SonolusLevelDetails details, string audioExtension, string coverExtension) =>
            JsonConvert.SerializeObject(CreateManifest(details, audioExtension, coverExtension), Formatting.Indented);

        private static SongPackageManifest CreateManifest(SonolusLevelDetails details, string audioExtension, string coverExtension)
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

            return manifest;
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

            // The package may have been deleted from the library. The next successful install replaces this entry.
            if (!File.Exists(Path.Combine(entry.Directory, SongPackageStorage.MANIFEST_FILE_NAME)))
            {
                entry = null;
                return false;
            }

            return true;
        }

        internal void CommitPublication(SonolusServerAddress server, SonolusLevel level, SongPackage installed)
        {
            var updated = new Dictionary<string, IndexEntry>(LoadIndex(), StringComparer.Ordinal)
            {
                [KeyOf(server, level.Name)] = new IndexEntry
                {
                    Directory = installed.RootDirectory,
                    Data = level.Data,
                    Bgm = level.Bgm,
                    Cover = File.Exists(installed.JacketPath) ? level.Cover : null,
                    Version = level.Version
                }
            };
            SaveIndex(updated);
            _index = updated;
        }

        private Dictionary<string, IndexEntry> LoadIndex()
        {
            if (_index != null)
            {
                return _index;
            }

            try
            {
                _index = ReadIndexFile();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Sonolus] Download index unreadable: " + exception.Message);
                return new Dictionary<string, IndexEntry>(StringComparer.Ordinal);
            }

            return _index;
        }

        private Dictionary<string, IndexEntry> ReadIndexFile()
        {
            var entries = new Dictionary<string, IndexEntry>(StringComparer.Ordinal);
            string json;
            try { json = File.ReadAllText(_indexPath); }
            catch (FileNotFoundException) { return entries; }
            catch (DirectoryNotFoundException) { return entries; }
            foreach (var pair in JObject.Parse(json).Properties())
            {
                if (pair.Value is not JObject value || string.IsNullOrEmpty(Json.String(value, "directory")))
                    throw new InvalidDataException("下载索引包含无效的曲包记录。");
                entries[pair.Name] = new IndexEntry
                {
                    Directory = Json.String(value, "directory"),
                    Data = SonolusSrl.From(value["data"]),
                    Bgm = SonolusSrl.From(value["bgm"]),
                    Cover = SonolusSrl.From(value["cover"]),
                    Version = (int)Json.Number(value, "version")
                };
            }
            return entries;
        }

        private void SaveIndex(Dictionary<string, IndexEntry> index)
        {
            var json = new JObject();
            foreach (KeyValuePair<string, IndexEntry> pair in index)
            {
                json[pair.Key] = new JObject
                {
                    ["directory"] = pair.Value.Directory,
                    ["data"] = ResourceJson(pair.Value.Data),
                    ["bgm"] = ResourceJson(pair.Value.Bgm),
                    ["cover"] = ResourceJson(pair.Value.Cover),
                    ["version"] = pair.Value.Version
                };
            }
            AtomicFile.WriteAllText(_indexPath, json.ToString(Formatting.Indented));
        }

        private static JObject ResourceJson(SonolusSrl resource) => resource == null ? null
            : new JObject { ["hash"] = resource.Hash, ["url"] = resource.Url };

        private sealed class IndexEntry
        {
            internal string Directory;
            internal SonolusSrl Data;
            internal SonolusSrl Bgm;
            internal SonolusSrl Cover;
            internal int Version;
        }
    }
}
