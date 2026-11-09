
namespace Sekai
{
	public class UserMusicResult
	{
		public const string MUSIC_PLAY_TYPE_SOLO = "solo";

		public const string MUSIC_PLAY_TYPE_MULTI = "multi";

		public const string MUSIC_PLAY_RESULT_FULL_PERFECT = "full_perfect";

		public const string MUSIC_PLAY_RESULT_FULL_COMBO = "full_combo";

		public const string MUSIC_PLAY_RESULT_CLEAR = "clear";

		public const string MUSIC_PLAY_RESULT_NOT_CLEAR = "not_clear";

				private int _musicId;
		public int musicId
		{
		    get => _musicId;
		    set => _musicId = value;
		}

				private string _musicDifficultyType;
		public string musicDifficultyType
		{
		    get => _musicDifficultyType;
		    set => _musicDifficultyType = value;
		}

				private string _playType;
		public string playType
		{
		    get => _playType;
		    set => _playType = value;
		}

				private string _playResult;
		public string playResult
		{
		    get => _playResult;
		    set => _playResult = value;
		}

				private int _highScore;
		public int highScore
		{
		    get => _highScore;
		    set => _highScore = value;
		}

				private bool _fullComboFlg;
		public bool fullComboFlg
		{
		    get => _fullComboFlg;
		    set => _fullComboFlg = value;
		}

				private bool _fullPerfectFlg;
		public bool fullPerfectFlg
		{
		    get => _fullPerfectFlg;
		    set => _fullPerfectFlg = value;
		}

				private int _mvpCount;
		public int mvpCount
		{
		    get => _mvpCount;
		    set => _mvpCount = value;
		}

				private int _superStarCount;
		public int superStarCount
		{
		    get => _superStarCount;
		    set => _superStarCount = value;
		}

		public MusicDifficulty MusicDifficulty
		{
			get
			{
				throw null;
			}
		}

		public int ClearStatus
		{
			get
			{
				throw null;
			}
		}

		public MusicPlayType MusicPlayType
		{
			get
			{
				throw null;
			}
		}

		public UserMusicResult()
		{
			throw null;
		}
	}
}
