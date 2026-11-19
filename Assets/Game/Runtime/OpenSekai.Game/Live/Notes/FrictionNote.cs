namespace Sekai.Live
{
	public class FrictionNote : ConnectionNote
	{
		internal override bool UsesContactWindow => true;
		public bool IsTailTraceOwner { get; }
		internal override float EarlyContactSeconds => IsTailTraceOwner ? 6.5f / 60f : base.EarlyContactSeconds;
		internal override float LateContactSeconds => IsTailTraceOwner ? 8f / 60f : base.LateContactSeconds;

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

		public FrictionNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, LiveBundleBuildData bundleBuildData, NoteType type, float speedRatio, NoteLineType lineType = NoteLineType.Linear, bool isTailTraceOwner = false)
			: base(info, id, laneStart, laneEnd, category, type, speedRatio, LiveUtility.GetLaneOffset(category, bundleBuildData), lineType)
		{
			IsTailTraceOwner = isTailTraceOwner;
		}
	}
}
