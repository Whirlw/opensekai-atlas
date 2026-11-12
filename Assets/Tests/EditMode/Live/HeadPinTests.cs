using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenSekai.Charts.Serialization;
using OpenSekai.Live.Conversion;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// A bar's body starts at the judgement line once the head's time has passed - but only where
	/// something is really standing there. An ordinary bar waits to be taken, and a bar being held is
	/// being held; a rub or a guide bar that nobody took has nothing to pin, so it must keep descending
	/// past the line instead of freezing on it.
	/// </summary>
	public sealed class HeadPinTests
	{
		private LiveHarness _harness;
		private MusicScore _score;
		private LongNote _ordinary;
		private LongNote _friction;

		[SetUp]
		public void SetUp()
		{
			_harness = new LiveHarness();
			var document = ChartJson.Read(File.ReadAllText("Tools/fixtures/deco-tail-test10/score.json"));
			_score = RuntimeChartConverter.Convert(document, Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData"));
			var bars = _score.NoteArray.OfType<LongNote>().Where(n => n.HasJudgment).ToArray();
			_ordinary = bars.First(n => n.IsOrdinaryHead);
			_friction = bars.First(n => n is FrictionLongNote && n.ChildNote is FrictionNote);
			_harness.Arrange(_score, new NoteBase[] { _ordinary, _friction });
		}

		[TearDown]
		public void TearDown()
		{
			_harness.Dispose();
		}

		[Test]
		public void AnUntakenOrdinaryBarKeepsItsHeadOnTheLine()
		{
			// The plain bar is where the body is supposed to shrink from, taken or not.
			Assert.That(_ordinary.IsOrdinaryHead, Is.True);
			Assert.That(_ordinary.KeepsHeadOnTheLine, Is.True);
		}

		[Test]
		public void AnUntakenFrictionBarKeepsItsHeadOnTheLine()
		{
			// A rub bar is held like any other bar, so its head parks on the line too. It used to keep
			// descending because the pin was tied to IsOrdinaryHead, which also answers "who may be
			// judged" - the head looked like an unpressed key sliding off the line.
			Assert.That(_friction.IsOrdinaryHead, Is.False);
			Assert.That(_friction.KeepsHeadOnTheLine, Is.True);
		}

		[Test]
		public void AGuideBarNeverKeepsItsHeadOnTheLine()
		{
			// A guide bar is not played at all (only the note at its end is), so its body has nothing to
			// stand on either.
			LongNote guide = _score.NoteArray.OfType<GuideNote>().First();

			Assert.That(guide.IsOrdinaryHead, Is.False);
			Assert.That(guide.KeepsHeadOnTheLine, Is.False);
		}
	}
}
