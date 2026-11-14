using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public class DialogHeader : MonoBehaviour
	{
		[SerializeField]
		private CustomText _title;

		public CustomText Title => _title;

		public string TitleText
		{
			get => _title != null ? _title.Text : string.Empty;
			set
			{
				if (_title != null)
				{
					_title.UseWordingKey = false;
					_title.SetText(value);
				}
			}
		}

		public string TitleKey
		{
			get => _title != null ? _title.WordingKey : string.Empty;
			set
			{
				if (_title != null)
				{
					_title.UseWordingKey = true;
					_title.SetWordingText(value);
				}
			}
		}
	}
}
