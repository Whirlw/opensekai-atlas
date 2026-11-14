using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OpenSekai.EditorTools
{
    /// <summary>
    /// Writes which code a player build was made from into <c>Assets/Game/Resources/BuildStamp.txt</c>
    /// (gitignored), so every live trace can say it in its header. A device log once came from a build
    /// that did not match the repository, and nothing in the log could show it.
    /// </summary>
    public sealed class BuildStampBuildStep : IPreprocessBuildWithReport
    {
        public const string RelativePath = "Assets/Game/Resources/BuildStamp.txt";

        public int callbackOrder => -900;

        public void OnPreprocessBuild(BuildReport report) => Write();

        [MenuItem("OpenSekai/Dev/Write Build Stamp")]
        public static void Write()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string commit = Git(projectRoot, "rev-parse --short=8 HEAD") ?? "unknown";
            string dirty = string.IsNullOrWhiteSpace(Git(projectRoot, "status --porcelain --untracked-files=no")) ? "" : "+dirty";
            string stamp = $"commit={commit}{dirty} built={DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}";

            string path = Path.Combine(projectRoot, RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, stamp);
            AssetDatabase.ImportAsset(RelativePath);
            Debug.Log("[BuildStamp] " + stamp);
        }

        private static string Git(string workingDirectory, string arguments)
        {
            try
            {
                var start = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using (Process process = Process.Start(start))
                {
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(5000);
                    return process.ExitCode == 0 ? output : null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
