using System;

namespace Sekai
{
	public class MasterMusic
	{
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

				private int _releaseConditionId;
		public int releaseConditionId
		{
		    get => _releaseConditionId;
		    set => _releaseConditionId = value;
		}

				private string _title;
		public string title
		{
		    get => _title;
		    set => _title = value;
		}

				private string _pronunciation;
		public string pronunciation
		{
		    get => _pronunciation;
		    set => _pronunciation = value;
		}

		public string[] categories;

				private int _creatorArtistId;
		public int creatorArtistId
		{
		    get => _creatorArtistId;
		    set => _creatorArtistId = value;
		}

				private string _lyricist;
		public string lyricist
		{
		    get => _lyricist;
		    set => _lyricist = value;
		}

				private string _composer;
		public string composer
		{
		    get => _composer;
		    set => _composer = value;
		}

				private string _arranger;
		public string arranger
		{
		    get => _arranger;
		    set => _arranger = value;
		}

				private int _dancerCount;
		public int dancerCount
		{
		    get => _dancerCount;
		    set => _dancerCount = value;
		}

				private int _selfDancerPosition;
		public int selfDancerPosition
		{
		    get => _selfDancerPosition;
		    set => _selfDancerPosition = value;
		}

				private string _assetbundleName;
		public string assetbundleName
		{
		    get => _assetbundleName;
		    set => _assetbundleName = value;
		}

				private string _liveTalkBackgroundAssetbundleName;
		public string liveTalkBackgroundAssetbundleName
		{
		    get => _liveTalkBackgroundAssetbundleName;
		    set => _liveTalkBackgroundAssetbundleName = value;
		}

				private string _description;
		public string description
		{
		    get => _description;
		    set => _description = value;
		}

				private long _publishedAt;
		public long publishedAt
		{
		    get => _publishedAt;
		    set => _publishedAt = value;
		}

				private long _releasedAt;
		public long releasedAt
		{
		    get => _releasedAt;
		    set => _releasedAt = value;
		}

				private int _liveStageId;
		public int liveStageId
		{
		    get => _liveStageId;
		    set => _liveStageId = value;
		}

				private float _fillerSec;
		public float fillerSec
		{
		    get => _fillerSec;
		    set => _fillerSec = value;
		}

				private int _musicCollaborationId;
		public int musicCollaborationId
		{
		    get => _musicCollaborationId;
		    set => _musicCollaborationId = value;
		}

				private bool _isNewlyWrittenMusic;
		public bool isNewlyWrittenMusic
		{
		    get => _isNewlyWrittenMusic;
		    set => _isNewlyWrittenMusic = value;
		}

				private bool _isFullLength;
		public bool isFullLength
		{
		    get => _isFullLength;
		    set => _isFullLength = value;
		}

				private int _secForMusicScoreMaker;
		public int secForMusicScoreMaker
		{
		    get => _secForMusicScoreMaker;
		    set => _secForMusicScoreMaker = value;
		}

				private bool _isAvailableForMusicScoreMaker;
		public bool isAvailableForMusicScoreMaker
		{
		    get => _isAvailableForMusicScoreMaker;
		    set => _isAvailableForMusicScoreMaker = value;
		}

		public DateTime PublishedTime
		{
			get
			{
				throw null;
			}
		}

		public DateTime ReleasedTime
		{
			get
			{
				throw null;
			}
		}

		public MusicCategory[] Categories
		{
			get
			{
				throw null;
			}
		}

		public MasterMusic()
		{
		}
	}
}
