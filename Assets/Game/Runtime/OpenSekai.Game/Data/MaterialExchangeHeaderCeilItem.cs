using Sekai.UI;
using UnityEngine;
using CP;

namespace Sekai
{
	public class MaterialExchangeHeaderCeilItem : MonoBehaviour
	{
		public class ViewData
		{
						private string _assetBundleName;
			public string AssetBundleName
			{
			    get => _assetBundleName;
			    set => _assetBundleName = value;
			}

						private string _assetBundleAtlasName;
			public string AssetBundleAtlasName
			{
			    get => _assetBundleAtlasName;
			    set => _assetBundleAtlasName = value;
			}

						private string _iconName;
			public string IconName
			{
			    get => _iconName;
			    set => _iconName = value;
			}

						private int _number;
			public int Number
			{
			    get => _number;
			    set => _number = value;
			}

			public ViewData()
			{
			}
		}

		[UnityEngine.Serialization.FormerlySerializedAs("_recoveryIcon")]
		[SerializeField]
		private CustomImage recoveryIcon;

		[UnityEngine.Serialization.FormerlySerializedAs("_atlasLoader")]
		[SerializeField]
		private UIAtlasImageLoader atlasLoader;

		[UnityEngine.Serialization.FormerlySerializedAs("_number")]
		[SerializeField]
		private CustomTextMesh number;

		private ViewData _viewData;

		public void Setup(ViewData data, bool setNativeSize = false)
		{
			_viewData = data;
			atlasLoader.gameObject.SetActive(false);
			recoveryIcon.SetActive(false);

			atlasLoader.Load(data.AssetBundleName, data.AssetBundleAtlasName, data.IconName, delegate
			{
				OnSuccessLoadAtlas(setNativeSize);
			}, delegate
			{
				OnErrorLoadAtlas(setNativeSize);
			});
			number.SetText(data.Number.ToString());
		}

		private void OnSuccessLoadAtlas(bool setNativeSize)
		{
			atlasLoader.gameObject.SetActive(true);
			recoveryIcon.SetActive(false);
			if (setNativeSize)
			{
				atlasLoader.AtlasImage.SetNativeSize();
			}
		}

		private void OnErrorLoadAtlas(bool setNativeSize)
		{
			atlasLoader.gameObject.SetActive(false);
			recoveryIcon.SetActive(true);
			recoveryIcon.SpriteName = _viewData.IconName;

			if (setNativeSize)
			{
				recoveryIcon.SetNativeSize();
			}

			if (recoveryIcon.sprite == null)
			{
				string atlasName = recoveryIcon.Atlas != null ? recoveryIcon.Atlas.name : "_recoveryIcon has no atlas assigned";
				CP.LogUtility.LogError(
					_viewData.IconName + " was not found. Reference [" +
					_viewData.AssetBundleName + "/" + _viewData.AssetBundleAtlasName + "], [" + atlasName + "]");
			}
		}

		public MaterialExchangeHeaderCeilItem()
		{
		}
	}
}
