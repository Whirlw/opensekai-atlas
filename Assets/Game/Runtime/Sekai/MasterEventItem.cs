
namespace Sekai
{
	public class MasterEventItem
	{
				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private int _eventId;
		public int eventId
		{
		    get => _eventId;
		    set => _eventId = value;
		}

				private int? _gameCharacterId;
		public int? gameCharacterId
		{
		    get => _gameCharacterId;
		    set => _gameCharacterId = value;
		}

				private string _name;
		public string name
		{
		    get => _name;
		    set => _name = value;
		}

				private string _fravorText;
		public string fravorText
		{
		    get => _fravorText;
		    set => _fravorText = value;
		}

				private string _assetbundleName;
		public string assetbundleName
		{
		    get => _assetbundleName;
		    set => _assetbundleName = value;
		}

		public MasterEventItem()
		{
		}
	}
}
