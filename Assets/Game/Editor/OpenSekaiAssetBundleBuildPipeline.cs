using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CP;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Sekai;

namespace Sekai.EditorTools
{
	public static class OpenSekaiAssetBundleBuildPipeline
	{
		private const string MENU_PATH = "OpenSekai/AssetBundles/Build Asset Bundles And Info";
		private const string CACHE_ROOT_NAME = "data";
		private const string DEFAULT_CATEGORY = "StartApp";
		private const string ASSET_BUNDLE_INFO_FILE_NAME = "AssetBundleInfo.bytes";
		private const string STREAMING_DATA_RELATIVE_PATH = "Assets/StreamingAssets/data";
		private const string TEMP_OUTPUT_RELATIVE_PATH = "Library/OpenSekaiAssetBundles";

		// Bundles the lightweight player actually loads. Everything else in the project is
		// full-game content and must not leak into the lite build (plan §9.3).
		private static readonly string[] LightweightBundleWhitelist =
		{
			"effect_asset/live/result/default",
			"effect_asset/live/tap_effect/0",
			"effect_asset/live/tap_effect/1",
			"live/2dmode/background/default",
			// live/note/custom01 and live/note/custom02 are no longer bundles: the note prefabs
			// live under Assets/Game/Resources/note so the player build packs them directly.
			// That keeps Assets/StreamingAssets/data down to the bundles whose content can
			// change without republishing the player.
			"live/sound/live_se/default",
			"live/tap_se/custom01",
			"live/tap_se/custom02",
			"shader/live",
			"shader/particles",
			"shader/sandbox",
			"sound/menu/menu_common"
		};

		[MenuItem(MENU_PATH)]
		public static void BuildForActiveTargetMenu()
		{
			BuildForTarget(EditorUserBuildSettings.activeBuildTarget, false);
		}

		public static bool BuildForTarget(BuildTarget target, bool failWhenNoBundles)
		{
			// Building rewrites Assets/StreamingAssets/data in place. Unity refuses to build while
			// playing, but only after the output folder has already been cleared, which leaves the
			// project without a catalog. Bail out before touching anything.
			if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
			{
				const string playingMessage = "AssetBundles cannot be rebuilt while in play mode. Exit play mode first; Assets/StreamingAssets/data was left untouched.";
				if (failWhenNoBundles)
				{
					throw new BuildFailedException(playingMessage);
				}

				Debug.LogError(playingMessage);
				return false;
			}

			AssetDatabase.RemoveUnusedAssetBundleNames();

			string[] configuredBundleNames = AssetDatabase.GetAllAssetBundleNames();
			AssetBundleBuild[] builds = CreateLightweightBuilds(configuredBundleNames, out int skippedBundleCount);
			if (skippedBundleCount > 0)
			{
				Debug.Log($"OpenSekai: skipped {skippedBundleCount} bundle(s) outside the lightweight whitelist.");
			}

			if (builds.Length == 0)
			{
				string message = "No whitelisted AssetBundle contains assets. AssetBundleInfo was left unchanged.";
				if (failWhenNoBundles)
				{
					throw new BuildFailedException(message);
				}

				Debug.LogWarning(message);
				return false;
			}

			string tempOutputPath = GetAbsoluteProjectPath(Path.Combine(TEMP_OUTPUT_RELATIVE_PATH, target.ToString()));
			string streamingDataPath = GetAbsoluteProjectPath(STREAMING_DATA_RELATIVE_PATH);
			RecreateOwnedDirectory(tempOutputPath, GetAbsoluteProjectPath(TEMP_OUTPUT_RELATIVE_PATH));
			RecreateOwnedDirectory(streamingDataPath, GetAbsoluteProjectPath(STREAMING_DATA_RELATIVE_PATH));

			AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
				tempOutputPath,
				builds,
				BuildAssetBundleOptions.ChunkBasedCompression,
				target);

