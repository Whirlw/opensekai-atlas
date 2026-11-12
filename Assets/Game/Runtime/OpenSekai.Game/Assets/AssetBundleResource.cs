using UnityEngine;

namespace Sekai
{
	public class AssetBundleResource
	{
				private AssetBundle _Bundle;
		public AssetBundle Bundle
		{
		    get => _Bundle;
		    set => _Bundle = value;
		}

				private AssetBundleElement _Element;
		public AssetBundleElement Element
		{
		    get => _Element;
		    set => _Element = value;
		}

				private string _DataPath;
		public string DataPath
		{
		    get => _DataPath;
		    set => _DataPath = value;
		}

				private AssetBundleStream _Stream;
		public AssetBundleStream Stream
		{
		    get => _Stream;
		    set => _Stream = value;
		}

		public void Unload(bool unloadLoadedAsset)
		{
			if (Bundle != null)
			{
				Bundle.Unload(unloadLoadedAsset);
				Bundle = null;
			}

			CloseStream();
		}

		public void CloseStream()
		{
			if (Stream != null)
			{
				Stream.Close();
				Stream = null;
			}
		}

		public AssetBundleResource()
		{
		}
	}
}
