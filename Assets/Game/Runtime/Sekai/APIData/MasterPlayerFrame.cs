
namespace Sekai.ApiData
{
	public class MasterPlayerFrame
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

				private int _playerFrameGroupId;
		public int playerFrameGroupId
		{
		    get => _playerFrameGroupId;
		    set => _playerFrameGroupId = value;
		}

				private string _description;
		public string description
		{
		    get => _description;
		    set => _description = value;
		}

				private int _gameCharacterId;
		public int gameCharacterId
		{
		    get => _gameCharacterId;
		    set => _gameCharacterId = value;
		}

		public MasterPlayerFrame()
		{
		}
	}
}
