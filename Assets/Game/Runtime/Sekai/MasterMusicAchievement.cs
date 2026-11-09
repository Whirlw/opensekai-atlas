using MessagePack;

namespace Sekai
{
	public class MasterMusicAchievement : IMessagePackSerializationCallbackReceiver
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private string _musicAchievementType;
		public string musicAchievementType
		{
		    get => _musicAchievementType;
		    set => _musicAchievementType = value;
		}

				private string _musicAchievementTypeValue;
		public string musicAchievementTypeValue
		{
		    get => _musicAchievementTypeValue;
		    set => _musicAchievementTypeValue = value;
		}

				private string _musicDifficultyType;
		public string musicDifficultyType
		{
		    get => _musicDifficultyType;
		    set => _musicDifficultyType = value;
		}

				private int _resourceBoxId;
		public int resourceBoxId
		{
		    get => _resourceBoxId;
		    set => _resourceBoxId = value;
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

		public MasterMusicAchievement()
		{
		}
	}
}
