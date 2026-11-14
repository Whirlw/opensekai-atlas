using Sekai.UI;
using UnityEngine;
using CP;

namespace Sekai
{
	public class MaterialExchangeHeaderCeilItem : MonoBehaviour
	{
		public class ViewData
		{
						private string _AssetBundleName;
			public string AssetBundleName
			{
			    get => _AssetBundleName;
			    set => _AssetBundleName = value;
			}

						private string _AssetBundleAtlasName;
			public string AssetBundleAtlasName
			{
			    get => _AssetBundleAtlasName;
			    set => _AssetBundleAtlasName = value;
			}

						private string _IconName;
			public string IconName
			{
			    get => _IconName;
			    set => _IconName = value;
			}

						private int _Number;
			public int Number
			{
			    get => _Number;
			    set => _Number = value;
			}

			public ViewData()
			{
			}
		}

		[SerializeField]
		private CustomImage _recoveryIcon;

		[SerializeField]
		private UIAtlasImageLoader _atlasLoader;

		[SerializeField]
		private CustomTextMesh _number;

		private ViewData _viewData;

		public void Setup(ViewData data, bool setNativeSize = false)
		{
			_viewData = data;
			_atlasLoader.gameObject.SetActive(false);
			_recoveryIcon.SetActive(false);

			_atlasLoader.Load(data.AssetBundleName, data.AssetBundleAtlasName, data.IconName, delegate
			{
				OnSuccessLoadAtlas(setNativeSize);
			}, delegate
			{
				OnErrorLoadAtlas(setNativeSize);
			});
			_number.SetText(data.Number.ToString());
		}

		private void OnSuccessLoadAtlas(bool setNativeSize)
		{
			_atlasLoader.gameObject.SetActive(true);
			_recoveryIcon.SetActive(false);
			if (setNativeSize)
			{
				_atlasLoader.AtlasImage.SetNativeSize();
			}
		}

		private void OnErrorLoadAtlas(bool setNativeSize)
		{
			_atlasLoader.gameObject.SetActive(false);
			_recoveryIcon.SetActive(true);
			_recoveryIcon.SpriteName = _viewData.IconName;

			if (setNativeSize)
			{
				_recoveryIcon.SetNativeSize();
			}

			if (_recoveryIcon.sprite == null)
			{
				string atlasName = _recoveryIcon.Atlas != null ? _recoveryIcon.Atlas.name : "_recoveryIcon has no atlas assigned";
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
