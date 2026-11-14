using System;
using System.IO;
using Newtonsoft.Json;
using OpenSekai.Shared.Settings;
using Sekai;
using UnityEngine;

namespace OpenSekai.Shared.Settings
{
    /// <summary>Owns the existing settings file and mutable UI cache. Construct with a path for isolated checks.</summary>
    public sealed class LiveSettingsStore
    {
        private static LiveSettingsStore _shared;
        public static LiveSettingsStore Shared => _shared ?? (_shared = new LiveSettingsStore(
            Path.Combine(Application.persistentDataPath, "LiveSettingData.json")));

        private readonly string _path;
        private LiveSettingData _cached;
        public LiveSettingsStore(string _path) { this._path = _path ?? throw new ArgumentNullException(nameof(_path)); }

        public LiveSettingData Load()
        {
            if (_cached != null) return _cached;
            if (File.Exists(_path))
            {
                try { _cached = JsonConvert.DeserializeObject<LiveSettingData>(File.ReadAllText(_path)); }
                catch (Exception e) { Debug.LogWarning("LiveSettingData could not be loaded. path:" + _path + " error:" + e.Message); }
            }
            _cached = _cached ?? new LiveSettingData();
            if (_cached.FrameRateLimit <= 0 && _cached.Use120FPS) _cached.FrameRateLimit = 120;
            // The old frame-based clock shift becomes an audio latency in ms, once.
            if (_cached.TimingAdjustData != 0f)
            {
                if (_cached.AudioLatencyMs == 0f)
                {
                    _cached.AudioLatencyMs = OpenSekai.Shared.Live.Simulation.TimingCompensation
                        .AudioLatencyFromLegacyFrames(_cached.TimingAdjustData);
                }
                _cached.TimingAdjustData = 0f;
            }
            return _cached;
        }

        public bool Save(LiveSettingData data)
        {
            _cached = data ?? new LiveSettingData();
            try
            {
                string directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(_path, JsonConvert.SerializeObject(_cached, Formatting.Indented));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("LiveSettingData could not be saved. path:" + _path + " error:" + e.Message);
                return false;
            }
        }

        public PlaybackSettingsSnapshot CaptureSnapshot() => new PlaybackSettingsSnapshot(Load());
    }
}
