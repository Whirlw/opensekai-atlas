using System;
using CriWare;
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

		private static readonly SoundManager _instance = new SoundManager();
		private readonly AudioCatalog _catalog = new AudioCatalog();
		private readonly ExternalSoundPack _externalSoundPack;
		private readonly VolumeService _volume = new VolumeService();

		private CriAtomExPlayer _soundEffectPlayer;
		private CriAtomExPlayer _ingameBgmPlayer;
		private CriAtomExPlayback _currentIngamePlayback = CriAtomExPlayback.Invalid;
		private uint _currentIngamePlaybackRequestId;
		private AudioSyncedUnityTimer _audioSyncedUnityTimer;
		public SoundManager()
		{
			_externalSoundPack = new ExternalSoundPack(_catalog);
		}

		public static SoundManager Instance => _instance;

		public void SetupVolume(float master, float bgm, float se, float voice)
		{
			_volume.Set(master, bgm, se);
			_soundEffectPlayer?.SetVolume(_volume.SoundEffect);
			_ingameBgmPlayer?.SetVolume(_volume.Bgm);
		}

		public bool IsLoadedSoundBundle(string bundleName) => _catalog.IsLoadedSoundBundle(bundleName);

		public void LoadSoundBundle(string bundleName, bool isResident) => _catalog.LoadSoundBundle(bundleName, isResident);

		public void Load(string cueSheetName, string acbFileName) => _catalog.Load(cueSheetName, acbFileName);

		public void LoadExternalSoundPack(Action onDone = null) => _externalSoundPack.Load(onDone);

		public void RegisterExternalAudioClip(string bundleName, string cueName, AudioClip clip) =>
			_externalSoundPack.RegisterClip(bundleName, cueName, clip);

		public bool ExistsCueName(string cueName) => _catalog.ExistsCueName(cueName);

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
					_ingameBgmPlayer.SetVolume(_volume.Bgm);
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
			return PlaySEInternal(cueName, 1f, 0f);
		}

		public void PlayIngameSEOneShot(string cueName)
		{
			PlaySEInternal(cueName, 1f, 0f);
		}

		public void PlayIngameVoiceOneShot(string cueName)
		{
			PlaySEInternal(cueName, 1f, 0f);
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

		private uint PlaySEInternal(string cueName, float volume, float startTime)
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
				CriAtomExPlayback _playback = player.Start();
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
				CriAtomAudioRuntime.SetPitch(_currentIngamePlayback.id, pitch);
			}
		}

		public void SetIngameBgmVolume(float volume)
		{
			if (_currentIngamePlayback.id != 0)
			{
				CriAtomAudioRuntime.SetVolume(_currentIngamePlayback.id, volume);
			}
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
