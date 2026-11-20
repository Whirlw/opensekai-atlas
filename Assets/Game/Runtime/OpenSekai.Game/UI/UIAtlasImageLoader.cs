using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.U2D;
using Sekai;

namespace Sekai.UI
{
	public class UIAtlasImageLoader : MonoBehaviour
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_atlasImage")]
		[SerializeField]
		protected AtlasImage atlasImage;

		[UnityEngine.Serialization.FormerlySerializedAs("_loadingObject")]
		[SerializeField]
		protected GameObject loadingObject;

		[UnityEngine.Serialization.FormerlySerializedAs("_notFoundObject")]
		[SerializeField]
		protected GameObject notFoundObject;

		[UnityEngine.Serialization.FormerlySerializedAs("_doCache")]
		[SerializeField]
		protected bool doCache;

		[UnityEngine.Serialization.FormerlySerializedAs("_showIndicator")]
		[SerializeField]
		protected bool showIndicator;

		[UnityEngine.Serialization.FormerlySerializedAs("_onDisableUnload")]
		[SerializeField]
		protected bool onDisableUnload;

		private string _bundleName;

		private string _imageName;

		private AssetManager.BundleElementLoadStatus _loadStatus;

		private bool _reserved;

		private string _reserveBundleName;

		private string _reserveAtlasName;

		private string _reserveImageName;

		private LruAssetCacheGeneral<UnityEngine.Object> _reserveLruCache;

		private LruAssetCacheHandle<UnityEngine.Object> _lruCacheHandle;

		private Action<AssetManager.BundleElement> _onSuccess;

		private Action<AssetManager.BundleElement> _onError;

		public Func<AssetManager.BundleElement, bool> OnPreUpdate { get; set; }

		public AtlasImage AtlasImage
		{
			get
			{
				return atlasImage;
			}
		}

		public void Load(string assetBundleName, string atlasName, string imageName, Action<AssetManager.BundleElement> onSuccess = null, Action<AssetManager.BundleElement> onError = null, LruAssetCacheGeneral<UnityEngine.Object> lruCache = null)
		{
			_onSuccess = onSuccess;
			_onError = onError;
			Load(assetBundleName, atlasName, imageName, lruCache);
		}

		public UniTask<(bool, AssetManager.BundleElement)> LoadAsync(string assetBundleName, string atlasName, string fileName, LruAssetCacheGeneral<UnityEngine.Object> lruCache = null, CancellationToken ct = default)
		{
			Load(assetBundleName, atlasName, fileName, lruCache);
			return UniTask.FromResult((_loadStatus == AssetManager.BundleElementLoadStatus.Success, (AssetManager.BundleElement)null));
		}

		public void Unload()
		{
			if (atlasImage != null)
			{
				atlasImage.sprite = null;
			}

			_bundleName = null;
			_imageName = null;
			_loadStatus = AssetManager.BundleElementLoadStatus.Ready;
			_lruCacheHandle = default;
		}

		public void ResetLoadingObject()
		{
			SetActiveLoadingObject(false);
			SetActiveNotFoundObject(_loadStatus == AssetManager.BundleElementLoadStatus.Error);
		}

		public void SetActiveLoadingObject(bool isActive)
		{
			if (loadingObject != null)
			{
				loadingObject.SetActive(isActive);
			}
		}

		public void SetActiveNotFoundObject(bool isActive)
		{
			if (notFoundObject != null)
			{
				notFoundObject.SetActive(isActive);
			}
		}

		public void SetupSize(Vector2 size)
		{
			if (atlasImage != null)
			{
				atlasImage.rectTransform.sizeDelta = size;
			}
		}

		public void HideTargetGraphic()
		{
			if (atlasImage != null)
			{
				atlasImage.enabled = false;
			}
		}

		private void Load(string assetBundleName, string atlasName, string imageName, LruAssetCacheGeneral<UnityEngine.Object> lruCache = null)
		{
			_bundleName = assetBundleName;
			_imageName = imageName;
			_reserved = false;
			_reserveBundleName = null;
			_reserveAtlasName = null;
			_reserveImageName = null;
			_reserveLruCache = null;

			SetActiveLoadingObject(showIndicator);
			SetActiveNotFoundObject(false);
			bool success = TryLoadFromResources(atlasName, imageName);
			_loadStatus = success ? AssetManager.BundleElementLoadStatus.Success : AssetManager.BundleElementLoadStatus.Error;
			SetActiveLoadingObject(false);
			SetActiveNotFoundObject(!success);

			if (success)
			{
				_onSuccess?.Invoke(null);
			}
			else
			{
				// TODO: load atlases asynchronously through AssetManager/LruAssetCache.
				_onError?.Invoke(null);
			}
		}

		private UniTask LoadWithLruCache(string assetBundleName, string fileName, LruAssetCacheGeneral<UnityEngine.Object> lruCache)
		{
			Load(assetBundleName, fileName, _reserveImageName, lruCache);
			return UniTask.CompletedTask;
		}

		private bool TryLoadFromResources(string atlasName, string imageName)
		{
			if (atlasImage == null || string.IsNullOrEmpty(imageName))
			{
				return false;
			}

			SpriteAtlas atlas = null;
			if (!string.IsNullOrEmpty(atlasName))
			{
				atlas = Resources.Load<SpriteAtlas>(atlasName);
			}

			if (atlas != null)
			{
				atlasImage.Atlas = atlas;
				atlasImage.SpriteName = imageName;
				atlasImage.enabled = atlasImage.sprite != null;
				return atlasImage.sprite != null;
			}

			Sprite sprite = Resources.Load<Sprite>(imageName);
			atlasImage.sprite = sprite;
			atlasImage.enabled = sprite != null;
			return sprite != null;
		}

		private void OnLoadFinished(AssetManager.BundleElement element)
		{
			_loadStatus = element != null ? AssetManager.BundleElementLoadStatus.Success : AssetManager.BundleElementLoadStatus.Error;
			if (_loadStatus == AssetManager.BundleElementLoadStatus.Success)
			{
				_onSuccess?.Invoke(element);
			}
			else
			{
				_onError?.Invoke(element);
			}
		}

		private void OnDisable()
		{
			if (onDisableUnload)
			{
				Unload();
			}
		}

		public UIAtlasImageLoader()
		{
		}
	}
}
