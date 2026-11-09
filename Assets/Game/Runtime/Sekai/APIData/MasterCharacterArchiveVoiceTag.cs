
namespace Sekai.ApiData
{
	public class MasterCharacterArchiveVoiceTag
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

				private string _characterArchiveVoiceTagType;
		public string characterArchiveVoiceTagType
		{
		    get => _characterArchiveVoiceTagType;
		    set => _characterArchiveVoiceTagType = value;
		}

				private string _name;
		public string name
		{
		    get => _name;
		    set => _name = value;
		}

		public MasterCharacterArchiveVoiceTag()
		{
		}
	}
}
