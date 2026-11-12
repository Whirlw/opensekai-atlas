using System;
using System.Collections;
using System.Collections.Generic;
using CP;
using Cysharp.Threading.Tasks;
using System.Threading;
using Sekai.ScreenLayerExtention;
using UnityEngine;

namespace Sekai
{
	internal sealed class ScreenStackService
	{
		private readonly ScreenManager _owner;
		private ScreenLayerState _currentUI;
		private ScreenLayerState _nextUI;
		private ScreenLayerState _prevUI;
		private IBgmPlayer _layerBgmPlayer;
		private IBackgroundChanger _layerBackgroundChanger;
		private IEnumerator _changeUILayerCoroutine;

		internal ScreenStackService(ScreenManager owner)
		{
			_owner = owner;
		}

		internal LimitedStack<Tuple<ScreenLayerState, object, ScreenLayer.BootArgBase>> UiScreenStack { get; private set; }
		internal bool IsUILayerWorking => _changeUILayerCoroutine != null;
		internal bool IsPopUIScreen { get; private set; }

		internal void InitializeStack() => UiScreenStack = new LimitedStack<Tuple<ScreenLayerState, object, ScreenLayer.BootArgBase>>(16);

		internal static Dictionary<MenuScreenType, ScreenLayerState> CreateRegistry(ScreenLayerDataList registry)
		{
			var map = new Dictionary<MenuScreenType, ScreenLayerState>();
			if (registry?.Entries != null)
			{
				foreach (var entry in registry.Entries)
				{
					if (entry != null && !map.ContainsKey(entry.ScreenType))
					{
						map.Add(entry.ScreenType, new ScreenLayerState { Data = entry });
					}
				}
			}
			return map;
		}

		internal static ScreenLayerDataList LoadEntryScreenLayers() => ResourceCatalog.EntryScreenLayers.Load();

		internal void SetupBgmPlayer(IBgmPlayer _layerBgmPlayer) => this._layerBgmPlayer = _layerBgmPlayer;

		internal void SetupLayerBackgroundChanger(IBackgroundChanger layerBgChanger) => _layerBackgroundChanger = layerBgChanger;

		internal void AddScreen(MenuScreenType screenType, ScreenLayer.BootArgBase bootArg, bool isForward) => _owner.StartCoroutine(AddScreenCore(screenType, bootArg, isForward));
		internal UniTask AddScreenWithoutHeaderSetting(MenuScreenType screenType, ScreenLayer.BootArgBase bootArg = null, CancellationToken ct = default(CancellationToken))
		{
			if (!ct.IsCancellationRequested)
			{
				_owner.StartCoroutine(AddScreenCore(screenType, bootArg, true, false));
			}

			return UniTask.CompletedTask;
		}

		internal void RemoveScreen(MenuScreenType screenType) => _owner.StartCoroutine(RemoveScreenCore(screenType));
		internal IEnumerator RemoveScreenCoroutine(MenuScreenType screenType) => RemoveScreenCore(screenType);
		internal UniTask RemoveScreenAsync(MenuScreenType screenType, CancellationToken ct = default(CancellationToken))
		{
			if (!ct.IsCancellationRequested)
			{
				_owner.StartCoroutine(RemoveScreenCore(screenType));
			}

			return UniTask.CompletedTask;
		}

		internal bool IsExistInScreenMap(MenuScreenType screenType) => _owner.RegisteredScreens != null && _owner.RegisteredScreens.ContainsKey(screenType);

		internal void ChangeUIScreen(MenuScreenType screenType, ScreenLayer.BootArgBase bootArg, bool isWaitExitAnimation = false, bool isForward = true, bool nextScreenAnimationIsForward = true, bool isClearBootDataOnExitCancel = false)
		{
			if (_changeUILayerCoroutine != null)
			{
				return;
			}

			_changeUILayerCoroutine = ChangeUIScreenCore(screenType, bootArg, isWaitExitAnimation, isForward, nextScreenAnimationIsForward, isClearBootDataOnExitCancel);
			_owner.StartCoroutine(_changeUILayerCoroutine);
		}

		internal void PushUIScreen(MenuScreenType screenType, ScreenLayer.BootArgBase bootArg, bool isWaitExitAnimation = false)
		{
			if (_changeUILayerCoroutine != null)
			{
				return;
			}

			if (_currentUI?.Data != null && _currentUI.Data.ScreenType == screenType)
			{
				return;
			}

			_owner.StartCoroutine(PushUIScreenCore(screenType, bootArg, isWaitExitAnimation));
		}

