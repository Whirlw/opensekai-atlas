using MessagePack;

namespace Sekai
{
	public class MasterMusicVocal : IMessagePackSerializationCallbackReceiver
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

		public string musicVocalType;

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

				private string _caption;
		public string caption
		{
		    get => _caption;
		    set => _caption = value;
		}

				private MasterMusicVocalCharacter[] _characters;
		public MasterMusicVocalCharacter[] characters
		{
		    get => _characters;
		    set => _characters = value;
		}

				private string _assetbundleName;
		public string assetbundleName
		{
		    get => _assetbundleName;
		    set => _assetbundleName = value;
		}

				private int _specialSeasonId;
		public int specialSeasonId
		{
		    get => _specialSeasonId;
		    set => _specialSeasonId = value;
		}

				private long _archivePublishedAt;
		public long archivePublishedAt
		{
		    get => _archivePublishedAt;
		    set => _archivePublishedAt = value;
		}

		public string archiveDisplayType;

		public ArchiveDisplayType ArchiveDisplayType
		{
			get
			{
				throw null;
			}
		}

		public MusicVocalType MusicVocalType
		{
			get
			{
				throw null;
			}
		}

		public bool ExistsCharacterByUnitType(UnitType unitType)
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

		public MasterMusicVocal()
		{
		}
	}
}
