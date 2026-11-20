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
		private readonly float _openANIMATIONDURATION;
		private readonly float _closeANIMATIONDURATION;

		[UnityEngine.Serialization.FormerlySerializedAs("_canvasGroup")]
		[SerializeField]
		private CanvasGroup canvasGroup;

		private MotionHandle _motion;

		public override UniTask OpenAnimation(CancellationToken ct = default)
		{
			if (canvasGroup == null)
			{
				OpenImmediately();
				return UniTask.CompletedTask;
			}

			canvasGroup.blocksRaycasts = true;
			return FadeTo(1f, _openANIMATIONDURATION, ct);
		}

		public override void InitializeAnimation()
		{
			CancelFade();
			canvasGroup = canvasGroup != null ? canvasGroup : GetComponent<CanvasGroup>();
			if (canvasGroup == null)
			{
				return;
			}

			canvasGroup.alpha = 0f;
			canvasGroup.blocksRaycasts = false;
		}

		public override UniTask CloseAnimation(CancellationToken ct = default)
		{
			return CloseAnimation(_closeANIMATIONDURATION, ct);
		}

		public override UniTask CloseAnimation(float duration, CancellationToken ct = default)
		{
			if (canvasGroup == null)
			{
				CloseImmediately();
				return UniTask.CompletedTask;
			}

			canvasGroup.blocksRaycasts = false;
			return FadeTo(0f, duration, ct);
		}

		public override void OpenImmediately()
		{
			CancelFade();
			if (canvasGroup == null)
			{
				canvasGroup = GetComponent<CanvasGroup>();
			}
			if (canvasGroup == null)
			{
				return;
			}

			canvasGroup.alpha = 1f;
			canvasGroup.blocksRaycasts = true;
		}

		public override void CloseImmediately()
		{
			CancelFade();
			if (canvasGroup == null)
			{
				canvasGroup = GetComponent<CanvasGroup>();
			}
			if (canvasGroup == null)
			{
				return;
			}

			canvasGroup.alpha = 0f;
			canvasGroup.blocksRaycasts = false;
		}

		private async UniTask FadeTo(float alpha, float duration, CancellationToken ct)
		{
			CancelFade();
			ct.ThrowIfCancellationRequested();
			MotionHandle motion = LMotion.Create(canvasGroup.alpha, alpha, Mathf.Max(0f, duration))
				.WithEase(Ease.OutQuad)
				.BindToAlpha(canvasGroup);
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
			_openANIMATIONDURATION = 0.2f;
			_closeANIMATIONDURATION = 0.2f;
		}
	}
}
