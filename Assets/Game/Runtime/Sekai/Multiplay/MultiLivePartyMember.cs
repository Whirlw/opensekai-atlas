using System;
using Sekai.MultiLive;
using Sekai;

namespace Sekai.Multiplay
{
	[Serializable]
	public struct MultiLivePartyMember
	{
		public int Index;

		public int ActorNum;

		public string UserId;

		public string UserName;

		public int CardId;

		public int CardLv;

		public int CardSkillLv;

		public int CardMasterRank;

		public UnitType CostumeUnitType;

		public int HairLiveCostumeId;

		public int BodyLiveCostumeId;

		public int AccessoryLiveCostumeId;

		public int TotalPowerIncludeBuff;

		public bool IsTraining;

		public string DefaultImage;

		public int[] SubCardIds;

		public int[] SubCardSkillLv;

		public string[] SubCardImages;

		public RoomUserHonorInfo MainHonor;

		public RoomUserHonorInfo[] SubHonors;

		public string Difficulty;

		public MultiLiveMultiplayConst.PartyMemberConnectStatus ConnectStatus;

		public FriendRequestStatus FriendRequestStatus;

		public MemberCharacterRank[] MemberCharacterRank;

		public int PlayerFrameId;

		public string CustomScoreId;

		public void Reset()
		{
			throw null;
		}

		public void Setup(PlayerInfo player)
		{
			throw null;
		}
	}
}
