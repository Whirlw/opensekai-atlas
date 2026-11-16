using System;

namespace OpenSekai.Shared.UI
{
    /// <summary>A cover that stays on screen from a menu page into the live scene.</summary>
    public interface ILiveEntryCover
    {
        /// <summary>Calls <paramref name="onCovered"/> once the screen is fully covered, keeping the cover across the scene load.</summary>
        void WhenCovered(Action onCovered);

        /// <summary>Fades the cover out and removes it.</summary>
        void Release();
    }

    /// <summary>
    /// The hand-over between the library's loading screen and the live's song info page. The
    /// library holds a cover while it prepares a song; the live loading screen waits for it to be
    /// fully in before it swaps scenes, and the live releases it when the info page starts, so the
    /// loading screen fades straight into the page with nothing blank in between. Lives started
    /// from elsewhere hold no cover and keep the legacy transition.
    /// </summary>
    public static class LiveEntryCurtain
    {
        private static ILiveEntryCover _current;

        public static bool IsHeld => _current != null;

        public static void Hold(ILiveEntryCover cover)
        {
            _current = cover;
        }

        /// <summary>Forgets <paramref name="cover"/> without fading it; for a cover that went away by itself.</summary>
        public static void Drop(ILiveEntryCover cover)
        {
            if (cover != null && ReferenceEquals(_current, cover))
            {
                _current = null;
            }
        }

        /// <summary>Runs <paramref name="onCovered"/> once the held cover is fully in, or at once when none is held.</summary>
        public static void WhenCovered(Action onCovered)
        {
            if (_current == null)
            {
                onCovered?.Invoke();
                return;
            }

            _current.WhenCovered(onCovered);
        }

        public static void Release()
        {
            ILiveEntryCover cover = _current;
            _current = null;
            cover?.Release();
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewRun()
        {
            _current = null;
        }
    }
}
