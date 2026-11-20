namespace Sekai.Live
{
	public class GuideEndNote : FrictionHideNote
	{
		public GuideEndNote()
		{
			Category = NoteCategory.GuideEnd;
		}

		public GuideEndNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, LiveBundleBuildData bundleBuildData, NoteType type, float speedRatio, NoteLineType lineType = NoteLineType.Linear)
			: this(info, id, laneStart, laneEnd, category, LiveUtility.GetLaneOffset(category, bundleBuildData), type, speedRatio, lineType) { }

		public GuideEndNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, float laneOffset, NoteType type, float speedRatio, NoteLineType lineType = NoteLineType.Linear)
			: base(info, id, laneStart, laneEnd, category, laneOffset, type, speedRatio, lineType)
		{
		}

		public override void SetParentNote(LongNote note)
		{
			base.SetParentNote(note);
			Category = NoteCategory.Guide;
		}
	}
}
