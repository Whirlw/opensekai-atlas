using UnityEngine;

namespace Sekai
{
	public class FrictionFlickNoteView : FlickNoteView
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

		public FrictionFlickNoteView()
		{
		}
	}
}
