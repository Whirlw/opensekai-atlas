using System.Collections.Generic;
using Sekai;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>What a UI sound means; each maps to one clip of the uisfx "zen" pack.</summary>
    internal enum UiCue
    {
        None,
        Press,
        Select,
        ToggleOn,
        ToggleOff,
        Expand,
        Collapse,
        Open,
        Close,
        Back,
        Forward,
        Start,
        Retry,
        Cancel,
        Warning,
        Delete,
        Success,
        Error,
        Achievement,
        Blocked
    }

    /// <summary>
    /// Plays the menu pages' interface sounds (everything outside the live itself). The clips are the
    /// uisfx "zen" pack (CC0, github.com/romainsimon/uisfx), loaded from Resources/UiSound/Zen on first
    /// use. Volume is <see cref="ApplicationLocalSettings.UiVolume"/>, separate from music and hit sounds.
    /// </summary>
    internal static class UiSound
    {
        private const string CLIP_FOLDER = "UiSound/Zen/";

        /// <summary>The same cue again inside this window is dropped, so a double fire never doubles up.</summary>
        private const float REPEAT_WINDOW = 0.04f;

        private static readonly Dictionary<UiCue, AudioClip> Clips = new Dictionary<UiCue, AudioClip>();
        private static readonly Dictionary<UiCue, float> LastPlayed = new Dictionary<UiCue, float>();
        private static AudioSource _source;
        private static float? _volume;

        internal static float Volume
        {
            get
            {
                if (_volume == null)
                {
                    _volume = Mathf.Clamp01(ApplicationLocalSettings.LoadFromStorage().UiVolume);
                }
                return _volume.Value;
            }
            set => _volume = Mathf.Clamp01(value);
        }

        internal static void Play(UiCue cue)
        {
            if (cue == UiCue.None || Volume <= 0f)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (LastPlayed.TryGetValue(cue, out float last) && now - last < REPEAT_WINDOW)
            {
                return;
            }

            AudioClip clip = ClipOf(cue);
            AudioSource source = Source();
            if (clip == null || source == null)
            {
                return;
            }

            LastPlayed[cue] = now;
            source.PlayOneShot(clip, Volume);
        }

        /// <summary>Plays <paramref name="cue"/> whenever <paramref name="button"/> is clicked, after its own listeners.</summary>
        internal static void Bind(UnityEngine.UI.Button button, UiCue cue)
        {
            if (button != null && cue != UiCue.None)
            {
                button.onClick.AddListener(() => Play(cue));
            }
        }

        private static AudioClip ClipOf(UiCue cue)
        {
            if (Clips.TryGetValue(cue, out AudioClip clip))
            {
                return clip;
            }

            clip = Resources.Load<AudioClip>(CLIP_FOLDER + FileName(cue));
            if (clip == null)
            {
                Debug.LogWarning("[UiSound] Missing clip for " + cue + ".");
            }
            Clips[cue] = clip;
            return clip;
        }

        /// <summary>One source per scene; the library scene is rebuilt after every live.</summary>
        private static AudioSource Source()
        {
            if (_source != null)
            {
                return _source;
            }

            var go = new GameObject("UiSound");
            go.hideFlags = HideFlags.HideInHierarchy;
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.ignoreListenerPause = true;
            return _source;
        }

        private static string FileName(UiCue cue)
        {
            switch (cue)
            {
                case UiCue.Press: return "press";
                case UiCue.Select: return "select";
                case UiCue.ToggleOn: return "toggle-on";
                case UiCue.ToggleOff: return "toggle-off";
                case UiCue.Expand: return "expand";
                case UiCue.Collapse: return "collapse";
                case UiCue.Open: return "open";
                case UiCue.Close: return "close";
                case UiCue.Back: return "back";
                case UiCue.Forward: return "forward";
                case UiCue.Start: return "start";
                case UiCue.Retry: return "retry";
                case UiCue.Cancel: return "cancel";
                case UiCue.Warning: return "warning";
                case UiCue.Delete: return "delete";
                case UiCue.Success: return "success";
                case UiCue.Error: return "error";
                case UiCue.Achievement: return "achievement";
                case UiCue.Blocked: return "blocked";
                default: return string.Empty;
            }
        }
    }
}
