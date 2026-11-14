using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using CP;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sekai.Live;
using Sekai.ScreenLayerExtension;

namespace Sekai
{
	[ExecuteInEditMode]
	public class ScreenManager : MonoBehaviour
	{
		public class SceneBackKeyHandler
		{
						private Action _OnExecuteBackKeyProcess;
			public Action OnExecuteBackKeyProcess
			{
			    get => _OnExecuteBackKeyProcess;
			    set => _OnExecuteBackKeyProcess = value;
			}

						private bool _EnableBackKeyProcess;
			public bool EnableBackKeyProcess
			{
			    get => _EnableBackKeyProcess;
			    set => _EnableBackKeyProcess = value;
			}

			public SceneBackKeyHandler()
			{
			}
		}

		public enum BlurStackDirection
		{
			Forward = 0,
			Back = 1
		}

		public enum BlurDialogProcessState
		{
			Ready = 0,
			Opening = 1
		}

		public class BlurLayerTarget : IEquatable<BlurLayerTarget>
		{
			private readonly IBlurTarget _target;

			private readonly GameObject _targetObject;

			private readonly DisplayLayerType _layerType;

			private IBlurTarget Target
			{
				[CompilerGenerated]
				get
				{
					return _target;
				}
			}

			public GameObject TargetObject
			{
				[CompilerGenerated]
				get
				{
					return _targetObject;
				}
			}

			public DisplayLayerType LayerType
			{
				[CompilerGenerated]
				get
				{
					return _layerType;
				}
			}

			public Canvas TargetCanvas
			{
				get
				{
					return _targetObject != null ? _targetObject.GetComponent<Canvas>() : null;
				}
			}

			public bool EnableBlur
			{
				get
				{
					return _target != null && _target.EnableBlur;
				}
			}

			public bool ExistsTarget
			{
				get
				{
					return _target != null && _targetObject != null;
				}
			}

			public BlurLayerTarget(IBlurTarget _target, GameObject _targetObject, DisplayLayerType _layerType)
			{
				this._target = _target;
				this._targetObject = _targetObject;
				this._layerType = _layerType;
				SetLayer(ScreenBlurController.DefaultLayerName);
			}

			public void AddCanvasCopyFrom(Canvas baseCanvas)
			{
				if (_targetObject == null || baseCanvas == null)
				{
					return;
				}

				var canvas = _targetObject.GetComponent<Canvas>();
				if (canvas == null)
				{
					canvas = _targetObject.AddComponent<Canvas>();
				}
				if (canvas == null)
				{
					return;
				}

				canvas.renderMode = baseCanvas.renderMode;
				canvas.worldCamera = baseCanvas.worldCamera;
				canvas.planeDistance = baseCanvas.planeDistance;
				canvas.overrideSorting = baseCanvas.overrideSorting;
				canvas.sortingLayerID = baseCanvas.sortingLayerID;
				canvas.sortingOrder = baseCanvas.sortingOrder + 1;

				if (baseCanvas.gameObject.GetComponent<GraphicRaycaster>() != null &&
				    _targetObject.GetComponent<GraphicRaycaster>() == null)
				{
					_targetObject.AddComponent<GraphicRaycaster>();
				}
			}

			public void RemoveCanvasIfExists()
			{
				if (_targetObject == null)
				{
					return;
				}

				var raycaster = _targetObject.GetComponent<GraphicRaycaster>();
				if (raycaster != null)
				{
					if (Application.isPlaying)
					{
						UnityEngine.Object.Destroy(raycaster);
					}
					else
					{
						UnityEngine.Object.DestroyImmediate(raycaster);
					}
				}

				var canvas = _targetObject.GetComponent<Canvas>();
				if (canvas != null)
				{
					if (Application.isPlaying)
					{
						UnityEngine.Object.Destroy(canvas);
					}
					else
					{
						UnityEngine.Object.DestroyImmediate(canvas);
					}
				}
			}

			public bool Equals(BlurLayerTarget other)
			{
				return other != null && _targetObject == other._targetObject && _layerType.Equals(other._layerType);
			}

