using System.Runtime.CompilerServices;
using Sekai;

namespace Sekai.Honor
{
	public class HonorInfo
	{
				private UserHonorMission _userHonorMission;
		public UserHonorMission userHonorMission
		{
		    get => _userHonorMission;
		    set => _userHonorMission = value;
		}

		public UserProfileHonor ProfileHonor
		{
			[CompilerGenerated]
			get
			{
				throw null;
			}
		}

		public bool IsValid
		{
			get
			{
				throw null;
			}
		}

		public HonorInfo(UserProfileHonor userProfileHonor, UserHonorMission userHonorMission)
		{
			throw null;
		}
	}
}
