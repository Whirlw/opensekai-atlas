namespace Sekai
{
	public class LiveDeckMember
	{
		public int CardId { get; set; }

		public bool IsLeader { get; set; }

		public bool IsSubLeader { get; set; }

		public int FormationIndex { get; set; }

		public bool UseDoubleSkill => false;

		public LiveDeckMember(int cardId)
		{
			CardId = cardId;
		}
	}
}