			private void SetLayer(string layerName)
			{
				if (_targetObject == null)
				{
					return;
				}

				var targetLayer = LayerMask.NameToLayer(layerName);
				if (targetLayer < 0)
				{
					return;
				}

				SetLayerRecursive(_targetObject.transform, targetLayer);
			}

			private static void SetLayerRecursive(Transform _target, int layer)
			{
				if (_target == null)
				{
					return;
				}

				_target.gameObject.layer = layer;
				for (var i = 0; i < _target.childCount; i++)
				{
					SetLayerRecursive(_target.GetChild(i), layer);
				}
			}
		}

		public const float SCREEN_TRANSITION_TIME = 0.2f;
		public static readonly Vector2 BASE_SCREEN_SIZE = new Vector2(1920f, 1080f);

		private static ScreenManager _instance;
		private readonly ScreenStackService _stack;
		private readonly TransitionService _transition;
		private readonly DialogService _dialogs;
		private readonly Dictionary<SceneManager.Scene, SceneBackKeyHandler> _sceneBackKeyHandlers =
			new Dictionary<SceneManager.Scene, SceneBackKeyHandler>();
		private Vector2 _screenToUIRatio;
		private Vector2 _baseScreenToUIRatio;
		private RectTransform _rootRect;
		private Canvas _rootCanvas;
		private ScreenCanvasScaler _rootCanvasScaler;
		private float _rootScale;
		private float _inverseRootScale;
		private Vector2 _contentSize;
		private ScreenConfig.ScreenSize _screenSize;

		[SerializeField] private Action<bool> _OnBackUIScreenOverride;
		[SerializeField] private bool _IsAutoClearBackUIScreenOverride = true;
		[SerializeField] private Action<MenuScreenType> _OnChangeUILayer;
		[SerializeField] private Action<ScreenConfig.ScreenSize> _OnChangeScreenSize;
		[SerializeField] private Action _OnExecuteBackKeyProcess;
		[SerializeField] private List<MenuScreenType> _MaintainableScreenList = new List<MenuScreenType>
		{
			(MenuScreenType)26, (MenuScreenType)37, (MenuScreenType)123, (MenuScreenType)89, (MenuScreenType)77
		};

		protected ScreenLayerDataList entryScreenLayers;
		protected Dictionary<MenuScreenType, ScreenLayerState> screenMap;
		[SerializeField] protected ColorFader fadeController;
		[SerializeField] private int _uiCameraPriority;

		public ScreenManager()
		{
			_stack = new ScreenStackService(this);
			_transition = new TransitionService(this);
			_dialogs = new DialogService(this, _transition.Blur);
			Header = new HeaderService(this);
		}

		internal HeaderService Header { get; }
		internal Dictionary<MenuScreenType, ScreenLayerState> RegisteredScreens => screenMap;
		internal ColorFader FadeController { get => fadeController; set => fadeController = value; }
		internal int UICameraPriority => _uiCameraPriority;

		public Action<bool> OnBackUIScreenOverride { get => _OnBackUIScreenOverride; set => _OnBackUIScreenOverride = value; }
		public bool IsAutoClearBackUIScreenOverride { get => _IsAutoClearBackUIScreenOverride; set => _IsAutoClearBackUIScreenOverride = value; }
		public Action<MenuScreenType> OnChangeUILayer { get => _OnChangeUILayer; set => _OnChangeUILayer = value; }
		public Action<ScreenConfig.ScreenSize> OnChangeScreenSize { get => _OnChangeScreenSize; set => _OnChangeScreenSize = value; }
		public Action OnExecuteBackKeyProcess { get => _OnExecuteBackKeyProcess; set => _OnExecuteBackKeyProcess = value; }
		public List<MenuScreenType> MaintainableScreenList { get => _MaintainableScreenList; set => _MaintainableScreenList = value; }
		public Action OnExecuteMysekaiBackKeyProcess { get; set; }
		public bool EnableMysekaiBackKeyProcess { get; set; }
		public object BootArgObject { get; set; }
		public BackKeyChecker BackKeyChecker { get; set; }

