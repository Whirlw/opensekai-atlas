using UnityEngine;
using OpenSekai.Shared.Live;
using Sekai.Live.Tails;

namespace Sekai.Live
{
	public partial class LongNote
	{
		/// <summary>
		/// The note that ends this bar, as the chart bound it (an ordinary note, a flick, a rub, a hidden
		/// marker). The behaviour is chosen from this note and the tail pass grades this note, so the two
		/// questions always answer the same object.
		/// 
		/// It is not "the last child in the list": a hidden interior point appended after the end must not
		/// steal the tail (TailBehaviourTests.AHiddenMarkerAfterTheTailDoesNotStealIt), and it is not "the
		/// latest by time" either - what matters is which note the bar was told is its end.
		/// </summary>
		internal NoteBase TailNote => Tail.EndNote ?? childNote;

		internal static bool IsIndependentTail(NoteBase note) =>
			note is FrictionFlickNote || note is FrictionNote and not FrictionHideNote;

		internal bool HasIndependentTail => IsIndependentTail(TailNote);

		internal bool HasManagedTail => TailNote is { HasJudgment: true, IsDecoration: false }
			&& (TailBehaviour.AcceptsRelease || TailBehaviour.AcceptsSwipe)
			&& IsTailCandidate(TailNote);

		internal bool HasPendingManagedTail => HasManagedTail && TailNote.State != NoteState.Done;

		internal bool CanReceiveTailSwipe => Tail.CanReceiveSwipe;

		/// <summary>
		/// How this bar is meant to end, derived from the note that ends it. Deliberately not cached:
		/// the chart is built once but bars are reset between runs, and any cached copy of this answer
		/// is wiped by that reset while the binding survives it.
		/// </summary>
		internal ITailBehaviour TailBehaviour => SkipsTailJudgement
			? SilentTail.Instance
			: TailNote == null ? ReleaseTail.Instance : TailBehaviours.For(TailNote);

		/// <summary>Whether the final chart point ends the bar without its own judgement.</summary>
		internal bool EndsWithoutJudgement => SkipsTailJudgement || (_lastBoundNote != null && !IsTailCandidate(_lastBoundNote));

		internal bool CanFinishSilently(float currentTime)
		{
			// Allow a visible head to publish its result before ending a silent tail.
			if (PublishesHeadMiss && !Head.IsSettled)
			{
				return false;
			}

			return EndsWithoutJudgement && _lastBoundNote != null
				&& currentTime > _lastBoundNote.MusicScoreInfo.time;
		}

		internal void FinishSilently()
		{
			if (State == NoteState.Done)
			{
				return;
			}

			// CanFinishSilently already waits for a visible head, but that wait is about the head's own
			// window: the head settles itself and is not touched here.
			_tail?.ResetDecision();
			// The leftover interior points are settled by the Done transition itself (see State), so this
			// path and every other ending agree without each one remembering to do it.
			State = NoteState.Done;
		}

		private LongNoteTail _tail;
		private LongNoteTail Tail => _tail ??= new LongNoteTail(this);

		internal static bool IsTailCandidate(NoteBase note) => LongNoteTail.IsTailCandidate(note);
		internal bool TryGetTail(out NoteBase tail) => Tail.TryGetTail(out tail);
		internal bool IsTailJudgeableAt(float currentTime) => Tail.IsTailJudgeableAt(currentTime);
		internal bool IsTailWindowOpenAt(float currentTime) => Tail.IsTailWindowOpenAt(currentTime);
		internal bool IsPastTailWindow(float currentTime) => Tail.IsPastTailWindow(currentTime);
		internal bool ExpireTail(float currentTime) => Tail.ExpireTail(currentTime);
		internal bool JudgeTailAsRelease(float currentTime) => Tail.JudgeTailAsRelease(currentTime);
		internal bool DetachStandaloneTail(float currentTime) => Tail.DetachStandaloneTail(currentTime);
		internal bool NotifyLaneSwipe(float magnitude, float currentTime) => Tail.NotifyLaneSwipe(magnitude, currentTime);
		internal bool HasDeferredSwipe => _tail?.HasDeferredSwipe ?? false;
		internal void MarkTailPressed() => Tail.MarkTailPressed();
		internal void NotifyLaneRelease() => _tail?.NotifyLaneRelease();
		internal bool TickSwipeTail(float currentTime) => Tail.TickSwipeTail(currentTime);
		internal bool FailTail() => Tail.FailTail();
		internal int? DeferredSwipeTouchId => _tail?.DeferredSwipeTouchId;
		internal bool TrySwipeTail(Vector2 delta, float deltaTime, float currentTime, int? touchId = null,
			bool fromTailLane = false) =>
			Tail.TrySwipeTail(delta, deltaTime, currentTime, touchId, fromTailLane);
		internal bool AcceptsSwipeFromTailLane(float lane, float currentTime) =>
			Tail.AcceptsSwipeFromTailLane(lane, currentTime);

		internal void SetTailResult(NoteResult result) => JudgeInfo = (result, NoteResultDescription.None);
		internal void CompleteTail() => State = NoteState.Done;

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
	}
}
