using CP;
using Sekai.Constants;
using UnityEngine;

namespace Sekai.UI
{
	public class ResourceIconLoader : MonoBehaviour
	{
		[SerializeField]
		private CustomImage _imageIcon;

		[SerializeField]
		private CustomRawImage _rawImageIcon;

		public void Load(ResourceType resourceType, int resourceId)
		{
			SetImageActive(false);
			SetRawImageActive(false);

			switch (resourceType)
			{
				case ResourceType.virtual_coin:
					SetImageSprite("icon_virtual_coin");
					break;
				case ResourceType.jewel:
				case ResourceType.paid_jewel:
					SetImageSprite("icon_jewel");
					break;
				case ResourceType.none:
					break;
				case ResourceType.gacha_ticket:
					SetRawTexture(null);
					// TODO(original): restore GachaUtility.GetTicketResourceTex(resourceId).
					break;
				default:
					LogUtility.LogError("ResourceIconLoader unsupported resource type: {0}", resourceType);
					break;
			}
		}

		private void SetImageSprite(string spriteName)
		{
			if (_imageIcon == null)
			{
				return;
			}

			_imageIcon.gameObject.SetActive(true);
			_imageIcon.SpriteName = spriteName;
		}

		private void SetRawTexture(Texture texture)
		{
			if (_rawImageIcon == null)
			{
				return;
			}

			_rawImageIcon.gameObject.SetActive(true);
			_rawImageIcon.texture = texture;
		}

		private void SetImageActive(bool active)
		{
			if (_imageIcon != null)
			{
				_imageIcon.gameObject.SetActive(active);
			}
		}

		private void SetRawImageActive(bool active)
		{
			if (_rawImageIcon != null)
			{
				_rawImageIcon.gameObject.SetActive(active);
			}
		}

		public ResourceIconLoader()
		{
		}
	}
}
