using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Sekai;
using OpenSekai.Shared.Diagnostics;
using OpenSekai.Shared.Live;
using Sekai.Live.Tails;

namespace Sekai.Live
{
	public class LongNote : NoteBase
	{
		// Bar geometry includes invisible endpoints; ViewNoteList contains only drawable keys.
		internal List<INote> LineNoteList { get; private set; } = new List<INote>();

		protected float lastInputTime;
		private bool _fingerOnIt;

		private bool _everTouched;

		protected string _guideColor = null;

		public string guideColor
		{
			get => _guideColor ?? MusicScoreInfo.guideColor;
		}

		public void SetGuideColor(string color)
		{
			_guideColor = color;
		}

		public float LaneOffset
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		public bool WasHoldingWhenTerminated
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		public float JudgedLaneEnd
		{
			get
			{
				return LaneEndF + LaneOffset;
			}
		}

		public float JudgedLaneStart
		{
			get
			{
				return LaneStartF - LaneOffset;
			}
		}

		public override int LaneStart
		{
			get
			{
				return (int)Math.Round(LaneStartF, MidpointRounding.ToEven);
			}
			set
			{
				base.LaneStart = value;
				LaneStartF = value;
			}
		}

		public override int LaneEnd
		{
			get
			{
				return (int)Math.Round(LaneEndF, MidpointRounding.ToEven);
			}
			set
			{
				base.LaneEnd = value;
				LaneEndF = value;
			}
		}

		public override NoteState State
		{
			get
			{
				return state;
			}
			protected set
			{
				if (value == NoteState.Done && state == NoteState.Done) return;

				if (value == NoteState.Done)
				{
					WasHoldingWhenTerminated = HasFinger;
					state = value;
					OnUnSpawnNote();
					return;
				}

				state = value;
				if (value == NoteState.Scrolling)
				{
					OnSpawnNote();
				}
			}
		}

		public override Action<NoteBase> OnUpdateScore
		{
			set
			{
				onUpdateScore = value;
				ApplyToChildNotes(note => note.OnUpdateScore = value);
			}
		}

		public override Action<NoteBase> OnUpdateCombo
		{
			set
			{
				onUpdateCombo = value;
				ApplyToChildNotes(note => note.OnUpdateCombo = value);
			}
		}

		public override Action<NoteBase> OnDamage
		{
			set
			{
				onDamage = value;
				ApplyToChildNotes(note => note.OnDamage = value);
			}
		}

		public override Action<NoteBase> OnSpawn
		{
			set
			{
				onSpawn = value;
				ApplyToChildNotes(note => note.OnSpawn = value);
			}
		}

		public override Action<NoteBase> OnJudgment
		{
			set
			{
				onJudgment = value;
				ApplyToChildNotes(note => note.OnJudgment = value);
			}
		}

		public override Action<NoteBase> OnUnspawn
		{
			set
			{
				onUnspawn = value;
				ApplyToChildNotes(note => note.OnUnspawn = value);
			}
		}

		public override Func<(NoteResult result, NoteResultDescription description), (NoteResult result, NoteResultDescription description)> OnUpdateResult
		{
			set
			{
				onUpdateResult = value;
				ApplyToChildNotes(note => note.OnUpdateResult = value);
			}
		}

		public LongNote()
		{
			NoteList = new List<NoteBase> { this };
			ViewNoteList = new List<INote> { this };
			LineNoteList.Add(this);
			Category = NoteCategory.Long;
			Type = NoteType.Default;
		}

		public LongNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, NoteType type, float speedRatio, float laneOffset, NoteLineType lineType = NoteLineType.Linear)
			: base(info, id, laneStart, laneEnd, category, speedRatio, laneOffset, type)
		{
			LineType = lineType;
			LaneOffset = laneOffset;
			LineNoteList.Add(this);
		}

		public void AddConnectionNote(NoteBase connectionNote)
	{
		if (connectionNote == null)
		{
			return;
		}

		NoteList ??= new List<NoteBase> { this };
		ViewNoteList ??= new List<INote> { this };
		connectionNote.speedRatio = speedRatio;
		// 继承父note的装饰属性
		connectionNote.SetDecoration(IsDecoration);
		NoteList.Add(connectionNote);
		ViewNoteList.Add(connectionNote);
		LineNoteList.Add(connectionNote);
		connectionNote.SetParentNote(this);
	}

		public void AddHoldCombo(LongHoldCombo longHoldCombo)
	{
		if (longHoldCombo == null)
		{
			return;
		}

		NoteList ??= new List<NoteBase> { this };
		longHoldCombo.speedRatio = speedRatio;
		// 继承父note的装饰属性
		longHoldCombo.SetDecoration(IsDecoration);
		// The point sits on the bar's lane, so it has to judge against the bar's lane too: the
		// constructor left it at lane zero, which is why points on any other lane never matched.
		longHoldCombo.LaneStart = LaneStart;
		longHoldCombo.LaneEnd = LaneEnd;
		longHoldCombo.LaneStartF = LaneStartF;
		longHoldCombo.LaneEndF = LaneEndF;
		longHoldCombo.JudgeLaneStart = JudgeLaneStart;
		longHoldCombo.JudgeLaneEnd = JudgeLaneEnd;
		NoteList.Add(longHoldCombo);
		longHoldCombo.SetParentNote(this, NoteParentRole.Combo);
	}

