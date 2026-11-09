using MessagePack;

namespace Sekai
{
	public class MasterMusicVocalCharacter : IMessagePackSerializationCallbackReceiver
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private int _musicId;
		public int musicId
		{
		    get => _musicId;
		    set => _musicId = value;
		}

				private int _musicVocalId;
		public int musicVocalId
		{
		    get => _musicVocalId;
		    set => _musicVocalId = value;
		}

		public string characterType;

				private int _characterId;
		public int characterId
		{
		    get => _characterId;
		    set => _characterId = value;
		}

				private int _seq;
		public int seq
		{
		    get => _seq;
		    set => _seq = value;
		}

		public CharacterType CharacterType
		{
			get
			{
				throw null;
			}
		}

		public UnitType GetUnitType()
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

		public MasterMusicVocalCharacter()
		{
		}
	}
}
