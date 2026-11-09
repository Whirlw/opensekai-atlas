
namespace Sekai.ApiData
{
	public class CustomMusicScoreOfficialCreatorPublishedResponse
	{
				private string _customMusicScoreId;
		public string customMusicScoreId
		{
		    get => _customMusicScoreId;
		    set => _customMusicScoreId = value;
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

		public CustomMusicScoreOfficialCreatorPublishedResponse()
		{
			throw null;
		}
	}
}
