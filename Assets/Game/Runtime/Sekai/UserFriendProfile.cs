using Sekai.ApiData;

namespace Sekai
{
	public class UserFriendProfile
	{
				private string _name;
		public string name
		{
		    get => _name;
		    set => _name = value;
		}

				private UserProfile _userProfile;
		public UserProfile userProfile
		{
		    get => _userProfile;
		    set => _userProfile = value;
		}

				private UserProfileHonor[] _userProfileHonors;
		public UserProfileHonor[] userProfileHonors
		{
		    get => _userProfileHonors;
		    set => _userProfileHonors = value;
		}

				private UserCard _userCard;
		public UserCard userCard
		{
		    get => _userCard;
		    set => _userCard = value;
		}

				private UserHonorMission[] _userHonorMissions;
		public UserHonorMission[] userHonorMissions
		{
		    get => _userHonorMissions;
		    set => _userHonorMissions = value;
		}

				private UserPlayerFrame[] _userPlayerFrames;
		public UserPlayerFrame[] userPlayerFrames
		{
		    get => _userPlayerFrames;
		    set => _userPlayerFrames = value;
		}

				private bool _isMysekaiOwnerAcceptVisitForFriend;
		public bool isMysekaiOwnerAcceptVisitForFriend
		{
		    get => _isMysekaiOwnerAcceptVisitForFriend;
		    set => _isMysekaiOwnerAcceptVisitForFriend = value;
		}

		public UserFriendProfile()
		{
			throw null;
		}
	}
}