		public void AddNoteListAndSetupChildNote(NoteBase note)
	{
		if (note == null)
		{
			return;
		}

		note.speedRatio = speedRatio;
		// 装饰属性属于长条自己：尾部不继承。装饰长条自己滚过去、不接受输入，但它的结束音
		// 仍然是一个真正的音符（划键、滑动、摩擦……），要按它本来的类型来玩。
		note.SetDecoration(false);
		// The converter hands us every note it walks: the head, combo ticks, hidden markers and the
		// real end note. Only the end note may become the child, otherwise a hidden marker that sits
		// after the tail would take its place and the bar would never learn how it ends.
		if (childNote == null || IsTailCandidate(note))
		{
			childNote = note;
		}

		_lastBoundNote = note;
		NoteList ??= new List<NoteBase> { this };
		ViewNoteList ??= new List<INote> { this };
		// The draw list holds every note the bar owns. Whether a note shows anything is the note's own
		// business; settlement is decided separately, in SettleLeftoverAsPerfect, where only the points
		// the bar itself ends are graded.
		note.SetParentNote(this, NoteParentRole.Tail);
		NoteList.Add(note);

		if (!note.IsSkip && !ViewNoteList.Contains(note))
		{
			ViewNoteList.Add(note);
		}

		LineNoteList.Add(note);
		SortNoteList();
	}
		public override void Excute(MusicScoreInfo currentFrameInfo, float offsetTime)
		{
			this.offsetTime = offsetTime;
			_lastFrameTime = currentFrameInfo.time;
			if (State == NoteState.Done)
			{
				return;
			}

			var prevState = State;
			var prevResult = Result;
			if (State == NoteState.Wait)
			{
				State = NoteState.Scrolling;
			}

			// Every bar leaves Last the same way: it greys out, because its head is gone. What
			// differs is only whether there is a head to have missed — a hidden head was never a
			// note the player could answer, so it stays silent instead of costing life and combo.
			if (State == NoteState.Last)
			{
				State = NoteState.Release;

				if (!Head.IsSettled) Head.PublishHeadMiss();
			}

			Progress = CalcProgress(currentFrameInfo, offsetTime);

			if (NoteList != null)
			{
				for (var i = 1; i < NoteList.Count; i++)
				{
					var note = NoteList[i];
					var childOffsetTime = CalcTimeOffset?.Invoke(note) ?? offsetTime;
					if (currentFrameInfo.time < note.MusicScoreInfo.time - childOffsetTime)
					{
						break;
					}

					note.Excute(currentFrameInfo, childOffsetTime);
				}
			}

			INote currentNote = this;
		INote nextNote = childNote;
		if (ViewNoteList != null)
		{
			for (var i = 1; i < ViewNoteList.Count - 1; i++)
			{
				var viewNote = ViewNoteList[i];
				if (viewNote == null || viewNote.IsSkip)
				{
					continue;
				}

				// 装饰note使用时间判断，普通note使用State判断
				// 装饰ConnectionNote永远不会被标记为Done，所以需要特殊处理
				bool isCompleted = viewNote.State == NoteState.Done;
			// A flick tail waits for its swipe after reaching the line; it must stop being drawn
			// on time all the same, or the key sits on the judgement line until the window closes.
			if (TailBehaviour.AcceptsSwipe && ReferenceEquals(viewNote, childNote) && GetTailKeyPhase(currentFrameInfo.time) is TailKeyPhase.SlidingOut or TailKeyPhase.Retired)
			{
				isCompleted = true;
			}
				if (viewNote is NoteBase noteBase && noteBase.IsDecoration)
				{
					// 装饰note：当时间已过判定时间时视为"已完成"
					isCompleted = currentFrameInfo.time >= viewNote.MusicScoreInfo.time;
				}

				if (!isCompleted)
				{
					nextNote = viewNote;
					break;
				}

				currentNote = viewNote;
			}
		}

			LiveUtility.CalcExcuteNoteLane(this, ref currentNote, ref nextNote, ref currentFrameInfo, MusicScoreInfo, childNote);

			// The head has its own judgement: it is settled once, when its window closes, and whether
			// the bar is lit or grey afterwards has nothing to do with it. A hidden head was never a
			// note the player could answer, so it settles silently.
			Head.SettleExpiredHead(currentFrameInfo.time);

			// The bar's appearance is driven only by whether a finger is on it. Lit while it is being
			// held, a three frame grace once nobody is on it any more, then grey. Touching it again at
			// any point brings it back to lit - the grace exists so that a bar does not flicker grey
			// between two frames of the same press.
			bool covered = HasFinger;

			if (State == NoteState.Scrolling && currentFrameInfo.time - MusicScoreInfo.time > LiveConfig.JudgeTime)
			{
				if (!covered)
				{
					// Crossing the line decides the bar's first picture: a finger already on the lane
					// keeps it lit, and no finger at all means the bar was never taken, so it greys at
					// once. The grace is only for a bar that was being held and lost its finger.
					if (_headTouched)
					{
						State = NoteState.Last;
				_graceStartedAt = currentFrameInfo.time;
					}
					else
					{
						State = NoteState.Release;
					}
				}
			}
			else if (State == NoteState.Last)
			{
				if (covered)
				{
				_graceStartedAt = currentFrameInfo.time;
				}
				else if (currentFrameInfo.time - _graceStartedAt > HoldGraceSeconds)
				{
					State = NoteState.Release;
				_graceStartedAt = currentFrameInfo.time;
				}
			}

			// Decoration notes: skip input frame check to prevent being marked as Release.
			// The frame that grabbed this bar never runs the freshness check: the press carries the
			// touch's start time, so a finger that went down before the head arrived would look stale
			// the instant it was recorded and the bar would grey out immediately.
			if (HasFinger && !ShowsAsReleased)
			{
				if (_pressedThisFrame)
				{
					lastInputTime = currentFrameInfo.time;
					_pressedThisFrame = false;
				}
				else if (!IsDecoration)
				{
					// The finger is gone: grey the bar now and let the countdown below decide when it
					// is finished. The grace is 50 ms of wall time, not three frames of whatever the
					State = NoteState.Last;
					_graceStartedAt = currentFrameInfo.time;
				}
			}

			if ((prevState != State || prevResult != Result) && LiveTrace.IsOn(LiveTraceCategory.Hold))
			{
				LiveTrace.Write(LiveTraceCategory.Hold, $"barExcute type={GetType().Name} cat={Category} barT={MusicScoreInfo.time:F3} now={currentFrameInfo.time:F3} prev={prevState}/{prevResult} next={State}/{Result} childT={(childNote == null ? 0f : childNote.MusicScoreInfo.time):F3} childState={(childNote == null ? NoteState.Wait : childNote.State)}");
			}
		}

		public virtual void ForceTerminate(NoteBase by = null)
		{

			if (State != NoteState.Done)
			{
				// Only the tail may end the bar. A connection note, a combo or a plain note that we
				// carry becomes Done on its own schedule; letting that finish the whole bar is how a
				// bar could be judged the moment it appeared, long before the head reached the line.
				if (by != null && by.ParentRole != NoteParentRole.Tail && !ReferenceEquals(by, childNote))
				{
					if (LiveTrace.IsOn(LiveTraceCategory.Hold))
					{
						LiveTrace.Write(LiveTraceCategory.Hold, $"forceTermRejected role={by.ParentRole} type={by.GetType().Name} barT={MusicScoreInfo.time:F3}");
					}

					return;
				}

				// The head has not even been reached yet: nothing on this bar may finish it. A swipe
				// that lands on the tail note while the bar is still playing must not award a Miss
				// for a bar the player never had a chance to hold.
				if (by != null && (State == NoteState.Wait || (State == NoteState.Scrolling && !HasFinger)))
				{
					if (LiveTrace.IsOn(LiveTraceCategory.Hold))
					{
						LiveTrace.Write(LiveTraceCategory.Hold, $"forceTermIgnored by={CallerName(1)} barT={MusicScoreInfo.time:F3} childT={(childNote == null ? 0f : childNote.MusicScoreInfo.time):F3} barState={State}");
					}

					return;
				}

				if (childNote != null && LiveTrace.IsOn(LiveTraceCategory.Hold))
				{
					LiveTrace.Write(LiveTraceCategory.Hold, $"forceTerm by={CallerName(1)} barT={MusicScoreInfo.time:F3} childT={childNote.MusicScoreInfo.time:F3} childRes={childNote.Result} childState={childNote.State} barState={State}");
				}

				JudgeInfo = childNote != null
					&& (childNote.Result > NoteResult.Pass || childNote.State == NoteState.Input)
						? (NoteResult.JustPerfect, NoteResultDescription.None)
						: (NoteResult.Miss, NoteResultDescription.None);
				State = NoteState.Done;
			}
		}

