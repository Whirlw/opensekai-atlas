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
		private readonly Dictionary<int, float> _previousTouchLanes = new Dictionary<int, float>(FingerCapacity);

		/// <summary>
		/// Touches that landed above the judgement line. The vertical gate is asked once, when a touch is
		/// born; a touch that failed it is not an input for the rest of its life, so nothing downstream
		/// has to ask about the y axis again.
		/// </summary>
		private readonly HashSet<int> _touchesAboveTheLine = new HashSet<int>();
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
			_previousTouchLanes.Clear();
			_touchesAboveTheLine.Clear();
		}

		internal void CollectTouches()
		{
			int activeInputCount = 0;
			ActiveInputs.Clear();
			_collector.Collect(_touchSource, _camera, _logic.ResolveInputMusicTime, OnProcessTouch, ref activeInputCount);
			foreach (LiveLogic.InputTmp input in ActiveInputs)
			{
				int touchId = input.Touch.touchId;
				if (IsEndedOrCanceled(input.Touch.phase))
					_previousTouchLanes.Remove(touchId);
				else
					_previousTouchLanes[touchId] = input.Lane;
			}
		}

		internal void BeginFrame() => JudgmentNotes.Clear();

		internal void Register(NoteBase note)
		{
			_candidates.Register(note, JudgmentNotes, _logic.currentFrameInfo.time + LiveClock.ReferenceFrameSeconds);
		}

		internal void CollectCandidates()
		{
			_candidates.Collect(JudgmentNotes, ActiveInputs, Holds, _judgedTouchTimes);
		}

		internal void AssignContinuingHolds() => Holds.AssignContinuingHolds(JudgmentNotes, ActiveInputs);

		internal void SortPressCandidates() => _candidates.SortPressCandidates(ActiveInputs, _logic.currentFrameInfo);

		internal void RecordJudgedTouch(LiveTouch touch) => _judgedTouchTimes[touch.touchId] = touch.musicTime;

		internal void ForgetJudgedTouch(int touchId) => _judgedTouchTimes.Remove(touchId);

		internal static bool IsEndedOrCanceled(InputTouchPhase phase)
		{
			return phase == InputTouchPhase.Ended || phase == InputTouchPhase.Canceled;
		}

		private void OnProcessTouch(in OpenSekai.Shared.Live.Simulation.SimTouch simTouch, ref int activeInputCount)
		{
			float judgmentLane = ResolveJudgmentLane(in simTouch);
			int touchId = simTouch.TouchId;
			// The collector measures displacement from the previous frame's position, or from
			// Began when this contact started in the current frame.
			if (simTouch.Phase == InputTouchPhase.Began)
				_previousTouchLanes[touchId] = judgmentLane;

			if (activeInputCount <= _inputBuffer.Length - 1)
			{
				LiveTouch processTouch = new LiveTouch(simTouch.FingerId, simTouch.TouchId, simTouch.Delta, simTouch.WorldPosition, simTouch.Phase, simTouch.MusicTime);
				LiveLogic.InputTmp input = _inputBuffer[activeInputCount++];
				input.Reset(ref processTouch, judgmentLane);
				if (_previousTouchLanes.TryGetValue(touchId, out float previousLane))
					input.PreviousLane = previousLane;
				ActiveInputs.Add(input);
			}

			if (simTouch.Phase == InputTouchPhase.Began)
			{
				_logic.TapCount++;
			}
		}


		/// <summary>
		/// Resolves one raw sample to a lane. This is the only place the vertical gate is consulted: the
		/// first sample of a touch decides whether the touch is an input at all, and the answer is kept in
		/// <see cref="_touchesAboveTheLine"/> for every sample that follows.
		/// </summary>
		private float ResolveJudgmentLane(in OpenSekai.Shared.Live.Simulation.SimTouch simTouch)
		{
			Vector2[] positions = LiveConfig.JudgmentPositions;
			float judgmentOffsetY = _bundle?.JudgmentOffsetY ?? 0f;

			if (simTouch.Phase == InputTouchPhase.Began
				&& OpenSekai.Shared.Live.TouchLane.IsAboveTheLine(positions, simTouch.WorldPosition, judgmentOffsetY))
			{
				_touchesAboveTheLine.Add(simTouch.TouchId);
			}

			bool isAnInput = !_touchesAboveTheLine.Contains(simTouch.TouchId);
			if (IsEndedOrCanceled(simTouch.Phase))
			{
				_touchesAboveTheLine.Remove(simTouch.TouchId);
			}

			if (!isAnInput)
			{
				return OpenSekai.Shared.Live.TouchLane.Outside;
			}

			return OpenSekai.Shared.Live.TouchLane.Resolve(
				positions, simTouch.WorldPosition, _bundle?.JudgmentOffsetX ?? 0f);
		}
	}
}
