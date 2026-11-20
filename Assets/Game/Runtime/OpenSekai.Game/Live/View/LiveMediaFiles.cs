using System.IO;
using OpenSekai.Shared.Packages;
using UnityEngine;

namespace Sekai.Live
{
	internal static class LiveMediaFiles
	{
		internal static string PrepareMoviePlaybackPath(string moviePath)
		{
#if UNITY_ANDROID
			return CopyMovieToAsciiCachePath(moviePath);
#else
			return moviePath;
#endif
		}

#if UNITY_ANDROID
		private static string CopyMovieToAsciiCachePath(string moviePath)
		{
			try
			{
				FileInfo sourceInfo = new FileInfo(moviePath);
				if (!sourceInfo.Exists)
				{
					return null;
				}

				string cacheDirectory = Path.Combine(Application.temporaryCachePath, "OpenSekai2DMV");
				Directory.CreateDirectory(cacheDirectory);
				string cachePath = Path.Combine(cacheDirectory, CreateAsciiMovieCacheFileName(sourceInfo));
				FileInfo cacheInfo = new FileInfo(cachePath);
				if (!cacheInfo.Exists || cacheInfo.Length != sourceInfo.Length || cacheInfo.LastWriteTimeUtc != sourceInfo.LastWriteTimeUtc)
				{
					File.Copy(sourceInfo.FullName, cachePath, true);
					File.SetLastWriteTimeUtc(cachePath, sourceInfo.LastWriteTimeUtc);
				}
				return cachePath;
			}
			catch (System.Exception exception)
			{
				Debug.LogWarningFormat("Failed to prepare 2DMV cache. path:{0} error:{1}", moviePath, exception.Message);
				return null;
			}
		}

		private static string CreateAsciiMovieCacheFileName(FileInfo sourceInfo)
		{
			string extension = NormalizeMovieExtension(sourceInfo.Extension);
			uint hash = 2166136261U;
			AddStringToHash(ref hash, sourceInfo.FullName);
			AddStringToHash(ref hash, sourceInfo.Length.ToString());
			AddStringToHash(ref hash, sourceInfo.LastWriteTimeUtc.Ticks.ToString());
			return "video_" + hash.ToString("x8") + extension;
		}

		private static string NormalizeMovieExtension(string extension)
		{
			if (string.IsNullOrEmpty(extension) || extension.Length > 8)
			{
				return ".mp4";
			}

			extension = extension.ToLowerInvariant();
			for (int i = 0; i < extension.Length; i++)
			{
				char c = extension[i];
				if (i == 0)
				{
					if (c != '.')
					{
						return ".mp4";
					}
				}
				else if ((c < '0' || c > '9') && (c < 'a' || c > 'z'))
				{
					return ".mp4";
				}
			}
			return extension;
		}

		private static void AddStringToHash(ref uint hash, string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return;
			}

			unchecked
			{
				for (int i = 0; i < value.Length; i++)
				{
					hash ^= value[i];
					hash *= 16777619U;
				}
			}
		}
#endif

		internal static Texture2D LoadCustomJacketTexture(FreeLiveBootData bootData)
		{
			if (bootData == null) return null;

			try
			{
				byte[] jacket = bootData.TemporaryJacketData;
				if (jacket == null && !string.IsNullOrEmpty(bootData.CustomMusicScorePath))
				{
					string path = SongPackageStorage.LoadEntry(bootData.CustomMusicScorePath)?.JacketPath;
					if (File.Exists(path)) jacket = File.ReadAllBytes(path);
				}
				if (jacket == null) return null;

				Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
				if (ImageConversion.LoadImage(texture, jacket)) return texture;
				Object.Destroy(texture);
				return null;
			}
			catch (System.Exception exception) when (exception is IOException || exception is System.UnauthorizedAccessException
				|| exception is System.ArgumentException)
			{
				return null;
			}
		}
	}
}
