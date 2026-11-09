
namespace Sekai
{
	public class MasterHonorLevel
	{
				private int _honorId;
		public int honorId
		{
		    get => _honorId;
		    set => _honorId = value;
		}

				private int _level;
		public int level
		{
		    get => _level;
		    set => _level = value;
		}

				private int _bonus;
		public int bonus
		{
		    get => _bonus;
		    set => _bonus = value;
		}

				private string _description;
		public string description
		{
		    get => _description;
		    set => _description = value;
		}

				private string _assetbundleName;
		public string assetbundleName
		{
		    get => _assetbundleName;
		    set => _assetbundleName = value;
		}

		public string honorRarity;

		public HonorRarity HonorRarity
		{
			get
			{
				throw null;
			}
		}

		public MasterHonorLevel()
		{
			throw null;
		}
	}
}
