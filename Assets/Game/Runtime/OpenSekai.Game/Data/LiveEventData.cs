namespace Sekai
{
	public class LiveEventData
	{
		public SkillData[] Skills { get; private set; }

		public CutinData[] Cutins { get; private set; }

		public LiveEventData()
		{
			Skills = System.Array.Empty<SkillData>();
			Cutins = System.Array.Empty<CutinData>();
		}

		public LiveEventData(IngameLotterySkill[] skills, IngameComboCutin[] comboCutIns, int deckId, bool isAuto)
			: this()
		{
		}

		public LiveEventData(IngameLotterySkill[] skills, IngameComboCutin[] comboCutIns, int[] deckCardIds, bool isAuto)
			: this()
		{
		}
	}
}