		internal TailKeyPhase GetTailKeyPhase(float currentTime)
		{
			// A bar whose end carries no judgement has no key phase at all: there is nothing to see
			// arrive, nothing to grade and nothing to retire.
			if (childNote == null || !childNote.HasJudgment) return TailKeyPhase.Hidden;
			float visibleFrom = childNote.MusicScoreInfo.time - (CalcTimeOffset?.Invoke(childNote) ?? offsetTime);
			return TailKeyLifetime.Of(childNote.MusicScoreInfo.time, visibleFrom, currentTime, childNote.Result, State == NoteState.Done);
		}

		protected override void OnUnSpawnNote()
		{
			ConnectionNoteTerminate();
			RemoveView();
		}


		/// <summary>
		/// True while a finger is on this bar, whatever phase that finger is in. The touch id is the
		/// truth; the InputBegan/Input states are its second spelling and are on their way out.
		/// </summary>
		public bool HasFinger => _fingerOnIt;
		/// <summary>
		/// True while a finger is on this bar and its hold has not gone stale. This is the fact the
		/// bar acts on: it says nothing about which input phase a finger is in, and it stops being
		/// true as soon as the last refresh is older than the hold tolerance — earlier than the
		/// state machine greys the bar out.
		/// </summary>
		public bool IsHeld(float currentTime)
		{
			return HasFinger;
		}

		/// <summary>
		/// True once a finger has ever taken this bar. The hold itself goes stale and the bar greys
		/// out, but the bar stays marked as one the player engaged with - that is what keeps it in
		/// the refresh pass until its tail resolves, rather than being dropped the moment it greys.
		/// </summary>
		public bool WasTouched => _everTouched;


		public override bool Judgment(ref LiveTouch touch, float lane)
		{
			return DriveJudgmentFrame(ref touch, lane);
		}

		/// <summary>Feeds this bar one input frame: head, hold facts, frame stamp, points and tail.</summary>
		internal bool DriveJudgmentFrame(ref LiveTouch touch, float lane)
		{
			if (!Head.TryJudge(ref touch, lane)) return false;
			RecordHoldingInput(touch.fingerId, MusicScore.CurrentFrameInfo.time);
			RestoreHeldState();
			MarkHeadTouched();
			MarkPressedThisFrame();
			if (LiveTrace.IsOn(LiveTraceCategory.Hold))
			{
				LiveTrace.Write(LiveTraceCategory.Hold, $"grab barT={MusicScoreInfo.time:F3} lane={lane:F2} range={JudgeLaneStart:F2}..{JudgeLaneEnd:F2} finger={touch.fingerId} phase={touch.phase}");
			}

			ClearHeadJudgeOffset();
			ConnectionNoteJudgment(ref touch, lane);

			if (childNote != null
				&& LiveConfig.IsLongEndJudgeTime(childNote.JudgeOffsetAt(touch.musicTime))
				&& childNote.IsJudgment(ref touch, lane)
				&& childNote.Judgment(ref touch, lane))
			{
				if (LiveTrace.IsOn(LiveTraceCategory.Hold))
				{
					LiveTrace.Write(LiveTraceCategory.Hold, $"headPressChild barT={MusicScoreInfo.time:F3} touchT={touch.musicTime:F3} childT={childNote.MusicScoreInfo.time:F3}");
				}

				State = NoteState.Done;
				return true;
			}

			// Being held: the bar is kept alive by its own refresh pass, so the finger can still serve
			// a note that sits on top of it.
			return true;
		}


		/// <summary>
		/// Keeps the bar in the held state because some finger is on it, without caring which one.
		/// A bar is not owned: any touch inside keeps it alive, which is what lets a finger come back
		/// after letting go and what stops a second finger from stealing the hold.
		/// </summary>
		/// <summary>
		/// Name of the caller <paramref name="depth"/> frames up, for trace lines only. Capturing a
		/// stack trace is expensive, so this is only ever reached with tracing on.
		/// </summary>
		private static string CallerName(int depth)
		{
			System.Diagnostics.StackFrame frame = new System.Diagnostics.StackTrace().GetFrame(depth);
			return frame == null || frame.GetMethod() == null
				? "?"
				: frame.GetMethod().DeclaringType.Name + "." + frame.GetMethod().Name;
		}

		internal void KeepAlive(float musicTime)
		{
			// A bar that is still alive must never be refreshed with a time from the past: that is how
			// a recycled note looked held long after the run that held it.
			LiveTrace.Invariant(musicTime + 0.001f >= lastInputTime, $"keepAlive went backwards barT={MusicScoreInfo.time:F3} last={lastInputTime:F3} now={musicTime:F3}");
			lastInputTime = Mathf.Max(lastInputTime, musicTime);

			// A finger on the bar means it is held, even when the head was missed: the chart lets a
			// finger that arrives late (or was already resting there) pick the bar up.
			RestoreHeldState();
		}

		/// <summary>
		/// How this bar is meant to end, derived from the note that ends it. Deliberately not cached:
		/// the chart is built once but bars are reset between runs, and any cached copy of this answer
		/// is wiped by that reset while the binding survives it.
		/// </summary>
		internal ITailBehaviour TailBehaviour => SkipsTailJudgement
			? SilentTail.Instance
			: childNote == null ? ReleaseTail.Instance : TailBehaviours.For(childNote);

		/// <summary>
		/// An ordinary bar: a plain long note, not a rub and not a decoration. Its head is where the
		/// body is supposed to shrink from, so it is the only kind that holds its head on the line while
		/// nobody is holding it.
		/// </summary>
		internal bool IsOrdinaryHead => Category == NoteCategory.Long && this is not GuideNote && this is not FrictionLongNote;

		/// <summary>
		/// Whether the bar's body starts at the judgement line while the head's time has passed. Only an
		/// ordinary bar does: that is where its body is supposed to shrink from, taken or not. A rub or a
		/// guide bar has no such state to stand in - it is played across its whole body, so its head must
		/// keep descending. An unplayed one therefore flows past the line and reads as a miss that went
		/// by, instead of freezing there.
		/// </summary>
		internal bool KeepsHeadOnTheLine => IsOrdinaryHead;

