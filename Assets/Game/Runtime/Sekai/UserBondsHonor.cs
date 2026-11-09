
namespace Sekai
{
	public class UserBondsHonor
	{
				private int _bondsHonorId;
		public int bondsHonorId
		{
		    get => _bondsHonorId;
		    set => _bondsHonorId = value;
		}

				private int _level;
		public int level
		{
		    get => _level;
		    set => _level = value;
		}

				private long _obtainedAt;
		public long obtainedAt
		{
		    get => _obtainedAt;
		    set => _obtainedAt = value;
		}

				private string _description;
		public string description
		{
		    get => _description;
		    set => _description = value;
		}

		public UserBondsHonor()
		{
		}
	}
}
