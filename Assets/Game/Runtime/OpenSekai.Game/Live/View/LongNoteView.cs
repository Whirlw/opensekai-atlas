using Sekai.Live;
using OpenSekai.Shared.Diagnostics;
using UnityEngine;

namespace Sekai
{
	public class LongNoteView : BaseNoteView
	{
		public override void Move(float musicTime)
		{
			if (note == null)
			{
				return;
			}

			if (note.Progress <= 0f)
			{
				if (LiveTrace.IsOn(LiveTraceCategory.Frame))
				{
					LiveTrace.Write(LiveTraceCategory.Frame, "viewHide barT=" + note.MusicScoreInfo.time.ToString("F3") + " state=" + note.State + " raw=" + note.Progress.ToString("F3"));
				}

				transform.localScale = Vector3.zero;
				return;
			}

			// A bar's key stands on the line after its head has been taken - that is the pin. It must not
			// outlive the note: once the bar is done, or its end note's own time has arrived (whichever comes
			// first), the key has nothing left to show and is taken off instead of staying pinned there with
			// the body.
			// A rub head's key goes the moment the head is judged, like a standalone friction note.
			if (note.State == NoteState.Done
				|| (note is LongNote endedBar && (endedBar.HeadKeyLeftWithItsVerdict
					|| (endedBar.ChildNote != null && musicTime >= endedBar.ChildNote.MusicScoreInfo.time))))
			{
				if (LiveTrace.IsOn(LiveTraceCategory.Frame))
				{
					LiveTrace.Write(LiveTraceCategory.Frame, "viewHide barT=" + note.MusicScoreInfo.time.ToString("F3") + " state=" + note.State + " now=" + musicTime.ToString("F3") + " end");
				}

				transform.localScale = Vector3.zero;
				return;
			}

			float progress = CalcProgress();
			if (LiveTrace.IsOn(LiveTraceCategory.Frame))
			{
				LiveTrace.Write(LiveTraceCategory.Frame, "view barT=" + note.MusicScoreInfo.time.ToString("F3") + " state=" + note.State + " raw=" + note.Progress.ToString("F3") + " view=" + progress.ToString("F3"));
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

			float progress = LiveConfig.ViewProgressOf(note);
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

			// The bar's own key stops on the line only for the kinds that stand there - an ordinary head, the
			// one the player presses and holds. A rub plays like a standalone friction note and a guide bar
			// sweeps across the lane, so both keep travelling past the line like a missed single note.
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
			// A bar that was ever taken draws in front of the line; that is a fact about the bar, not a phase of a finger.
			float z = note is LongNote touchedBar && touchedBar.WasTouched ? 3f - posZ : posZ;
			return new Vector3(position.x, position.y, z);
		}

		public LongNoteView()
		{
		}
	}
}
