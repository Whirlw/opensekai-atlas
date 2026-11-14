using System;
using System.Collections.Generic;
using CriWare;
using UnityEngine;

namespace Sekai
{
	internal sealed class ExternalSoundPack
	{
		private readonly AudioCatalog _catalog;
		private bool _externalSoundPackRequested;

		internal ExternalSoundPack(AudioCatalog catalog)
		{
			_catalog = catalog;
		}

		/// <summary>
		/// Loads StreamingAssets/sound/*.wav, which is how a delivered sound pack replaces the cue sheets
		/// without rebuilding them. Each bundle listed in the manifest becomes one external cue sheet.
		/// </summary>
		internal void Load(Action onDone = null)
		{
			if (_externalSoundPackRequested)
			{
				onDone?.Invoke();
				return;
			}

			_externalSoundPackRequested = true;
			string rootUrl = BuildStreamingAssetUrl("sound");
			CriWare.SoundClipLoader.LoadAll(rootUrl, byBundle =>
			{
				if (byBundle != null)
				{
					foreach (KeyValuePair<string, List<KeyValuePair<string, AudioClip>>> pair in byBundle)
					{
						RegisterExternalAudioClips(pair.Key, pair.Value);
					}
				}

				onDone?.Invoke();
			});
		}

		private void RegisterExternalAudioClips(string bundleName, List<KeyValuePair<string, AudioClip>> clips)
		{
			AudioCatalog.EnsureAtomInitialized();
			CriAtomExAcb acb = CriAtomExAcb.CreateFromAudioClips(clips);
			if (acb == null || !acb.isAvailable)
			{
				return;
			}

			_catalog.Register(bundleName, acb);

			Debug.Log("[SoundManager] external pack: " + clips.Count + " cues for " + bundleName);
		}

		private static string BuildStreamingAssetUrl(string relativePath)
		{
			string path = Application.streamingAssetsPath.TrimEnd('/', '\\') + "/" + relativePath;
			return path.Contains("://") ? path : "file://" + path;
		}

		internal void RegisterClip(string bundleName, string cueName, AudioClip clip)
		{
			if (clip == null || string.IsNullOrEmpty(cueName))
			{
				return;
			}

			if (string.IsNullOrEmpty(bundleName))
			{
				bundleName = cueName;
			}

			AudioCatalog.EnsureAtomInitialized();
			CriAtomExAcb acb = CriAtomExAcb.CreateFromAudioClip(cueName, clip);
			if (acb == null || !acb.isAvailable)
			{
				return;
			}

			_catalog.Register(bundleName, acb);
			string liveMusicBundleName = AudioCatalog.GetLiveMusicBundleNameCandidate(bundleName);
			if (!string.IsNullOrEmpty(liveMusicBundleName))
			{
				_catalog.Register(liveMusicBundleName, acb);
			}
		}
	}
}
