using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Sekai;
using Sekai.Core.Live;
using OpenSekai.Shared.Diagnostics;
using OpenSekai.Shared.Live;

namespace Sekai.Live
{
	public class NoteBase : INote
	{
		protected Action<NoteBase> onUpdateScore;

		protected Action<NoteBase> onUpdateCombo;

		protected Action<NoteBase> onDamage;

		protected Action<NoteBase> onSpawn;

		protected Action<NoteBase> onJudgment;

		protected Action<NoteBase> onUnspawn;
		private bool viewReleased;

		protected Func<(NoteResult result, NoteResultDescription description), (NoteResult result, NoteResultDescription description)> onUpdateResult;

				private Func<NoteBase, float> _CalcTimeOffset;
		public Func<NoteBase, float> CalcTimeOffset
		{
		    get => _CalcTimeOffset;
		    set => _CalcTimeOffset = value;
		}

		protected NoteState state;

		protected LongNote parentNote;

		protected NoteBase childNote;

		public virtual Action<NoteBase> OnUpdateScore
		{
			set
			{
				onUpdateScore = value;
			}
		}

		public virtual Action<NoteBase> OnUpdateCombo
		{
			set
			{
				onUpdateCombo = value;
			}
		}

		public virtual Action<NoteBase> OnDamage
		{
			set
			{
				onDamage = value;
			}
		}

		public virtual Action<NoteBase> OnJudgment
		{
			set
			{
				onJudgment = value;
			}
		}

		public virtual Func<(NoteResult result, NoteResultDescription description), (NoteResult result, NoteResultDescription description)> OnUpdateResult
		{
			set
			{
				onUpdateResult = value;
			}
		}

		public virtual Action<NoteBase> OnSpawn
		{
			set
			{
				onSpawn = value;
			}
		}

		public virtual Action<NoteBase> OnUnspawn
		{
			set
			{
				onUnspawn = value;
			}
		}

		public int DefaultLeftLane
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		public int DefaultRightLane
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		[JsonIgnore]
		public virtual int LaneStart
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		[JsonIgnore]
		public virtual int LaneEnd
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		[JsonIgnore]
		public virtual float LaneStartF
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		[JsonIgnore]
		public virtual float LaneEndF
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		[JsonIgnore]
		public virtual float JudgeLaneStart
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		[JsonIgnore]
		public virtual float JudgeLaneEnd
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		public MusicScoreInfo MusicScoreInfo
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		/// <summary>
		/// How far past its own time a note is, at <paramref name="currentTime"/>. This is the only
		/// honest way to ask the question: <see cref="OffsetJudgeTime"/> is a cache refreshed by
		/// <see cref="Excute"/>, so a note that has not been executed yet (a bar's tail, for one)
		/// still reports zero and would look exactly on time.
		/// </summary>
		public float JudgeOffsetAt(float currentTime)
		{
			return currentTime - MusicScoreInfo.time;
		}

		[JsonIgnore]
		public float OffsetJudgeTime
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		[JsonIgnore]
		public float Progress
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			protected set;
		}

		[JsonIgnore]
		public virtual NoteState State
		{
			get
			{
				return state;
			}
			protected set
			{
				if (value == NoteState.Done && state == NoteState.Done) return;
				state = value;
				if (value == NoteState.Scrolling)
				{
					OnSpawnNote();
				}
				else if (value == NoteState.Done)
				{
					OnUnSpawnNote();
				}
			}
		}

