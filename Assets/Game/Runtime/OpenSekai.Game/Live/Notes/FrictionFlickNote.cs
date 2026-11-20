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
		private readonly (int id, bool distance, bool trueDirection)[] _inputResults;

		private readonly float _flickDistance;

		/// <summary>The swipe length this note asks for, in the same units as a touch delta.</summary>
		internal float FlickDistance => _flickDistance;
		internal bool IsTraceTail { get; }
		private bool UsesTailInputWindow => IsTraceTail || IsTailFlickOwner || IsTailTraceFlickOwner || GradesAsBarPart && ParentRole == NoteParentRole.Tail;
		private const float GRADE_LATE_WINDOW = 7.5f / 60f;
		private bool _everCorrect;
		private float NextRushEarlyWindow => (UsesTailInputWindow ? 7.5f : 6.5f) / 60f;
		private float NextRushLateWindow => (UsesTailInputWindow ? 8.5f : 7.5f) / 60f;
		private float _nextRushCorrectTime;
		private float _missPendingAt = float.PositiveInfinity;
		private bool _nextRushHadContact;
		public bool IsTraceFlickOwner { get; }
		public bool IsTailTraceFlickOwner { get; }
		public bool IsTailFlickOwner => IsTraceFlickOwner && Category == NoteCategory.Flick;
		internal bool CanStartNextRushHold => HasCapturedPress || State == NoteState.Done;
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
			_inputResults = new (int id, bool distance, bool trueDirection)[8];
			Category = NoteCategory.FrictionFlick;
		}

		public FrictionFlickNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, float speedRatio,
			NoteType type, NoteDirection direction, float screenDpiToInch, float laneOffset,
			bool isTraceFlickOwner = false, bool isTailTraceFlickOwner = false, bool isTraceTail = false)
			: base(info, id, laneStart, laneEnd, category, speedRatio, laneOffset, type)
		{
			_inputResults = new (int id, bool distance, bool trueDirection)[8];
			_flickDistance = screenDpiToInch;
			Direction = direction;
			IsTraceFlickOwner = isTraceFlickOwner;
			IsTraceTail = isTraceTail;
			IsTailTraceFlickOwner = isTailTraceFlickOwner;
		}

		public override void Excute(MusicScoreInfo currentFrameInfo, float offsetTime)
		{
			AdvanceNote(currentFrameInfo, offsetTime, settleTimeout: false);
			if (State == NoteState.Done) return;
			float now = currentFrameInfo.time;
			if (now > _missPendingAt)
				FinishNextRushHead((NoteResult.Miss, NoteResultDescription.None));
			else if (now > MusicScoreInfo.time + 8.5f / 60f)
				_missPendingAt = now;
			else if (!IsTailFlickOwner && now >= MusicScoreInfo.time && (_everCorrect || !_nextRushHadContact))
			{
				bool moved = false, correct = false;
				foreach (var input in _inputResults)
				{
					moved |= input.distance;
					correct |= input.trueDirection;
				}
				if (moved) FinishTraceFlick(now, correct);
			}
		}

		internal bool ReadNextRushHeadSample(ref LiveTouch touch, float lane, float sampleTime)
		{
			if (!float.IsPositiveInfinity(_missPendingAt) || touch.touchId == 0 || State is not (NoteState.Scrolling or NoteState.Input) || touch.phase == InputTouchPhase.Canceled)
				return false;
			bool unregistered = IsLaneCheck(touch);
			if (unregistered && (sampleTime <= MusicScoreInfo.time - NextRushEarlyWindow
				|| sampleTime >= MusicScoreInfo.time + NextRushLateWindow || float.IsNegativeInfinity(lane)
				|| lane < JudgeLaneStart || lane > JudgeLaneEnd || _inputResults[^1].id != 0)) return false;
			RecordSlideEndInput(NextRushLateWindow);
			State = NoteState.Input;
			bool moved = false, correct = false;
			UpdateDelta(touch.delta, ref moved, ref correct);
			if (unregistered || moved) AddInputResult(ref touch, ref moved, ref correct);
			if (correct)
			{
				_everCorrect = true;
				_nextRushCorrectTime = sampleTime;
				if (!IsTailFlickOwner && sampleTime >= MusicScoreInfo.time + LiveConfig.JustPerfectJudgeTimeHalf)
					FinishTraceFlick(sampleTime, true);
			}
			return true;
		}

		internal void SettleNextRushHead(float now, bool hasContact)
		{
			float target = MusicScoreInfo.time;
			bool contactRecently = _nextRushHadContact || hasContact;
			_nextRushHadContact = hasContact;
			if (State == NoteState.Done || now < target) return;
			bool hasMotion = false, correct = false;
			foreach (var input in _inputResults)
			{
				if (!input.distance) continue;
				hasMotion = true;
				correct |= input.trueDirection;
			}

			if (IsTailFlickOwner && hasMotion && (!contactRecently ||
				correct && _nextRushCorrectTime >= target + LiveConfig.JustPerfectJudgeTimeHalf))
			{
				float time = contactRecently ? _nextRushCorrectTime : now;
				HasNextRushHit = IsNextRush;
				FinishNextRushHead(LiveConfig.CalculateLongEndFlickNoteResult(this, time, correct));
			}
			else if (!IsTailFlickOwner && hasMotion && (!contactRecently || Category == NoteCategory.Flick && correct))
			{
				FinishTraceFlick(now, correct);
			}
		}

		private void FinishTraceFlick(float time, bool correct)
		{
			HasNextRushHit = IsNextRush;
			var result = CalcNoteResult(time);
			if (!correct && result == NoteResult.Perfect)
				FinishNextRushHead((NoteResult.Great, NoteResultDescription.FlickMiss));
			else
				FinishNextRushHead((result, NoteResultDescription.None));
		}

		private void FinishNextRushHead((NoteResult, NoteResultDescription) result)
		{
			JudgeInfo = onUpdateResult != null ? onUpdateResult(result) : result;
			State = NoteState.Done;
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
			return State is NoteState.Scrolling or NoteState.Input && (!IsLaneCheck(touch)
				|| lane >= JudgeLaneStart && lane <= JudgeLaneEnd && touch.musicTime > MusicScoreInfo.time - NextRushEarlyWindow
				&& touch.musicTime < MusicScoreInfo.time + NextRushLateWindow);
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
			// A fresh press can unlock a release tail. The swipe itself uses the contact channel.
			if (IsTraceFlickOwner)
			{
				if (!IsJudgment(ref touch, lane)) return false;
				HasCapturedPress = true;
				return true;
			}
			ReadNextRushHeadSample(ref touch, lane, touch.musicTime);
			return State == NoteState.Done;
		}

		private void AddInputResult(ref LiveTouch touch, ref bool isUpdatedDelta, ref bool _isTrueDirection)
		{
			for (var i = 0; i < _inputResults.Length; i++)
			{
				if (_inputResults[i].id == 0 || _inputResults[i].id == touch.touchId)
				{
					_inputResults[i] = (touch.touchId, isUpdatedDelta, _isTrueDirection);
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
			if (IsTailFlickOwner) return LiveConfig.CalculateLongEndFlickNoteResult(this, musicTime, true).Item1;
			return musicTime >= MusicScoreInfo.time - 6.5f / 60f && musicTime <= MusicScoreInfo.time + GRADE_LATE_WINDOW
				? NoteResult.Perfect : NoteResult.Miss;
		}

		public override void ResetNote()
		{
			base.ResetNote();
			Array.Clear(_inputResults, 0, _inputResults.Length);
			_missPendingAt = float.PositiveInfinity;
			_everCorrect = false;
			_nextRushCorrectTime = 0f;
			_nextRushHadContact = false;
			HasCapturedPress = false;
		}
	}
}