		/// <summary>
		/// The note that really ends this bar: the latest one that is neither skipped nor decorative.
		/// Picking the latest by time alone picked up a hidden or skipped point, so the real tail was
		/// never "the tail" and its window could never open - every ordinary bar missed its end.
		/// </summary>
		internal bool IsChildTheTail
		{
			get
			{
				return TryGetTail(out NoteBase tail) && ReferenceEquals(childNote, tail);
			}
		}

		/// <summary>
		/// Whether a note can be the thing a bar ends with. A combo tick carries no view, a hidden
		/// marker only moves the lane for what follows it, and a decoration is carried rather than
		/// played - none of the three can be the end of the bar.
		/// </summary>
		/// <summary>
		/// True when the bar's last note carries no judgement of its own (a hidden marker). Such a bar
		/// ends silently: no tail result, no combo, and letting go of it early changes nothing.
		/// </summary>
	internal bool EndsWithoutJudgement => SkipsTailJudgement || (_lastBoundNote != null && !IsTailCandidate(_lastBoundNote));

	/// <summary>
	/// True when the bar starts with a note the player taps. Only an ordinary head is one: a rubbed
	/// bar is played across its whole body and a decoration is carried, so neither has a head that
	/// can go by untouched. Bars all travel the same states; this only decides whether there is a
	/// result to publish when the head's window closes.
	/// </summary>

		/// <summary>
		/// A bar whose hold has lapsed is already grey while it still has a few frames of grace,
		/// so Last reads as released on screen but still counts as held by the state machine.
		/// </summary>
		public override bool ShowsAsReleased => State == NoteState.Release || State == NoteState.Last;
	internal bool PublishesHeadMiss => IsOrdinaryHead && HasJudgment;

	/// <summary>Finishes the bar with no result at all, once its last note has gone by.</summary>
	internal bool CanFinishSilently(float currentTime)
	{
		// A bar with a head the player could press is not over until that head has had its say:
		// finishing silently first would swallow its miss, and the bar would vanish with no result at all.
		if (PublishesHeadMiss && !Head.IsSettled)
		{
			return false;
		}

		return EndsWithoutJudgement && _lastBoundNote != null
			&& currentTime > _lastBoundNote.MusicScoreInfo.time;
	}

	/// <summary>
	/// Settles whatever is still bound to the bar and unjudged, so a bar never ends with holes in it.
	/// Only parts that are still attached count: a detached standalone tail owns its own result and a
	/// skipped marker is not a unit of judgement.
	/// </summary>
	private void SettleLeftoverAsPerfect()
	{
		if (NoteList == null)
		{
			return;
		}

		for (int i = 0; i < NoteList.Count; i++)
		{
			NoteBase part = NoteList[i];
			if (part == null || !(part is LongHoldCombo))
			{
				continue;
			}

			if (part.ParentNote != this || part.Result != NoteResult.None)
			{
				continue;
			}

			part.FinishWith(NoteResult.Perfect);
		}
	}


	internal void FinishSilently()
	{
		if (State == NoteState.Done)
		{
			return;
		}

		_swipeTailDeferred = false;
		_swipeTailCapped = false;
		_framesSinceRelease = -1;
		SettleLeftoverAsPerfect();
		State = NoteState.Done;
	}

	internal static bool IsTailCandidate(NoteBase note)
		{
			return note != null
				&& !note.IsSkip
				&& !note.IsDecoration
				&& !(note is HiddenConnectionNote)
				&& !(note is FrictionHideNote);
		}

		internal bool TryGetTail(out NoteBase tail)
		{
			tail = null;
			if (NoteList == null)
			{
				return false;
			}

			for (int i = 0; i < NoteList.Count; i++)
			{
				NoteBase candidate = NoteList[i];
				if (!IsTailCandidate(candidate))
				{
					continue;
				}

				if (tail == null || candidate.MusicScoreInfo.time >= tail.MusicScoreInfo.time)
				{
					tail = candidate;
				}
			}

			return tail != null;
		}

		internal bool IsTailJudgeableAt(float currentTime)
		{
			// Only the real tail counts. Every interior point - combo ticks as well as the preset and
			// hidden ones - sits at offset ~0 when it comes up, so testing "the current child is inside
			// its window" made a release anywhere in the bar look like the tail.
			//
			// The tail is the note with the latest time, not the last one in the list: hidden points and
			// preset points are appended in chart order and can come after the end.
			if (NoteList == null || NoteList.Count == 0 || childNote == null)
			{
				return false;
			}

			if (!TryGetTail(out NoteBase tail) || !ReferenceEquals(childNote, tail) || !TailBehaviour.AcceptsRelease)
			{
				return false;
			}

			// Every kind of end note is release-finishable: a bar closing on a friction or a hidden
			// note ends exactly like a plain one. Only a decorative point is not a real end.
			//
			// The category used to be pinned to Long here, which silently disabled the release tail
			// for every bar ending on FrictionLong/FrictionHideLong: the tail was never judged,
			// nothing else could end the bar, and the note stayed stuck on the line.
			// A flick-ended bar is not finished by a release: it waits for the swipe, which is the
			// separate path in TrySwipeTail. Every other kind of end note is release-finishable.
			if (childNote.IsDecoration || childNote is FlickNote || childNote is FrictionFlickNote)
			{
				return false;
			}
			return IsTailWindowOpenAt(currentTime);
		}

		/// <summary>
		/// Whether this bar's tail is the current child and its grading window is open right now. Split
		/// from <see cref="IsTailJudgeableAt"/> because a flick tail is finished by a swipe rather than by
		/// a release, and its note category is the flick one, not Long.
		/// </summary>
		internal bool IsTailWindowOpenAt(float currentTime)
		{
			if (NoteList == null || NoteList.Count == 0 || childNote == null)
			{
				return false;
			}

			// The window itself has nothing to do with how the bar ends: a swipe tail and a
			// self-judged tail are scored inside the very same window, they just answer a
			// different question about who writes the result.
			if (!TryGetTail(out NoteBase tail) || !ReferenceEquals(childNote, tail) || childNote.IsDecoration)
			{
				return false;
			}

			// The offset has to come from the clock, not from the note: a note only refreshes its
			// OffsetJudgeTime when it is executed, and the tail is not executed while the bar is still
			// mid-body - reading the stale value (0) graded every mid-body release as a perfect tail.
			NoteResult grade = OpenSekai.Shared.Live.LongEndJudgement.Evaluate(currentTime - childNote.MusicScoreInfo.time);
			if (grade == NoteResult.None && currentTime > childNote.MusicScoreInfo.time && !_windowTraced
				&& LiveTrace.IsOn(LiveTraceCategory.Tail))
			{
				_windowTraced = true;
				LiveTrace.Write(LiveTraceCategory.Tail, $"winClosed barT={MusicScoreInfo.time:F3} now={currentTime:F3} child={childNote.GetType().Name}@{childNote.MusicScoreInfo.time:F3} childState={childNote.State} childRes={childNote.Result} same={ReferenceEquals(childNote, tail)} deco={childNote.IsDecoration} tail={tail.GetType().Name}@{tail.MusicScoreInfo.time:F3}");
			}

			return grade != NoteResult.None;
		}

