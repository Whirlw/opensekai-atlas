using Sekai.Live;
using UnityEngine;

namespace Sekai.Core.Live
{
	internal sealed class LiveNoteFrame
	{
		private readonly LiveLogic _logic;
		private readonly LiveSessionState _session;
		private readonly LiveInputRouter _input;
		private readonly LivePresentationBridge _presentation;

		internal LiveNoteFrame(LiveLogic logic, LiveSessionState session, LiveInputRouter input, LivePresentationBridge presentation)
		{
			_logic = logic;
			_session = session;
			_input = input;
			_presentation = presentation;
		}

		private MusicScoreInfo Frame => _logic.currentFrameInfo;

		internal void TickViews()
		{
			_presentation.TickTailKeys(Frame.time);
			_presentation.TickRetiring(Frame);
		}

		internal void UpdateNotes()
		{
			// Heads first: a head settles on its own window, and that window may close on a frame the
			// bar itself is no longer walked (a tail ended it earlier).
			_session.Notes.TickHeads(Frame.time);
			_session.Notes.WalkNotes(ScanNote);
			_session.Notes.WalkHighSpeedNotes(ScanHighSpeedNote);
		}

		/// <summary>
		/// One normal note on its display frame. Returns <see cref="LiveNoteSession.Scan.Stop"/> once the
		/// array has reached notes that are still too far ahead, which ends the walk for this frame.
		/// </summary>
		private LiveNoteSession.Scan ScanNote(NoteBase note)
		{
			if (note.IsHighSpeedNote)
			{
				return LiveNoteSession.Scan.Skip;
			}

			float offsetTime = CalcTimeOffset(note);
			if (IsTooFarAheadForDisplayHighSpeed(offsetTime, note))
			{
				return LiveNoteSession.Scan.Stop;
			}
			if (offsetTime < 0f && Frame.time >= note.MusicScoreInfo.time)
			{
				offsetTime = -offsetTime;
			}

			PreloadView(note, offsetTime);
			note.Excute(Frame, offsetTime);
			RegisterOpenHead(note);

			// Decoration notes need to be processed after Excute to check OffsetJudgeTime
			if (note.IsDecoration && note.State != NoteState.Done && ShouldTriggerDecorationJudgment(note))
			{
				DecorationJudgment(note);
			}

			if (note.State != NoteState.Done && (!note.IsDecoration || PlayableTailOf(note)))
			{
				_input.Register(note);
			}

			return LiveNoteSession.Scan.Handle;
		}

		/// <summary>
		/// Hands the bar's head to the head sweep. The head has its own window and may outlive the bar's
		/// being walked (a tail can end the bar before the head window closes), so it cannot rely on
		/// this scan to reach it every frame.
		/// </summary>
		private void RegisterOpenHead(NoteBase note)
		{
			if (note is LongNote bar && bar.PublishesHeadMiss)
			{
				_session.Notes.RegisterHead(bar.Head);
				// The head is a candidate of its own: the press loop asks this object, so the verdict
				// never travels through the bar's Judgement. The bar stays in the list for the hold
				// coordinator, which finds bars by type.
				_input.Register(bar.Head.Candidate);
			}
		}

		/// <summary>
		/// How much earlier than its own display window a note's view is built, so that building it can never
		/// decide where the note first appears.
		/// </summary>
		internal const float PreloadMarginSeconds = 0.1f;

		/// <summary>
		/// Builds the note's view a little before it can be seen. Creating a view is what used to happen at
		/// whatever progress the frame loop reached it at; doing it a fixed 100 ms early means the note is
		/// already there when its window opens, and the drawn position is what decides where it appears.
		/// </summary>
		private void PreloadView(NoteBase note, float offsetTime)
		{
			if (note.State == NoteState.Done || offsetTime <= 0f)
			{
				return;
			}

			float timeLeft = note.MusicScoreInfo.time - Frame.time;
			if (timeLeft > offsetTime + PreloadMarginSeconds)
			{
				return;
			}

			// Idempotent on the view side (the note pool keys off the note), so it can simply be asked every
			// frame of the margin instead of remembering that it was asked.
			_presentation.Spawn(_session.Views, note, Frame.time);
		}

