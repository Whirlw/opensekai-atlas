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
						private float _bgm = 1f;
			public float Bgm
			{
			    get => _bgm;
			    set => _bgm = value;
			}
						private float _se = 1f;
			public float Se
			{
			    get => _se;
			    set => _se = value;
			}
						private float _voice = 1f;
			public float Voice
			{
			    get => _voice;
			    set => _voice = value;
			}
		}

				private VolumeSettings _systemVolume = new VolumeSettings();
		public VolumeSettings SystemVolume
		{
		    get => _systemVolume;
		    set => _systemVolume = value;
		}
				private VolumeSettings _liveVolume;
		public VolumeSettings LiveVolume
		{
		    get => _liveVolume;
		    set => _liveVolume = value;
		}
		/// <summary>Volume of the menu pages' interface sounds, apart from music and hit sounds.</summary>
		private float _uiVolume = 0.6f;
		public float UiVolume
		{
		    get => _uiVolume;
		    set => _uiVolume = value;
		}
		/// <summary>响度平均: scale every song's music, preview and live, by its measured loudness.</summary>
		private bool _loudnessNormalization;
		public bool LoudnessNormalization
		{
		    get => _loudnessNormalization;
		    set => _loudnessNormalization = value;
		}
				private bool? _fullscreenEnabled;
		public bool? FullscreenEnabled
		{
		    get => _fullscreenEnabled;
		    set => _fullscreenEnabled = value;
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
