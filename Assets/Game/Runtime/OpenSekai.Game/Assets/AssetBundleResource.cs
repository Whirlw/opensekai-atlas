using UnityEngine;

namespace Sekai
{
	public class AssetBundleResource
	{
				private AssetBundle _bundle;
		public AssetBundle Bundle
		{
		    get => _bundle;
		    set => _bundle = value;
		}

				private AssetBundleElement _element;
		public AssetBundleElement Element
		{
		    get => _element;
		    set => _element = value;
		}

				private string _dataPath;
		public string DataPath
		{
		    get => _dataPath;
		    set => _dataPath = value;
		}

				private AssetBundleStream _stream;
		public AssetBundleStream Stream
		{
		    get => _stream;
		    set => _stream = value;
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
