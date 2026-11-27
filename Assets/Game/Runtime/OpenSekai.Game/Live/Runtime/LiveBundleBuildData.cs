using OpenSekai.Shared.Scoring;
using UnityEngine;

namespace Sekai.Live
{
	/// <summary>Captures Unity asset and display values before a chart is built on a worker.</summary>
	public readonly struct ChartConversionSettings
	{
		private readonly JudgeTuning _tuning;
		public readonly float FlickPixels;
		public readonly int LongNoteComboBeat;

		public readonly bool ConstantScroll;

		public ChartConversionSettings(LiveBundleBuildData config, bool constantScroll = false)
		{
			ConstantScroll = constantScroll;
			_tuning = LiveBundleBuildData.GetJudgeTuning(config);
			FlickPixels = _tuning.FlickDistanceInPixels(Screen.dpi);
			LongNoteComboBeat = config == null ? 0 : config.LongNoteComboBeat;
		}

		public float LaneOffsetFor(NoteCategory category) => _tuning.LaneOffsetFor(category);
	}

	public class LiveBundleBuildData : ScriptableObject
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_judgmentOffsetX")]
		[SerializeField]
		private float judgmentOffsetX;

		[UnityEngine.Serialization.FormerlySerializedAs("_judgmentOffsetY")]
		[SerializeField]
		private float judgmentOffsetY;

		[UnityEngine.Serialization.FormerlySerializedAs("_longNoteComboBeat")]
		[SerializeField]
		private int longNoteComboBeat;

		[UnityEngine.Serialization.FormerlySerializedAs("_life")]
		[SerializeField]
		private int life;

		[UnityEngine.Serialization.FormerlySerializedAs("_maxLife")]
		[SerializeField]
		private int maxLife;

		[UnityEngine.Serialization.FormerlySerializedAs("_dyingLife")]
		[SerializeField]
		private int dyingLife;

		[UnityEngine.Serialization.FormerlySerializedAs("_cutinDelayTime")]
		[SerializeField]
		private float cutinDelayTime;

		[UnityEngine.Serialization.FormerlySerializedAs("_normalNoteOffsetX")]
		[Tooltip("Normal notes, long-note end")]
		[SerializeField]
		private float normalNoteOffsetX;

		[UnityEngine.Serialization.FormerlySerializedAs("_flickNoteOffsetX")]
		[Tooltip("Flick start, flick end")]
		[SerializeField]
		private float flickNoteOffsetX;

		[UnityEngine.Serialization.FormerlySerializedAs("_longNoteOffsetX")]
		[Tooltip("Long-note start")]
		[SerializeField]
		private float longNoteOffsetX;

		[UnityEngine.Serialization.FormerlySerializedAs("_flickDistance")]
		[SerializeField]
		private float flickDistance;

		private readonly float _initLaneOffsetX;

		public float JudgmentOffsetX
		{
			get
			{
				return judgmentOffsetX;
			}
		}

		public float JudgmentOffsetY
		{
			get
			{
				return judgmentOffsetY;
			}
		}

		public int LongNoteComboBeat
		{
			get
			{
				return longNoteComboBeat;
			}
		}

		public int Life
		{
			get
			{
				return life;
			}
		}

		public int MaxLife
		{
			get
			{
				return maxLife;
			}
		}

		public int DyingLife
		{
			get
			{
				return dyingLife;
			}
		}

		public int CutinDelayTime
		{
			get
			{
				return Mathf.RoundToInt(cutinDelayTime);
			}
		}

		public float NormalNoteOffsetX
		{
			get
			{
				return normalNoteOffsetX;
			}
			set
			{
				normalNoteOffsetX = value;
			}
		}

		public float FlickNoteOffsetX
		{
			get
			{
				return flickNoteOffsetX;
			}
			set
			{
				flickNoteOffsetX = value;
			}
		}

		public float LongNoteOffsetX
		{
			get
			{
				return longNoteOffsetX;
			}
			set
			{
				longNoteOffsetX = value;
			}
		}

		public float FlickDistance
		{
			get
			{
				return flickDistance;
			}
			set
			{
				flickDistance = value;
			}
		}

		public LiveBundleBuildData()
		{
		}

		public static JudgeTuning GetJudgeTuning(LiveBundleBuildData data)
		{
			if (data == null)
			{
				return default;
			}

			return new JudgeTuning(
				data.NormalNoteOffsetX, data.LongNoteOffsetX, data.FlickNoteOffsetX, data.FlickDistance);
		}
	}
}