		/// <summary>One high speed note: same shape, its own display lead.</summary>
		private LiveNoteSession.Scan ScanHighSpeedNote(NoteBase note)
		{
			float offsetTime = Frame.time >= note.MusicScoreInfo.time
				? _session.NoteDisplayTimeOffset
				: CalcTimeOffset(note);
			if (IsTooFarAheadForDisplay(offsetTime, note))
			{
				return LiveNoteSession.Scan.Stop;
			}

			PreloadView(note, offsetTime);
			note.Excute(Frame, offsetTime);
			RegisterOpenHead(note);

			if (note.IsDecoration && note.State != NoteState.Done && ShouldTriggerDecorationJudgment(note))
			{
				DecorationJudgment(note);
			}

			if (note.State != NoteState.Done && (!note.IsDecoration || PlayableTailOf(note)))
			{
				_input.Register(note);
			}

			return LiveNoteSession.Scan.Handle;
		}

		private bool ShouldTriggerDecorationJudgment(NoteBase note)
		{
			if (note == null)
			{
				return false;
			}

			// For long notes, trigger at both start and end times
			if (note is LongNote longNote)
			{
				// Trigger at start time (head)
				if (Frame.time >= longNote.MusicScoreInfo.time && longNote.State == NoteState.Scrolling)
				{
					return true;
				}

				// Trigger at end time (tail)
				if (longNote.NoteList != null && longNote.NoteList.Count > 0)
				{
					NoteBase lastChild = longNote.NoteList[longNote.NoteList.Count - 1];
					if (lastChild != null && Frame.time >= lastChild.MusicScoreInfo.time)
					{
						return true;
					}
				}

				return false;
			}

			// For normal notes, trigger at the judgment time
			return Frame.time >= note.MusicScoreInfo.time;
		}

		private void DecorationJudgment(NoteBase note)
		{
			if (note == null || note.State == NoteState.Done)
			{
				return;
			}

			// Decoration notes are auto-judged when reaching judgment time
			// Play tap effect and SE, but do not trigger score/combo callbacks or judgment text

			// For long notes, handle specially: play head effect at start, keep body showing, play end effect at end
			if (note is LongNote longNote)
			{
				DecorationLongNoteJudgment(longNote);
				return;
			}

			// Set JudgeInfo to Perfect for effect playback (TapEffectView checks Result)
			note.SetJudgeInfoForDecoration(NoteResult.JustPerfect);
			// For normal notes: play effect and remove
			_presentation.Judged(_session.Views, note);
			note.SetStateUnnotice(NoteState.Done);
			_presentation.Unspawn(_session.Views, note);
		}

		private void DecorationLongNoteJudgment(LongNote longNote)
		{
			if (longNote == null)
			{
				return;
			}

			// Check if we are at the start or end of the long note
			NoteBase lastChild = longNote.NoteList != null && longNote.NoteList.Count > 0
				? longNote.NoteList[longNote.NoteList.Count - 1]
				: null;

			bool isAtEnd = lastChild != null && Frame.time >= lastChild.MusicScoreInfo.time;
			bool isAtStart = Frame.time >= longNote.MusicScoreInfo.time && longNote.State == NoteState.Scrolling;

			// At start: play head effect, but keep the long note showing (don't mark as Done)
			if (isAtStart && !isAtEnd && longNote.Result == NoteResult.None)
			{
				// Set JudgeInfo to Perfect for effect playback (TapEffectView checks Result)
				longNote.SetJudgeInfoForDecoration(NoteResult.JustPerfect);
				// Play head judgment effect (tap effect and SE)
				_presentation.Judged(_session.Views, longNote);
			}

			// 头部触发之后，处理中间的LongHoldCombo自动判定
			if (longNote.NoteList != null && longNote.Result != NoteResult.None)
			{
				foreach (NoteBase childNote in longNote.NoteList)
				{
					// 跳过LongNote自身和尾部，已经处理过
					if (childNote == longNote || childNote == lastChild)
					{
						continue;
					}

					// 只处理LongHoldCombo
					if (childNote is LongHoldCombo holdCombo && childNote.State != NoteState.Done)
					{
						// 检查该combo点是否到达判定时间
						if (childNote.MusicScoreInfo.time <= Frame.time)
						{
							// 自动判定该combo点；它只是隐形刻度，不发声也不出特效
							holdCombo.SetJudgeInfoForDecoration(NoteResult.JustPerfect);
							holdCombo.SetStateUnnotice(NoteState.Done);
						}
					}
				}
			}

			// At end: play end effect using the last child note's type and remove the long note
			// The end note is a real note now and judges itself; settling it here would take the
			// hit away from the player and end the bar before it could be made.
			if (isAtEnd && lastChild != null && lastChild.IsDecoration)
			{
				// The bar ends here; the key at its end is only a decoration and nobody played it, so
				// no tap effect and no hit sound: the bar must never speak for the player.
				longNote.SetStateUnnotice(NoteState.Done);
				_presentation.Unspawn(_session.Views, longNote);
			}
		}

