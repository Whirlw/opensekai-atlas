using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Sekai;

using OpenSekai.Shared.Diagnostics;
using OpenSekai.Shared.Live;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Sekai.Live
{
	public class FlickNote : NoteBase
	{
		private float _lastInputTime;

		private readonly (int id, bool distance, bool trueDirection)[] _inputResults;

		private float _flickDistance;

		/// <summary>The swipe length this note asks for, in the same units as a touch delta.</summary>
		internal float FlickDistance => _flickDistance;
		internal bool IsNextRushHead { get; }
		internal bool CanStartNextRushHold => State is NoteState.Input or NoteState.Done;

		private FlickTailJudge _tailJudge;

		/// <summary>
		/// The flick ends a bar the player holds: it grades itself by <see cref="FlickTailJudge"/>, and the
		/// bar does not judge it. A flick at the end of a decorative bar is still a plain single flick.
		/// </summary>
		internal bool JudgesAsBarTail => ParentRole == NoteParentRole.Tail && GradesAsBarPart;

		internal FlickTailJudge TailJudge => _tailJudge ??= new FlickTailJudge(this);

		public override NoteState State
		{
			get => state;
			protected set
			{
				if (value == NoteState.Done && state != NoteState.Done)
				{
					if (LiveTrace.IsOn(LiveTraceCategory.Tail))
					{
						var frames = new System.Diagnostics.StackTrace().GetFrames();
						string caller = "";
						for (int i = 1; i < frames.Length && i < 4; i++)
						{
							caller += frames[i].GetMethod().DeclaringType.Name + "." + frames[i].GetMethod().Name + "<";
						}

						LiveTrace.Write(LiveTraceCategory.Tail, "tailDone childT=" + MusicScoreInfo.time.ToString("F3") + " res=" + Result + " caller=" + caller);
					}

					state = NoteState.Done;
					// The verdict is written and published first, and only then does it end the bar: a bar
					// that left before the tail published could recycle its views while the note was still
					// showing its judgement (tail-classes.md §3 R2).
					OnUnSpawnNote();
					NotifyParentFinished();
					return;
				}
				base.State = value;
			}
		}
		public FlickNote()
		{
			_inputResults = new (int id, bool distance, bool trueDirection)[8];
			Category = NoteCategory.Flick;
		}

		public FlickNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, float speedRatio,
			NoteType type, NoteDirection direction, float laneOffset, float screenDpiToInch, bool isNextRushHead = false)
			: base(info, id, laneStart, laneEnd, category, speedRatio, laneOffset, type)
		{
			_inputResults = new (int id, bool distance, bool trueDirection)[8];
			_flickDistance = screenDpiToInch;
			Direction = direction;
			IsNextRushHead = isNextRushHead;
		}

		public override void Excute(MusicScoreInfo currentFrameInfo, float offsetTime)
		{
			if (IsNextRushHead)
			{
				AdvanceNote(currentFrameInfo, offsetTime, settleTimeout: false);
				return;
			}

			if (JudgesAsBarTail)
			{
				TailJudge.SettleFrame(currentFrameInfo.time);
			}

			base.Excute(currentFrameInfo, offsetTime);
			if (State == NoteState.Input
				&& _lastInputTime + ReferenceFrame.SECONDS < currentFrameInfo.time
				&& CalcProgress(currentFrameInfo, offsetTime) >= 1f)
			{
				// A tail is graded by its own swipe rule (FlickTailJudge), never by this settle: doing
				// so graded a stationary finger as Perfect.
				if (ParentRole != NoteParentRole.Tail)
				{
					JudgmentFlick(currentFrameInfo.time);
				}
			}
		}

		internal bool AcceptsNextRushHeadSample(ref LiveTouch touch, float lane)
		{
			if (State != NoteState.Input || touch.phase == InputTouchPhase.Canceled
				|| !LiveUtility.IsJudgmentTiming(LiveUtility.GetINoteToJudgeFrameType(this),
					MusicScoreInfo.time - touch.musicTime)) return false;

			// A registered finger may swipe out of the head's lane into the moving hold.
			return touch.phase == InputTouchPhase.Began
				? !float.IsNegativeInfinity(lane) && JudgeLaneStart <= lane && lane <= JudgeLaneEnd
				: HasInputResult(touch.touchId);
		}

		internal void SettleNextRushHead(float now)
		{
			if (State != NoteState.Done && LiveConfig.IsPastJudgeWindow(
				LiveUtility.GetINoteToJudgeFrameType(this), MusicScoreInfo.time - now))
			{
				JudgeInfo = (NoteResult.Miss, NoteResultDescription.None);
				State = NoteState.Done;
			}
		}

		private bool JudgmentFlick(float musicTime)
		{
			if (_inputResults == null || _inputResults.Length == 0)
			{
				return false;
			}

			bool hasInput = false;
			bool isDirection = false;
			foreach (var result in _inputResults)
			{
				if (result.id != 0 && result.distance && result.trueDirection)
				{
					hasInput = true;
					isDirection = true;
					break;
				}
			}

			if (!hasInput)
			{
				foreach (var result in _inputResults)
				{
					if (result.id != 0 && result.distance)
					{
						hasInput = true;
						isDirection = result.trueDirection;
						break;
					}
				}
			}

			if (!hasInput)
			{
				return false;
			}

			// A flick ending a decorative bar (guide or decoration) is a plain single flick, not a hold's end.
			bool singleFlick = !GradesAsBarPart;
			var judgeInfo = singleFlick
			  ? LiveConfig.CalculateFlickNoteResult(this, musicTime, isDirection)
			  : LiveConfig.CalculateLongEndFlickNoteResult(this, musicTime, isDirection);
			HasNextRushHit = IsNextRush;
			JudgeInfo = onUpdateResult != null ? onUpdateResult(judgeInfo) : judgeInfo;
			State = NoteState.Done;
			return true;
		}

		public override bool IsJudgment(ref LiveTouch touch, float lane)
		{
			// A bar's end flick takes its samples through FlickTailJudge.Offer only.
			if (State == NoteState.Done || JudgesAsBarTail)
			{
				return false;
			}

			// A press arms a flick on no bar; from then on a swipe from any finger completes it - not only
			// the one that armed it - as long as the sample is in the flick's own lanes and inside its judge
			// window. The speed (5 cm/s, the note's flick distance per reference frame) is checked when the
			// sample is read (FlickGesture).
			if (parentNote == null && State == NoteState.Input)
			{
				return JudgeLaneStart <= lane && lane <= JudgeLaneEnd
					&& LiveUtility.IsJudgmentTiming(LiveUtility.GetINoteToJudgeFrameType(this), MusicScoreInfo.time - touch.musicTime);
			}

			if (IsLaneCheck(touch))
			{
				if (JudgeLaneStart > lane || JudgeLaneEnd < lane)
				{
					return false;
				}

				var offset = MusicScoreInfo.time - touch.musicTime;
				if (!LiveUtility.IsJudgmentTiming(LiveUtility.GetINoteToJudgeFrameType(this), offset))
				{
					return false;
				}
			}

			return parentNote != null || State != NoteState.Scrolling || touch.phase == UnityEngine.InputSystem.TouchPhase.Began;
		}

		private bool IsLaneCheck(LiveTouch touch)
		{
			if (State != NoteState.Input)
			{
				return true;
			}

			foreach (var result in _inputResults)
			{
				if (result.id != 0 && result.id == touch.touchId)
				{
					return false;
				}
			}

			return true;
		}

		public override bool Judgment(ref LiveTouch touch, float lane)
		{
			if (State == NoteState.Done || JudgesAsBarTail)
			{
				return false;
			}

			if (State == NoteState.Scrolling)
			{
				State = NoteState.Input;
			}

			if (State != NoteState.Input)
			{
				return false;
			}

			var isUpdatedDelta = false;
			var isTrueDirection = false;
			if (parentNote == null)
			{
				if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
				{
					AddInputResult(ref touch, ref isUpdatedDelta, ref isTrueDirection);
					return false;
				}

				// Any finger's movement completes an armed flick, not only the one that armed it.
				UpdateDelta(touch.delta, ref isUpdatedDelta, ref isTrueDirection);
				if (!isUpdatedDelta)
				{
					return false;
				}

				AddInputResult(ref touch, ref isUpdatedDelta, ref isTrueDirection);
				return FinishFlick(touch.musicTime);
			}

			UpdateDelta(touch.delta, ref isUpdatedDelta, ref isTrueDirection);
			if (isUpdatedDelta)
			{
				AddInputResult(ref touch, ref isUpdatedDelta, ref isTrueDirection);
			}

			_lastInputTime = touch.musicTime;
			if (MusicScoreInfo.time - touch.musicTime <= -LiveConfig.OnTimeWindowHalfSeconds && isTrueDirection && isUpdatedDelta)
			{
				return FinishFlick(touch.musicTime);
			}

			// The swipe was recorded but the note completes in Excute once the gesture has settled, so
			// this press does not spend the finger's frame.
			return false;
		}

		/// <summary>Completes the flick here and records that the press spent the frame.</summary>
		private bool FinishFlick(float musicTime)
		{
			if (OpenSekai.Shared.Diagnostics.LiveTrace.IsOn(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Tail))
			{
				OpenSekai.Shared.Diagnostics.LiveTrace.Write(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Tail, $"flickFinished t={MusicScoreInfo.time:F3} now={musicTime:F3} barT={(parentNote == null ? 0f : parentNote.MusicScoreInfo.time):F3} isTail={parentNote != null && ReferenceEquals(parentNote.ChildNote, this)}");
			}

			return FinishFlickCore(musicTime);
		}

		private bool FinishFlickCore(float musicTime)
		{
			bool done = JudgmentFlick(musicTime);
			return done;
		}

		private void AddInputResult(ref LiveTouch touch, ref bool isUpdatedDelta, ref bool isTrueDirection)
		{
			for (var i = 0; i < _inputResults.Length; i++)
			{
				if (_inputResults[i].id == 0 || _inputResults[i].id == touch.touchId)
				{
					_inputResults[i] = (touch.touchId, isUpdatedDelta, isTrueDirection);
					return;
				}
			}
		}

		private void UpdateDelta(Vector2 touchDelta, ref bool isUpdatedDelta, ref bool isTrueDirection)
		{
			if (!OpenSekai.Shared.Live.FlickGesture.TryUpdate(
				touchDelta, LiveUtility.FrameDeltaTime, Direction, _flickDistance, out bool trueDirection))
			{
				isUpdatedDelta = false;
				isTrueDirection = false;
				return;
			}

			isUpdatedDelta = true;
			isTrueDirection = trueDirection;
		}

		/// <summary>
		/// The grade of a bar's end flick swiped at <paramref name="musicTime"/>: the end table the bar
		/// used to grade it with, and a swipe the wrong way never reaches Perfect.
		/// </summary>
		internal (NoteResult, NoteResultDescription) GradeAsTail(float musicTime, bool trueDirection)
		{
			var hit = LongEndJudgement.EvaluateHit(musicTime - MusicScoreInfo.time);
			if (!trueDirection && hit.result == NoteResult.Perfect)
			{
				return (NoteResult.Great, NoteResultDescription.FlickMiss);
			}

			return hit;
		}

		/// <summary>Writes the tail judge's verdict and finishes the note.</summary>
		internal void FinishAsTail((NoteResult, NoteResultDescription) judgeInfo)
		{
			if (State == NoteState.Done)
			{
				return;
			}

			if (LiveTrace.IsOn(LiveTraceCategory.Tail))
			{
				LiveTrace.Write(LiveTraceCategory.Tail, $"flickTailJudged t={MusicScoreInfo.time:F3} res={judgeInfo.Item1}");
			}

			JudgeInfo = onUpdateResult != null ? onUpdateResult(judgeInfo) : judgeInfo;
			State = NoteState.Done;
		}

		public override NoteResult CalcNoteResult(float musicTime)
		{
			return (!GradesAsBarPart
			  ? LiveConfig.CalculateFlickNoteResult(this, musicTime, true)
			  : LiveConfig.CalculateLongEndFlickNoteResult(this, musicTime, true)).Item1;
		}

		public override void ResetNote()
		{
			base.ResetNote();
			Array.Clear(_inputResults, 0, _inputResults.Length);
			_lastInputTime = 0f;
			_tailJudge?.Reset();
		}

		/// <summary>Whether a press of <paramref name="touchId"/> armed this flick, which now waits for a swipe.</summary>
		internal bool IsArmedBy(int touchId) => State == NoteState.Input && HasInputResult(touchId);

		private bool HasInputResult(int touchId)
		{
			foreach (var result in _inputResults)
			{
				if (result.id != 0 && result.id == touchId)
				{
					return true;
				}
			}

			return false;
		}
	}
}
