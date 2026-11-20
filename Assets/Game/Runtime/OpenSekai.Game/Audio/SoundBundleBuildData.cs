using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sekai
{
    public class SoundBundleBuildData : ScriptableObject
    {
        [UnityEngine.Serialization.FormerlySerializedAs("_acfFile")]
        [SerializeField]
        private string acfFile;

        [UnityEngine.Serialization.FormerlySerializedAs("_acbFiles")]
        [SerializeField]
        private SoundBundleData[] acbFiles;

        public string AcfFile => acfFile;

        public IReadOnlyList<SoundBundleData> AcbFiles => Array.AsReadOnly(acbFiles ?? Array.Empty<SoundBundleData>());
    }
}
