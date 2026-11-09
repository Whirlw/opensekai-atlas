using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenSekai.EditorTools
{
    /// <summary>
    /// Entry point for `unity build --execute-method OpenSekai.EditorTools.CliBuild.PerformBuild`.
    /// Keeps the command-line and in-editor builds on the same scene list and options.
    /// </summary>
    public static class CliBuild
    {
        private static readonly string[] Scenes =
        {
            "Assets/Game/Scenes/Library.unity",
            "Assets/Game/Scenes/TransitionBlank.unity",
            "Assets/Game/Scenes/Core.unity",
        };

        private const string DefaultOutputPath = "Builds/Android/OpenSekaiAtlas.apk";

        public static void PerformBuild()
        {
            string outputPath = ResolveOutputPath();
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string[] scenes = Scenes.Where(File.Exists).ToArray();
            if (scenes.Length != Scenes.Length)
            {
                throw new InvalidOperationException(
                    "Missing build scenes: " + string.Join(", ", Scenes.Except(scenes)));
            }

            Debug.Log("[CliBuild] building " + scenes.Length + " scenes -> " + outputPath);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log("[CliBuild] result=" + summary.result + " errors=" + summary.totalErrors +
                      " bytes=" + summary.totalSize + " seconds=" + summary.totalTime.TotalSeconds.ToString("0.0"));

            if (summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }

            EditorApplication.Exit(0);
        }

        private static string ResolveOutputPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-buildOutput", StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return DefaultOutputPath;
        }
    }
}
