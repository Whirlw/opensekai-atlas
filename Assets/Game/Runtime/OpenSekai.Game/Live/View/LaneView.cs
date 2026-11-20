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

		public void Setup(LiveSettingData liveSetting)
		{
			if (liveSetting == null)
			{
				return;
			}
			float laneAlpha = liveSetting.LaneTransparent;
			float judgeLineAlpha = LiveConfig.JudgeLineAlpha;
			if (defaultLaneBase != null)
			{
				defaultLaneBase.color = new Color(1f, 1f, 1f, laneAlpha);
				defaultLaneBase.enabled = laneAlpha > 0f;
			}
			if (defaultLaneLine != null)
			{
				defaultLaneLine.color = new Color(1f, 1f, 1f, judgeLineAlpha);
				defaultLaneLine.enabled = judgeLineAlpha > 0f;
			}
			if (defaultJudgeLine != null)
			{
				defaultJudgeLine.color = new Color(1f, 1f, 1f, judgeLineAlpha);
				defaultJudgeLine.enabled = judgeLineAlpha > 0f;
			}
		}

		public LaneView()
		{
		}
	}
}
