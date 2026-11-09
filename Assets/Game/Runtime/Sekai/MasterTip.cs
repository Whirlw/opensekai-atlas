
namespace Sekai
{
	public class MasterTip
	{
		public enum ViewType
		{
			TYPE_COMIC = 0,
			TYPE_TEXT_TIPS = 1
		}

				private int _id;
		public int id
		{
		    get => _id;
		    set => _id = value;
		}

				private string _title;
		public string title
		{
		    get => _title;
		    set => _title = value;
		}

				private string _description;
		public string description
		{
		    get => _description;
		    set => _description = value;
		}

				private string _assetbundleName;
		public string assetbundleName
		{
		    get => _assetbundleName;
		    set => _assetbundleName = value;
		}

				private int _fromUserRank;
		public int fromUserRank
		{
		    get => _fromUserRank;
		    set => _fromUserRank = value;
		}

				private int _toUserRank;
		public int toUserRank
		{
		    get => _toUserRank;
		    set => _toUserRank = value;
		}

		public ViewType GetViewType()
		{
			return string.IsNullOrEmpty(description) ? ViewType.TYPE_COMIC : ViewType.TYPE_TEXT_TIPS;
		}

		public MasterTip()
		{
		}
	}
}
