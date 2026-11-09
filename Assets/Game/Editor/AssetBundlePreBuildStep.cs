using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sekai.EditorTools
{
    /// <summary>
    /// Fails the player build when the lightweight AssetBundles are older than the assets they were
    /// built from.
    ///
    /// The lite player loads its effects, shaders and sounds from the bundles in
    /// <c>Assets/StreamingAssets/data</c>, and those are produced by a separate step:
    /// <c>OpenSekai/AssetBundles/Build Asset Bundles And Info</c>. A player build does not rebuild
    /// them, and the editor hides that because it loads source assets instead of bundles - which is
    /// exactly how a renamed prefab field shipped twice while the device kept rendering the old
    /// asset. Rebuilding from inside the preprocess step is not an option (it clears the folder the
    /// build is about to copy), so the build simply refuses to run on stale bundles.
    /// </summary>
    public sealed class AssetBundleFreshnessCheck : IPreprocessBuildWithReport
    {
        private const string AssetBundleSourceRelativePath = "Assets/Game/AssetBundles";
        private const string StreamingDataRelativePath = "Assets/StreamingAssets/data";
        private const string AssetBundleInfoFileName = "AssetBundleInfo.bytes";

        /// <summary>
        /// How many files <c>Assets/StreamingAssets/data</c> held when the build started. Captured
        /// here because the folder cannot be trusted after the build finished.
        /// </summary>
        internal static int ExpectedPackagedFileCount { get; private set; }

        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            ExpectedPackagedFileCount = 0;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string streamingData = Path.Combine(projectRoot, StreamingDataRelativePath);
            string infoPath = Path.Combine(streamingData, AssetBundleInfoFileName);

            if (!File.Exists(infoPath))
            {
                throw new BuildFailedException(
                    "AssetBundles are missing: " + StreamingDataRelativePath + " has no " + AssetBundleInfoFileName +
                    ". Run OpenSekai/AssetBundles/Build Asset Bundles And Info before building the player.");
            }

            DateTime bundlesBuiltAt = File.GetLastWriteTimeUtc(infoPath);
            DateTime newestSource = GetNewestSourceTimestamp(Path.Combine(projectRoot, AssetBundleSourceRelativePath));
            if (newestSource != DateTime.MinValue && newestSource > bundlesBuiltAt)
            {
                string stale = FindFirstNewer(Path.Combine(projectRoot, AssetBundleSourceRelativePath), bundlesBuiltAt);
                throw new BuildFailedException(
                    "AssetBundles are stale: " + stale + " is newer than " + AssetBundleInfoFileName +
                    " (" + bundlesBuiltAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + "). " +
                    "Run OpenSekai/AssetBundles/Build Asset Bundles And Info, then build the player again. " +
                    "Without that step the device keeps loading the previous prefabs while the editor looks correct.");
            }

            ExpectedPackagedFileCount = CountPackagedFiles(streamingData);
        }

        private static int CountPackagedFiles(string directory)
        {
            int count = 0;
            foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                // Unity writes a .meta next to every file it imports, and those never travel inside
                // the package; counting them would make the post build check fail on every build.
                if (!path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private static DateTime GetNewestSourceTimestamp(string root)
        {
            if (!Directory.Exists(root))
            {
                return DateTime.MinValue;
            }

            DateTime newest = DateTime.MinValue;
            foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                DateTime stamp = File.GetLastWriteTimeUtc(path);
                if (stamp > newest)
                {
                    newest = stamp;
                }
            }

            return newest;
        }

        private static string FindFirstNewer(string root, DateTime threshold)
        {
            foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (File.GetLastWriteTimeUtc(path) > threshold)
                {
                    return path.Replace('\\', '/');
                }
            }

            return root;
        }
    }

    /// <summary>
    /// Verifies that the bundles actually made it into the player.
    ///
    /// A scripted <c>BuildPipeline.BuildPlayer</c> can finish with <c>Succeeded</c> while packing
    /// none of <c>Assets/StreamingAssets/data</c> - the observed symptom is a player that boots
    /// fine, plays the chart, and silently has no sound effects and no stage background because
    /// every one of them comes from a bundle. Nothing in the build log says so, and the folder on
    /// disk is not a reliable witness either (it has been seen emptied afterwards), so the count is
    /// captured before the build and compared against the finished package.
    /// </summary>
    public sealed class AssetBundlePackagingVerifier : IPostprocessBuildWithReport
    {
        private const string AndroidStreamingAssetsPrefix = "assets/data/";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android)
            {
                return;
            }

            int expected = AssetBundleFreshnessCheck.ExpectedPackagedFileCount;
            if (expected <= 0)
            {
                // The preprocess step did not run (or found nothing); it already reported that.
                return;
            }

            string apkPath = report.summary.outputPath;
            if (string.IsNullOrEmpty(apkPath) || !File.Exists(apkPath))
            {
                throw new BuildFailedException("Cannot verify AssetBundle packaging: the built package was not found at '" + apkPath + "'.");
            }

            if (TryVerify(apkPath, expected, out int packaged, out string message))
            {
                return;
            }

            throw new BuildFailedException(message);
        }

        /// <summary>
        /// Counts the bundle files the package carries under <c>assets/data/</c>. Kept separate from
        /// the report callback so the check can be exercised directly.
        /// </summary>
        public static int CountPackagedBundleFiles(string apkPath)
        {
            int packaged = 0;
            using (ZipArchive archive = ZipFile.OpenRead(apkPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.StartsWith(AndroidStreamingAssetsPrefix, StringComparison.Ordinal))
                    {
                        packaged++;
                    }
                }
            }

            return packaged;
        }

        public static bool TryVerify(string apkPath, int expected, out int packaged, out string message)
        {
            packaged = CountPackagedBundleFiles(apkPath);
            if (packaged >= expected)
            {
                message = null;
                return true;
            }

            message =
                "AssetBundle packaging is broken: " + expected + " files were expected under " + AndroidStreamingAssetsPrefix +
                " but the package contains " + packaged + ". The player would start without sound effects and without the stage " +
                "background, because both come from bundles. Rebuild the bundles (" +
                "OpenSekai/AssetBundles/Build Asset Bundles And Info) and build the player again.";
            return false;
        }
    }
}
