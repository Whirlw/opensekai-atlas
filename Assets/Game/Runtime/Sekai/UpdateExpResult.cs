
namespace Sekai
{
	public class UpdateExpResult
	{
				private int _beforeTotalExp;
		public int beforeTotalExp
		{
		    get => _beforeTotalExp;
		    set => _beforeTotalExp = value;
		}

				private int _afterTotalExp;
		public int afterTotalExp
		{
		    get => _afterTotalExp;
		    set => _afterTotalExp = value;
		}

				private int _beforeExp;
		public int beforeExp
		{
		    get => _beforeExp;
		    set => _beforeExp = value;
		}

				private int _afterExp;
		public int afterExp
		{
		    get => _afterExp;
		    set => _afterExp = value;
		}

				private int _beforeLevel;
		public int beforeLevel
		{
		    get => _beforeLevel;
		    set => _beforeLevel = value;
		}

				private int _afterLevel;
		public int afterLevel
		{
		    get => _afterLevel;
		    set => _afterLevel = value;
		}

		public UpdateExpResult()
		{
		}
	}
}
