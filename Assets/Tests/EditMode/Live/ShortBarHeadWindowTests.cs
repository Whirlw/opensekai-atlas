using NUnit.Framework;
using Sekai;
using Sekai.Live;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// A bar shorter than its own head's window. A one-tick bar - what every chart that breaks in the reports
	/// has - is past its tail window about six milliseconds in, so it finished, went back to the pool and took
	/// the head's remaining window with it: a press that landed after that had nothing left to answer. The bar
	/// now waits for its head's verdict, which is the same window every other head gets.
	/// </summary>
	public sealed class ShortBarHeadWindowTests
	{
		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2f, time, 120, 4, 1, 1);

		private static LongNote OneTickBar()
		{
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			// The end note one tick later: two milliseconds of bar.
			var end = new HiddenConnectionNote(Info(0.002f), 2, 4, 5, NoteCategory.Hidden, NoteType.Default, 1f, 0f);
			bar.AddNoteListAndSetupChildNote(end);
			bar.SetStateUnnotice(NoteState.Scrolling);
			return bar;
		}

		private static LiveTouch Press(float musicTime) => new LiveTouch
		{
			fingerId = 1,
			touchId = 1,
			musicTime = musicTime,
			phase = TouchPhase.Began,
			worldPosition = LiveConfig.JudgmentPositions[5],
			delta = Vector2.zero
		};

		[Test]
		public void AOneTickBarDoesNotLeaveBeforeItsHeadHasAVerdict()
		{
			LongNote bar = OneTickBar();

			Assert.That(bar.IsPastTailWindow(0.05f), Is.True, "its body is long over");
			Assert.That(bar.ExpireTail(0.05f), Is.False, "but its head still has a window to answer in");
			Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done));
		}

		[Test]
		public void APressInsideTheHeadWindowOfAOneTickBarIsJudged()
		{
			LongNote bar = OneTickBar();

			LiveTouch press = Press(0.05f);
			Assert.That(bar.Head.Candidate.IsJudgment(ref press, 5f), Is.True, "the press is inside the head's window");
			Assert.That(bar.Head.Candidate.Judgment(ref press, 5f), Is.True);

			Assert.That(bar.Head.Result, Is.Not.EqualTo(NoteResult.None), "the head keeps its own verdict");
		}

		[Test]
		public void AOneTickBarStillLeavesOnceTheHeadWindowHasClosed()
		{
			LongNote bar = OneTickBar();
			bar.Head.SettleExpiredHead(LiveConfig.JudgeTime + 0.01f);

			Assert.That(bar.Head.Result, Is.EqualTo(NoteResult.Miss), "an untouched head misses on its own clock");
			Assert.That(bar.ExpireTail(LiveConfig.JudgeTime + 0.01f), Is.True, "and then the bar leaves");
		}
	}
}
