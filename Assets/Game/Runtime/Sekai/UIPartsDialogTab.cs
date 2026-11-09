using System;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	[RequireComponent(typeof(CustomToggle))]
	public class UIPartsDialogTab : MonoBehaviour
	{
		[SerializeField]
		private CustomImage _backgroundImage;

		[SerializeField]
		private CustomTextMesh _text;

		[SerializeField]
		private GameObject _badge;

		[SerializeField]
		private GameObject _lineObj;

		private bool _isOn;

		private CustomToggle _toggle;

		private Action _onValueChanged;

		public bool IsOn
		{
			get
			{
				return _toggle != null ? _toggle.isOn : _isOn;
			}
			set
			{
				_isOn = value;
				if (_toggle != null)
				{
					_toggle.isOn = value;
				}
				if (value)
				{
					On();
				}
				else
				{
					Off();
				}
			}
		}

		public bool IsShowBadge
		{
			get
			{
				return _badge != null && _badge.activeSelf;
			}
			set
			{
				if (_badge != null)
				{
					_badge.SetActive(value);
				}
			}
		}

		protected void Awake()
		{
			_toggle = GetComponent<CustomToggle>();
		}

		protected void OnEnable()
		{
			if (_toggle == null)
			{
				_toggle = GetComponent<CustomToggle>();
			}
			if (_toggle != null)
			{
				_toggle.onValueChanged.RemoveListener(Change);
				_toggle.onValueChanged.AddListener(Change);
				Change(_toggle.isOn);
			}
		}

		protected void OnDisable()
		{
			if (_toggle != null)
			{
				_toggle.onValueChanged.RemoveListener(Change);
			}
		}

		private void On()
		{
			_isOn = true;
			if (_backgroundImage != null)
			{
				_backgroundImage.color = Color.white;
			}
			if (_text != null)
			{
				_text.color = Color.white;
			}
			ShowLine();
		}

		private void Off()
		{
			_isOn = false;
			if (_backgroundImage != null)
			{
				_backgroundImage.color = new Color(1f, 1f, 1f, 0.65f);
			}
			if (_text != null)
			{
				_text.color = new Color(1f, 1f, 1f, 0.65f);
			}
			HideLine();
		}

		public void Setup(Action _onValueChanged)
		{
			this._onValueChanged = _onValueChanged;
			if (_toggle == null)
			{
				_toggle = GetComponent<CustomToggle>();
			}
			if (_toggle != null)
			{
				_toggle.onValueChanged.RemoveListener(Change);
				_toggle.onValueChanged.AddListener(Change);
			}
		}

		private void Change(bool _isOn)
		{
			if (_isOn)
			{
				On();
			}
			else
			{
				Off();
			}
			_onValueChanged?.Invoke();
		}

		public void ShowLine()
		{
			if (_lineObj != null)
			{
				_lineObj.SetActive(true);
			}
		}

		public void HideLine()
		{
			if (_lineObj != null)
			{
				_lineObj.SetActive(false);
			}
		}

		public UIPartsDialogTab()
		{
		}
	}
}
