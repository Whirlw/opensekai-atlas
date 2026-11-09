
namespace Sekai
{
	public class MasterMusicVideoCharacter
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

		public string defaultMusicType;

				private int _gameCharacterUnitId;
		public int gameCharacterUnitId
		{
		    get => _gameCharacterUnitId;
		    set => _gameCharacterUnitId = value;
		}

				private int _dancePriority;
		public int dancePriority
		{
		    get => _dancePriority;
		    set => _dancePriority = value;
		}

				private int _seq;
		public int seq
		{
		    get => _seq;
		    set => _seq = value;
		}

		public DefaultMusicType DefaultMusicType
		{
			get
			{
				throw null;
			}
		}

		public MasterMusicVideoCharacter()
		{
		}
	}
}
