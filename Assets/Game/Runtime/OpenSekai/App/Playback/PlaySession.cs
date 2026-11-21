using System;
using OpenSekai.Charts;
using OpenSekai.Live.Conversion;
using OpenSekai.Shared.Packages;
using OpenSekai.Shared.Settings;
using Sekai;
using Sekai.Live;

namespace OpenSekai.App.Playback
{
    /// <summary>
    /// Everything one live attempt needs, resolved before the loading screen is pushed: the package,
    /// the converted chart, the settings snapshot and the request that started it. Passing this
    /// instead of four loose arguments keeps the conversion in one place.
    /// </summary>
    public sealed class PlaySession
    {
        private PlaySession(
            PlayRequest request,
            SongPackage package,
            RuntimeChartDefinition chart,
            LiveBundleBuildData bundleConfig,
            LiveSettingData settings, LiveMaterialAudio materialAudio, LiveComboSkin comboSkin, LiveSpriteSkin judgmentSkin, LiveHitEffectSkin hitFxSkin, LiveNoteSkin noteSkin, LiveSpriteSkin laneSkin, LiveBackgroundSkin backgroundSkin, LiveHudSkin hudSkin)
        {
            Request = request;
            Package = package;
            Chart = chart;
            BundleConfig = bundleConfig;
            Settings = settings;
            MaterialAudio = materialAudio;
            ComboSkin = comboSkin;
            JudgmentSkin = judgmentSkin;
            HitFxSkin = hitFxSkin;
            BackgroundSkin = backgroundSkin;
            HudSkin = hudSkin;
            LaneSkin = laneSkin;
            NoteSkin = noteSkin;
        }

        public PlayRequest Request { get; }

        public LiveMaterialAudio MaterialAudio { get; }
        public LiveComboSkin ComboSkin { get; }
        public LiveSpriteSkin JudgmentSkin { get; }
        public LiveBackgroundSkin BackgroundSkin { get; }
        public LiveHudSkin HudSkin { get; }
        public LiveSpriteSkin LaneSkin { get; }
        public LiveNoteSkin NoteSkin { get; }
        public LiveHitEffectSkin HitFxSkin { get; }

        public SongPackage Package { get; }

        public RuntimeChartDefinition Chart { get; }

        /// <summary>The bundle configuration the chart was converted with; kept for diagnostics.</summary>
        public LiveBundleBuildData BundleConfig { get; }

        /// <summary>Playback copy of the live settings, already detached from the store.</summary>
        public LiveSettingData Settings { get; }

        public static PlaySession FromChart(PlayRequest request, SongPackage package, RuntimeChartDefinition chart,
            LiveBundleBuildData bundleConfig, PlaybackSettingsSnapshot settings, LiveMaterialAudio materialAudio = null, LiveComboSkin comboSkin = null, LiveSpriteSkin judgmentSkin = null, LiveHitEffectSkin hitFxSkin = null, LiveNoteSkin noteSkin = null, LiveSpriteSkin laneSkin = null, LiveBackgroundSkin backgroundSkin = null, LiveHudSkin hudSkin = null)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (package == null) throw new ArgumentNullException(nameof(package));
            if (chart == null) throw new ArgumentNullException(nameof(chart));
            if (bundleConfig == null) throw new ArgumentNullException(nameof(bundleConfig));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return new PlaySession(request, package, chart, bundleConfig, settings.ToLegacyData(), materialAudio, comboSkin, judgmentSkin, hitFxSkin, noteSkin, laneSkin, backgroundSkin, hudSkin);
        }

        public static PlaySession Create(
            PlayRequest request,
            SongPackage package,
            ChartDocument document,
            LiveBundleBuildData bundleConfig,
            PlaybackSettingsSnapshot settings)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }
            if (bundleConfig == null)
            {
                throw new ArgumentNullException(nameof(bundleConfig));
            }
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            MusicScore score = RuntimeChartConverter.Convert(document, bundleConfig, settings.ShowSimultaneousLine);
            var chart = RuntimeChartDefinition.Create(document, score);
            return FromChart(request, package, chart, bundleConfig, settings);
        }
    }
}
