using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;

namespace Sekai
{
	public class ScreenSlideInOut : ScreenInOutAnimBase
	{
		private const float Duration = 0.2f;

		private MotionHandle _motion;

		private Vector3 _from;

		private Vector3 _to;

		private RectTransform _rt;

		public override void Setup(GameObject obj)
		{
			Destory();
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

			MotionHandle motion = StartSlide();
			try
			{
				await UniTask.WaitUntil(motion, static handle => !handle.IsActive(), cancellationToken: ct);
			}
			catch (OperationCanceledException)
			{
				motion.TryCancel();
			}
		}

		public override void Play(Action callback)
		{
			if (_rt == null)
			{
				callback?.Invoke();
				return;
			}

			StartSlide(callback);
		}

		private MotionHandle StartSlide(Action onComplete = null)
		{
			Destory();
			Vector2 start = _from;
			_rt.anchoredPosition = start;
			_motion = LMotion.Create(start, (Vector2)_to, Duration)
				.WithEase(Ease.OutQuart)
				.WithOnComplete(onComplete)
				.BindToAnchoredPosition(_rt);
			return _motion;
		}

		public void Complete()
		{
			_motion.TryComplete();
		}

		public override void Destory()
		{
			_motion.TryCancel();
			_motion = default;
		}

		private void OnDisable()
		{
			Destory();
		}
	}
}
