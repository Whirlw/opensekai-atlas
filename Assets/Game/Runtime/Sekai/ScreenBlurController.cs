using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using static Sekai.ScreenManager;

namespace Sekai
{
	internal sealed class ScreenBlurController
	{
		private const string _uiBlurLayerManagerPrefabPath = "UI/Blur/UIBlurLayerManager";
		internal const string DefaultLayerName = "UI";
		private const string BlurLayerName = "BlurUI";
		private static readonly List<SceneManager.Scene> EnableBlurSceneList = new List<SceneManager.Scene>
		{
			SceneManager.Scene.Title,
			SceneManager.Scene.MusicScoreMaker,
			SceneManager.Scene.Library
		};

		private readonly ScreenManager _owner;
		private readonly List<BlurLayerTarget> _blurTargets = new List<BlurLayerTarget>();
		private readonly Queue<(BlurLayerTarget, BlurStackDirection)> _blurQueue = new Queue<(BlurLayerTarget, BlurStackDirection)>();
		private UIBlurLayerManager _uiBlurLayerManager;
		private UIBlurLayerManager.Mode _blurCaptureModeCached;
		private BlurDialogProcessState _dialogProcessState;

		internal ScreenBlurController(ScreenManager owner)
		{
			_owner = owner;
		}

		internal void OnOpeningDialog(DialogBase dialog, DisplayLayerType layer)
		{
			if (dialog != null && IsEnabledBlurScene())
			{
				EnableBlurCore(new BlurLayerTarget(dialog, dialog.gameObject, layer));
			}
		}

		internal void OnClosedDialog(DialogBase dialog, DisplayLayerType layer)
		{
			if (dialog != null && IsEnabledBlurScene())
			{
				RemoveBlurTarget(new BlurLayerTarget(dialog, dialog.gameObject, layer));
			}
		}

		internal UniTask SetUIBlurCaptureMode(UIBlurLayerManager.Mode mode)
		{
			if (_uiBlurLayerManager != null)
			{
				_blurCaptureModeCached = _uiBlurLayerManager.CaptureMode;
			}
			return SetUIBlurCaptureModeCore(mode);
		}

		private async UniTask SetUIBlurCaptureModeCore(UIBlurLayerManager.Mode mode)
		{
			SetUIBlurLayerManager();
			if (_uiBlurLayerManager == null)
			{
				return;
			}

			await _uiBlurLayerManager.SetCaptureMode(mode);
			if (!_uiBlurLayerManager.IsEnabledBlurImage)
			{
				return;
			}

			if (_uiBlurLayerManager.CaptureMode == UIBlurLayerManager.Mode.Always)
			{
				EnableRenderArea(true);
				return;
			}

			if (_uiBlurLayerManager.CaptureMode == UIBlurLayerManager.Mode.OnShot)
			{
				await UniTask.WaitUntil(() => _uiBlurLayerManager == null || !_uiBlurLayerManager.BlurExecuting, PlayerLoopTiming.Update, _owner.GetCancellationTokenOnDestroy());
				await UniTask.WaitForEndOfFrame(_owner, _owner.GetCancellationTokenOnDestroy());
				EnableRenderArea(false);
			}
		}

		internal void EnableBlur()
		{
			SetUIBlurLayerManager();
			if (_uiBlurLayerManager == null)
			{
				return;
			}

			SetUICameraCullingMask(true);
			_uiBlurLayerManager.Enable();
		}

		internal void DisableBlur()
		{
			foreach (var _target in _blurTargets.ToArray())
			{
				_target?.RemoveCanvasIfExists();
				if (_target != null)
				{
					SetLayer(DefaultLayerName, _target);
				}
			}
			_blurTargets.Clear();
			_blurQueue.Clear();
			_dialogProcessState = BlurDialogProcessState.Ready;
			_uiBlurLayerManager?.Disable();
			SetUICameraCullingMask(false);
		}
		private void SetUIBlurLayerManager()
		{
			if (_uiBlurLayerManager != null)
			{
				return;
			}

			var prefab = Resources.Load<GameObject>(_uiBlurLayerManagerPrefabPath)
				?? Resources.Load<GameObject>(_uiBlurLayerManagerPrefabPath.ToLowerInvariant());
			GameObject blurObject;
			var parent = _owner.transform;
			if (prefab != null)
			{
				blurObject = UnityEngine.Object.Instantiate(prefab, parent, false);
			}
			else
			{
				blurObject = new GameObject("UIBlurLayerManager", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster), typeof(UIBlurLayerManager));
				blurObject.transform.SetParent(parent, false);
				var rect = blurObject.GetComponent<RectTransform>();
				rect.anchorMin = Vector2.zero;
				rect.anchorMax = Vector2.one;
				rect.offsetMin = Vector2.zero;
				rect.offsetMax = Vector2.zero;
			}

