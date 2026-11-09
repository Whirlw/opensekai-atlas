
namespace Sekai
{
	public class MasterHonorGroup
	{
		public const string TYPE_CHARACTER = "character";

		public const string TYPE_ACHIEVEMENT = "achievement";

		public const string TYPE_EVENT = "event";

		public const string TYPE_BONDS = "bonds";

		public const string TYPE_RANK_LIVE = "rank_match";

		public const string TYPE_BIRTHDAY = "birthday";

				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private string _honorType;
		public string honorType
		{
		    get => _honorType;
		    set => _honorType = value;
		}

				private string _name;
		public string name
		{
		    get => _name;
		    set => _name = value;
		}

				private string _backgroundAssetbundleName;
		public string backgroundAssetbundleName
		{
		    get => _backgroundAssetbundleName;
		    set => _backgroundAssetbundleName = value;
		}

				private string _frameName;
		public string frameName
		{
		    get => _frameName;
		    set => _frameName = value;
		}

				private string _pronunciation;
		public string pronunciation
		{
		    get => _pronunciation;
		    set => _pronunciation = value;
		}

		public MasterHonorGroup()
		{
			throw null;
		}
	}
}
