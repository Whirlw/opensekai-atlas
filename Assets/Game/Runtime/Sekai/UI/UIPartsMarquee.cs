using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.UI
{
	public class UIPartsMarquee : MonoBehaviour
	{
		[Header("回転を考慮しなくてもよい場合はRectMask2D")]
		[SerializeField]
		private RectMask2D _mask2D;

		[SerializeField]
		private RectTransform _marqueeTarget;

		[SerializeField]
		private float _spd;

		[SerializeField]
		private float _wait_sec;

		private float _wait_sec_cnt;

		private bool _isWait;

		private bool _isWaitSec;

		[SerializeField]
		private bool _enableMarquee;

		[SerializeField]
		private bool _unconditionalMarquee;

		[SerializeField]
		private bool _waitBasePosition;

		[SerializeField]
		private bool _defaultFadeIn;

		[SerializeField]
		private float _waitBeforeSpd;

		private bool _isPlay;

		private bool _isMove;

		private bool _isMarqueeGroup;

		private float _initPositionX;

		private float _restartPositionX;

		private float _endPositionX;

		private int _loopNum;

		public Action OnCompleted
		{
			[CompilerGenerated]
			get
			{
				return _OnCompleted;
			}
			[CompilerGenerated]
			set
			{
				_OnCompleted = value;
			}
		}

		public Func<RectTransform, float> GetMarqueeTargetWidthHandler
		{
			[CompilerGenerated]
			get
			{
				return _GetMarqueeTargetWidthHandler;
			}
			[CompilerGenerated]
			set
			{
				_GetMarqueeTargetWidthHandler = value;
			}
		}

		[CompilerGenerated]
		private Action _OnCompleted;

		[CompilerGenerated]
		private Func<RectTransform, float> _GetMarqueeTargetWidthHandler;

		public bool EnableMarquee
		{
			get
			{
				return _enableMarquee;
			}
			set
			{
				if (value && !_enableMarquee)
				{
					Init();
				}

				if (_enableMarquee != value && _marqueeTarget != null)
				{
					var position = _marqueeTarget.anchoredPosition;
					position.x = GetInitPositionX();
					_marqueeTarget.anchoredPosition = position;
				}

				_enableMarquee = value;
				SetEnabledMask(value);
			}
		}

		public bool IsMove
		{
			get
			{
				return _isMove;
			}
		}

		public bool IsMarqueeGroup
		{
			get
			{
				return _isMarqueeGroup;
			}
			set
			{
				_isMarqueeGroup = value;
			}
		}

		public bool IsPlay
		{
			get
			{
				return _isPlay;
			}
			set
			{
				_isPlay = value;
			}
		}

		public void Enable()
		{
			enabled = true;
		}

		public void Disable()
		{
			enabled = false;
		}

		public void SetEnabledMask(bool enable)
		{
			if (_mask2D != null)
			{
				_mask2D.enabled = enable;
			}
		}

		private void Awake()
		{
			if (GetMarqueeTargetWidthHandler == null)
			{
				GetMarqueeTargetWidthHandler = GetMarqueeTargetWidthDefault;
			}
			if (_enableMarquee)
			{
				Init();
			}
		}

		private void OnEnable()
		{
			if (_enableMarquee)
			{
				Init();
			}
		}

		public void SetUnconditionalMarquee(bool flag)
		{
			_unconditionalMarquee = flag;
		}

		public void Init()
		{
			if (_marqueeTarget == null)
			{
				_marqueeTarget = transform as RectTransform;
			}

			if (_marqueeTarget == null)
			{
				return;
			}

			if (NeedsMarqueeMoveWidth() || _unconditionalMarquee)
			{
				_isMove = true;
				_initPositionX = GetInitPositionX();
				_restartPositionX = GetRestartPositionX();
				_endPositionX = GetEndPositionX();
				if (_waitBasePosition)
				{
					_wait_sec_cnt = 0f;
					_isWait = true;
				}

				if (_defaultFadeIn)
				{
					_isWait = false;
					Restart();
					return;
				}
			}
			else
			{
				_isMove = false;
				_initPositionX = GetInitPositionX();
			}

			var position = _marqueeTarget.anchoredPosition;
			position.x = _initPositionX;
			_marqueeTarget.anchoredPosition = position;
		}

		public void Restart()
		{
			if (!(NeedsMarqueeMoveWidth() || _unconditionalMarquee) || _marqueeTarget == null)
			{
				return;
			}

			_isMove = true;
			var position = _marqueeTarget.anchoredPosition;
			position.x = _restartPositionX;
			_marqueeTarget.anchoredPosition = position;
			if (_waitBasePosition)
			{
				_wait_sec_cnt = 0f;
			}
		}

		public bool NeedsMarqueeMoveWidth()
		{
			if (_marqueeTarget == null)
			{
				return false;
			}

			var handler = GetMarqueeTargetWidthHandler ?? GetMarqueeTargetWidthDefault;
			return GetMaskRect().width < handler(_marqueeTarget);
		}

		public void SetActive(bool isActive)
		{
			gameObject.SetActive(isActive);
		}

		private void End()
		{
			if (_isMarqueeGroup)
			{
				_isMove = false;
			}
			else
			{
				Restart();
			}

			OnCompleted?.Invoke();
		}

		private void Update()
		{
			if (_enableMarquee)
			{
				UpdateMarquee();
			}
		}

		private void UpdateMarquee()
		{
			if (!(NeedsMarqueeMoveWidth() || _unconditionalMarquee) || !_isMove || !_isPlay || _marqueeTarget == null)
			{
				return;
			}

			if (_isWait)
			{
				_wait_sec_cnt += Time.deltaTime;
				if (_wait_sec_cnt < _wait_sec)
				{
					return;
				}

				_isWait = false;
			}

			var position = _marqueeTarget.anchoredPosition;
			var moveSpeed = _waitBasePosition && position.x > 0f ? _waitBeforeSpd : _spd;
			position.x -= moveSpeed * Time.deltaTime;
			if (position.x >= _endPositionX)
			{
				if (_wait_sec_cnt < _wait_sec && _marqueeTarget.anchoredPosition.x <= 0f)
				{
					position.x = 0f;
					_isWait = true;
				}

				_marqueeTarget.anchoredPosition = position;
				return;
			}

			position.x = _endPositionX;
			_marqueeTarget.anchoredPosition = position;
			End();
		}

		private Rect GetMaskRect()
		{
			if (_mask2D != null)
			{
				return _mask2D.rectTransform.rect;
			}

			return _marqueeTarget != null ? _marqueeTarget.rect : default;
		}

		private float GetInitPositionX()
		{
			if (_marqueeTarget == null)
			{
				return 0f;
			}

			var handler = GetMarqueeTargetWidthHandler ?? GetMarqueeTargetWidthDefault;
			return _marqueeTarget.pivot.x * handler(_marqueeTarget);
		}

		private float GetRestartPositionX()
		{
			return GetInitPositionX() + GetMaskRect().width;
		}

		private float GetEndPositionX()
		{
			var handler = GetMarqueeTargetWidthHandler ?? GetMarqueeTargetWidthDefault;
			return _marqueeTarget != null ? GetInitPositionX() - handler(_marqueeTarget) : 0f;
		}

		private float GetMarqueeTargetWidthDefault(RectTransform target)
		{
			return target != null ? target.rect.width : 0f;
		}

		public UIPartsMarquee()
		{
			_spd = 200f;
			_wait_sec = 0.5f;
			_waitBeforeSpd = 400f;
			_isPlay = true;
		}
	}
}