		public static bool ExistsInstance => _instance != null;
		public Vector2 ScreenToUIRatio => _screenToUIRatio;
		public Vector2 ContentSize => _contentSize;
		public Vector2 BaseScreenToUIRatio => _baseScreenToUIRatio;
		public float RootScale => _rootScale;
		public float InverseRootScale => _inverseRootScale;
		public Canvas RootCanvas => _rootCanvas;
		public Camera RenderCamera => _transition.RenderCamera;
		public bool IsUILayerWorking => _stack.IsUILayerWorking;
		public LimitedStack<Tuple<ScreenLayerState, object, ScreenLayer.BootArgBase>> UiScreenStack => _stack.UiScreenStack;
		public bool IsPlayFading => _transition.IsPlayFading;
		public ScreenCameraStack CameraStack => _transition.CameraStack;
		public bool IsActiveDialogQueue => _dialogs.IsActiveDialogQueue;
		public bool IsContentSizeFixed => _contentSize.x > 0f && _contentSize.y > 0f;
		public bool IsPopUIScreen => _stack.IsPopUIScreen;

		private void Awake()
		{
			SetupInstance(this);

			BackKeyChecker = new BackKeyChecker();
			BackKeyChecker.Initialize(OnBackKey);
			entryScreenLayers = ScreenStackService.LoadEntryScreenLayers();
			screenMap = ScreenStackService.CreateRegistry(entryScreenLayers);
			OnChangeUILayer = null;

			_rootCanvas = GetComponent<Canvas>();
			_rootCanvasScaler = GetComponent<ScreenCanvasScaler>();
			if (_rootCanvas != null)
			{
				_rootRect = _rootCanvas.GetComponent<RectTransform>();
			}

			_transition.InitializeLayers(GetComponent<CanvasGroup>());
			SetUpScreenResolution();
			_transition.InitializeCameras();

			_stack.InitializeStack();
			_screenSize = new ScreenConfig.ScreenSize { width = Screen.width, height = Screen.height };
			OnChangeScreenSize += ForceUpdateScreenResolution;
		}

		public static ScreenManager Instance
		{
			get
			{
				if (_instance == null)
				{
					var canvasRoot = GameObject.Find("CanvasRoot");
					if (canvasRoot != null)
					{
						_instance = canvasRoot.GetComponent<ScreenManager>();
					}
				}

				return _instance;
			}
		}

		public Action GetSceneExecuteBackKeyProcess(SceneManager.Scene scene)
		{
			return GetSceneBackKeyHandler(scene).OnExecuteBackKeyProcess;
		}

		public void SetSceneExecuteBackKeyProcess(SceneManager.Scene scene, Action action)
		{
			GetSceneBackKeyHandler(scene).OnExecuteBackKeyProcess = action;
		}

		public bool GetSceneEnableBackKeyProcess(SceneManager.Scene scene)
		{
			return GetSceneBackKeyHandler(scene).EnableBackKeyProcess;
		}

		public void SetSceneEnableBackKeyProcess(SceneManager.Scene scene, bool enable)
		{
			GetSceneBackKeyHandler(scene).EnableBackKeyProcess = enable;
		}

		public void RemoveSceneBackKeyHandler(SceneManager.Scene scene)
		{
			_sceneBackKeyHandlers?.Remove(scene);
		}

		public SceneBackKeyHandler GetSceneBackKeyHandler(SceneManager.Scene scene)
		{
			if (!_sceneBackKeyHandlers.TryGetValue(scene, out var handler))
			{
				handler = new SceneBackKeyHandler();
				_sceneBackKeyHandlers[scene] = handler;
			}

			return handler;
		}

		public static void SetupInstance(ScreenManager _target)
		{
			_instance = _target;
		}

		private void Start()
		{
			CalcContentsSize();
		}

		private void SetUpScreenResolution()
		{
			if (_rootCanvasScaler != null)
			{
				var screenAspect = Screen.width > 0 ? (float)Screen.height / Screen.width : BASE_SCREEN_SIZE.y / BASE_SCREEN_SIZE.x;
				_rootCanvasScaler.matchWidthOrHeight = screenAspect >= BASE_SCREEN_SIZE.y / BASE_SCREEN_SIZE.x ? 0f : 1f;
				_rootCanvasScaler.ForceUpdate();
			}

			Canvas.ForceUpdateCanvases();
			if (_rootRect != null)
			{
				_rootRect.ForceUpdateRectTransforms();
			}
		}

