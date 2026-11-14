using System;
using System.Collections.Generic;
using System.Threading;
using Beebyte.Obfuscator;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sekai;

namespace Sekai.UI
{
	[AddComponentMenu("Custom UI/Custom Button")]
	public class CustomButton : Button
	{
		public enum ShapeType
		{
			Manual = 0,
			Rect_64_White = 1,
			Rect_104_Green = 2,
			Rect_104_White = 3,
			Square_112_Green = 4,
			Square_112_White = 5,
			Circle_72_White = 6,
			Circle_128_White = 7
		}

		public enum DisableActionType
		{
			None = 0,
			Grayout = 1
		}

		private const float PRESS_SCALE_RATIO = 0.95f;
		private const float PRESS_SCALE_DURATION = 0.08f;
		private const float RELEASE_SCALE_DURATION = 0.12f;
		private const float LONG_PRESS_SEC = 0.4f;

		[SerializeField]
		protected SeType se = SeType.Decide;

		[SerializeField]
		protected string otherSeName;

		[SerializeField]
		protected InputManager.IntervalUseType interval = InputManager.IntervalUseType.Use;

		[SerializeField]
		private bool _absolutelyPress;

		[SerializeField]
		private bool _enableLongPress;

		[SerializeField]
		private ButtonViewInteractionBase _buttonViewInteraction;

		[SerializeField]
		private ShapeType _shape;

		[SerializeField]
		private CustomImage _shapeButtonImage;

		[SerializeField]
		private CustomImage _shapeRectDotImage;

		[SerializeField]
		private CustomText _shapeRectText;

		[SerializeField]
		protected DisableActionType disableActionType = DisableActionType.Grayout;

		[SerializeField]
		private Graphic _coverImage;

		[SerializeField]
		private List<Graphic> _optionalCoverImages;

		[SerializeField]
		protected float pressScale = PRESS_SCALE_RATIO;

		protected Tweener pressTween;

				[SerializeField] private Action _OnLongPressEvent;
		public Action OnLongPressEvent
		{
		    get => _OnLongPressEvent;
		    set => _OnLongPressEvent = value;
		}

		private bool _executedLongPress;

				[SerializeField] private Action _OnHoldPressEvent;
		public Action OnHoldPressEvent
		{
		    get => _OnHoldPressEvent;
		    set => _OnHoldPressEvent = value;
		}

				[SerializeField] private Action _OnPressInteractionOverride;
		public Action OnPressInteractionOverride
		{
		    get => _OnPressInteractionOverride;
		    set => _OnPressInteractionOverride = value;
		}

				[SerializeField] private Action _OnReleaseInteractionOverride;
		public Action OnReleaseInteractionOverride
		{
		    get => _OnReleaseInteractionOverride;
		    set => _OnReleaseInteractionOverride = value;
		}

		private InputManager.ControlState _controlState;

		private CancellationTokenSource _holdRepeatCts;

		private CancellationTokenSource _longPressCts;

		[SerializeField]
		private bool _enableHoldRepeat;

		[SerializeField]
		private float _holdRepeatInterval = 0.2f;

		private bool _isHolding;

		private float _holdTimer;

		public Action OnHoldRepeatEvent { get; set; }

		public SeType SE
		{
			get { return se; }
			set { se = value; }
		}

		public ShapeType Shape
		{
			get { return _shape; }
			set { _shape = value; }
		}

		public CustomImage ShapeButtonImage
		{
			get { return _shapeButtonImage; }
			set { _shapeButtonImage = value; }
		}

		public CustomImage ShapeRectDotImage
		{
			get { return _shapeRectDotImage; }
			set { _shapeRectDotImage = value; }
		}

		public CustomText ShapeRectText
		{
			get { return _shapeRectText; }
			set { _shapeRectText = value; }
		}

		public Graphic CoverImage
		{
			get { return _coverImage; }
			set { _coverImage = value; }
		}

		public Vector3 BaseScale { get; set; }

		public bool EnableLongPress
		{
			get { return _enableLongPress; }
			set { _enableLongPress = value; }
		}

		public bool EnableHoldRepeat
		{
			get { return _enableHoldRepeat; }
			set { _enableHoldRepeat = value; }
		}

