using OpenSekai.Charts;
using OpenSekai.Shared.Packages;
using UnityEngine;
using Sekai;

namespace OpenSekai.App.Playback
{
    public sealed class PlayRequest
    {
        public string PackageDirectory { get; }
        internal TemporarySongPackage TemporaryPackage { get; }
        internal OpenSekai.Shared.Playback.DanLiveSong DanSong { get; }
        internal bool? DanRanked { get; }
        internal SceneManager.Scene SourceScene { get; }
        internal PlayRequest(string directory, OpenSekai.Shared.Playback.DanLiveSong song, SceneManager.Scene source, bool? ranked = null)
            : this(directory, false, SceneManager.Scene.Library)
        {
            DanSong = song;
            DanRanked = ranked;
            SourceScene = source;
        }

        internal OpenSekai.Shared.Playback.RankingSettlement RetryFrom { get; init; }

        public OpenSekai.Shared.Playback.PracticePlan Practice { get; init; }
        internal bool ConstantScroll { get; init; }
        internal bool Death { get; init; }
        public bool IsAuto { get; }
        public SceneManager.Scene ReturnScene { get; }

        public PlayRequest(string packageDirectory, bool isAuto, SceneManager.Scene returnScene)
            : this(packageDirectory, isAuto, returnScene, null)
        {
        }

        internal PlayRequest(string packageDirectory, bool isAuto, SceneManager.Scene returnScene, TemporarySongPackage temporaryPackage)
        {
            PackageDirectory = packageDirectory;
            TemporaryPackage = temporaryPackage;
            IsAuto = isAuto;
            ReturnScene = returnScene;
            SourceScene = returnScene;
        }
    }

    internal sealed class TemporarySongPackage
    {
        internal TemporarySongPackage(SongPackage metadata, NextRushChart chart, string chartHash, AudioClip audio,
            byte[] chartData, byte[] audioData, byte[] coverData)
        {
            Metadata = metadata;
            Chart = chart;
            ChartHash = chartHash;
            Audio = audio;
            ChartData = chartData;
            AudioData = audioData;
            CoverData = coverData;
        }

        internal SongPackage Metadata { get; }
        internal NextRushChart Chart { get; }
        internal string ChartHash { get; }
        internal AudioClip Audio { get; }
        internal byte[] ChartData { get; }
        internal byte[] AudioData { get; }
        internal byte[] CoverData { get; }

        internal long RetainedBytes => (long)ChartData.Length + AudioData.Length + (CoverData?.Length ?? 0)
            + (Audio != null ? (long)Audio.samples * Audio.channels * sizeof(float) : 0L);
        internal float? Loudness { get; set; }
        internal bool LoudnessMeasured { get; set; }
        internal bool Ranked { get; set; }
    }
}
