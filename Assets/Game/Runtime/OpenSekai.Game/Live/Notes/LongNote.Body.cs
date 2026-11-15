using OpenSekai.Shared.Diagnostics;
using UnityEngine;

namespace Sekai.Live
{
	public partial class LongNote
	{
		private sealed class HoldBody
		{
			private readonly LongNote _bar;
			private bool _fingerOnIt;
			private bool _everTouched;
			private bool _pressedThisFrame;
			private float _lastFrameTime;
			private float _graceStartedAt;

			internal HoldBody(LongNote bar)
			{
				_bar = bar;
			}

			internal bool HasFinger => _fingerOnIt;
			internal bool WasTouched => _everTouched;
			internal bool HeadTouched { get; private set; }

			internal void BeginFrame(float currentTime) => _lastFrameTime = currentTime;
			internal void MarkHeadTouched() => HeadTouched = true;

			internal void Reset()
			{
				_fingerOnIt = false;
				_everTouched = false;
				_pressedThisFrame = false;
				_lastFrameTime = 0f;
				_graceStartedAt = 0f;
				HeadTouched = false;
			}

			internal void Update(float currentTime)
			{
				bool covered = _fingerOnIt;

				if (_bar.State == NoteState.Scrolling && currentTime - _bar.MusicScoreInfo.time > LiveConfig.JudgeTime)
				{
					if (!covered)
					{
						// Crossing the line decides the bar's first picture: a finger already on the lane
						// keeps it lit, and no finger at all means the bar was never taken, so it greys at
						// once. The grace is only for a bar that was being held and lost its finger.
						if (HeadTouched)
						{
							_bar.State = NoteState.Last;
							_graceStartedAt = currentTime;
						}
						else
						{
							_bar.State = NoteState.Release;
						}
					}
					else if (HoldHasBegun(currentTime))
					{
						// A finger already on the lane as the bar crosses: the bar is held from here on.
						_bar.SetStateUnnotice(NoteState.Input);
					}
				}
				else if ((_bar.State == NoteState.Input || _bar.State == NoteState.InputBegan)
					&& !covered && _everTouched && !_bar.IsDecoration)
				{
					// A held bar (ordinary or rub) is Input, so the crossing branch above, which only decides a
					// bar's first picture, never sees it lose its finger. This is the branch that starts its
					// grace.
					_bar.State = NoteState.Last;
					_graceStartedAt = currentTime;
				}
				else if (_bar.State == NoteState.Last)
				{
					if (covered)
					{
						_graceStartedAt = currentTime;
					}
					else if (currentTime - _graceStartedAt > HoldGraceSeconds)
					{
						_bar.State = NoteState.Release;
						_graceStartedAt = currentTime;
					}
				}

				// Decoration notes: skip input frame check to prevent being marked as Release.
				// The frame that grabbed this bar never runs the freshness check: the press carries the
				// touch's start time, so a finger that went down before the head arrived would look stale
				// the instant it was recorded and the bar would grey out immediately.
				if (_fingerOnIt && _bar.State != NoteState.Last && _bar.State != NoteState.Release)
				{
					if (_pressedThisFrame)
					{
						_bar.lastInputTime = currentTime;
						_pressedThisFrame = false;
					}
					else if (!_bar.IsDecoration)
					{
						// The finger is gone: grey the bar now and let the countdown below decide when it
						// is finished. The grace uses 50 ms of elapsed time.
						_bar.State = NoteState.Last;
						_graceStartedAt = currentTime;
					}
				}
			}

			internal void KeepAlive(float musicTime)
			{
				// A bar that is still alive must never be refreshed with a time from the past: that is how
				// a recycled note looked held long after the run that held it.
				if (musicTime + 0.001f < _bar.lastInputTime && LiveTrace.Enabled)
					LiveTrace.Invariant(false, $"keepAlive went backwards barT={_bar.MusicScoreInfo.time:F3} last={_bar.lastInputTime:F3} now={musicTime:F3}");
				_bar.lastInputTime = Mathf.Max(_bar.lastInputTime, musicTime);

				// A finger on the bar means it is held, even when the head was missed: the chart lets a
				// finger that arrives late (or was already resting there) pick the bar up.
				RestoreHeldState();
			}

