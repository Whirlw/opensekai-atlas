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

		[UnityEngine.Serialization.FormerlySerializedAs("_subWindowComponent")]
		[SerializeField]
		private SubWindowComponent subWindowComponent;

		[UnityEngine.Serialization.FormerlySerializedAs("_slideOffset")]
		[SerializeField]
		private Vector2 slideOffset;

		[UnityEngine.Serialization.FormerlySerializedAs("_slideType")]
		[SerializeField]
		private SlideType slideType;

		private ScreenSlideInOut _slideInOut;

		private Vector2 SlideSize
		{
			get
			{
				Vector2 size = Vector2.zero;
				if (subWindowComponent != null)
				{
					size = slideType == SlideType.ContentOnly ? subWindowComponent.ContentSize : subWindowComponent.WindowSize;
				}
				return size + slideOffset;
			}
		}

		public override void InitializeAnimation()
		{
			base.InitializeAnimation();
			if (subWindowComponent == null)
			{
				return;
			}

			_slideInOut = subWindowComponent.gameObject.AddComponent<ScreenSlideInOut>();
			_slideInOut.Setup(subWindowComponent.gameObject);
		}

		public override UniTask OpenAnimation(CancellationToken ct = default(CancellationToken))
		{
			if (_slideInOut == null || subWindowComponent == null)
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
			if (_slideInOut == null || subWindowComponent == null)
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
			_slideInOut?.Destory();
			RectTransform rectTransform = subWindowComponent != null ? subWindowComponent.transform as RectTransform : null;
			if (rectTransform != null)
			{
				rectTransform.anchoredPosition = GetCloseTarget();
			}
		}

		public override void OpenImmediately()
		{
			_slideInOut?.Destory();
			RectTransform rectTransform = subWindowComponent != null ? subWindowComponent.transform as RectTransform : null;
			if (rectTransform != null)
			{
				rectTransform.anchoredPosition = Vector2.zero;
			}
		}

		private Vector2 GetOpenStart()
		{
			if (subWindowComponent == null)
			{
				return Vector2.zero;
			}

			switch (subWindowComponent.GetWindowType)
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
			if (subWindowComponent == null)
			{
				return Vector2.zero;
			}

			switch (subWindowComponent.GetWindowType)
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
