namespace Sekai
{
	public class AssetBundleElement
	{
				private string _bundleName;
		public string bundleName
		{
		    get => _bundleName;
		    set => _bundleName = value;
		}

				private string _cacheFileName;
		public string cacheFileName
		{
		    get => _cacheFileName;
		    set => _cacheFileName = value;
		}

				private string _cacheDirectoryName;
		public string cacheDirectoryName
		{
		    get => _cacheDirectoryName;
		    set => _cacheDirectoryName = value;
		}

				private string _hash;
		public string hash
		{
		    get => _hash;
		    set => _hash = value;
		}

				private string _category;
		public string category
		{
		    get => _category;
		    set => _category = value;
		}

				private uint _crc;
		public uint crc
		{
		    get => _crc;
		    set => _crc = value;
		}

				private long _fileSize;
		public long fileSize
		{
		    get => _fileSize;
		    set => _fileSize = value;
		}

				private string[] _dependencies;
		public string[] dependencies
		{
		    get => _dependencies;
		    set => _dependencies = value;
		}

				private string[] _paths;
		public string[] paths
		{
		    get => _paths;
		    set => _paths = value;
		}

				private bool _isBuiltin;
		public bool isBuiltin
		{
		    get => _isBuiltin;
		    set => _isBuiltin = value;
		}

				private bool _isRelocate;
		public bool isRelocate
		{
		    get => _isRelocate;
		    set => _isRelocate = value;
		}

		public AssetBundleElement()
		{
		}

		public AssetBundleElement(AssetBundleElement element)
		{
			Copy(element);
		}

		public void Copy(AssetBundleElement element)
		{
			if (element == null)
			{
				return;
			}

			bundleName = element.bundleName;
			cacheFileName = element.cacheFileName;
			cacheDirectoryName = element.cacheDirectoryName;
			hash = element.hash;
			category = element.category;
			crc = element.crc;
			fileSize = element.fileSize;
			dependencies = element.dependencies;
			paths = element.paths;
			isBuiltin = element.isBuiltin;
			isRelocate = element.isRelocate;
		}

	}
}