		public float HoldRepeatInterval
		{
			get { return _holdRepeatInterval; }
			set { _holdRepeatInterval = value; }
		}

		protected override void Awake()
		{
			base.Awake();
			BaseScale = transform.localScale;
			// AssetRipper kept the TapEffect objects, but some restored prefabs were
			// serialized while CustomButton was still a stub and lost this field.
			_buttonViewInteraction ??= GetComponentInChildren<ButtonViewInteractionBase>(true);
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			HideCover();
			_buttonViewInteraction?.OnButtonViewEnabled();
			_buttonViewInteraction?.ResetInteraction();
			ResetScale();
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			StopHoldRepeat();
			StopLongPressCheck();
			if (disableActionType == DisableActionType.Grayout)
			{
				ShowCover();
			}
			_buttonViewInteraction?.OnButtonViewDisabled();
			if (_controlState == InputManager.ControlState.Press)
			{
				OnFinishedControlSelectable();
			}
			else
			{
				_controlState = InputManager.ControlState.NoControl;
			}
			_buttonViewInteraction?.ResetInteraction();
		}

		private void OnFinishedControlSelectable()
		{
			if (_controlState == InputManager.ControlState.Press)
			{
				PlayReleaseEffect();
			}
			_controlState = InputManager.ControlState.NoControl;
		}

		public void SetHoldRepeatEvent(Action action)
		{
			OnHoldRepeatEvent = action;
		}

		public void StopHoldRepeat()
		{
			_isHolding = false;
			_holdTimer = 0f;
			if (_holdRepeatCts != null)
			{
				_holdRepeatCts.Cancel();
				_holdRepeatCts.Dispose();
				_holdRepeatCts = null;
			}
		}

		[Skip]
		public override void OnPointerClick(PointerEventData eventData)
		{
			if (!_executedLongPress || _absolutelyPress)
			{
				if (CustomSelectableDefine.CheckPointerClickAction(this, interval, se, otherSeName))
				{
					base.OnPointerClick(eventData);
				}
			}
			_executedLongPress = false;
		}

		public override void OnPointerDown(PointerEventData eventData)
		{
			base.OnPointerDown(eventData);
			if (!IsInteractable())
			{
				return;
			}

			_controlState = InputManager.ControlState.Press;
			CustomPointerDown();
			PlayPressEffect();
			StartLongPressCheck();
			StartHoldRepeat();
		}

		public override void OnPointerUp(PointerEventData eventData)
		{
			base.OnPointerUp(eventData);
			CustomPointerUp();
			StopLongPressCheck();
			StopHoldRepeat();
			if (_controlState == InputManager.ControlState.Press)
			{
				PlayReleaseEffect();
			}
			_controlState = InputManager.ControlState.ClickCheck;
		}

		public override void OnPointerExit(PointerEventData eventData)
		{
			base.OnPointerExit(eventData);
			StopLongPressCheck();
			StopHoldRepeat();
			if (_controlState == InputManager.ControlState.Press)
			{
				PlayReleaseEffect();
			}
			_controlState = InputManager.ControlState.NoControl;
		}

		public void ShowCover()
		{
			SetCoverActive(true);
		}

		public void HideCover()
		{
			SetCoverActive(false);
		}

		private void SetCoverActive(bool isActive)
		{
			Color color = global::Sekai.ColorUtility.COLOR_DISABLE_ALPHA_50;
			if (_coverImage != null)
			{
				_coverImage.color = color;
				_coverImage.gameObject.SetActive(isActive);
			}
			if (_optionalCoverImages == null)
			{
				return;
			}
			for (int i = 0; i < _optionalCoverImages.Count; i++)
			{
				Graphic graphic = _optionalCoverImages[i];
				if (graphic != null)
				{
					graphic.color = color;
					graphic.gameObject.SetActive(isActive);
				}
			}
		}

		private void PlayPressEffect()
		{
			if (OnPressInteractionOverride != null)
			{
				OnPressInteractionOverride();
				return;
			}
			if (_buttonViewInteraction != null)
			{
				_buttonViewInteraction.OnPressed();
			}
		}

