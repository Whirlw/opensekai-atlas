using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using OpenSekai.Shared.Audio;
using OpenSekai.Shared.Packages;
using CriWare;
namespace Sekai.MusicScoreMaker.Common
{
    public sealed class CustomMusicScoreEntry
    {
        public SongPackage Package { get; }
        private readonly PackageAudio _audio;
        public CustomMusicScoreEntry(string rootDirectory, CustomMusicScoreManifest manifest)
        { Package = new SongPackage(rootDirectory,manifest ?? new CustomMusicScoreManifest()); _audio = new PackageAudio(Package); }
        public static CustomMusicScoreEntry FromPackage(SongPackage package) => package == null ? null : new CustomMusicScoreEntry(
            package.RootDirectory, JsonConvert.DeserializeObject<CustomMusicScoreManifest>(JsonConvert.SerializeObject(package.Manifest)));
        public string RootDirectory => Package.RootDirectory;
        public CustomMusicScoreManifest Manifest => (CustomMusicScoreManifest)Package.Manifest;
        public int MusicId => Package.MusicId;
        public string AudioCueName => Package.AudioCueName;
        public long AudioLengthMs => Package.AudioLengthMs;
        public int MusicDurationSec => Package.MusicDurationSec;
        public string ManifestPath => Package.ManifestPath;
        public string ScorePath => Package.ScorePath;
        public string AudioPath => Package.AudioPath;
        public string JacketPath => Package.JacketPath;
        public string VideoPath => Package.VideoPath;
        public UniTask<bool> RegisterAudioAsync(CancellationToken token) => _audio.RegisterAudioAsync(token);
        public float[] GetAudioSamples(int count) => _audio.GetAudioSamples(count);
        public AudioClip GetAudioClip() => _audio.GetAudioClip();
    }
}
