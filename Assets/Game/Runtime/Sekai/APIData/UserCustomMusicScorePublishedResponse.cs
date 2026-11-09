using System.Collections.Generic;
using Sekai;

namespace Sekai.ApiData
{
	public class UserCustomMusicScorePublishedResponse
	{
				private bool _isOfficialScore;
		public bool isOfficialScore
		{
		    get => _isOfficialScore;
		    set => _isOfficialScore = value;
		}

				private UserCustomMusicScoreInfo _userCustomMusicScoreInfoJson;
		public UserCustomMusicScoreInfo userCustomMusicScoreInfoJson
		{
		    get => _userCustomMusicScoreInfoJson;
		    set => _userCustomMusicScoreInfoJson = value;
		}

				private string _userCustomMusicScoreId;
		public string userCustomMusicScoreId
		{
		    get => _userCustomMusicScoreId;
		    set => _userCustomMusicScoreId = value;
		}

				private long _userId;
		public long userId
		{
		    get => _userId;
		    set => _userId = value;
		}

				private string _userName;
		public string userName
		{
		    get => _userName;
		    set => _userName = value;
		}

				private int _musicId;
		public int musicId
		{
		    get => _musicId;
		    set => _musicId = value;
		}

				private List<int> _customMusicScoreTags;
		public List<int> customMusicScoreTags
		{
		    get => _customMusicScoreTags;
		    set => _customMusicScoreTags = value;
		}

				private string _musicDifficultyType;
		public string musicDifficultyType
		{
		    get => _musicDifficultyType;
		    set => _musicDifficultyType = value;
		}

				private int _playLevel;
		public int playLevel
		{
		    get => _playLevel;
		    set => _playLevel = value;
		}

				private string _description;
		public string description
		{
		    get => _description;
		    set => _description = value;
		}

				private bool _isDerivativeAllowed;
		public bool isDerivativeAllowed
		{
		    get => _isDerivativeAllowed;
		    set => _isDerivativeAllowed = value;
		}

				private float _previewStartTimeSec;
		public float previewStartTimeSec
		{
		    get => _previewStartTimeSec;
		    set => _previewStartTimeSec = value;
		}

				private long _publishedAt;
		public long publishedAt
		{
		    get => _publishedAt;
		    set => _publishedAt = value;
		}

				private int _reviewCount;
		public int reviewCount
		{
		    get => _reviewCount;
		    set => _reviewCount = value;
		}

				private int _playCount;
		public int playCount
		{
		    get => _playCount;
		    set => _playCount = value;
		}

				private float _fullComboRate;
		public float fullComboRate
		{
		    get => _fullComboRate;
		    set => _fullComboRate = value;
		}

				private float _customMusicScoreSearchSortValue;
		public float customMusicScoreSearchSortValue
		{
		    get => _customMusicScoreSearchSortValue;
		    set => _customMusicScoreSearchSortValue = value;
		}

				private string _playResult;
		public string playResult
		{
		    get => _playResult;
		    set => _playResult = value;
		}

				private bool _isReviewed;
		public bool isReviewed
		{
		    get => _isReviewed;
		    set => _isReviewed = value;
		}

				private bool _isReviewAllowed;
		public bool isReviewAllowed
		{
		    get => _isReviewAllowed;
		    set => _isReviewAllowed = value;
		}

		public string ResolvedUserName
		{
			get
			{
				throw null;
			}
		}

		public MusicClearStatus GetClearStatus()
		{
			throw null;
		}

		public UserCustomMusicScorePublishedResponse()
		{
		}
	}
}