			if (manifest == null)
			{
				throw new BuildFailedException("BuildPipeline.BuildAssetBundles did not return a manifest.");
			}

			AssetBundleInfo info = CreateAssetBundleInfo(manifest, tempOutputPath, streamingDataPath);
			byte[] bytes = SerializeAssetBundleInfo(info);
			WriteAssetBundleInfo(bytes, streamingDataPath);

			AssetDatabase.Refresh();
			Debug.Log($"OpenSekai AssetBundles built. target={target}, bundles={info.bundles.Count}, output={STREAMING_DATA_RELATIVE_PATH}");
			return true;
		}

		private static AssetBundleBuild[] CreateLightweightBuilds(string[] configuredBundleNames, out int skippedBundleCount)
		{
			var whitelist = new HashSet<string>(LightweightBundleWhitelist, StringComparer.OrdinalIgnoreCase);
			var builds = new List<AssetBundleBuild>();
			skippedBundleCount = 0;

			foreach (string bundleName in configuredBundleNames ?? Array.Empty<string>())
			{
				if (!whitelist.Contains(bundleName))
				{
					skippedBundleCount++;
					continue;
				}

				string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
				if (assetPaths == null || assetPaths.Length == 0)
				{
					Debug.LogWarning($"OpenSekai: whitelisted bundle '{bundleName}' has no assets and was skipped.");
					continue;
				}

				builds.Add(new AssetBundleBuild
				{
					assetBundleName = bundleName,
					assetNames = assetPaths
				});
			}

			return builds.ToArray();
		}

		public static bool HasPackagedAssetBundleInfo()
		{
			return File.Exists(Path.Combine(GetAbsoluteProjectPath(STREAMING_DATA_RELATIVE_PATH), ASSET_BUNDLE_INFO_FILE_NAME));
		}

		private static AssetBundleInfo CreateAssetBundleInfo(AssetBundleManifest manifest, string tempOutputPath, string streamingDataPath)
		{
			AssetBundleInfo info = new AssetBundleInfo
			{
				version = PlayerSettings.bundleVersion,
				bundles = new Dictionary<string, AssetBundleElement>()
			};

			string[] bundleNames = manifest.GetAllAssetBundles();
			Array.Sort(bundleNames, StringComparer.Ordinal);

			foreach (string rawBundleName in bundleNames)
			{
				string bundleName = NormalizeBundleName(rawBundleName);
				string sourcePath = Path.Combine(tempOutputPath, ToFileSystemRelativePath(bundleName));
				if (!File.Exists(sourcePath))
				{
					throw new FileNotFoundException($"Built AssetBundle file was not found: {bundleName}", sourcePath);
				}

				string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName)
					.OrderBy(path => path, StringComparer.Ordinal)
					.ToArray();
				string[] dependencies = manifest.GetAllDependencies(bundleName)
					.Select(NormalizeBundleName)
					.OrderBy(path => path, StringComparer.Ordinal)
					.ToArray();

				if (!BuildPipeline.GetCRCForAssetBundle(sourcePath, out uint crc))
				{
					Debug.LogWarning($"Failed to read AssetBundle CRC: {bundleName}");
				}

				AssetBundleElement element = new AssetBundleElement
				{
					bundleName = bundleName,
					cacheFileName = GetCacheFileName(bundleName),
					cacheDirectoryName = GetCacheDirectoryName(bundleName),
					hash = manifest.GetAssetBundleHash(bundleName).ToString(),
					category = InferCategory(bundleName),
					crc = crc,
					fileSize = new FileInfo(sourcePath).Length,
					dependencies = dependencies,
					paths = assetPaths,
					isBuiltin = false,
					isRelocate = true
				};

				string outputPath = GetCacheOutputPath(streamingDataPath, element);
				string outputDirectory = Path.GetDirectoryName(outputPath);
				if (!string.IsNullOrEmpty(outputDirectory))
				{
					Directory.CreateDirectory(outputDirectory);
				}

				File.Copy(sourcePath, outputPath, true);
				info.bundles[bundleName] = element;
			}