		private void CalcContentsSize()
		{
			if (_rootRect == null)
			{
				_contentSize = BASE_SCREEN_SIZE;
				_screenToUIRatio = Vector2.one;
				_baseScreenToUIRatio = Vector2.one;
				_rootScale = 1f;
				_inverseRootScale = 1f;
				return;
			}

			var size = _rootRect.sizeDelta;
			_screenToUIRatio = new Vector2(
				Screen.width > 0 ? size.x / Screen.width : 1f,
				Screen.height > 0 ? size.y / Screen.height : 1f);
			_rootScale = transform.localScale.x;
			_inverseRootScale = _rootCanvas != null ? 1f / _rootCanvas.transform.localScale.x : 1f;
			_contentSize = size;
			_baseScreenToUIRatio = new Vector2(
				BASE_SCREEN_SIZE.x != 0f ? size.x / BASE_SCREEN_SIZE.x : 1f,
				BASE_SCREEN_SIZE.y != 0f ? size.y / BASE_SCREEN_SIZE.y : 1f);
		}

		private void ForceUpdateScreenResolution(ScreenConfig.ScreenSize _screenSize)
		{
			SetUpScreenResolution();
			CalcContentsSize();
		}

		private void Update()
		{
			if (Screen.width != _screenSize.width || Screen.height != _screenSize.height)
			{
				_screenSize.width = Screen.width;
				_screenSize.height = Screen.height;
				OnChangeScreenSize?.Invoke(_screenSize);
			}

			BackKeyChecker?.Check();
		}

		private void OnDestroy()
		{
			screenMap?.Clear();
			screenMap = null;
			if (_instance == this)
			{
				_instance = null;
			}
		}

		public Vector2 WorldToScreenPoint(Vector3 position, Vector2? referencePoint = null)
		{
			var camera = RenderCamera != null ? RenderCamera : Camera.main;
			if (camera == null)
			{
				return referencePoint ?? Vector2.zero;
			}

			var screenPoint = camera.WorldToScreenPoint(position);
			var uiPoint = new Vector2(screenPoint.x * _screenToUIRatio.x, screenPoint.y * _screenToUIRatio.y);
			return referencePoint.HasValue ? uiPoint - referencePoint.Value : uiPoint;
		}

		public T GetBootData<T>()
		{
			if (BootArgObject is T value)
			{
				return value;
			}

			return default;
		}

		public void ShowBackground(Action<CrossFader.FinishedStatus> onFinished = null)
		{
			throw null;
		}

		public void ShowBackground(ScreenLayerBackground.BackgroundType type, Action<CrossFader.FinishedStatus> onFinished = null)
		{
			throw null;
		}

		public void ShowBackground(string bundleName, string fileName, Action<CrossFader.FinishedStatus> onFinished = null)
		{
			throw null;
		}

		public void ShowBackgroundSync(string bundleName, string fileName, Action<CrossFader.FinishedStatus> onFinished = null, ScreenLayerBackground.BackgroundType backgroundType = ScreenLayerBackground.BackgroundType.Unknown)
		{
			throw null;
		}

		public void ActiveArea(bool isActive, bool isRenderAreaSync = true)
		{
			throw null;
		}

		public void PlayArea()
		{
			throw null;
		}

		public void PauseArea()
		{
			throw null;
		}

		public void HideBackground(Action<CrossFader.FinishedStatus> onFinished = null, bool isPlayArea = true)
		{
			throw null;
		}

		public void HideBackgroundDisableBlur()
		{
			throw null;
		}

		public ScreenLayerBackground.BackgroundType GetCurrentBackgroundType()
		{
			throw null;
		}

		private void OnBackKey(List<RaycastResult> results)
		{
			if (EnableMysekaiBackKeyProcess)
			{
				OnExecuteMysekaiBackKeyProcess?.Invoke();
				return;
			}

			if (SceneManager.Instance != null && GetSceneEnableBackKeyProcess(SceneManager.Instance.CurrentScene))
			{
				GetSceneExecuteBackKeyProcess(SceneManager.Instance.CurrentScene)?.Invoke();
				return;
			}

			if (OnExecuteBackKeyProcess != null)
			{
				OnExecuteBackKeyProcess.Invoke();
				return;
			}

			BackUIScreen();
		}

