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

		private LongNoteHeadNote _candidate;

		/// <summary>
		/// The head as the judgement list sees it: its own candidate object, so a press reaches the
		/// head's entry and not the bar's. The visual side keeps using the bar.
		/// </summary>
		internal LongNoteHeadNote Candidate => _candidate ??= new LongNoteHeadNote(_bar, this);

		/// <summary>
		/// The note the head's verdict is scored as. The bar carries the same number for the picture -
		/// the judgement effect is played on the bar, and the views read the bar - but the head's own
		/// result lives here, on an object of its own, so score, combo and damage stop being answers
		/// about the bar's Result. One object per bar, rearmed by <see cref="Reset"/>.
		/// </summary>
		private NoteBase _identity;

		internal NoteBase Identity => _identity ??= new NoteBase(
			_bar.MusicScoreInfo, _bar.Id, _bar.LaneStart, _bar.LaneEnd, _bar.Category, _bar.speedRatio, _bar.LaneOffset, _bar.Type);

		internal LongNoteHead(LongNote bar)
		{
			_bar = bar;
		}

		internal void Reset()
		{
			_headPublished = false;
			_result = (NoteResult.None, NoteResultDescription.None);
			if (_identity != null)
			{
				_identity.ResetNote();
				_identity.SetDecoration(_bar.IsDecoration);
			}
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
			// The bar keeps the same number for the picture; the verdict itself is the head's.
			_bar.SetHeadJudgeInfo(result);
			// NoteBase's JudgeInfo setter is protected, so this uses its one public write. It is the same
			// assignment: the head's own object carries the head's own verdict.
			Identity.SetJudgeInfoForDecoration(result.result);
			// Guard before invoking user callbacks, including reentrant state changes or presses.
			_headPublished = true;
			if (!_bar.HasJudgment) return;
			_bar.HeadDamage?.Invoke(Identity);
			_bar.HeadCombo?.Invoke(Identity);
			// Only an ordinary head borrows the bar as its visual object: its judgement effect is played
			// on the bar the view layer knows. Every other head is expected to be visually unrelated to
			// the bar, so it speaks for itself.
			_bar.HeadJudgment?.Invoke(_bar.IsOrdinaryHead ? (NoteBase)_bar : Identity);
			// Timeout misses historically omit score; a press keeps its score callback even if Miss.
			if (updateScore) _bar.HeadScore?.Invoke(Identity);
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

		/// <summary>
		/// The head's own judgement entry: one touch, one lane, this frame. It is the door the candidate
		/// list will call once the head is its own candidate; today the bar's <c>Judgment</c> only
		/// forwards here, so the head is already the place the press is answered.
		/// </summary>
		internal bool Judgment(ref LiveTouch touch, float lane) => _bar.DriveJudgmentFrame(ref touch, lane);

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
