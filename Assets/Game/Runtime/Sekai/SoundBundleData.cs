using System;
using UnityEngine;

namespace Sekai
{
    [Serializable]
    public class SoundBundleData
    {
        public enum FormatType
        {
            Normal = 0,
            Split = 1
        }

        [SerializeField]
        private FormatType _formatType;

        [SerializeField]
        private string _assetBundleFileName;

        [SerializeField]
        private string _cueSheetName;

        // Keep the legacy serialized field typo so copied assets deserialize correctly.
        [SerializeField]
        private int _spilitFileNum;

        public FormatType Format
        {
            get => _formatType;
            set => _formatType = value;
        }

        public string AssetBundleFileName
        {
            get => _assetBundleFileName;
            set => _assetBundleFileName = value;
        }

        public string CueSheetName
        {
            get => _cueSheetName;
            set => _cueSheetName = value;
        }

        public int SpilitFileNum
        {
            get => _spilitFileNum;
            set => _spilitFileNum = value;
        }
    }
}