		public void StartCoroutineInExternal(IEnumerator coroutine)
		{
			if (coroutine != null)
			{
				StartCoroutine(coroutine);
			}
		}

		public void SetupBgmPlayer(IBgmPlayer _layerBgmPlayer) => _stack.SetupBgmPlayer(_layerBgmPlayer);
		public void SetupLayerBackgroundChanger(IBackgroundChanger layerBgChanger) => _stack.SetupLayerBackgroundChanger(layerBgChanger);
		public void AddScreen(MenuScreenType screenType) => _stack.AddScreen(screenType, null, true);
		public void AddScreen(MenuScreenType screenType, bool isForward) => _stack.AddScreen(screenType, null, isForward);
		public void AddScreen(MenuScreenType screenType, ScreenLayer.BootArgBase bootArg) => _stack.AddScreen(screenType, bootArg, true);
		public void AddScreen(MenuScreenType screenType, ScreenLayer.BootArgBase bootArg, bool isForward) => _stack.AddScreen(screenType, bootArg, isForward);
		public UniTask AddScreenWithoutHeaderSetting(MenuScreenType screenType, ScreenLayer.BootArgBase bootArg = null, CancellationToken ct = default(CancellationToken)) => _stack.AddScreenWithoutHeaderSetting(screenType, bootArg, ct);
		public void RemoveScreen(MenuScreenType screenType) => _stack.RemoveScreen(screenType);
		public IEnumerator RemoveScreenCoroutine(MenuScreenType screenType) => _stack.RemoveScreenCoroutine(screenType);
		public UniTask RemoveScreenAsync(MenuScreenType screenType, CancellationToken ct = default(CancellationToken)) => _stack.RemoveScreenAsync(screenType, ct);
		public bool IsExistInScreenMap(MenuScreenType screenType) => _stack.IsExistInScreenMap(screenType);
		public void ChangeUIScreen(MenuScreenType screenType, bool isWaitExitAnimation = false, bool isForward = true, bool nextScreenAnimationIsForward = true, bool isClearBootDataOnExitCancel = false) => _stack.ChangeUIScreen(screenType, null, isWaitExitAnimation, isForward, nextScreenAnimationIsForward, isClearBootDataOnExitCancel);
		public void ChangeUIScreen(MenuScreenType screenType, ScreenLayer.BootArgBase bootArg, bool isWaitExitAnimation = false, bool isForward = true, bool nextScreenAnimationIsForward = true, bool isClearBootDataOnExitCancel = false) => _stack.ChangeUIScreen(screenType, bootArg, isWaitExitAnimation, isForward, nextScreenAnimationIsForward, isClearBootDataOnExitCancel);
		public void PushUIScreen(MenuScreenType screenType, bool isWaitExitAnimation = false) => _stack.PushUIScreen(screenType, null, isWaitExitAnimation);
		public void PushUIScreen(MenuScreenType screenType, ScreenLayer.BootArgBase bootArg, bool isWaitExitAnimation = false) => _stack.PushUIScreen(screenType, bootArg, isWaitExitAnimation);
		public void BackUIScreen(bool isWaitExitAnimation = false) => _stack.BackUIScreen(isWaitExitAnimation);
		public void BackUIScreenAndDestroyExitedScreen(MenuScreenType screenType, bool isWaitExitAnimation = false) => _stack.BackUIScreenAndDestroyExitedScreen(screenType, isWaitExitAnimation);
		public void ForceBackUIScreen(bool isWaitExitAnimation = false) => _stack.ForceBackUIScreen(isWaitExitAnimation);
		public void PreloadUIScreen(MenuScreenType screenType) => _stack.PreloadUIScreen(screenType);
		public void RebuildScreen(MenuScreenType screenType, Action<ScreenLayer> onFinished = null, bool isFoward = true, bool isClearBootDataOnExitCancel = false) => _stack.RebuildScreen(screenType, onFinished, isFoward, isClearBootDataOnExitCancel);
		public UniTask RebuildScreenAsync(MenuScreenType screenType, Action<ScreenLayer> onFinished = null, bool isFoward = true, bool isClearBootDataOnExitCancel = false) => _stack.RebuildScreenAsync(screenType, onFinished, isFoward, isClearBootDataOnExitCancel);
		public void ClearScreenStack() => _stack.ClearScreenStack();
		public void PopScreenStack() => _stack.PopScreenStack();
		public void PushScreenStack(MenuScreenType screenType, object screenObject = null) => _stack.PushScreenStack(screenType, screenObject);
		public List<MenuScreenType> GetScreenStackList() => _stack.GetScreenStackList();
		public void DestroyScreenInstance(MenuScreenType screenType) => _stack.DestroyScreenInstance(screenType);
		public T GetLayerComponent<T>(MenuScreenType screenType) where T : ScreenLayer => _stack.GetLayerComponent<T>(screenType);
		public bool IsActiveScreen(MenuScreenType screenType) => _stack.IsActiveScreen(screenType);
		public bool IsInstantiatedScreen(MenuScreenType screenType) => _stack.IsInstantiatedScreen(screenType);
		public MenuScreenType GetPeekScreenType() => _stack.GetPeekScreenType();
		public bool TryGetScreenLayerData(MenuScreenType screenType, out ScreenLayerData screenLayerData) => _stack.TryGetScreenLayerData(screenType, out screenLayerData);
		public MenuScreenType GetCurrentUIScreenType() => _stack.GetCurrentUIScreenType();
		public MenuScreenType GetNextUIScreenType() => _stack.GetNextUIScreenType();
		public MenuScreenType GetPrevUIScreenType() => _stack.GetPrevUIScreenType();
		public void ExitScene() => _stack.ExitScene();
		public void EnableRenderArea(bool isRendering) => _transition.Blur.EnableRenderArea(isRendering);

