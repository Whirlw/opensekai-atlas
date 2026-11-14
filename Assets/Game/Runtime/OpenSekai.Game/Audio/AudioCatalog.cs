using System;
using System.Collections.Generic;
using System.IO;
using CriWare;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	internal sealed class AudioCatalog
	{
		private const string MENU_COMMON_BUNDLE_NAME = "sound/menu/menu_common";
		private const string MENU_COMMON_CUE_SHEET_NAME = "MenuCommon";
		private const string MENU_COMMON_ACB_FILE_NAME = "MenuCommon.acb";
		private const string MENU_COMMON_BUILT_IN_CUE_SHEET_NAME = "MenuCommon_Built_in";
		private const string MENU_COMMON_BUILT_IN_ACB_FILE_NAME = "MenuCommon_Built_in.acb";
		private const string LIVE_DEFAULT_SOUND_BUNDLE_NAME = "live/sound/live_se/default";
		private const string LIVE_DEFAULT_COMMON_ACB_FILE_NAME = "se_live_common.acb.bytes";
		private const string LIVE_TAP_SE_BUNDLE_NAME_PREFIX = "live/tap_se";
		private const string LIVE_TAP_SE_CUSTOM01_BUNDLE_NAME = "live/tap_se/custom01";
		private const string LIVE_MUSIC_BUNDLE_NAME_BASE = "music/long/{0}";
		private static readonly string[] LiveTapPredecodeCueNames =
		{
			"se_live_long",
			"se_live_long_critical",
			"se_live_perfect",
			"se_live_tap",
			"se_live_great",
			"se_live_good",
			"se_live_critical",
			"se_live_connect",
			"se_live_connect_critical",
			"se_live_flick",
			"se_live_flick_critical",
			"se_live_trace",
			"se_live_trace_critical"
		};

		private static readonly string[] LiveDefaultPredecodeCueNames =
		{
			"se_live_long",
			"se_live_long_critical",
			"se_live_perfect",
			"se_live_tap",
			"se_live_great",
			"se_live_good",
			"se_live_critical",
			"se_live_connect",
			"se_live_connect_critical",
			"se_live_flick",
			"se_live_flick_critical",
			"se_live_trace",
			"se_live_trace_critical",
			"se_live_clear",
			"se_live_full_combo",
			"se_live_all_perfect",
			"se_live_end_finish",
			"se_live_end_clear",
			"se_live_end_perfect",
			"se_live_end_all_perfect"
		};

		private static readonly Dictionary<string, string[]> PredecodeCueNamesByBundleName = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
		{
			{ LIVE_DEFAULT_SOUND_BUNDLE_NAME, LiveDefaultPredecodeCueNames },
			{ LIVE_TAP_SE_BUNDLE_NAME_PREFIX, LiveTapPredecodeCueNames },
			{ LIVE_TAP_SE_CUSTOM01_BUNDLE_NAME, LiveTapPredecodeCueNames }
		};
		private static readonly HashSet<string> LiveTapCueNameSet = new HashSet<string>(LiveTapPredecodeCueNames, StringComparer.OrdinalIgnoreCase);

		private readonly HashSet<string> _loadedBundles = new HashSet<string>();
		private readonly List<string> _loadedBundleSearchOrder = new List<string>();
		private readonly Dictionary<string, CriAtomExAcb> _acbByBundleName = new Dictionary<string, CriAtomExAcb>();
		private readonly Dictionary<string, string> _acbFileNameByBundleName = new Dictionary<string, string>
		{
			{ MENU_COMMON_BUNDLE_NAME, MENU_COMMON_ACB_FILE_NAME },
			{ MENU_COMMON_CUE_SHEET_NAME, MENU_COMMON_ACB_FILE_NAME },
			{ MENU_COMMON_BUILT_IN_CUE_SHEET_NAME, MENU_COMMON_BUILT_IN_ACB_FILE_NAME }
		};
		private readonly HashSet<string> _warnedMissingBundles = new HashSet<string>();
		private readonly HashSet<string> _warnedMissingCues = new HashSet<string>();

		internal bool IsLoadedSoundBundle(string bundleName)
		{
			return !string.IsNullOrEmpty(bundleName)
				&& (_loadedBundles.Contains(bundleName) || _acbByBundleName.ContainsKey(bundleName));
		}

		internal void LoadSoundBundle(string bundleName, bool isResident)
		{
			if (string.IsNullOrEmpty(bundleName) || _loadedBundles.Contains(bundleName) || _acbByBundleName.ContainsKey(bundleName))
			{
				return;
			}

			if (TryRegisterLoadedBundleAlias(bundleName))
			{
				return;
			}

			EnsureAtomInitialized();
			List<string> acbFileNames = GetAcbFileNameCandidates(bundleName);
			if (TryLoadSoundBundleFromBuildData(bundleName, acbFileNames))
			{
				return;
			}

			foreach (string acbFileName in acbFileNames)
			{
				CriAtomExAcb acb = AcbAssetSource.Load(bundleName, acbFileName, ShouldPreferAssetBundleAcb(bundleName));
				if (acb == null || !acb.isAvailable)
				{
					continue;
				}

				Register(bundleName, acb);
				PredecodeSoundBundle(bundleName, acb);
				return;
			}

			LogMissingBundle(bundleName, string.Join(", ", acbFileNames.ToArray()));
		}

		internal void Load(string cueSheetName, string acbFileName)
		{
			if (string.IsNullOrEmpty(cueSheetName) || string.IsNullOrEmpty(acbFileName))
			{
				return;
			}

			_acbFileNameByBundleName[cueSheetName] = acbFileName;
			LoadSoundBundle(cueSheetName, true);
		}

		internal bool ExistsCueName(string cueName)
		{
			return ResolveCueName(cueName, out _) != null;
		}

		internal CriAtomEx.CueInfo[] GetCueInfos(string bundleName)
		{
			if (string.IsNullOrEmpty(bundleName))
			{
				return Array.Empty<CriAtomEx.CueInfo>();
			}

			LoadSoundBundle(bundleName, true);
			if (!_acbByBundleName.TryGetValue(bundleName, out CriAtomExAcb acb) || acb == null)
			{
				return Array.Empty<CriAtomEx.CueInfo>();
			}

			return GetCueInfosWithLength(acb);
		}

		internal string ResolveCueName(string cueName, out CriAtomExAcb acb)
		{
			acb = null;
			if (string.IsNullOrEmpty(cueName))
			{
				return null;
			}

			if (IsLiveTapCueName(cueName) && TryResolveCurrentLiveTapSeCue(cueName, out acb, out string currentTapSeCueName))
			{
				return currentTapSeCueName;
			}

			// MenuCommon_Built_in is registered before
			// dynamic menu bundles, so common button SE should resolve there first.
			string builtInCueName = ResolveCueNameFromBundle(MENU_COMMON_BUILT_IN_CUE_SHEET_NAME, cueName, out acb);
			if (builtInCueName != null)
			{
				return builtInCueName;
			}

			foreach (string bundleName in _loadedBundleSearchOrder)
			{
				if (string.Equals(bundleName, MENU_COMMON_BUILT_IN_CUE_SHEET_NAME, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				string resolvedCueName = ResolveCueNameFromBundle(bundleName, cueName, out acb);
				if (resolvedCueName != null)
				{
					return resolvedCueName;
				}
			}

			return null;
		}

		private static bool IsLiveTapCueName(string cueName)
		{
			return !string.IsNullOrEmpty(cueName) && LiveTapCueNameSet.Contains(cueName);
		}

		private bool TryResolveCurrentLiveTapSeCue(string cueName, out CriAtomExAcb acb, out string resolvedCueName)
		{
			string bundleName = LIVE_TAP_SE_BUNDLE_NAME_PREFIX + "/" + LiveConfig.NoteSeName;
			resolvedCueName = ResolveCueNameFromBundle(bundleName, cueName, out acb);
			return resolvedCueName != null;
		}

		private string ResolveCueNameFromBundle(string bundleName, string cueName, out CriAtomExAcb acb)
		{
			acb = null;
			if (string.IsNullOrEmpty(bundleName) || string.IsNullOrEmpty(cueName))
			{
				return null;
			}
			if (!_acbByBundleName.TryGetValue(bundleName, out CriAtomExAcb loadedAcb))
			{
				return null;
			}
			if (loadedAcb != null && loadedAcb.Exists(cueName))
			{
				acb = loadedAcb;
				return cueName;
			}
			return null;
		}

		internal bool TryResolveIngameCue(string cueName, out CriAtomExAcb acb, out string resolvedCueName)
		{
			acb = null;
			resolvedCueName = null;
			if (string.IsNullOrEmpty(cueName))
			{
				return false;
			}

			string liveMusicBundleName = GetLiveMusicBundleNameCandidate(cueName);
			if (TryResolveCueFromSpecificBundle(liveMusicBundleName, cueName, out acb, out resolvedCueName))
			{
				return true;
			}

			if (TryResolveCueFromSpecificBundle(cueName, cueName, out acb, out resolvedCueName))
			{
				return true;
			}

			foreach (string bundleName in _loadedBundleSearchOrder)
			{
				if (TryResolveCueFromSpecificBundle(bundleName, cueName, out acb, out resolvedCueName))
				{
					return true;
				}
			}

			if (!string.IsNullOrEmpty(liveMusicBundleName))
			{
				LoadSoundBundle(liveMusicBundleName, true);
				if (TryResolveCueFromSpecificBundle(liveMusicBundleName, cueName, out acb, out resolvedCueName))
				{
					return true;
				}
			}

			LoadSoundBundle(cueName, true);
			if (TryResolveCueFromSpecificBundle(cueName, cueName, out acb, out resolvedCueName))
			{
				return true;
			}

			if (_acbByBundleName.TryGetValue(cueName, out CriAtomExAcb directAcb) && TryGetFirstCueName(directAcb, out resolvedCueName))
			{
				acb = directAcb;
				return true;
			}

			return false;
		}

		internal static string GetLiveMusicBundleNameCandidate(string cueName)
		{
			if (string.IsNullOrEmpty(cueName))
			{
				return string.Empty;
			}

			string normalized = cueName.Replace('\\', '/');
			if (normalized.StartsWith("music/long/", StringComparison.OrdinalIgnoreCase))
			{
				return normalized;
			}

			if (normalized.Contains("/"))
			{
				return string.Empty;
			}

			return string.Format(LIVE_MUSIC_BUNDLE_NAME_BASE, normalized);
		}

		private bool TryRegisterLoadedBundleAlias(string bundleName)
		{
			if (string.IsNullOrEmpty(bundleName))
			{
				return false;
			}

			string normalized = bundleName.Replace('\\', '/');
			if (!normalized.StartsWith("music/long/", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			string cueName = normalized.Substring("music/long/".Length);
			if (string.IsNullOrEmpty(cueName) || !_acbByBundleName.TryGetValue(cueName, out CriAtomExAcb acb) || acb == null)
			{
				return false;
			}

			Register(bundleName, acb);
			return true;
		}

		private bool TryResolveCueFromSpecificBundle(string bundleName, string cueName, out CriAtomExAcb acb, out string resolvedCueName)
		{
			foreach (string candidateCueName in GetCueNameCandidates(cueName))
			{
				resolvedCueName = ResolveCueNameFromBundle(bundleName, candidateCueName, out acb);
				if (!string.IsNullOrEmpty(resolvedCueName) && acb != null)
				{
					return true;
				}
			}
			resolvedCueName = null;
			acb = null;
			return false;
		}

		private static IEnumerable<string> GetCueNameCandidates(string cueName)
		{
			if (string.IsNullOrEmpty(cueName))
			{
				yield break;
			}
			yield return cueName;
			string normalized = cueName.Replace('\\', '/');
			int slashIndex = normalized.LastIndexOf('/');
			if (slashIndex >= 0 && slashIndex + 1 < normalized.Length)
			{
				normalized = normalized.Substring(slashIndex + 1);
				yield return normalized;
			}
			if (normalized.EndsWith(".acb", StringComparison.OrdinalIgnoreCase))
			{
				yield return normalized.Substring(0, normalized.Length - ".acb".Length);
			}
		}

		private static bool TryGetFirstCueName(CriAtomExAcb acb, out string cueName)
		{
			cueName = null;
			if (acb == null || !acb.isAvailable)
			{
				return false;
			}
			CriAtomEx.CueInfo[] cueInfos = acb.GetCueInfoList();
			for (int i = 0; i < cueInfos.Length; i++)
			{
				if (!string.IsNullOrEmpty(cueInfos[i].name))
				{
					cueName = cueInfos[i].name;
					return true;
				}
			}
			return false;
		}

		private static CriAtomEx.CueInfo[] GetCueInfosWithLength(CriAtomExAcb acb)
		{
			if (acb == null || !acb.isAvailable)
			{
				return Array.Empty<CriAtomEx.CueInfo>();
			}
			CriAtomEx.CueInfo[] cueInfos = acb.GetCueInfoList();
			for (int i = 0; i < cueInfos.Length; i++)
			{
				if (cueInfos[i].length > 0 || string.IsNullOrEmpty(cueInfos[i].name))
				{
					continue;
				}
				try
				{
					AudioClip clip = acb.GetOrCreateAudioClip(cueInfos[i].name);
					if (clip != null)
					{
						cueInfos[i].length = (int)(clip.length * 1000f);
					}
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
				}
			}
			return cueInfos;
		}

		internal static void EnsureAtomInitialized()
		{
			if (!CriAtomPlugin.IsLibraryInitialized())
			{
				CriAtomPlugin.InitializeLibrary();
			}
		}

		private string GetAcbFileName(string bundleName)
		{
			if (_acbFileNameByBundleName.TryGetValue(bundleName, out string acbFileName))
			{
				return acbFileName;
			}

			string lastSegment = bundleName.Replace('\\', '/');
			int slashIndex = lastSegment.LastIndexOf('/');
			if (slashIndex >= 0)
			{
				lastSegment = lastSegment.Substring(slashIndex + 1);
			}
			return lastSegment + ".acb";
		}

		private static bool ShouldPreferAssetBundleAcb(string bundleName)
		{
			// Project-local menu_common contains score-maker cues and is built through
			// the README AssetBundle flow; the loose StreamingAssets ACB is only a fallback.
			return string.Equals(bundleName, MENU_COMMON_BUNDLE_NAME, StringComparison.OrdinalIgnoreCase);
		}

		private List<string> GetAcbFileNameCandidates(string bundleName)
		{
			List<string> candidates = new List<string>();
			AddAcbFileNameCandidateWithVariants(candidates, GetAcbFileName(bundleName));
			if (string.Equals(bundleName, LIVE_DEFAULT_SOUND_BUNDLE_NAME, StringComparison.OrdinalIgnoreCase))
			{
				AddAcbFileNameCandidateWithVariants(candidates, LIVE_DEFAULT_COMMON_ACB_FILE_NAME);
			}
			if (string.Equals(bundleName, MENU_COMMON_BUILT_IN_CUE_SHEET_NAME, StringComparison.OrdinalIgnoreCase))
			{
				AddAcbFileNameCandidateWithVariants(candidates, MENU_COMMON_BUILT_IN_ACB_FILE_NAME);
			}
			if (string.Equals(bundleName, MENU_COMMON_BUNDLE_NAME, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(bundleName, MENU_COMMON_CUE_SHEET_NAME, StringComparison.OrdinalIgnoreCase))
			{
				AddAcbFileNameCandidateWithVariants(candidates, MENU_COMMON_ACB_FILE_NAME);
			}
			return candidates;
		}

		private static void AddAcbFileNameCandidateWithVariants(List<string> candidates, string acbFileName)
		{
			if (string.IsNullOrEmpty(acbFileName))
			{
				return;
			}

			if (acbFileName.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
			{
				string withoutBytes = acbFileName.Substring(0, acbFileName.Length - ".bytes".Length);
				AddAcbFileNameCandidate(candidates, withoutBytes);
				AddAcbFileNameCandidate(candidates, acbFileName);
				return;
			}

			AddAcbFileNameCandidate(candidates, acbFileName);
			AddAcbFileNameCandidate(candidates, acbFileName + ".bytes");
		}

		private static void AddAcbFileNameCandidate(List<string> candidates, string acbFileName)
		{
			if (string.IsNullOrEmpty(acbFileName) || candidates.Contains(acbFileName))
			{
				return;
			}
			candidates.Add(acbFileName);
		}

		private bool TryLoadSoundBundleFromBuildData(string bundleName, List<string> diagnosticCandidates)
		{
			SoundBundleBuildData buildData = AcbAssetSource.GetSoundBundleBuildData(bundleName);
			SoundBundleData[] acbFiles = buildData?.AcbFiles;
			if (acbFiles == null || acbFiles.Length == 0)
			{
				return false;
			}

			bool loadedAny = false;
			for (int i = 0; i < acbFiles.Length; i++)
			{
				SoundBundleData soundBundleData = acbFiles[i];
				if (soundBundleData == null)
				{
					continue;
				}

				List<string> acbFileCandidates = new List<string>();
				AddAcbFileNameCandidateWithVariants(acbFileCandidates, soundBundleData.AssetBundleFileName);
				for (int j = 0; j < acbFileCandidates.Count; j++)
				{
					AddAcbFileNameCandidate(diagnosticCandidates, acbFileCandidates[j]);
					CriAtomExAcb acb = AcbAssetSource.Load(bundleName, acbFileCandidates[j], preferBundle: true);
					if (acb == null || !acb.isAvailable)
					{
						continue;
					}

					string cueSheetName = GetCueSheetName(soundBundleData);
					if (string.IsNullOrEmpty(cueSheetName))
					{
						cueSheetName = bundleName;
					}

					_acbByBundleName[cueSheetName] = acb;
					AddLoadedBundleSearchOrder(cueSheetName);
					if (!_acbByBundleName.ContainsKey(bundleName))
					{
						_acbByBundleName[bundleName] = acb;
					}
					PredecodeSoundBundle(bundleName, acb);
					loadedAny = true;
					break;
				}
			}

			if (!loadedAny)
			{
				return false;
			}

			_loadedBundles.Add(bundleName);
			return true;
		}

		private static string GetCueSheetName(SoundBundleData soundBundleData)
		{
			if (!string.IsNullOrEmpty(soundBundleData.CueSheetName))
			{
				return soundBundleData.CueSheetName;
			}

			string fileName = soundBundleData.AssetBundleFileName;
			if (string.IsNullOrEmpty(fileName))
			{
				return string.Empty;
			}

			if (fileName.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
			{
				fileName = fileName.Substring(0, fileName.Length - ".bytes".Length);
			}
			if (fileName.EndsWith(".acb", StringComparison.OrdinalIgnoreCase))
			{
				fileName = fileName.Substring(0, fileName.Length - ".acb".Length);
			}
			return Path.GetFileName(fileName);
		}

		private void AddLoadedBundleSearchOrder(string bundleName)
		{
			if (!string.IsNullOrEmpty(bundleName) && !_loadedBundleSearchOrder.Contains(bundleName))
			{
				_loadedBundleSearchOrder.Add(bundleName);
			}
		}

		private static void PredecodeSoundBundle(string bundleName, CriAtomExAcb acb)
		{
			if (acb == null || !acb.isAvailable)
			{
				return;
			}

			string[] cueNames = GetPredecodeCueNames(bundleName);
			if (cueNames == null || cueNames.Length == 0)
			{
				return;
			}

			try
			{
				acb.PreloadAudioClips(cueNames);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}

		private static string[] GetPredecodeCueNames(string bundleName)
		{
			if (string.IsNullOrEmpty(bundleName))
			{
				return null;
			}

			string normalized = bundleName.Replace('\\', '/').Trim('/');
			if (PredecodeCueNamesByBundleName.TryGetValue(normalized, out string[] cueNames))
			{
				return cueNames;
			}

			int slashIndex = normalized.LastIndexOf('/');
			while (slashIndex > 0)
			{
				normalized = normalized.Substring(0, slashIndex);
				if (PredecodeCueNamesByBundleName.TryGetValue(normalized, out cueNames))
				{
					return cueNames;
				}
				slashIndex = normalized.LastIndexOf('/');
			}

			return null;
		}

		private void LogMissingBundle(string bundleName, string acbFileName)
		{
			string key = bundleName + "|" + acbFileName;
			if (_warnedMissingBundles.Add(key))
			{
				Debug.LogWarningFormat("Sound bundle could not be loaded. bundle:{0} acb:{1}", bundleName, acbFileName);
			}
		}

		internal void LogMissingCue(string cueName)
		{
			if (!string.IsNullOrEmpty(cueName) && _warnedMissingCues.Add(cueName))
			{
				Debug.LogWarningFormat("{0} is not registered.", cueName);
			}
		}

		internal void Register(string bundleName, CriAtomExAcb acb)
		{
			_acbByBundleName[bundleName] = acb;
			_loadedBundles.Add(bundleName);
			AddLoadedBundleSearchOrder(bundleName);
		}
	}
}
