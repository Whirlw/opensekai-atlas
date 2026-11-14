using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Sekai
{
	public class SubWindowFadeAnimation : SubWindowAnimationBase
	{
		private readonly float OPEN_ANIMATION_DURATION;
		private readonly float CLOSE_ANIMATION_DURATION;

		[SerializeField]
		private CanvasGroup _canvasGroup;

		public override async UniTask OpenAnimation(CancellationToken ct = default)
		{
			if (_canvasGroup == null)
			{
				OpenImmediately();
				return;
			}

			_canvasGroup.DOKill();
			_canvasGroup.blocksRaycasts = true;
			_canvasGroup.DOFade(1f, OPEN_ANIMATION_DURATION);
			await UniTask.Delay((int)(OPEN_ANIMATION_DURATION * 1000f), cancellationToken: ct);
		}

		public override void InitializeAnimation()
		{
			_canvasGroup = _canvasGroup != null ? _canvasGroup : GetComponent<CanvasGroup>();
			if (_canvasGroup == null)
			{
				return;
			}

			_canvasGroup.DOKill();
			_canvasGroup.alpha = 0f;
			_canvasGroup.blocksRaycasts = false;
		}

		public override UniTask CloseAnimation(CancellationToken ct = default)
		{
			return CloseAnimation(CLOSE_ANIMATION_DURATION, ct);
		}

		public override async UniTask CloseAnimation(float duration, CancellationToken ct = default)
		{
			if (_canvasGroup == null)
			{
				CloseImmediately();
				return;
			}

			_canvasGroup.DOKill();
			_canvasGroup.blocksRaycasts = false;
			_canvasGroup.DOFade(0f, duration);
			await UniTask.Delay((int)(duration * 1000f), cancellationToken: ct);
		}

		public override void OpenImmediately()
		{
			if (_canvasGroup == null)
			{
				_canvasGroup = GetComponent<CanvasGroup>();
			}
			if (_canvasGroup == null)
			{
				return;
			}

			_canvasGroup.DOKill();
			_canvasGroup.alpha = 1f;
			_canvasGroup.blocksRaycasts = true;
		}

		public override void CloseImmediately()
		{
			if (_canvasGroup == null)
			{
				_canvasGroup = GetComponent<CanvasGroup>();
			}
			if (_canvasGroup == null)
			{
				return;
			}

			_canvasGroup.DOKill();
			_canvasGroup.alpha = 0f;
			_canvasGroup.blocksRaycasts = false;
		}

		public SubWindowFadeAnimation()
		{
			OPEN_ANIMATION_DURATION = 0.2f;
			CLOSE_ANIMATION_DURATION = 0.2f;
		}
	}
}
