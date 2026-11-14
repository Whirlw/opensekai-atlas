using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace CriWare
{
	/// <summary>
	/// Loads loose wav files that ship next to the game instead of inside an ACB.
	/// The game reads StreamingAssets/sound/&lt;bundle&gt;/&lt;cue&gt;.wav, so a delivered sound pack can be
	/// dropped in without rebuilding the cue sheets.
	/// </summary>
	internal sealed class SoundClipLoader : MonoBehaviour
	{
		private static SoundClipLoader _instance;

		private static SoundClipLoader Instance
		{
			get
			{
				if (_instance == null)
				{
					GameObject host = new GameObject("SoundClipLoader");
					UnityEngine.Object.DontDestroyOnLoad(host);
					_instance = host.AddComponent<SoundClipLoader>();
				}

				return _instance;
			}
		}

		public static void LoadText(string url, Action<string> onDone)
		{
			Instance.StartCoroutine(LoadTextRoutine(url, onDone));
		}

		public static void LoadClip(string url, Action<AudioClip> onDone)
		{
			Instance.StartCoroutine(LoadClipRoutine(url, onDone));
		}

		private static IEnumerator LoadTextRoutine(string url, Action<string> onDone)
		{
			using (UnityWebRequest request = UnityWebRequest.Get(url))
			{
				yield return request.SendWebRequest();
				if (request.result != UnityWebRequest.Result.Success)
				{
					Debug.LogWarning("[SoundClipLoader] " + url + ": " + request.error);
					onDone?.Invoke(null);
					yield break;
				}

				onDone?.Invoke(request.downloadHandler.text);
			}
		}

		private static IEnumerator LoadClipRoutine(string url, Action<AudioClip> onDone)
		{
			using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
			{
				yield return request.SendWebRequest();
				if (request.result != UnityWebRequest.Result.Success)
				{
					Debug.LogWarning("[SoundClipLoader] " + url + ": " + request.error);
					onDone?.Invoke(null);
					yield break;
				}

				onDone?.Invoke(DownloadHandlerAudioClip.GetContent(request));
			}
		}

		/// <summary>
		/// Reads the manifest (bundle,cue) and loads every listed wav, grouping them per bundle so each
		/// bundle ends up with one external cue sheet.
		/// </summary>
		public static void LoadAll(string rootUrl, Action<Dictionary<string, List<KeyValuePair<string, AudioClip>>>> onDone)
		{
			Instance.StartCoroutine(LoadAllRoutine(rootUrl, onDone));
		}

		private static IEnumerator LoadAllRoutine(string rootUrl, Action<Dictionary<string, List<KeyValuePair<string, AudioClip>>>> onDone)
		{
			string manifestText = null;
			yield return LoadTextRoutine(rootUrl + "/manifest.csv", text => manifestText = text);
			if (string.IsNullOrEmpty(manifestText))
			{
				onDone?.Invoke(null);
				yield break;
			}

			Dictionary<string, List<KeyValuePair<string, AudioClip>>> byBundle =
				new Dictionary<string, List<KeyValuePair<string, AudioClip>>>(StringComparer.OrdinalIgnoreCase);
			string[] lines = manifestText.Replace("\r", string.Empty).Split('\n');
			for (int i = 1; i < lines.Length; i++)
			{
				string line = lines[i].Trim();
				if (line.Length == 0)
				{
					continue;
				}

				int comma = line.IndexOf(',');
				if (comma <= 0 || comma >= line.Length - 1)
				{
					continue;
				}

				string bundle = line.Substring(0, comma).Trim();
				string cue = line.Substring(comma + 1).Trim();
				string url = rootUrl + "/" + bundle + "/" + cue + ".wav";

				AudioClip clip = null;
				yield return LoadClipRoutine(url, loaded => clip = loaded);
				if (clip == null)
				{
					continue;
				}

				if (!byBundle.TryGetValue(bundle, out List<KeyValuePair<string, AudioClip>> list))
				{
					list = new List<KeyValuePair<string, AudioClip>>();
					byBundle[bundle] = list;
				}

				list.Add(new KeyValuePair<string, AudioClip>(cue, clip));
			}

			onDone?.Invoke(byBundle);
		}
	}
}
