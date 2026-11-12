using System;
using System.Collections.Generic;
using UnityEngine;
using Sekai;

namespace Sekai.UI
{
	[RequireComponent(typeof(RectTransform), typeof(CustomButton), typeof(CommonButtonTapEffect))]
	public class UIPartsCommonButton : MonoBehaviour
	{
		private enum ButtonTypeEnum
		{
			Main = 0,
			Sub = 1
		}

		public enum DisplayTypeEnum
		{
			Normal = 0,
			Primary = 1,
			Special = 2
		}

		public enum SizeEnum
		{
			S = 0,
			M = 1,
			L = 2
		}

		private static class ButtonTypeData
		{
			public static readonly Dictionary<ButtonTypeEnum, string> SpriteNameDictionary;

			public static readonly Dictionary<ButtonTypeEnum, float> HeightDictionary;

			public static readonly Dictionary<ButtonTypeEnum, float> FontSizeDictionary;

			static ButtonTypeData()
			{
				SpriteNameDictionary = new Dictionary<ButtonTypeEnum, string>
				{
					{ ButtonTypeEnum.Main, "80" },
					{ ButtonTypeEnum.Sub, "56" }
				};
				HeightDictionary = new Dictionary<ButtonTypeEnum, float>
				{
					{ ButtonTypeEnum.Main, 96f },
					{ ButtonTypeEnum.Sub, 72f }
				};
				FontSizeDictionary = new Dictionary<ButtonTypeEnum, float>
				{
					{ ButtonTypeEnum.Main, 32f },
					{ ButtonTypeEnum.Sub, 28f }
				};
			}
		}

		[SerializeField]
		private RectTransform _rectTransform;

		[SerializeField]
		private CustomImage _baseImage;

		[SerializeField]
		private CustomImage _coverImage;

		[SerializeField]
		private CustomButton _customButton;

		[SerializeField]
		private bool _adjustButtonSetting;

		[SerializeField]
		private bool _adjustTextSize;

		[SerializeField]
		private bool _changeSprite;

		[SerializeField]
		private CustomTextMesh _customTextMesh;

		[SerializeField]
		private ButtonTypeEnum _buttonType;

		[SerializeField]
		private DisplayTypeEnum _displayType;

		[SerializeField]
		private SizeEnum _size;

		[SerializeField]
		private CommonButtonTapEffect _tapEffect;

		private const float MAIN_S_WIDTH = 256f;

		private const float MAIN_M_WIDTH = 280f;

		private const float MAIN_L_WIDTH = 336f;

		private const float SUB_S_WIDTH = 176f;

		private const float SUB_M_WIDTH = 216f;

		private const float SUB_L_WIDTH = 296f;

		private static readonly Dictionary<DisplayTypeEnum, Lazy<Color>> GlowImageColorDictionary;

		private static readonly Dictionary<DisplayTypeEnum, Lazy<Color>> GlowTextColorDictionary;

		private static readonly Dictionary<DisplayTypeEnum, string> DisplayTypeDictionary;

		public CustomImage BaseImage
		{
			get
			{
				return _baseImage;
			}
		}

		public CustomButton CustomButton
		{
			get
			{
				return _customButton;
			}
		}

		private string ButtonTypeName
		{
			get
			{
				return ButtonTypeData.SpriteNameDictionary.TryGetValue(_buttonType, out string value) ? value : "80";
			}
		}

		private void Awake()
		{
			if (_rectTransform == null)
			{
				_rectTransform = GetComponent<RectTransform>();
			}
			if (_customButton == null)
			{
				_customButton = GetComponent<CustomButton>();
			}
			if (_tapEffect == null)
			{
				_tapEffect = GetComponent<CommonButtonTapEffect>();
			}
			SetDisableCover();
			SetGlowCoverColor();
			if (_adjustButtonSetting)
			{
				SetBaseImage();
				SetSize();
			}
		}

		private string GetSpriteName()
		{
			string display = DisplayTypeDictionary.TryGetValue(_displayType, out string value) ? value : "wh";
			return "btn_round_h" + ButtonTypeName + "_" + display;
		}

		private Vector2 GetSizeDelta()
		{
			float width = _rectTransform != null ? _rectTransform.sizeDelta.x : 0f;
			switch (_buttonType)
			{
				case ButtonTypeEnum.Main:
					width = _size == SizeEnum.S ? MAIN_S_WIDTH : _size == SizeEnum.M ? MAIN_M_WIDTH : MAIN_L_WIDTH;
					break;
				case ButtonTypeEnum.Sub:
					width = _size == SizeEnum.S ? SUB_S_WIDTH : _size == SizeEnum.M ? SUB_M_WIDTH : SUB_L_WIDTH;
					break;
			}
			float height = ButtonTypeData.HeightDictionary.TryGetValue(_buttonType, out float value) ? value : 96f;
			return new Vector2(width, height);
		}

