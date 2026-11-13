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
			if (IsSettled || _headPublished || result.result == NoteResult.None)
			{
				if (LiveTrace.IsOn(LiveTraceCategory.Judge))
				{
					LiveTrace.Write(LiveTraceCategory.Judge, string.Concat(
						"headSkip id=", _bar.Id, " settled=", IsSettled, " published=", _headPublished,
						" want=", result.result, " t=", _bar.MusicScoreInfo.time.ToString("F3")));
				}

				return;
			}
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
			// The head answers to its own window and to nothing else. The bar leaving the play state -
			// a release, a tail that ends it early, the bar being recycled - is not a verdict on the
			// head: one door out of the play state used to settle this head, which let the bar's
			// lifecycle decide a result the head owns.
			if (!IsSettled && currentTime - _bar.MusicScoreInfo.time > LiveConfig.JudgeTime)
			{
				Settle((NoteResult.Miss, NoteResultDescription.None), updateScore: false);
			}
		}

		internal bool TryJudge(ref LiveTouch touch, float lane)
		{
			// Kept as a second lock, not as the answer to "may a finished bar be pressed". The input
			// path already refuses a Done bar in LongNote.IsJudgment, and the bar publishes its head
			// on the way out (LongNote.LeavePlayState), so by the time this line can be reached the
			// head is settled anyway and this only saves the frame's bookkeeping. tail-classes.md
			// §12.4-4 asked whether it is redundant: it is, and it stays because removing it would let
			// a direct caller drive a finished bar's child note.
			if (_bar.State == NoteState.Done)
			{
				return false;
			}

			// A bar has no owner any more, so a second finger landing on it cannot steal the head:
			// the head is settled once, by whichever touch reaches the window first.
			bool headSettledAlready = IsSettled;

			// The head is a tap note that happens to have a body behind it: it is judged on its own
			// window, whatever the body is doing, and its result never decides how the body behaves.
			if (!IsSettled)
			{
				var judgeInfo = LiveConfig.CalculateLongStartNoteResult(_bar, touch.musicTime);
				Settle(_bar.HeadResultUpdater != null ? _bar.HeadResultUpdater(judgeInfo) : judgeInfo);
			}

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
