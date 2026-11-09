using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Sekai
{
	public class ApplicationLocalSettings
	{
		private const string STORAGE_FILE_NAME = "ApplicationLocalSettings.json";

		private static ApplicationLocalSettings _cachedStorage;

		public class VolumeSettings
		{
						private float _Bgm = 1f;
			public float Bgm
			{
			    get => _Bgm;
			    set => _Bgm = value;
			}
						private float _Se = 1f;
			public float Se
			{
			    get => _Se;
			    set => _Se = value;
			}
						private float _Voice = 1f;
			public float Voice
			{
			    get => _Voice;
			    set => _Voice = value;
			}
		}

				private VolumeSettings _SystemVolume = new VolumeSettings();
		public VolumeSettings SystemVolume
		{
		    get => _SystemVolume;
		    set => _SystemVolume = value;
		}
				private VolumeSettings _LiveVolume;
		public VolumeSettings LiveVolume
		{
		    get => _LiveVolume;
		    set => _LiveVolume = value;
		}
				private bool? _FullscreenEnabled;
		public bool? FullscreenEnabled
		{
		    get => _FullscreenEnabled;
		    set => _FullscreenEnabled = value;
		}

		public static ApplicationLocalSettings LoadFromStorage()
		{
			if (_cachedStorage != null)
			{
				return _cachedStorage;
			}

			ApplicationLocalSettings settings = null;
			string path = StoragePath;
			if (File.Exists(path))
			{
				try
				{
					settings = JsonConvert.DeserializeObject<ApplicationLocalSettings>(File.ReadAllText(path));
				}
				catch (Exception exception)
				{
					Debug.LogWarningFormat("ApplicationLocalSettings could not be loaded. path:{0} error:{1}", path, exception.Message);
				}
			}

			_cachedStorage = settings ?? new ApplicationLocalSettings();
			_cachedStorage.EnsureDefaults();
			return _cachedStorage;
		}

		public static void SaveToStorage(ApplicationLocalSettings settings)
		{
			_cachedStorage = settings ?? new ApplicationLocalSettings();
			if (_cachedStorage.LiveVolume == null)
			{
				_cachedStorage.LiveVolume = new VolumeSettings();
			}
			string path = StoragePath;
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				File.WriteAllText(path, JsonConvert.SerializeObject(_cachedStorage, Formatting.Indented));
			}
			catch (Exception exception)
			{
				Debug.LogWarningFormat("ApplicationLocalSettings could not be saved. path:{0} error:{1}", path, exception.Message);
			}
		}

		public VolumeSettings SetupLiveVolume()
		{
			LiveVolume = new VolumeSettings();
			return LiveVolume;
		}

		private static string StoragePath => Path.Combine(Application.persistentDataPath, STORAGE_FILE_NAME);

		private void EnsureDefaults()
		{
			SystemVolume ??= new VolumeSettings();
			LiveVolume ??= new VolumeSettings();
		}
	}
}
