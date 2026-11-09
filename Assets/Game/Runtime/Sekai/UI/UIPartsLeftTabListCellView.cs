using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sekai.UI
{
	public class UIPartsLeftTabListCellView : MonoBehaviour
	{
		[SerializeField]
		protected GameObject attentionBadge;

		[SerializeField]
		private GameObject _selectedObj;

		[SerializeField]
		protected GameObject lockObj;

		[SerializeField]
		private CustomImage _lineImage;

		[SerializeField]
		protected CustomTextMesh text;

		[SerializeField]
		private GameObject[] _subTabMarkObjects;

		[SerializeField]
		private GameObject[] _subTabArrowObjects;

		[SerializeField]
		private UIPartsCommonBalloon _lockBalloon;

		[SerializeField]
		private GraphicButtonTapEffect _tapEffect;

		private static readonly Lazy<Dictionary<bool, Color>> ActiveTextColorTable;

		public void SetAttentionBadge(bool showsBadge)
		{
			if (attentionBadge != null)
			{
				attentionBadge.SetActive(showsBadge);
			}
		}

		public void SetLine(bool isSub)
		{
			if (_lineImage == null)
			{
				return;
			}

			_lineImage.SetActive(true);
			_lineImage.color = isSub ? new Color(0.78f, 0.86f, 0.9f, 1f) : new Color(0.35f, 0.68f, 0.78f, 1f);
		}

		public void SetSubTabObject(bool hasSubData, bool isSelected, bool isDisplayingSubTab)
		{
			if (hasSubData)
			{
				SetActiveAt(_subTabMarkObjects, 0, !isSelected);
				SetActiveAt(_subTabMarkObjects, 1, isSelected);
				SetActiveAt(_subTabArrowObjects, 0, !isSelected || !isDisplayingSubTab);
				SetActiveAt(_subTabArrowObjects, 1, isSelected && isDisplayingSubTab);
				return;
			}

			SetActiveAll(_subTabMarkObjects, false);
			SetActiveAll(_subTabArrowObjects, false);
		}

		public void SetText(string name)
		{
			text?.SetText(name);
		}

		public void SetActiveText(bool isActive)
		{
			text?.SetActive(isActive);
		}

		public virtual void SetSelected(bool isSelected)
		{
			if (_selectedObj != null)
			{
				_selectedObj.SetActive(isSelected);
			}

			var color = ActiveTextColorTable.Value[isSelected];
			if (_tapEffect != null)
			{
				_tapEffect.SetDefaultColor(color);
				_tapEffect.ResetInteraction();
			}

			if (text != null)
			{
				text.color = color;
			}
		}

		public void SetLocked(bool isLocked)
		{
			if (text != null && !string.IsNullOrEmpty(text.Text))
			{
				var current = text.color;
				current.a = isLocked ? 0.45f : 1f;
				text.color = current;
			}

			if (lockObj != null)
			{
				lockObj.SetActive(isLocked);
			}
		}

		public void SetLockBalloon(bool showsBalloon, string text)
		{
			if (_lockBalloon == null)
			{
				return;
			}

			_lockBalloon.gameObject.SetActive(showsBalloon);
			if (showsBalloon)
			{
				_lockBalloon.Setup(text, false, false);
			}
		}

		public UIPartsLeftTabListCellView()
		{
		}

		static UIPartsLeftTabListCellView()
		{
			ActiveTextColorTable = new Lazy<Dictionary<bool, Color>>(() => new Dictionary<bool, Color>
			{
				{ true, Color.black },
				{ false, Color.white }
			});
		}

		private static void SetActiveAt(GameObject[] objects, int index, bool active)
		{
			if (objects != null && index >= 0 && index < objects.Length && objects[index] != null)
			{
				objects[index].SetActive(active);
			}
		}

		private static void SetActiveAll(GameObject[] objects, bool active)
		{
			if (objects == null)
			{
				return;
			}

			foreach (var obj in objects)
			{
				if (obj != null)
				{
					obj.SetActive(active);
				}
			}
		}
	}
}