		/// <summary>
		/// The tail's own time has gone by and its window has closed without anything ending the bar.
		/// Only a flick tail used to get a timeout, so a bar closed by a friction or a hidden note kept
		/// living forever: nothing could end it and a finger in its lane revived it every frame.
		/// </summary>
		internal bool IsPastTailWindow(float currentTime)
		{
			return childNote != null
				&& currentTime > childNote.MusicScoreInfo.time
				&& !IsTailWindowOpenAt(currentTime);
		}

		/// <summary>
		/// Ends a bar whose tail was never resolved, grading it a miss.
		/// </summary>
		internal bool ExpireTail(float currentTime)
		{
			if (childNote != null && currentTime > childNote.MusicScoreInfo.time && !_expireTraced
				&& LiveTrace.IsOn(LiveTraceCategory.Tail))
			{
				_expireTraced = true;
				LiveTrace.Write(LiveTraceCategory.Tail, $"expireCheck barT={MusicScoreInfo.time:F3} now={currentTime:F3} state={State} res={Result} child={childNote.GetType().Name}@{childNote.MusicScoreInfo.time:F3} past={IsPastTailWindow(currentTime)}");
			}

			if (State == NoteState.Done)
			{
				return false;
			}

			if (CanFinishSilently(currentTime))
			{
				FinishSilently();
				return true;
			}

			if (!IsPastTailWindow(currentTime))
			{
				return false;
			}

			// Past the last window nothing about the bar may survive: no hold keeps it alive and no
			// deferred swipe keeps waiting. A standalone tail owns its own result, so the bar just
			// detaches instead of writing a miss onto it.
			if (TailBehaviour.JudgesItself)
			{
				return DetachStandaloneTail(currentTime);
			}

			return FailTail();
		}

		/// <summary>
		/// Scores the tail because a finger left the screen inside the bar. The bar finishes and the
		/// tail note carries the result, so the tail is judged explicitly here.
		/// </summary>
		internal bool JudgeTailAsRelease(float currentTime)
		{
			if (State == NoteState.Done || !IsTailJudgeableAt(currentTime))
			{
				return false;
			}

			if (LiveTrace.IsOn(LiveTraceCategory.Tail))
			{
				LiveTrace.Write(LiveTraceCategory.Tail, $"release barT={MusicScoreInfo.time:F3} now={currentTime:F3} tailT={childNote.MusicScoreInfo.time:F3}");
			}

			return JudgeTail(currentTime);
		}

		/// <summary>Ends the bar with a fixed grade, used when the gesture itself was wrong.</summary>
		private bool JudgeTailAs(NoteResult result)
		{
			if (State == NoteState.Done || childNote == null)
			{
				return false;
			}

			if (childNote.Result == NoteResult.None)
			{
				childNote.FinishWith(result);
			}

			JudgeInfo = (result, NoteResultDescription.None);
			State = NoteState.Done;
			_swipeTailDeferred = false;
			_swipeWindowStart = -1f;
			_swipeAccum = Vector2.zero;
			_swipeTailCapped = false;
			_framesSinceRelease = -1;
			return true;
		}

		/// <summary>Scores the tail and finishes the bar. Callers own the gate; this only grades.</summary>
		private bool JudgeTail(float currentTime)
		{
			if (LiveTrace.IsOn(LiveTraceCategory.Tail))
			{
				LiveTrace.Write(LiveTraceCategory.Tail, $"tailJudge barT={MusicScoreInfo.time:F3} now={currentTime:F3} tailT={childNote.MusicScoreInfo.time:F3} capped={_swipeTailCapped} deferred={_swipeTailDeferred} state={State}");
			}

			return JudgeTailCore(currentTime);
		}

		private bool JudgeTailCore(float currentTime)
		{
			if (State == NoteState.Done || childNote == null)
			{
				return false;
			}

			// Every route into grading re-checks the window: a bar that is reused from the pool can
			// still carry a deferred swipe from the previous run, and grading that would finish the
			// bar before it ever reached the line.
			if (!IsTailWindowOpenAt(currentTime))
			{
				_swipeTailDeferred = false;
				_swipeTailCapped = false;
				_framesSinceRelease = -1;
				return false;
			}

			NoteResult result = OpenSekai.Shared.Live.LongEndJudgement.Evaluate(currentTime - childNote.MusicScoreInfo.time);

			// A malformed gesture keeps its timing grade but can never reach Perfect.
			if (_swipeTailCapped && (result == NoteResult.Perfect || result == NoteResult.JustPerfect))
			{
				result = NoteResult.Great;
			}

			if (childNote.Result == NoteResult.None)
			{
				childNote.FinishWith(result);
			}

			JudgeInfo = (result, NoteResultDescription.None);
			State = NoteState.Done;
			_swipeTailDeferred = false;
			_swipeTailCapped = false;
			_framesSinceRelease = -1;
			return true;
		}

		internal bool DetachStandaloneTail(float currentTime)
		{
			if (State == NoteState.Done || childNote == null)
			{
				return false;
			}

			bool settled = childNote.Result != NoteResult.None || childNote.State == NoteState.Done;
			if (!settled && !IsPastTailWindow(currentTime))
			{
				return false;
			}

			JudgeInfo = settled
				? (childNote.Result, NoteResultDescription.None)
				: (NoteResult.Miss, NoteResultDescription.None);
			State = NoteState.Done;

			// The tail judges itself, so the bar must not grade it: it leaves the list instead. The parent
			// link stays, because the tail still belongs to this bar - it just is not a part the bar can end.
			if (NoteList != null)
			{
				NoteList.Remove(childNote);
			}

			// Nobody graded this tail and its window is gone, so it fails itself here: the bar only
			// noticed the time, the verdict is still the tail's own.
			if (!settled && childNote.Result == NoteResult.None && childNote.State != NoteState.Done)
			{
				childNote.FinishWith(NoteResult.Miss);
			}
			_swipeTailDeferred = false;
			_swipeTailCapped = false;
			_framesSinceRelease = -1;
			return true;
		}

		private bool _swipeTailDeferred;
		private bool _swipeTailCapped;
		private Vector2 _swipeAccum;
		private float _swipeWindowStart = -1f;
		private int _framesSinceRelease = -1;

