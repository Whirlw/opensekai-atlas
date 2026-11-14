using UnityEngine;

namespace Sekai.UI
{
	public class ListViewItem : MonoBehaviour
	{
		[SerializeField]
		private float _sizeX;

		[SerializeField]
		private float _sizeY;

		protected int currentIndex;

		public Vector3 SettingAnchoredPosition { get; set; }

		public float SizeX
		{
			get
			{
				return _sizeX;
			}
		}

		public float SizeY
		{
			get
			{
				return _sizeY;
			}
		}

		public int CurrentIndex
		{
			get
			{
				return currentIndex;
			}
		}

		public virtual void SetData(int index)
		{
			currentIndex = index;
		}

		public ListViewItem()
		{
		}
	}
}