			return info;
		}

		private static byte[] SerializeAssetBundleInfo(AssetBundleInfo info)
		{
			return BinarySerializer.Serialize(info);
		}

		private static void WriteAssetBundleInfo(byte[] bytes, string streamingDataPath)
		{
			File.WriteAllBytes(Path.Combine(streamingDataPath, ASSET_BUNDLE_INFO_FILE_NAME), bytes);
		}

		private static string InferCategory(string bundleName)
		{
			if (bundleName.IndexOf("tutorial", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return "Tutorial";
			}

			return DEFAULT_CATEGORY;
		}

		private static string GetCacheOutputPath(string root, AssetBundleElement element)
		{
			if (string.IsNullOrEmpty(element.cacheDirectoryName))
			{
				return Path.Combine(root, element.cacheFileName);
			}

			return Path.Combine(root, element.cacheDirectoryName, element.cacheFileName);
		}

		private static string GetCacheFileName(string bundleName)
		{
			return Md5Hex(NormalizeBundleName(bundleName));
		}

		private static string GetCacheDirectoryName(string bundleName)
		{
			string normalized = NormalizeBundleName(bundleName);
			int slash = normalized.LastIndexOf('/');
			if (slash < 0)
			{
				return string.Empty;
			}

			return Md5Hex(normalized.Substring(0, slash)).Substring(0, 4);
		}

		private static string Md5Hex(string value)
		{
			using (MD5 md5 = MD5.Create())
			{
				byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(value));
				StringBuilder builder = new StringBuilder(hash.Length * 2);
				foreach (byte b in hash)
				{
					builder.Append(b.ToString("x2"));
				}

				return builder.ToString();
			}
		}

		private static string NormalizeBundleName(string bundleName)
		{
			return (bundleName ?? string.Empty).Replace('\\', '/').Trim('/');
		}

		private static string ToFileSystemRelativePath(string bundleName)
		{
			return NormalizeBundleName(bundleName).Replace('/', Path.DirectorySeparatorChar);
		}

		private static string GetAbsoluteProjectPath(string relativePath)
		{
			return Path.GetFullPath(Path.Combine(GetProjectRoot(), relativePath));
		}

		private static string GetProjectRoot()
		{
			return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
		}

		private static void RecreateOwnedDirectory(string path, string expectedRoot)
		{
			string fullPath = Path.GetFullPath(path);
			string fullRoot = Path.GetFullPath(expectedRoot);
			if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException($"Refusing to clear unexpected directory: {fullPath}");
			}

			if (Directory.Exists(fullPath))
			{
				Directory.Delete(fullPath, true);
			}

			Directory.CreateDirectory(fullPath);
		}
	}

	[InitializeOnLoad]
	internal static class OpenSekaiBuildPlayerHandler
	{
		static OpenSekaiBuildPlayerHandler()
		{
			BuildPlayerWindow.RegisterBuildPlayerHandler(BuildPlayerWithAssetBundles);
		}

		private static void BuildPlayerWithAssetBundles(BuildPlayerOptions options)
		{
			BuildTarget target = options.target != BuildTarget.NoTarget
				? options.target
				: EditorUserBuildSettings.activeBuildTarget;

			OpenSekaiAssetBundleBuildPipeline.BuildForTarget(target, false);
			BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
		}
	}

	internal sealed class OpenSekaiAssetBundleBuildPreprocessor : IPreprocessBuildWithReport
	{
		public int callbackOrder => -1000;

		public void OnPreprocessBuild(BuildReport report)
		{
			if (!OpenSekaiAssetBundleBuildPipeline.HasPackagedAssetBundleInfo())
			{
				Debug.LogWarning(
					"OpenSekai AssetBundleInfo was not found in StreamingAssets. " +
					"Use Unity's Build button or OpenSekai/AssetBundles/Build AssetBundles And Info before scripted builds.");
			}
		}
	}
}

