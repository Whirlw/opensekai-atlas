using System.Collections.Generic;
using Sekai.Live;
using UnityEngine;
using UnityEngine.InputSystem;
using OpenSekai.Shared.Diagnostics;
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
		internal List<GuideNote> DamageConnectors { get; } = new List<GuideNote>();
		internal List<LiveLogic.InputTmp> ActiveInputs { get; } = new List<LiveLogic.InputTmp>(InputCapacity);
		internal LiveHoldCoordinator Holds { get; } = new LiveHoldCoordinator(InputCapacity);

		private readonly Dictionary<int, double> _judgedTouchTimes = new Dictionary<int, double>(FingerCapacity);
		private readonly Dictionary<int, float> _previousTouchLanes = new Dictionary<int, float>(FingerCapacity);

		/// <summary>
		/// JSON/SUS touches that started above the accepted height stay excluded until release.
		/// </summary>
		private readonly HashSet<int> _touchesAboveTheLine = new HashSet<int>();

		/// <summary>
		/// Touches that may no longer light an empty lane: the touch held a bar, rubbed a friction key,
		/// or spent its press on a key. The mark lasts until the finger leaves the screen, so a finger
		/// that slides off a bar stays quiet over the empty lanes it crosses.
		/// </summary>
		internal HashSet<int> SilencedTouches { get; } = new HashSet<int>();

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
			Holds.ClearKeyPresses();
			_previousTouchLanes.Clear();
			_touchesAboveTheLine.Clear();
			SilencedTouches.Clear();
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

		internal void BeginFrame()
		{
			JudgmentNotes.Clear();
			DamageConnectors.Clear();
		}

		internal void Register(NoteBase note)
		{
			_candidates.Register(note, JudgmentNotes, _logic.currentFrameInfo.time + LiveClock.ReferenceFrameSeconds);
		}

		internal void CollectCandidates(int pressWave = 0)
		{
			_candidates.Collect(JudgmentNotes, ActiveInputs, Holds, _judgedTouchTimes, pressWave);
		}

		internal void AssignContinuingHolds() => Holds.AssignContinuingHolds(JudgmentNotes, ActiveInputs);

		internal void SortPressCandidates(bool deduplicate = true) =>
			_candidates.SortPressCandidates(ActiveInputs, _logic.currentFrameInfo, deduplicate);

		internal void JudgeLifts() => Holds.JudgeLifts(JudgmentNotes, ActiveInputs, _logic.currentFrameInfo, _candidates);

		internal void RecordJudgedTouch(LiveTouch touch) => _judgedTouchTimes[touch.touchId] = touch.musicTime;

		internal void ForgetJudgedTouch(int touchId)
		{
			_judgedTouchTimes.Remove(touchId);
			Holds.ForgetKeyPress(touchId);
			SilencedTouches.Remove(touchId);
		}

		internal static bool IsEndedOrCanceled(InputTouchPhase phase)
		{
			return phase == InputTouchPhase.Ended || phase == InputTouchPhase.Canceled;
		}

		private void OnProcessTouch(in OpenSekai.Shared.Live.Simulation.SimTouch simTouch, bool residentSample,
			ref int activeInputCount)
		{
			float judgmentLane = ResolveJudgmentLane(in simTouch);
			int touchId = simTouch.TouchId;
			// The collector measures displacement from the previous frame's position, or from
			// Began when this contact started in the current frame.
			if (simTouch.Phase == InputTouchPhase.Began)
				_previousTouchLanes[touchId] = judgmentLane;

			if (activeInputCount <= _inputBuffer.Length - 1)
			{
				LiveTouch processTouch = new LiveTouch(simTouch.FingerId, simTouch.TouchId, simTouch.Delta, simTouch.WorldPosition, simTouch.Phase, simTouch.MusicTime)
				{
					PreviousFrameMusicTime = _logic.Clock.PreviousFrameTime
				};
				LiveLogic.InputTmp input = _inputBuffer[activeInputCount++];
				input.Reset(ref processTouch, judgmentLane);
				input.IsResidentSample = residentSample;
				if (_previousTouchLanes.TryGetValue(touchId, out float previousLane))
					input.PreviousLane = previousLane;
				ActiveInputs.Add(input);
			}
			else
			{
				LatencyCapture.Record("router_overflow", touchId, simTouch.FingerId, simTouch.Phase,
					song: simTouch.MusicTime);
			}

			if (simTouch.Phase == InputTouchPhase.Began)
			{
				_logic.TapCount++;
			}
		}


		/// <summary>
		/// Native hit tests use the sample's current position. Legacy charts remember the start height.
		/// </summary>
		private float ResolveJudgmentLane(in OpenSekai.Shared.Live.Simulation.SimTouch simTouch)
		{
			Vector2[] positions = LiveConfig.JudgmentPositions;
			float judgmentOffsetY = _bundle?.JudgmentOffsetY ?? 0f;

			if (_logic.Session.MusicScore?.UseExtendedLanes == true)
			{
				// The fixed stage's upper hitbox extent is 3.5 / 1.19 lane widths.
				float upperExtent = LiveConfig.widthX * (3.5f / 1.19f);
				if (simTouch.Phase == InputTouchPhase.Canceled ||
					OpenSekai.Shared.Live.TouchLane.IsAboveTheLine(positions, simTouch.WorldPosition, upperExtent))
					return OpenSekai.Shared.Live.TouchLane.Outside;

				return (simTouch.WorldPosition.x - positions[0].x) / LiveConfig.widthX;
			}

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

			if (!isAnInput || simTouch.Phase == InputTouchPhase.Canceled)
			{
				return OpenSekai.Shared.Live.TouchLane.Outside;
			}

			return OpenSekai.Shared.Live.TouchLane.Resolve(
				positions, simTouch.WorldPosition, _bundle?.JudgmentOffsetX ?? 0f);
		}
	}
}
