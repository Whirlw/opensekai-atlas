using LitMotion;
using UnityEngine;
using UnityEngine.UI;
using Sekai;

namespace Sekai.UI
{
	public class GraphicButtonTapEffect : ButtonViewInteractionBase
	{
		[SerializeField]
		protected Graphic _effectGraphic;

		[SerializeField]
		protected Graphic _effectGraphicIcon;

		[Header("Default color (ColorEntry)")]
		[SerializeField]
		protected ColorEntry _defaultColorPalette;

		[SerializeField]
		[Header("Effect color (ColorEntry)")]
		protected ColorEntry _effectColorPalette;

		[Header("Override effect alpha")]
		[SerializeField]
		protected bool _useCustomAlpha;

		[SerializeField]
		[Header("Default override alpha")]
		protected float _customDefaultAlpha;

		[Header("Override effect alpha")]
		[SerializeField]
		protected float _customEffectAlpha;

		[SerializeField]
		private bool _refreshColorWhenAwake;

		protected Color _defaultColor;

		protected Color _effectColor;

		private MotionHandle _effectMotion;

		private MotionHandle _effectIconMotion;

		protected virtual void Awake()
		{
			if (_refreshColorWhenAwake)
			{
				RefreshColor();
			}
			ResetInteraction();
		}

		public void RefreshColor()
		{
			_defaultColor = global::Sekai.PaletteUtility.GetColor(_defaultColorPalette);
			_effectColor = global::Sekai.PaletteUtility.GetColor(_effectColorPalette);

			if (_useCustomAlpha)
			{
				_defaultColor.a = _customDefaultAlpha;
				_effectColor.a = _customEffectAlpha;
			}
		}

		public void SetEffectGraphic(Graphic effectGraphic)
		{
			_effectGraphic = effectGraphic;
			ResetInteraction();
		}

		public override void KillFadeTween()
		{
			_effectMotion.TryCancel();
			_effectIconMotion.TryCancel();
			_effectMotion = default;
			_effectIconMotion = default;
		}

		public override void ResetInteraction()
		{
			KillFadeTween();
			if (_effectGraphic != null)
			{
				_effectGraphic.color = _defaultColor;
			}
			if (_effectGraphicIcon != null)
			{
				_effectGraphicIcon.color = _defaultColor;
			}
		}

		public void SetDefaultColor(Color defaultColor)
		{
			_defaultColor = defaultColor;
			if (_effectGraphic != null)
			{
				_effectGraphic.color = defaultColor;
			}
			if (_effectGraphicIcon != null)
			{
				_effectGraphicIcon.color = defaultColor;
			}
		}

		public void SetEffectColor(Color effectColor)
		{
			_effectColor = effectColor;
		}

		public override void OnPressed()
		{
			Fade(_effectColor, FadeInTime);
		}

		public override void OnReleased()
		{
			Fade(_defaultColor, FadeOutTime);
		}

		private void Fade(Color color, float duration)
		{
			KillFadeTween();
			if (_effectGraphic != null)
			{
				_effectMotion = PlayColor(_effectGraphic, color, duration);
			}
			if (_effectGraphicIcon != null)
			{
				_effectIconMotion = PlayColor(_effectGraphicIcon, color, duration);
			}
		}

		public GraphicButtonTapEffect()
		{
		}
	}
}