		private float GetFontSize()
		{
			return ButtonTypeData.FontSizeDictionary.TryGetValue(_buttonType, out float value) ? value : 32f;
		}

		private void SetBaseImage()
		{
			if (_changeSprite && _baseImage != null)
			{
				_baseImage.SpriteName = GetSpriteName();
			}
		}

		private void SetDisableCover()
		{
			if (_coverImage != null)
			{
				_coverImage.SpriteName = "bg_base_round_h" + ButtonTypeName + "_wh";
			}
		}

		public void SetGlowCoverColor()
		{
			if (_tapEffect == null)
			{
				return;
			}
			Color imageColor = GlowImageColorDictionary.TryGetValue(_displayType, out Lazy<Color> imageLazy) ? imageLazy.Value : Color.white;
			Color textColor = GlowTextColorDictionary.TryGetValue(_displayType, out Lazy<Color> textLazy) ? textLazy.Value : Color.white;
			_tapEffect.Setup(ButtonTypeName, imageColor, textColor);
		}

		public void SetImage(DisplayTypeEnum displayTypeEnum)
		{
			_displayType = displayTypeEnum;
			SetBaseImage();
			SetGlowCoverColor();
		}

		public void SetPrimary()
		{
			SetImage(DisplayTypeEnum.Primary);
		}

		private void SetSize()
		{
			if (_rectTransform != null)
			{
				_rectTransform.sizeDelta = GetSizeDelta();
			}
			if (_adjustTextSize && _customTextMesh != null)
			{
				_customTextMesh.fontSize = GetFontSize();
			}
		}

		public void SetSize(SizeEnum _size)
		{
			this._size = _size;
			SetSize();
		}

		public void SetText(string text)
		{
			if (_customTextMesh != null)
			{
				_customTextMesh.SetText(text);
			}
		}

		public void SetDefaultFontDB()
		{
			_customTextMesh?.SetDefaultFontDB();
		}

		public void SetDefaultFontEB()
		{
			_customTextMesh?.SetDefaultFontEB();
		}

		public void SetWordingKey(string wordingKey)
		{
			_customTextMesh?.SetWordingKey(wordingKey);
		}

		public void RemoveAllAndAddListener(Action action)
		{
			if (_customButton == null)
			{
				_customButton = GetComponent<CustomButton>();
			}
			if (_customButton == null)
			{
				return;
			}
			_customButton.onClick.RemoveAllListeners();
			if (action != null)
			{
				_customButton.onClick.AddListener(() => action());
			}
		}

		public void Show()
		{
			gameObject.SetActive(true);
		}

		public void Hide()
		{
			gameObject.SetActive(false);
		}

		public void SetCustomButtonActive(bool isActive)
		{
			if (_customButton != null)
			{
				_customButton.interactable = isActive;
			}
		}

		public void RemoveTapEffect()
		{
			if (_tapEffect != null)
			{
				_tapEffect.enabled = false;
			}
		}

		public void Enable()
		{
			SetCustomButtonActive(true);
			if (_coverImage != null)
			{
				_coverImage.Hide();
			}
		}

		public void Disable()
		{
			SetCustomButtonActive(false);
			if (_coverImage != null)
			{
				_coverImage.Show();
			}
		}

		public UIPartsCommonButton()
		{
		}

		static UIPartsCommonButton()
		{
			GlowImageColorDictionary = new Dictionary<DisplayTypeEnum, Lazy<Color>>
			{
				{ DisplayTypeEnum.Normal, new Lazy<Color>(() => UnityEngine.ColorUtility.TryParseHtmlString("#A1F4EB", out Color color) ? color : Color.white) },
				{ DisplayTypeEnum.Primary, new Lazy<Color>(() => UnityEngine.ColorUtility.TryParseHtmlString("#E4FCF8", out Color color) ? color : Color.white) },
				{ DisplayTypeEnum.Special, new Lazy<Color>(() => UnityEngine.ColorUtility.TryParseHtmlString("#E4FCF8", out Color color) ? color : Color.white) }
			};
			GlowTextColorDictionary = new Dictionary<DisplayTypeEnum, Lazy<Color>>
			{
				{ DisplayTypeEnum.Normal, new Lazy<Color>(() => Color.white) },
				{ DisplayTypeEnum.Primary, new Lazy<Color>(() => UnityEngine.ColorUtility.TryParseHtmlString("#77EEDD", out Color color) ? color : Color.white) },
				{ DisplayTypeEnum.Special, new Lazy<Color>(() => UnityEngine.ColorUtility.TryParseHtmlString("#77EEDD", out Color color) ? color : Color.white) }
			};
			DisplayTypeDictionary = new Dictionary<DisplayTypeEnum, string>
			{
				{ DisplayTypeEnum.Normal, "wh" },
				{ DisplayTypeEnum.Primary, "gn" },
				{ DisplayTypeEnum.Special, "or" }
			};
		}
	}
}
