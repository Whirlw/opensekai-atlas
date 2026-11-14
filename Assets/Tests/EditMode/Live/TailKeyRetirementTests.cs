using System.Collections.Generic;
using NUnit.Framework;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// A bar's ending key belongs to the bar until it is judged, and after that it belongs to nobody: the
	/// bridge has to retire it. Tracking only the keys that happened to be in the "following" phase when
	/// their view was built left the others with no owner at all - a tail's Done never removes its own view
	/// - so a caught friction end stayed pinned on the judgement line ("贴图粘在判定线上").
	/// </summary>
	public sealed class TailKeyRetirementTests
	{
		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2f, time, 120, 4, 1, 1);

		private sealed class Recorder : LiveViewBase
		{
			internal readonly List<NoteBase> Spawned = new List<NoteBase>();
			internal readonly List<NoteBase> Unspawned = new List<NoteBase>();

			public override void SpawnNote(NoteBase note) => Spawned.Add(note);

			public override void UnspawnNote(NoteBase note) => Unspawned.Add(note);
		}

		private GameObject _host;
		private Recorder _view;

		[SetUp]
		public void SetUp()
		{
			_host = new GameObject("tail key observer");
			_view = _host.AddComponent<Recorder>();
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(_host);
		}

		[Test]
		public void AJudgedTailReleasesItsOwnKeyWhenItIsDone()
		{
			// The "unbinding" step: a tail's Done used to skip RemoveView ("the bar owns that key"), so the
			// only thing that could ever retire the key was the bridge's registry - and anything outside that
			// registry kept its sprite forever. A drawn tail now releases its own view the moment it is done,
			// exactly like a standalone note; no bridge pass is involved in this test.
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			var tail = new FrictionFlickNote(Info(1f), 2, 4, 5, NoteCategory.FrictionFlick, 1f,
				NoteType.Default, NoteDirection.Default, 0.0833f, 0f);
			bar.AddNoteListAndSetupChildNote(tail);
			var views = new LiveViewBase[] { _view };
			tail.OnUnspawn = note => LiveViewExt.UnspawnNote(views, note);

			tail.FinishWith(NoteResult.JustPerfect);

			Assert.That(_view.Unspawned, Does.Contain(tail), "a judged end key releases its own view");
		}

		[Test]
		public void ARetryStartsWithNoOpenEndNotes()
		{
			// Every take starts clean: the session's tick lists belong to the take that is running. Carrying
			// them over meant a retry ticked a note it had never scanned, before that note's own bar was on
			// screen - the friction-flick end vanishing on a restart.
			var session = new LiveNoteSession();
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			var tail = new FrictionFlickNote(Info(1f), 2, 4, 5, NoteCategory.FrictionFlick, 1f,
				NoteType.Default, NoteDirection.Default, 0.0833f, 0f);
			bar.AddNoteListAndSetupChildNote(tail);
			session.RegisterTail(tail);

			Assert.That(session.PendingTailCount, Is.EqualTo(1));

			session.Reset();

			Assert.That(session.PendingTailCount, Is.Zero, "a retry starts with no open end notes");
		}

		[Test]
		public void ATailKeyIsRetiredWhateverPhaseItWasBuiltIn()
		{
			var bridge = new LivePresentationBridge();
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			var tail = new NormalNote(Info(1f), 2, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f);
			bar.AddNoteListAndSetupChildNote(tail);

			// The view is built while the bar is still far from its end, which is not the "following" phase.
			var views = new LiveViewBase[] { _view };
			// Production binds these in BindNoteCallbacks; the bridge retires a key by asking the note to
			// release its view, so the same chain has to exist here.
			tail.OnSpawn = note => LiveViewExt.SpawnNote(views, note);
			tail.OnUnspawn = note => LiveViewExt.UnspawnNote(views, note);
			bridge.Spawn(views, tail, bar.MusicScoreInfo.time);

			Assert.That(_view.Spawned, Does.Contain(tail), "the key was drawn");

			// The bar is done, which is one of the two conditions, whatever the note's own state is.
			tail.FinishWith(NoteResult.JustPerfect);
			bridge.TickTailKeys(bar.MusicScoreInfo.time);

			Assert.That(_view.Unspawned, Does.Contain(tail), "a settled end key leaves the line");
		}

		[Test]
		public void TheEndKeyStaysVisibleAtItsOwnTimeUntilItSettles()
		{
			var bridge = new LivePresentationBridge();
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			var tail = new NormalNote(Info(1f), 2, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f);
			bar.AddNoteListAndSetupChildNote(tail);
			var views = new LiveViewBase[] { _view };
			tail.OnSpawn = note => LiveViewExt.SpawnNote(views, note);
			tail.OnUnspawn = note => LiveViewExt.UnspawnNote(views, note);
			bridge.Spawn(views, tail, bar.MusicScoreInfo.time);
			bar.SetStateUnnotice(NoteState.Scrolling);

			bridge.TickTailKeys(0.5f);
			Assert.That(_view.Unspawned, Is.Empty, "the bar is alive and the key is not due yet, so it stays");

			bridge.TickTailKeys(tail.MusicScoreInfo.time);
			Assert.That(_view.Unspawned, Is.Empty);
			tail.FinishWith(NoteResult.Miss);
			Assert.That(_view.Unspawned, Does.Contain(tail));
		}

		[Test]
		public void TheEndKeyOutlivesItsBar()
		{
			var bridge = new LivePresentationBridge();
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			var tail = new NormalNote(Info(1f), 2, 4, 5, NoteCategory.Normal, 1f, NoteType.Default, 0f);
			bar.AddNoteListAndSetupChildNote(tail);
			var views = new LiveViewBase[] { _view };
			tail.OnSpawn = note => LiveViewExt.SpawnNote(views, note);
			tail.OnUnspawn = note => LiveViewExt.UnspawnNote(views, note);
			bridge.Spawn(views, tail, bar.MusicScoreInfo.time);
			bar.RecordHoldingInput(0.5f);

			Assert.That(bar.ExpireTail(0.6f), Is.False, "the bar is still inside its own window");
			Assert.That(_view.Unspawned, Is.Empty, "so the key stays");

			bar.SetStateUnnotice(NoteState.Done);
			bridge.TickTailKeys(0.7f);

			Assert.That(_view.Unspawned, Is.Empty);
			Assert.That(bar.JudgeTailAsRelease(1.02f), Is.True);
			Assert.That(_view.Unspawned, Does.Contain(tail));
		}
	}
}
