using UnityEngine;

namespace Sekai.Live
{
	public class LiveBundleBuildData : ScriptableObject
	{
		[SerializeField]
		private float _judgmentOffsetX;

		[SerializeField]
		private float _judgmentOffsetY;

		[SerializeField]
		private int _longNoteComboBeat;

		[SerializeField]
		private int _life;

		[SerializeField]
		private int _maxLife;

		[SerializeField]
		private int _dyingLife;

		[SerializeField]
		private float _cutinDelayTime;

		[Tooltip("Normal notes, long-note end")]
		[SerializeField]
		private float _normalNoteOffsetX;

		[Tooltip("Flick start, flick end")]
		[SerializeField]
		private float _flickNoteOffsetX;

		[Tooltip("Long-note start")]
		[SerializeField]
		private float _longNoteOffsetX;

		[SerializeField]
		private float _flickDistance;

		private readonly float _initLaneOffsetX;

		public float JudgmentOffsetX
		{
			get
			{
				return _judgmentOffsetX;
			}
		}

		public float JudgmentOffsetY
		{
			get
			{
				return _judgmentOffsetY;
			}
		}

		public int LongNoteComboBeat
		{
			get
			{
				return _longNoteComboBeat;
			}
		}

		public int Life
		{
			get
			{
				return _life;
			}
		}

		public int MaxLife
		{
			get
			{
				return _maxLife;
			}
		}

		public int DyingLife
		{
			get
			{
				return _dyingLife;
			}
		}

		public int CutinDelayTime
		{
			get
			{
				return Mathf.RoundToInt(_cutinDelayTime);
			}
		}

		public float NormalNoteOffsetX
		{
			get
			{
				return _normalNoteOffsetX;
			}
			set
			{
				_normalNoteOffsetX = value;
			}
		}

		public float FlickNoteOffsetX
		{
			get
			{
				return _flickNoteOffsetX;
			}
			set
			{
				_flickNoteOffsetX = value;
			}
		}

		public float LongNoteOffsetX
		{
			get
			{
				return _longNoteOffsetX;
			}
			set
			{
				_longNoteOffsetX = value;
			}
		}

		public float FlickDistance
		{
			get
			{
				return _flickDistance;
			}
			set
			{
				_flickDistance = value;
			}
		}

		public LiveBundleBuildData()
		{
		}
	}
}
