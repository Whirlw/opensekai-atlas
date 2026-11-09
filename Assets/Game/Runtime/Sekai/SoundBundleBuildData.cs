using UnityEngine;

namespace Sekai
{
    public class SoundBundleBuildData : ScriptableObject
    {
        [SerializeField]
        private string _acfFile;

        [SerializeField]
        private SoundBundleData[] _acbFiles;

        public string AcfFile
        {
            get => _acfFile;
            set => _acfFile = value;
        }

        public SoundBundleData[] AcbFiles
        {
            get => _acbFiles;
            set => _acbFiles = value;
        }
    }
}
