using System;
using System.Collections.Generic;
using CP;
using UnityEngine;

namespace Sekai
{
	internal sealed class DialogService
	{
		private const string DIALOG_PREFAB_PATH_BASE = "Dialog/";
		private readonly ScreenManager _owner;
		private readonly ScreenBlurController _blur;
		private readonly List<DialogBase> _dialogQueue = new List<DialogBase>();

		internal DialogService(ScreenManager owner, ScreenBlurController blur)
		{
			_owner = owner;
			_blur = blur;
		}

		internal bool IsActiveDialogQueue => _dialogQueue.Count > 0;

		internal void ShowModuleMaintenanceDialog()
		{
			ShowModuleMaintenanceSubWindowDialog();
		}

		internal void ShowModuleMaintenanceSubWindowDialog()
		{
			LogUtility.LogError("Module maintenance dialog requested.", Array.Empty<object>());
		}

		internal T InstantiateDialog<T>(DialogType dialogType, DisplayLayerType _layerType) where T : DialogBase
		{
			var layerObject = _owner.GetLayerObject(_layerType);
			return layerObject != null ? InstantiateDialog<T>(dialogType, layerObject.transform) : null;
		}

		internal T InstantiateDialog<T>(DialogType dialogType, Transform parent) where T : DialogBase
		{
			string dialogName = dialogType.ToString();
			GameObject prefab = Resources.Load<GameObject>(DIALOG_PREFAB_PATH_BASE + dialogName);
			if (prefab == null)
			{
				prefab = Resources.Load<GameObject>("dialog/" + dialogName);
			}
			if (prefab == null)
			{
				UnityEngine.Debug.LogError($"Dialog prefab not found: {dialogName}");
				return null;
			}

			GameObject _instance = UnityEngine.Object.Instantiate(prefab, parent, false);
			_instance.name = dialogName;
			T dialog = _instance.GetComponent<T>() ?? _instance.GetComponentInChildren<T>(true);
			if (dialog == null)
			{
				UnityEngine.Debug.LogError($"Dialog component {typeof(T).Name} not found on prefab: {dialogName}");
				UnityEngine.Object.Destroy(_instance);
			}
			return dialog;
		}

		internal T ShowDialog<T>(DialogType dialogType, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog, DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true) where T : DialogBase
		{
			T dialog = InstantiateDialog<T>(dialogType, _layerType);
			dialog?.Initialize(dialogSize, allowCloseExternal);
			OpenDialog(dialog, _layerType);
			return dialog;
		}

		internal T Show1ButtonDialog<T>(DialogType dialogType, string messageBodyKey, string okButtonLabelKey, Action onClickOK, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog, DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true) where T : Common1ButtonDialog
		{
			T dialog = InstantiateDialog<T>(dialogType, _layerType);
			dialog?.Initialize(messageBodyKey, okButtonLabelKey, onClickOK, dialogSize, allowCloseExternal);
			OpenDialog(dialog, _layerType);
			return dialog;
		}

		internal T Show1ButtonDialog<T>(DialogType dialogType, string titleKey, string messageBodyKey, string okButtonLabelKey, Action onClickOK, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog, DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true) where T : Common1ButtonDialog
		{
			T dialog = InstantiateDialog<T>(dialogType, _layerType);
			dialog?.Initialize(titleKey, messageBodyKey, okButtonLabelKey, onClickOK, dialogSize, allowCloseExternal);
			OpenDialog(dialog, _layerType);
			return dialog;
		}

		internal T Show1ButtonDialog<T>(DialogType dialogType, Action onClickOK, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog, DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true) where T : Common1ButtonDialog
		{
			return Show1ButtonDialog<T>(dialogType, null, null, onClickOK, _layerType, dialogSize, allowCloseExternal);
		}

		internal T Show2ButtonDialog<T>(DialogType dialogType, string messageBodyKey, string okButtonLabelKey, string cancelButtonLabelKey, Action onClickOK, Action onClickCancel, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog, DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true) where T : Common2ButtonDialog
		{
			T dialog = InstantiateDialog<T>(dialogType, _layerType);
			dialog?.Initialize(messageBodyKey, okButtonLabelKey, cancelButtonLabelKey, onClickOK, onClickCancel, dialogSize, allowCloseExternal);
			OpenDialog(dialog, _layerType);
			return dialog;
		}

		internal T Show2ButtonDialog<T>(DialogType dialogType, string titleKey, string messageBodyKey, string okButtonLabelKey, string cancelButtonLabelKey, Action onClickOK, Action onClickCancel, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog, DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true) where T : Common2ButtonDialog
		{
			T dialog = InstantiateDialog<T>(dialogType, _layerType);
			dialog?.Initialize(titleKey, messageBodyKey, okButtonLabelKey, cancelButtonLabelKey, onClickOK, onClickCancel, dialogSize, allowCloseExternal);
			OpenDialog(dialog, _layerType);
			return dialog;
		}

		internal T Show2ButtonDialog<T>(DialogType dialogType, Action onClickOK, Action onClickCancel, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog, DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true) where T : Common2ButtonDialog
		{
			return Show2ButtonDialog<T>(dialogType, null, null, null, onClickOK, onClickCancel, _layerType, dialogSize, allowCloseExternal);
		}

