using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Sekai
{
	public class ScreenSlideInOut : ScreenInOutAnimBase
	{
		private const float Duration = 0.2f;

		private Tweener _tweener;

		private Vector3 _from;

		private Vector3 _to;

		private RectTransform _rt;

		public override void Setup(GameObject obj)
		{
			base.Setup(obj);
			if (attachObject == null)
			{
				_rt = null;
				return;
			}

			CanvasGroup canvasGroup = attachObject.GetComponent<CanvasGroup>();
			if (canvasGroup != null)
			{
				Destroy(canvasGroup);
			}

			_rt = attachObject.GetComponent<RectTransform>();
			Destory();
		}

		public override void SetVector(Vector3 start, Vector3 target)
		{
			_from = start;
			_to = target;
			if (_rt != null)
			{
				_rt.anchoredPosition = new Vector2(_from.x, _from.y);
			}
		}

		public override async UniTask Play(CancellationToken ct = default(CancellationToken))
		{
			if (_rt == null)
			{
				return;
			}

			Destory();
			_rt.anchoredPosition = new Vector2(_from.x, _from.y);
			bool finished = false;
			_tweener = _rt.DOAnchorPos(new Vector2(_to.x, _to.y), Duration).SetEase(Ease.OutQuart);
			_tweener.OnComplete(() => finished = true);
			_tweener.OnKill(() => finished = true);

			try
			{
				await UniTask.WaitUntil(() => finished, cancellationToken: ct);
			}
			catch (OperationCanceledException)
			{
				Destory();
			}
		}

		public override void Play(Action callback)
		{
			this.callback = callback;
			if (_rt == null)
			{
				OnFinished();
				return;
			}

			Destory();
			_rt.anchoredPosition = new Vector2(_from.x, _from.y);
			_tweener = _rt.DOAnchorPos(new Vector2(_to.x, _to.y), Duration)
				.SetEase(Ease.OutQuart)
				.OnComplete(OnFinished);
		}

		public void Complete()
		{
			_tweener?.Complete();
		}

		public override void Destory()
		{
			if (_tweener != null && _tweener.IsActive())
			{
				_tweener.Kill(false);
			}
			_tweener = null;
		}
	}
}