		/// <summary>
		/// A swipe was seen inside this bar. Swiping before the tail time is only recorded: judging it
		/// right away would reward an early swipe exactly like an on time one, and the original defers
		/// to the third frame after the release so that the swipe is graded closer to the tail time.
		/// </summary>
		internal bool NotifyLaneSwipe(float magnitude, float currentTime)
		{
			// The speed gate already ran in TrySwipeTail; a raw displacement threshold here would
			// reject a 5 cm/s swipe at high frame rates, where one frame moves under a millimetre.
			if (State == NoteState.Done || !TailBehaviour.AcceptsSwipe || magnitude <= 0f)
			{
				return false;
			}

			// The end point only exists once the bar is being held. Without this a swipe made right
			// after the bar appears would finish it while its head is still travelling towards the
			// line, which is also how a short bar could be ended before it was ever started.
			if (!HasFinger)
			{
				return false;
			}

			if (!IsTailWindowOpenAt(currentTime))
			{
				return false;
			}

			if (LiveTrace.IsOn(LiveTraceCategory.Swipe))
			{
				LiveTrace.Write(LiveTraceCategory.Swipe, $"swipe barT={MusicScoreInfo.time:F3} now={currentTime:F3} tailT={childNote.MusicScoreInfo.time:F3} mag={magnitude:F1}");
			}

			if (currentTime < childNote.MusicScoreInfo.time)
			{
				// A swipe only counts while the tail is inside its early window (-125 ms). Anything
				// earlier is a gesture aimed at the bar body, not at its end point; remembering it
				// let a small wobble during the hold auto grade the tail when the bar arrived.
				if (childNote.MusicScoreInfo.time - currentTime > OpenSekai.Shared.Live.LongEndJudgement.EarlyGoodMs / 1000f)
				{
					if (OpenSekai.Shared.Diagnostics.LiveTrace.IsOn(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Swipe))
					{
						OpenSekai.Shared.Diagnostics.LiveTrace.Write(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Swipe, $"tailSwipeTooEarly barT={MusicScoreInfo.time:F3} now={currentTime:F3} tailT={childNote.MusicScoreInfo.time:F3}");
					}

					return false;
				}

				// The original defers an early swipe to the third frame after the release: the
				// countdown starts when the finger leaves, not when the swipe happens.
				_swipeTailDeferred = true;
				_framesSinceRelease = -1;
				return false;
			}

			return JudgeTail(currentTime);
		}

		/// <summary>A swipe was seen and is waiting for the release that starts the countdown.</summary>
		internal bool HasDeferredSwipe => _swipeTailDeferred;

		private bool _tailPressed;

		/// <summary>
		/// The finger actually clicked this bar. A decorative bar carries a real tail but its body
		/// takes no input, so the tail has to behave like the plain note it is: a drag that merely
		/// crosses it is not a flick until something pressed it.
		/// </summary>
		internal void MarkTailPressed()
		{
			_tailPressed = true;
		}

		/// <summary>An in lane finger lifted while a deferred swipe is pending; counting starts now.</summary>
		internal void NotifyLaneRelease()
		{
			if (_swipeTailDeferred && _framesSinceRelease < 0)
			{
				_framesSinceRelease = 0;
			}
		}

		/// <summary>Advances the deferred swipe; it is graded on the third frame after the release.</summary>
		internal bool TickSwipeTail(float currentTime)
		{
			if (State == NoteState.Done || !TailBehaviour.AcceptsSwipe)
			{
				return false;
			}

			// Reaching the tail's own time counts as a release: the three frame countdown starts now,
			// which is what turns an early swipe into a late — and therefore better — grade.
			if (_swipeTailDeferred && _framesSinceRelease < 0 && childNote != null
				&& currentTime >= childNote.MusicScoreInfo.time)
			{
				// The frame that notices the tail time is frame zero of the release, not frame one.
				NotifyLaneRelease();
				return false;
			}

			if (_swipeTailDeferred && _framesSinceRelease >= 0)
			{
				// A bar that has not been held yet cannot have a deferred swipe of its own; anything
				// left here is pool residue from the previous run.
				// The countdown starts exactly when the bar stops being held, so Last and Release must
				// keep counting: treating them as residue cleared every deferred swipe on its first frame.
				if (!HasFinger && State != NoteState.Last && State != NoteState.Release)
				{
					_swipeTailDeferred = false;
					_swipeTailCapped = false;
					_framesSinceRelease = -1;
					return false;
				}

				_framesSinceRelease++;
				if (_framesSinceRelease < HoldGraceFrames)
				{
					return false;
				}

				// The cap has to survive until the judgement is written: a wrong way swipe that was
				// seen early is only graded once the release delay is over, and clearing the flag here
				// is what let a wrong way swipe be graded Perfect. JudgeTail clears it.
				_swipeTailDeferred = false;
				_framesSinceRelease = -1;
				return JudgeTail(currentTime);
			}

			// No swipe is pending and the tail window has closed. A flick ended bar is not finished by
			// lifting, so nothing else can end it: without this the tail note would sit on the judgement
			// line for the rest of the chart and the bar would never be scored.
			if (currentTime > childNote.MusicScoreInfo.time && !IsTailWindowOpenAt(currentTime))
			{
				return FailTail();
			}

			return false;
		}

		/// <summary>
		/// Ends a bar whose tail can no longer be earned. The tail is graded Miss instead of being left
		/// in the air, so its note stops sticking to the judgement line.
		/// </summary>
		internal bool FailTail()
		{
			if (State == NoteState.Done || childNote == null)
			{
				return false;
			}

			if (LiveTrace.IsOn(LiveTraceCategory.Swipe))
			{
				LiveTrace.Write(LiveTraceCategory.Swipe, $"tailMiss barT={MusicScoreInfo.time:F3} tailT={childNote.MusicScoreInfo.time:F3} state={State} deferred={_swipeTailDeferred}");
			}

			_swipeTailDeferred = false;
			_swipeTailCapped = false;
			_framesSinceRelease = -1;
			if (childNote.Result == NoteResult.None)
			{
				// Nothing was played here: grade it Miss. The judgement popup has to show, and the tap
				// effect decides on its own that a Miss earns no hit sound.
				childNote.FinishWith(NoteResult.Miss);
			}

			JudgeInfo = (NoteResult.Miss, NoteResultDescription.None);
			State = NoteState.Done;
			return true;
		}

