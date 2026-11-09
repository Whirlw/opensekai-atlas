using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.IO;
using AcbDecoder;
using UnityEngine;
using UnityEngine.Networking;
using Sekai;

namespace CriWare
{
	public sealed class CriFsBinder
	{
	}

	public static class Common
	{
		public static string streamingAssetsPath => Application.streamingAssetsPath;
	}

	public static class CriAtomPlugin
	{
		private static bool _initialized;

		public static bool IsLibraryInitialized()
		{
			return _initialized;
		}

		public static void InitializeLibrary()
		{
			_initialized = true;
			CriAtomAudioRuntime.EnsureInitialized();
		}

		public static void FinalizeLibrary()
		{
			_initialized = false;
		}
	}

	public static class CriAtomEx
	{
		public struct CueInfo
		{
			public int id;
			public string name;
			public int length;
			public int numTracks;
			public int numRelatedWaveforms;
		}

		public static bool RegisterAcf(CriFsBinder binder, string path)
		{
			// The compatibility layer decodes ACB/HCA directly, so ACF data is not needed.
			return !string.IsNullOrEmpty(path);
		}
	}

	public sealed class CriAtomExAcb
	{
		private readonly AcbFile _acbFile;
		private readonly Dictionary<string, List<CueEntry>> _entriesByCueName = new Dictionary<string, List<CueEntry>>(StringComparer.Ordinal);
		private readonly Dictionary<string, AudioClip> _clipsByCueName = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
		private readonly Dictionary<AcbEntry, AudioClip> _clipsByEntry = new Dictionary<AcbEntry, AudioClip>();
		private readonly Dictionary<string, AudioClip> _externalClipsByCueName = new Dictionary<string, AudioClip>(StringComparer.Ordinal);

		private CriAtomExAcb()
		{
			isAvailable = false;
		}

		private CriAtomExAcb(AcbFile _acbFile)
		{
			this._acbFile = _acbFile;
			isAvailable = _acbFile != null;
			if (_acbFile == null)
			{
				return;
			}

			Dictionary<int, AcbEntry> entriesByWaveId = new Dictionary<int, AcbEntry>();
			foreach (AcbEntry entry in _acbFile.Entries)
			{
				if (!entriesByWaveId.ContainsKey(entry.WaveId))
				{
					entriesByWaveId.Add(entry.WaveId, entry);
				}
			}

			foreach (KeyValuePair<string, IReadOnlyList<AcbCueWaveform>> pair in _acbFile.CueWaveforms)
			{
				foreach (AcbCueWaveform waveform in pair.Value)
				{
					if (entriesByWaveId.TryGetValue(waveform.WaveId, out AcbEntry entry))
					{
						RegisterCueName(pair.Key, entry, waveform.VolumeScale);
					}
				}
			}

			foreach (AcbEntry entry in _acbFile.Entries)
			{
				RegisterCueNameFallback(entry.Name, entry);
			}
		}

		private CriAtomExAcb(string _cueName, AudioClip clip)
		{
			isAvailable = clip != null && !string.IsNullOrEmpty(_cueName);
			if (!isAvailable)
			{
				return;
			}

			_externalClipsByCueName[_cueName] = clip;
			_clipsByCueName[_cueName] = clip;
		}

		public bool isAvailable { get; private set; }

		public static CriAtomExAcb LoadAcbFile(CriFsBinder binder, string acbPath, string awbPath)
		{
			byte[] bytes = ReadAllBytes(acbPath);
			return LoadAcbData(bytes, null, null);
		}

		public static CriAtomExAcb LoadAcbData(byte[] acbBytes, object awbBinder, object awb)
		{
			if (acbBytes == null || acbBytes.Length == 0)
			{
				return null;
			}

			try
			{
				return new CriAtomExAcb(AcbFile.Load(acbBytes));
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				return null;
			}
		}

