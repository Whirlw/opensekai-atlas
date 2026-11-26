using System;
using CriWare;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using OpenSekai.Shared.Audio;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class SoundManager
	{
		public enum BundleWorkingStatus
		{
			None = 0,
			Loading = 1,
			Loaded = 2
		}

		public class AcbDataMemoryContainer
		{
		}

		private static SoundManager _instance;
		private readonly AudioCatalog _catalog = new AudioCatalog();
		private readonly ExternalSoundPack _externalSoundPack;
		private readonly VolumeService _volume = new VolumeService();

		private CriAtomExPlayer _soundEffectPlayer;
		private CriAtomExPlayer _ingameBgmPlayer;
		private CriAtomExAcb _packageAudio;
		private AudioClip _packageClip;
		private bool _ownsPackageClip;
		private PracticeAudioBank _practiceAudio;
		private CriAtomExPlayback _currentIngamePlayback = CriAtomExPlayback.Invalid;
		private uint _currentIngamePlaybackRequestId;
		private AudioSyncedUnityTimer _audioSyncedUnityTimer;
		public SoundManager()
		{
			_externalSoundPack = new ExternalSoundPack(_catalog);
		}

		public static SoundManager Instance => _instance ??= new SoundManager();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void BeginSession()
		{
			EndSession();
			Application.quitting -= EndSession;
			Application.quitting += EndSession;
		}

		private static void EndSession()
		{
			// The editor can retain statics after Play Mode has destroyed the catalog's AudioClips.
			_instance = null;
			CriAtomPlugin.FinalizeLibrary();
		}

		/// <summary>Refresh fallback ownership when a scene creates its own listener after loading.</summary>
		public static void NotifySceneAudioListenersChanged() =>
			CriAtomAudioRuntime.ReleaseFallbackAudioListenerIfExternalExists();

		/// <summary>
		/// Extra factor on the in-game music, set per song before the live starts: the 响度平均 gain,
		/// 1 when that setting is off.
		/// </summary>
		public float IngameBgmGain { get; set; } = 1f;

		public void SetupVolume(float master, float bgm, float se, float voice)
		{
			_volume.Set(master, bgm, se);
			_soundEffectPlayer?.SetVolume(_volume.SoundEffect);
			_ingameBgmPlayer?.SetVolume(_volume.Bgm * IngameBgmGain);
		}

		public bool IsLoadedSoundBundle(string bundleName) => _catalog.IsLoadedSoundBundle(bundleName);

		public void LoadSoundBundle(string bundleName, bool isResident) => _catalog.LoadSoundBundle(bundleName, isResident);

		public void Load(string cueSheetName, string acbFileName) => _catalog.Load(cueSheetName, acbFileName);

		public void LoadExternalSoundPack(Action onDone = null) => _externalSoundPack.Load(onDone);

		public void RegisterExternalAudioClip(string bundleName, string cueName, AudioClip clip) =>
			_externalSoundPack.RegisterClip(bundleName, cueName, clip);

		internal void RegisterPackageAudioClip(string cueName, AudioClip clip, bool ownsClip = true)
		{
			if (ReferenceEquals(_packageClip, clip)) return;
			ReleasePackageAudio();
			_packageClip = clip;
			_ownsPackageClip = ownsClip;
			_packageAudio = _externalSoundPack.RegisterClip(cueName, cueName, clip);
		}

		/// <summary>Ends the package's lifetime. Stopping playback alone keeps it ready for a retry.</summary>
		public void ReleasePackageAudio()
		{
			StopIngame();
			_practiceAudio?.Dispose();
			_practiceAudio = null;
			if (_packageAudio != null) _catalog.Unregister(_packageAudio);
			if (_ownsPackageClip && _packageClip != null) UnityEngine.Object.Destroy(_packageClip);
			_packageAudio = null;
			_packageClip = null;
			_ownsPackageClip = false;
		}

		public async UniTask<double> PreparePracticeAudioAsync(CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (_practiceAudio != null) return (double)_packageClip.samples / _packageClip.frequency;
			AudioClip original = _packageClip;
			var prepared = await PracticeAudioBank.PrepareAsync(original, token);
			if (_packageClip != original || original == null || token.IsCancellationRequested)
			{
				prepared.Dispose();
				token.ThrowIfCancellationRequested();
				throw new InvalidDataException("歌曲已更换，请重新开始练习。");
			}
			if (_practiceAudio == null) _practiceAudio = prepared;
			else prepared.Dispose();
            return (double)original.samples / original.frequency;
		}

		public bool ExistsCueName(string cueName) => _catalog.ExistsCueName(cueName);

		/// <summary>
		/// The live cues' pack sounds: the resource pack page's demo while one runs (no live runs then),
		/// otherwise the current live's.
		/// </summary>
		private static LiveMaterialAudio CurrentMaterialAudio
		{
			get
			{
				Sekai.Core.Live.LiveDemoController demo = Sekai.Core.Live.LiveDemoController.Current;
				return demo != null ? demo.MaterialAudio : UserDataManager.Instance.FreeLiveBootData?.MaterialAudio;
			}
		}

		internal bool ExistsLiveCue(string cueName) => CurrentMaterialAudio?.Contains(cueName) == true || ExistsCueName(cueName);

		public CriAtomEx.CueInfo[] GetCueInfos(string bundleName) => _catalog.GetCueInfos(bundleName);

		public SoundBundleBuildData GetSoundBundleBuildData(string bundleName) => AcbAssetSource.GetSoundBundleBuildData(bundleName);

		public bool IsAudioSyncedUnityTimerEnabled => _audioSyncedUnityTimer != null;

		public uint PrepareIngameBGM(string cueName, float startTime, Action callback = null, bool loop = false)
		{
			StopIngame();
			uint requestId = AllocateIngamePlaybackRequestId();
			float startTimeSeconds = Mathf.Max(0f, startTime);
			long startTimeMs = (long)(startTimeSeconds * 1000f);

			if (_catalog.TryResolveIngameCue(cueName, out CriAtomExAcb acb, out string resolvedCueName))
			{
				try
				{
					_ingameBgmPlayer = new CriAtomExPlayer();
					_ingameBgmPlayer.SetCue(acb, resolvedCueName);
					_ingameBgmPlayer.SetVolume(_volume.Bgm * IngameBgmGain);
					_ingameBgmPlayer.SetStartTime(startTimeMs);
					_currentIngamePlayback = _ingameBgmPlayer.Start();
					// OpenSekai: CRI PrepareCore waits in prepared playback until OnMusicStart resumes it.
					// Our AudioSource shim starts immediately, so hold it during MusicReady.
					_currentIngamePlayback.Pause();
					_audioSyncedUnityTimer = new AudioSyncedUnityTimer(_currentIngamePlayback);
					callback?.Invoke();
					return requestId;
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
				}
			}

			_currentIngamePlayback = CriAtomExPlayback.Invalid;
			_audioSyncedUnityTimer = null;
			if (!string.IsNullOrEmpty(cueName))
			{
				_catalog.LogMissingCue(cueName);
			}
			callback?.Invoke();
			return requestId;
		}

		public CriAtomExPlayback? GetPlayback(uint requestId)
		{
			if (requestId == 0 || requestId != _currentIngamePlaybackRequestId || _currentIngamePlayback.id == 0)
			{
				return null;
			}
			return _currentIngamePlayback;
		}

		public void ResumePreparedPlaybackIngame()
		{
			_currentIngamePlayback.Resume();
			ResetAudioSyncedUnityTimer();
		}

		public void ResumeIngame(long musicTimeMs)
		{
			_currentIngamePlayback.Resume();
			ResetAudioSyncedUnityTimer();
		}

		public void SetAudioSyncedUnityTimer(uint requestId)
		{
			if (requestId != 0 && requestId != _currentIngamePlaybackRequestId)
			{
				return;
			}
			ResetAudioSyncedUnityTimer();
		}

		public long GetAudioSyncedUnityTimer()
		{
			if (_audioSyncedUnityTimer == null)
			{
				return 0L;
			}

			if (_currentIngamePlayback.id != 0)
			{
				CriAtomExPlayback.Status status = _currentIngamePlayback.GetStatus();
				if (status == CriAtomExPlayback.Status.Playing)
				{
					_audioSyncedUnityTimer.Execute(Time.time);
					return _audioSyncedUnityTimer.PlaybackTime;
				}
				if (status == CriAtomExPlayback.Status.PlayEnd || status == CriAtomExPlayback.Status.Stop || status == CriAtomExPlayback.Status.Error)
				{
					return 0L;
				}
				return _audioSyncedUnityTimer.PlaybackTime;
			}

			return 0L;
		}

		public bool TryGetAudioSyncedUnityTimer(out long playbackTime)
		{
			playbackTime = 0L;
			if (_audioSyncedUnityTimer == null)
			{
				return false;
			}

			_audioSyncedUnityTimer.Execute(Time.time);
			playbackTime = _audioSyncedUnityTimer.PlaybackTime;
			return true;
		}

		private void ResetAudioSyncedUnityTimer()
		{
			_audioSyncedUnityTimer = _currentIngamePlayback.id != 0 ? new AudioSyncedUnityTimer(_currentIngamePlayback) : null;
		}

		public void StopIngame()
		{
			_currentIngamePlayback.Stop();
			_ingameBgmPlayer?.Dispose();
			_ingameBgmPlayer = null;
			_currentIngamePlayback = CriAtomExPlayback.Invalid;
			_audioSyncedUnityTimer = null;
		}

		public void StopAll()
		{
			CurrentMaterialAudio?.Stop();
			StopIngame();
			_soundEffectPlayer?.Stop();
		}

		public uint PlaySE(string cueName)
		{
			return PlaySE(cueName, 1f, 0f, 1);
		}

		public uint PlaySE(string cueName, float vol)
		{
			return PlaySE(cueName, vol, 0f, 1);
		}

		public uint PlaySE(string cueName, float volume = 1f, float startTime = 0f, int playerIndex = 1)
		{
			return PlaySEInternal(cueName, volume, startTime);
		}

		public void PlaySEOneShot(string cueName, int priority = 0)
		{
			PlaySEInternal(cueName, 1f, 0f);
		}

		public uint PlayIngameSE(string cueName)
		{
			return PlayLiveCue(cueName);
		}

		internal uint PlayHoldLoop(string cueName) => PlayLiveCue(cueName, loop: true);

		private uint PlayLiveCue(string cueName, int sequence = 0, bool loop = false)
		{
			// The demo is quiet until its sound is switched on.
			Sekai.Core.Live.LiveDemoController demo = Sekai.Core.Live.LiveDemoController.Current;
			if (demo != null && !demo.Audible) return 0;
			var audio = CurrentMaterialAudio;
			if (audio != null && audio.TryPlay(cueName, loop, _volume.SoundEffect, sequence, out uint playback))
				return playback;
			return PlaySEInternal(cueName, 1f, 0f, sequence, loop);
		}

		public void PlayIngameSEOneShot(string cueName)
		{
			PlayLiveCue(cueName);
		}

		internal void PlayIngameSEOneShot(string cueName, int requestSequence)
		{
			PlayLiveCue(cueName, requestSequence);
		}

		public void PlayIngameVoiceOneShot(string cueName)
		{
			PlayLiveCue(cueName);
		}

		public void StopSE(uint playbackId)
		{
			if (playbackId == 0)
			{
				return;
			}
			new CriAtomExPlayback(playbackId).Stop();
		}

		public void ResumeIngameSe()
		{
		}

		private uint PlaySEInternal(string cueName, float volume, float startTime, int requestSequence = 0, bool loop = false)
		{
			string resolvedCueName = _catalog.ResolveCueName(cueName, out CriAtomExAcb acb);
			if (string.IsNullOrEmpty(resolvedCueName) || acb == null)
			{
				_catalog.LogMissingCue(cueName);
				return 0;
			}

			try
			{
				CriAtomExPlayer player = GetSoundEffectPlayer();
				player.SetCue(acb, resolvedCueName);
				player.SetVolume(_volume.SoundEffect * Mathf.Clamp01(volume));
				if (startTime > 0f)
				{
					player.SetStartTime((long)(startTime * 1000f));
				}
				CriAtomExPlayback _playback = player.Start(requestSequence, loop);
				return _playback.id;
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				return 0;
			}
		}

		private CriAtomExPlayer GetSoundEffectPlayer()
		{
			AudioCatalog.EnsureAtomInitialized();
			if (_soundEffectPlayer == null)
			{
				_soundEffectPlayer = new CriAtomExPlayer();
				_soundEffectPlayer.SetVolume(_volume.SoundEffect);
			}
			return _soundEffectPlayer;
		}

		private uint AllocateIngamePlaybackRequestId()
		{
			_currentIngamePlaybackRequestId++;
			if (_currentIngamePlaybackRequestId == 0)
			{
				_currentIngamePlaybackRequestId = 1;
			}
			return _currentIngamePlaybackRequestId;
		}

		public void Pause()
		{
			_currentIngamePlayback.Pause();
		}

		public void Resume()
		{
			_currentIngamePlayback.Resume();
			ResetAudioSyncedUnityTimer();
		}

		public void SetIngameBgmPitch(float pitch)
		{
			if (_currentIngamePlayback.id != 0)
			{
				if (_practiceAudio == null) CriAtomAudioRuntime.SetPitch(_currentIngamePlayback.id, pitch);
				else CriAtomAudioRuntime.SetPracticePitch(_currentIngamePlayback.id, pitch, _practiceAudio);
			}
		}

		internal double ScheduleIngameEnd(double playbackSeconds) =>
			CriAtomAudioRuntime.ScheduleEnd(_currentIngamePlayback.id, playbackSeconds);

		public void SetIngameBgmVolume(float volume)
		{
			if (_currentIngamePlayback.id != 0)
			{
				CriAtomAudioRuntime.SetVolume(_currentIngamePlayback.id, volume);
			}
		}

		/// <summary>
		/// Where the sound of an in-game BGM cue starts and stops, in seconds, leaving out the silence at
		/// either end. False when the cue has no clip or its samples cannot be read.
		/// </summary>
		public bool TryGetIngameAudibleRange(string cueName, out float startSeconds, out float endSeconds)
		{
			startSeconds = 0f;
			endSeconds = 0f;
			if (!_catalog.TryResolveIngameCue(cueName, out CriAtomExAcb acb, out string resolvedCueName) || acb == null)
			{
				return false;
			}

			CriAtomExAcb.CueAudioClip[] clips = acb.GetOrCreateAudioClips(resolvedCueName);
			return clips.Length > 0 && AudibleRange.TryFind(clips[0].Clip, out startSeconds, out endSeconds);
		}

		/// <summary>The in-game BGM's status; Stop when there is none.</summary>
		public CriAtomExPlayback.Status GetIngameStatus()
		{
			return _currentIngamePlayback.id != 0 ? _currentIngamePlayback.GetStatus() : CriAtomExPlayback.Status.Stop;
		}

		/// <summary>Moves the in-game BGM and its synced clock to a music time, paused or playing.</summary>
		public void SeekIngame(long musicTimeMs)
		{
			if (_currentIngamePlayback.id == 0)
			{
				return;
			}

			CriAtomAudioRuntime.Seek(_currentIngamePlayback.id, musicTimeMs / 1000f);
			ResetAudioSyncedUnityTimer();
		}

		/// <summary>Writes the in-game BGM's clock and audio positions to the Audio trace.</summary>
		public void TraceIngameSync(string reason)
		{
			CriAtomAudioRuntime.TraceSync(_currentIngamePlayback.id, reason);
		}

		public uint GetCurrentIngamePlaybackId()
		{
			return _currentIngamePlayback.id;
		}
	}

	internal sealed class AudioSyncedUnityTimer
	{
		private readonly CriAtomExPlayback _playback;

		public long PlaybackTime { get; private set; } = -1L;

		public long DebugAudioSyncedTime { get; private set; } = -1L;

		public AudioSyncedUnityTimer(CriAtomExPlayback _playback)
		{
			this._playback = _playback;
		}

		public void Execute(float unityTime)
		{
			_playback.GetTimeAndScaleSyncedWithAudio(out long audioTimeMs, out float timeScale);
			DebugAudioSyncedTime = audioTimeMs;
			PlaybackTime = audioTimeMs;
		}
	}
}
