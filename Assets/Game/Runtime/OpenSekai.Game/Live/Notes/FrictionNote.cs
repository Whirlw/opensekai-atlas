namespace Sekai.Live
{
	public class FrictionNote : ConnectionNote
	{
		internal override bool UsesContactWindow => true;
		public bool IsTailTraceOwner { get; }
		private readonly bool _isTraceTail;
		private bool UsesTailWindow => _isTraceTail || IsTailTraceOwner || GradesAsBarPart && ParentRole == OpenSekai.Shared.Live.NoteParentRole.Tail;
		internal override float EarlyContactSeconds => UsesTailWindow ? 6.5f / 60f * LiveConfig.PracticeWindowScale : base.EarlyContactSeconds;
		internal override float LateContactSeconds => UsesTailWindow ? 8f / 60f * LiveConfig.PracticeWindowScale : base.LateContactSeconds;

		public override NoteState State
		{
			get => state;
			protected set
			{
				if (value == NoteState.Done && state != NoteState.Done)
				{
					state = NoteState.Done;
					// Publish first, then tell the bar: the same order as every other end note
					// (tail-classes.md §3 R2).
					OnUnSpawnNote();
					NotifyParentFinished();
					return;
				}
				base.State = value;
			}
		}
		public FrictionNote()
		{
			Category = NoteCategory.Friction;
		}

		public FrictionNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, LiveBundleBuildData bundleBuildData, NoteType type, float speedRatio, NoteLineType lineType = NoteLineType.Linear, bool isTailTraceOwner = false, bool isTraceTail = false)
			: this(info, id, laneStart, laneEnd, category, LiveUtility.GetLaneOffset(category, bundleBuildData), type, speedRatio, lineType, isTailTraceOwner, isTraceTail) { }

		public FrictionNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, float laneOffset, NoteType type, float speedRatio, NoteLineType lineType = NoteLineType.Linear, bool isTailTraceOwner = false, bool isTraceTail = false)
			: base(info, id, laneStart, laneEnd, category, type, speedRatio, laneOffset, lineType)
		{
			_isTraceTail = isTraceTail;
			IsTailTraceOwner = isTailTraceOwner;
		}
	}
}
