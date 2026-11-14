using MessagePack;

namespace Sekai
{
	public class MasterIngameCutinCharacters : IMessagePackSerializationCallbackReceiver
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private int _priority;
		public int priority
		{
		    get => _priority;
		    set => _priority = value;
		}

				private string _assetbundleName;
		public string assetbundleName
		{
		    get => _assetbundleName;
		    set => _assetbundleName = value;
		}

				private int _gameCharacterUnitId1;
		public int gameCharacterUnitId1
		{
		    get => _gameCharacterUnitId1;
		    set => _gameCharacterUnitId1 = value;
		}

				private int _gameCharacterUnitId2;
		public int gameCharacterUnitId2
		{
		    get => _gameCharacterUnitId2;
		    set => _gameCharacterUnitId2 = value;
		}

				private string _assetbundleName1;
		public string assetbundleName1
		{
		    get => _assetbundleName1;
		    set => _assetbundleName1 = value;
		}

				private string _assetbundleName2;
		public string assetbundleName2
		{
		    get => _assetbundleName2;
		    set => _assetbundleName2 = value;
		}

				private string _ingameCutinCharacterType;
		public string ingameCutinCharacterType
		{
		    get => _ingameCutinCharacterType;
		    set => _ingameCutinCharacterType = value;
		}

				private int _releaseConditionId;
		public int releaseConditionId
		{
		    get => _releaseConditionId;
		    set => _releaseConditionId = value;
		}

				private bool _isLotteryTarget;
		public bool isLotteryTarget
		{
		    get => _isLotteryTarget;
		    set => _isLotteryTarget = value;
		}

				private int _firstCharacterArchiveVoiceId;
		public int firstCharacterArchiveVoiceId
		{
		    get => _firstCharacterArchiveVoiceId;
		    set => _firstCharacterArchiveVoiceId = value;
		}

				private int _secondCharacterArchiveVoiceId;
		public int secondCharacterArchiveVoiceId
		{
		    get => _secondCharacterArchiveVoiceId;
		    set => _secondCharacterArchiveVoiceId = value;
		}

		public void OnBeforeSerialize()
		{
			throw null;
		}

		public void OnAfterDeserialize()
		{
			throw null;
		}

		public MasterIngameCutinCharacters()
		{
		}
	}
}
