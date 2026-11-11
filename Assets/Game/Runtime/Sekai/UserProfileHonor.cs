namespace Sekai
{
	public class UserProfileHonor
	{
				private int _seq;
		public int seq
		{
		    get => _seq;
		    set => _seq = value;
		}

				private string _profileHonorType;
		public string profileHonorType
		{
		    get => _profileHonorType;
		    set => _profileHonorType = value;
		}

				private int _honorId;
		public int honorId
		{
		    get => _honorId;
		    set => _honorId = value;
		}

				private string _bondsHonorViewType;
		public string bondsHonorViewType
		{
		    get => _bondsHonorViewType;
		    set => _bondsHonorViewType = value;
		}

				private int _bondsHonorWordId;
		public int bondsHonorWordId
		{
		    get => _bondsHonorWordId;
		    set => _bondsHonorWordId = value;
		}

				private int _honorLevel;
		public int honorLevel
		{
		    get => _honorLevel;
		    set => _honorLevel = value;
		}

		public HonorType Type
		{
			get
			{
				throw null;
			}
		}

		public HonorViewType BondsViewType
		{
			get
			{
				throw null;
			}
		}

		public bool useUnitVirtualSinger
		{
			get
			{
				throw null;
			}
		}

		public bool IsEmpty
		{
			get
			{
				throw null;
			}
		}

		public UserProfileHonor()
		{
		}

		public UserProfileHonor(int seq, int honorId, int honorLevel)
		{
			throw null;
		}

		public UserProfileHonor(int seq)
		{
			throw null;
		}

		public UserProfileHonor(UserBondsHonor profileDataUserHonor)
		{
			throw null;
		}

		public UserProfileHonor(int seq, int honorId, int honorLevel, int wordId, bool reverse, bool useUnitVirtualSinger)
		{
			throw null;
		}
	}
}
