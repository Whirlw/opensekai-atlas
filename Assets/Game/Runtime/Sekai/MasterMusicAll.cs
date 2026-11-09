using System.Collections.Generic;

namespace Sekai
{
	public class MasterMusicAll
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private MasterMusic _music;
		public MasterMusic music
		{
		    get => _music;
		    set => _music = value;
		}

				private MasterMusicArtist _musicArtist;
		public MasterMusicArtist musicArtist
		{
		    get => _musicArtist;
		    set => _musicArtist = value;
		}

				private MasterMusicDifficulty[] _musicDifficulties;
		public MasterMusicDifficulty[] musicDifficulties
		{
		    get => _musicDifficulties;
		    set => _musicDifficulties = value;
		}

				private MasterMusicVocal[] _musicVocals;
		public MasterMusicVocal[] musicVocals
		{
		    get => _musicVocals;
		    set => _musicVocals = value;
		}

				private MasterMusicAchievement[] _musicAchievements;
		public MasterMusicAchievement[] musicAchievements
		{
		    get => _musicAchievements;
		    set => _musicAchievements = value;
		}

				private List<MasterMusicVideoCharacter> _musicVideoCharacters;
		public List<MasterMusicVideoCharacter> musicVideoCharacters
		{
		    get => _musicVideoCharacters;
		    set => _musicVideoCharacters = value;
		}

				private bool _isLimitedTimeMusic;
		public bool isLimitedTimeMusic
		{
		    get => _isLimitedTimeMusic;
		    set => _isLimitedTimeMusic = value;
		}

		public string ArtistName
		{
			get
			{
				throw null;
			}
		}

		public bool ExistDifficulty(MusicDifficulty difficulty)
		{
			throw null;
		}

		public MasterMusicDifficulty GetMasterMusicDifficulty(MusicDifficulty difficulty)
		{
			throw null;
		}

		public int GetPlayLevelByDifficulty(MusicDifficulty difficulty)
		{
			throw null;
		}

		public bool ContainsWord(string word)
		{
			throw null;
		}

		public IEnumerable<string> GetFilteredElementsByWord(string word)
		{
			throw null;
		}

		public MasterMusicAll()
		{
		}
	}
}