		internal void BackUIScreen(bool isWaitExitAnimation = false)
		{
			if (_owner.OnBackUIScreenOverride != null)
			{
				ExecuteBackUIScreenOverride(isWaitExitAnimation);
				return;
			}

			if (_changeUILayerCoroutine != null)
			{
				return;
			}

			_changeUILayerCoroutine = PopUIScreen(isWaitExitAnimation);
			_owner.StartCoroutine(_changeUILayerCoroutine);
		}

		internal void BackUIScreenAndDestroyExitedScreen(MenuScreenType screenType, bool isWaitExitAnimation = false)
		{
			if (_owner.OnBackUIScreenOverride != null)
			{
				ExecuteBackUIScreenOverride(isWaitExitAnimation);
				DestroyScreenInstance(screenType);
				return;
			}

			if (_changeUILayerCoroutine != null)
			{
				return;
			}

			_changeUILayerCoroutine = BackUIScreenAndDestroyExitedScreenRoutine(screenType, isWaitExitAnimation);
			_owner.StartCoroutine(_changeUILayerCoroutine);
		}

		internal void ForceBackUIScreen(bool isWaitExitAnimation = false)
		{
			_owner.OnBackUIScreenOverride = null;
			BackUIScreen(isWaitExitAnimation);
		}

		private void ExecuteBackUIScreenOverride(bool isWaitExitAnimation)
		{
			var callback = _owner.OnBackUIScreenOverride;
			if (_owner.IsAutoClearBackUIScreenOverride)
			{
				_owner.OnBackUIScreenOverride = null;
			}

			callback?.Invoke(isWaitExitAnimation);
		}

		internal void PreloadUIScreen(MenuScreenType screenType) => _owner.StartCoroutine(PreloadUIScreenCore(screenType));

		internal void RebuildScreen(MenuScreenType screenType, Action<ScreenLayer> onFinished = null, bool isFoward = true, bool isClearBootDataOnExitCancel = false) => _owner.StartCoroutine(RebuildUIScreenCore(screenType, onFinished, isFoward, isClearBootDataOnExitCancel));

		internal UniTask RebuildScreenAsync(MenuScreenType screenType, Action<ScreenLayer> onFinished = null, bool isFoward = true, bool isClearBootDataOnExitCancel = false)
		{
			_owner.StartCoroutine(RebuildUIScreenCore(screenType, onFinished, isFoward, isClearBootDataOnExitCancel));
			return UniTask.CompletedTask;
		}
		private IEnumerator BackUIScreenAndDestroyExitedScreenRoutine(MenuScreenType screenType, bool isWaitExitAnimation)
		{
			yield return _owner.StartCoroutine(PopUIScreen(isWaitExitAnimation));
			DestroyScreenInstance(screenType);
			_changeUILayerCoroutine = null;
		}
		private IEnumerator AddScreenCore(MenuScreenType screenType, ScreenLayer.BootArgBase bootArg, bool isForward = true, bool applyHeaderSetting = true)
		{
			if (_owner.RegisteredScreens == null || !_owner.RegisteredScreens.TryGetValue(screenType, out var layer))
			{
				LogUtility.LogError("Not Register ScreenLayer;" + screenType, Array.Empty<object>());
				return EmptyCoroutine();
			}

			return AddScreenCore(layer, bootArg, isForward, null, applyHeaderSetting);
		}

		internal void ClearScreenStack() => UiScreenStack?.Clear();

		internal void PopScreenStack() => UiScreenStack?.Pop();

		internal void PushScreenStack(MenuScreenType screenType, object screenObject = null)
		{
			if (_owner.RegisteredScreens != null && _owner.RegisteredScreens.TryGetValue(screenType, out var layer))
			{
				UiScreenStack?.Push(Tuple.Create(layer, screenObject, (ScreenLayer.BootArgBase)null));
			}
		}

		internal List<MenuScreenType> GetScreenStackList()
		{
			var result = new List<MenuScreenType>();
			if (UiScreenStack == null)
			{
				return result;
			}

			for (var i = 0; i < UiScreenStack.Count; i++)
			{
				var layer = UiScreenStack[i]?.Item1;
				if (layer?.Data != null)
				{
					result.Add(layer.Data.ScreenType);
				}
			}

			return result;
		}