			blurObject.name = "UIBlurLayerManager";
			_uiBlurLayerManager = blurObject.GetComponent<UIBlurLayerManager>() ?? blurObject.AddComponent<UIBlurLayerManager>();
		}

		private void EnableBlur(BlurLayerTarget _target)
		{
			if (_target == null || !_target.ExistsTarget || !_target.EnableBlur)
			{
				return;
			}
			if (!IsEnabledBlurScene() || IsStacked(_target))
			{
				return;
			}

			SetUIBlurLayerManager();
			if (_uiBlurLayerManager == null)
			{
				return;
			}

			AddTarget(_target);
			SetBlurCore(_target);
		}

		private bool IsStacked(BlurLayerTarget _target)
		{
			return _target != null && _blurTargets.Exists(x => x != null && x.Equals(_target));
		}

		private void AddTarget(BlurLayerTarget _target)
		{
			if (_target != null && !IsStacked(_target))
			{
				_blurTargets.Add(_target);
			}
		}

		private void EnqueueBlur((BlurLayerTarget, BlurStackDirection) blurParam)
		{
			_blurQueue.Enqueue(blurParam);
		}
		private UniTask DequeueBlur()
		{
			while (_blurQueue.Count > 0)
			{
				var item = _blurQueue.Dequeue();
				SetBlurCore(item.Item1, item.Item2);
			}
			return UniTask.CompletedTask;
		}
		private void SetBlurCore(BlurLayerTarget _target, BlurStackDirection dir = BlurStackDirection.Forward)
		{
			if (_target == null || !_target.ExistsTarget)
			{
				return;
			}

			var blurTarget = _target.EnableBlur ? _target : GetLastEnabledBlurTarget();
			if (blurTarget == null)
			{
				foreach (var blurTargetEntry in _blurTargets.ToArray())
				{
					blurTargetEntry?.RemoveCanvasIfExists();
				}
				SetLayer(DefaultLayerName, _target);
				DisableBlur();
				return;
			}

			var blurLayerCanvas = _owner.GetLayerObject(blurTarget.LayerType)?.GetComponent<Canvas>();
			if (blurLayerCanvas == null)
			{
				return;
			}

			_uiBlurLayerManager?.SetCanvas(blurLayerCanvas);
			SetLayer(BlurLayerName, blurTarget);
			EnableBlur();

			var dirTarget = dir == BlurStackDirection.Back ? blurTarget : _target;
			if (dirTarget == null)
			{
				return;
			}

			var targetCanvas = _owner.GetLayerObject(dirTarget.LayerType)?.GetComponent<Canvas>();
			if (dirTarget.EnableBlur)
			{
				foreach (var blurTargetEntry in _blurTargets.ToArray())
				{
					if (blurTargetEntry != null && !blurTargetEntry.Equals(dirTarget))
					{
						blurTargetEntry.RemoveCanvasIfExists();
					}
				}
			}
			else
			{
				var sameLayerBlurTarget = GetLastSameLayerBlurTarget(dirTarget);
				if (sameLayerBlurTarget != null)
				{
					targetCanvas = sameLayerBlurTarget.TargetCanvas;
				}
			}

			if (targetCanvas == null)
			{
				return;
			}

			dirTarget.AddCanvasCopyFrom(targetCanvas);
			SetTargetLayer(DefaultLayerName, dirTarget.TargetObject);
		}

		private BlurLayerTarget GetLastEnabledBlurTarget()
		{
			for (var i = _blurTargets.Count - 1; i >= 0; i--)
			{
				var _target = _blurTargets[i];
				if (_target != null && _target.EnableBlur)
				{
					return _target;
				}
			}

			return null;
		}

		private BlurLayerTarget GetLastSameLayerBlurTarget(BlurLayerTarget dirTarget)
		{
			if (dirTarget == null)
			{
				return null;
			}

			for (var i = _blurTargets.Count - 1; i >= 0; i--)
			{
				var _target = _blurTargets[i];
				if (_target != null && !_target.Equals(dirTarget) && _target.LayerType == dirTarget.LayerType)
				{
					return _target;
				}
			}

			return null;
		}

		private void SetLayer(string layerName, BlurLayerTarget _target)
		{
			if (_target == null || _target.LayerType < 0)
			{
				return;
			}

			var layer = LayerMask.NameToLayer(layerName);
			if (layer < 0)
			{
				return;
			}

			// Do not recurse the whole target tree here.
			// It changes only Canvas GameObjects in display layers up to the target
			// layer, excluding the target root, so the blur renderer can draw those
			// canvases into the BlurUI capture while the target remains above it.
			for (var i = 0; i <= (int)_target.LayerType; i++)
			{
				var layerObject = _owner.GetLayerObject((DisplayLayerType)i);
				if (layerObject == null)
				{
					continue;
				}

				var canvases = layerObject.GetComponentsInChildren<Canvas>(true);
				foreach (var canvas in canvases)
				{
					if (canvas == null || canvas.gameObject == _target.TargetObject)
					{
						continue;
					}

					canvas.gameObject.layer = layer;
				}
			}
		}

