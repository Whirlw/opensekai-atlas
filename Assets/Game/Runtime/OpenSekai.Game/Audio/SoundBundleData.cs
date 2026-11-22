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

        [UnityEngine.Serialization.FormerlySerializedAs("_formatType")]
        [SerializeField]
        private FormatType formatType;

        [UnityEngine.Serialization.FormerlySerializedAs("_assetBundleFileName")]
        [SerializeField]
        private string assetBundleFileName;

        [UnityEngine.Serialization.FormerlySerializedAs("_cueSheetName")]
        [SerializeField]
        private string cueSheetName;

        // Keep the legacy serialized field typo so copied assets deserialize correctly.
        [UnityEngine.Serialization.FormerlySerializedAs("_spilitFileNum")]
        [SerializeField]
        private int spilitFileNum;

        public FormatType Format => formatType;

        public string AssetBundleFileName => assetBundleFileName;

        public string CueSheetName => cueSheetName;

        public int SpilitFileNum => spilitFileNum;
    }
}
