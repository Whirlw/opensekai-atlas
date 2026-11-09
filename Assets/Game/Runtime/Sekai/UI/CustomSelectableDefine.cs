using System;
using System.Collections.Generic;
using CP;
using UnityEngine;
using UnityEngine.UI;
using Sekai;
using Sekai.Core.Live;

namespace Sekai.UI
{
	public class CustomSelectableDefine
	{
		private const float LOCAL_INTERVAL_SEC = 0.2f;

		private static readonly ColorBlock _defaultColors;
		private static readonly Dictionary<SeType, string> _seTable;
		private static readonly Navigation _defaultNavigation;
		private static float _localIntervalStartTime;

		static CustomSelectableDefine()
		{
			ColorBlock colors = ColorBlock.defaultColorBlock;
			colors.normalColor = Color.white;
			colors.highlightedColor = Color.white;
			colors.pressedColor = Color.white;
			colors.selectedColor = Color.white;
			colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
			colors.colorMultiplier = 1f;
			colors.fadeDuration = 0.1f;
			_defaultColors = colors;

			_seTable = new Dictionary<SeType, string>
			{
				{ SeType.Decide, "SE_DECIDE1" },
				{ SeType.Cancel, "SE_CANCEL" },
				{ SeType.Other, string.Empty },
				{ SeType.MysekaiDecide, "se_mysekai_ui_decision" },
				{ SeType.MysekaiSelect, "se_mysekai_ui_select" },
				{ SeType.MysekaiCancel, "se_mysekai_ui_cancel" },
				{ SeType.None, string.Empty }
			};

			_defaultNavigation = new Navigation
			{
				mode = Navigation.Mode.None
			};
		}

		public static void SetDefalutValue(Selectable selectable)
		{
			if (selectable == null)
			{
				return;
			}

			if (selectable.targetGraphic == null)
			{
				selectable.targetGraphic = selectable.GetComponent<Graphic>();
			}
			selectable.colors = _defaultColors;
			selectable.navigation = _defaultNavigation;
			selectable.transition = Selectable.Transition.None;
		}

		public static bool CheckPointerClickAction(Selectable selectable, InputManager.IntervalUseType interval, SeType se, string OtherSeName = "")
		{
			if (CheckAndResetIntervalTime(interval))
			{
				return false;
			}

			if (selectable != null && selectable.IsActive() && selectable.IsInteractable())
			{
				PlaySE(se, OtherSeName);
			}

			return true;
		}

		public static void PlaySE(SeType se, string OtherSeName = "")
		{
			if (!_seTable.TryGetValue(se, out string cueName) || string.IsNullOrEmpty(cueName))
			{
				if (se != SeType.Other)
				{
					return;
				}
				cueName = OtherSeName;
			}

			if (string.IsNullOrEmpty(cueName))
			{
				return;
			}

			if (se == SeType.Other && !SoundManager.Instance.ExistsCueName(cueName))
			{
				LogUtility.LogError("存在しないキュー名が指定されました {0}", cueName);
				return;
			}

			SoundManager.Instance.PlaySEOneShot(cueName, 0);
		}

		private static bool CheckAndResetIntervalTime(InputManager.IntervalUseType interval)
		{
			if (interval == InputManager.IntervalUseType.None)
			{
				return false;
			}

			try
			{
				InputManager inputManager = UnityEngine.Object.FindFirstObjectByType<InputManager>();
				if (inputManager != null)
				{
					return inputManager.CheckAndResetIntervalTime(interval);
				}
			}
			catch (Exception exception)
			{
				LogUtility.LogWarning("InputManager interval check failed: {0}", exception.Message);
			}

			float now = Time.realtimeSinceStartup;
			if (now - _localIntervalStartTime < LOCAL_INTERVAL_SEC)
			{
				return true;
			}
			_localIntervalStartTime = now;
			return false;
		}
	}
}
