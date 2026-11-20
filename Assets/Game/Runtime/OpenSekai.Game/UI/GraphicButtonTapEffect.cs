using LitMotion;
using UnityEngine;
using UnityEngine.UI;
using Sekai;

namespace Sekai.UI
{
	public class GraphicButtonTapEffect : ButtonViewInteractionBase
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_effectGraphic")]
		[SerializeField]
		protected Graphic effectGraphic;

		[UnityEngine.Serialization.FormerlySerializedAs("_effectGraphicIcon")]
		[SerializeField]
		protected Graphic effectGraphicIcon;

		[UnityEngine.Serialization.FormerlySerializedAs("_defaultColorPalette")]
		[Header("Default color (ColorEntry)")]
		[SerializeField]
		protected ColorEntry defaultColorPalette;

		[UnityEngine.Serialization.FormerlySerializedAs("_effectColorPalette")]
		[SerializeField]
		[Header("Effect color (ColorEntry)")]
		protected ColorEntry effectColorPalette;

		[UnityEngine.Serialization.FormerlySerializedAs("_useCustomAlpha")]
		[Header("Override effect alpha")]
		[SerializeField]
		protected bool useCustomAlpha;

		[UnityEngine.Serialization.FormerlySerializedAs("_customDefaultAlpha")]
		[SerializeField]
		[Header("Default override alpha")]
		protected float customDefaultAlpha;

		[UnityEngine.Serialization.FormerlySerializedAs("_customEffectAlpha")]
		[Header("Override effect alpha")]
		[SerializeField]
		protected float customEffectAlpha;

		[UnityEngine.Serialization.FormerlySerializedAs("_refreshColorWhenAwake")]
		[SerializeField]
		private bool refreshColorWhenAwake;

		protected Color _defaultColor;

		protected Color _effectColor;

		private MotionHandle _effectMotion;

		private MotionHandle _effectIconMotion;

		protected virtual void Awake()
		{
			if (refreshColorWhenAwake)
			{
				RefreshColor();
			}
			ResetInteraction();
		}

		public void RefreshColor()
		{
			_defaultColor = global::Sekai.PaletteUtility.GetColor(defaultColorPalette);
			_effectColor = global::Sekai.PaletteUtility.GetColor(effectColorPalette);

			if (useCustomAlpha)
			{
				_defaultColor.a = customDefaultAlpha;
				_effectColor.a = customEffectAlpha;
			}
		}

		public void SetEffectGraphic(Graphic effectGraphic)
		{
			this.effectGraphic = effectGraphic;
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
			if (effectGraphic != null)
			{
				effectGraphic.color = _defaultColor;
			}
			if (effectGraphicIcon != null)
			{
				effectGraphicIcon.color = _defaultColor;
			}
		}

		public void SetDefaultColor(Color defaultColor)
		{
			_defaultColor = defaultColor;
			if (effectGraphic != null)
			{
				effectGraphic.color = defaultColor;
			}
			if (effectGraphicIcon != null)
			{
				effectGraphicIcon.color = defaultColor;
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
			if (effectGraphic != null)
			{
				_effectMotion = PlayColor(effectGraphic, color, duration);
			}
			if (effectGraphicIcon != null)
			{
				_effectIconMotion = PlayColor(effectGraphicIcon, color, duration);
			}
		}

		public GraphicButtonTapEffect()
		{
		}
	}
}
