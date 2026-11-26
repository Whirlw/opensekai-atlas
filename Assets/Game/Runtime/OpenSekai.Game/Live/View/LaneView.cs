using OpenSekai.MaterialPacks;
using UnityEngine;
using Sekai.Live;

namespace Sekai
{
	public class LaneView : MonoBehaviour
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_defaultLaneBase")]
		[SerializeField]
		private SpriteRenderer defaultLaneBase;

		[UnityEngine.Serialization.FormerlySerializedAs("_defaultLaneLine")]
		[SerializeField]
		private SpriteRenderer defaultLaneLine;

		[UnityEngine.Serialization.FormerlySerializedAs("_defaultJudgeLine")]
		[SerializeField]
		private SpriteRenderer defaultJudgeLine;

		private SpriteRendererBaseline _baseBaseline;
		private SpriteRendererBaseline _dividerBaseline;
		private SpriteRendererBaseline _judgeBaseline;

		internal Bounds? JudgeLineBounds => defaultJudgeLine != null && defaultJudgeLine.enabled
			? defaultJudgeLine.bounds : (Bounds?)null;

		public void Setup(LiveSettingData liveSetting, LiveSpriteSkin skin = null)
		{
			if (liveSetting == null) return;
			Apply(defaultLaneBase, ref _baseBaseline, MaterialLaneSlots.BASE, liveSetting.LaneTransparent, skin);
			Apply(defaultLaneLine, ref _dividerBaseline, MaterialLaneSlots.DIVIDERS, LiveConfig.JudgeLineAlpha, skin);
			Apply(defaultJudgeLine, ref _judgeBaseline, MaterialLaneSlots.JUDGE_LINE, LiveConfig.JudgeLineAlpha, skin);
		}

		private static void Apply(SpriteRenderer renderer, ref SpriteRendererBaseline baseline, string slot,
			float alpha, LiveSpriteSkin skin)
		{
			if (renderer == null) return;
			if (skin != null || baseline != null)
			{
				baseline ??= new SpriteRendererBaseline(renderer);
				baseline.Draw(renderer, skin?.Resolve(slot, baseline.Sprite), baseline.Sprite, preservePrefabScale: true);
			}
			renderer.color = new Color(1f, 1f, 1f, alpha);
			renderer.enabled = alpha > 0f;
		}
	}
}
