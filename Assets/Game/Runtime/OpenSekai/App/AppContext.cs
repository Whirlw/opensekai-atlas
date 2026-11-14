using OpenSekai.App.Playback;
using OpenSekai.Infrastructure.Packages;

namespace OpenSekai.App
{
    /// <summary>
    /// Composition root for the player library: the services the App layer needs, assembled once by
    /// <see cref="Bootstrap.LibraryEntryPoint"/> and handed to the screens it builds. Nothing here is
    /// Unity-specific, so a test can build one with substitutes instead of touching the singletons.
    /// </summary>
    public sealed class AppContext
    {
        public AppContext(SongPackageManager packages, PlayService playback)
        {
            Packages = packages ?? throw new System.ArgumentNullException(nameof(packages));
            Playback = playback ?? throw new System.ArgumentNullException(nameof(playback));
        }

        public SongPackageManager Packages { get; }

        public PlayService Playback { get; }

        /// <summary>Builds the production context: one package store, one playback service.</summary>
        public static AppContext CreateDefault()
        {
            return new AppContext(SongPackageManager.Shared, PlayService.Instance);
        }
    }
}
