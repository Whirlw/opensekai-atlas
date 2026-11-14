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
				}
				else if ((_bar.State == NoteState.Input || _bar.State == NoteState.InputBegan)
					&& !covered && _everTouched && !_bar.IsDecoration)
				{
					// A rub bar is held in Input, not Scrolling, so the crossing branch above never sees it
					// lose its finger. Its lift used to be caught only because the rub path never marked its
					// frames and the freshness check below greyed it on every update; with the frames marked,
					// this is the branch that starts its grace.
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
			/// Brings a bar back to its held picture when a finger lands on it again. A bar that has already
			/// crossed its own line is left alone: Scrolling means "never taken yet", and sending a bar that
			/// has moved on back there made the crossing branch fire again every frame (Scrolling to Last),
			/// so the grace that leads to Release never finished and the tail was left unjudged until its
			/// window shut. What a bar looks like after the line follows ShowsAsReleased, not this.
			/// </summary>
			internal void RestoreHeldState()
			{
				if (_bar.State != NoteState.Release && _bar.State != NoteState.Last)
				{
					return;
				}

				if (_bar is FrictionLongNote)
				{
					_bar.SetStateUnnotice(NoteState.Input);
					return;
				}

				if (MusicScore.CurrentFrameInfo.time - _bar.MusicScoreInfo.time <= LiveConfig.JudgeTime)
				{
					_bar.SetStateUnnotice(NoteState.Scrolling);
				}
			}
		}
	}
}
