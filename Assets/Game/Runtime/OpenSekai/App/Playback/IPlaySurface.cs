using System;
using Sekai;

namespace OpenSekai.App.Playback
{
    /// <summary>
    /// The screen, scene and transition state a start depends on. PlayService reads this instead of
    /// reaching for the retained singletons, so a test can describe a library that is not ready.
    /// </summary>
    public interface IPlaySurface
    {
        /// <summary>True when a screen manager and scene manager are alive.</summary>
        bool Exists { get; }

        SceneManager.Scene CurrentScene { get; }

        /// <summary>True when the page that owns the start button can still start a live.</summary>
        bool IsLibraryReady { get; }

        bool IsLiveLoadingReady { get; }

        void BeginSession(string chartHash);

        void ForceFinishTransition();

        void PushLiveLoading();

        void SubscribeSceneExit(Action handler);

        void UnsubscribeSceneExit(Action handler);
    }
}
