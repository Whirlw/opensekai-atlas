using OpenSekai.Shared.Diagnostics;

namespace Sekai.Live
{
	/// <summary>Owns the once-only head result and publication, independently of the body's lifecycle.</summary>
	internal sealed class LongNoteHead
	{
		private readonly LongNote _bar;
		private bool _headPublished;
		private (NoteResult result, NoteResultDescription description) _result;

		internal bool IsSettled => _result.result != NoteResult.None;
		internal NoteResult Result => _result.result;

		internal LongNoteHead(LongNote bar)
		{
			_bar = bar;
		}

		internal void Reset()
		{
			_headPublished = false;
			_result = (NoteResult.None, NoteResultDescription.None);
		}

		internal void Settle((NoteResult result, NoteResultDescription description) result, bool updateScore = true)
		{
			if (IsSettled || _headPublished || result.result == NoteResult.None) return;
			_result = result;
			_bar.SetHeadJudgeInfo(result);
			// Guard before invoking user callbacks, including reentrant state changes or presses.
			_headPublished = true;
			if (!_bar.HasJudgment) return;
			_bar.HeadDamage?.Invoke(_bar);
			_bar.HeadCombo?.Invoke(_bar);
			_bar.HeadJudgment?.Invoke(_bar);
			// Timeout misses historically omit score; a press keeps its score callback even if Miss.
			if (updateScore) _bar.HeadScore?.Invoke(_bar);
		}

		internal void PublishHeadMiss()
		{
			if (_bar.PublishesHeadMiss && !IsSettled)
			{
				Settle((NoteResult.Miss, NoteResultDescription.None), updateScore: false);
			}
		}

		internal void SettleExpiredHead(float currentTime)
		{
			if (!IsSettled && currentTime - _bar.MusicScoreInfo.time > LiveConfig.JudgeTime)
			{
				PublishHeadMiss();
			}
		}

		internal bool Judgment(ref LiveTouch touch, float lane)
		{
			if (_bar.State == NoteState.Done)
			{
				return false;
			}

			// Only an ordinary head has an owner: it is the press that a second finger could
			// steal, and it is the only part of a bar the chart asks a single note to answer.
			// A rubbed bar and a hidden head are taken by whatever finger is over the lane.
			bool headSettledAlready = IsSettled;
			if (!headSettledAlready && _bar.PublishesHeadMiss && _bar.IsHeldByAnotherFinger(touch.fingerId, touch.musicTime))
			{
				return false;
			}

			// The head is a tap note that happens to have a body behind it: it is judged on its own
			// window, whatever the body is doing, and its result never decides how the body behaves.
			if (!IsSettled)
			{
				var judgeInfo = LiveConfig.CalculateLongStartNoteResult(_bar, touch.musicTime);
				Settle(_bar.HeadResultUpdater != null ? _bar.HeadResultUpdater(judgeInfo) : judgeInfo);
			}

			_bar.RecordHoldingInput(touch.fingerId, touch.musicTime);
			_bar.RestoreHeldState();
			_bar.MarkHeadTouched();
			_bar.MarkPressedThisFrame();
			if (LiveTrace.IsOn(LiveTraceCategory.Hold))
			{
				LiveTrace.Write(LiveTraceCategory.Hold, $"grab barT={_bar.MusicScoreInfo.time:F3} lane={lane:F2} range={_bar.JudgeLaneStart:F2}..{_bar.JudgeLaneEnd:F2} finger={touch.fingerId} phase={touch.phase}");
			}

			_bar.ClearHeadJudgeOffset();
			ConnectionNoteJudgment(ref touch, lane);

			if (_bar.HeadChildNote != null
				&& LiveConfig.IsLongEndJudgeTime(_bar.HeadChildNote.JudgeOffsetAt(touch.musicTime))
				&& _bar.HeadChildNote.IsJudgment(ref touch, lane)
				&& _bar.HeadChildNote.Judgment(ref touch, lane))
			{
				if (LiveTrace.IsOn(LiveTraceCategory.Hold))
				{
					LiveTrace.Write(LiveTraceCategory.Hold, $"headPressChild barT={_bar.MusicScoreInfo.time:F3} touchT={touch.musicTime:F3} childT={_bar.HeadChildNote.MusicScoreInfo.time:F3}");
				}

				_bar.SetHeadJudgmentState(NoteState.Done);
				return true;
			}

			// Being held: the bar is kept alive by its own refresh pass, so the finger can still serve
			// a note that sits on top of it.
			return true;
		}

		internal void ConnectionNoteJudgment(ref LiveTouch touch, float lane)
		{
			if (!_bar.HasFinger && _bar.State != NoteState.Last && _bar.State != NoteState.Release)
			{
				return;
			}

			if (_bar.NoteList == null)
			{
				return;
			}

			// MusicScoreInfo is a value type: an uninitialized frame safely defaults to time zero.
			// A held touch can retain its original press stamp; only the frame clock gates points.
			float now = MusicScore.CurrentFrameInfo.time;
			for (var i = 1; i < _bar.NoteList.Count - 1; i++)
			{
				var note = _bar.NoteList[i];
				if (note != null
					&& now - note.MusicScoreInfo.time >= -OpenSekai.Shared.Live.LongEndJudgement.EarlyGoodMs / 1000f
					&& now - note.MusicScoreInfo.time <= OpenSekai.Shared.Live.LongEndJudgement.LateGoodMs / 1000f)
				{
					note.Judgment(ref touch, lane);
				}
			}
		}
	}
}
