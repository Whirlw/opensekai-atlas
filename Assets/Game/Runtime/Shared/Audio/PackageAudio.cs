using System;
using System.Collections.Generic;
using Sekai;
using Sekai.Live;
using System.IO;
using System.Threading;
using CP;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using OpenSekai.Shared.Packages;
namespace OpenSekai.Shared.Audio
{
    public sealed class PackageAudio
    {
        private AudioClip _audioClip;
        public SongPackage Package { get; }
        public PackageAudio(SongPackage package) { Package = package ?? throw new ArgumentNullException(nameof(package)); }
		public async UniTask<bool> RegisterAudioAsync(CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (_audioClip == null)
			{
				_audioClip = await LoadAudioClipAsync(token);
			}

			if (_audioClip == null)
			{
				return false;
			}

			Package.AudioLengthMs = (long)(_audioClip.length * 1000f);
			SoundManager.Instance.RegisterExternalAudioClip(Package.AudioCueName, Package.AudioCueName, _audioClip);
			return true;
		}

		public float[] GetAudioSamples(int sampleCount)
		{
			if (_audioClip == null || sampleCount <= 0)
			{
				return null;
			}

			float[] samples = new float[sampleCount];
			float[] channelData = new float[_audioClip.samples * _audioClip.channels];
			if (!_audioClip.GetData(channelData, 0))
			{
				return null;
			}

			int samplesPerPixel = channelData.Length / sampleCount;
			if (samplesPerPixel < 1)
			{
				samplesPerPixel = 1;
			}

			for (int i = 0; i < sampleCount; i++)
			{
				int startIndex = i * samplesPerPixel;
				float maxAmplitude = 0f;
				int endIndex = Mathf.Min(startIndex + samplesPerPixel, channelData.Length);
				for (int j = startIndex; j < endIndex; j++)
				{
					float absValue = Mathf.Abs(channelData[j]);
					if (absValue > maxAmplitude)
					{
						maxAmplitude = absValue;
					}
				}
				samples[i] = maxAmplitude;
			}

			return samples;
		}

		public AudioClip GetAudioClip()
		{
			return _audioClip;
		}

		private async UniTask<AudioClip> LoadAudioClipAsync(CancellationToken token)
		{
			if (!File.Exists(Package.AudioPath))
			{
				LogUtility.LogWarning("Custom music audio not found. path:{0}", Package.AudioPath);
				return null;
			}

			AudioType audioType = ResolveAudioType(Package.AudioPath);
			if (audioType == AudioType.UNKNOWN)
			{
				LogUtility.LogWarning("Unsupported custom music audio format. path:{0}", Package.AudioPath);
				return null;
			}

			using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(ToFileUri(Package.AudioPath), audioType))
			{
				UnityWebRequestAsyncOperation operation = request.SendWebRequest();
				await UniTask.WaitUntil(() => operation.isDone, cancellationToken: token);
				token.ThrowIfCancellationRequested();

				if (request.result != UnityWebRequest.Result.Success)
				{
					LogUtility.LogWarning("Failed to load custom music audio. path:{0} error:{1}", Package.AudioPath, request.error);
					return null;
				}

				AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
				if (clip != null)
				{
					clip.name = Package.AudioCueName;
				}
				return clip;
			}
		}


		private static string ToFileUri(string path)
		{
			return new Uri(Path.GetFullPath(path)).AbsoluteUri;
		}
		private static AudioType ResolveAudioType(string path)
		{
			string extension = Path.GetExtension(path)?.ToLowerInvariant();
			switch (extension)
			{
				case ".ogg":
					return AudioType.OGGVORBIS;
				case ".mp3":
					return AudioType.MPEG;
				case ".wav":
					return AudioType.WAV;
				default:
					return AudioType.UNKNOWN;
			}
		}
    }
}