			internal void RecordInput(float musicTime)
			{
				_bar.lastInputTime = Mathf.Max(_bar.lastInputTime, musicTime);
				_fingerOnIt = true;
				_everTouched = true;
			}

			/// <summary>Rebuilds the per-frame fact; the coordinator clears it for every bar each frame.</summary>
			internal void ClearFinger()
			{
				_fingerOnIt = false;
			}

			/// <summary>
			/// The press that lands on this bar already happened on this frame, so the freshness check
			/// must not run against it. <c>touch.musicTime</c> is when the finger went down, which can be
			/// hundreds of milliseconds before the bar's head arrived; judged against the current time it
			/// looks stale immediately and the bar releases on the very frame it was grabbed.
			/// </summary>
			internal void MarkPressed()
			{
				_pressedThisFrame = true;

				// The press time carried by the touch is when the finger went down, which can be long
				// before the bar's head. The freshest clock this note has seen is its own last frame, so
				// stamp that instead; it is at most one frame old.
				_bar.lastInputTime = Mathf.Max(_bar.lastInputTime, _lastFrameTime);
			}

			/// <summary>
			/// Brings a bar back to its held state when a finger lands on it again.
			///
			/// Inside the grace (Last) a finger recorded on the bar this frame takes it back: a hand-off
			/// between two fingers that leaves one frame uncovered must not keep a held bar in Last for the
			/// rest of its life (device trace 2026-11-14, bar 34.105: finger 1 lifted at 35.507, finger 0
			/// took over at 35.519, and the bar stayed Last until its end). The finger has to be recorded,
			/// not only kept alive: a restore without one is what made the crossing branch fire again every
			/// frame (Scrolling to Last), so the grace never finished and the tail was left unjudged.
			///
			/// After the grace (Release) an ordinary bar only comes back inside its head's window; past that,
			/// Release is final and what the bar looks like follows ShowsAsReleased.
			/// </summary>
			internal void RestoreHeldState()
			{
				float now = MusicScore.CurrentFrameInfo.time;

				// Scrolling means the bar has not reached the line. A held bar is Input, as a rub bar always
				// was: keeping an ordinary held bar in Scrolling sent every uncovered frame of a zigzag bar
				// through the first-crossing branch of Update (Scrolling to Last), and each round trip
				// restarted the hold loop SE on top of the last one (device trace 2026-11-15, bar 65.407:
				// 18 trips to Last, 16 se_live_long starts in 2.6 s).
				if (_bar.State == NoteState.Scrolling)
				{
					if (_fingerOnIt && HoldHasBegun(now))
					{
						_bar.SetStateUnnotice(NoteState.Input);
					}

					return;
				}

				if (_bar.State != NoteState.Release && _bar.State != NoteState.Last)
				{
					return;
				}

				if (_bar is FrictionLongNote)
				{
					_bar.SetStateUnnotice(NoteState.Input);
					return;
				}

				if ((_bar.State == NoteState.Last && _fingerOnIt)
					|| now - _bar.MusicScoreInfo.time <= LiveConfig.JudgeTime)
				{
					_bar.SetStateUnnotice(_fingerOnIt && HoldHasBegun(now) ? NoteState.Input : NoteState.Scrolling);
				}
			}

			/// <summary>
			/// The hold has begun once the head is answered or has reached the line. A finger resting in the
			/// lane before an unanswered head arrives is not holding anything yet, so the bar stays Scrolling.
			/// </summary>
			private bool HoldHasBegun(float now) => _bar.Head.IsSettled || now >= _bar.MusicScoreInfo.time;
		}
	}
}
