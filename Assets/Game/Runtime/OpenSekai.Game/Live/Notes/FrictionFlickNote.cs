using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Sekai;

using OpenSekai.Shared.Live;

namespace Sekai.Live
{
	public class FrictionFlickNote : NoteBase
	{
		private float _lastInputTime;

		private readonly (int id, bool distance, bool trueDirection)[] inputResults;

		private readonly float _flickDistance;

		/// <summary>The swipe length this note asks for, in the same units as a touch delta.</summary>
		internal float FlickDistance => _flickDistance;

		private bool _isTrueDirection;

		public override NoteState State
		{
			get => state;
			protected set
			{
				if (value == NoteState.Done && state != NoteState.Done)
				{
					state = NoteState.Done;
					NotifyParentFinished();
					OnUnSpawnNote();
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

		public FrictionFlickNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, float speedRatio, NoteType type, NoteDirection direction, float screenDpiToInch, float laneOffset)
			: base(info, id, laneStart, laneEnd, category, speedRatio, laneOffset, type)
		{
			inputResults = new (int id, bool distance, bool trueDirection)[8];
			_flickDistance = screenDpiToInch;
			Direction = direction;
		}

		public override void Excute(MusicScoreInfo currentFrameInfo, float offsetTime)
		{
			base.Excute(currentFrameInfo, offsetTime);
			if ((_isTrueDirection || (State == NoteState.Input && _lastInputTime + ReferenceFrame.Seconds < currentFrameInfo.time))
				&& CalcProgress(currentFrameInfo, offsetTime) >= 1f)
			{
				JudgmentFlick(currentFrameInfo.time);
			}
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

			_lastInputTime = touch.musicTime;

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
				touchDelta, Time.deltaTime, Direction, _flickDistance, out bool trueDirection))
			{
				isUpdatedDelta = false;
				_isTrueDirection = false;
				return;
			}

			isUpdatedDelta = true;
			_isTrueDirection = trueDirection;
			if (trueDirection)
			{
				this._isTrueDirection = true;
			}
		}

		public override NoteResult CalcNoteResult(float musicTime)
		{
			return LiveConfig.CalculateFrictionFlickNoteResult(this, musicTime, true).Item1;
		}

		public override void ResetNote()
		{
			base.ResetNote();
			Array.Clear(inputResults, 0, inputResults.Length);
			_lastInputTime = 0f;
			_isTrueDirection = false;
		}
	}
}
