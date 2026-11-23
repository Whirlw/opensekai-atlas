namespace Sekai.Live
{
	public class LongHoldCombo : HoldPointNote
	{
		internal override bool UsesHoldPointJudgment => true;
		public override bool IsVisible => !HasNativeAttachment;

		public LongHoldCombo()
		{
			Category = NoteCategory.Combo;
		}

		public LongHoldCombo(MusicScoreInfo info, NoteType type, float speedRatio, float _laneOffset)
			: base(info, 0, 0, 0, NoteCategory.Combo, speedRatio, _laneOffset, type)
		{
		}

		public LongHoldCombo(MusicScoreInfo info, NoteType type, int id, float speedRatio, float _laneOffset,
			bool alwaysHeld = false)
			: base(info, id, 0, 0, NoteCategory.Combo, speedRatio, _laneOffset, type, alwaysHeld)
		{
		}

		public override bool IsJudgment(ref LiveTouch touch, float lane) => false;

		protected override void OnSpawnNote()
		{
		}

		protected override void PublishResult()
		{
			// Recycling an unanswered marker must not publish a score or a combo event.
			if (JudgeInfo.result == NoteResult.None)
			{
				return;
			}

			ResultPublication.Publish(this, Callbacks, this, PublishShape.InteriorPoint);
		}

		public override bool AutoJudgment(MusicScoreInfo currentFrameInfo)
		{
			if (State == NoteState.Done || MusicScoreInfo.time > currentFrameInfo.time)
			{
				return false;
			}

			JudgeInfo = (NoteResult.Auto, NoteResultDescription.None);
			State = NoteState.Done;
			return true;
		}
	}
}
