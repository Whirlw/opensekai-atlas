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
            LiveSettingData settings)
        {
            Request = request;
            Package = package;
            Chart = chart;
            BundleConfig = bundleConfig;
            Settings = settings;
        }

        public PlayRequest Request { get; }

        public SongPackage Package { get; }

        public RuntimeChartDefinition Chart { get; }

        /// <summary>The bundle configuration the chart was converted with; kept for diagnostics.</summary>
        public LiveBundleBuildData BundleConfig { get; }

        /// <summary>Playback copy of the live settings, already detached from the store.</summary>
        public LiveSettingData Settings { get; }

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
            return new PlaySession(request, package, chart, bundleConfig, settings.ToLegacyData());
        }
    }
}
