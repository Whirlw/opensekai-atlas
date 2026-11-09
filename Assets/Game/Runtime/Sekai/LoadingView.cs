using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public class LoadingView : MonoBehaviour
	{
		[SerializeField]
		private CustomTextMesh _title;

		[SerializeField]
		private CustomTextMesh _description;

		[SerializeField]
		private UITextureLoader _imageLoader;

		private MasterTip _masterTip;

		public void Setup(MasterTip tip)
		{
			_masterTip = tip;
			if (tip == null)
			{
				return;
			}

			if (tip.GetViewType() == MasterTip.ViewType.TYPE_TEXT_TIPS)
			{
				if (_title != null)
				{
					_title.SetText(tip.title ?? string.Empty);
				}
				if (_description != null)
				{
					_description.SetText(tip.description ?? string.Empty);
				}
			}
			else if (_imageLoader != null && !string.IsNullOrEmpty(tip.assetbundleName))
			{
				_imageLoader.Load("comic/one_frame", tip.assetbundleName);
			}
		}

		public void SetDescription(string text)
		{
			if (_description != null)
			{
				_description.SetText(text ?? string.Empty);
			}
		}

		public void Unload()
		{
			if (_masterTip != null && _masterTip.GetViewType() == MasterTip.ViewType.TYPE_COMIC)
			{
				_imageLoader?.Unload();
			}
		}

		public LoadingView()
		{
		}
	}
}
