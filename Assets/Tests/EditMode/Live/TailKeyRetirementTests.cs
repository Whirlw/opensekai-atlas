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
		public void ATailKeyIsRetiredWhateverPhaseItWasBuiltIn()
		{
			var bridge = new LivePresentationBridge();
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			var tail = new FrictionNote(Info(1f), 2, 4, 5, NoteCategory.Friction, null, NoteType.Default, 1f);
			bar.AddNoteListAndSetupChildNote(tail);

			// The view is built while the bar is still far from its end, which is not the "following" phase.
			var views = new LiveViewBase[] { _view };
			// Production binds these in BindNoteCallbacks; the bridge retires a key by asking the note to
			// release its view, so the same chain has to exist here.
			tail.OnSpawn = note => LiveViewExt.SpawnNote(views, note);
			tail.OnUnspawn = note => LiveViewExt.UnspawnNote(views, note);
			bridge.Spawn(views, tail, bar.MusicScoreInfo.time);

			Assert.That(_view.Spawned, Does.Contain(tail), "the key was drawn");

			tail.FinishWith(NoteResult.JustPerfect);
			bridge.TickTailKeys(bar.MusicScoreInfo.time + 1f);

			Assert.That(_view.Unspawned, Does.Contain(tail), "a settled end key leaves the line");
		}
	}
}
