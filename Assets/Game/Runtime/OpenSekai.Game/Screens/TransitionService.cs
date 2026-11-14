using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Sekai
{
	internal sealed class TransitionService
	{
		private readonly ScreenManager _owner;
		private readonly Color _fadeInColor = new Color(0f, 0f, 0f, 0f);
		private readonly Color _fadeOutColor = new Color(0f, 0f, 0f, 1f);
		private Camera _mainUICamera;
		private Camera _backUICamera;
		private CanvasGroup _rootCanvasGroup;
		private Dictionary<DisplayLayerType, GameObject> _displayLayersCache;
		private Dictionary<string, Dictionary<UnityEngine.Object, bool>> _disableTapCommandObjects;

		internal TransitionService(ScreenManager owner)
		{
			_owner = owner;
			Blur = new ScreenBlurController(owner);
		}

		internal ScreenBlurController Blur { get; }
		internal Camera RenderCamera => _mainUICamera != null ? _mainUICamera : GetUICamera();
		internal ScreenCameraStack CameraStack { get; private set; }

		internal bool IsPlayFading
		{
			get
			{
				var fader = ResolveFadeController();
				return fader != null && fader.IsPlayFading;
			}
		}

		internal void InitializeLayers(CanvasGroup rootCanvasGroup)
		{
			_rootCanvasGroup = rootCanvasGroup;
			_displayLayersCache = new Dictionary<DisplayLayerType, GameObject>();
			_disableTapCommandObjects = new Dictionary<string, Dictionary<UnityEngine.Object, bool>>();
			foreach (DisplayLayerType layerType in Enum.GetValues(typeof(DisplayLayerType)))
			{
				var layer = GetLayerObject(layerType);
				if (layer != null)
				{
					GetDisableCommandMap(layer.name);
				}
			}
			GetDisableCommandMap("all");
		}

		internal void InitializeCameras()
		{
			CameraStack = new ScreenCameraStack();
			var uiCamera = GetUICamera();
			if (uiCamera != null)
			{
				CameraStack.SetBaseCamera(uiCamera);
			}
		}

		internal Camera GetUICamera()
		{
			if (_mainUICamera == null)
			{
				_mainUICamera = _owner.GetComponentInChildren<Camera>(true);
			}

			return _mainUICamera;
		}

		internal Camera GetBackCamera()
		{
			return _backUICamera;
		}

		internal GameObject GetLayerObject(DisplayLayerType _layerType)
		{
			_displayLayersCache ??= new Dictionary<DisplayLayerType, GameObject>();
			if (_displayLayersCache.TryGetValue(_layerType, out var layerObject))
			{
				return layerObject;
			}

			var layerTransform = _owner.transform.Find(_layerType.ToString());
			if (layerTransform == null)
			{
				return null;
			}

			layerObject = layerTransform.gameObject;
			_displayLayersCache.Add(_layerType, layerObject);
			return layerObject;
		}

		private ColorFader ResolveFadeController()
		{
			if (_owner.FadeController != null)
			{
				return _owner.FadeController;
			}

			_owner.FadeController = _owner.GetComponentInChildren<ColorFader>(true);
			if (_owner.FadeController == null)
			{
				_owner.FadeController = UnityEngine.Object.FindFirstObjectByType<ColorFader>(FindObjectsInactive.Include);
			}

			return _owner.FadeController;
		}

		internal void SetScreenCoverDirect(Color color)
		{
			ResolveFadeController()?.Set(color);
		}

		internal void FadeIn(float delay, float duration, Action onFinish = null)
		{
			ResolveFadeController()?.Play(_fadeInColor, delay, duration, onFinish);
		}

		internal void FadeIn(Color color, float delay, float duration, Action onFinish = null)
		{
			ResolveFadeController()?.Play(color, delay, duration, onFinish);
		}

		internal void FadeOut(float delay, float duration, Action onFinish = null)
		{
			ResolveFadeController()?.Play(_fadeOutColor, delay, duration, onFinish);
		}
		internal UniTask FadeOutAsync(float duration, CancellationToken token)
		{
			if (!token.IsCancellationRequested)
			{
				FadeOut(0f, duration);
			}

			return UniTask.CompletedTask;
		}

		internal void FadeOut(Color color, float delay, float duration, Action onFinish = null)
		{
			ResolveFadeController()?.Play(color, delay, duration, onFinish);
		}

		internal void WhiteOut(float delay, float duration, Action onFinish = null)
		{
			ResolveFadeController()?.Play(Color.white, delay, duration, onFinish);
		}

		internal void WhiteIn(float delay, float duration, Action onFinish = null)
		{
			var color = Color.white;
			color.a = 0f;
			ResolveFadeController()?.Play(color, delay, duration, onFinish);
		}

		internal void FadeRoot(bool isShow, bool interactable, bool blockRaycasts = true, float duration = 0.2f, float delay = 0f, Action onFinish = null)
		{
			if (_rootCanvasGroup != null)
			{
				_rootCanvasGroup.alpha = isShow ? 1f : 0f;
				_rootCanvasGroup.interactable = interactable;
				_rootCanvasGroup.blocksRaycasts = blockRaycasts;
			}

			onFinish?.Invoke();
		}

		internal void SetLayerCanvasGroup(DisplayLayerType layer, bool interactable, bool blockRaycasts = true)
		{
			var layerObject = GetLayerObject(layer);
			if (layerObject == null)
			{
				return;
			}

			var canvasGroup = layerObject.GetComponent<CanvasGroup>() ?? layerObject.AddComponent<CanvasGroup>();
			canvasGroup.interactable = interactable;
			canvasGroup.blocksRaycasts = blockRaycasts;
		}

		internal void FadeOutLayer(DisplayLayerType layer, float delay, float duration, Action onFinish = null)
		{
			SetLayerCanvasGroup(layer, false, false);
			onFinish?.Invoke();
		}

		internal void FadeInLayer(DisplayLayerType layer, float delay, float duration, Action onFinish = null)
		{
			SetLayerCanvasGroup(layer, true, true);
			onFinish?.Invoke();
		}

		private Dictionary<UnityEngine.Object, bool> GetDisableCommandMap(string key)
		{
			_disableTapCommandObjects ??= new Dictionary<string, Dictionary<UnityEngine.Object, bool>>();
			if (!_disableTapCommandObjects.TryGetValue(key, out var map))
			{
				map = new Dictionary<UnityEngine.Object, bool>();
				_disableTapCommandObjects[key] = map;
			}

			return map;
		}

		internal void EnableTapScreenForce()
		{
			GetDisableCommandMap("all").Clear();
			if (_rootCanvasGroup != null)
			{
				_rootCanvasGroup.interactable = true;
				_rootCanvasGroup.blocksRaycasts = true;
			}
		}

		internal void EnableTapScreenForce(DisplayLayerType layer)
		{
			var layerObject = GetLayerObject(layer);
			if (layerObject == null)
			{
				return;
			}

			GetDisableCommandMap(layerObject.name).Clear();
			SetLayerCanvasGroup(layer, true, true);
		}

		internal void DisableTapScreen(UnityEngine.Object commandObject)
		{
			DisableTapScreen(commandObject, default);
		}

		internal void DisableTapScreen(UnityEngine.Object commandObject, DisplayLayerType layer)
		{
			if (commandObject == null)
			{
				return;
			}

			if (EqualityComparer<DisplayLayerType>.Default.Equals(layer, default))
			{
				GetDisableCommandMap("all")[commandObject] = true;
				if (_rootCanvasGroup != null)
				{
					_rootCanvasGroup.interactable = false;
					_rootCanvasGroup.blocksRaycasts = true;
				}
				return;
			}

			var layerObject = GetLayerObject(layer);
			if (layerObject != null)
			{
				GetDisableCommandMap(layerObject.name)[commandObject] = true;
				SetLayerCanvasGroup(layer, false, true);
			}
		}

		internal void EnableTapScreen(UnityEngine.Object commandObject)
		{
			EnableTapScreen(commandObject, default);
		}

		internal void EnableTapScreen(UnityEngine.Object commandObject, DisplayLayerType layer)
		{
			if (commandObject == null)
			{
				return;
			}

			if (EqualityComparer<DisplayLayerType>.Default.Equals(layer, default))
			{
				var map = GetDisableCommandMap("all");
				map.Remove(commandObject);
				if (_rootCanvasGroup != null && map.Count == 0)
				{
					_rootCanvasGroup.interactable = true;
					_rootCanvasGroup.blocksRaycasts = true;
				}
				return;
			}

			var layerObject = GetLayerObject(layer);
			if (layerObject != null)
			{
				var map = GetDisableCommandMap(layerObject.name);
				map.Remove(commandObject);
				if (map.Count == 0)
				{
					SetLayerCanvasGroup(layer, true, true);
				}
			}
		}

		internal bool CanTapByRootCanvas()
		{
			return _rootCanvasGroup == null || _rootCanvasGroup.interactable;
		}

		internal bool CanTapByDisplayLayerType(DisplayLayerType layer)
		{
			var layerObject = GetLayerObject(layer);
			if (layerObject == null)
			{
				return true;
			}

			var canvasGroup = layerObject.GetComponent<CanvasGroup>();
			return canvasGroup == null || canvasGroup.interactable;
		}

		internal string GetDisplayLayerName(DisplayLayerType layerTye)
		{
			return layerTye.ToString();
		}

		internal Camera CreateEmptyBaseCamera()
		{
			if (CameraStack != null)
			{
				var baseCamera = CameraStack.CreateEmptyBaseCamera(_owner.transform);
				SetBaseCamera(baseCamera);
				return baseCamera;
			}

			var cameraObject = new GameObject("BaseCamera");
			cameraObject.transform.SetParent(_owner.transform, false);
			var camera = cameraObject.AddComponent<Camera>();
			SetBaseCamera(camera);
			return camera;
		}

		internal void SetBaseCamera(Camera camera)
		{
			if (CameraStack != null)
			{
				CameraStack.SetBaseCamera(camera);
				var uiCamera = GetUICamera();
				if (uiCamera != null)
				{
					CameraStack.AddToStack(uiCamera, _owner.UICameraPriority);
				}
			}
			else
			{
				_backUICamera = camera;
			}
		}

		internal void AddToUICameraStack(Camera camera, int priority)
		{
			CameraStack?.AddToStack(camera, priority);
		}

		internal void AddEffectOverlayLayer()
		{
			throw null;
		}

		internal void RemoveEffectOverlayLayer()
		{
			throw null;
		}
	}
}
