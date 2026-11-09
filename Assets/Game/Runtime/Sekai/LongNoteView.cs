using Sekai.Live;
using OpenSekai.Shared.Diagnostics;
using UnityEngine;

namespace Sekai
{
	public class LongNoteView : BaseNoteView
	{
		public override void Move()
		{
			if (note == null)
			{
				return;
			}

			if (note.Progress <= 0f)
			{
				if (LiveTrace.IsOn(LiveTraceCategory.View))
				{
					LiveTrace.Write(LiveTraceCategory.View, "viewHide barT=" + note.MusicScoreInfo.time.ToString("F3") + " state=" + note.State + " raw=" + note.Progress.ToString("F3"));
				}

				transform.localScale = Vector3.zero;
				return;
			}

			float progress = CalcProgress();
			if (LiveTrace.IsOn(LiveTraceCategory.View))
			{
				LiveTrace.Write(LiveTraceCategory.View, "view barT=" + note.MusicScoreInfo.time.ToString("F3") + " state=" + note.State + " raw=" + note.Progress.ToString("F3") + " view=" + progress.ToString("F3"));
			}

			transform.localScale = Vector3.one * progress;
			transform.localPosition = CalcNotePosition(ref progress);
			UpdateSpritePayload(progress);

			if (note.Category == Sekai.Live.NoteCategory.Long && spriteRenderer != null)
			{
				spriteRenderer.size = LiveUtility.CalcSpriteRendererSize(note.LaneEndF - note.LaneStartF);
			}
		}

		protected float CalcProgress()
		{
			if (note == null)
			{
				return 0f;
			}

			float progress = note.Progress;
			if (note.State == NoteState.Done)
			{
				return LiveConfig.GetNoteViewProgress(progress);
			}

			// While the bar is alive (held, or released but not finished) the body must not collapse:
			// the view only shrinks once the note is actually done.
			if (progress <= 0f)
			{
				progress = Mathf.Max(progress, 0.001f);
			}

			// The bar's own key stops on the line only for the kinds that stand there. A rub or a guide
			// bar that nobody took keeps travelling past it, like a missed single note does.
			LongNote bar = note as LongNote;
			bool pinsAtLine = bar == null || bar.KeepsHeadOnTheLine;
			if (note.ChildNote != null)
			{
				progress = OpenSekai.Shared.Live.NoteLineProgress.BarKey(progress, pinsAtLine);
			}

			return LiveConfig.GetNoteViewProgress(progress);
		}

		protected override Vector3 CalcNotePosition(ref float progress)
		{
			if (note == null)
			{
				return Vector3.zero;
			}

			Vector2 center = GetLaneCenter(note.LaneStartF, note.LaneEndF);
			Vector2 effectiveSpawnPos = spawnPosition;
			if (note is NoteBase noteBase && noteBase.IsNegativeSpeed)
			{
				// 使用 spawnPosition 关于判定线的对称点作为负流速起点
				effectiveSpawnPos = 2f * center - spawnPosition;
				progress = Mathf.Min(progress, 1f);
			}
			Vector2 position = Vector2.LerpUnclamped(effectiveSpawnPos, center, progress);
			float z = note.State >= NoteState.InputBegan ? 3f - posZ : posZ;
			return new Vector3(position.x, position.y, z);
		}

		public LongNoteView()
		{
		}
	}
}