		private void PlayReleaseEffect()
		{
			if (OnReleaseInteractionOverride != null)
			{
				OnReleaseInteractionOverride();
				return;
			}
			if (_buttonViewInteraction != null)
			{
				_buttonViewInteraction.OnReleased();
			}
		}

		private void StartLongPressCheck()
		{
			if (!_enableLongPress)
			{
				return;
			}

			StopLongPressCheck();
			_executedLongPress = false;
			_longPressCts = new CancellationTokenSource();
			LongPressUniTask(_longPressCts.Token).Forget();
		}

		private async UniTask LongPressUniTask(CancellationToken token)
		{
			try
			{
				await UniTask.Delay(TimeSpan.FromSeconds(LONG_PRESS_SEC), cancellationToken: token);
				if (token.IsCancellationRequested)
				{
					return;
				}
				_executedLongPress = true;
				if (OnLongPressEvent != null)
				{
					SoundManager.Instance.PlaySEOneShot("SE_DECIDE1", 0);
					OnLongPressEvent.Invoke();
				}
				if (OnHoldPressEvent != null)
				{
					CustomSelectableDefine.PlaySE(se, otherSeName);
					OnHoldPressEvent.Invoke();
				}
			}
			catch (OperationCanceledException)
			{
			}
		}

		private void StopLongPressCheck()
		{
			if (_longPressCts != null)
			{
				_longPressCts.Cancel();
				_longPressCts.Dispose();
				_longPressCts = null;
			}
		}

		private void StartHoldRepeat()
		{
			if (!_enableHoldRepeat || OnHoldRepeatEvent == null)
			{
				return;
			}

			StopHoldRepeat();
			_isHolding = true;
			_holdTimer = 0f;
			_holdRepeatCts = new CancellationTokenSource();
			HoldRepeatUniTask(_holdRepeatCts.Token).Forget();
		}

		private async UniTask HoldRepeatUniTask(CancellationToken token)
		{
			try
			{
				while (!token.IsCancellationRequested)
				{
					float intervalSec = Mathf.Max(0.01f, _holdRepeatInterval);
					await UniTask.Delay(TimeSpan.FromSeconds(intervalSec), cancellationToken: token);
					_holdTimer += intervalSec;
					if (_isHolding && !token.IsCancellationRequested)
					{
						OnHoldRepeatEvent?.Invoke();
					}
				}
			}
			catch (OperationCanceledException)
			{
			}
		}

		public void ResetScale()
		{
			KillPressTween();
			transform.localScale = BaseScale == Vector3.zero ? Vector3.one : BaseScale;
		}

		private void ResetScaleAnimated()
		{
			KillPressTween();
			Vector3 targetScale = BaseScale == Vector3.zero ? Vector3.one : BaseScale;
			pressTween = transform.DOScale(targetScale, RELEASE_SCALE_DURATION).SetEase(Ease.OutQuart);
		}

		public void ScalePointerDown()
		{
			KillPressTween();
			Vector3 baseScale = BaseScale == Vector3.zero ? Vector3.one : BaseScale;
			pressTween = transform.DOScale(baseScale * pressScale, PRESS_SCALE_DURATION).SetEase(Ease.OutQuart);
		}

		public void SetPressScaleRatio(float ratio)
		{
			pressScale = ratio;
		}

		protected virtual void CustomPointerDown()
		{
		}

		protected virtual void CustomPointerUp()
		{
		}

		protected virtual void KillPressTween()
		{
			if (pressTween != null && pressTween.IsActive())
			{
				pressTween.Kill(false);
			}
			pressTween = null;
			_buttonViewInteraction?.KillFadeTween();
		}

		public void SetInterval(InputManager.IntervalUseType intervalType)
		{
			interval = intervalType;
		}

		public void ChangeOtherSE(string seName)
		{
			otherSeName = seName;
			se = string.IsNullOrEmpty(seName) ? SeType.Decide : SeType.Other;
		}

		public void SetInteraction(ButtonViewInteractionBase interaction)
		{
			_buttonViewInteraction = interaction;
		}

		public void SetActive(bool isActive)
		{
			gameObject.SetActive(isActive);
		}
	}
}
