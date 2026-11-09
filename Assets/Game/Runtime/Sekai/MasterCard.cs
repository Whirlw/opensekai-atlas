using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MessagePack;

namespace Sekai
{
	public class MasterCard : IMessagePackSerializationCallbackReceiver
	{
		public const int INVALID_CARD_ID = -1;

		public const string CARD_ATTR_1 = "cute";

		public const string CARD_ATTR_2 = "cool";

		public const string CARD_ATTR_3 = "pure";

		public const string CARD_ATTR_4 = "happy";

		public const string CARD_ATTR_5 = "mysterious";

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

				private int _characterId;
		public int characterId
		{
		    get => _characterId;
		    set => _characterId = value;
		}

				private string _cardRarityType;
		public string cardRarityType
		{
		    get => _cardRarityType;
		    set => _cardRarityType = value;
		}

				private int _maxPower;
		public int maxPower
		{
		    get => _maxPower;
		    set => _maxPower = value;
		}

				private int _specialTrainingPower1BonusFixed;
		public int specialTrainingPower1BonusFixed
		{
		    get => _specialTrainingPower1BonusFixed;
		    set => _specialTrainingPower1BonusFixed = value;
		}

				private int _specialTrainingPower2BonusFixed;
		public int specialTrainingPower2BonusFixed
		{
		    get => _specialTrainingPower2BonusFixed;
		    set => _specialTrainingPower2BonusFixed = value;
		}

				private int _specialTrainingPower3BonusFixed;
		public int specialTrainingPower3BonusFixed
		{
		    get => _specialTrainingPower3BonusFixed;
		    set => _specialTrainingPower3BonusFixed = value;
		}

				private string _attr;
		public string attr
		{
		    get => _attr;
		    set => _attr = value;
		}

				private string _supportUnit;
		public string supportUnit
		{
		    get => _supportUnit;
		    set => _supportUnit = value;
		}

				private int _skillId;
		public int skillId
		{
		    get => _skillId;
		    set => _skillId = value;
		}

				private string _cardSkillName;
		public string cardSkillName
		{
		    get => _cardSkillName;
		    set => _cardSkillName = value;
		}

				private int? _specialTrainingSkillId;
		public int? specialTrainingSkillId
		{
		    get => _specialTrainingSkillId;
		    set => _specialTrainingSkillId = value;
		}

				private string _specialTrainingSkillName;
		public string specialTrainingSkillName
		{
		    get => _specialTrainingSkillName;
		    set => _specialTrainingSkillName = value;
		}

				private string _prefix;
		public string prefix
		{
		    get => _prefix;
		    set => _prefix = value;
		}

				private string _assetbundleName;
		public string assetbundleName
		{
		    get => _assetbundleName;
		    set => _assetbundleName = value;
		}

				private string _gachaPhrase;
		public string gachaPhrase
		{
		    get => _gachaPhrase;
		    set => _gachaPhrase = value;
		}

				private string _flavorText;
		public string flavorText
		{
		    get => _flavorText;
		    set => _flavorText = value;
		}

				private long _releaseAt;
		public long releaseAt
		{
		    get => _releaseAt;
		    set => _releaseAt = value;
		}

				private MasterCardParameter[] _cardParameters;
		public MasterCardParameter[] cardParameters
		{
		    get => _cardParameters;
		    set => _cardParameters = value;
		}

				private MasterSpecialTrainingCost[] _specialTrainingCosts;
		public MasterSpecialTrainingCost[] specialTrainingCosts
		{
		    get => _specialTrainingCosts;
		    set => _specialTrainingCosts = value;
		}

				private MasterMasterLessonAchieveResource[] _masterLessonAchieveResources;
		public MasterMasterLessonAchieveResource[] masterLessonAchieveResources
		{
		    get => _masterLessonAchieveResources;
		    set => _masterLessonAchieveResources = value;
		}

				private MasterCardExchangeResource[] _masterCardExchangeResources;
		public MasterCardExchangeResource[] masterCardExchangeResources
		{
		    get => _masterCardExchangeResources;
		    set => _masterCardExchangeResources = value;
		}

				private long _archivePublishedAt;
		public long archivePublishedAt
		{
		    get => _archivePublishedAt;
		    set => _archivePublishedAt = value;
		}

		public string archiveDisplayType;

				private int _cardSupplyId;
		public int cardSupplyId
		{
		    get => _cardSupplyId;
		    set => _cardSupplyId = value;
		}

				private int _specialTrainingRewardResourceBoxId;
		public int specialTrainingRewardResourceBoxId
		{
		    get => _specialTrainingRewardResourceBoxId;
		    set => _specialTrainingRewardResourceBoxId = value;
		}

				private string _initialSpecialTrainingStatus;
		public string initialSpecialTrainingStatus
		{
		    get => _initialSpecialTrainingStatus;
		    set => _initialSpecialTrainingStatus = value;
		}

		private Dictionary<CardParameterType, MasterCardParameter[]> _masterCardParametersMap;

		public ArchiveDisplayType ArchiveDisplayType
		{
			get
			{
				throw null;
			}
		}

		public bool IsOnlyAfterTraining
		{
			get
			{
				throw null;
			}
		}

		public UnitType SupportUnitType
		{
			[CompilerGenerated]
			get
			{
				throw null;
			}
			[CompilerGenerated]
			private set
			{
				throw null;
			}
		}

		public bool HasSpecialTrainingSkill
		{
			get
			{
				throw null;
			}
		}

		public CardRarityType RarityType
		{
			[CompilerGenerated]
			get
			{
				throw null;
			}
			[CompilerGenerated]
			private set
			{
				throw null;
			}
		}

		public CardRarityFilterType RarityFilterType
		{
			[CompilerGenerated]
			get
			{
				throw null;
			}
			[CompilerGenerated]
			private set
			{
				throw null;
			}
		}

		public CardAttributeType AttributeType
		{
			[CompilerGenerated]
			get
			{
				throw null;
			}
			[CompilerGenerated]
			private set
			{
				throw null;
			}
		}

		public IReadOnlyDictionary<CardParameterType, MasterCardParameter[]> MasterCardParametersMap
		{
			get
			{
				throw null;
			}
		}

		public DateTime ReleaseAtTime
		{
			get
			{
				throw null;
			}
		}

		void IMessagePackSerializationCallbackReceiver.OnBeforeSerialize()
		{
			throw null;
		}

		void IMessagePackSerializationCallbackReceiver.OnAfterDeserialize()
		{
			throw null;
		}

		public MasterCard()
		{
		}
	}
}
