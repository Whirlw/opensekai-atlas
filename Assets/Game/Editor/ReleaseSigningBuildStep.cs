#if UNITY_ANDROID
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenSekai.EditorTools
{
    /// <summary>
    /// Applies the public release keystore before every Android build.
    /// </summary>
    /// <remarks>
    /// The keystore and its password are committed on purpose: this project is distributed from
    /// GitHub only, and a stable signature is what lets a player install a new APK on top of an
    /// older one. Anyone can rebuild an identically signed package, so authenticity comes from the
    /// published release and its checksum, not from the key being secret.
    /// </remarks>
    public sealed class ReleaseSigningBuildStep : IPreprocessBuildWithReport
    {
        public const string KeystoreRelativePath = "Tools/signing/opensekai-atlas-release.keystore";
        public const string KeystorePassword = "opensekai";
        public const string KeyaliasName = "opensekai";

        /// <summary>Runs before the default preprocessors so the keystore is in place early.</summary>
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report == null || report.summary.platform != BuildTarget.Android)
            {
                return;
            }

            Apply();
        }

        [MenuItem("OpenSekai/Dev/Apply Release Signing")]
        public static void Apply()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absolutePath = Path.Combine(projectRoot, KeystoreRelativePath);
            if (!File.Exists(absolutePath))
            {
                Debug.LogWarning("[ReleaseSigning] keystore not found: " + absolutePath);
                return;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = KeystoreRelativePath;
            PlayerSettings.Android.keystorePass = KeystorePassword;
            PlayerSettings.Android.keyaliasName = KeyaliasName;
            PlayerSettings.Android.keyaliasPass = KeystorePassword;

            Debug.Log("[ReleaseSigning] signing with " + KeystoreRelativePath + " (alias " + KeyaliasName + ").");
        }
    }
}
#endif
