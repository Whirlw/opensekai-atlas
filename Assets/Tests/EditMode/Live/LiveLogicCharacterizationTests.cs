// 第 1 步：特征测试（Characterization）。
// 这里不断言「应该怎样」，只把 0.3.0 这一刻的真实行为钉住，供后续 11 步重构逐阶段对照。
// 断言里出现「quirk / 现状」字样的，都是已登记、要单独修的问题，不要在重构里顺手改掉。
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
	/// Pins the behaviour the refactor must not change: the two input-time mappings, the frame a late
	/// touch is judged on, the notification order of a note's lifetime, and the finger identity quirk
	/// in hold release.
	/// </summary>
	public sealed class LiveLogicCharacterizationTests
	{
		private const double FRAME_GAME_TIME = 100d;
		private const float FRAME_MUSIC_TIME = 10f;

		// ---- 1. 两套时间映射 -------------------------------------------------

		[Test]
		public void TheLegacyMappingShrinksLatencyToSixtieths()
		{
			// 旧的公式把墙钟位移乘 1/60 秒：120 ms 的真实延迟只算 2 ms。
			float mapped = LiveLogic.ComputeLegacyInputMusicTime(FRAME_MUSIC_TIME, FRAME_GAME_TIME, FRAME_GAME_TIME + 0.12);

			Assert.That(mapped, Is.EqualTo(FRAME_MUSIC_TIME + 0.12f / 60f).Within(1e-5f));
			Assert.That(mapped, Is.LessThan(FRAME_MUSIC_TIME + 0.005f), "latency is shrunk sixty times, not passed through");
		}

		[Test]
		public void TheCurrentMappingPassesLatencyAndClampsItToAQuarterSecond()
		{
			// 早到：原样通过。
			Assert.That(
				LiveLogic.ComputeInputMusicTime(FRAME_MUSIC_TIME, FRAME_GAME_TIME, FRAME_GAME_TIME - 0.1),
				Is.EqualTo(FRAME_MUSIC_TIME - 0.1f).Within(1e-5f));

			// 迟到（inputTime 在帧之后）：夹到 0，不把未来的时间算进来。
			Assert.That(
				LiveLogic.ComputeInputMusicTime(FRAME_MUSIC_TIME, FRAME_GAME_TIME, FRAME_GAME_TIME + 0.1),
				Is.EqualTo(FRAME_MUSIC_TIME).Within(1e-5f));

			// 过期太久（暂停/失焦后残留的触摸）：夹到 -250 ms。
			Assert.That(
				LiveLogic.ComputeInputMusicTime(FRAME_MUSIC_TIME, FRAME_GAME_TIME, FRAME_GAME_TIME - 5d),
				Is.EqualTo(FRAME_MUSIC_TIME - 0.25f).Within(1e-5f));
		}

		// ---- 2. 迟到触摸落在哪一帧 -------------------------------------------

		[Test]
		public void ALateTouchIsJudgedAtTheFrameTime()
		{
			using var harness = new LiveHarness();
			var info = new MusicScoreInfo(0, 0f, 0f, 120f, 4f, 1f, 1f);
			var note = new NormalNote(info, 1, 4, 4, NoteCategory.Normal, 1f, NoteType.Default, 0f);
			var score = new MusicScore(new[] { info }, System.Array.Empty<EventBase>(), new NoteBase[] { note });
			harness.Arrange(score);

			harness.OnUpdate(0f, FRAME_GAME_TIME);
			// 采样时间比帧晚 200 ms —— 当前映射把它夹回帧时间，所以仍然是一帧内的 Perfect。
			harness.AddTouch(1, LiveConfig.JudgmentPositions[4], FRAME_GAME_TIME + 0.2);
			harness.OnInput();

			Assert.That(note.Result, Is.Not.EqualTo(NoteResult.None), "a touch newer than the frame is still judged");
			Assert.That(note.OffsetJudgeTime, Is.EqualTo(0f).Within(0.02f), "and it is judged exactly on the frame's music time");
		}

		// ---- 3. 遗留开关如何影响整条链路 -------------------------------------

		[Test]
		public void TheLegacySwitchChangesHowTheLiveLoopMapsTouches()
		{
			using var harness = new LiveHarness();
			var info = new MusicScoreInfo(0, 0f, 0f, 120f, 4f, 1f, 1f);
			var note = new NormalNote(info, 1, 4, 4, NoteCategory.Normal, 1f, NoteType.Default, 0f);
			var score = new MusicScore(new[] { info }, System.Array.Empty<EventBase>(), new NoteBase[] { note });
			harness.Arrange(score);

			bool saved = LiveConfig.UseLegacyJudgeTiming;
			try
			{
				harness.OnUpdate(0f, FRAME_GAME_TIME);

				LiveConfig.UseLegacyJudgeTiming = false;
				float current = harness.Logic.ResolveInputMusicTime(FRAME_GAME_TIME + 0.12);

				LiveConfig.UseLegacyJudgeTiming = true;
				float legacy = harness.Logic.ResolveInputMusicTime(FRAME_GAME_TIME + 0.12);

				Assert.That(current, Is.EqualTo(0f).Within(1e-5f), "current mapping clamps a later-than-frame touch back to the frame");
				Assert.That(legacy, Is.EqualTo(0.12f / 60f).Within(1e-5f), "legacy mapping shrinks the same gap sixty times");
			}
			finally
			{
				LiveConfig.UseLegacyJudgeTiming = saved;
			}
		}

		// ---- 4. 抬手用哪个 ID 认人 -------------------------------------------

		[Test]
		public void AReleaseWhoseTouchIdDiffersFromTheFingerIdDoesNotEndTheBar()
		{
			// 现状（quirk）：按住时记的是 fingerId，抬手分支比的却是 touchId。
			// 测试用的 sample 默认 fingerId == touchId，所以平时看不出来；这里把两者拆开，
			// 把「ID 不等时抬手不生效」这个行为钉住。修它是独立的规则变更，不在重构里做。
			using var harness = new LiveHarness();
			var document = ChartJson.Read(File.ReadAllText("Tools/fixtures/deco-tail-test10/score.json"));
			var score = RuntimeChartConverter.Convert(document, Resources.Load<LiveBundleBuildData>("live/config/LiveBundleBuildData"));
			var bar = score.NoteArray.OfType<LongNote>().First(n => n.ChildNote is NormalNote && n.HasJudgment);
			var tail = (NoteBase)bar.ChildNote;
			harness.Arrange(score, new NoteBase[] { bar });

			Vector3 mine = LanePosition(bar.JudgeLaneStart);
			float start = bar.MusicScoreInfo.time;
			const float frame = 1f / 60f;

			harness.Step(start - 0.02f, FRAME_GAME_TIME - 0.02f);
			harness.Step(start, FRAME_GAME_TIME,
				LiveHarness.Touch(1, mine, FRAME_GAME_TIME, TouchPhase.Began));
			Assert.That(bar.HoldingTouchId, Is.EqualTo(1), "the hold is acquired by finger 1");

			// 抬手样本：touchId 换成 7，fingerId 仍是 1，位置也移到轨道外（只留 ID 这条路）。
			float far = bar.JudgeLaneEnd >= 5.5f ? 0.5f : 10.5f;
			harness.Step(start + frame, FRAME_GAME_TIME + frame,
				new LiveTouchSample(7, new Vector2(800f, 600f), LanePosition(far),
					FRAME_GAME_TIME + frame, TouchPhase.Ended, Vector2.zero, fingerId: 1));

			Assert.That(tail.Result, Is.EqualTo(NoteResult.None),
				"现状：touchId 与 fingerId 不等时，抬手既不算条内释放，也不结束这条");
			Assert.That(bar.State, Is.Not.EqualTo(NoteState.Done));
		}

		private static Vector3 LanePosition(float lane)
		{
			int index = Mathf.Clamp(Mathf.RoundToInt(lane), 0, LiveConfig.JudgmentPositions.Length - 1);
			return LiveConfig.JudgmentPositions[index];
		}
	}
}
