using OpenSekai.Shared.Live;
using UnityEngine;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Sekai.Live
{
	/// <summary>
	/// How the flick that ends a playable bar grades itself. The bar no longer judges this end: the note
	/// is a self-judged tail (like a rub flick end), and this class is its rule.
	///
	/// <list type="bullet">
	/// <item>Inside the flick's own lanes, a swipe faster than 5 cm/s up to 125 ms before the tail time is
	/// only recorded. That finger is graded 50 ms after it lifts or slides out of the flick's lanes, at that
	/// moment's offset; if it is still in the lanes when the tail time comes, it is graded right then.</item>
	/// <item>Each new qualifying swipe of a finger overrides the direction recorded for it.</item>
	/// <item>When several fingers come due on the same frame, the best grade is kept.</item>
	/// <item>From the tail time on, a qualifying swipe grades at once (unchanged from the bar's rule).</item>
	/// </list>
	/// </summary>
	internal sealed class FlickTailJudge
	{
		/// <summary>How early a swipe may be recorded: the early edge of the end window, 125 ms.</summary>
		internal const float EarlySeconds = LongEndJudgement.EarlyGoodMs / 1000f;

		/// <summary>The wait between leaving and grading: three reference frames, 50 ms.</summary>
		internal static float ReleaseDelaySeconds => LongNote.HoldGraceSeconds;

		private struct Finger
		{
			public int TouchId;
			public bool Seen;
			public Vector2 Accum;
			public float WindowStart;
			public bool Armed;
			public bool TrueDirection;
			public float LeftAt;
		}

		private readonly FlickNote _note;
		private readonly Finger[] _fingers = new Finger[10];
		private float _lastFrameTime = -1f;

		/// <summary>
		/// The music time since the previous input frame this judge saw, or one reference frame on its
		/// first. A swipe's speed is measured on the music clock, not on the engine's frame time.
		/// </summary>
		internal float FrameDeltaTime(float now) =>
			_lastFrameTime >= 0f && now > _lastFrameTime
				? Mathf.Min(now - _lastFrameTime, 0.1f)
				: FlickGesture.ReferenceFrameSeconds;

		internal FlickTailJudge(FlickNote note)
		{
			_note = note;
			Reset();
		}

		internal void Reset()
		{
			ClearFingers();
			_lastFrameTime = -1f;
		}

		private void ClearFingers()
		{
			for (int i = 0; i < _fingers.Length; i++)
			{
				Free(i);
			}
		}

		/// <summary>Whether a finger recorded an early swipe that is still waiting to be graded.</summary>
		internal bool HasArmedFinger
		{
			get
			{
				for (int i = 0; i < _fingers.Length; i++)
				{
					if (_fingers[i].TouchId != 0 && _fingers[i].Armed)
					{
						return true;
					}
				}

				return false;
			}
		}

		/// <summary>
		/// One sample of one finger. <paramref name="previousLane"/> is where the movement started: the final
		/// sample of a flick can carry the whole gesture and land outside the playfield.
		/// </summary>
		internal void Offer(ref LiveTouch touch, float lane, float previousLane, float deltaTime)
		{
			if (_note.State == NoteState.Done || touch.touchId == 0)
			{
				return;
			}

			float now = touch.musicTime;
			float offset = now - _note.MusicScoreInfo.time;
			int slot = Find(touch.touchId);
			if (slot >= 0)
			{
				_fingers[slot].Seen = true;
			}

			// Before the early edge nothing counts, and past the late edge the note's own timeout decides.
			if (offset < -EarlySeconds || LongEndJudgement.IsPast(offset))
			{
				return;
			}

			bool ended = touch.phase == InputTouchPhase.Ended || touch.phase == InputTouchPhase.Canceled;
			float moveLane = previousLane >= 0f || ended ? previousLane : lane;
			if (touch.delta.sqrMagnitude > 0f && InLanes(moveLane))
			{
				if (slot < 0)
				{
					slot = Allocate(touch.touchId);
				}

				if (slot >= 0 && TrySwipe(ref _fingers[slot], touch.delta, deltaTime, now, out bool trueDirection))
				{
					if (offset < 0f)
					{
						// Early: remember it. A newer swipe of the same finger overrides the direction.
						_fingers[slot].Armed = true;
						_fingers[slot].TrueDirection = trueDirection;
					}
					else
					{
						// From the tail time on, a swipe is graded at once - together with anything that came
						// due on this same moment.
						Settle(now, now, trueDirection);
						return;
					}
				}
			}

			if (slot < 0)
			{
				return;
			}

			ref Finger finger = ref _fingers[slot];
			if (finger.Armed && finger.LeftAt < 0f && (ended || !InLanes(lane)))
			{
				finger.LeftAt = now;
			}
			else if (!finger.Armed && ended)
			{
				Free(slot);
			}
		}

		/// <summary>
		/// Closes the frame's input: an armed finger that sent no sample this frame has left the screen, and
		/// is treated as lifted now. Then grades whatever came due.
		/// </summary>
		internal void EndInputFrame(float now)
		{
			_lastFrameTime = now;
			for (int i = 0; i < _fingers.Length; i++)
			{
				ref Finger finger = ref _fingers[i];
				if (finger.TouchId == 0)
				{
					continue;
				}

				if (!finger.Seen)
				{
					if (finger.Armed)
					{
						if (finger.LeftAt < 0f)
						{
							finger.LeftAt = now;
						}
					}
					else
					{
						Free(i);
						continue;
					}
				}

				finger.Seen = false;
			}

			Settle(now);
		}

		/// <summary>Grades every armed finger whose moment has come; the best grade wins.</summary>
		internal bool Settle(float now) => Settle(now, -1f, false);

		private bool Settle(float now, float swipeTime, bool swipeTrueDirection)
		{
			if (_note.State == NoteState.Done)
			{
				return false;
			}

			float tailTime = _note.MusicScoreInfo.time;
			bool found = false;
			(NoteResult result, NoteResultDescription description) best = default;

			if (swipeTime >= 0f)
			{
				best = _note.GradeAsTail(swipeTime, swipeTrueDirection);
				found = best.result != NoteResult.None;
			}

			for (int i = 0; i < _fingers.Length; i++)
			{
				Finger finger = _fingers[i];
				if (finger.TouchId == 0 || !finger.Armed)
				{
					continue;
				}

				// Lifted or slid out: 50 ms after that. Still in the lanes: the tail time itself.
				float due = finger.LeftAt >= 0f ? finger.LeftAt + ReleaseDelaySeconds : tailTime;
				if (due > now)
				{
					continue;
				}

				(NoteResult result, NoteResultDescription description) grade = _note.GradeAsTail(due, finger.TrueDirection);
				if (grade.result != NoteResult.None && (!found || grade.result > best.result))
				{
					best = grade;
					found = true;
				}
			}

			if (!found)
			{
				return false;
			}

			_note.FinishAsTail(best);
			ClearFingers();
			return true;
		}

		/// <summary>
		/// A flick is judged on one 60 fps frame of travel, not on a single high rate sample: a resting finger
		/// jitters by a few tenths of a millimetre per sample, which reads as several cm/s. The net travel over
		/// 16.7 ms keeps a real flick and drops the jitter. The speed bar is the note's flick distance per
		/// reference frame, which is 5 cm/s.
		/// </summary>
		private bool TrySwipe(ref Finger finger, Vector2 delta, float deltaTime, float now, out bool trueDirection)
		{
			trueDirection = false;
			float flickDistance = _note.FlickDistance;
			if (finger.WindowStart < 0f)
			{
				finger.WindowStart = now - Mathf.Max(deltaTime, 0f);
				finger.Accum = Vector2.zero;
			}

			finger.Accum += delta;
			float frameSeconds = FlickGesture.ReferenceFrameSeconds;
			float elapsed = now - finger.WindowStart;

			// Wait for one frame of travel, unless a whole flick distance is already covered: that much in a
			// few milliseconds cannot be jitter, and waiting would only add latency.
			if (elapsed + 1e-4f < frameSeconds && finger.Accum.magnitude < flickDistance)
			{
				return false;
			}

			Vector2 net = finger.Accum;
			finger.Accum = Vector2.zero;
			finger.WindowStart = now;

			if (!FlickGesture.TryUpdate(net, frameSeconds, _note.Direction, flickDistance, out trueDirection))
			{
				return false;
			}

			float speed = net.magnitude / Mathf.Max(elapsed, 1e-4f);
			return speed >= flickDistance / frameSeconds;
		}

		private bool InLanes(float lane) =>
			lane >= 0f && lane >= _note.JudgeLaneStart && lane <= _note.JudgeLaneEnd;

		private int Find(int touchId)
		{
			for (int i = 0; i < _fingers.Length; i++)
			{
				if (_fingers[i].TouchId == touchId)
				{
					return i;
				}
			}

			return -1;
		}

		private int Allocate(int touchId)
		{
			for (int i = 0; i < _fingers.Length; i++)
			{
				if (_fingers[i].TouchId == 0)
				{
					_fingers[i].TouchId = touchId;
					_fingers[i].Seen = true;
					return i;
				}
			}

			return -1;
		}

		private void Free(int i)
		{
			_fingers[i] = new Finger { TouchId = 0, WindowStart = -1f, LeftAt = -1f };
		}
	}
}
