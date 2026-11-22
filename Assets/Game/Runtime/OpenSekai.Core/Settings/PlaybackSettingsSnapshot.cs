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

        /// <summary>
        /// This snapshot with the built-in note skin, hit effect and hit sound replaced by the
        /// resource pack page's choice (0-based, as the live reads them). The store's copy of the
        /// settings is left alone.
        /// </summary>
        public PlaybackSettingsSnapshot WithBuiltinSets(int noteSkinIndex, int noteEffectIndex, int noteSeIndex)
        {
            var copy = new PlaybackSettingsSnapshot(_data);
            copy._data.NoteSkinIndex = noteSkinIndex;
            copy._data.NoteEffect = noteEffectIndex;
            copy._data.NoteSeIndex = noteSeIndex;
            return copy;
        }
    }
}
