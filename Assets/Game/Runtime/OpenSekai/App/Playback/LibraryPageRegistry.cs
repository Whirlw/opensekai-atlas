using System;

namespace OpenSekai.App.Playback
{
    /// <summary>
    /// A page that owns the "library is alive" precondition for starting a live.
    /// Implemented by the lightweight library screen; the legacy editor home page keeps
    /// working through <see cref="PlayService"/>'s fallback check.
    /// </summary>
    public interface ILibraryPage
    {
        bool IsReady { get; }
    }

    /// <summary>
    /// Current library page, if the lightweight screen is mounted. Kept as a plain static
    /// because PlayService has no scene/scene-object reference of its own.
    /// </summary>
    public static class LibraryPageRegistry
    {
        public static ILibraryPage Current { get; private set; }

        public static void Register(ILibraryPage page)
        {
            if (page == null)
            {
                return;
            }
            Current = page;
        }

        public static void Unregister(ILibraryPage page)
        {
            if (page != null && ReferenceEquals(Current, page))
            {
                Current = null;
            }
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewRun()
        {
            Current = null;
        }
    }
}
