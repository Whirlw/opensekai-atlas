using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using OpenSekai.Shared.Storage;
using CP;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sekai
{
	public class AssetBundleManager
	{
		public enum DownloadResultCode
		{
			Success = 0,
			Error = 1
		}

		public enum DownloadErrorAction
		{
			None = 0
		}

		public enum CompareType
		{
			None = 0,
			Hash = 1
		}

		public enum ParallelDownloadControlStatus
		{
			None = 0
		}

		public class DownloadInfo
		{
						private float _progress;
			public float Progress
			{
			    get => _progress;
			    set => _progress = value;
			}
			public DownloadResultCode ResultCode;
		}

		public class DeleteOldAssetBundleInfo
		{
			public string BundleName { get; }
			public string FilePath { get; }

			public DeleteOldAssetBundleInfo()
			{
			}

			public DeleteOldAssetBundleInfo(string bundleName, string filePath)
			{
				BundleName = bundleName;
				FilePath = filePath;
			}
		}

		public class ParallelDownloadContainer
		{
			public enum DownloadStatus
			{
				Ready = 0,
				Downloading = 1,
				Finished = 2
			}
		}

		private const string ASSET_BUNDLE_INFO_FILE_NAME = "AssetBundleInfo.bytes";
		private const string CACHE_DIRECTORY_NAME = "data";

		private static readonly AssetBundleManager _instance = new AssetBundleManager();

		private AssetBundleInfo _clientAssetBundleInfo;
		private AssetBundleInfo _serverAssetBundleInfo;
		private Dictionary<string, List<string>> _categoryResourceMap;
		private Dictionary<string, AssetBundleElement> _serverAssetBundleCacheFileNameMap;
		private bool _initialized;
		private string _preparedCache;
		private AsyncLazy<bool> _preparation;

		private CancellationTokenSource _preparationLifetime;

		public UniTask<bool> PrepareStreamingAssetsAsync(CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (_initialized) return UniTask.FromResult(true);
			if (IsDirectFilePath(Application.streamingAssetsPath)) return UniTask.FromResult(Initialize());
			if (_preparation == null)
			{
				var lifetime = CancellationTokenSource.CreateLinkedTokenSource(Application.exitCancellationToken);
				_preparationLifetime = lifetime;
				_preparation = new AsyncLazy<bool>(() => PrepareUriCacheAsync(lifetime));
			}
			// Leaving one scene stops that waiter. Other scenes can still use the same download.
			return _preparation.Task.AttachExternalCancellation(cancellationToken);
		}

		private async UniTask<bool> PrepareUriCacheAsync(CancellationTokenSource lifetime)
		{
			try { return await PrepareUriCacheAsync(lifetime.Token); }
			finally
			{
				if (ReferenceEquals(_preparationLifetime, lifetime))
				{
					_preparation = null;
					_preparationLifetime = null;
				}
				lifetime.Dispose();
			}
		}

		private async UniTask<bool> PrepareUriCacheAsync(CancellationToken token)
		{
			string catalogUri = CombineStreamingAssetsPath(CACHE_DIRECTORY_NAME, ASSET_BUNDLE_INFO_FILE_NAME);
			byte[] catalog = await ReadUriAsync(catalogUri, token);
			var info = BinarySerializer.Deserialize<AssetBundleInfo>(catalog);
			if (info?.bundles == null || info.bundles.Count == 0) return false;
			string cache = Path.Combine(Application.temporaryCachePath, "OpenSekaiBundles");
			foreach (var element in info.bundles.Values)
			{
				token.ThrowIfCancellationRequested();
				string destination = CachedBundlePath(cache, element);
				if (File.Exists(destination) && new FileInfo(destination).Length == element.fileSize) continue;
				string uri = CombineStreamingAssetsPath(CACHE_DIRECTORY_NAME, element.cacheDirectoryName, element.cacheFileName);
				byte[] bytes = await ReadUriAsync(uri, token);
				if (bytes.LongLength != element.fileSize) throw new IOException("Incomplete AssetBundle: " + element.bundleName);
				AtomicFile.Write(destination, temp => File.WriteAllBytes(temp, bytes));
			}
			_preparedCache = cache;
			_clientAssetBundleInfo = _serverAssetBundleInfo = info;
			ParseServerAssetBundleInfo();
			return _initialized = true;
		}

		private static string CachedBundlePath(string cache, AssetBundleElement element) =>
			Path.Combine(cache, element.cacheFileName + "-" + element.hash);

		internal static async UniTask<byte[]> ReadUriAsync(string uri, CancellationToken token, int timeoutSeconds = 30)
		{
			token.ThrowIfCancellationRequested();
			using var request = UnityWebRequest.Get(uri);
			request.timeout = timeoutSeconds;
			using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
			using var timer = deadline.CancelAfterSlim(TimeSpan.FromSeconds(timeoutSeconds), DelayType.Realtime);
			try
			{
				// Await the underlying operation so protocol failures are reported with the requested URI.
				AsyncOperation operation = request.SendWebRequest();
				await operation.ToUniTask(cancellationToken: deadline.Token);
			}
			catch (OperationCanceledException)
			{
				request.Abort();
				token.ThrowIfCancellationRequested();
				throw new TimeoutException("Timed out reading packaged asset: " + uri);
			}
			token.ThrowIfCancellationRequested();
			if (request.result != UnityWebRequest.Result.Success)
				throw new IOException($"Could not read packaged asset {uri} ({request.responseCode}): {request.error}");
			return request.downloadHandler.data;
		}

		public static AssetBundleManager Instance => _instance;

		public bool IsValid => _clientAssetBundleInfo?.bundles != null && _clientAssetBundleInfo.bundles.Count > 0;

		public bool IsDownloading => false;

		public long TotalDownloadFileSize { get; private set; }

		public bool IsCleanUpPause { get; set; }

		public bool Initialize()
		{
			if (!IsDirectFilePath(Application.streamingAssetsPath)) return _initialized;
			InitLocalPaths();
			LoadClientAssetBundleInfo();
			_serverAssetBundleInfo = _clientAssetBundleInfo;
			ParseServerAssetBundleInfo();
			_initialized = IsValid;
			return _initialized;
		}

		public void InitializeForLocalCache()
		{
			Initialize();
		}

		public void ClearServerBundleInfo()
		{
			_serverAssetBundleInfo = null;
			_categoryResourceMap = null;
			_serverAssetBundleCacheFileNameMap = null;
		}

		public void ClearCache()
		{
			var lifetime = _preparationLifetime;
			_preparationLifetime = null;
			_preparation = null;
			lifetime?.Cancel();
			ClearServerBundleInfo();
			_clientAssetBundleInfo = null;
			_preparedCache = null;
			_initialized = false;
		}

		public bool HasBundle(string bundleName)
		{
			EnsureInitialized();
			return _clientAssetBundleInfo?.bundles != null && _clientAssetBundleInfo.bundles.ContainsKey(bundleName);
		}

		public AssetBundleElement GetAssetBundleElement(string bundleName)
		{
			EnsureInitialized();
			if (_clientAssetBundleInfo?.bundles == null || string.IsNullOrEmpty(bundleName))
			{
				return null;
			}

			_clientAssetBundleInfo.bundles.TryGetValue(bundleName, out AssetBundleElement element);
			return element;
		}

		public string GetStorageAssetBundleName(string bundleName)
		{
			return GetStreamingAssetsAssetBundleName(bundleName);
		}

		public string GetStreamingAssetsAssetBundleName(string bundleName)
		{
			AssetBundleElement element = GetAssetBundleElement(bundleName);
			if (element == null)
			{
				return null;
			}

			return _preparedCache == null
				? CombineStreamingAssetsPath(CACHE_DIRECTORY_NAME, element.cacheDirectoryName, element.cacheFileName)
				: CachedBundlePath(_preparedCache, element);
		}

		public AssetBundleResource LoadAssetBundle(string bundleName, uint readBufferSize = 65536u)
		{
			EnsureInitialized();
			AssetBundleElement element = GetAssetBundleElement(bundleName);
			if (element == null)
			{
				LogUtility.LogError("AssetBundleInfo does not contain bundle: {0}", bundleName);
				return null;
			}

			string path = GetStreamingAssetsAssetBundleName(bundleName);
			if (!ExistsPackagedAssetBundle(element, path))
			{
				LogUtility.LogError("AssetBundle file not found. bundle:{0} streamingPath:{1}", bundleName, path);
				return null;
			}

			AssetBundle bundle = null;
			try
			{
				bundle = AssetBundle.LoadFromFile(path);

				if (bundle == null)
				{
					LogUtility.LogError("Failed to load AssetBundle. bundle:{0} path:{1}", bundleName, path);
					return null;
				}

				AssetBundleMetaManager.Instance.UpdateClientMetaLastCheckDate(bundleName);
				return new AssetBundleResource
				{
					Bundle = bundle,
					Element = element,
					DataPath = path,
					Stream = null
				};
			}
			catch (Exception ex)
			{
				LogUtility.LogException(ex);
				return null;
			}
		}

		public bool ExistsBundleFile(string bundleName)
		{
			AssetBundleElement element = GetAssetBundleElement(bundleName);
			return element != null && ExistsPackagedAssetBundle(element, GetStreamingAssetsAssetBundleName(bundleName));
		}

		public bool ExistsClientBundleFile(string bundleName)
		{
			return ExistsBundleFile(bundleName);
		}

		public long GetClientBundleFileSize(string bundleName)
		{
			AssetBundleElement element = GetAssetBundleElement(bundleName);
			if (element == null)
			{
				return 0L;
			}

			string path = GetStreamingAssetsAssetBundleName(bundleName);
			if (IsDirectFilePath(path) && File.Exists(path))
			{
				return new FileInfo(path).Length;
			}

			return element.fileSize;
		}

		public long GetClientBundleFileTotalSize()
		{
			EnsureInitialized();
			long total = 0L;
			if (_clientAssetBundleInfo?.bundles == null)
			{
				return total;
			}

			foreach (string bundleName in _clientAssetBundleInfo.bundles.Keys)
			{
				total += GetClientBundleFileSize(bundleName);
			}

			return total;
		}

		public List<string> GetClientBundleNameList()
		{
			EnsureInitialized();
			return _clientAssetBundleInfo?.bundles == null
				? new List<string>()
				: new List<string>(_clientAssetBundleInfo.bundles.Keys);
		}

		public bool CheckNeedsDownload(string bundleName)
		{
			AssetBundleElement element = GetAssetBundleElement(bundleName);
			if (element == null)
			{
				return true;
			}

			string path = GetStreamingAssetsAssetBundleName(bundleName);
			if (!ExistsPackagedAssetBundle(element, path))
			{
				return true;
			}

			if (IsDirectFilePath(path) && element.fileSize > 0L && new FileInfo(path).Length != element.fileSize)
			{
				return true;
			}

			return false;
		}

		public bool ExistsDependencies(string bundleName)
		{
			AssetBundleElement element = GetAssetBundleElement(bundleName);
			return element?.dependencies != null && element.dependencies.Length > 0;
		}

		public string[] GetDependencies(string bundleName)
		{
			return GetAssetBundleElement(bundleName)?.dependencies;
		}

		public void OverwriteDataVersion(string dataVersion)
		{
			if (_clientAssetBundleInfo != null)
			{
				_clientAssetBundleInfo.version = dataVersion;
			}
		}

		public List<string> GetAssetBundleContainsNameFilesInCategory(string categoryName, string[] containNames, string[] excludeNames)
		{
			EnsureInitialized();
			List<string> result = new List<string>();
			if (_serverAssetBundleInfo?.bundles == null)
			{
				return result;
			}

			foreach (AssetBundleElement element in _serverAssetBundleInfo.bundles.Values)
			{
				if (!string.Equals(element.category, categoryName, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				string bundleName = element.bundleName;
				if (!ContainsAll(bundleName, containNames) || ContainsAny(bundleName, excludeNames))
				{
					continue;
				}

				result.Add(bundleName);
			}

			return result;
		}

		public List<string> GetAssetBundleContainsNameFilesInCategory(string categoryName, string[] containNames)
		{
			return GetAssetBundleContainsNameFilesInCategory(categoryName, containNames, null);
		}

		public List<string> GetAssetBundleContainsNameFiles(string[] containNames)
		{
			EnsureInitialized();
			List<string> result = new List<string>();
			if (_serverAssetBundleInfo?.bundles == null)
			{
				return result;
			}

			foreach (string bundleName in _serverAssetBundleInfo.bundles.Keys)
			{
				if (ContainsAll(bundleName, containNames))
				{
					result.Add(bundleName);
				}
			}

			return result;
		}

		public bool MatchWithServerInfo(AssetBundleElement elem)
		{
			if (elem == null)
			{
				return false;
			}

			AssetBundleElement server = GetAssetBundleElement(elem.bundleName);
			return server != null && string.Equals(server.hash, elem.hash, StringComparison.Ordinal);
		}

		public bool MatchWithServerInfo(string bundleName)
		{
			return GetAssetBundleElement(bundleName) != null;
		}

		public List<string> GetServerAssetBundleMatchList(string regularExpressionName)
		{
			EnsureInitialized();
			List<string> result = new List<string>();
			if (_serverAssetBundleInfo?.bundles == null || string.IsNullOrEmpty(regularExpressionName))
			{
				return result;
			}

			System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(regularExpressionName);
			foreach (string bundleName in _serverAssetBundleInfo.bundles.Keys)
			{
				if (regex.IsMatch(bundleName))
				{
					result.Add(bundleName);
				}
			}

			return result;
		}

		public int InitCategoryDownloadInfo(List<string> categoryNames, List<string> bundleNames = null)
		{
			return 0;
		}

		public int InitBundleListDownloadInfo(List<string> bundleNames, Action onAssetBundleError = null)
		{
			return 0;
		}

		public int StartCategoryDownload(List<string> categoryNames, List<string> bundleNames, DownloadEventHandler onDownloadProgress, DownloadEventHandler onFinished, DownloadEventHandler onError, int parallelDLSize = 1)
		{
			DownloadInfo info = new DownloadInfo { Progress = 1f, ResultCode = DownloadResultCode.Success };
			onDownloadProgress?.Invoke(info);
			onFinished?.Invoke(info);
			return 0;
		}

		public void DownloadResourceList(DownloadResourceResultHandler onFinished)
		{
			onFinished?.Invoke(null);
		}

		public delegate void DownloadEventHandler(DownloadInfo info);

		public delegate void DownloadResourceResultHandler(object result);

		private void EnsureInitialized()
		{
			if (!_initialized)
			{
				Initialize();
			}
		}

		private void InitLocalPaths()
		{
		}

		private static bool ExistsPackagedAssetBundle(AssetBundleElement element, string streamingPath)
		{
			if (element == null || string.IsNullOrEmpty(streamingPath))
			{
				return false;
			}

			return !IsDirectFilePath(streamingPath) || File.Exists(streamingPath);
		}

		private static string CombineStreamingAssetsPath(params string[] parts)
		{
			string path = Application.streamingAssetsPath.TrimEnd('/', '\\');
			foreach (string part in parts)
			{
				if (!string.IsNullOrEmpty(part))
				{
					path += "/" + part.Trim('/', '\\');
				}
			}

			return path;
		}

		private static bool IsDirectFilePath(string path)
		{
			return !string.IsNullOrEmpty(path)
				&& path.IndexOf("://", StringComparison.Ordinal) < 0
				&& !path.StartsWith("jar:", StringComparison.OrdinalIgnoreCase);
		}

		private void LoadClientAssetBundleInfo()
		{
#if UNITY_EDITOR
			_clientAssetBundleInfo = CreateEditorAssetBundleInfo();
			if (_clientAssetBundleInfo?.bundles == null || _clientAssetBundleInfo.bundles.Count == 0)
			{
				Debug.LogWarning("No AssetBundle names are configured in the Editor AssetDatabase.");
				_clientAssetBundleInfo = new AssetBundleInfo();
			}
			return;
#else
			byte[] assetBundleInfoBytes = LoadClientAssetBundleInfoBytes();
			if (assetBundleInfoBytes == null || assetBundleInfoBytes.Length == 0)
			{
				LogUtility.LogError("AssetBundleInfo.bytes was not found.");
				_clientAssetBundleInfo = new AssetBundleInfo();
				return;
			}

			try
			{
				_clientAssetBundleInfo = BinarySerializer.Deserialize<AssetBundleInfo>(assetBundleInfoBytes);
				if (_clientAssetBundleInfo == null)
				{
					_clientAssetBundleInfo = new AssetBundleInfo();
				}
			}
			catch (Exception ex)
			{
				_clientAssetBundleInfo = new AssetBundleInfo();
				LogUtility.LogException(ex);
			}
#endif
		}

		private byte[] LoadClientAssetBundleInfoBytes()
		{
			string streamingPath = CombineStreamingAssetsPath(CACHE_DIRECTORY_NAME, ASSET_BUNDLE_INFO_FILE_NAME);
			if (IsDirectFilePath(streamingPath))
			{
				if (File.Exists(streamingPath))
				{
					return File.ReadAllBytes(streamingPath);
				}
			}

			return null;
		}

#if UNITY_EDITOR
		private static AssetBundleInfo CreateEditorAssetBundleInfo()
		{
			AssetBundleInfo info = new AssetBundleInfo
			{
				version = Application.version,
				bundles = new Dictionary<string, AssetBundleElement>()
			};

			string[] bundleNames = AssetDatabase.GetAllAssetBundleNames();
			if (bundleNames == null)
			{
				return info;
			}

			Array.Sort(bundleNames, StringComparer.Ordinal);
			for (int i = 0; i < bundleNames.Length; i++)
			{
				string bundleName = NormalizeBundleName(bundleNames[i]);
				if (string.IsNullOrEmpty(bundleName))
				{
					continue;
				}

				info.bundles[bundleName] = new AssetBundleElement
				{
					bundleName = bundleName,
					cacheFileName = bundleName,
					cacheDirectoryName = string.Empty,
					hash = string.Empty,
					category = InferEditorCategory(bundleName),
					crc = 0u,
					fileSize = 0L,
					dependencies = GetEditorAssetBundleDependencies(bundleName),
					paths = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName),
					isBuiltin = false,
					isRelocate = true
				};
			}

			return info;
		}

		private static string[] GetEditorAssetBundleDependencies(string bundleName)
		{
			string[] dependencies = AssetDatabase.GetAssetBundleDependencies(bundleName, true);
			if (dependencies == null || dependencies.Length == 0)
			{
				return Array.Empty<string>();
			}

			for (int i = 0; i < dependencies.Length; i++)
			{
				dependencies[i] = NormalizeBundleName(dependencies[i]);
			}

			Array.Sort(dependencies, StringComparer.Ordinal);
			return dependencies;
		}

		private static string InferEditorCategory(string bundleName)
		{
			return bundleName.IndexOf("tutorial", StringComparison.OrdinalIgnoreCase) >= 0
				? "Tutorial"
				: "StartApp";
		}

		private static string NormalizeBundleName(string bundleName)
		{
			return (bundleName ?? string.Empty).Replace('\\', '/').Trim('/');
		}
#endif

		private void ParseServerAssetBundleInfo()
		{
			_categoryResourceMap = new Dictionary<string, List<string>>();
			_serverAssetBundleCacheFileNameMap = new Dictionary<string, AssetBundleElement>();
			if (_serverAssetBundleInfo?.bundles == null)
			{
				return;
			}

			foreach (AssetBundleElement element in _serverAssetBundleInfo.bundles.Values)
			{
				if (element == null)
				{
					continue;
				}

				if (!string.IsNullOrEmpty(element.cacheFileName) && !_serverAssetBundleCacheFileNameMap.ContainsKey(element.cacheFileName))
				{
					_serverAssetBundleCacheFileNameMap.Add(element.cacheFileName, element);
				}

				if (string.IsNullOrEmpty(element.category))
				{
					continue;
				}

				if (!_categoryResourceMap.TryGetValue(element.category, out List<string> list))
				{
					list = new List<string>();
					_categoryResourceMap.Add(element.category, list);
				}
				list.Add(element.bundleName);
			}
		}

		private static bool ContainsAll(string value, string[] contains)
		{
			if (contains == null || contains.Length == 0)
			{
				return true;
			}

			foreach (string contain in contains)
			{
				if (!string.IsNullOrEmpty(contain) && (value == null || value.IndexOf(contain, StringComparison.OrdinalIgnoreCase) < 0))
				{
					return false;
				}
			}

			return true;
		}

		private static bool ContainsAny(string value, string[] contains)
		{
			if (contains == null || contains.Length == 0)
			{
				return false;
			}

			foreach (string contain in contains)
			{
				if (!string.IsNullOrEmpty(contain) && value != null && value.IndexOf(contain, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}

			return false;
		}
	}
}