		public static CriAtomExAcb CreateFromAudioClip(string _cueName, AudioClip clip)
		{
			return new CriAtomExAcb(_cueName, clip);
		}
		public static CriAtomExAcb CreateFromAudioClips(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, AudioClip>> clips)
		{
			if (clips == null)
			{
				return null;
			}

			CriAtomExAcb acb = new CriAtomExAcb();
			foreach (System.Collections.Generic.KeyValuePair<string, AudioClip> pair in clips)
			{
				if (pair.Value == null || string.IsNullOrEmpty(pair.Key))
				{
					continue;
				}

				acb._externalClipsByCueName[pair.Key] = pair.Value;
				acb._clipsByCueName[pair.Key] = pair.Value;
			}

			acb.isAvailable = acb._externalClipsByCueName.Count > 0;
			return acb.isAvailable ? acb : null;
		}


		public bool Exists(string _cueName)
		{
			return !string.IsNullOrEmpty(_cueName)
				&& (_entriesByCueName.ContainsKey(_cueName) || _externalClipsByCueName.ContainsKey(_cueName));
		}

		public bool GetCueInfo(string _cueName, out CriAtomEx.CueInfo cueInfo)
		{
			cueInfo = default(CriAtomEx.CueInfo);
			if (!string.IsNullOrEmpty(_cueName) && _externalClipsByCueName.TryGetValue(_cueName, out AudioClip clip))
			{
				cueInfo = CreateCueInfo(_cueName, clip);
				return true;
			}
			if (string.IsNullOrEmpty(_cueName) || !_entriesByCueName.TryGetValue(_cueName, out List<CueEntry> entries) || entries.Count == 0)
			{
				return false;
			}

			cueInfo = CreateCueInfo(_cueName, entries);
			return true;
		}

		public CriAtomEx.CueInfo[] GetCueInfoList()
		{
			CriAtomEx.CueInfo[] cueInfos = new CriAtomEx.CueInfo[_entriesByCueName.Count + _externalClipsByCueName.Count];
			HashSet<string> addedNames = new HashSet<string>(StringComparer.Ordinal);
			int index = 0;
			foreach (KeyValuePair<string, AudioClip> pair in _externalClipsByCueName)
			{
				cueInfos[index++] = CreateCueInfo(pair.Key, pair.Value);
				addedNames.Add(pair.Key);
			}
			foreach (KeyValuePair<string, List<CueEntry>> pair in _entriesByCueName)
			{
				if (addedNames.Contains(pair.Key))
				{
					continue;
				}
				cueInfos[index++] = CreateCueInfo(pair.Key, pair.Value);
			}
			if (index != cueInfos.Length)
			{
				Array.Resize(ref cueInfos, index);
			}
			return cueInfos;
		}

		internal AudioClip GetOrCreateAudioClip(string _cueName)
		{
			if (!string.IsNullOrEmpty(_cueName) && _externalClipsByCueName.TryGetValue(_cueName, out AudioClip externalClip))
			{
				return externalClip;
			}
			if (string.IsNullOrEmpty(_cueName) || !_entriesByCueName.TryGetValue(_cueName, out List<CueEntry> entries) || entries.Count == 0)
			{
				return null;
			}

			AcbEntry entry = entries[0].Entry;
			if (_clipsByCueName.TryGetValue(_cueName, out AudioClip cachedClip) && cachedClip != null)
			{
				return cachedClip;
			}

			if (_clipsByEntry.TryGetValue(entry, out AudioClip cachedEntryClip) && cachedEntryClip != null)
			{
				_clipsByCueName[_cueName] = cachedEntryClip;
				return cachedEntryClip;
			}

			AudioClip clip = DecodeEntryToAudioClip(entry, _cueName);
			RegisterDecodedClip(entry, clip);
			return clip;
		}

		internal CueAudioClip[] GetOrCreateAudioClips(string _cueName)
		{
			if (!string.IsNullOrEmpty(_cueName) && _externalClipsByCueName.TryGetValue(_cueName, out AudioClip externalClip))
			{
				return new[] { new CueAudioClip(externalClip, 1f) };
			}
			if (string.IsNullOrEmpty(_cueName) || !_entriesByCueName.TryGetValue(_cueName, out List<CueEntry> entries) || entries.Count == 0)
			{
				return Array.Empty<CueAudioClip>();
			}

			var clips = new List<CueAudioClip>(entries.Count);
			foreach (CueEntry cueEntry in entries)
			{
				if (cueEntry?.Entry == null)
				{
					continue;
				}

				AudioClip clip = GetOrCreateAudioClip(cueEntry.Entry, _cueName);
				if (clip != null)
				{
					clips.Add(new CueAudioClip(clip, cueEntry.VolumeScale));
				}
			}
			return clips.ToArray();
		}

