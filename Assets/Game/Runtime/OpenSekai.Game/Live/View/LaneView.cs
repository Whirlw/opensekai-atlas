using UnityEngine;
using Sekai.Live;

namespace Sekai
{
	public class LaneView : MonoBehaviour
	{
		[SerializeField]
		private SpriteRenderer _defaultLaneBase;

		[SerializeField]
		private SpriteRenderer _defaultLaneLine;

		[SerializeField]
		private SpriteRenderer _defaultJudgeLine;

		public void Setup(LiveSettingData liveSetting)
		{
			if (liveSetting == null)
			{
				return;
			}
			float laneAlpha = liveSetting.LaneTransparent;
			float judgeLineAlpha = LiveConfig.JudgeLineAlpha;
			if (_defaultLaneBase != null)
			{
				_defaultLaneBase.color = new Color(1f, 1f, 1f, laneAlpha);
				_defaultLaneBase.enabled = laneAlpha > 0f;
			}
			if (_defaultLaneLine != null)
			{
				_defaultLaneLine.color = new Color(1f, 1f, 1f, judgeLineAlpha);
				_defaultLaneLine.enabled = judgeLineAlpha > 0f;
			}
			if (_defaultJudgeLine != null)
			{
				_defaultJudgeLine.color = new Color(1f, 1f, 1f, judgeLineAlpha);
				_defaultJudgeLine.enabled = judgeLineAlpha > 0f;
			}
		}

		public LaneView()
		{
		}
	}
}
