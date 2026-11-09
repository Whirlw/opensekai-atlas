using UnityEngine;
using Sekai;

namespace Sekai.UI
{
	public sealed class UIPartsIconInfoView : MonoBehaviour
	{
		public class ViewData
		{
						private string _InfoTextKey;
			public string InfoTextKey
			{
			    get => _InfoTextKey;
			    set => _InfoTextKey = value;
			}

						private bool _IsLocked;
			public bool IsLocked
			{
			    get => _IsLocked;
			    set => _IsLocked = value;
			}

			public ViewData()
			{
			}
		}

		[SerializeField]
		private GameObject _infoTextRoot;

		[SerializeField]
		private CustomTextMesh _infoText;

		[SerializeField]
		private GameObject _lockedObject;

		public void Setup(ViewData viewData)
		{
			if (viewData == null)
			{
				return;
			}

			SetInfoText(viewData.InfoTextKey);
			if (_lockedObject != null)
			{
				_lockedObject.SetActive(viewData.IsLocked);
			}
		}

		private void SetInfoText(string infoTextKey)
		{
			bool hasInfo = !string.IsNullOrEmpty(infoTextKey);
			if (_infoTextRoot != null)
			{
				_infoTextRoot.SetActive(hasInfo);
			}

			if (hasInfo && _infoText != null)
			{
				_infoText.SetText(WordingManager.Get(infoTextKey));
			}
		}

		public void Show()
		{
			gameObject.SetActive(true);
		}

		public void Hide()
		{
			gameObject.SetActive(false);
		}

		public UIPartsIconInfoView()
		{
		}
	}
}
