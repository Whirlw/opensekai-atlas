using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sekai.SubWindow;
using UnityEngine;

namespace Sekai
{
	public class SubWindowSlideAnimation : SubWindowAnimationBase
	{
		[Serializable]
		private enum SlideType
		{
			WindowAll = 0,
			ContentOnly = 1
		}

		[SerializeField]
		private SubWindowComponent _subWindowComponent;

		[SerializeField]
		private Vector2 _slideOffset;

		[SerializeField]
		private SlideType _slideType;

		private ScreenSlideInOut _slideInOut;

		private Vector2 SlideSize
		{
			get
			{
				Vector2 size = Vector2.zero;
				if (_subWindowComponent != null)
				{
					size = _slideType == SlideType.ContentOnly ? _subWindowComponent.ContentSize : _subWindowComponent.WindowSize;
				}
				return size + _slideOffset;
			}
		}

		public override void InitializeAnimation()
		{
			base.InitializeAnimation();
			if (_subWindowComponent == null)
			{
				return;
			}

			_slideInOut = _subWindowComponent.gameObject.AddComponent<ScreenSlideInOut>();
			_slideInOut.Setup(_subWindowComponent.gameObject);
		}

		public override UniTask OpenAnimation(CancellationToken ct = default(CancellationToken))
		{
			if (_slideInOut == null || _subWindowComponent == null)
			{
				OpenImmediately();
				return UniTask.CompletedTask;
			}

			Vector2 openStart = GetOpenStart();
			_slideInOut.SetVector(new Vector3(openStart.x, openStart.y, 0f), Vector3.zero);
			return _slideInOut.Play(ct);
		}

		public override UniTask CloseAnimation(CancellationToken ct = default(CancellationToken))
		{
			if (_slideInOut == null || _subWindowComponent == null)
			{
				CloseImmediately();
				return UniTask.CompletedTask;
			}

			Vector2 closeTarget = GetCloseTarget();
			_slideInOut.SetVector(Vector3.zero, new Vector3(closeTarget.x, closeTarget.y, 0f));
			return _slideInOut.Play(ct);
		}

		public override void CloseImmediately()
		{
			RectTransform rectTransform = _subWindowComponent != null ? _subWindowComponent.transform as RectTransform : null;
			if (rectTransform != null)
			{
				rectTransform.anchoredPosition = GetCloseTarget();
			}
		}

		public override void OpenImmediately()
		{
			RectTransform rectTransform = _subWindowComponent != null ? _subWindowComponent.transform as RectTransform : null;
			if (rectTransform != null)
			{
				rectTransform.anchoredPosition = Vector2.zero;
			}
		}

		private Vector2 GetOpenStart()
		{
			if (_subWindowComponent == null)
			{
				return Vector2.zero;
			}

			switch (_subWindowComponent.GetWindowType)
			{
				case WindowType.Left:
					return new Vector2(-SlideSize.x, 0f);
				case WindowType.Right:
					return new Vector2(SlideSize.x, 0f);
				case WindowType.Bottom:
					return new Vector2(0f, -SlideSize.y);
				default:
					return Vector2.zero;
			}
		}

		private Vector2 GetCloseTarget()
		{
			if (_subWindowComponent == null)
			{
				return Vector2.zero;
			}

			switch (_subWindowComponent.GetWindowType)
			{
				case WindowType.Left:
					return new Vector2(-SlideSize.x, 0f);
				case WindowType.Right:
					return new Vector2(SlideSize.x, 0f);
				case WindowType.Bottom:
					return new Vector2(0f, -SlideSize.y);
				default:
					return Vector2.zero;
			}
		}

		public override void Complete()
		{
			if (_slideInOut != null)
			{
				_slideInOut.Complete();
			}
		}
	}
}
