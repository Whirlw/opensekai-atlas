using System.Collections.Generic;
using Sekai.UI;
using UnityEngine;
using Sekai.Live;

namespace Sekai
{
	public class LiveModeButton : MonoBehaviour
	{
		[SerializeField]
		private CustomButton _changeButton;

		[SerializeField]
		private CustomTextMesh _modeText;

		private int _index;

		private LiveSettingData _settings;

		private bool _isUpdated;

		private bool _selectableSettingMode;

		private MasterMusicAll _musicAll;

		private List<LiveSettingData.LiveModeType> _modes;

		private void Awake()
		{
			if (_changeButton != null)
			{
				_changeButton.onClick.AddListener(UpdateMode);
			}
		}

		public void SetupLiveMode(MasterMusicAll _musicAll, bool isCheckParameter = false)
		{
			InitializeLiveMode(_musicAll, isCheckParameter);
		}

		public void Setup(MasterMusicAll _musicAll, bool isMVOnly)
		{
			InitializeMode(_musicAll, isMVOnly);
		}

		private void InitializeLiveMode(MasterMusicAll nextMusicAll, bool isCheckParameter)
		{
			InitializeMode(nextMusicAll, false);
		}

		private void InitializeMode(MasterMusicAll _musicAll, bool isMvOnly)
		{
			this._musicAll = _musicAll;
			_selectableSettingMode = true;
			_modes = CreateSelectableModes(_musicAll, isMvOnly);
			LiveSettingData.LiveModeType initialMode = GetPriorityLiveModeType(_musicAll?.music);
			_index = Mathf.Max(0, _modes.IndexOf(initialMode));
			RefreshChangeButtonEnabled();
			RefreshText();
		}

		private LiveSettingData.LiveModeType GetPriorityLiveModeType(MasterMusic masterMusic)
		{
			if (_settings != null && _modes != null && _modes.Contains(_settings.LiveMode))
			{
				return _settings.LiveMode;
			}
			return _modes != null && _modes.Count > 0 ? _modes[0] : LiveSettingData.LiveModeType.Low;
		}

		private void UpdateMode()
		{
			if (_modes == null || _modes.Count == 0)
			{
				return;
			}
			_index = (_index + 1) % _modes.Count;
			_isUpdated = true;
			RefreshText();
		}

		public void SelectLiveMode(LiveSettingData.LiveModeType liveModeType)
		{
			if (_modes == null || _modes.Count == 0)
			{
				_modes = new List<LiveSettingData.LiveModeType> { liveModeType };
			}
			int found = _modes.IndexOf(liveModeType);
			if (found < 0)
			{
				_modes.Add(liveModeType);
				found = _modes.Count - 1;
			}
			_index = found;
			_isUpdated = true;
			RefreshText();
		}

		public void SetupFixedModes(params LiveSettingData.LiveModeType[] selectableModes)
		{
			_selectableSettingMode = true;
			_modes = new List<LiveSettingData.LiveModeType>();
			if (selectableModes != null)
			{
				for (int i = 0; i < selectableModes.Length; i++)
				{
					LiveSettingData.LiveModeType mode = selectableModes[i];
					if (!_modes.Contains(mode))
					{
						_modes.Add(mode);
					}
				}
			}
			if (_modes.Count == 0)
			{
				_modes.Add(LiveSettingData.LiveModeType.Low);
			}
			_index = 0;
			_isUpdated = false;
			RefreshChangeButtonEnabled();
			RefreshText();
		}

		public void SetChangeButtonEnabled(bool enabled)
		{
			_selectableSettingMode = enabled;
			if (_changeButton != null)
			{
				_changeButton.enabled = enabled;
			}
		}

		public void RefreshChangeButtonEnabled()
		{
			if (_changeButton != null)
			{
				_changeButton.enabled = _modes != null && _modes.Count > 1;
			}
		}

		public LiveSettingData.LiveModeType SaveChanges()
		{
			LiveSettingData.LiveModeType selected = Get();
			if (_settings != null)
			{
				_settings.LiveMode = selected;
			}
			_isUpdated = false;
			return selected;
		}

		public LiveSettingData.LiveModeType Get()
		{
			if (_modes == null || _modes.Count == 0)
			{
				return LiveSettingData.LiveModeType.Low;
			}
			return _modes[Mathf.Clamp(_index, 0, _modes.Count - 1)];
		}

		public LiveModeButton()
		{
			_modes = new List<LiveSettingData.LiveModeType>();
			_selectableSettingMode = true;
		}

		private static List<LiveSettingData.LiveModeType> CreateSelectableModes(MasterMusicAll _musicAll, bool isMvOnly)
		{
			List<LiveSettingData.LiveModeType> result = new List<LiveSettingData.LiveModeType>();
			LiveSettingData liveSetting = LiveSettingData.LoadFromStorage();
			string[] categories = _musicAll?.music?.categories;
			bool hasMv = categories == null || categories.Length == 0 || System.Array.Exists(categories, x => x == "mv");
			bool has2d = categories != null && System.Array.Exists(categories, x => x == "mv_2d");
			bool hasOriginal = categories != null && System.Array.Exists(categories, x => x == "original");
			if (hasMv)
			{
				result.Add(liveSetting != null && liveSetting.QualityType != MVQualityType.Default
					? LiveSettingData.LiveModeType.High3D
					: LiveSettingData.LiveModeType.Default3D);
			}
			if (has2d)
			{
				result.Add(LiveSettingData.LiveModeType.Mode2D);
			}
			if (!isMvOnly)
			{
				result.Add(LiveSettingData.LiveModeType.Low);
			}
			if (hasOriginal)
			{
				result.Add(LiveSettingData.LiveModeType.OriginalMV);
			}
			if (result.Count == 0)
			{
				result.Add(LiveSettingData.LiveModeType.Low);
			}
			return result;
		}

		private void RefreshText()
		{
			if (_modeText != null)
			{
				_modeText.SetWordingText(LiveSettingData.GetLiveModeTextKey(Get()));
			}
		}
	}
}
