using System.Collections.Generic;

namespace Sekai
{
	public class AssetBundleInfo
	{
				private string _version;
		public string version
		{
		    get => _version;
		    set => _version = value;
		}

				private Dictionary<string, AssetBundleElement> _bundles;
		public Dictionary<string, AssetBundleElement> bundles
		{
		    get => _bundles;
		    set => _bundles = value;
		}

		public AssetBundleInfo()
		{
			bundles = new Dictionary<string, AssetBundleElement>();
		}
	}
}