		internal int PreloadAllAudioClips()
		{
			if (_acbFile == null)
			{
				return 0;
			}

			int count = 0;
			foreach (AcbEntry entry in _acbFile.Entries)
			{
				try
				{
					if (_clipsByEntry.ContainsKey(entry))
					{
						continue;
					}

					string clipName = string.IsNullOrEmpty(entry.Name) ? "ACB Audio" : entry.Name;
					AudioClip clip = DecodeEntryToAudioClip(entry, clipName);
					RegisterDecodedClip(entry, clip);
					count++;
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
				}
			}
			return count;
		}

		internal int PreloadAudioClips(IEnumerable<string> cueNames)
		{
			if (cueNames == null)
			{
				return 0;
			}

			int count = 0;
			foreach (string _cueName in cueNames)
			{
				try
				{
					if (GetOrCreateAudioClips(_cueName).Length > 0)
					{
						count++;
					}
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
				}
			}
			return count;
		}

		private AudioClip DecodeEntryToAudioClip(AcbEntry entry, string clipName)
		{
			AudioData audioData = _acbFile.Decode(entry);
			return audioData.ToAudioClip(clipName, false);
		}

		private AudioClip GetOrCreateAudioClip(AcbEntry entry, string clipName)
		{
			if (entry == null)
			{
				return null;
			}

			if (_clipsByEntry.TryGetValue(entry, out AudioClip cachedEntryClip) && cachedEntryClip != null)
			{
				return cachedEntryClip;
			}

			AudioClip clip = DecodeEntryToAudioClip(entry, clipName);
			RegisterDecodedClip(entry, clip);
			return clip;
		}

		private void RegisterDecodedClip(AcbEntry entry, AudioClip clip)
		{
			if (entry == null || clip == null)
			{
				return;
			}

			_clipsByEntry[entry] = clip;
			foreach (KeyValuePair<string, List<CueEntry>> pair in _entriesByCueName)
			{
				foreach (CueEntry cueEntry in pair.Value)
				{
					if (ReferenceEquals(cueEntry.Entry, entry) && !_clipsByCueName.ContainsKey(pair.Key))
					{
						_clipsByCueName[pair.Key] = clip;
					}
				}
			}
		}

		private static CriAtomEx.CueInfo CreateCueInfo(string _cueName, List<CueEntry> entries)
		{
			AcbEntry entry = entries != null && entries.Count > 0 ? entries[0].Entry : null;
			return new CriAtomEx.CueInfo
			{
				id = entry != null ? entry.WaveId : 0,
				name = _cueName,
				length = 0,
				numTracks = entries != null ? entries.Count : 0,
				numRelatedWaveforms = entries != null ? entries.Count : 0
			};
		}

		private static CriAtomEx.CueInfo CreateCueInfo(string _cueName, AudioClip clip)
		{
			return new CriAtomEx.CueInfo
			{
				id = 0,
				name = _cueName,
				length = clip != null ? (int)(clip.length * 1000f) : 0,
				numTracks = 1,
				numRelatedWaveforms = clip != null ? 1 : 0
			};
		}

		private static void RegisterCueName(Dictionary<string, List<CueEntry>> map, string _cueName, AcbEntry entry, float volumeScale)
		{
			if (string.IsNullOrEmpty(_cueName) || entry == null)
			{
				return;
			}

			if (!map.TryGetValue(_cueName, out List<CueEntry> entries))
			{
				entries = new List<CueEntry>();
				map.Add(_cueName, entries);
			}

			entries.Add(new CueEntry(entry, volumeScale));
		}

		private void RegisterCueName(string _cueName, AcbEntry entry, float volumeScale)
		{
			RegisterCueName(_entriesByCueName, _cueName, entry, volumeScale);
		}

