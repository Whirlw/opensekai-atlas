using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using OpenSekai.Infrastructure.Charts;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Infrastructure.Sonolus;
using OpenSekai.Shared.Audio;
using OpenSekai.Shared.Packages;
using Sekai;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.App.Playback
{
    internal sealed class PreparedDanSong
    {
        public string Directory { get; }
        public string Title { get; }
        public string ScoreKey { get; }

        internal PreparedDanSong(string directory, string title, string scoreKey)
            => (Directory, Title, ScoreKey) = (directory, title, scoreKey);
    }

    // Pin files, not decoded music clips. Only one song needs to occupy audio memory at a time.
    internal sealed class DanSongPreparation : IDisposable
    {
        private readonly string _directory;
        private readonly Dictionary<string, PreparedDanSong> _prepared = new();
        private readonly ChartConversionSettings _config;

        private readonly SonolusLevelInstaller _installer;

        internal DanSongPreparation(SonolusLevelInstaller installer, string root)
        {
            var config = ResourceCatalog.LiveBundleConfig.Load();
            if (config == null) throw new InvalidOperationException("打歌配置尚未就绪。");
            _config = new ChartConversionSettings(config);
            _installer = installer;
            Directory.CreateDirectory(root);
            SongPackageStorage.RejectReparsePoint(root);
            // The service owns at most one preparation. Reclaim copies left by a killed process.
            foreach (string previous in Directory.GetDirectories(root))
                if (Guid.TryParseExact(Path.GetFileName(previous), "N", out _)) SongPackageManager.DeleteOwnedTree(previous);
            _directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        }

        internal async UniTask<PreparedDanSong> PrepareAsync(string link, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_prepared.TryGetValue(link, out var previous)) return previous;
            if (!SonolusServerAddress.TryParse(link, out var address, out var level, out var error))
                throw new InvalidDataException(error);
            string local = _installer.FindPlayableInstalled(address, level)?.RootDirectory;
            if (local == null)
            {
                var client = new SonolusClient(address);
                var details = await client.GetLevelAsync(level, token);
                var installed = await _installer.InstallAsync(client, details, null, token);
                local = installed.RootDirectory;
            }

            string pinned = Path.Combine(_directory, _prepared.Count.ToString(CultureInfo.InvariantCulture));
            var (copy, hash) = await Task.Run(() => Pin(local, pinned, token), token);
            var clip = await PackageAudio.LoadClipAsync(copy, token);
            if (clip == null) throw new InvalidDataException("曲目音频无法解码。");
            UnityEngine.Object.Destroy(clip);
            string title = copy.Manifest.scoreTitle;
            var song = new PreparedDanSong(pinned, string.IsNullOrWhiteSpace(title) ? level : title, hash);
            _prepared.Add(link, song);
            return song;
        }

        private (SongPackage, string) Pin(string source, string pinned, CancellationToken token)
        {
            var package = SongPackageStorage.LoadEntry(source) ?? throw new InvalidDataException("曲包信息无效。");
            Directory.CreateDirectory(pinned);
            var copy = new SongPackage(pinned, package.Manifest);
            File.Copy(package.ManifestPath, copy.ManifestPath);
            File.Copy(package.ScorePath, copy.ScorePath);
            File.Copy(package.AudioPath, copy.AudioPath);
            if (File.Exists(package.JacketPath)) File.Copy(package.JacketPath, copy.JacketPath);
            if (File.Exists(package.VideoPath)) File.Copy(package.VideoPath, copy.VideoPath);
            token.ThrowIfCancellationRequested();
            if (!RuntimeChartLoader.IsNextRushChart(copy) || !RuntimeChartLoader.Load(copy, _config, true, false).HasPlayableNotes)
                throw new InvalidDataException("该谱面无法手动游玩。");
            string hash = SongPackageHash.Compute(copy.ScorePath);
            if (string.IsNullOrEmpty(hash)) throw new IOException("无法确认谱面版本。");
            return (copy, hash);
        }

        public void Dispose()
        {
            try { SongPackageManager.DeleteOwnedTree(_directory); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { Debug.LogWarning("[Dan] 临时曲包清理失败：" + e.Message); }
        }
    }
}
