using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OpenSekai.Shared.Live;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// The grace a bar gets when the finger leaves it, and the fact that "held" is the touch id, not a state.
	/// These are the contract the hold loop has to satisfy; the implementation is expected to fail them
	/// until the four rows in docs/operations/judgement-input-split.md section 11.4 land.
	/// </summary>
	public class LongNoteGraceTests
	{
		

		[SetUp]
		public void SetUp()
		{
		}

		[TearDown]
		public void TearDown()
		{
			
		}

		internal static void AssertHeldBody(LongNote bar, string message = null)
		{
			Assert.That(bar.HasFinger, Is.True, message);
			Assert.That(bar.ShowsAsReleased, Is.False, message);
			Assert.That(bar.State, Is.Not.EqualTo(NoteState.InputBegan).And.Not.EqualTo(NoteState.Input), message);
		}

		private static MusicScoreInfo Info(float time)
		{
			return new MusicScoreInfo(0, time / 2f, time, 120, 4, 1, 1);
		}

		private static LiveLogic.InputTmp Input(int finger, float lane)
		{
			return new LiveLogic.InputTmp
			{
				Lane = lane,
				Touch = new LiveTouch
				{
					fingerId = finger,
					touchId = finger,
					musicTime = 0f,
					phase = TouchPhase.Stationary
				}
			};
		}

		private static T Field<T>(LiveLogic logic, string name)
		{
			return (T)typeof(LiveLogic).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(logic);
		}

		private static void Stage(LiveLogic logic, string name)
		{
			typeof(LiveLogic).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(logic, null);
		}

		/// <summary>A bar of lanes 4..5 that is already being held by <paramref name="finger"/>.</summary>
		private static LongNote HeldBar(int finger)
		{
			var bar = new LongNote(Info(0f), finger, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			bar.SetStateUnnotice(NoteState.Scrolling);
			bar.RecordHoldingInput(finger, 0f);
			return bar;
		}

		[Test]
		public void ALostFingerGreysTheBarAndReleasesThreeFramesLater()
		{
			var logic = new LiveLogic(null);
			var bar = HeldBar(1);
			Field<List<NoteBase>>(logic, "_judgmentNoteList").Add(bar);
			// A finger sits somewhere else entirely: lanes 9..10 are not the bar's 4..5.
			Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList").Add(Input(2, 9f));

			bar.Excute(Info(1f), 0f);

			Assert.That(bar.State, Is.EqualTo(NoteState.Last),
				"the frame the finger goes away the bar greys, it does not vanish");

			for (int frame = 0; frame < 4; frame++)
			{
				bar.Excute(Info(1f + frame * 0.001f), 0f);
			}

			Assert.That(bar.State, Is.EqualTo(NoteState.Release),
				"after the grace the bar releases without a judgement of its own");
		}

		[Test]
		public void AFingerReturningInsideTheGraceTakesTheBarBack()
		{
			var logic = new LiveLogic(null);
			var bar = HeldBar(1);
			Field<List<NoteBase>>(logic, "_judgmentNoteList").Add(bar);
			var inputs = Field<List<LiveLogic.InputTmp>>(logic, "_activeInputList");

			bar.Excute(Info(1f), 0f);
			Assert.That(bar.State, Is.EqualTo(NoteState.Last), "precondition: the bar is in its grace");

			inputs.Add(Input(3, 4.5f));
			Stage(logic, "RefreshSustainedHolds");

			LongNoteGraceTests.AssertHeldBody(bar,
				"a finger that arrives inside the grace picks the bar back up");
		}

		[Test]
		public void HasFingerFollowsTheTouchIdOnly()
		{
			var bar = HeldBar(1);

			Assert.That(bar.HasFinger, Is.True, "a held bar has a finger by definition");
			Assert.That(bar.IsHeld(0f), Is.True, "and IsHeld is just HasFinger now");

			typeof(LongNote).GetField("_holdingTouchId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bar, -1);

			Assert.That(bar.HasFinger, Is.False, "no touch id means no finger, whatever the state says");
			Assert.That(bar.IsHeld(0f), Is.False, "the time argument no longer decides anything");
		}

		[Test]
		public void HeldBodyNeverEntersAnInputPhase()
		{
			var bar = new LongNote(Info(0f), 1, 4, 5, NoteCategory.Long, NoteType.Default, 1f, 0f);
			bar.SetStateUnnotice(NoteState.Scrolling);
			var touch = Input(1, 4.5f).Touch;
			for (int frame = 0; frame < 4; frame++)
			{
				bar.Judgment(ref touch, 4.5f);
				AssertHeldBody(bar);
			}
		}

		[Test]
		public void FingerPresenceTracksAssignmentAndReset()
		{
			var bar = HeldBar(1);
			foreach (int finger in new[] { 2, -1, 0 })
			{
				bar.RecordHoldingInput(finger, 0f);
				Assert.That(bar.HoldingTouchId, Is.EqualTo(finger));
				Assert.That(bar.HasFinger, Is.EqualTo(bar.HoldingTouchId >= 0));
			}
			bar.ResetNote();
			Assert.That(bar.HoldingTouchId, Is.EqualTo(-1));
			Assert.That(bar.HasFinger, Is.False);
		}
	}
}