		private bool InstantiateLayerScreen(ScreenLayerState layer)
		{
			if (layer?.Data == null)
			{
				return false;
			}

			var layerObject = _owner.GetLayerObject(layer.Data.DisplayLayer);
			if (layerObject == null)
			{
				LogUtility.LogError("Not Found Layer;" + layer.Data.DisplayLayer, Array.Empty<object>());
				return false;
			}

			var prefab = layer.Data.LoadScreenLayerPrefab();
			if (prefab == null)
			{
				LogUtility.LogError("Not Found ScreenLayer;" + layer.Data.name, Array.Empty<object>());
				return false;
			}

			var instanceObject = UnityEngine.Object.Instantiate(prefab, layerObject.transform);
			instanceObject.transform.localScale = Vector3.one;
			instanceObject.transform.localPosition = Vector3.zero;
			instanceObject.name = layer.Data.name;
			layer.RefInstance = instanceObject;
			layer.ScreenLayer = instanceObject.GetComponent<ScreenLayer>();
			if (layer.ScreenLayer == null)
			{
				LogUtility.LogError("Not Found ScreenLayer;" + instanceObject.name, Array.Empty<object>());
				return false;
			}

			if (layer.Data.hasLayerCamera && layer.ScreenLayer.LayerCamera != null && _owner.CameraStack != null)
			{
				_owner.CameraStack.AddToStack(layer.ScreenLayer.LayerCamera, layer.Data.layerCameraPriority);
			}

			return true;
		}

		internal void DestroyScreenInstance(MenuScreenType screenType)
		{
			if (_owner.RegisteredScreens == null || !_owner.RegisteredScreens.TryGetValue(screenType, out var layer) || layer == null)
			{
				return;
			}

			if (layer.ScreenLayer != null)
			{
				if (layer.Data != null && layer.Data.hasLayerCamera && layer.ScreenLayer.LayerCamera != null && _owner.CameraStack != null)
				{
					_owner.CameraStack.RemoveFromStack(layer.ScreenLayer.LayerCamera);
				}
				layer.ScreenLayer.OnScreenLayerExitScene();
			}

			if (layer.RefInstance != null)
			{
				if (Application.isPlaying)
				{
					UnityEngine.Object.Destroy(layer.RefInstance);
				}
				else
				{
					UnityEngine.Object.DestroyImmediate(layer.RefInstance);
				}
			}

			layer.RefInstance = null;
			layer.ScreenLayer = null;
		}

		internal T GetLayerComponent<T>(MenuScreenType screenType) where T : ScreenLayer
		{
			if (_owner.RegisteredScreens != null && _owner.RegisteredScreens.TryGetValue(screenType, out var layer))
			{
				return layer.ScreenLayer as T;
			}

			return null;
		}

		internal bool IsActiveScreen(MenuScreenType screenType) => _currentUI?.Data != null && _currentUI.Data.ScreenType == screenType;

		internal bool IsInstantiatedScreen(MenuScreenType screenType) => _owner.RegisteredScreens != null && _owner.RegisteredScreens.TryGetValue(screenType, out var layer) && layer.RefInstance != null;

		private static IEnumerator EmptyCoroutine()
		{
			yield break;
		}

		private IEnumerator ChangeUIScreenCore(MenuScreenType screenType, ScreenLayer.BootArgBase bootArg, bool isWaitExitAnimation, bool isForward, bool nextScreenAnimationIsForward, bool isClearBootDataOnExitCancel)
		{
			if (_owner.RegisteredScreens == null || !_owner.RegisteredScreens.TryGetValue(screenType, out var layer))
			{
				LogUtility.LogError("Not Register ScreenLayer;" + screenType, Array.Empty<object>());
				_changeUILayerCoroutine = null;
				yield break;
			}

			var oldCurrent = _currentUI;
			_nextUI = layer;
			if (oldCurrent != null)
			{
				var exitCoroutine = ExitScreen(oldCurrent, true, isForward, isClearBootDataOnExitCancel);
				if (isWaitExitAnimation)
				{
					yield return _owner.StartCoroutine(exitCoroutine);
				}
				else
				{
					_owner.StartCoroutine(exitCoroutine);
				}
			}

			_prevUI = oldCurrent;
			_currentUI = layer;
			yield return _owner.StartCoroutine(AddScreenCore(_currentUI, bootArg, nextScreenAnimationIsForward));
			_owner.OnChangeUILayer?.Invoke(_currentUI.Data.ScreenType);
			yield return WaitUntilPlaying(_currentUI);
			_changeUILayerCoroutine = null;
		}