		/// <summary>
		/// Tests a finger's movement this frame as the swipe that finishes a flick ended bar. The swipe
		/// is judged by the tail flick's own rule (distance and direction), so a bar whose tail asks for
		/// a left flick is not finished by a swipe to the right, and a vertical drag inside a single
		/// lane is only a flick when the tail accepts any direction.
		/// </summary>
		internal bool TrySwipeTail(Vector2 delta, float deltaTime, float currentTime)
		{
			if (State == NoteState.Done || !TailBehaviour.AcceptsSwipe || childNote == null)
			{
				return false;
			}

			// The end point only exists while the bar is being held and inside its tail window.
			if (!HasFinger)
			{
				return false;
			}

			// A decorative bar is not played, only its tail is: without a click on it this is just a
			// finger dragging across the screen. A guide bar is the decorative kind (it carries no
			// judgement of its own), and IsDecoration is not set on it.
			if (!HasJudgment && !_tailPressed)
			{
				return false;
			}

			if (!IsTailWindowOpenAt(currentTime))
			{
				return false;
			}

			float flickDistance = childNote switch
			{
				FlickNote flick => flick.FlickDistance,
				FrictionFlickNote frictionFlick => frictionFlick.FlickDistance,
				_ => 0f
			};

			// A flick is judged on one 60 fps frame of travel, not on a single 165 Hz sample: a held
			// finger jitters by a few tenths of a millimetre per frame, which reads as 5-14 cm/s and
			// would pass a per-frame speed gate on every frame. Accumulating the *net* displacement
			// over 16.7 ms keeps a real flick (one direction, large net travel) and drops jitter
			// (oscillating, net travel near zero).
			if (_swipeWindowStart < 0f)
			{
				_swipeWindowStart = currentTime - Mathf.Max(deltaTime, 0f);
				_swipeAccum = Vector2.zero;
			}

			_swipeAccum += delta;

			float frameSeconds = OpenSekai.Shared.Live.FlickGesture.ReferenceFrameSeconds;
			float elapsed = currentTime - _swipeWindowStart;

			// Wait for one 60 fps frame of travel, or stop early once the finger has already covered a
			// whole flick distance inside the window: that much travel in a few milliseconds cannot be
			// jitter, and waiting only adds latency to a real flick.
			if (elapsed + 1e-4f < frameSeconds && _swipeAccum.magnitude < flickDistance)
			{
				return false;
			}

			Vector2 netDelta = _swipeAccum;
			_swipeAccum = Vector2.zero;
			_swipeWindowStart = currentTime;

			float minSpeed = flickDistance / frameSeconds;
			float speed = netDelta.magnitude / elapsed;

			if (!OpenSekai.Shared.Live.FlickGesture.TryUpdate(netDelta, frameSeconds, childNote.Direction, flickDistance, out bool trueDirection))
			{
				return false;
			}

			// A swipe the wrong way is not ignored: it ends the bar like any other, it just can never
			// be graded above Great. The cap is remembered here and applied wherever the swipe is
			// settled, because an early swipe is only graded a few frames after it is seen.
			_swipeTailCapped = !trueDirection;

			// A flick is a speed, not a displacement: the note's own flick distance is what 5 cm/s
			// looks like in one 60 fps frame, so requiring it in pixels per second is the same rule
			// the plain flick uses, only frame rate independent. A displacement gate on top of that
			// rejected legitimate swipes at 165 Hz, where 5 cm/s is under five pixels per frame.

			if (OpenSekai.Shared.Diagnostics.LiveTrace.IsOn(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Swipe))
			{
				OpenSekai.Shared.Diagnostics.LiveTrace.Write(OpenSekai.Shared.Diagnostics.LiveTraceCategory.Swipe, $"tailSwipe barT={MusicScoreInfo.time:F3} now={currentTime:F3} tailT={childNote.MusicScoreInfo.time:F3} speed={speed:F0} min={minSpeed:F0} state={State}");
			}

			if (speed < minSpeed)
			{
				return false;
			}

			return NotifyLaneSwipe(delta.magnitude, currentTime);
		}

		/// <summary>Kept for callers that still describe a swipe by its magnitude.</summary>
		internal const float TailSwipeThreshold = 5f;


		// Body transitions remain visual/lifecycle operations. Only Head publishes head results.
		internal void SetHeadJudgmentState(NoteState value) => State = value;

		internal NoteBase HeadChildNote => childNote;

		internal void SetHeadJudgeInfo((NoteResult result, NoteResultDescription description) value) => JudgeInfo = value;

		internal Func<(NoteResult result, NoteResultDescription description), (NoteResult result, NoteResultDescription description)> HeadResultUpdater => onUpdateResult;

		internal void MarkHeadTouched() => _headTouched = true;

		internal void ClearHeadJudgeOffset() => OffsetJudgeTime = 0f;

		internal Action<NoteBase> HeadDamage => onDamage;
		internal Action<NoteBase> HeadCombo => onUpdateCombo;
		internal Action<NoteBase> HeadJudgment => onJudgment;
		internal Action<NoteBase> HeadScore => onUpdateScore;

