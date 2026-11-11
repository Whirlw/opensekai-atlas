using System;
using System.Collections.Generic;
using Sekai.Live;

namespace OpenSekai.Charts
{
	public sealed class ChartNote
	{
		public enum NoteBaseType
		{
			Base = 0,
			Normal = 1,
			Long = 2,
			Flick = 3,
			FrictionFlick = 4,
			Connection = 5,
			HiddenConnection = 6,
			LongHoldCombo = 7,
			FrictionLong = 8,
			FrictionHideLong = 9,
			Guide = 10,
			Friction = 11,
			FrictionHide = 12,
			GuideEnd = 13,
			GuideHiddenConnection = 14
		}

				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private long _ticks;
		public long ticks
		{
		    get => _ticks;
		    set => _ticks = value;
		}

				private int _laneStart;
		public int laneStart
		{
		    get => _laneStart;
		    set => _laneStart = value;
		}

				private int _laneEnd;
		public int laneEnd
		{
		    get => _laneEnd;
		    set => _laneEnd = value;
		}

		public NoteCategory category;

		public NoteType type;

				private float _speedRatio = 1f;
		public float speedRatio
		{
		    get => _speedRatio;
		    set => _speedRatio = value;
		}

		public NoteLineType noteLineType;

		public NoteBaseType noteBaseType;

				private int _previousConnectionId;
		public int previousConnectionId
		{
		    get => _previousConnectionId;
		    set => _previousConnectionId = value;
		}

				private int _nextConnectionId;
		public int nextConnectionId
		{
		    get => _nextConnectionId;
		    set => _nextConnectionId = value;
		}

		public NoteDirection direction;

				private bool _isSkip;
		public bool isSkip
		{
		    get => _isSkip;
		    set => _isSkip = value;
		}

				private bool _isDecoration;
		public bool isDecoration
		{
		    get => _isDecoration;
		    set => _isDecoration = value;
		}

				private string _guideColor = null;
		public string guideColor
		{
		    get => _guideColor;
		    set => _guideColor = value;
		}


		public ChartNote() { previousConnectionId = nextConnectionId = -1; }
		public ChartNote(int id, long ticks, int laneStart, int laneEnd, NoteCategory category, NoteType type = NoteType.Default, float speedRatio = 1f, NoteLineType noteLineType = NoteLineType.Linear, NoteBaseType noteBaseType = NoteBaseType.Normal, bool isSkip = false, NoteDirection direction = NoteDirection.Default, int previousConnectionId = -1, int nextConnectionId = -1, bool isDecoration = false)
		{
			this.id = id;
			this.ticks = ticks;
			this.laneStart = laneStart;
			this.laneEnd = laneEnd;
			this.category = category;
			this.type = type;
			this.speedRatio = speedRatio;
			this.noteLineType = noteLineType;
			this.noteBaseType = noteBaseType;
			this.previousConnectionId = previousConnectionId;
			this.nextConnectionId = nextConnectionId;
			this.direction = direction;
			this.isSkip = isSkip;
			this.isDecoration = isDecoration;
		}
		public ChartNote Copy() => (ChartNote)MemberwiseClone();
	}
}
