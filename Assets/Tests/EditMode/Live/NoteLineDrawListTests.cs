using NUnit.Framework;

using Sekai;
using Sekai.Live;

using UnityEngine;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// A bar's body is drawn from the list a view keeps, one mesh per entry. The scan asks for a note's
	/// view every frame of its preload margin - a spawn request has to be repeatable - so the list has to
	/// be the thing that stays unique. A second entry is a second identical mesh on top of the first:
	/// the bar looks brighter, and the mesh budget it eats is what made DEINOS stutter.
	///
	/// Measured before the fix (deinos-phainein, autoplay, 10068 frames): one note received up to 342
	/// spawn requests, and the bars whose category has no note pool (friction-hide bars, guide bars)
	/// never reach the manager's dictionary, so every one of those requests added another entry.
	/// </summary>
	public sealed class NoteLineDrawListTests
	{
		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);

		private static LongNote Bar(float time) =>
			new LongNote(Info(time), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);

		private static GuideNote Guide(float time) =>
			new GuideNote(Info(time), 2, 4, 5, NoteCategory.Guide, NoteType.Default, null, 1f);

		private GameObject _host;
		private NoteLineView _view;

		[SetUp]
		public void SetUp()
		{
			_host = new GameObject("note-line-view");
			_view = _host.AddComponent<NoteLineView>();
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(_host);
		}

		[Test]
		public void ASpawnRequestedEveryFrameStillDrawsOneBar()
		{
			LongNote bar = Bar(1f);
			for (int i = 0; i < 342; i++)
			{
				_view.Add(bar);
			}

			Assert.That(_view.BarCount, Is.EqualTo(1), "a bar that asked 342 times is still one bar");

			_view.Remove(bar);
			Assert.That(_view.BarCount, Is.Zero, "and it leaves as one bar");

			_view.Add(bar);
			Assert.That(_view.BarCount, Is.EqualTo(1), "a bar that comes back is drawn again");
		}

		[Test]
		public void AGuideBarAskedEveryFrameStillDrawsOneLine()
		{
			GuideNote guide = Guide(1f);
			for (int i = 0; i < 174; i++)
			{
				_view.Add(guide);
			}

			Assert.That(_view.BarCount, Is.EqualTo(1), "a guide bar is one line, not a stack of them");
		}

		[Test]
		public void TwoDifferentBarsAreBothDrawn()
		{
			_view.Add(Bar(1f));
			_view.Add(Bar(2f));

			Assert.That(_view.BarCount, Is.EqualTo(2), "uniqueness must not merge distinct bars");
		}
	}
}
