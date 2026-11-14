namespace OpenSekai.Shared.Packages
{
    /// <summary>
    /// The manifest of a player-made package: the shared schema plus nothing. It sits next to
    /// <see cref="SongPackageManifest"/> because that is the type a package is read with; P3-3 moved
    /// it out of the legacy <c>Sekai.MusicScoreMaker.Common</c> namespace with the storage classes.
    /// </summary>
    public sealed class CustomMusicScoreManifest : SongPackageManifest { }
}
