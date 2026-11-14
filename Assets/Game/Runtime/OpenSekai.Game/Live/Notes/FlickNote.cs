using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Sekai;

using OpenSekai.Shared.Diagnostics;
using OpenSekai.Shared.Live;

namespace Sekai.Live
{
	public class FlickNote : NoteBase
	{
		private float _lastInputTime;

		private readonly (int id, bool distance, bool trueDirection)[] inputResults;

		private float _flickDistance;

		/// <summary>The swipe length this note asks for, in the same units as a touch delta.</summary>
		internal float FlickDistance => _flickDistance;

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
			inputResults = new (int id, bool distance, bool trueDirection)[8];
			Category = NoteCategory.Flick;
		}

		public FlickNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, float speedRatio, NoteType type, NoteDirection direction, float laneOffset, float screenDpiToInch)
			: base(info, id, laneStart, laneEnd, category, speedRatio, laneOffset, type)
		{
			inputResults = new (int id, bool distance, bool trueDirection)[8];
			_flickDistance = screenDpiToInch;
			Direction = direction;
		}

		public override void Excute(MusicScoreInfo currentFrameInfo, float offsetTime)
		{
			base.Excute(currentFrameInfo, offsetTime);
			if (State == NoteState.Input
				&& _lastInputTime + ReferenceFrame.Seconds < currentFrameInfo.time
				&& CalcProgress(currentFrameInfo, offsetTime) >= 1f)
			{
				// A tail belongs to its bar: the bar decides the swipe, so the note must not
				// complete itself here. Doing so graded a stationary finger as JustPerfect.
				if (ParentRole != NoteParentRole.Tail)
				{
					JudgmentFlick(currentFrameInfo.time);
				}
			}
		}

		private bool JudgmentFlick(float musicTime)
		{
			if (inputResults == null || inputResults.Length == 0)
			{
				return false;
			}

			bool hasInput = false;
			bool isDirection = false;
			foreach (var result in inputResults)
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
				foreach (var result in inputResults)
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

			bool singleFlick = parentNote == null || parentNote.IsDecoration;
			// A flick ending a decorative bar is a plain single flick, not part of a hold.
			var judgeInfo = singleFlick
			  ? LiveConfig.CalculateFlickNoteResult(this, musicTime, isDirection)
			  : LiveConfig.CalculateLongEndFlickNoteResult(this, musicTime, isDirection);
			JudgeInfo = onUpdateResult != null ? onUpdateResult(judgeInfo) : judgeInfo;
			State = NoteState.Done;
			return true;
		}

		public override bool IsJudgment(ref LiveTouch touch, float lane)
		{
			if (State == NoteState.Done)
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

			foreach (var result in inputResults)
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
			if (State == NoteState.Done)
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
			if (MusicScoreInfo.time - touch.musicTime <= -LiveConfig.JustPerfectJudgeTimeHalf && isTrueDirection && isUpdatedDelta)
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
			for (var i = 0; i < inputResults.Length; i++)
			{
				if (inputResults[i].id == 0 || inputResults[i].id == touch.touchId)
				{
					inputResults[i] = (touch.touchId, isUpdatedDelta, isTrueDirection);
					return;
				}
			}
		}

		private void UpdateDelta(Vector2 touchDelta, ref bool isUpdatedDelta, ref bool isTrueDirection)
		{
			if (!OpenSekai.Shared.Live.FlickGesture.TryUpdate(
				touchDelta, Time.deltaTime, Direction, _flickDistance, out bool trueDirection))
			{
				isUpdatedDelta = false;
				isTrueDirection = false;
				return;
			}

			isUpdatedDelta = true;
			isTrueDirection = trueDirection;
		}

		public override NoteResult CalcNoteResult(float musicTime)
		{
			return ((parentNote == null || parentNote.IsDecoration)
			  ? LiveConfig.CalculateFlickNoteResult(this, musicTime, true)
			  : LiveConfig.CalculateLongEndFlickNoteResult(this, musicTime, true)).Item1;
		}

		public override void ResetNote()
		{
			base.ResetNote();
			Array.Clear(inputResults, 0, inputResults.Length);
			_lastInputTime = 0f;
		}

		/// <summary>Whether a press of <paramref name="touchId"/> armed this flick, which now waits for a swipe.</summary>
		internal bool IsArmedBy(int touchId) => State == NoteState.Input && HasInputResult(touchId);

		private bool HasInputResult(int touchId)
		{
			foreach (var result in inputResults)
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