		internal T Show2ButtonDialog<T>(DialogType dialogType) where T : Common2ButtonDialog
		{
			return Show2ButtonDialog<T>(dialogType, null, null);
		}

		internal T ShowMultiButtonDialog<T>(DialogType dialogType, string messageBodyKey, Dictionary<string, string> labelKeyDic, Dictionary<string, Action> actionDic, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog, DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true) where T : CommonMultiButtonDialog
		{
			T dialog = InstantiateDialog<T>(dialogType, _layerType);
			if (dialog == null)
			{
				return null;
			}

			dialog.Initialize(messageBodyKey, labelKeyDic, actionDic, dialogSize, allowCloseExternal);
			OpenDialog(dialog, _layerType);
			return dialog;
		}

		internal T ShowMultiButtonDialog<T>(DialogType dialogType, Dictionary<string, Action> actionDic, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog, DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true) where T : CommonMultiButtonDialog
		{
			T dialog = InstantiateDialog<T>(dialogType, _layerType);
			if (dialog == null)
			{
				return null;
			}

			dialog.Initialize(actionDic, dialogSize, allowCloseExternal);
			OpenDialog(dialog, _layerType);
			return dialog;
		}

		internal T ShowSubWindowDialog<T>(string messageBody = null, Action onClose = null, bool allowCloseExternal = true, DialogType dialogType = DialogType.SubWindowDialog, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog) where T : SubWindowDialog
		{
			T dialog = InstantiateDialog<T>(dialogType, _layerType);
			if (dialog == null)
			{
				return null;
			}

			dialog.Initialize(messageBody, onClose, allowCloseExternal);
			dialog.Open();
			return dialog;
		}

		internal void CloseDialogs(DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog)
		{
			foreach (DialogBase dialog in GetActiveDialogs(_layerType))
			{
				if (dialog != null)
				{
					dialog.Close();
				}
			}
		}

		internal bool IsActiveDialog(string dialogName, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog)
		{
			return GetActiveDialog(dialogName, _layerType) != null;
		}

		internal DialogBase GetActiveDialog(string dialogName, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog)
		{
			foreach (DialogBase dialog in GetActiveDialogs(_layerType))
			{
				if (dialog != null && dialog.name == dialogName)
				{
					return dialog;
				}
			}
			return null;
		}

		internal T GetActiveDialog<T>(DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog) where T : DialogBase
		{
			TryGetActiveDialog(_layerType, out T dialog);
			return dialog;
		}

		internal bool TryGetActiveDialog<T>(out T dialog) where T : DialogBase
		{
			return TryGetActiveDialog(DisplayLayerType.Layer_Dialog, out dialog);
		}

		internal bool TryGetActiveDialog<T>(DisplayLayerType _layerType, out T dialog) where T : DialogBase
		{
			dialog = null;
			foreach (T activeDialog in GetActiveDialogs<T>(_layerType))
			{
				if (activeDialog != null)
				{
					dialog = activeDialog;
					return true;
				}
			}
			return false;
		}

		internal T[] GetActiveDialogs<T>(DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog) where T : DialogBase
		{
			var result = new List<T>();
			var layerObject = _owner.GetLayerObject(_layerType);
			if (layerObject == null)
			{
				return result.ToArray();
			}

			foreach (T dialog in layerObject.GetComponentsInChildren<T>(true))
			{
				if (dialog != null && dialog.gameObject.activeSelf)
				{
					result.Add(dialog);
				}
			}
			return result.ToArray();
		}

		internal bool ExistsDialog(DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog)
		{
			return GetActiveDialogs(_layerType).Length > 0;
		}

		internal DialogBase[] GetActiveDialogs(DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog)
		{
			return GetActiveDialogs<DialogBase>(_layerType);
		}

		internal bool IsActiveDialog<T>(DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog) where T : UnityEngine.Object
		{
			var layerObject = _owner.GetLayerObject(_layerType);
			if (layerObject == null)
			{
				return false;
			}

			foreach (T component in layerObject.GetComponentsInChildren<T>(true))
			{
				if (component != null)
				{
					return true;
				}
			}
			return false;
		}

		internal void CloseCommonWebviewDialog(WebviewPageType type)
		{
			throw null;
		}

		internal ChainDialogPlayer CreateChainDialogPlayer()
		{
			throw null;
		}

		internal void ShowOutOfPeriodPlayLoginStoryDialog(Action onClickOKCallback = null)
		{
			throw null;
		}

		private void OpenDialog(DialogBase dialog, DisplayLayerType _layerType)
		{
			if (dialog == null)
			{
				return;
			}

			dialog.OnOpenPreprocess += () => OnOpenPreprocessDialog(dialog, _layerType);
			dialog.OnOpen += _blur.OnOpenedDialog;
			dialog.OnClose += () => OnCloseDialog(dialog, _layerType);
			dialog.Open();
		}

		private void OnOpenPreprocessDialog(DialogBase dialog, DisplayLayerType _layerType)
		{
			if (dialog != null && !_dialogQueue.Contains(dialog))
			{
				_dialogQueue.Add(dialog);
			}
			_blur.OnOpeningDialog(dialog, _layerType);
		}

		private void OnCloseDialog(DialogBase dialog, DisplayLayerType _layerType)
		{
			if (dialog != null)
			{
				_dialogQueue.Remove(dialog);
			}
			_blur.OnClosedDialog(dialog, _layerType);
		}
	}
}
