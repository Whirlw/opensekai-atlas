namespace Sekai.Live
{
	public sealed class NextRushHiddenTickNote : NextRushConnectorTickNote
	{
		public NextRushHiddenTickNote(MusicScoreInfo info, int id, int left, int right, float laneOffset)
			: base(info, id, left, right, NoteType.Default, laneOffset)
		{
			SetNextRushContactWindow();
		}

		public override bool IsVisible => false;

		protected override void OnSpawnNote() { }
		protected override void PublishResult() =>
			ResultPublication.Publish(this, Callbacks, this, PublishShape.InteriorPoint);
	}
}
