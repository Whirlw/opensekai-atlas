namespace Sekai
{
	public class LiveDeckData
	{
		public class UserData
		{
			public int DeckId { get; set; }
			public string DeckName { get; set; }
			public int Leader { get; set; }
			public int SubLeader { get; set; }
			public int Member1 { get; set; }
			public int Member2 { get; set; }
			public int Member3 { get; set; }
			public int Member4 { get; set; }
			public int Member5 { get; set; }
			public int CharacterId { get; set; }

			public UserData()
			{
				DeckName = string.Empty;
			}

			public UserData(int deckId)
			{
				DeckId = deckId;
				DeckName = string.Empty;
			}
		}

		public UserData UserInfo { get; set; }

		public LiveDeckMember[] Members { get; private set; }

		public LiveDeckMember[] FormationMembers { get; set; }

		public int TotalPowerIncludeBuff { get; set; }

		public LiveDeckData()
		{
			Members = System.Array.Empty<LiveDeckMember>();
			FormationMembers = Members;
		}

		public LiveDeckData(int deckId, bool is6MemberMv = false)
			: this()
		{
			UserInfo = new UserData(deckId);
		}

		public LiveDeckData(int[] deckCardIds)
			: this()
		{
		}

		public void AttachFormationInfo(int[] formationIndexes)
		{
			FormationMembers = Members;
		}
	}
}
