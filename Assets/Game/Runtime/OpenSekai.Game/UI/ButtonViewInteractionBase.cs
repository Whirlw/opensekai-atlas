using LitMotion;
using LitMotion.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.UI
{
	public abstract class ButtonViewInteractionBase : MonoBehaviour
	{
		private const float FADE_IN_TIME = 0.1f;

		private const float FADE_OUT_TIME = 0.4f;

		protected virtual float FadeInTime
		{
			get
			{
				return FADE_IN_TIME;
			}
		}

		protected virtual float FadeOutTime
		{
			get
			{
				return FADE_OUT_TIME;
			}
		}

		public virtual void OnButtonViewEnabled()
		{
		}

		public virtual void OnButtonViewDisabled()
		{
			KillFadeTween();
		}

		public virtual void OnPressed()
		{
		}

		public virtual void OnReleased()
		{
		}

		public virtual void KillFadeTween()
		{
		}

		public virtual void ResetInteraction()
		{
		}

		protected static MotionHandle PlayColor(Graphic graphic, Color color, float duration)
		{
			return LMotion.Create(graphic.color, color, duration)
				.WithEase(Ease.OutQuad)
				.BindToColor(graphic);
		}

		protected virtual void OnDisable()
		{
			KillFadeTween();
		}

		protected virtual void OnDestroy()
		{
			KillFadeTween();
		}

		protected ButtonViewInteractionBase()
		{
		}
	}
}
