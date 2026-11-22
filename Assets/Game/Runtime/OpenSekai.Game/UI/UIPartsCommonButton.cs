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

		[UnityEngine.Serialization.FormerlySerializedAs("_rectTransform")]
		[SerializeField]
		private RectTransform rectTransform;

		[UnityEngine.Serialization.FormerlySerializedAs("_baseImage")]
		[SerializeField]
		private CustomImage baseImage;

		[UnityEngine.Serialization.FormerlySerializedAs("_coverImage")]
		[SerializeField]
		private CustomImage coverImage;

		[UnityEngine.Serialization.FormerlySerializedAs("_customButton")]
		[SerializeField]
		private CustomButton customButton;

		[UnityEngine.Serialization.FormerlySerializedAs("_adjustButtonSetting")]
		[SerializeField]
		private bool adjustButtonSetting;

		[UnityEngine.Serialization.FormerlySerializedAs("_adjustTextSize")]
		[SerializeField]
		private bool adjustTextSize;

		[UnityEngine.Serialization.FormerlySerializedAs("_changeSprite")]
		[SerializeField]
		private bool changeSprite;

		[UnityEngine.Serialization.FormerlySerializedAs("_customTextMesh")]
		[SerializeField]
		private CustomTextMesh customTextMesh;

		[UnityEngine.Serialization.FormerlySerializedAs("_buttonType")]
		[SerializeField]
		private ButtonTypeEnum buttonType;

		[UnityEngine.Serialization.FormerlySerializedAs("_displayType")]
		[SerializeField]
		private DisplayTypeEnum displayType;

		[UnityEngine.Serialization.FormerlySerializedAs("_size")]
		[SerializeField]
		private SizeEnum size;

		[UnityEngine.Serialization.FormerlySerializedAs("_tapEffect")]
		[SerializeField]
		private CommonButtonTapEffect tapEffect;

		private const float MAIN_S_WIDTH = 256f;

		private const float MAIN_M_WIDTH = 280f;

		private const float MAIN_L_WIDTH = 336f;

		private const float SUB_S_WIDTH = 176f;

		private const float SUB_M_WIDTH = 216f;

		private const float SUB_L_WIDTH = 296f;

		private static readonly Dictionary<DisplayTypeEnum, Lazy<Color>> _glowImageColorDictionary;

		private static readonly Dictionary<DisplayTypeEnum, Lazy<Color>> _glowTextColorDictionary;

		private static readonly Dictionary<DisplayTypeEnum, string> _displayTypeDictionary;

		public CustomImage BaseImage
		{
			get
			{
				return baseImage;
			}
		}

		public CustomButton CustomButton
		{
			get
			{
				return customButton;
			}
		}

		private string ButtonTypeName
		{
			get
			{
				return ButtonTypeData.SpriteNameDictionary.TryGetValue(buttonType, out string value) ? value : "80";
			}
		}

		private void Awake()
		{
			if (rectTransform == null)
			{
				rectTransform = GetComponent<RectTransform>();
			}
			if (customButton == null)
			{
				customButton = GetComponent<CustomButton>();
			}
			if (tapEffect == null)
			{
				tapEffect = GetComponent<CommonButtonTapEffect>();
			}
			SetDisableCover();
			SetGlowCoverColor();
			if (adjustButtonSetting)
			{
				SetBaseImage();
				SetSize();
			}
		}

		private string GetSpriteName()
		{
			string display = _displayTypeDictionary.TryGetValue(displayType, out string value) ? value : "wh";
			return "btn_round_h" + ButtonTypeName + "_" + display;
		}

		private Vector2 GetSizeDelta()
		{
			float width = rectTransform != null ? rectTransform.sizeDelta.x : 0f;
			switch (buttonType)
			{
				case ButtonTypeEnum.Main:
					width = size == SizeEnum.S ? MAIN_S_WIDTH : size == SizeEnum.M ? MAIN_M_WIDTH : MAIN_L_WIDTH;
					break;
				case ButtonTypeEnum.Sub:
					width = size == SizeEnum.S ? SUB_S_WIDTH : size == SizeEnum.M ? SUB_M_WIDTH : SUB_L_WIDTH;
					break;
			}
			float height = ButtonTypeData.HeightDictionary.TryGetValue(buttonType, out float value) ? value : 96f;
			return new Vector2(width, height);
		}

		private float GetFontSize()
		{
			return ButtonTypeData.FontSizeDictionary.TryGetValue(buttonType, out float value) ? value : 32f;
		}

		private void SetBaseImage()
		{
			if (changeSprite && baseImage != null)
			{
				baseImage.SpriteName = GetSpriteName();
			}
		}

		private void SetDisableCover()
		{
			if (coverImage != null)
			{
				coverImage.SpriteName = "bg_base_round_h" + ButtonTypeName + "_wh";
			}
		}

		public void SetGlowCoverColor()
		{
			if (tapEffect == null)
			{
				return;
			}
			Color imageColor = _glowImageColorDictionary.TryGetValue(displayType, out Lazy<Color> imageLazy) ? imageLazy.Value : Color.white;
			Color textColor = _glowTextColorDictionary.TryGetValue(displayType, out Lazy<Color> textLazy) ? textLazy.Value : Color.white;
			tapEffect.Setup(ButtonTypeName, imageColor, textColor);
		}

		public void SetImage(DisplayTypeEnum displayTypeEnum)
		{
			displayType = displayTypeEnum;
			SetBaseImage();
			SetGlowCoverColor();
		}

		public void SetPrimary()
		{
			SetImage(DisplayTypeEnum.Primary);
		}

		private void SetSize()
		{
			if (rectTransform != null)
			{
				rectTransform.sizeDelta = GetSizeDelta();
			}
			if (adjustTextSize && customTextMesh != null)
			{
				customTextMesh.fontSize = GetFontSize();
			}
		}

		public void SetSize(SizeEnum _size)
		{
			this.size = _size;
			SetSize();
		}

		public void SetText(string text)
		{
			if (customTextMesh != null)
			{
				customTextMesh.SetText(text);
			}
		}

		public void SetDefaultFontDB()
		{
			customTextMesh?.SetDefaultFontDB();
		}

		public void SetDefaultFontEB()
		{
			customTextMesh?.SetDefaultFontEB();
		}

		public void SetWordingKey(string wordingKey)
		{
			customTextMesh?.SetWordingKey(wordingKey);
		}

		public void RemoveAllAndAddListener(Action action)
		{
			if (customButton == null)
			{
				customButton = GetComponent<CustomButton>();
			}
			if (customButton == null)
			{
				return;
			}
			customButton.onClick.RemoveAllListeners();
			if (action != null)
			{
				customButton.onClick.AddListener(() => action());
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
			if (customButton != null)
			{
				customButton.interactable = isActive;
			}
		}

		public void RemoveTapEffect()
		{
			if (tapEffect != null)
			{
				tapEffect.enabled = false;
			}
		}

		public void Enable()
		{
			SetCustomButtonActive(true);
			if (coverImage != null)
			{
				coverImage.Hide();
			}
		}

		public void Disable()
		{
			SetCustomButtonActive(false);
			if (coverImage != null)
			{
				coverImage.Show();
			}
		}

		public UIPartsCommonButton()
		{
		}

		static UIPartsCommonButton()
		{
			_glowImageColorDictionary = new Dictionary<DisplayTypeEnum, Lazy<Color>>
			{
				{ DisplayTypeEnum.Normal, new Lazy<Color>(() => UnityEngine.ColorUtility.TryParseHtmlString("#A1F4EB", out Color color) ? color : Color.white) },
				{ DisplayTypeEnum.Primary, new Lazy<Color>(() => UnityEngine.ColorUtility.TryParseHtmlString("#E4FCF8", out Color color) ? color : Color.white) },
				{ DisplayTypeEnum.Special, new Lazy<Color>(() => UnityEngine.ColorUtility.TryParseHtmlString("#E4FCF8", out Color color) ? color : Color.white) }
			};
			_glowTextColorDictionary = new Dictionary<DisplayTypeEnum, Lazy<Color>>
			{
				{ DisplayTypeEnum.Normal, new Lazy<Color>(() => Color.white) },
				{ DisplayTypeEnum.Primary, new Lazy<Color>(() => UnityEngine.ColorUtility.TryParseHtmlString("#77EEDD", out Color color) ? color : Color.white) },
				{ DisplayTypeEnum.Special, new Lazy<Color>(() => UnityEngine.ColorUtility.TryParseHtmlString("#77EEDD", out Color color) ? color : Color.white) }
			};
			_displayTypeDictionary = new Dictionary<DisplayTypeEnum, string>
			{
				{ DisplayTypeEnum.Normal, "wh" },
				{ DisplayTypeEnum.Primary, "gn" },
				{ DisplayTypeEnum.Special, "or" }
			};
		}
	}
}
