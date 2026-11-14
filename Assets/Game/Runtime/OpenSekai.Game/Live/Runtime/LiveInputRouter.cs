using System.Collections.Generic;
using Sekai.Live;
using UnityEngine;
using UnityEngine.InputSystem;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Sekai.Core.Live
{
	internal sealed class LiveInputRouter
	{
		private const int InputCapacity = 128;
		private const int FingerCapacity = 10;
		private readonly LiveLogic _logic;
		private readonly LiveBundleBuildData _bundle;
		private readonly InputSystemTouchSource _deviceTouchSource = new InputSystemTouchSource();
		private readonly LiveInputCollector _collector = new LiveInputCollector(FingerCapacity);
		private readonly LiveCandidatePipeline _candidates = new LiveCandidatePipeline();
		private readonly LiveLogic.InputTmp[] _inputBuffer = new LiveLogic.InputTmp[InputCapacity];
		private ILiveTouchSource _touchSource;
		private Camera _camera;

		internal List<NoteBase> JudgmentNotes { get; } = new List<NoteBase>();
		internal List<LiveLogic.InputTmp> ActiveInputs { get; } = new List<LiveLogic.InputTmp>(InputCapacity);
		internal LiveHoldCoordinator Holds { get; } = new LiveHoldCoordinator(InputCapacity);

		private readonly Dictionary<int, double> _judgedTouchTimes = new Dictionary<int, double>(FingerCapacity);

		internal LiveInputRouter(LiveLogic logic, LiveBundleBuildData bundle)
		{
			_logic = logic;
			_bundle = bundle;
			_touchSource = _deviceTouchSource;
			NativeInput.Enable();
			for (int i = 0; i < _inputBuffer.Length; i++)
			{
				_inputBuffer[i] = new LiveLogic.InputTmp();
			}
		}

		internal void SetCamera(Camera camera)
		{
			_camera = camera;
			_deviceTouchSource.Camera = camera;
		}

		internal void SetTouchSource(ILiveTouchSource source) => _touchSource = source ?? _deviceTouchSource;

		internal void Reset()
		{
			NativeInput.ResetPointerButtons();
			foreach (var device in InputSystem.devices)
			{
				InputSystem.ResetDevice(device);
			}

			NativeInput.Enable();
			_collector.Reset();
			ActiveInputs.Clear();
			_judgedTouchTimes.Clear();
		}

		internal void CollectTouches()
		{
			int activeInputCount = 0;
			ActiveInputs.Clear();
			_collector.Collect(_touchSource, _camera, _logic.ResolveInputMusicTime, OnProcessTouch, ref activeInputCount);
		}

		internal void BeginFrame() => JudgmentNotes.Clear();

		internal void Register(NoteBase note)
		{
			_candidates.Register(note, JudgmentNotes, _logic.currentFrameInfo.time + LiveClock.ReferenceFrameSeconds);
		}

		internal void CollectCandidates()
		{
			_candidates.Collect(JudgmentNotes, ActiveInputs, Holds, ConvertLaneBy, _judgedTouchTimes);
		}

		internal void AssignContinuingHolds() => Holds.AssignContinuingHolds(JudgmentNotes, ActiveInputs, ConvertLaneBy);

		internal void SortPressCandidates() => _candidates.SortPressCandidates(ActiveInputs, _logic.currentFrameInfo, ConvertLaneBy);

		internal void RecordJudgedTouch(LiveTouch touch) => _judgedTouchTimes[touch.touchId] = touch.musicTime;

		internal void ForgetJudgedTouch(int touchId) => _judgedTouchTimes.Remove(touchId);

		internal static bool IsEndedOrCanceled(InputTouchPhase phase)
		{
			return phase == InputTouchPhase.Ended || phase == InputTouchPhase.Canceled;
		}

		private void OnProcessTouch(in OpenSekai.Shared.Live.Simulation.SimTouch simTouch, ref int activeInputCount)
		{
			float judgmentLane = GetJudgmentLane(simTouch.WorldPosition, true);
			if (activeInputCount <= _inputBuffer.Length - 1)
			{
				LiveTouch processTouch = new LiveTouch(simTouch.FingerId, simTouch.TouchId, simTouch.Delta, simTouch.WorldPosition, simTouch.Phase, simTouch.MusicTime);
				LiveLogic.InputTmp input = _inputBuffer[activeInputCount++];
				input.Reset(ref processTouch, judgmentLane);
				ActiveInputs.Add(input);
			}

			if (simTouch.Phase == InputTouchPhase.Began)
			{
				_logic.TapCount++;
			}
		}

		internal float ConvertLaneBy(ref NoteBase note, ref LiveLogic.InputTmp input)
		{
			if (note == null || input == null)
			{
				return input?.Lane ?? -1f;
			}

			if ((note.Category == NoteCategory.Flick || note.Category == NoteCategory.FrictionFlick)
				&& note.Direction == NoteDirection.Left)
			{
				return GetJudgmentLane(input.Touch.worldPosition, false);
			}

			return input.Lane;
		}

		private float GetJudgmentLane(Vector3 touchPosition, bool checkJudgmentY)
		{
			Vector2[] positions = LiveConfig.JudgmentPositions;
			if (positions == null || positions.Length == 0)
			{
				return OpenSekai.Shared.Live.TouchLane.Outside;
			}

			float judgmentOffsetX = _bundle?.JudgmentOffsetX ?? 0f;
			float judgmentOffsetY = _bundle?.JudgmentOffsetY ?? 0f;

			return OpenSekai.Shared.Live.TouchLane.Resolve(
				positions, touchPosition, checkJudgmentY, judgmentOffsetX, judgmentOffsetY);
		}
	}
}
