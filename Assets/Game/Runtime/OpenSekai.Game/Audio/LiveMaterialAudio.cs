using System;
using System.Collections.Generic;
using System.IO;
using CriWare;
using UnityEngine;

namespace Sekai
{
    /// <summary>Prepared, live-only cues. A retry keeps the clips and stops any previous playback.</summary>
    public sealed class LiveMaterialAudio : IDisposable
    {
        private readonly Dictionary<(string cue, bool loop), CriAtomExAcb.CueAudioClip[]> _bindings = new();
        private readonly HashSet<AudioClip> _clips = new();
        private Action _release;

        public LiveMaterialAudio(Action release) => _release = release;

        public AudioClip CreateClip(string name, int frames, int channels, int rate, float[] samples)
        {
            var clip = AudioClip.Create(name, frames, channels, rate, false);
            if (clip == null) throw new InvalidDataException("无法创建音效 clip。");
            try
            {
                if (!clip.SetData(samples, 0)) throw new InvalidDataException("无法写入音效采样。");
            }
            catch
            {
                UnityEngine.Object.Destroy(clip);
                throw;
            }
            _clips.Add(clip);
            return clip;
        }

        public void Bind(string cue, bool loop, AudioClip clip)
        {
            _bindings.Add((cue, loop), new[] { new CriAtomExAcb.CueAudioClip(clip, 1f) });
        }

        internal bool Contains(string cue) => _bindings.ContainsKey((cue, false)) || _bindings.ContainsKey((cue, true));

        internal bool TryPlay(string cue, bool loop, float volume, int sequence, out uint playback)
        {
            playback = 0;
            if (!_bindings.TryGetValue((cue, loop), out var clips)) return false;
            playback = CriAtomAudioRuntime.Play(clips, volume, 0, sequence, loop).id;
            return true;
        }

        internal void Stop()
        {
            if (_clips.Count > 0) CriAtomAudioRuntime.StopUsingClips(_clips);
        }

        public void Dispose()
        {
            Stop();
            foreach (var clip in _clips) UnityEngine.Object.Destroy(clip);
            _bindings.Clear();
            _clips.Clear();
            _release?.Invoke();
            _release = null;
        }
    }
}
