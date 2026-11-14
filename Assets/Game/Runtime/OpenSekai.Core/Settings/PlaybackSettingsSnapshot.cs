using Sekai;

namespace OpenSekai.Shared.Settings
{
    /// <summary>Private copy of the current preferences, independent of later UI changes.</summary>
    public sealed class PlaybackSettingsSnapshot
    {
        private readonly LiveSettingData _data;
        public bool IsMirror => _data.IsMirror;
        public bool ShowSimultaneousLine => _data.UseSimultaneousPushingLine;
        public int FrameRateLimit => _data.FrameRateLimit;

        internal PlaybackSettingsSnapshot(LiveSettingData source) { _data = source.CopyForPlayback(); }
        public LiveSettingData ToLegacyData() => _data.CopyForPlayback();
    }
}