		public Camera GetUICamera() => _transition.GetUICamera();
		public Camera GetBackCamera() => _transition.GetBackCamera();
		public GameObject GetLayerObject(DisplayLayerType _layerType) => _transition.GetLayerObject(_layerType);
		public void SetScreenCoverDirect(Color color) => _transition.SetScreenCoverDirect(color);
		public void FadeIn(float delay, float duration, Action onFinish = null) => _transition.FadeIn(delay, duration, onFinish);
		public void FadeIn(Color color, float delay, float duration, Action onFinish = null) => _transition.FadeIn(color, delay, duration, onFinish);
		public void FadeOut(float delay, float duration, Action onFinish = null) => _transition.FadeOut(delay, duration, onFinish);
		public UniTask FadeOutAsync(float duration, CancellationToken token) => _transition.FadeOutAsync(duration, token);
		public void FadeOut(Color color, float delay, float duration, Action onFinish = null) => _transition.FadeOut(color, delay, duration, onFinish);
		public void WhiteOut(float delay, float duration, Action onFinish = null) => _transition.WhiteOut(delay, duration, onFinish);
		public void WhiteIn(float delay, float duration, Action onFinish = null) => _transition.WhiteIn(delay, duration, onFinish);
		public void FadeRoot(bool isShow, bool interactable, bool blockRaycasts = true, float duration = 0.2f, float delay = 0f, Action onFinish = null) => _transition.FadeRoot(isShow, interactable, blockRaycasts, duration, delay, onFinish);
		public void SetLayerCanvasGroup(DisplayLayerType layer, bool interactable, bool blockRaycasts = true) => _transition.SetLayerCanvasGroup(layer, interactable, blockRaycasts);
		public void FadeOutLayer(DisplayLayerType layer, float delay, float duration, Action onFinish = null) => _transition.FadeOutLayer(layer, delay, duration, onFinish);
		public void FadeInLayer(DisplayLayerType layer, float delay, float duration, Action onFinish = null) => _transition.FadeInLayer(layer, delay, duration, onFinish);
		public void EnableTapScreenForce() => _transition.EnableTapScreenForce();
		public void EnableTapScreenForce(DisplayLayerType layer) => _transition.EnableTapScreenForce(layer);
		public void DisableTapScreen(UnityEngine.Object commandObject) => _transition.DisableTapScreen(commandObject);
		public void DisableTapScreen(UnityEngine.Object commandObject, DisplayLayerType layer) => _transition.DisableTapScreen(commandObject, layer);
		public void EnableTapScreen(UnityEngine.Object commandObject) => _transition.EnableTapScreen(commandObject);
		public void EnableTapScreen(UnityEngine.Object commandObject, DisplayLayerType layer) => _transition.EnableTapScreen(commandObject, layer);
		public bool CanTapByRootCanvas() => _transition.CanTapByRootCanvas();
		public bool CanTapByDisplayLayerType(DisplayLayerType layer) => _transition.CanTapByDisplayLayerType(layer);
		public string GetDisplayLayerName(DisplayLayerType layerTye) => _transition.GetDisplayLayerName(layerTye);
		public Camera CreateEmptyBaseCamera() => _transition.CreateEmptyBaseCamera();
		public void SetBaseCamera(Camera camera) => _transition.SetBaseCamera(camera);
		public void AddToUICameraStack(Camera camera, int priority) => _transition.AddToUICameraStack(camera, priority);
		public void AddEffectOverlayLayer() => _transition.AddEffectOverlayLayer();
		public void RemoveEffectOverlayLayer() => _transition.RemoveEffectOverlayLayer();

