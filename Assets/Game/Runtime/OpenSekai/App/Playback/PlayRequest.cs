using Sekai;

namespace OpenSekai.App.Playback
{
    public sealed class PlayRequest
    {
        public string PackageDirectory { get; }
        public bool IsAuto { get; }
        public SceneManager.Scene ReturnScene { get; }

        public PlayRequest(string packageDirectory, bool isAuto, SceneManager.Scene returnScene)
        {
            PackageDirectory = packageDirectory;
            IsAuto = isAuto;
            ReturnScene = returnScene;
        }
    }
}
