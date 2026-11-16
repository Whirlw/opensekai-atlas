using System.Collections.Generic;
using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.UI
{
	public class CommonButtonTapEffect : ButtonViewInteractionBase
	{
		[SerializeField]
		private Graphic _buttonImage;

		[SerializeField]
		private CustomImage _glowButtonImage;

		[SerializeField]
		private CustomTextMesh _buttonTextMesh;

		[SerializeField]
		private List<CustomTextMesh> _additionalTextList;

		[SerializeField]
		private bool _autoSetEffectImage;

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
			_defaultButtonColor = _buttonImage != null ? _buttonImage.color : Color.white;
			_defaultTextColor = _buttonTextMesh != null ? _buttonTextMesh.color : Color.white;
			if (_autoSetEffectImage)
			{
				SetGlowImage(buttonTypeName);
			}
		}

		private void SetGlowImage(string buttonTypeName)
		{
			if (_glowButtonImage != null)
			{
				_glowButtonImage.SpriteName = "eff_btn_round_h" + buttonTypeName + "_a70_wh";
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
			if (_buttonImage != null)
			{
				_buttonImageMotion = PlayColor(_buttonImage, buttonColor, duration);
			}
			if (_glowButtonImage != null)
			{
				_glowButtonImageMotion = PlayColor(_glowButtonImage, buttonColor, duration);
			}
			if (_buttonTextMesh != null)
			{
				_textMotion = PlayColor(_buttonTextMesh, textColor, duration);
			}
			if (_additionalTextList != null)
			{
				_additionalTextMotions ??= new List<MotionHandle>();
				foreach (CustomTextMesh text in _additionalTextList)
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