		private IEnumerator PushUIScreenCore(MenuScreenType screenType, ScreenLayer.BootArgBase bootArg, bool isWaitExitAnimation)
		{
			if (_owner.RegisteredScreens == null || !_owner.RegisteredScreens.TryGetValue(screenType, out var layer))
			{
				LogUtility.LogError("Not Register ScreenLayer;" + screenType, Array.Empty<object>());
				_changeUILayerCoroutine = null;
				yield break;
			}

			var oldCurrent = _currentUI;
			_nextUI = layer;
			if (oldCurrent != null && oldCurrent.ScreenLayer != null)
			{
				oldCurrent.ScreenLayer.OnWillExit();
				while (oldCurrent.ScreenLayer.WillExitBehaviour == ScreenLayer.WillExitBehaviourStatus.None)
				{
					yield return null;
				}

				if (oldCurrent.ScreenLayer.WillExitBehaviour == ScreenLayer.WillExitBehaviourStatus.Cancel)
				{
					_nextUI = null;
					_changeUILayerCoroutine = null;
					yield break;
				}

				UiScreenStack?.Push(Tuple.Create(oldCurrent, oldCurrent.ScreenLayer.GetStackObject(), bootArg));
				var exitCoroutine = ExitScreen(oldCurrent, false, true);
				if (isWaitExitAnimation)
				{
					yield return _owner.StartCoroutine(exitCoroutine);
				}
				else
				{
					_owner.StartCoroutine(exitCoroutine);
				}
			}

			_prevUI = oldCurrent;
			_currentUI = layer;
			yield return _owner.StartCoroutine(AddScreenCore(_currentUI, bootArg, true));
			_owner.OnChangeUILayer?.Invoke(_currentUI.Data.ScreenType);
			yield return WaitUntilPlaying(_currentUI);
			_changeUILayerCoroutine = null;
		}

		private IEnumerator PopUIScreen(bool isWaitExitAnimation)
		{
			if (UiScreenStack == null || UiScreenStack.Count <= 0)
			{
				_changeUILayerCoroutine = null;
				yield break;
			}

			IsPopUIScreen = true;
			var layerData = UiScreenStack.Pop();
			var layer = layerData?.Item1;
			if (layer == null)
			{
				_changeUILayerCoroutine = null;
				IsPopUIScreen = false;
				yield break;
			}

			var oldCurrent = _currentUI;
			_nextUI = layer;
			if (oldCurrent != null)
			{
				var exitCoroutine = ExitScreen(oldCurrent, true, false);
				if (isWaitExitAnimation)
				{
					yield return _owner.StartCoroutine(exitCoroutine);
				}
				else
				{
					_owner.StartCoroutine(exitCoroutine);
				}
			}

			_prevUI = oldCurrent;
			_currentUI = layer;
			yield return _owner.StartCoroutine(AddScreenCore(layer, layerData.Item3, false, layerData.Item2));
			_owner.OnChangeUILayer?.Invoke(layer.Data.ScreenType);
			yield return WaitUntilPlaying(layer);
			IsPopUIScreen = false;
			_changeUILayerCoroutine = null;
		}

		private IEnumerator PreloadUIScreenCore(MenuScreenType screenType)
		{
			if (_owner.RegisteredScreens == null || !_owner.RegisteredScreens.TryGetValue(screenType, out var layer) || layer == null)
			{
				yield break;
			}

			if (layer.RefInstance == null && !InstantiateLayerScreen(layer))
			{
				yield break;
			}

			if (layer.ScreenLayer != null && layer.ScreenLayer.gameObject.activeSelf)
			{
				layer.ScreenLayer.gameObject.SetActive(false);
			}
		}

		private IEnumerator RebuildUIScreenCore(MenuScreenType screenType, Action<ScreenLayer> onFinished, bool isFoward, bool isClearBootDataOnExitCancel)
		{
			if (_owner.RegisteredScreens != null && _owner.RegisteredScreens.TryGetValue(screenType, out var layer))
			{
				yield return _owner.StartCoroutine(ExitScreen(layer, true, isFoward, isClearBootDataOnExitCancel));
				yield return _owner.StartCoroutine(AddScreenCore(layer, null, isFoward));
				onFinished?.Invoke(layer.ScreenLayer);
			}
		}

