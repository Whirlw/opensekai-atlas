using System;
using System.Collections.Generic;
using System.IO;
using CriWare;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sekai
{
	internal static class AcbAssetSource
	{
		private const string SOUND_BUNDLE_BUILD_DATA_ASSET_NAME = "SoundBundleBuildData";

		internal static CriAtomExAcb Load(string bundleName, string fileName, bool preferBundle)
		{
			if (preferBundle)
			{
				return LoadAcbData(TryLoadAcbBytesFromAssetBundle(bundleName, fileName))
					?? TryLoadAcbFromStreamingAssets(fileName);
			}

			return TryLoadAcbFromStreamingAssets(fileName)
				?? LoadAcbData(TryLoadAcbBytesFromAssetBundle(bundleName, fileName));
		}

		internal static SoundBundleBuildData GetSoundBundleBuildData(string bundleName)
		{
			if (string.IsNullOrEmpty(bundleName))
			{
				return null;
			}

#if UNITY_EDITOR
			SoundBundleBuildData editorBuildData = TryLoadSoundBundleBuildDataFromEditorAssets(bundleName);
			if (editorBuildData != null)
			{
				return editorBuildData;
			}
#endif

			try
			{
				if (!AssetBundleManager.Instance.HasBundle(bundleName))
				{
					return null;
				}

				return AssetBundleUtility.LoadAsset<SoundBundleBuildData>(bundleName, SOUND_BUNDLE_BUILD_DATA_ASSET_NAME, false);
			}
			catch
			{
				return null;
			}
		}

		private static byte[] TryLoadAcbBytesFromAssetBundle(string bundleName, string acbFileName)
		{
			if (string.IsNullOrEmpty(bundleName) || string.IsNullOrEmpty(acbFileName))
			{
				return null;
			}

			try
			{
#if UNITY_EDITOR
				byte[] editorBytes = TryLoadAcbBytesFromEditorAssets(bundleName, acbFileName);
				if (editorBytes != null && editorBytes.Length > 0)
				{
					return editorBytes;
				}
#endif

				if (!AssetBundleManager.Instance.HasBundle(bundleName))
				{
					return null;
				}
				TextAsset acbAsset = AssetBundleUtility.LoadAsset<TextAsset>(bundleName, acbFileName, false);
				return acbAsset != null ? acbAsset.bytes : null;
			}
			catch
			{
				return null;
			}
		}

#if UNITY_EDITOR
		private static SoundBundleBuildData TryLoadSoundBundleBuildDataFromEditorAssets(string bundleName)
		{
			foreach (string basePath in GetEditorAssetBundleResourceBasePaths(bundleName))
			{
				string path = basePath + "/" + SOUND_BUNDLE_BUILD_DATA_ASSET_NAME + ".asset";
				SoundBundleBuildData buildData = AssetDatabase.LoadAssetAtPath<SoundBundleBuildData>(path);
				if (buildData != null)
				{
					return buildData;
				}
			}
			return null;
		}

		private static byte[] TryLoadAcbBytesFromEditorAssets(string bundleName, string acbFileName)
		{
			foreach (string basePath in GetEditorAssetBundleResourceBasePaths(bundleName))
			{
				foreach (string fileName in GetEditorAcbFileNameCandidates(acbFileName))
				{
					string path = basePath + "/" + fileName;
					TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
					if (textAsset != null && textAsset.bytes != null && textAsset.bytes.Length > 0)
					{
						return textAsset.bytes;
					}

					string fullPath = Path.Combine(Directory.GetCurrentDirectory(), path);
					if (File.Exists(fullPath))
					{
						return File.ReadAllBytes(fullPath);
					}
				}
			}
			return null;
		}

		private static IEnumerable<string> GetEditorAssetBundleResourceBasePaths(string bundleName)
		{
			string normalized = bundleName.Replace('\\', '/').Trim('/');
			yield return "Assets/Game/AssetBundles/resources/startapp/" + normalized;
			yield return "Assets/Game/AssetBundles/resources/tutorial/" + normalized;
			yield return "Assets/Game/AssetBundles/resources/" + normalized;
		}

		private static IEnumerable<string> GetEditorAcbFileNameCandidates(string acbFileName)
		{
			if (string.IsNullOrEmpty(acbFileName))
			{
				yield break;
			}

			yield return acbFileName;
			if (acbFileName.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
			{
				yield return acbFileName.Substring(0, acbFileName.Length - ".bytes".Length);
			}
			else
			{
				yield return acbFileName + ".bytes";
			}
		}
#endif

		private static CriAtomExAcb LoadAcbData(byte[] acbBytes)
		{
			if (acbBytes == null || acbBytes.Length == 0)
			{
				return null;
			}

			try
			{
				return CriAtomExAcb.LoadAcbData(acbBytes, null, null);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				return null;
			}
		}

		private static CriAtomExAcb TryLoadAcbFromStreamingAssets(string acbFileName)
		{
			if (string.IsNullOrEmpty(acbFileName))
			{
				return null;
			}

			foreach (string candidate in GetStreamingAssetPathCandidates(acbFileName))
			{
				if (!ShouldTryStreamingAssetPath(candidate))
				{
					continue;
				}

				try
				{
					CriAtomExAcb acb = CriAtomExAcb.LoadAcbFile(null, candidate, null);
					if (acb != null && acb.isAvailable)
					{
						return acb;
					}
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
				}
			}

			return null;
		}

		private static IEnumerable<string> GetStreamingAssetPathCandidates(string fileName)
		{
			string[] candidates =
			{
				Path.Combine(CriWare.Common.streamingAssetsPath, fileName),
				Path.Combine(CriWare.Common.streamingAssetsPath, "data", fileName),
				Path.Combine(CriWare.Common.streamingAssetsPath, "sound/menu/menu_common", fileName),
				Path.Combine(Application.streamingAssetsPath, fileName),
				Path.Combine(Application.streamingAssetsPath, "data", fileName),
				Path.Combine(Application.streamingAssetsPath, "sound/menu/menu_common", fileName)
			};

			foreach (string candidate in candidates)
			{
				if (!string.IsNullOrEmpty(candidate))
				{
					yield return candidate;
				}
			}
		}

		private static bool ShouldTryStreamingAssetPath(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return false;
			}

			if (Application.platform == RuntimePlatform.Android)
			{
				return true;
			}

			return File.Exists(path);
		}
	}
}