		private void RegisterCueNameFallback(string cueNames, AcbEntry entry)
		{
			if (string.IsNullOrEmpty(cueNames))
			{
				return;
			}
			string[] splitNames = cueNames.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string splitName in splitNames)
			{
				string _cueName = splitName.Trim();
				if (!_entriesByCueName.ContainsKey(_cueName))
				{
					RegisterCueName(_entriesByCueName, _cueName, entry, 1f);
				}
			}
		}

		internal readonly struct CueAudioClip
		{
			public CueAudioClip(AudioClip clip, float volumeScale)
			{
				Clip = clip;
				VolumeScale = volumeScale;
			}

			public AudioClip Clip { get; }

			public float VolumeScale { get; }
		}

		private sealed class CueEntry
		{
			public CueEntry(AcbEntry entry, float volumeScale)
			{
				Entry = entry;
				VolumeScale = Mathf.Max(0f, volumeScale);
			}

			public AcbEntry Entry { get; }

			public float VolumeScale { get; }
		}

		private static byte[] ReadAllBytes(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return null;
			}

			if (File.Exists(path))
			{
				return File.ReadAllBytes(path);
			}

			if (!path.Contains("://") && !path.Contains("!/"))
			{
				return null;
			}

			using (UnityWebRequest request = UnityWebRequest.Get(path))
			{
				UnityWebRequestAsyncOperation operation = request.SendWebRequest();
				while (!operation.isDone)
				{
				}

				if (request.result != UnityWebRequest.Result.Success)
				{
					Debug.LogWarningFormat("Failed to load ACB file. path:{0} error:{1}", path, request.error);
					return null;
				}

				return request.downloadHandler.data;
			}
		}
	}

	public sealed class CriAtomExPlayer : IDisposable
	{
		private CriAtomExAcb _acb;
		private string _cueName;
		private float _volume = 1f;
		private long _startTimeMs;
		private CriAtomExPlayback _currentPlayback;

		public void SetCue(CriAtomExAcb _acb, string _cueName)
		{
			this._acb = _acb;
			this._cueName = _cueName;
		}

		public void SetVolume(float _volume)
		{
			this._volume = Mathf.Max(0f, _volume);
		}

		public void SetStartTime(long _startTimeMs)
		{
			this._startTimeMs = Math.Max(0L, _startTimeMs);
		}

		public void SetPitch(float pitch)
		{
			// Pitch will be applied when Start() is called via CriAtomAudioRuntime
			// Store for later use or apply to existing playback
			if (_currentPlayback.id != 0)
			{
				CriAtomAudioRuntime.SetPitch(_currentPlayback.id, pitch);
			}
		}

		public CriAtomExPlayback Start()
		{
			if (_acb == null || string.IsNullOrEmpty(_cueName))
			{
				_currentPlayback = CriAtomExPlayback.Invalid;
				return _currentPlayback;
			}

			CriAtomExAcb.CueAudioClip[] clips = _acb.GetOrCreateAudioClips(_cueName);
			_currentPlayback = CriAtomAudioRuntime.Play(clips, _volume, _startTimeMs / 1000f);
			_startTimeMs = 0L;
			return _currentPlayback;
		}

		public void Stop()
		{
			_currentPlayback.Stop();
		}

		public void Pause()
		{
			_currentPlayback.Pause();
		}

		public void Resume()
		{
			_currentPlayback.Resume();
		}

		public CriAtomExPlayback.Status GetStatus()
		{
			return _currentPlayback.GetStatus();
		}

		public void Dispose()
		{
			Stop();
		}
	}

	public struct CriAtomExPlayback
	{
		public enum Status
		{
			Stop = 0,
			Prep = 1,
			Playing = 2,
			PlayEnd = 3,
			Error = 4
		}

		public static readonly CriAtomExPlayback Invalid = new CriAtomExPlayback(0);

		public readonly uint id;

		internal CriAtomExPlayback(uint id)
		{
			this.id = id;
		}

		public Status GetStatus()
		{
			return CriAtomAudioRuntime.GetStatus(id);
		}

		public long GetTime()
		{
			return CriAtomAudioRuntime.GetTimeMs(id);
		}

		public void GetTimeAndScaleSyncedWithAudio(out long playbackTime, out float timeScale)
		{
			CriAtomAudioRuntime.GetTimeAndScaleSyncedWithAudio(id, out playbackTime, out timeScale);
		}

		public void Stop()
		{
			CriAtomAudioRuntime.Stop(id);
		}

		public void Pause()
		{
			CriAtomAudioRuntime.Pause(id);
		}

		public void Resume()
		{
			CriAtomAudioRuntime.Resume(id);
		}
	}

	internal static class CriAtomAudioRuntime
	{
		private sealed class PlaybackState
		{
						private GameObject _GameObject;
			public GameObject GameObject
			{
			    get => _GameObject;
			    set => _GameObject = value;
			}
						private AudioSource _PrimarySource;
			public AudioSource PrimarySource
			{
			    get => _PrimarySource;
			    set => _PrimarySource = value;
			}
						private AudioSource[] _Sources;
			public AudioSource[] Sources
			{
			    get => _Sources;
			    set => _Sources = value;
			}
						private bool _Stopped;
			public bool Stopped
			{
			    get => _Stopped;
			    set => _Stopped = value;
			}
						private float _BaseTimeSeconds;
			public float BaseTimeSeconds
			{
			    get => _BaseTimeSeconds;
			    set => _BaseTimeSeconds = value;
			}
						private long _BaseStopwatchTicks;
			public long BaseStopwatchTicks
			{
			    get => _BaseStopwatchTicks;
			    set => _BaseStopwatchTicks = value;
			}
						private float _PausedTimeSeconds;
			public float PausedTimeSeconds
			{
			    get => _PausedTimeSeconds;
			    set => _PausedTimeSeconds = value;
			}
						private bool _Paused;
						private bool _InUse;
						public bool InUse { get => _InUse; set => _InUse = value; }
			public bool Paused
			{
			    get => _Paused;
			    set => _Paused = value;
			}
		}

		private static readonly Dictionary<uint, PlaybackState> _playbacks = new Dictionary<uint, PlaybackState>();
		// Playing a sound used to build a GameObject and an AudioSource per hit, which costs setup time
		// on the audio thread and a garbage spike every note. Finished playbacks are parked here instead.
		private static readonly List<PlaybackState> _freeStates = new List<PlaybackState>();
		private static readonly double StopwatchSecondsPerTick = 1d / Stopwatch.Frequency;
		private static GameObject _root;
		private static AudioListener _fallbackAudioListener;
		private static uint _nextPlaybackId = 1;

		public static void EnsureInitialized()
		{
			if (_root == null)
			{
				_root = new GameObject("CriAtomAudioRuntime");
				UnityEngine.Object.DontDestroyOnLoad(_root);
			}
			if (_freeStates.Count == 0)
			{
				// Warm one playback so the first hit of a song does not pay for creating a source.
				ReleasePlaybackState(RentPlaybackState(2, 0), 0);
			}

			EnsureAudioListenerGuard();
			CacheFallbackAudioListener();
			if (HasEnabledExternalAudioListener())
			{
				ReleaseFallbackAudioListener();
				return;
			}

			EnsureFallbackAudioListener();
		}

		public static CriAtomExPlayback Play(AudioClip clip, float _volume, float startTimeSeconds)
		{
			if (clip == null)
			{
				return CriAtomExPlayback.Invalid;
			}

			return Play(new[] { new CriAtomExAcb.CueAudioClip(clip, 1f) }, _volume, startTimeSeconds);
		}

		public static CriAtomExPlayback Play(CriAtomExAcb.CueAudioClip[] clips, float _volume, float startTimeSeconds)
		{
			if (clips == null || clips.Length == 0)
			{
				return CriAtomExPlayback.Invalid;
			}

			EnsureInitialized();
			uint id = AllocatePlaybackId();
			int clipCount = 0;
			for (int i = 0; i < clips.Length; i++)
			{
				if (clips[i].Clip != null)
				{
					clipCount++;
				}
			}

			if (clipCount == 0)
			{
				return CriAtomExPlayback.Invalid;
			}

			PlaybackState state = RentPlaybackState(clipCount, id);
			AudioSource[] sources = state.Sources;
			float primaryStartTimeSeconds = 0f;
			int sourceIndex = 0;
			for (int i = 0; i < clips.Length; i++)
			{
				AudioClip clip = clips[i].Clip;
				if (clip == null)
				{
					continue;
				}

				AudioSource source = sources[sourceIndex];
				source.Stop();
				source.playOnAwake = false;
				source.spatialBlend = 0f;
				source.clip = clip;
				source.volume = Mathf.Max(0f, _volume * clips[i].VolumeScale);
				bool canSeek = startTimeSeconds > 0f && startTimeSeconds < clip.length;
				source.time = canSeek ? startTimeSeconds : 0f;
				if (sourceIndex == 0 && canSeek)
				{
					primaryStartTimeSeconds = startTimeSeconds;
				}

				sourceIndex++;
			}

			state.PrimarySource = sources[0];
			state.Stopped = false;
			state.InUse = true;
			state.BaseTimeSeconds = primaryStartTimeSeconds;
			state.PausedTimeSeconds = primaryStartTimeSeconds;
			state.Paused = false;
			_playbacks[id] = state;

			EnsureAudioListener();
			state.BaseStopwatchTicks = Stopwatch.GetTimestamp();
			for (int i = 0; i < sourceIndex; i++)
			{
				sources[i].Play();
			}

			return new CriAtomExPlayback(id);
		}

		private static PlaybackState RentPlaybackState(int clipCount, uint id)
		{
			PlaybackState state = null;
			for (int i = _freeStates.Count - 1; i >= 0; i--)
			{
				PlaybackState candidate = _freeStates[i];
				if (candidate.GameObject == null || candidate.InUse)
				{
					_freeStates.RemoveAt(i);
					continue;
				}

				if (candidate.Sources == null || candidate.Sources.Length < clipCount)
				{
					continue;
				}

				state = candidate;
				_freeStates.RemoveAt(i);
				break;
			}

			if (state == null)
			{
				GameObject playbackObject = new GameObject("CriAtomExPlayback");
				playbackObject.transform.SetParent(_root.transform, false);
				playbackObject.AddComponent<CriAtomPlaybackCleaner>();
				state = new PlaybackState
				{
					GameObject = playbackObject,
					Sources = new AudioSource[0]
				};
			}

			state.GameObject.SetActive(true);
			if (state.Sources.Length < clipCount)
			{
				AudioSource[] grown = new AudioSource[clipCount];
				Array.Copy(state.Sources, grown, state.Sources.Length);
				for (int i = state.Sources.Length; i < clipCount; i++)
				{
					AudioSource source = state.GameObject.AddComponent<AudioSource>();
					source.playOnAwake = false;
					source.spatialBlend = 0f;
					grown[i] = source;
				}

				state.Sources = grown;
			}

			CriAtomPlaybackCleaner cleaner = state.GameObject.GetComponent<CriAtomPlaybackCleaner>();
			if (cleaner != null)
			{
				cleaner.Id = id;
			}

			return state;
		}

		private static void ReleasePlaybackState(PlaybackState state, uint id)
		{
			if (state == null)
			{
				return;
			}

			if (id != 0)
			{
				_playbacks.Remove(id);
			}

			state.InUse = false;
			state.Stopped = true;
			if (state.Sources != null)
			{
				for (int i = 0; i < state.Sources.Length; i++)
				{
					AudioSource source = state.Sources[i];
					if (source == null)
					{
						continue;
					}

					source.Stop();
					source.clip = null;
				}
			}

			if (state.GameObject != null)
			{
				state.GameObject.SetActive(false);
				if (!_freeStates.Contains(state))
				{
					_freeStates.Add(state);
				}
			}
		}

		public static CriAtomExPlayback.Status GetStatus(uint id)
		{
			if (id == 0 || !_playbacks.TryGetValue(id, out PlaybackState state))
			{
				return CriAtomExPlayback.Status.Stop;
			}

			if (state.Stopped)
			{
				_playbacks.Remove(id);
				return CriAtomExPlayback.Status.Stop;
			}

			if (state.PrimarySource == null || state.GameObject == null)
			{
				_playbacks.Remove(id);
				return CriAtomExPlayback.Status.PlayEnd;
			}

			if (state.Paused)
			{
				return CriAtomExPlayback.Status.Playing;
			}

			if (state.Sources != null)
			{
				for (int i = 0; i < state.Sources.Length; i++)
				{
					AudioSource source = state.Sources[i];
					if (source != null && source.isPlaying)
					{
						return CriAtomExPlayback.Status.Playing;
					}
				}
			}

			return CriAtomExPlayback.Status.PlayEnd;
		}

		public static long GetTimeMs(uint id)
		{
			if (id == 0 || !_playbacks.TryGetValue(id, out PlaybackState state) || state.PrimarySource == null)
			{
				return 0L;
			}

			return (long)(GetMonotonicPlaybackTimeSeconds(state) * 1000f);
		}

		public static void GetTimeAndScaleSyncedWithAudio(uint id, out long playbackTime, out float timeScale)
		{
			playbackTime = -1L;
			timeScale = 1f;
			if (id == 0 || !_playbacks.TryGetValue(id, out PlaybackState state) || state.PrimarySource == null)
			{
				return;
			}

			playbackTime = (long)(GetMonotonicPlaybackTimeSeconds(state) * 1000f);
			timeScale = state.PrimarySource.pitch;
			if (timeScale <= 0f)
			{
				timeScale = 1f;
			}
		}

		public static void Stop(uint id)
		{
			if (id == 0 || !_playbacks.TryGetValue(id, out PlaybackState state))
			{
				return;
			}

			state.Stopped = true;
			if (state.Sources != null)
			{
				for (int i = 0; i < state.Sources.Length; i++)
				{
					AudioSource source = state.Sources[i];
					if (source != null)
					{
						source.Stop();
					}
				}
			}
			ReleasePlaybackState(state, id);
		}

		public static void Pause(uint id)
		{
			if (id != 0 && _playbacks.TryGetValue(id, out PlaybackState state) && state.PrimarySource != null)
			{
				if (!state.Paused)
				{
					state.PausedTimeSeconds = GetMonotonicPlaybackTimeSeconds(state);
					state.Paused = true;
				}
				if (state.Sources != null)
				{
					for (int i = 0; i < state.Sources.Length; i++)
					{
						AudioSource source = state.Sources[i];
						if (source != null)
						{
							source.Pause();
						}
					}
				}
			}
		}

		public static void Resume(uint id)
		{
			if (id != 0 && _playbacks.TryGetValue(id, out PlaybackState state) && state.PrimarySource != null)
			{
				if (state.Paused)
				{
					state.BaseTimeSeconds = state.PausedTimeSeconds;
					state.BaseStopwatchTicks = Stopwatch.GetTimestamp();
					state.Paused = false;
				}
				if (state.Sources != null)
				{
					for (int i = 0; i < state.Sources.Length; i++)
					{
						AudioSource source = state.Sources[i];
						if (source != null)
						{
							source.UnPause();
						}
					}
				}
			}
		}

		public static void SetPitch(uint id, float pitch)
		{
			if (id == 0 || !_playbacks.TryGetValue(id, out PlaybackState state))
			{
				return;
			}

			if (state.Sources != null)
			{
				for (int i = 0; i < state.Sources.Length; i++)
				{
					AudioSource source = state.Sources[i];
					if (source != null)
					{
						source.pitch = Mathf.Max(0.1f, pitch);
					}
				}
			}
		}

		public static void SetVolume(uint id, float _volume)
		{
			if (id == 0 || !_playbacks.TryGetValue(id, out PlaybackState state))
			{
				return;
			}

			if (state.Sources != null)
			{
				for (int i = 0; i < state.Sources.Length; i++)
				{
					AudioSource source = state.Sources[i];
					if (source != null)
					{
						source.volume = Mathf.Max(0f, _volume);
					}
				}
			}
		}

		// OpenSekai: AudioSource.time and dspTime can advance in large chunks on some Android devices.
		// Use a process monotonic clock for visual sync while AudioSource still owns actual audio output.
		private static float GetMonotonicPlaybackTimeSeconds(PlaybackState state)
		{
			if (state == null || state.PrimarySource == null)
			{
				return 0f;
			}
			if (state.Paused)
			{
				return state.PausedTimeSeconds;
			}

			float pitch = state.PrimarySource.pitch;
			if (pitch <= 0f)
			{
				pitch = 1f;
			}

			double elapsedSeconds = (Stopwatch.GetTimestamp() - state.BaseStopwatchTicks) * StopwatchSecondsPerTick * pitch;
			float playbackTimeSeconds = state.BaseTimeSeconds + (float)Math.Max(0d, elapsedSeconds);
			AudioClip clip = state.PrimarySource.clip;
			if (clip != null && clip.length > 0f)
			{
				playbackTimeSeconds = Mathf.Min(playbackTimeSeconds, clip.length);
			}

			return Mathf.Max(0f, playbackTimeSeconds);
		}

		private static uint AllocatePlaybackId()
		{
			if (_nextPlaybackId == 0)
			{
				_nextPlaybackId = 1;
			}
			return _nextPlaybackId++;
		}

		private static void EnsureAudioListener()
		{
			EnsureInitialized();
			if (HasEnabledExternalAudioListener())
			{
				ReleaseFallbackAudioListener();
				return;
			}

			EnsureFallbackAudioListener();
		}

		private static void EnsureFallbackAudioListener()
		{
			CacheFallbackAudioListener();

			if (_fallbackAudioListener == null && _root != null)
			{
				_fallbackAudioListener = _root.AddComponent<AudioListener>();
			}

			if (_fallbackAudioListener != null)
			{
				_fallbackAudioListener.enabled = true;
			}
			EnsureAudioListenerGuard();
		}

		private static void CacheFallbackAudioListener()
		{
			if (_fallbackAudioListener == null && _root != null)
			{
				_fallbackAudioListener = _root.GetComponent<AudioListener>();
			}
		}

		private static void EnsureAudioListenerGuard()
		{
			if (_root != null && _root.GetComponent<CriAtomAudioListenerGuard>() == null)
			{
				_root.AddComponent<CriAtomAudioListenerGuard>();
			}
		}

		internal static void ReleaseFallbackAudioListenerIfExternalExists()
		{
			if (HasEnabledExternalAudioListener())
			{
				ReleaseFallbackAudioListener();
				return;
			}

			EnsureAudioListener();
		}

		private static bool HasEnabledExternalAudioListener()
		{
			AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
			for (int i = 0; i < listeners.Length; i++)
			{
				AudioListener listener = listeners[i];
				if (listener != null && listener != _fallbackAudioListener && listener.enabled && listener.gameObject.activeInHierarchy)
				{
					return true;
				}
			}

			return false;
		}

		private static void ReleaseFallbackAudioListener()
		{
			if (_fallbackAudioListener == null && _root != null)
			{
				_fallbackAudioListener = _root.GetComponent<AudioListener>();
			}

			if (_fallbackAudioListener != null)
			{
				UnityEngine.Object.Destroy(_fallbackAudioListener);
				_fallbackAudioListener = null;
			}
		}

		internal static void Forget(uint id)
		{
			if (id != 0)
			{
				_playbacks.Remove(id);
			}
		}

		internal static void DestroyIfPlaybackEnded(uint id)
		{
			if (id == 0 || !_playbacks.TryGetValue(id, out PlaybackState state))
			{
				return;
			}
			if (state.Stopped || state.Paused || state.GameObject == null)
			{
				return;
			}

			if (state.Sources != null)
			{
				for (int i = 0; i < state.Sources.Length; i++)
				{
					AudioSource source = state.Sources[i];
					if (source != null && source.isPlaying)
					{
						return;
					}
				}
			}

			ReleasePlaybackState(state, id);
		}
	}

	internal sealed class CriAtomPlaybackCleaner : MonoBehaviour
	{
		public uint Id { get; set; }

		private void Update()
		{
			CriAtomAudioRuntime.DestroyIfPlaybackEnded(Id);
		}

		private void OnDestroy()
		{
			CriAtomAudioRuntime.Forget(Id);
		}
	}

	internal sealed class CriAtomAudioListenerGuard : MonoBehaviour
	{
		private void LateUpdate()
		{
			CriAtomAudioRuntime.ReleaseFallbackAudioListenerIfExternalExists();
		}
	}
}