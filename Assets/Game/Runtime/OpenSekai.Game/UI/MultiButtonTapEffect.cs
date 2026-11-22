using UnityEngine;

namespace Sekai.UI
{
	public class MultiButtonTapEffect : ButtonViewInteractionBase
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_tapEffectList")]
		[SerializeField]
		private ButtonViewInteractionBase[] tapEffectList;

		public override void OnButtonViewEnabled()
		{
			ForEachEffect(effect => effect.OnButtonViewEnabled());
		}

		public override void OnButtonViewDisabled()
		{
			ForEachEffect(effect => effect.OnButtonViewDisabled());
		}

		public override void OnPressed()
		{
			ForEachEffect(effect => effect.OnPressed());
		}

		public override void OnReleased()
		{
			ForEachEffect(effect => effect.OnReleased());
		}

		public override void KillFadeTween()
		{
			ForEachEffect(effect => effect.KillFadeTween());
		}

		public override void ResetInteraction()
		{
			ForEachEffect(effect => effect.ResetInteraction());
		}

		private void ForEachEffect(System.Action<ButtonViewInteractionBase> action)
		{
			if (tapEffectList == null)
			{
				return;
			}

			foreach (ButtonViewInteractionBase effect in tapEffectList)
			{
				if (effect != null)
				{
					action(effect);
				}
			}
		}

		public MultiButtonTapEffect()
		{
		}
	}
}