		private IEnumerator AddScreenCore(ScreenLayerState layer, ScreenLayer.BootArgBase bootArg, bool isForward, object layerObject = null, bool applyHeaderSetting = true)
		{
			if (layer == null)
			{
				yield break;
			}

			if (layer.RefInstance == null && !InstantiateLayerScreen(layer))
			{
				yield break;
			}

			if (layer.ScreenLayer == null)
			{
				yield break;
			}

			layer.ScreenLayer.gameObject.SetActive(true);
			layer.ScreenLayer.LayerStackObject = layerObject;
			var rectTransform = layer.ScreenLayer.GetComponent<RectTransform>();
			if (rectTransform != null)
			{
				rectTransform.SetAsLastSibling();
			}

			layer.ScreenLayer.ScreenInsertDirection = isForward ? ScreenLayer.InsertDirection.Forward : ScreenLayer.InsertDirection.Back;
			if (!isForward)
			{
				layer.ScreenLayer.ReverseStartAnimationType();
			}

			_owner.BootArgObject = bootArg;
			yield return _owner.StartCoroutine(layer.ScreenLayer.OnScreenLayerBoot(layer.Data, bootArg));
			_owner.BootArgObject = null;

			if (!isForward)
			{
				layer.ScreenLayer.ReverseStartAnimationType();
			}

			_layerBgmPlayer?.Play(layer.Data.bgmType);
			_layerBackgroundChanger?.Show(layer.Data.BackgroundType);
			if (applyHeaderSetting)
			{
				HeaderUtility.SetDisplayHeader(layer.Data);
			}
			layer.ScreenLayer.OnScreenLayerInitComponent();
			layer.ScreenLayer.OnScreenLayerStart();
		}

		private IEnumerator RemoveScreenCore(MenuScreenType screenType, bool isForward = true, bool isClearBootDataOnExitCancel = false)
		{
			if (_owner.RegisteredScreens != null && _owner.RegisteredScreens.TryGetValue(screenType, out var layer))
			{
				yield return _owner.StartCoroutine(ExitScreen(layer, true, isForward, isClearBootDataOnExitCancel));
			}
		}

		private IEnumerator ExitScreen(ScreenLayerState layer, bool executeOnWillExit, bool isForward = true, bool isClearBootDataOnExitCancel = false)
		{
			if (layer?.ScreenLayer == null)
			{
				yield break;
			}

			if (executeOnWillExit)
			{
				layer.ScreenLayer.OnWillExit();
				while (layer.ScreenLayer.WillExitBehaviour == ScreenLayer.WillExitBehaviourStatus.None)
				{
					yield return null;
				}

				if (layer.ScreenLayer.WillExitBehaviour == ScreenLayer.WillExitBehaviourStatus.Cancel)
				{
					if (isClearBootDataOnExitCancel)
					{
						_owner.BootArgObject = null;
					}
					yield break;
				}
			}

			if (!isForward)
			{
				layer.ScreenLayer.ReverseExitAnimationType();
			}

			yield return _owner.StartCoroutine(layer.ScreenLayer.OnScreenLayerExitStart());

			if (!isForward)
			{
				layer.ScreenLayer.ReverseExitAnimationType();
			}

			if (layer.ScreenLayer != null)
			{
				layer.ScreenLayer.gameObject.SetActive(false);
			}
		}

		private static IEnumerator WaitUntilPlaying(ScreenLayerState layer)
		{
			while (layer?.ScreenLayer != null && layer.ScreenLayer.ScreenLayerState != ScreenLayer.State.Playing)
			{
				yield return null;
			}
		}

		internal MenuScreenType GetPeekScreenType()
		{
			var layer = UiScreenStack?.Peek()?.Item1;
			return layer?.Data != null ? layer.Data.ScreenType : MenuScreenType.None;
		}

		internal bool TryGetScreenLayerData(MenuScreenType screenType, out ScreenLayerData screenLayerData)
		{
			screenLayerData = null;
			if (_owner.RegisteredScreens == null || !_owner.RegisteredScreens.TryGetValue(screenType, out var layer))
			{
				return false;
			}

			screenLayerData = layer.Data;
			return screenLayerData != null;
		}

		internal MenuScreenType GetCurrentUIScreenType() => _currentUI?.Data != null ? _currentUI.Data.ScreenType : MenuScreenType.None;

		internal MenuScreenType GetNextUIScreenType() => _nextUI?.Data != null ? _nextUI.Data.ScreenType : MenuScreenType.None;

		internal MenuScreenType GetPrevUIScreenType() => _prevUI?.Data != null ? _prevUI.Data.ScreenType : MenuScreenType.None;

		internal void ExitScene()
		{
			foreach (var pair in _owner.RegisteredScreens ?? new Dictionary<MenuScreenType, ScreenLayerState>())
			{
				pair.Value.ScreenLayer?.OnScreenLayerExitScene();
			}
		}
	}
}
