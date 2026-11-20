using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public class UIPartsMusicDifficulty : MonoBehaviour
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_defaultImage")]
		[SerializeField]
		private CustomImage defaultImage;

		[UnityEngine.Serialization.FormerlySerializedAs("_appendObject")]
		[SerializeField]
		private GameObject appendObject;

		public void Setup(MusicDifficulty musicDifficulty)
		{
			bool isAppend = musicDifficulty == MusicDifficulty.append;
			if (isAppend)
			{
				if (defaultImage != null)
				{
					defaultImage.SetActive(false);
				}
				if (appendObject != null)
				{
					appendObject.SetActive(true);
				}
				return;
			}

			if (defaultImage != null)
			{
				defaultImage.color = ColorUtility.GetDifficultyColor(musicDifficulty);
				defaultImage.SetActive(true);
			}
			if (appendObject != null)
			{
				appendObject.SetActive(false);
			}
		}

		public void Setup(string musicDifficulty)
		{
			if (!System.Enum.TryParse(musicDifficulty, true, out MusicDifficulty parsed))
			{
				return;
			}
			Setup(parsed);
		}

		public virtual void Show()
		{
			gameObject.SetActive(true);
			if (defaultImage != null)
			{
				defaultImage.gameObject.SetActive(true);
			}
		}

		public virtual void Hide()
		{
			gameObject.SetActive(false);
		}

		public UIPartsMusicDifficulty()
		{
		}
	}
}