		[JsonIgnore]
		public (NoteResult result, NoteResultDescription description) JudgeInfo
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			protected set;
		}

		[JsonIgnore]
		public NoteResult Result
		{
			get
			{
				return JudgeInfo.result;
			}
		}

		[JsonIgnore]
		public NoteResultDescription Description
		{
			get
			{
				return JudgeInfo.description;
			}
		}

		public virtual NoteCategory Category
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		public NoteType Type
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		public NoteDirection Direction
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		public NoteLineType LineType
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		[JsonIgnore]
		public INote ParentNote
		{
			get
			{
				return parentNote;
			}
		}

		[JsonIgnore]
		public INote ChildNote
		{
			get
			{
				return childNote;
			}
		}

		[JsonIgnore]
		public INote PairNote
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			private set;
		}

		/// <summary>
		/// Whether the player is judged on this note. A note without judgement takes no press, never
		/// misses and publishes nothing: no effect, no sound, no text, no score, no combo. A type can turn
		/// it off (hidden points, guide bars, the rub carrier), and so can the chart's decoration flag:
		/// a decorative note is exactly as silent as a guide bar.
		/// </summary>
		[JsonIgnore]
		public virtual bool HasJudgment => !IsDecoration;

		/// <summary>
		/// Whether the note is drawn. Independent of <see cref="HasJudgment"/>: a guide bar and a
		/// decorative note are drawn and not judged; the hidden points are neither.
		/// </summary>
		[JsonIgnore]
		public virtual bool IsVisible => true;

		/// <summary>
		/// True when the note is drawn as let go. Only the appearance depends on this: a bar whose
		/// hold has lapsed is drawn grey while it still counts as held for another few frames, so
		/// the state machine and the picture do not have to agree.
		/// </summary>
		[JsonIgnore]
		public virtual bool ShowsAsReleased => State == NoteState.Release;

		public virtual bool IsSkip
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		public virtual bool IsDecoration
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		[JsonIgnore]
		public virtual int Id
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		public float speedRatio
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		/// <summary>Whether this note is walked by the high speed pass, decided when the chart was bound.</summary>
		internal bool IsHighSpeedNote { get; set; }

		/// <summary>
		/// The note's place in the bound chart, set by <see cref="Sekai.Core.Live.LiveNoteSession.Bind"/>.
		/// It breaks ties between press candidates at the same time, so the winner does not depend on
		/// the order the frame happened to register them in.
		/// </summary>
		internal virtual int ChartOrder { get; set; }

		/// <summary>
		/// 标记音符是否使用负流速渲染（从屏幕下方飞入）
		/// </summary>
		[JsonIgnore]
		public bool IsNegativeSpeed { get; set; }

		public float offsetTime
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		public List<NoteBase> NoteList
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		[JsonIgnore]
		public List<INote> ViewNoteList
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		public NoteBase()
		{
		}

		public NoteBase(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, float speedRatio, float laneOffset, NoteType type = NoteType.Default)
		{
			MusicScoreInfo = info;
			Id = id;
			DefaultLeftLane = laneStart;
			DefaultRightLane = laneEnd;
			LaneStart = laneStart;
			LaneEnd = laneEnd;
			LaneStartF = laneStart;
			LaneEndF = laneEnd;
			OpenSekai.Shared.Scoring.JudgeTuning.WidenLaneRange(laneStart, laneEnd, laneOffset, out float judgeStart, out float judgeEnd);
			JudgeLaneStart = judgeStart;
			JudgeLaneEnd = judgeEnd;
			State = NoteState.Wait;
			Category = category;
			Type = type;
			this.speedRatio = speedRatio;
			NoteList = new List<NoteBase> { this };
			ViewNoteList = new List<INote> { this };
		}

		public void SetStateUnnotice(NoteState state)
		{
			this.state = state;
		}

		/// <summary>
		/// Set JudgeInfo for decoration notes to enable effect playback
		/// </summary>
		/// <summary>
		/// Finishes this note with an explicit result. Used by a long note that has to score its tail
		/// itself: the tail is a separate note and carries its own judgment.
		/// </summary>
		internal void FinishWith(NoteResult result)
		{
			if (State == NoteState.Done) return;
			JudgeInfo = (result, NoteResultDescription.None);
			State = NoteState.Done;
		}

		public void SetJudgeInfoForDecoration(NoteResult result)
		{
			JudgeInfo = (result, NoteResultDescription.None);
		}

