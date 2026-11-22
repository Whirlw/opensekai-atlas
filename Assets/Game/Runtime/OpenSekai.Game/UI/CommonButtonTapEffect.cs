using System.Collections.Generic;
using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.UI
{
	public class CommonButtonTapEffect : ButtonViewInteractionBase
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_buttonImage")]
		[SerializeField]
		private Graphic buttonImage;

		[UnityEngine.Serialization.FormerlySerializedAs("_glowButtonImage")]
		[SerializeField]
		private CustomImage glowButtonImage;

		[UnityEngine.Serialization.FormerlySerializedAs("_buttonTextMesh")]
		[SerializeField]
		private CustomTextMesh buttonTextMesh;

		[UnityEngine.Serialization.FormerlySerializedAs("_additionalTextList")]
		[SerializeField]
		private List<CustomTextMesh> additionalTextList;

		[UnityEngine.Serialization.FormerlySerializedAs("_autoSetEffectImage")]
		[SerializeField]
		private bool autoSetEffectImage;

		private Color _defaultButtonColor;

		private const string DEFAULT_TEXT_COLOR_CODE = "#444466";

		private Color _effectButtonColor;

		private Color _defaultTextColor;

		private Color _effectTextColor;

		private MotionHandle _buttonImageMotion;

		private MotionHandle _glowButtonImageMotion;

		private MotionHandle _textMotion;

		private List<MotionHandle> _additionalTextMotions;

		public void Setup(string buttonTypeName, Color effectButtonColor, Color effectTextColor)
		{
			_effectButtonColor = effectButtonColor;
			_effectTextColor = effectTextColor;
			_defaultButtonColor = buttonImage != null ? buttonImage.color : Color.white;
			_defaultTextColor = buttonTextMesh != null ? buttonTextMesh.color : Color.white;
			if (autoSetEffectImage)
			{
				SetGlowImage(buttonTypeName);
			}
		}

		private void SetGlowImage(string buttonTypeName)
		{
			if (glowButtonImage != null)
			{
				glowButtonImage.SpriteName = "eff_btn_round_h" + buttonTypeName + "_a70_wh";
			}
		}

		public override void OnPressed()
		{
			Fade(_effectButtonColor, _effectTextColor, FadeInTime);
		}

		public override void OnReleased()
		{
			Fade(_defaultButtonColor, _defaultTextColor, FadeOutTime);
		}

		public override void KillFadeTween()
		{
			_buttonImageMotion.TryCancel();
			_glowButtonImageMotion.TryCancel();
			_textMotion.TryCancel();
			_buttonImageMotion = default;
			_glowButtonImageMotion = default;
			_textMotion = default;
			if (_additionalTextMotions == null)
			{
				return;
			}
			foreach (MotionHandle motion in _additionalTextMotions)
			{
				motion.TryCancel();
			}
			_additionalTextMotions.Clear();
		}

		private void Fade(Color buttonColor, Color textColor, float duration)
		{
			KillFadeTween();
			if (buttonImage != null)
			{
				_buttonImageMotion = PlayColor(buttonImage, buttonColor, duration);
			}
			if (glowButtonImage != null)
			{
				_glowButtonImageMotion = PlayColor(glowButtonImage, buttonColor, duration);
			}
			if (buttonTextMesh != null)
			{
				_textMotion = PlayColor(buttonTextMesh, textColor, duration);
			}
			if (additionalTextList != null)
			{
				_additionalTextMotions ??= new List<MotionHandle>();
				foreach (CustomTextMesh text in additionalTextList)
				{
					if (text != null)
					{
						_additionalTextMotions.Add(PlayColor(text, textColor, duration));
					}
				}
			}
		}

		public CommonButtonTapEffect()
		{
		}
	}
}
