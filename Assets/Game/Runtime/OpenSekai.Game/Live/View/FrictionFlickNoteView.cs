using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class FrictionFlickNoteView : FlickNoteView
	{
		[SerializeField]
		protected SpriteRenderer centerIconRenderer;

		public override void Move(float musicTime)
		{
			if (note == null)
			{
				return;
			}

			if (!LiveConfig.IsKeyVisible(note, musicTime))
			{
				transform.localScale = Vector3.zero;
				return;
			}

			float progress = LiveConfig.GetNoteViewProgress(LiveConfig.ViewProgressOf(note, musicTime));
			if (note is FrictionFlickNote { IsConnectorHeadView: true } marker)
			{
				ApplyStyle(marker.NativeStyle);
				if (spriteRenderer != null)
					spriteRenderer.size = LiveUtility.CalcSpriteRendererSize(marker.LaneEndF - marker.LaneStartF);
			}
			transform.localPosition = CalcNotePosition(ref progress);
			transform.localScale = Vector3.one * progress;
			UpdateFrictionSpritePayload(progress);
			UpdateFlickArrow(musicTime);
		}

		private void UpdateFrictionSpritePayload(float progress)
		{
			if (spriteRenderer == null || note == null || judgmentPositions == null || judgmentPositions.Length == 0)
			{
				return;
			}

			Color color = spriteRenderer.color;
			float left = note.LaneStart, right = note.LaneEnd;
			if (note is FrictionFlickNote { IsConnectorHeadView: true })
				(left, right) = (note.LaneStartF, note.LaneEndF);
			color.r = ((left + right) * 0.5f + 0.5f) / judgmentPositions.Length;
			color.g = (right - left + 0.5f) / judgmentPositions.Length;
			color.b = progress;
			spriteRenderer.color = color;
			if (centerIconRenderer != null)
			{
				centerIconRenderer.color = color;
			}
		}

		public FrictionFlickNoteView()
		{
		}
	}
}