		public UniTask SetUIBlurCaptureMode(UIBlurLayerManager.Mode mode) => _transition.Blur.SetUIBlurCaptureMode(mode);
		public void EnableBlur() => _transition.Blur.EnableBlur();
		public void DisableBlur() => _transition.Blur.DisableBlur();
		public void EnableBlurByScreenLayer(ScreenLayer screenLayer, DisplayLayerType _layerType) => _transition.Blur.EnableBlurByScreenLayer(screenLayer, _layerType);
		public void RemoveBlurByScreenLayer(ScreenLayer screenLayer, DisplayLayerType _layerType) => _transition.Blur.RemoveBlurByScreenLayer(screenLayer, _layerType);

		public void ShowModuleMaintenanceDialog() => _dialogs.ShowModuleMaintenanceDialog();
		public void ShowModuleMaintenanceSubWindowDialog() => _dialogs.ShowModuleMaintenanceSubWindowDialog();
		public T InstantiateDialog<T>(DialogType dialogType, DisplayLayerType _layerType) where T : DialogBase => _dialogs.InstantiateDialog<T>(dialogType, _layerType);
		public T InstantiateDialog<T>(DialogType dialogType, Transform parent) where T : DialogBase => _dialogs.InstantiateDialog<T>(dialogType, parent);
		public T ShowDialog<T>(DialogType dialogType, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog, DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true) where T : DialogBase => _dialogs.ShowDialog<T>(dialogType, _layerType, dialogSize, allowCloseExternal);
		public T Show1ButtonDialog<T>(DialogType dialogType, string messageBodyKey, string okButtonLabelKey, Action onClickOK, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog, DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true) where T : Common1ButtonDialog => _dialogs.Show1ButtonDialog<T>(dialogType, messageBodyKey, okButtonLabelKey, onClickOK, _layerType, dialogSize, allowCloseExternal);
		public T Show1ButtonDialog<T>(DialogType dialogType, string titleKey, string messageBodyKey, string okButtonLabelKey, Action onClickOK, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog, DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true) where T : Common1ButtonDialog => _dialogs.Show1ButtonDialog<T>(dialogType, titleKey, messageBodyKey, okButtonLabelKey, onClickOK, _layerType, dialogSize, allowCloseExternal);
		public T Show1ButtonDialog<T>(DialogType dialogType, Action onClickOK, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog, DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true) where T : Common1ButtonDialog => _dialogs.Show1ButtonDialog<T>(dialogType, onClickOK, _layerType, dialogSize, allowCloseExternal);
		public T Show2ButtonDialog<T>(DialogType dialogType, string messageBodyKey, string okButtonLabelKey, string cancelButtonLabelKey, Action onClickOK, Action onClickCancel, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog, DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true) where T : Common2ButtonDialog => _dialogs.Show2ButtonDialog<T>(dialogType, messageBodyKey, okButtonLabelKey, cancelButtonLabelKey, onClickOK, onClickCancel, _layerType, dialogSize, allowCloseExternal);
		public T Show2ButtonDialog<T>(DialogType dialogType, string titleKey, string messageBodyKey, string okButtonLabelKey, string cancelButtonLabelKey, Action onClickOK, Action onClickCancel, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog, DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true) where T : Common2ButtonDialog => _dialogs.Show2ButtonDialog<T>(dialogType, titleKey, messageBodyKey, okButtonLabelKey, cancelButtonLabelKey, onClickOK, onClickCancel, _layerType, dialogSize, allowCloseExternal);
		public T Show2ButtonDialog<T>(DialogType dialogType, Action onClickOK, Action onClickCancel, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog, DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true) where T : Common2ButtonDialog => _dialogs.Show2ButtonDialog<T>(dialogType, onClickOK, onClickCancel, _layerType, dialogSize, allowCloseExternal);
		public T Show2ButtonDialog<T>(DialogType dialogType) where T : Common2ButtonDialog => _dialogs.Show2ButtonDialog<T>(dialogType);
		public T ShowMultiButtonDialog<T>(DialogType dialogType, string messageBodyKey, Dictionary<string, string> labelKeyDic, Dictionary<string, Action> actionDic, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog, DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true) where T : CommonMultiButtonDialog => _dialogs.ShowMultiButtonDialog<T>(dialogType, messageBodyKey, labelKeyDic, actionDic, _layerType, dialogSize, allowCloseExternal);
		public T ShowMultiButtonDialog<T>(DialogType dialogType, Dictionary<string, Action> actionDic, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog, DialogSize dialogSize = DialogSize.Manual, bool allowCloseExternal = true) where T : CommonMultiButtonDialog => _dialogs.ShowMultiButtonDialog<T>(dialogType, actionDic, _layerType, dialogSize, allowCloseExternal);
		public T ShowSubWindowDialog<T>(string messageBody = null, Action onClose = null, bool allowCloseExternal = true, DialogType dialogType = DialogType.SubWindowDialog, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog) where T : SubWindowDialog => _dialogs.ShowSubWindowDialog<T>(messageBody, onClose, allowCloseExternal, dialogType, _layerType);
		public void CloseDialogs(DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog) => _dialogs.CloseDialogs(_layerType);
		public bool IsActiveDialog(string dialogName, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog) => _dialogs.IsActiveDialog(dialogName, _layerType);
		public DialogBase GetActiveDialog(string dialogName, DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog) => _dialogs.GetActiveDialog(dialogName, _layerType);
		public T GetActiveDialog<T>(DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog) where T : DialogBase => _dialogs.GetActiveDialog<T>(_layerType);
		public bool TryGetActiveDialog<T>(out T dialog) where T : DialogBase => _dialogs.TryGetActiveDialog<T>(out dialog);
		public bool TryGetActiveDialog<T>(DisplayLayerType _layerType, out T dialog) where T : DialogBase => _dialogs.TryGetActiveDialog<T>(_layerType, out dialog);
		public T[] GetActiveDialogs<T>(DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog) where T : DialogBase => _dialogs.GetActiveDialogs<T>(_layerType);
		public bool ExistsDialog(DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog) => _dialogs.ExistsDialog(_layerType);
		public DialogBase[] GetActiveDialogs(DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog) => _dialogs.GetActiveDialogs(_layerType);
		public bool IsActiveDialog<T>(DisplayLayerType _layerType = DisplayLayerType.Layer_Dialog) where T : UnityEngine.Object => _dialogs.IsActiveDialog<T>(_layerType);
		public void CloseCommonWebviewDialog(WebviewPageType type) => _dialogs.CloseCommonWebviewDialog(type);
		public ChainDialogPlayer CreateChainDialogPlayer() => _dialogs.CreateChainDialogPlayer();
		public void ShowOutOfPeriodPlayLoginStoryDialog(Action onClickOKCallback = null) => _dialogs.ShowOutOfPeriodPlayLoginStoryDialog(onClickOKCallback);

	}
}
