using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MessagePack;

namespace Sekai
{
	public class MasterHonor : IMessagePackSerializationCallbackReceiver
	{
		public enum HonorType
		{
			@default = 0,
			rank_match_grade = 1
		}

				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private int _seq;
		public int seq
		{
		    get => _seq;
		    set => _seq = value;
		}

				private int _groupId;
		public int groupId
		{
		    get => _groupId;
		    set => _groupId = value;
		}

				private string _honorRarity;
		public string honorRarity
		{
		    get => _honorRarity;
		    set => _honorRarity = value;
		}

				private string _name;
		public string name
		{
		    get => _name;
		    set => _name = value;
		}

				private string _assetbundleName;
		public string assetbundleName
		{
		    get => _assetbundleName;
		    set => _assetbundleName = value;
		}

				private string _description;
		public string description
		{
		    get => _description;
		    set => _description = value;
		}

				private MasterHonorLevel[] _levels;
		public MasterHonorLevel[] levels
		{
		    get => _levels;
		    set => _levels = value;
		}

				private int _honorTypeId;
		public int honorTypeId
		{
		    get => _honorTypeId;
		    set => _honorTypeId = value;
		}

		public string honorMissionType;

		public static IReadOnlyList<HonorMissionType> LiveMasterHonorMissionTypes
		{
			[CompilerGenerated]
			get
			{
				throw null;
			}
		}

		public HonorMissionType HonorMissionType
		{
			get
			{
				throw null;
			}
		}

		public HonorRarity? Rarity
		{
			get
			{
				throw null;
			}
		}

		public bool ExistsBonus
		{
			get
			{
				throw null;
			}
		}

		public bool ExistsLevels
		{
			get
			{
				throw null;
			}
		}

		public int GetBonus(int level)
		{
			throw null;
		}

		public string GetLevelDescription(int level)
		{
			throw null;
		}

		public bool IsLiveMasterHonor()
		{
			throw null;
		}

		public void OnBeforeSerialize()
		{
			throw null;
		}

		public void OnAfterDeserialize()
		{
			throw null;
		}

		public MasterHonor()
		{
			throw null;
		}

		static MasterHonor()
		{
			throw null;
		}
	}
}
