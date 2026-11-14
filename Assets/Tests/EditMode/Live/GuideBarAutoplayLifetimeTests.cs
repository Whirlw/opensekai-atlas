using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

using Sekai.Live;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// Auto 模式下装饰长条（GuideNote：谱面上画出来的引导线）的寿命。
	///
	/// 报告：装饰长条一碰到判定线就整条消失。它的头是引导线本身、不判定，尾巴在几帧到一秒之后；
	/// 自动演奏把「没有可判定的尾」直接当成「整条已完成」，于是头过线的那一帧 State 变 Done，
	/// 整条被回收。这里钉住的是它能被看见的区间：从它自己的头，到它自己的最后一个点。
	/// </summary>
	public sealed class GuideBarAutoplayLifetimeTests
	{
		private static MusicScoreInfo Info(float time) => new MusicScoreInfo(0, time / 2, time, 120, 4, 1, 1);

		/// <summary>A guide bar from <paramref name="head"/> to <paramref name="tail"/>, drawn the whole way.</summary>
		private static GuideNote Bar(float head, float tail)
		{
			var bar = new GuideNote(Info(head), 1, 4, 5, NoteCategory.Guide, NoteType.Default, null, 1f);
			bar.AddNoteListAndSetupChildNote(new GuideEndNote(Info(tail), 2, 4, 5, NoteCategory.Guide, null, NoteType.Default, 1f));
			return bar;
		}

		[Test]
		public void AGuideBarWithoutAPlayableEndIsStillDrawnAfterItsHeadCrossedTheLine()
		{
			GuideNote bar = Bar(0f, 2f);

			Assert.That(bar.AutoJudgment(Info(0.05f)), Is.False, "the head crossing the line does not end the bar");
			Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done), "the body is still on its way down");
			Assert.That(bar.IsAutoPlaying, Is.True, "and autoplay keeps it lit while it travels");

			Assert.That(bar.AutoJudgment(Info(1.9f)), Is.False, "the bar lives until its last point");

			Assert.That(bar.AutoJudgment(Info(2.01f)), Is.True, "the frame the last point passes it is finished");
			Assert.That(bar.State, Is.EqualTo(NoteState.Done));
		}

		[Test]
		public void NoBarEndsWhileItsBodyIsStillOnItsWayDown()
		{
			// The same question on real charts, one whole autoplay run each.
			//
			// Test16 (the reporter's chart, copied from the library as Tools/fixtures/test16) before the
			// fix: its two decorative bars were finished at 0.000 s - the frame their head stood on the
			// judgement line - while their tails were at 1.000 s, so the body was gone for a whole second.
			// After the fix they finish at 1.017 s, where their tails are.
			//
			// deinos-phainein before the fix: 25 bars finished on the frame their head reached the line,
			// e.g. head=101.694 done=101.707 tail=102.000.
			Assert.That(Offenders("test16"), Is.Empty);
			Assert.That(Offenders("deinos-phainein").Take(8), Is.Empty);
		}

		[Test]
		public void AGuideBarStaysLitAndWholeUntilItsLastPointWithNobodyPlaying()
		{
			// Report: in a normal (not autoplay) run a guide bar went Last - grey, and pulsing, since the
			// body animates every state after InputBegan - the moment its head reached the line. A guide
			// bar is never played: it is lit from its head to its last point, its body stops at the line.
			Assert.That(GuideOffenders("test16"), Is.Empty);
			Assert.That(GuideOffenders("deinos-phainein").Take(8), Is.Empty);
		}

		[Test]
		public void AGuideBarEndsAtItsLastPointWithNobodyPlaying()
		{
			// A guide bar takes no input, so it never joins the judgement list, and outside autoplay
			// nothing ended it: it stayed alive (and, before, Last) for the rest of the chart. It ends
			// after its last point - here a bend at 1 s and the end at 2 s - and is lit until then.
			var bar = new GuideNote(Info(0f), 1, 4, 5, NoteCategory.Guide, NoteType.Default, null, 1f);
			var bend = new GuideHiddenConnectionNote(Info(1f), 2, 6, 7, NoteCategory.GuideHidden, null, NoteType.Default, 1f);
			var end = new GuideEndNote(Info(2f), 3, 2, 3, NoteCategory.Guide, null, NoteType.Default, 1f);
			bar.AddNoteListAndSetupChildNote(bend);
			bar.AddNoteListAndSetupChildNote(end);

			var infos = new[] { Info(0f), Info(1f), Info(2f) };
			var score = new MusicScore(infos, System.Array.Empty<EventBase>(), new NoteBase[] { bar });
			float? goneAt = null;
			using (var harness = new LiveHarness())
			{
				harness.Arrange(score);
				const float frame = 1f / 60f;
				for (float now = 0f; now < 2.5f && goneAt == null; now += frame)
				{
					harness.OnUpdate(now, 10d + now);
					harness.OnInput();
					if (bar.State != NoteState.Done)
					{
						Assert.That(bar.State, Is.Not.EqualTo(NoteState.Last).And.Not.EqualTo(NoteState.Release), $"at {now:F3}s");
						Assert.That(bar.ShowsAsReleased, Is.False, $"lit at {now:F3}s");
					}

					if (bar.State == NoteState.Done)
					{
						goneAt = now;
					}
				}
			}

			Assert.That(goneAt, Is.Not.Null, "the bar ends after its last point");
			Assert.That(goneAt.Value, Is.InRange(2f, 2f + 0.05f), "and not before it");
		}

		private static List<string> GuideOffenders(string fixture)
		{
			MusicScore score = AutoplayCompletionTests.LoadFixture(fixture);
			var offenders = new List<string>();
			var reported = new HashSet<NoteBase>();
			using (var harness = new LiveHarness())
			{
				harness.Arrange(score);
				float end = score.NoteArray.Max(note => note.MusicScoreInfo.time) + 2f;
				const float frame = 1f / 60f;
				for (float now = 0f; now < end; now += frame)
				{
					harness.OnUpdate(now, 10d + now);
					foreach (GuideNote bar in score.NoteArray.OfType<GuideNote>())
					{
						if (reported.Contains(bar) || bar.NoteList == null || bar.NoteList.Count == 0)
						{
							continue;
						}

						float bodyEnd = bar.NoteList[bar.NoteList.Count - 1].MusicScoreInfo.time;
						if (bar.State == NoteState.Done && now + 0.02f < bodyEnd)
						{
							reported.Add(bar);
							offenders.Add($"{fixture}: guide id={bar.Id} gone at {now:F3}s, head={bar.MusicScoreInfo.time:F3}s, last point={bodyEnd:F3}s");
						}
						else if (bar.State != NoteState.Done && now >= bar.MusicScoreInfo.time
							&& (bar.ShowsAsReleased || bar.State == NoteState.Last || bar.State == NoteState.Release))
						{
							reported.Add(bar);
							offenders.Add($"{fixture}: guide id={bar.Id} state={bar.State} at {now:F3}s, head={bar.MusicScoreInfo.time:F3}s");
						}
					}
				}
			}

			foreach (GuideNote bar in score.NoteArray.OfType<GuideNote>())
			{
				if (bar.State != NoteState.Done && !reported.Contains(bar))
				{
					offenders.Add($"{fixture}: guide id={bar.Id} never ended, head={bar.MusicScoreInfo.time:F3}s");
				}
			}

			return offenders;
		}

		private static List<string> Offenders(string fixture)
		{
			MusicScore score = AutoplayCompletionTests.LoadFixture(fixture);
			var doneAt = new Dictionary<NoteBase, float>();
			using (var harness = new LiveHarness())
			{
				harness.Arrange(score);
				float end = score.NoteArray.Max(note => note.MusicScoreInfo.time) + 2f;
				const float frame = 1f / 60f;
				for (float now = 0f; now < end; now += frame)
				{
					harness.OnUpdate(now, 10d + now);
					harness.Logic.OnAutoInput();
					foreach (NoteBase note in score.NoteArray)
					{
						if (note != null && note.State == NoteState.Done && !doneAt.ContainsKey(note))
						{
							doneAt[note] = now;
						}
					}
				}
			}

			var offenders = new List<string>();
			foreach (LongNote bar in score.NoteArray.OfType<LongNote>())
			{
				if (bar.NoteList == null || bar.NoteList.Count == 0 || !doneAt.TryGetValue(bar, out float done))
				{
					continue;
				}

				float bodyEnd = bar.NoteList[bar.NoteList.Count - 1].MusicScoreInfo.time;
				if (done + 0.02f < bodyEnd)
				{
					offenders.Add($"{fixture}: {bar.GetType().Name} id={bar.Id} finished at {done:F3}s,"
						+ $" head={bar.MusicScoreInfo.time:F3}s, last point={bodyEnd:F3}s");
				}
			}

			return offenders;
		}
	}
}
