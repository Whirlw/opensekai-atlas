namespace Sekai.Live
{
	public class GuideHiddenConnectionNote : HiddenConnectionNote
	{
		public GuideHiddenConnectionNote()
		{
			Category = NoteCategory.GuideHidden;
		}

		public GuideHiddenConnectionNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, LiveBundleBuildData bundleBuildData, NoteType type, float speedRatio, NoteLineType lineType = NoteLineType.Linear)
			: this(info, id, laneStart, laneEnd, category, LiveUtility.GetLaneOffset(category, bundleBuildData), type, speedRatio, lineType) { }

		public GuideHiddenConnectionNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, float laneOffset, NoteType type, float speedRatio, NoteLineType lineType = NoteLineType.Linear)
			: base(info, id, laneStart, laneEnd, category, type, speedRatio, laneOffset, lineType)
		{
		}
	}
}
