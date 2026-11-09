using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenSekai.EditorTools
{
    /// <summary>
    /// Copies the repository fixture chart into the editor's persistent data folder so the
    /// library can list it without going through the device.
    /// </summary>
    internal static class DevInstallTestChart
    {
        private const string FixtureDirectory = "Tools/fixtures/baseline_tap";
        private const string PackageFolderName = "Baseline_Tap_120_BPM_codexbaseline20261101tap";

        [MenuItem("OpenSekai/Dev/Install Test Chart")]
        private static void Install()
        {
            string source = Path.GetFullPath(FixtureDirectory);
            if (!Directory.Exists(source))
            {
                Debug.LogError("[DevInstallTestChart] fixture not found: " + source);
                return;
            }

            string target = Path.Combine(Application.persistentDataPath, "CustomMusicScores", PackageFolderName);
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            }

            Debug.Log("[DevInstallTestChart] installed to " + target);
        }

        [MenuItem("OpenSekai/Dev/Remove Test Chart")]
        private static void Remove()
        {
            string target = Path.Combine(Application.persistentDataPath, "CustomMusicScores", PackageFolderName);
            if (Directory.Exists(target))
            {
                Directory.Delete(target, true);
            }

            Debug.Log("[DevInstallTestChart] removed " + target);
        }
    }
}