/// <summary>
		/// Why this note hangs under its parent. Only the bar's child note may end it, so the role is
		/// read back from the binding instead of being remembered: the pool resets a note after the
		/// chart was bound, and a remembered role was wiped to None by that reset (the bar then never
		/// learned that its tail finished).
		/// </summary>
		public NoteParentRole ParentRole
		{
			get
			{
				if (parentNote == null)
				{
					return NoteParentRole.None;
				}

				return ReferenceEquals(parentNote.ChildNote, this)
					? NoteParentRole.Tail
					: NoteParentRole.Connection;
			}
		}

		internal void BindGeometryParent(LongNote note) => parentNote = note;

		public virtual void SetParentNote(LongNote note)
		{
			parentNote = note;
			if (note.Type == NoteType.Critical)
			{
				Type = note.Type;
			}
		}

		/// <summary>
		/// Tells the parent this note just finished. A bar is only finished by its tail: a connection
		/// note or a combo marker finishing is its own business, and letting it reach the parent is how
		/// a bar used to end before it was ever held.
		/// </summary>
		protected void NotifyParentFinished()
		{
			if (parentNote == null || (ParentRole == NoteParentRole.Tail && LongNote.IsIndependentTail(this)))
			{
				return;
			}

			if (ParentRole != NoteParentRole.Tail)
			{
				if (LiveTrace.IsOn(LiveTraceCategory.Hold))
				{
					LiveTrace.Write(LiveTraceCategory.Hold, $"childDoneIgnored role={ParentRole} type={GetType().Name} state={State} result={Result}");
				}

				return;
			}

			parentNote.ForceTerminate(this);
		}

		public void SetChildNote(NoteBase note)
		{
			childNote = note;
		}

		public void SetPairNote(NoteBase note)
		{
			PairNote = note;
		}

		public virtual void SetSkip(bool skip)
		{
			IsSkip = skip;
		}

		public virtual void SetDecoration(bool decoration)
		{
			IsDecoration = decoration;
		}

		public virtual void Excute(MusicScoreInfo currentFrameInfo, float offsetTime) =>
			AdvanceNote(currentFrameInfo, offsetTime, settleTimeout: true);

		internal void AdvanceNote(MusicScoreInfo currentFrameInfo, float offsetTime, bool settleTimeout)
		{
			this.offsetTime = offsetTime;
			if (State == NoteState.Done)
			{
				return;
			}

			if (settleTimeout && State >= NoteState.Last)
			{
				JudgeInfo = (NoteResult.Miss, NoteResultDescription.None);
				State = NoteState.Done;
				return;
			}

			// The cached offset is a display convenience, refreshed here and read by the views. The
			// judgement below uses the value computed in this call, so a note that has never been
			// executed (a tail note, say) cannot be graded on a stale zero.
			float judgeOffset = JudgeOffsetAt(currentFrameInfo.time);
			OffsetJudgeTime = judgeOffset;
			float progress = CalcProgress(currentFrameInfo, offsetTime);
			if (progress < 0f && state != NoteState.Scrolling)
			{
				return;
			}

			if (State == NoteState.Wait)
			{
				State = NoteState.Scrolling;
			}

			Progress = progress;
			// A note without judgement never misses: it is retired silently when its time comes.
			if (settleTimeout && HasJudgment
				&& LiveConfig.IsPastJudgeWindow(LiveUtility.GetINoteToJudgeFrameType(this), -judgeOffset))
			{
				State = NoteState.Last;
			}
		}

		protected float CalcProgress(MusicScoreInfo currentFrameInfo, float offsetTime)
		{
			if (offsetTime.Equals(0f))
			{
				return 1f;
			}

			return (currentFrameInfo.time + offsetTime - MusicScoreInfo.time) / offsetTime;
		}

		public virtual void Terminate()
		{
		}

		/// <summary>
		public virtual bool Judgment(ref LiveTouch touch, float lane)
		{
			return true;
		}

		protected virtual void OnSpawnNote()
		{
			if (parentNote != null && !parentNote.ViewNoteList.Contains(this)) return;

			// A note handed out by the pool must be clean. The bug this catches is expensive to find
			// otherwise: a deferred swipe left on a bar from the previous run finished the first bar
			// of the next run a quarter of a second before its own time.
			if (LiveTrace.IsOn(LiveTraceCategory.Judge))
				LiveTrace.Write(LiveTraceCategory.Judge, string.Concat("spawn id=", Id, " type=", GetType().Name, " t=", MusicScoreInfo.time.ToString("F3"), " state=", State, " deco=", IsDecoration));

			if ((State != NoteState.Wait || Result != NoteResult.None) && LiveTrace.Enabled)
				LiveTrace.Invariant(false,
					$"pool handed out a dirty note type={GetType().Name} state={State} result={Result} offset={OffsetJudgeTime:F3}");

			onSpawn?.Invoke(this);
		}

		/// <summary>Removes only the view. Result publication belongs to the Done transition.</summary>
		internal void RemoveView()
		{
			if (viewReleased) return;
			viewReleased = true;
			onUnspawn?.Invoke(this);
		}

		/// <summary>Advances the view of a retiring key with the music clock, without judging it.</summary>
		internal void TickRetire(float currentTime)
		{
			if (offsetTime.Equals(0f))
			{
				return;
			}

			Progress = (currentTime + offsetTime - MusicScoreInfo.time) / offsetTime;
		}

		// Done is accepted only once, before notifying the parent. Reentrant completion and
		// repeated cleanup therefore cannot publish a second result. No view flag is involved.
		protected virtual void OnUnSpawnNote()
		{
			PublishResult();

			// A bar's ending key is the note's own view, so the note releases it when the note is done -
			// that is the "it has a result" half of the rule (the other half, "its own time has arrived",
			// is the presentation pass). Tail keys used to be excluded here because the bar owned that
			// key; keeping the exclusion left every drawn end key with no owner once its bar was gone,
			// which is why only a hidden end (no key at all) seemed to disappear promptly.
			// An end that is not drawn has no key to release.
			if (ParentRole != NoteParentRole.Tail || IsVisible) RemoveView();
		}

		/// <summary>Publishes this note's verdict as a key: judgement, combo, score, damage.</summary>
		protected virtual void PublishResult() =>
			ResultPublication.Publish(this, Callbacks, this, PublishShape.Key);

		/// <summary>This note's four result callbacks, for <see cref="ResultPublication"/>.</summary>
		internal ResultCallbacks Callbacks => new ResultCallbacks(onJudgment, onUpdateCombo, onUpdateScore, onDamage);

		private bool resultPublished;

		/// <summary>Whether this judge point already published its verdict in this take.</summary>
		internal bool HasPublishedResult => resultPublished;

		/// <summary>
		/// Marks this point published. Only <see cref="ResultPublication"/> and a point that has nothing to
		/// say (a carrier's head) call it; <see cref="ResetNote"/> clears it.
		/// </summary>
		internal bool ClaimPublication()
		{
			if (resultPublished)
			{
				return false;
			}

			resultPublished = true;
			return true;
		}

		public virtual float LaneDistance(ref MusicScoreInfo currentFrameInfo, float inputLane)
		{
			float centerLane = (LaneStartF + LaneEndF) * 0.5f;
			float distance = centerLane - inputLane;
			return distance * distance;
		}

		public virtual NoteResult CalcNoteResult(float musicTime)
		{
			return NoteResult.None;
		}

		public virtual bool IsJudgment(ref LiveTouch touch, float lane)
		{
			return true;
		}

		public virtual bool AutoJudgment(MusicScoreInfo currentFrameInfo)
		{
			if (State == NoteState.Done || MusicScoreInfo.time > currentFrameInfo.time)
			{
				return false;
			}

			JudgeInfo = (NoteResult.Auto, NoteResultDescription.None);
			State = NoteState.Done;
			return true;
		}

		public virtual void ResetNote()
		{
			State = NoteState.Wait;
			OffsetJudgeTime = 0f;
			Progress = 0f;
			viewReleased = false;
			resultPublished = false;
			JudgeInfo = (NoteResult.None, NoteResultDescription.None);
			offsetTime = 0f;
			IsHighSpeedNote = false;
		}
	}
}
