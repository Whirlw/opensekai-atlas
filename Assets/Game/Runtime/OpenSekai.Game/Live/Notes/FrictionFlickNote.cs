using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Sekai;

using OpenSekai.Shared.Live;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Sekai.Live
{
	public class FrictionFlickNote : NoteBase
	{
		private readonly (int id, bool distance, bool trueDirection)[] inputResults;

		private readonly float _flickDistance;

		/// <summary>The swipe length this note asks for, in the same units as a touch delta.</summary>
		internal float FlickDistance => _flickDistance;
		private float NextRushEarlyWindow => (IsTailFlickOwner ? 7.5f : 6.5f) / 60f;
		private float NextRushLateWindow => (IsTailFlickOwner ? 8.5f : 7.5f) / 60f;
		private float _nextRushCorrectTime;
		private bool _nextRushHadContact;
		internal bool UsesNextRushTraceFlickInput { get; }
		public bool IsTraceFlickOwner { get; }
		public bool IsTailTraceFlickOwner { get; }
		public bool IsTailFlickOwner => IsTraceFlickOwner && Category == NoteCategory.Flick;
		internal bool CanStartNextRushHold => HasCapturedPress || State == NoteState.Done;
		internal bool IsConnectorHeadView { get; }
		internal bool HasCapturedPress { get; private set; }

		public override NoteState State
		{
			get => state;
			protected set
			{
				if (value == NoteState.Done && state != NoteState.Done)
				{
					state = NoteState.Done;
					// Publish first, then tell the bar: the same order as every other end note
					// (tail-classes.md §3 R2).
					OnUnSpawnNote();
					NotifyParentFinished();
					return;
				}
				base.State = value;
			}
		}
		public FrictionFlickNote()
		{
			inputResults = new (int id, bool distance, bool trueDirection)[8];
			Category = NoteCategory.FrictionFlick;
		}

		public FrictionFlickNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, float speedRatio,
			NoteType type, NoteDirection direction, float screenDpiToInch, float laneOffset, bool useNextRushTraceFlickInput = false,
			bool isTraceFlickOwner = false, bool isConnectorHeadView = false, bool isTailTraceFlickOwner = false)
			: base(info, id, laneStart, laneEnd, category, speedRatio, laneOffset, type)
		{
			inputResults = new (int id, bool distance, bool trueDirection)[8];
			_flickDistance = screenDpiToInch;
			Direction = direction;
			IsTraceFlickOwner = isTraceFlickOwner;
			IsTailTraceFlickOwner = isTailTraceFlickOwner;
			IsConnectorHeadView = isConnectorHeadView;
			UsesNextRushTraceFlickInput = useNextRushTraceFlickInput || isTraceFlickOwner || isTailTraceFlickOwner;
		}

		public override void Excute(MusicScoreInfo currentFrameInfo, float offsetTime)
		{
			if (UsesNextRushTraceFlickInput)
			{
				AdvanceNote(currentFrameInfo, offsetTime, settleTimeout: false);
				return;
			}

			// A caught note settles the moment it reaches the line, and that has to be decided BEFORE the base
			// class can close the window. Waiting one more frame after the last input (the old shape of this
			// check) dropped the note on the very frame its window closed: device log
			// live-20261113-152906.log shows the single combo break of that run as childState=Input at
			// now=19.824, Last at 19.830 and res=Miss at 19.836 for t=19.688 - a caught note with a recorded
			// flick, one frame of grace short.
			if (State == NoteState.Input && CalcProgress(currentFrameInfo, offsetTime) >= 1f)
			{
				JudgmentFlick(currentFrameInfo.time);
			}

			base.Excute(currentFrameInfo, offsetTime);
		}

		internal bool ReadNextRushHeadSample(ref LiveTouch touch, float lane, float sampleTime)
		{
			if (State is not (NoteState.Scrolling or NoteState.Input) || touch.phase == InputTouchPhase.Canceled)
				return false;
			float offset = sampleTime - MusicScoreInfo.time;
			if (offset <= -NextRushEarlyWindow || offset >= NextRushLateWindow) return false;

			bool unregistered = IsLaneCheck(touch);
			if (unregistered && (float.IsNegativeInfinity(lane) || lane < JudgeLaneStart || lane > JudgeLaneEnd)) return false;
			State = NoteState.Input;
			bool moved = false, correct = false;
			UpdateDelta(touch.delta, ref moved, ref correct);
			if (unregistered || moved) AddInputResult(ref touch, ref moved, ref correct);
			if (correct) _nextRushCorrectTime = sampleTime;
			return true;
		}

		internal void SettleNextRushHead(float now, bool hasContact)
		{
			float target = MusicScoreInfo.time;
			bool contactRecently = _nextRushHadContact || hasContact;
			_nextRushHadContact = hasContact;
			if (State == NoteState.Done || now < target) return;
			bool hasMotion = false, correct = false;
			foreach (var input in inputResults)
			{
				if (!input.distance) continue;
				hasMotion = true;
				correct |= input.trueDirection;
			}

			if (IsTailFlickOwner && hasMotion && (!contactRecently ||
				correct && _nextRushCorrectTime >= target + LiveConfig.JustPerfectJudgeTimeHalf))
			{
				float time = contactRecently ? _nextRushCorrectTime : now;
				FinishNextRushHead(LiveConfig.CalculateLongEndFlickNoteResult(this, time, correct));
			}
			else if (correct && !IsTailFlickOwner)
			{
				// A valid early trace swipe waits for the head to reach the line.
				FinishNextRushHead((NoteResult.Perfect, NoteResultDescription.None));
			}
			else if (now > target + NextRushLateWindow)
			{
				FinishNextRushHead((NoteResult.Miss, NoteResultDescription.None));
			}
			else if (hasMotion && !contactRecently)
			{
				FinishNextRushHead((NoteResult.Great, NoteResultDescription.FlickMiss));
			}
		}

		private void FinishNextRushHead((NoteResult, NoteResultDescription) result)
		{
			JudgeInfo = onUpdateResult != null ? onUpdateResult(result) : result;
			State = NoteState.Done;
		}

		private bool JudgmentFlick(float musicTime)
		{
			if (inputResults == null || inputResults.Length == 0)
			{
				return false;
			}

			bool hasInput = false;
			bool direction = false;
			foreach (var result in inputResults)
			{
				if (result.id != 0 && result.distance && result.trueDirection)
				{
					hasInput = true;
					direction = true;
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
						direction = result.trueDirection;
						break;
					}
				}
			}


			if (!hasInput)
			{
				return false;
			}

			var judgeInfo = LiveConfig.CalculateFrictionFlickNoteResult(this, musicTime, direction);
			JudgeInfo = onUpdateResult != null ? onUpdateResult(judgeInfo) : judgeInfo;
			State = NoteState.Done;
			return true;
		}

		public override bool IsJudgment(ref LiveTouch touch, float lane)
		{
			if (IsTraceFlickOwner)
			{
				float pressOffset = touch.musicTime - MusicScoreInfo.time;
				return !HasCapturedPress && State is NoteState.Scrolling or NoteState.Input
					&& touch.phase == InputTouchPhase.Began && pressOffset > -NextRushEarlyWindow && pressOffset < NextRushLateWindow
					&& lane >= JudgeLaneStart && lane <= JudgeLaneEnd;
			}
			if (State == NoteState.Done)
			{
				return false;
			}

			if (!IsLaneCheck(touch))
			{
				return true;
			}

			if (JudgeLaneStart > lane || JudgeLaneEnd < lane)
			{
				return false;
			}

			var offset = MusicScoreInfo.time - touch.musicTime;
			return LiveUtility.IsJudgmentTiming(LiveUtility.GetINoteToJudgeFrameType(this), offset);
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
			// A fresh press can unlock a release tail. The swipe itself uses the contact channel.
			if (IsTraceFlickOwner)
			{
				if (!IsJudgment(ref touch, lane)) return false;
				HasCapturedPress = true;
				return true;
			}
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
			var trueDirection = false;
			UpdateDelta(touch.delta, ref isUpdatedDelta, ref trueDirection);
			if (isUpdatedDelta)
			{
				AddInputResult(ref touch, ref isUpdatedDelta, ref trueDirection);
			}

			if (trueDirection && isUpdatedDelta && MusicScoreInfo.time - touch.musicTime <= -LiveConfig.JustPerfectJudgeTimeHalf)
			{
				bool done = JudgmentFlick(touch.musicTime);
				return done;
			}

			return false;
		}

		private void AddInputResult(ref LiveTouch touch, ref bool isUpdatedDelta, ref bool _isTrueDirection)
		{
			for (var i = 0; i < inputResults.Length; i++)
			{
				if (inputResults[i].id == 0 || inputResults[i].id == touch.touchId)
				{
					inputResults[i] = (touch.touchId, isUpdatedDelta, _isTrueDirection);
					return;
				}
			}
		}

		private void UpdateDelta(Vector2 touchDelta, ref bool isUpdatedDelta, ref bool _isTrueDirection)
		{
			if (touchDelta == LiveUtility.Vector2Zero)
			{
				return;
			}

			if (!OpenSekai.Shared.Live.FlickGesture.TryUpdate(
				touchDelta, LiveUtility.FrameDeltaTime, Direction, _flickDistance, out bool trueDirection))
			{
				isUpdatedDelta = false;
				_isTrueDirection = false;
				return;
			}

			isUpdatedDelta = true;
			_isTrueDirection = trueDirection;
		}

		public override NoteResult CalcNoteResult(float musicTime)
		{
			return LiveConfig.CalculateFrictionFlickNoteResult(this, musicTime, true).Item1;
		}

		public override void ResetNote()
		{
			base.ResetNote();
			Array.Clear(inputResults, 0, inputResults.Length);
			_nextRushCorrectTime = 0f;
			_nextRushHadContact = false;
			HasCapturedPress = false;
		}
	}
}
