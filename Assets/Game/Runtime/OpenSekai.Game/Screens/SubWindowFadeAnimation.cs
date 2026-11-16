using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;

namespace Sekai
{
	public class SubWindowFadeAnimation : SubWindowAnimationBase
	{
		private readonly float OPEN_ANIMATION_DURATION;
		private readonly float CLOSE_ANIMATION_DURATION;

		[SerializeField]
		private CanvasGroup _canvasGroup;

		private MotionHandle _motion;

		public override UniTask OpenAnimation(CancellationToken ct = default)
		{
			if (_canvasGroup == null)
			{
				OpenImmediately();
				return UniTask.CompletedTask;
			}

			_canvasGroup.blocksRaycasts = true;
			return FadeTo(1f, OPEN_ANIMATION_DURATION, ct);
		}

		public override void InitializeAnimation()
		{
			CancelFade();
			_canvasGroup = _canvasGroup != null ? _canvasGroup : GetComponent<CanvasGroup>();
			if (_canvasGroup == null)
			{
				return;
			}

			_canvasGroup.alpha = 0f;
			_canvasGroup.blocksRaycasts = false;
		}

		public override UniTask CloseAnimation(CancellationToken ct = default)
		{
			return CloseAnimation(CLOSE_ANIMATION_DURATION, ct);
		}

		public override UniTask CloseAnimation(float duration, CancellationToken ct = default)
		{
			if (_canvasGroup == null)
			{
				CloseImmediately();
				return UniTask.CompletedTask;
			}

			_canvasGroup.blocksRaycasts = false;
			return FadeTo(0f, duration, ct);
		}

		public override void OpenImmediately()
		{
			CancelFade();
			if (_canvasGroup == null)
			{
				_canvasGroup = GetComponent<CanvasGroup>();
			}
			if (_canvasGroup == null)
			{
				return;
			}

			_canvasGroup.alpha = 1f;
			_canvasGroup.blocksRaycasts = true;
		}

		public override void CloseImmediately()
		{
			CancelFade();
			if (_canvasGroup == null)
			{
				_canvasGroup = GetComponent<CanvasGroup>();
			}
			if (_canvasGroup == null)
			{
				return;
			}

			_canvasGroup.alpha = 0f;
			_canvasGroup.blocksRaycasts = false;
		}

		private async UniTask FadeTo(float alpha, float duration, CancellationToken ct)
		{
			CancelFade();
			ct.ThrowIfCancellationRequested();
			MotionHandle motion = LMotion.Create(_canvasGroup.alpha, alpha, Mathf.Max(0f, duration))
				.WithEase(Ease.OutQuad)
				.BindToAlpha(_canvasGroup);
			_motion = motion;
			try
			{
				await UniTask.WaitUntil(motion, static handle => !handle.IsActive(), cancellationToken: ct);
			}
			catch (OperationCanceledException)
			{
				motion.TryCancel();
				throw;
			}
		}

		private void CancelFade()
		{
			_motion.TryCancel();
			_motion = default;
		}

		private void OnDisable()
		{
			CancelFade();
		}

		public SubWindowFadeAnimation()
		{
			OPEN_ANIMATION_DURATION = 0.2f;
			CLOSE_ANIMATION_DURATION = 0.2f;
		}
	}
}
