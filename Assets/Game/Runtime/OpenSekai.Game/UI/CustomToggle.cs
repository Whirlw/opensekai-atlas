using System;
using System.Collections.Generic;
using Beebyte.Obfuscator;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sekai;

namespace Sekai.UI
{
	[AddComponentMenu("Custom UI/Custom Toggle")]
	public class CustomToggle : Toggle
	{
		public enum DisableActionType
		{
			None = 0,
			Grayout = 1
		}

		private readonly Lazy<Color> _disableColor;

		[SerializeField]
		protected SeType se;

		[SerializeField]
		private InputManager.IntervalUseType _interval;

		[SerializeField]
		private DisableActionType _disableActionType;

		[SerializeField]
		private Graphic _coverImage;

		[SerializeField]
		private List<Graphic> _optionalCoverImages;

		[SerializeField]
		private ButtonViewInteractionBase _interaction;

		[SerializeField]
		private CustomTextMesh _captionText;

				[SerializeField] private Action _onPointerClickAction;
		public Action onPointerClickAction
		{
		    get => _onPointerClickAction;
		    set => _onPointerClickAction = value;
		}

		protected override void Awake()
		{
			base.Awake();
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			if (!interactable && _disableActionType == DisableActionType.Grayout)
			{
				ShowCover(_disableColor?.Value ?? Color.gray);
			}
		}

		protected override void OnDisable()
		{
			base.OnDisable();
		}

		[Skip]
		public override void OnPointerClick(PointerEventData eventData)
		{
			if (group != null && isOn)
			{
				return;
			}

			if (CustomSelectableDefine.CheckPointerClickAction(this, _interval, se))
			{
				base.OnPointerClick(eventData);
				onPointerClickAction?.Invoke();
			}
		}

		private void ShowCover(Color color)
		{
			if (_coverImage != null)
			{
				_coverImage.color = color;
				_coverImage.gameObject.SetActive(true);
			}
			if (_optionalCoverImages == null)
			{
				return;
			}
			for (int i = 0; i < _optionalCoverImages.Count; i++)
			{
				if (_optionalCoverImages[i] != null)
				{
					_optionalCoverImages[i].color = color;
					_optionalCoverImages[i].gameObject.SetActive(true);
				}
			}
		}

		public void HideCover()
		{
			if (_coverImage != null)
			{
				_coverImage.gameObject.SetActive(false);
			}
			if (_optionalCoverImages == null)
			{
				return;
			}
			for (int i = 0; i < _optionalCoverImages.Count; i++)
			{
				if (_optionalCoverImages[i] != null)
				{
					_optionalCoverImages[i].gameObject.SetActive(false);
				}
			}
		}

		public void SetOptionalCoverImage(int index)
		{
			if (_optionalCoverImages == null)
			{
				return;
			}
			for (int i = 0; i < _optionalCoverImages.Count; i++)
			{
				if (_optionalCoverImages[i] != null)
				{
					_optionalCoverImages[i].gameObject.SetActive(i == index);
				}
			}
		}

		public override void OnPointerDown(PointerEventData eventData)
		{
			base.OnPointerDown(eventData);
		}

		public override void OnPointerUp(PointerEventData eventData)
		{
			base.OnPointerUp(eventData);
		}

		public void SetCaptionText(string caption)
		{
			if (_captionText != null)
			{
				_captionText.text = caption;
			}
		}

		public CustomToggle()
		{
			_disableColor = new Lazy<Color>(() => Color.gray);
		}
	}
}