		private bool IsTooFarAheadForDisplayHighSpeed(float timeOffset, NoteBase note)
		{
			return note != null && note.State != NoteState.Scrolling && IsTooFarAheadForDisplay(timeOffset, note);
		}

		/// <summary>
		/// True while the scan may stop here: everything from this note on is still outside the display
		/// window this frame.
		/// </summary>
		/// <remarks>
		/// The gate uses the DISPLAY lead, which shrinks as the scroll speed rises, but a lead can shrink
		/// far below anything a frame can draw: the notes at bar 80.5078 of the shipped chart get a lead of
		/// 0.0007 s because the span behind them holds a 9 ms 799 event. The scan walks notes in chart
		/// order and stops at the first note still outside its lead, so such a note parked the cursor and
		/// the whole stretch behind it was first looked at once the playhead got past - a dozen notes
		/// created in one frame, each at whatever progress it happened to have (p 0.62, 0.04, 0.037 ...).
		/// That is the "notes are generated in the middle of the screen, at a position that keeps moving"
		/// report. The lead is therefore floored at one whole display window: no note can be drawn before
		/// the frame loop has looked at it at least that far ahead.
		///
		/// Nothing about how a note's lead is computed changes here; this only decides how far the walk
		/// goes. Registration into the judgement list is a different question with a fixed lead; see
		/// <see cref="OpenSekai.Shared.Live.JudgeListLead"/> in LiveInputRouter.Register. This gate must
		/// never be reused for registration: doing that is what put a note behind a flick into the list
		/// already past its bad window.
		/// </remarks>
		internal bool IsTooFarAheadForDisplay(float timeOffset, NoteBase note)
		{
			if (note == null || Mathf.Approximately(timeOffset, 0f))
			{
				return false;
			}

			float lead = Mathf.Max(timeOffset, _session.NoteDisplayTimeOffset);
			return Frame.time + lead + LiveClock.ReferenceFrameSeconds <= note.MusicScoreInfo.time;
		}

		/// <summary>
		/// A decorative bar takes no input itself, but the note at its end is a real note of its
		/// own kind (flick, slide, rub) and has to stay playable. Such a bar therefore keeps being
		/// fed to the judgement list even though it is decoration.
		/// </summary>
		private static bool PlayableTailOf(NoteBase note)
		{
			return note is LongNote bar && bar.ChildNote is NoteBase tail && tail.HasJudgment;
		}

		internal void Spawn(NoteBase note)
		{
			_presentation.Spawn(_session.Views, note, Frame.time);
		}

		internal void Unspawn(NoteBase note)
		{
			_presentation.Unspawn(_session.Views, note);
		}

		internal float CalcTimeOffset(NoteBase note)
		{
			if (note == null)
			{
				return 0f;
			}

			float currentProgress = Frame.bar + Frame.barProgress;
			float noteProgress = note.MusicScoreInfo.bar + note.MusicScoreInfo.barProgress;
			float speedRatio = CalcNoteSpeedRatio(currentProgress, noteProgress);

			bool isNegativeSpeed = note.IsNegativeSpeed;
			float timeOffset = OpenSekai.Shared.Live.NoteDisplayOffset.Resolve(
				_session.NoteDisplayTimeOffset, speedRatio, note.speedRatio, ref isNegativeSpeed, note.offsetTime);
			note.IsNegativeSpeed = isNegativeSpeed;
			return timeOffset;
		}

		private float CalcNoteSpeedRatio(float currentProgress, float noteProgress)
		{
			return OpenSekai.Shared.Live.NoteSpeedRatio.Resolve(
				_session.MusicScore?.musicScoreInfoArray, currentProgress, noteProgress, Frame.speedRatio);
		}
	}
}