		private static void SetTargetLayer(string layerName, GameObject _target)
		{
			if (_target == null)
			{
				return;
			}

			var layer = LayerMask.NameToLayer(layerName);
			if (layer < 0)
			{
				return;
			}

			_target.layer = layer;
		}

		private void DisableBlur(BlurLayerTarget _target)
		{
			if (_target == null)
			{
				return;
			}

			RemoveTarget(_target);
			_target.RemoveCanvasIfExists();
			SetLayer(DefaultLayerName, _target);
			if (_blurTargets.Count == 0)
			{
				_uiBlurLayerManager?.Disable();
				SetUICameraCullingMask(false);
			}
		}

		private void RemoveTarget(BlurLayerTarget _target)
		{
			if (_target == null)
			{
				return;
			}

			_blurTargets.RemoveAll(x => x == null || x.Equals(_target));
		}

		private void SetUICameraCullingMask(bool addBlur)
		{
			var camera = _owner.GetUICamera();
			if (camera == null)
			{
				return;
			}

			var uiLayer = LayerMask.NameToLayer(DefaultLayerName);
			var blurLayer = LayerMask.NameToLayer(BlurLayerName);
			if (uiLayer < 0 || blurLayer < 0)
			{
				return;
			}

			var cullingMask = 1 << uiLayer;
			if (addBlur)
			{
				cullingMask |= 1 << blurLayer;
			}

			camera.cullingMask = cullingMask;
		}

		internal void EnableBlurByScreenLayer(ScreenLayer screenLayer, DisplayLayerType _layerType)
		{
			if (screenLayer != null)
			{
				EnableBlurCore(new BlurLayerTarget(screenLayer, screenLayer.gameObject, _layerType));
			}
		}

		private void EnableBlurCore(BlurLayerTarget _target)
		{
			if (_target == null || !_target.ExistsTarget || !_target.EnableBlur)
			{
				return;
			}

			if (_dialogProcessState == BlurDialogProcessState.Ready)
			{
				_blurCaptureModeCached = _uiBlurLayerManager != null ? _uiBlurLayerManager.CaptureMode : UIBlurLayerManager.Mode.OnShot;
				if (_blurTargets.Count == 0)
				{
					SetUIBlurCaptureModeCore(UIBlurLayerManager.Mode.Always).Forget();
				}
				_dialogProcessState = BlurDialogProcessState.Opening;
			}

			EnableBlur(_target);
		}

		internal void RemoveBlurByScreenLayer(ScreenLayer screenLayer, DisplayLayerType _layerType)
		{
			if (screenLayer != null)
			{
				RemoveBlurTarget(new BlurLayerTarget(screenLayer, screenLayer.gameObject, _layerType));
			}
		}

		internal void OnOpenedDialog()
		{
			if (_dialogProcessState != BlurDialogProcessState.Opening)
			{
				return;
			}

			var restoreMode = _blurCaptureModeCached;
			_dialogProcessState = BlurDialogProcessState.Ready;
			if (restoreMode != UIBlurLayerManager.Mode.None)
			{
				SetUIBlurCaptureModeCore(restoreMode).Forget();
			}
		}

		private void RemoveBlurTarget(BlurLayerTarget _target)
		{
			if (_target == null || !_target.EnableBlur)
			{
				return;
			}

			DisableBlur(_target);
			if (_blurTargets.Count > 0)
			{
				var backTarget = _blurTargets[_blurTargets.Count - 1];
				EnqueueBlur((backTarget, BlurStackDirection.Back));
				DequeueBlur().Forget();
			}
		}

		private bool IsEnabledBlurScene()
		{
			if (SceneManager.Instance == null)
			{
				return true;
			}

			return EnableBlurSceneList == null || EnableBlurSceneList.Count == 0 || EnableBlurSceneList.Contains(SceneManager.Instance.CurrentScene);
		}

		internal void EnableRenderArea(bool isRendering)
		{
			// Toggle ScreenLayerArea.AreaController.IsRenderArea while blur capture mode changes.
			// MusicScoreMaker does not currently instantiate/copy the large area stack, so keep this hook
			// dependency-free until area scenes are supported.
			if (_owner.RegisteredScreens == null)
			{
				return;
			}

			foreach (var state in _owner.RegisteredScreens.Values)
			{
				var layer = state?.ScreenLayer;
				if (layer == null || layer.GetType().Name != "ScreenLayerArea")
				{
					continue;
				}

				var areaControllerProperty = layer.GetType().GetProperty("AreaController", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
				var areaController = areaControllerProperty?.GetValue(layer);
				if (areaController == null)
				{
					return;
				}

				var renderAreaProperty = areaController.GetType().GetProperty("IsRenderArea", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
				if (renderAreaProperty != null && renderAreaProperty.CanWrite)
				{
					renderAreaProperty.SetValue(areaController, isRendering);
				}
				return;
			}
		}
	}
}
