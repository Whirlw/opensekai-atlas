using System;
using Beebyte.Obfuscator;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public class UIPartsCheckBox : MonoBehaviour
	{
		[SerializeField]
		private GameObject _checkObj;

		[SerializeField]
		private bool _check;

		[SerializeField]
		private CustomToggle _toggle;

		public CustomToggle Toggle
		{
			get
			{
				return EnsureToggle();
			}
		}

		public Action<bool> OnCheckValueChanged { get; set; }

		public Action OnPointerClickAction
		{
			get
			{
				CustomToggle customToggle = EnsureToggle();
				return customToggle != null ? customToggle.onPointerClickAction : null;
			}
			set
			{
				CustomToggle customToggle = EnsureToggle();
				if (customToggle != null)
				{
					customToggle.onPointerClickAction = value;
				}
			}
		}

		public bool Check
		{
			get
			{
				return _check;
			}
			set
			{
				SetVisual(value);
				CustomToggle customToggle = EnsureToggle();
				if (customToggle != null && customToggle.isOn != _check)
				{
					customToggle.isOn = _check;
				}
			}
		}

		public bool CheckDefaultValue
		{
			get
			{
				return _check;
			}
			set
			{
				SetVisual(value);
				CustomToggle customToggle = EnsureToggle();
				if (customToggle != null)
				{
					customToggle.SetIsOnWithoutNotify(_check);
				}
			}
		}

		[Skip]
		public void OnValueChanged(bool isOn)
		{
			CustomToggle customToggle = EnsureToggle();
			Check = customToggle != null ? customToggle.isOn : isOn;
			OnCheckValueChanged?.Invoke(_check);
		}

		public void SetEnableToggle(bool enabled)
		{
			CustomToggle customToggle = EnsureToggle();
			if (customToggle != null)
			{
				customToggle.enabled = enabled;
			}
		}

		public void SetCheckValueByToggleEnabled()
		{
			CustomToggle customToggle = EnsureToggle();
			if (customToggle != null)
			{
				_check = customToggle.enabled;
			}
		}

		public UIPartsCheckBox()
		{
		}

		private void SetVisual(bool value)
		{
			if (_checkObj != null)
			{
				_checkObj.SetActive(value);
			}
			_check = value;
		}

		private CustomToggle EnsureToggle()
		{
			if (_toggle == null)
			{
				_toggle = GetComponent<CustomToggle>();
			}
			return _toggle;
		}
	}
}
