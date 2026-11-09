using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sekai
{
	public class LiveOutUIController
	{
		private const string LIVE_OUT_UI_PREFAB_PATH = "Live/UI/LiveOutUI";
		private const string LIVE_OUT_UI_PREFAB_FALLBACK_PATH = "live/ui/LiveOutUI";
		private const string LIVE_OUT_UI_ROOT_NAME = "LiveOutUI";
		private const string BUTTON_KEY_CANCEL = "Cancel";
		private const string BUTTON_KEY_RETRY = "Retry";
		private const string BUTTON_KEY_RESUME = "Resume";

		private GameObject _uiPrefab;
		private GameObject _uiRootObject;
		private ScreenManager _screenManager;
		private Camera _baseCamera;
		private DialogBase _activeDialog;

		public void Initialize(LivePlayMode livePlayMode, Camera _baseCamera)
		{
			_uiPrefab = Resources.Load<GameObject>(LIVE_OUT_UI_PREFAB_PATH)
				?? Resources.Load<GameObject>(LIVE_OUT_UI_PREFAB_FALLBACK_PATH);
			_uiRootObject = null;
			_screenManager = null;
			_activeDialog = null;
			this._baseCamera = _baseCamera;
		}

		public void Destroy()
		{
			if (_activeDialog != null)
			{
				_activeDialog.Close();
				_activeDialog = null;
			}
			RemoveUICameraFromBaseStack();
			if (_uiRootObject != null)
			{
				UnityEngine.Object.Destroy(_uiRootObject);
				_uiRootObject = null;
			}
			_screenManager = null;
		}


		public void ShowPauseDialog(Action onClickResume, Action onClickCancel, Action onClickRetry)
		{
			InstantiateIfNeeded();
			SetActiveDialog(_screenManager?.ShowMultiButtonDialog<LivePauseDialog>(
				DialogType.LivePauseDialog,
				CreatePauseActionDictionary(onClickResume, onClickCancel, onClickRetry),
				DisplayLayerType.Layer_Dialog,
				DialogSize.Manual,
				false));
		}


		public void ShowConfirmRetryDialog(Action onClickOK, Action onClickCancel)
		{
			InstantiateIfNeeded();
			SetActiveDialog(_screenManager?.Show2ButtonDialog<Common2ButtonDialog>(
				DialogType.Common2ButtonDialog,
				"WORD_RETRY",
				"MSG_PAUSE_LIVE_RETRY",
				"WORD_RETRY",
				"WORD_CANCEL",
				onClickOK,
				onClickCancel,
				DisplayLayerType.Layer_Dialog,
				DialogSize.Manual,
				true));
		}

		public void ShowConfirmRetireDialog(Action onClickOK, Action onClickCancel)
		{
			InstantiateIfNeeded();
			SetActiveDialog(_screenManager?.Show2ButtonDialog<Common2ButtonDialog>(
				DialogType.Common2ButtonDialog,
				"WORD_ABORT",
				"MSG_PAUSE_LIVE_ABORT",
				"WORD_ABORT",
				"WORD_CANCEL",
				onClickOK,
				onClickCancel,
				DisplayLayerType.Layer_Dialog,
				DialogSize.Manual,
				true));
		}

		private bool ExistsInstance => _uiRootObject != null;

		private void RemoveUICameraFromBaseStack()
		{
			if (_screenManager == null)
			{
				return;
			}

			Camera uiCamera = _screenManager.GetUICamera();
			if (uiCamera != null)
			{
				_screenManager.CameraStack?.RemoveFromStack(uiCamera);
			}
		}

		private void InstantiateIfNeeded()
		{
			if (ExistsInstance)
			{
				return;
			}

			Instantiate();
		}

		private void Instantiate()
		{
			if (_uiPrefab == null)
			{
				_uiPrefab = Resources.Load<GameObject>(LIVE_OUT_UI_PREFAB_PATH)
					?? Resources.Load<GameObject>(LIVE_OUT_UI_PREFAB_FALLBACK_PATH);
			}
			if (_uiPrefab == null)
			{
				Debug.LogErrorFormat("LiveOutUI prefab not found: {0}", LIVE_OUT_UI_PREFAB_PATH);
				return;
			}

			_uiRootObject = UnityEngine.Object.Instantiate(_uiPrefab);
			_uiRootObject.name = LIVE_OUT_UI_ROOT_NAME;
			_screenManager = _uiRootObject.GetComponent<ScreenManager>();
			if (_screenManager == null)
			{
				Debug.LogError("ScreenManager component not found on LiveOutUI.");
				return;
			}

			ScreenManager.SetupInstance(_screenManager);
			_screenManager.SetBaseCamera(_baseCamera);
		}

		private static Dictionary<string, Action> CreatePauseActionDictionary(Action onClickResume, Action onClickCancel, Action onClickRetry)
		{
			return new Dictionary<string, Action>
			{
				{ BUTTON_KEY_CANCEL, onClickCancel },
				{ BUTTON_KEY_RETRY, onClickRetry },
				{ BUTTON_KEY_RESUME, onClickResume }
			};
		}

		private void SetActiveDialog(DialogBase dialog)
		{
			_activeDialog = dialog;
			if (_activeDialog != null)
			{
				DialogBase registeredDialog = _activeDialog;
				_activeDialog.OnClose += () => ClearActiveDialogIfNeeded(registeredDialog);
			}
		}

		private void ClearActiveDialogIfNeeded(DialogBase registeredDialog)
		{
			if (_activeDialog == registeredDialog)
			{
				_activeDialog = null;
			}
		}
	}
}
