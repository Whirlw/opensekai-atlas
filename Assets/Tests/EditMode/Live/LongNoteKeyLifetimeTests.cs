using NUnit.Framework;
using Sekai;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// A bar's key stands on the judgement line after its head is taken - that is what the pin is for. It
	/// must not outlive the note: the player's rule is that the key is gone when the bar is done, or when its
	/// end note's own time arrives, whichever comes first. Only a hidden end was ever prompt, because it drew
	/// no key at all.
	/// </summary>
	public sealed class LongNoteKeyLifetimeTests
	{
		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2f, time, 120, 4, 1, 1);

		private GameObject _host;
		private LongNoteView _view;

		[SetUp]
		public void SetUp()
		{
			_host = new GameObject("long note key");
			_view = _host.AddComponent<LongNoteView>();
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(_host);
		}

		private LongNote LiveBar()
		{
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			var tail = new FrictionNote(Info(1f), 2, 4, 5, NoteCategory.Friction, null, NoteType.Default, 1f);
			bar.AddNoteListAndSetupChildNote(tail);
			// A note only has a progress once it is ticked; the view hides anything at or below zero.
			bar.Excute(Info(0.5f), 0.5f);
			bar.SetStateUnnotice(NoteState.Input);
			return bar;
		}

		[Test]
		public void TheKeyStandsOnTheLineUntilTheEndNoteIsDue()
		{
			LongNote bar = LiveBar();
			_view.Spawn(bar, 0f);

			_view.Move(0.5f);

			Assert.That(_view.transform.localScale.x, Is.GreaterThan(0f), "the key is on the line while the bar is alive");
		}

		[Test]
		public void TheKeyIsTakenOffWhenTheEndNoteIsDue()
		{
			LongNote bar = LiveBar();
			_view.Spawn(bar, 0f);

			// The bar is still `Input` - nothing has ended it - and the end note's own time has arrived. The
			// key used to stay pinned on the line with the body from then on.
			_view.Move(bar.ChildNote.MusicScoreInfo.time);

			Assert.That(_view.transform.localScale, Is.EqualTo(Vector3.zero), "an ordinary key does not outlive its end note");
		}

		[Test]
		public void TheKeyIsTakenOffWhenTheBarIsDone()
		{
			LongNote bar = LiveBar();
			_view.Spawn(bar, 0f);
			bar.SetStateUnnotice(NoteState.Done);

			_view.Move(0.2f);

			Assert.That(_view.transform.localScale, Is.EqualTo(Vector3.zero), "a finished bar keeps no key");
		}

		[Test]
		public void ARubHeadsKeyIsTakenOffWithItsVerdict()
		{
			// A rub head lives like a standalone friction note: the key goes on the frame it is judged,
			// while the body still has a second to run.
			var host = new GameObject("rub key");
			try
			{
				var view = host.AddComponent<FrictionLongNoteView>();
				var bar = new FrictionLongNote(Info(0f), 3, 4, 5, NoteCategory.FrictionLong, NoteType.Default, null, 1f);
				bar.AddNoteListAndSetupChildNote(new FrictionNote(Info(1f), 4, 4, 5, NoteCategory.Friction, null, NoteType.Default, 1f));
				bar.Excute(Info(0f), 0.5f);
				view.Spawn(bar, 0f);

				view.Move(0f);
				Assert.That(view.transform.localScale.x, Is.GreaterThan(0f), "the key is drawn before its verdict");

				bar.Head.Settle((NoteResult.JustPerfect, NoteResultDescription.None));
				view.Move(0f);
				Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done));
				Assert.That(view.transform.localScale, Is.EqualTo(Vector3.zero), "the judged rub key is gone");
			}
			finally
			{
				Object.DestroyImmediate(host);
			}
		}
	}
}
