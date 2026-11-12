using MessagePack;

namespace Sekai
{
	public class MasterMusicDifficulty : IMessagePackSerializationCallbackReceiver
	{
		public const int INVALID_ID = -1;

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

		public string musicDifficulty;

				private int _playLevel;
		public int playLevel
		{
		    get => _playLevel;
		    set => _playLevel = value;
		}

				private int _totalNoteCount;
		public int totalNoteCount
		{
		    get => _totalNoteCount;
		    set => _totalNoteCount = value;
		}

		public MusicDifficulty MusicDifficulty
		{
			get
			{
				throw null;
			}
		}

		public void OnBeforeSerialize()
		{
			throw null;
		}

		public void OnAfterDeserialize()
		{
			throw null;
		}

		public MasterMusicDifficulty()
		{
		}
	}
}
