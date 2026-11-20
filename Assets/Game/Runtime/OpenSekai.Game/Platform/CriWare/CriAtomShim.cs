using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.IO;
using AcbDecoder;
using UnityEngine;
using UnityEngine.Networking;
using OpenSekai.Shared.Diagnostics;
using OpenSekai.Shared.Live.Simulation;
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

		public CriAtomExPlayback Start() => Start(0);

		internal CriAtomExPlayback Start(int requestSequence, bool loop = false)
		{
			if (_acb == null || string.IsNullOrEmpty(_cueName))
			{
				_currentPlayback = CriAtomExPlayback.Invalid;
				return _currentPlayback;
			}

			CriAtomExAcb.CueAudioClip[] clips = _acb.GetOrCreateAudioClips(_cueName);
			_currentPlayback = CriAtomAudioRuntime.Play(clips, _volume, _startTimeMs / 1000f, requestSequence, loop);
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
						private GameObject _gameObject;
			public GameObject GameObject
			{
			    get => _gameObject;
			    set => _gameObject = value;
			}
						private AudioSource _primarySource;
			public AudioSource PrimarySource
			{
			    get => _primarySource;
			    set => _primarySource = value;
			}
						private AudioSource[] _sources;
			public AudioSource[] Sources
			{
			    get => _sources;
			    set => _sources = value;
			}
						private bool _stopped;
			public bool Stopped
			{
			    get => _stopped;
			    set => _stopped = value;
			}
						private float _baseTimeSeconds;
			public float BaseTimeSeconds
			{
			    get => _baseTimeSeconds;
			    set => _baseTimeSeconds = value;
			}
						private long _baseStopwatchTicks;
			public long BaseStopwatchTicks
			{
			    get => _baseStopwatchTicks;
			    set => _baseStopwatchTicks = value;
			}
						private float _pausedTimeSeconds;
			public float PausedTimeSeconds
			{
			    get => _pausedTimeSeconds;
			    set => _pausedTimeSeconds = value;
			}
						private bool _paused;
						private bool _inUse;
						public bool InUse { get => _inUse; set => _inUse = value; }
			public bool Paused
			{
			    get => _paused;
			    set => _paused = value;
			}

			// Drift guard: the last timeSamples seen on the primary source, the clock-minus-audio
			// readings taken each time it moved, and the rate a slew is running the clock at. See
			// ReconcileWithAudio.
			public int LastObservedSamples = -1;
			public readonly AudioClockDrift Drift = new AudioClockDrift();
			public float RateScale = 1f;

			// Set while the app itself is paused (backgrounded, screen off). Independent of Paused,
			// which is the game's own pause: the OS stops the audio whether or not the game asked.
			public bool Suspended;
			public float SuspendedTimeSeconds;
			public AudioMixProbe MixProbe;
		}

		// On suspend, how far the clock has to be past the audio before the audio's own position is
		// trusted as where the music stopped. AudioSource.time is reported in chunks on some Android
		// devices, so this sits well above a chunk.
		private const float SUSPEND_FREEZE_THRESHOLD_SECONDS = 0.15f;

		private static readonly Dictionary<uint, PlaybackState> _playbacks = new Dictionary<uint, PlaybackState>();
		// Playing a sound used to build a GameObject and an AudioSource per hit, which costs setup time
		// on the audio thread and a garbage spike every note. Finished playbacks are parked here instead.
		private static readonly List<PlaybackState> _freeStates = new List<PlaybackState>();
		private static readonly List<PlaybackState> _freeProbeStates = new List<PlaybackState>();
		private static readonly double _stopwatchSecondsPerTick = 1d / Stopwatch.Frequency;
		private static GameObject _root;
		private static AudioListener _fallbackAudioListener;
		private static AudioListener[] _sceneListeners = Array.Empty<AudioListener>();
		private static uint _nextPlaybackId = 1;

		public static void EnsureInitialized()
		{
			if (_root == null)
			{
				_root = new GameObject("CriAtomAudioRuntime");
				UnityEngine.Object.DontDestroyOnLoad(_root);
				_root.AddComponent<CriAtomAudioListenerGuard>();
			}
			if (_freeStates.Count == 0)
			{
				// Warm one playback so the first hit of a song does not pay for creating a source.
				ReleasePlaybackState(RentPlaybackState(2, 0), 0);
			}

			UpdateAudioListener();
		}

		public static CriAtomExPlayback Play(AudioClip clip, float _volume, float startTimeSeconds)
		{
			if (clip == null)
			{
				return CriAtomExPlayback.Invalid;
			}

			return Play(new[] { new CriAtomExAcb.CueAudioClip(clip, 1f) }, _volume, startTimeSeconds);
		}

		public static CriAtomExPlayback Play(CriAtomExAcb.CueAudioClip[] clips, float _volume, float startTimeSeconds,
			int requestSequence = 0, bool loop = false)
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

			AudioMixProbe.Ticket mixTicket = requestSequence != 0 && clipCount == 1
				? LatencyCapture.ReserveMixProbe(requestSequence) : null;
			PlaybackState state = RentPlaybackState(clipCount, id, mixTicket != null);
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
				source.loop = loop;
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
			state.Suspended = false;
			ResetDriftWindow(state);
			_playbacks[id] = state;

			state.BaseStopwatchTicks = Stopwatch.GetTimestamp();
			if (requestSequence != 0)
				LatencyCapture.Record("note_se_play", value: id, requestSequence: requestSequence);
			if (mixTicket != null)
				state.MixProbe.Arm(mixTicket);
			for (int i = 0; i < sourceIndex; i++)
			{
				sources[i].Play();
			}

			return new CriAtomExPlayback(id);
		}

		private static PlaybackState RentPlaybackState(int clipCount, uint id, bool probeMix = false)
		{
			List<PlaybackState> pool = probeMix ? _freeProbeStates : _freeStates;
			PlaybackState state = null;
			for (int i = pool.Count - 1; i >= 0; i--)
			{
				PlaybackState candidate = pool[i];
				if (candidate.GameObject == null || candidate.InUse)
				{
					pool.RemoveAt(i);
					continue;
				}

				if (candidate.Sources == null || candidate.Sources.Length < clipCount)
				{
					continue;
				}

				state = candidate;
				pool.RemoveAt(i);
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
			if (probeMix && state.MixProbe == null)
				state.MixProbe = state.GameObject.AddComponent<AudioMixProbe>();

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
			if (state.MixProbe != null)
				state.MixProbe.Disarm();
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
				List<PlaybackState> pool = state.MixProbe != null ? _freeProbeStates : _freeStates;
				if (!pool.Contains(state))
				{
					pool.Add(state);
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

			ReconcileWithAudio(state, id);
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
					TraceSync(state, id, "pause");
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
					// The OS can tear down and rebuild the audio output while the app is in the
					// background, and the sources may come back somewhere other than where they were
					// paused. The clock is the reference here: put the audio back under it before it
					// plays again, so a trip to the background cannot leave the two seconds apart.
					TraceSync(state, id, "resume-before-seek");
					SeekSources(state, state.PausedTimeSeconds);
					state.BaseTimeSeconds = state.PausedTimeSeconds;
					state.BaseStopwatchTicks = Stopwatch.GetTimestamp();
					state.Paused = false;
					ResetDriftWindow(state);
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
			if (state.Suspended)
			{
				return state.SuspendedTimeSeconds;
			}

			float pitch = state.PrimarySource.pitch;
			if (pitch <= 0f)
			{
				pitch = 1f;
			}

			// A base in the future holds the clock still until then (see ApplyDriftCorrection).
			double elapsedSeconds = (Stopwatch.GetTimestamp() - state.BaseStopwatchTicks) * _stopwatchSecondsPerTick * pitch * state.RateScale;
			float playbackTimeSeconds = state.BaseTimeSeconds + (float)Math.Max(0d, elapsedSeconds);
			AudioClip clip = state.PrimarySource.clip;
			if (clip != null && clip.length > 0f)
			{
				playbackTimeSeconds = Mathf.Min(playbackTimeSeconds, clip.length);
			}

			return Mathf.Max(0f, playbackTimeSeconds);
		}

		private static void SeekSources(PlaybackState state, float timeSeconds)
		{
			if (state.Sources == null)
			{
				return;
			}

			for (int i = 0; i < state.Sources.Length; i++)
			{
				AudioSource source = state.Sources[i];
				AudioClip clip = source != null ? source.clip : null;
				if (clip == null || clip.frequency <= 0 || clip.samples <= 0)
				{
					continue;
				}

				int samples = (int)Math.Round((double)timeSeconds * clip.frequency);
				source.timeSamples = Mathf.Clamp(samples, 0, clip.samples - 1);
			}
		}

		private static void ResetDriftWindow(PlaybackState state)
		{
			// Every caller has just put the clock's base somewhere new, so the slew is dropped with it.
			state.LastObservedSamples = -1;
			state.Drift.Reset();
			state.RateScale = 1f;
		}

		// Closes the loop the monotonic clock leaves open. Each time the primary source reports a new
		// position, the clock-minus-audio reading goes into AudioClockDrift; a full window decides
		// whether to leave the clock alone, slew it, jump it forward, or hold it until the audio catches
		// up. The clock never steps back. See AudioClockDrift for the numbers.
		private static void ReconcileWithAudio(PlaybackState state, uint id)
		{
			AudioSource source = state.PrimarySource;
			if (state.Paused || state.Suspended || state.Stopped || source == null || !source.isPlaying)
			{
				return;
			}

			AudioClip clip = source.clip;
			if (clip == null || clip.frequency <= 0)
			{
				return;
			}

			int samples = source.timeSamples;
			if (samples == state.LastObservedSamples)
			{
				return;
			}

			bool wentBack = state.LastObservedSamples >= 0 && samples < state.LastObservedSamples;
			state.LastObservedSamples = samples;
			if (wentBack)
			{
				// A loop or a seek: what came before says nothing about now.
				state.Drift.Reset();
				return;
			}

			long now = Stopwatch.GetTimestamp();
			if (now < state.BaseStopwatchTicks)
			{
				// Holding for the audio: the clock stands still on purpose, so its readings would only
				// say how long is left. The window starts over once the hold is done.
				state.Drift.Reset();
				return;
			}

			float audioSeconds = (float)samples / clip.frequency;
			float clockSeconds = GetMonotonicPlaybackTimeSeconds(state);
			if (LatencyCapture.Active)
				LatencyCapture.Record("audio_sample", noteId: (int)id, song: clockSeconds, note: audioSeconds,
					value: (clockSeconds - audioSeconds) * 1000d);
			if (clip.length > 0f && clockSeconds >= clip.length)
			{
				// The clock is clamped at the end of the clip, so the offset there is meaningless.
				return;
			}

			if (state.Drift.Add(clockSeconds - audioSeconds, out AudioClockCorrection correction))
			{
				ApplyDriftCorrection(state, id, now, clockSeconds, audioSeconds, correction);
			}
		}

		private static void ApplyDriftCorrection(PlaybackState state, uint id, long now, float clockSeconds, float audioSeconds,
			AudioClockCorrection correction)
		{
			float pitch = state.PrimarySource.pitch;
			if (pitch <= 0f)
			{
				pitch = 1f;
			}

			switch (correction.Kind)
			{
				case AudioClockCorrectionKind.None:
				case AudioClockCorrectionKind.Slew:
					if (state.RateScale == correction.RateScale)
					{
						return;
					}

					// Rebase at the current reading so the new rate only applies from now on.
					state.BaseTimeSeconds = clockSeconds;
					state.BaseStopwatchTicks = now;
					state.RateScale = correction.RateScale;
					break;

				case AudioClockCorrectionKind.JumpForward:
					state.BaseTimeSeconds = clockSeconds + correction.JumpSeconds;
					state.BaseStopwatchTicks = now;
					state.RateScale = 1f;
					break;

				case AudioClockCorrectionKind.Wait:
					// A base in the future: the clock reads BaseTimeSeconds until the stopwatch gets there.
					state.BaseTimeSeconds = clockSeconds;
					state.BaseStopwatchTicks = now + (long)(correction.WaitSeconds / pitch / _stopwatchSecondsPerTick);
					state.RateScale = 1f;
					break;
			}

			if (LatencyCapture.Active)
				LatencyCapture.Record("audio_correction", noteId: (int)id, song: clockSeconds, note: audioSeconds,
					value: correction.ErrorSeconds * 1000d, detail: correction.Kind.ToString());
			if (LiveTrace.IsOn(LiveTraceCategory.Audio))
			{
				LiveTrace.Write(LiveTraceCategory.Audio, string.Concat("drift ", correction.Kind.ToString(),
					" id=", id.ToString(),
					" clock=", clockSeconds.ToString("F3"), " audio=", audioSeconds.ToString("F3"),
					" err=", (correction.ErrorSeconds * 1000f).ToString("F1"), "ms",
					" spread=", (correction.SpreadSeconds * 1000f).ToString("F1"), "ms",
					" rate=", state.RateScale.ToString("F4")));
			}
		}

		// The app going to the background stops the audio output, but the game only pauses itself once
		// the rhythm game is under way: in the lead-in after the music starts, or with the game already
		// over, nothing paused the clock and it ran on for as long as the app was away. Freeze every
		// running playback here instead, whatever the game is doing, and on return put the audio back
		// under the clock. A game pause taken meanwhile wins: it keeps the frozen time and its own
		// Resume does the seek.
		internal static void OnSystemSuspend(bool suspended)
		{
			foreach (KeyValuePair<uint, PlaybackState> entry in _playbacks)
			{
				PlaybackState state = entry.Value;
				if (state == null || state.Stopped || state.PrimarySource == null)
				{
					continue;
				}

				if (suspended)
				{
					if (!state.Paused && !state.Suspended)
					{
						TraceSync(state, entry.Key, "suspend");
						// Losing focus can stop the audio output a good while before the app pause
						// arrives (0.9 s on a test device). Freezing at the clock would then skip that
						// much music on return; freeze where the audio actually stopped instead.
						float clock = GetMonotonicPlaybackTimeSeconds(state);
						float audio = state.PrimarySource.time;
						state.SuspendedTimeSeconds = clock - audio > SUSPEND_FREEZE_THRESHOLD_SECONDS && audio > 0f
							? audio
							: clock;
						state.Suspended = true;
					}
					continue;
				}

				if (!state.Suspended)
				{
					continue;
				}

				// Traced while still suspended, so the clock it shows is the frozen one.
				if (!state.Paused)
				{
					TraceSync(state, entry.Key, "unsuspend-before-seek");
				}
				state.Suspended = false;
				if (!state.Paused)
				{
					SeekSources(state, state.SuspendedTimeSeconds);
					state.BaseTimeSeconds = state.SuspendedTimeSeconds;
					state.BaseStopwatchTicks = Stopwatch.GetTimestamp();
					ResetDriftWindow(state);
				}
			}
		}

		/// <summary>Moves a playback, its sources and its clock together, paused or not.</summary>
		internal static void Seek(uint id, float timeSeconds)
		{
			if (id == 0 || !_playbacks.TryGetValue(id, out PlaybackState state) || state.PrimarySource == null)
			{
				return;
			}

			timeSeconds = Mathf.Max(0f, timeSeconds);
			SeekSources(state, timeSeconds);
			state.PausedTimeSeconds = timeSeconds;
			state.SuspendedTimeSeconds = timeSeconds;
			state.BaseTimeSeconds = timeSeconds;
			state.BaseStopwatchTicks = Stopwatch.GetTimestamp();
			ResetDriftWindow(state);
			TraceSync(state, id, "seek");
		}

		internal static void TraceSync(uint id, string reason)
		{
			if (id != 0 && _playbacks.TryGetValue(id, out PlaybackState state))
			{
				TraceSync(state, id, reason);
			}
		}

		private static void TraceSync(PlaybackState state, uint id, string reason)
		{
			if (!LiveTrace.IsOn(LiveTraceCategory.Audio) || state.PrimarySource == null)
			{
				return;
			}

			AudioSource source = state.PrimarySource;
			float clock = GetMonotonicPlaybackTimeSeconds(state);
			float audio = source.time;
			LiveTrace.Write(LiveTraceCategory.Audio, string.Concat("sync ", reason, " id=", id.ToString(),
				" clock=", clock.ToString("F3"), " audio=", audio.ToString("F3"),
				" diff=", ((clock - audio) * 1000f).ToString("F0"), "ms paused=", state.Paused ? "1" : "0",
				" playing=", source.isPlaying ? "1" : "0"));
		}

		private static uint AllocatePlaybackId()
		{
			if (_nextPlaybackId == 0)
			{
				_nextPlaybackId = 1;
			}
			return _nextPlaybackId++;
		}

		internal static void RefreshSceneListeners()
		{
			_sceneListeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		}

		internal static void ReleaseFallbackAudioListenerIfExternalExists()
		{
			RefreshSceneListeners();
			if (_root != null) UpdateAudioListener();
		}

		internal static void UpdateAudioListener()
		{
			foreach (var listener in _sceneListeners)
			{
				if (listener != null && listener != _fallbackAudioListener && listener.isActiveAndEnabled)
				{
					if (_fallbackAudioListener != null) _fallbackAudioListener.enabled = false;
					return;
				}
			}
			if (_fallbackAudioListener == null) _fallbackAudioListener = _root.AddComponent<AudioListener>();
			if (!_fallbackAudioListener.enabled) _fallbackAudioListener.enabled = true;
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
		private void OnEnable()
		{
			UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
			CriAtomAudioRuntime.RefreshSceneListeners();
		}
		private void OnDisable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
		private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
			=> CriAtomAudioRuntime.RefreshSceneListeners();

		private void LateUpdate()
		{
			CriAtomAudioRuntime.UpdateAudioListener();
		}

		private void OnApplicationPause(bool pauseStatus)
		{
			CriAtomAudioRuntime.OnSystemSuspend(pauseStatus);
		}
	}
}
