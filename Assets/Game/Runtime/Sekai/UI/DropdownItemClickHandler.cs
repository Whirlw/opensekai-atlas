using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sekai.UI
{
	public class DropdownItemClickHandler : MonoBehaviour, IPointerClickHandler
	{
		private Action _onClickCallback;

		public void SetCallback(Action callback)
		{
			_onClickCallback = callback;
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			_onClickCallback?.Invoke();
		}
	}
}
