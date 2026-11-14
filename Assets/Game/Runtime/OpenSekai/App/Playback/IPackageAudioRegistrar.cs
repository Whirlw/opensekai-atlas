using System.Threading;
using Cysharp.Threading.Tasks;
using OpenSekai.Shared.Packages;

namespace OpenSekai.App.Playback
{
    /// <summary>
    /// Registers a package's audio before the live starts. Split out of PlayService so a start path
    /// test does not need a real audio file and the external-audio registry.
    /// </summary>
    public interface IPackageAudioRegistrar
    {
        UniTask<bool> RegisterAsync(SongPackage package, CancellationToken cancellationToken);
    }
}
