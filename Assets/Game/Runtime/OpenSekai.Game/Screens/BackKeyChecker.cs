using System;
using System.Collections.Generic;
using Sekai.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sekai
{
	public class BackKeyChecker
	{
		private Action<List<RaycastResult>> _onBackKey;

		public CustomButton BackButton { get; set; }
		public bool EnableQuit { get; set; }

		public void Initialize(Action<List<RaycastResult>> _onBackKey)
		{
			this._onBackKey = _onBackKey;
		}

		public void Check()
		{
			if (NativeInput.BackPressedThisFrame)
			{
				ExecuteBackKey();
			}
		}

		private void ExecuteBackKey()
		{
			var results = new List<RaycastResult>();
			if (EventSystem.current != null)
			{
				var pointer = new PointerEventData(EventSystem.current)
				{
					position = NativeInput.PointerPosition
				};
				EventSystem.current.RaycastAll(pointer, results);
			}

			_onBackKey?.Invoke(results);
		}
	}
}
