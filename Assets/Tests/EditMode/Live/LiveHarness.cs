// 禁止用「new LongNote(...) 后反射调内部阶段」编写判定用例。
// 子音符的进度、视图、候选只在 OnUpdate 的整条扫描里推进，缺了它 IsJudgment 恒为假。
// 需要真实音符图时，请先 ChartJson.Read → RuntimeChartConverter.Convert，再交给本脚手架逐帧驱动。
using System;
using NUnit.Framework;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;
using UnityEngine.InputSystem;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OpenSekai.Tests.EditMode.Live
{
    /// <summary>只注入谱面和空视图；候选扫描与判定仍走真实 OnUpdate / OnInput。</summary>
    internal sealed class LiveHarness : IDisposable
    {
        private readonly bool _previousLegacyTiming;
        private readonly LiveConfig.NoteTypeJudgeData _previousJudgeData;
        private readonly MusicScoreInfo _previousFrame;
        private readonly Mouse _previousMouse;
        private readonly Mouse _isolatedMouse;
        private bool _disposed;

        internal LiveLogic Logic { get; }
        internal FakeTouchSource Source { get; }

        internal LiveHarness()
        {
            _previousLegacyTiming = LiveConfig.UseLegacyJudgeTiming;
            _previousJudgeData = LiveConfig.noteTypeJudgeData;
            _previousFrame = MusicScore.CurrentFrameInfo;
            LiveConfig.LoadMasterConfig();
            LiveConfig.UseLegacyJudgeTiming = false;
            // 隔离编辑器的真实鼠标，避免无触摸帧走物理输入兜底。
            _previousMouse = Mouse.current;
            _isolatedMouse = InputSystem.AddDevice<Mouse>();
            Source = new FakeTouchSource();
            Logic = new LiveLogic(null);
            Logic.SetTouchSource(Source);
        }

        internal void Arrange(MusicScore score, NoteBase[] notes = null, float displayTimeOffset = 1f, bool auto = false)
        {
            Logic.Session.MusicScore = score;
            Logic.Notes.Bind(notes ?? score.NoteArray);
            Logic.Session.NoteDisplayTimeOffset = displayTimeOffset;
            Logic.Session.IsAuto = auto;
            Logic.Session.Views = Array.Empty<LiveViewBase>();
        }

        /// <summary>Bind the real live callbacks to an observing view; note scanning stays unchanged.</summary>
        internal void BindViews(params LiveViewBase[] views)
        {
            Logic.Session.Views = views;
            var notes = Logic.Notes.Notes;
            Logic.Scheduler.BindNoteCallbacks(notes);
        }

        /// <summary>开始一帧并清空上一帧样本；随后可 AddTouch 多次，再 OnInput。</summary>
        internal void OnUpdate(float now, double inputTime)
        {
            Logic.OnUpdate(now, inputTime);
            Source.Clear();
        }

        internal void AddTouch(int id, Vector3 position, double inputTime,
            InputTouchPhase phase = InputTouchPhase.Began, Vector2 delta = default)
        {
            Source.Add(Touch(id, position, inputTime, phase, delta));
        }

        internal void OnInput() => Logic.OnInput();

        /// <summary>按游戏顺序推进一帧，支持同帧多指与同指多个样本。</summary>
        internal void Step(float now, double inputTime, params LiveTouchSample[] touches)
        {
            OnUpdate(now, inputTime);
            foreach (LiveTouchSample touch in touches) Source.Add(touch);
            OnInput();
        }

        internal static LiveTouchSample Touch(int id, Vector3 position, double inputTime,
            InputTouchPhase phase = InputTouchPhase.Began, Vector2 delta = default) =>
            new LiveTouchSample(id, new Vector2(800f, 600f), position, inputTime, phase, delta);


        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            InputSystem.RemoveDevice(_isolatedMouse);
            if (_previousMouse != null && _previousMouse.added) _previousMouse.MakeCurrent();
            LiveConfig.UseLegacyJudgeTiming = _previousLegacyTiming;
            LiveConfig.noteTypeJudgeData = _previousJudgeData;
            MusicScore.CurrentFrameInfo = _previousFrame;
        }
    }

    public sealed class LiveHarnessTests
    {
        private const int LANE = 4;
        private const double INPUT_TIME = 10d;
        private LiveHarness _harness;

        [SetUp]
        public void SetUp() => _harness = new LiveHarness();

        [TearDown]
        public void TearDown() => _harness.Dispose();

        [Test]
        public void ANormalNoteGetsAResultWhenPressed()
        {
            var info = new MusicScoreInfo(0, 0f, 0f, 120f, 4f, 1f, 1f);
            var note = new NormalNote(info, 1, LANE, LANE, NoteCategory.Normal, 1f, NoteType.Default, 0f);
            var score = new MusicScore(new[] { info }, Array.Empty<EventBase>(), new NoteBase[] { note });
            _harness.Arrange(score);

            _harness.OnUpdate(0f, INPUT_TIME);
            _harness.AddTouch(1, LiveConfig.JudgmentPositions[LANE], INPUT_TIME);
            _harness.OnInput();

            Assert.That(note.Result, Is.Not.EqualTo(NoteResult.None));
        }
    }
}