		internal void RecordHoldingInput(int fingerId, float musicTime)
		{
			lastInputTime = Mathf.Max(lastInputTime, musicTime);
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
		internal void MarkPressedThisFrame()
		{
			_pressedThisFrame = true;

			// The press time carried by the touch is when the finger went down, which can be long
			// before the bar's head. The freshest clock this note has seen is its own last frame, so
			// stamp that instead; it is at most one frame old.
			lastInputTime = Mathf.Max(lastInputTime, _lastFrameTime);
		}

		private bool _pressedThisFrame;

		private LongNoteHead _head;

		internal LongNoteHead Head => _head ??= new LongNoteHead(this);
	private bool _headTouched;

	/// <summary>Frames spent in <see cref="NoteState.Last"/> since the last frame a finger was on the bar.</summary>
		private float _graceStartedAt;

		/// <summary>Music time this note was last updated with, used when a press needs a stamp.</summary>
		private float _lastFrameTime;

		/// <summary>Set once a bar has already reported a closed tail window, so the log stays one line per bar.</summary>
		private NoteBase _lastBoundNote;

		/// <summary>
		/// A decorative bar never owns its end: the tail is a note of its own that the player judges.
		/// The bar just stops when that note's time passes, with no result of its own.
		/// </summary>
		private bool _skipTailJudgement;

		public void SkipTailJudgement()
		{
			_skipTailJudgement = true;
		}

		/// <summary>
		/// A bar type may decide by itself that the note at its end is played by the player.
		/// <see cref="GuideNote"/> does, so the converter flag is only a fallback.
		/// </summary>
		protected virtual bool SkipsTailJudgement => _skipTailJudgement;

	private bool _windowTraced;

		/// <summary>Set once a bar has reported its first pass over the tail time, so the log stays short.</summary>
		private bool _expireTraced;

		/// <summary>
		/// Slack a hold gets before the update pass calls it released. One reference frame plus the
		/// chart's own <c>MissMesh</c>, so a 165 Hz device does not release a hold 6 ms after the last
		/// refresh while a 60 Hz device gives it 16.7 ms.
		/// </summary>
		private static float HoldToleranceSeconds =>
			ReferenceFrame.FromFrames(HoldGraceFrames + LiveConfig.noteTypeJudgeData.MissMesh);

		/// <summary>
		/// Releasing or stepping out of the bar keeps the hold alive for this many frames; only after
		/// that does the bar become "released". The chart's own MissMesh is added on top.
		/// </summary>
		internal const int HoldGraceFrames = 3;
		/// <summary>
		/// The same grace as a duration: three frames of the 60 fps judgement tables, so 50 ms on a
		/// 165 Hz phone and on a 60 Hz one alike.
		/// </summary>
		internal static float HoldGraceSeconds => ReferenceFrame.FromFrames(HoldGraceFrames);



		/// <summary>
		/// Brings a bar back to its held picture when a finger lands on it again. A bar that has already
		/// crossed its own line is left alone: Scrolling means "never taken yet", and sending a bar that
		/// has moved on back there made the crossing branch fire again every frame (Scrolling to Last),
		/// so the grace that leads to Release never finished and the tail was left unjudged until its
		/// window shut. What a bar looks like after the line follows ShowsAsReleased, not this.
		/// </summary>
		internal void RestoreHeldState()
		{
			if (State != NoteState.Release && State != NoteState.Last)
			{
				return;
			}

			if (this is FrictionLongNote)
			{
				SetStateUnnotice(NoteState.Input);
				return;
			}

			if (MusicScore.CurrentFrameInfo.time - MusicScoreInfo.time <= LiveConfig.JudgeTime)
			{
				SetStateUnnotice(NoteState.Scrolling);
			}
		}

		/// <summary>
		/// Refresh a hold that this finger already owns. Interior combo ticks are judged inside
		/// ConnectionNoteJudgment, so a refresh that skipped it would leave the sixteenth-note markers
		/// unjudged even though the hold stays alive.
		/// </summary>
		internal void RefreshHeldInput(ref LiveTouch touch, float lane)
		{
			RecordHoldingInput(touch.fingerId, touch.musicTime);
			RestoreHeldState();
			ConnectionNoteJudgment(ref touch, lane);
		}

		protected void ConnectionNoteJudgment(ref LiveTouch touch, float lane)
		{
			Head.ConnectionNoteJudgment(ref touch, lane);
		}

		protected void ConnectionNoteTerminate()
		{
			if (NoteList == null)
			{
				return;
			}

			for (var i = 1; i < NoteList.Count - 1; i++)
			{
				var note = NoteList[i];
				if (note != null && note.State != NoteState.Done)
				{
					note.Terminate();
				}
			}
		}

		public override float LaneDistance(ref MusicScoreInfo currentFrameInfo, float inputLane)
		{
			var centerLane = (LaneStartF + LaneEndF) * 0.5f;
			var distance = centerLane - inputLane;
			return distance * distance;
		}

		public override bool IsJudgment(ref LiveTouch touch, float lane)
		{
			if (State == NoteState.Done)
			{
				return false;
			}

			var left = JudgeInfo.result == NoteResult.None ? JudgeLaneStart : JudgedLaneStart;
			var right = JudgeInfo.result == NoteResult.None ? JudgeLaneEnd : JudgedLaneEnd;
			if (left > lane || right < lane)
			{
				return false;
			}

			// Judged through the frame offset on purpose: candidate admission happens after Excute for the
			// same frame, which is the contract this cache stands for.
			return LiveUtility.IsJudgmentTiming(LiveUtility.GetINoteToJudgeFrameType(this), OffsetJudgeTime)
				&& ((HasFinger && !ShowsAsReleased) || (State != NoteState.Scrolling && State != NoteState.Last) || touch.phase == UnityEngine.InputSystem.TouchPhase.Began);
		}

		/// <summary>
		/// Lane containment without the judgment timing window. A hold that is already being held has to
		/// keep refreshing its input frame while its finger stays inside the lane, including the frames
		/// between two sixteenth-note combo checks, where <see cref="IsJudgment"/> is closed.
		/// </summary>
		internal bool ContainsLane(float lane)
		{
			var left = JudgeInfo.result == NoteResult.None ? JudgeLaneStart : JudgedLaneStart;
			var right = JudgeInfo.result == NoteResult.None ? JudgeLaneEnd : JudgedLaneEnd;
			return left <= lane && right >= lane;
		}

		public virtual bool IsJudgmentChild(ref LiveTouch touch, float lane)
		{
			return State != NoteState.Done && JudgedLaneStart <= lane && JudgedLaneEnd >= lane;
		}

		public override bool AutoJudgment(MusicScoreInfo currentFrameInfo)
		{
			if (State == NoteState.Done || MusicScoreInfo.time > currentFrameInfo.time)
			{
				return false;
			}

			lastInputTime = currentFrameInfo.time;
			Head.Settle((NoteResult.Auto, NoteResultDescription.None));
			RestoreHeldState();
			OffsetJudgeTime = 0f;
			ConnectionNoteAutoJudgment(currentFrameInfo);

			if (childNote != null && childNote.AutoJudgment(currentFrameInfo))
			{
				State = NoteState.Done;
			}

			return true;
		}

		private void ConnectionNoteAutoJudgment(MusicScoreInfo currentFrameInfo)
		{
			if (Head.Result != NoteResult.Auto)
			{
				return;
			}

			if (NoteList == null)
			{
				return;
			}

			for (var i = 1; i < NoteList.Count - 1; i++)
			{
				NoteList[i]?.AutoJudgment(currentFrameInfo);
			}
		}

		public override NoteResult CalcNoteResult(float musicTime)
		{
			return LiveConfig.CalculateLongStartNoteResult(this, musicTime).Item1;
		}

		public override void ResetNote()
		{
			base.ResetNote();
			lastInputTime = 0f;
			_fingerOnIt = false;
			_everTouched = false;
			WasHoldingWhenTerminated = false;
			_swipeTailDeferred = false;
			_swipeTailCapped = false;
			_framesSinceRelease = -1;
			_pressedThisFrame = false;
			_head?.Reset();
			_headTouched = false;
			_graceStartedAt = 0f;
			_tailPressed = false;
			_lastFrameTime = 0f;
			_swipeAccum = Vector2.zero;
			_swipeWindowStart = -1f;
			_windowTraced = false;
			_expireTraced = false;
			_lastBoundNote = null;
			_skipTailJudgement = false;
		}

		public void SortNoteList()
		{
			if (NoteList != null)
			{
				NoteList.Sort((a, b) => a.MusicScoreInfo.time.CompareTo(b.MusicScoreInfo.time));
			}

			LineNoteList = LineNoteList.OrderBy(note => note.MusicScoreInfo.time).ToList();

			if (ViewNoteList != null)
			{
				ViewNoteList = ViewNoteList.OrderBy(note => note.MusicScoreInfo.time).ToList();
			}
		}

		private void ApplyToChildNotes(Action<NoteBase> apply)
		{
			if (NoteList == null || apply == null)
			{
				return;
			}

			foreach (var note in NoteList)
			{
				if (!ReferenceEquals(note, this))
				{
					apply(note);
				}
			}
		}
	}
}