using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class FrictionLongNoteView : LongNoteView
	{
		[SerializeField]
		protected SpriteRenderer centerIconRenderer;

		public override void Move(float musicTime)
		{
			base.Move(musicTime);
			if (centerIconRenderer != null && spriteRenderer != null)
			{
				centerIconRenderer.color = spriteRenderer.color;
			}
		}

		public FrictionLongNoteView()
		{
		}
	}
}
