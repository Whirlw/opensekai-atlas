using System;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	[RequireComponent(typeof(CustomButton))]
	public class UIPartsFilterButton : MonoBehaviour
	{
		[SerializeField]
		private CustomButton _button;

		[SerializeField]
		protected CustomImage offImage;

		[SerializeField]
		protected CustomImage onImage;

		private Action _onClick;

		private bool _filter;

		public bool Filter
		{
			get
			{
				throw null;
			}
			set
			{
				throw null;
			}
		}

		public void Initialize(Action _onClick)
		{
			throw null;
		}

		public void Setup(bool _filter, Action _onClick = null)
		{
			throw null;
		}

		public void SetCallback(Action _onClick)
		{
			throw null;
		}

		public void SetDisplayEnableFilter(bool enabled)
		{
			throw null;
		}

		public void Show()
		{
			throw null;
		}

		public void Hide()
		{
			throw null;
		}

		public void EnableClick()
		{
			throw null;
		}

		public void DisableClick()
		{
			throw null;
		}

		public void SetEnabled(bool enabled)
		{
			throw null;
		}

		public void Dispose()
		{
			throw null;
		}

		private void OnClick()
		{
			throw null;
		}

		private void UpdateImage()
		{
			throw null;
		}

		public UIPartsFilterButton()
		{
		}
	}
}
