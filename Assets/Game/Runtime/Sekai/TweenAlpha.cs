using UnityEngine;

namespace Sekai
{
	public class TweenAlpha : TweenBase
	{
		[Range(0f, 1f)]
		[SerializeField]
		private float _from;

		[Range(0f, 1f)]
		[SerializeField]
		private float _to;

		private float _current;

		public float From
		{
			get
			{
				return _from;
			}
			set
			{
				_from = Mathf.Clamp01(value);
			}
		}

		public float To
		{
			get
			{
				return _to;
			}
			set
			{
				_to = Mathf.Clamp01(value);
			}
		}

		public override void SetupTarget()
		{
			if (componentType == PlayComponentType.CanvasGroup)
			{
				tweenTarget = new TweenTargetCanvasAlpha();
			}
			else if (componentType == PlayComponentType.Normal)
			{
				tweenTarget = new TweenTargetColorNormal();
			}
			else
			{
				tweenTarget = new TweenTargetColorUnityUI();
			}
			tweenTarget.Initialize(gameObject);
		}

		public override void PlayCore(PlayDirection playDirection)
		{
			direction = playDirection;
			if (tweenTarget == null)
			{
				SetupTarget();
			}
			if (tweenTarget == null || !tweenTarget.IsExist)
			{
				return;
			}
			float start = playDirection == PlayDirection.Forward ? _from : _to;
			float end = playDirection == PlayDirection.Forward ? _to : _from;
			_current = start;
			tweenTarget.Alpha = start;
			tweener = DG.Tweening.DOTween.To(() => _current, value =>
			{
				_current = value;
				tweenTarget.Alpha = value;
			}, end, duration);
			SetTweenOptionCommon();
		}

		protected override void ToTween(float normalizeTime)
		{
			KillTween();
			if (tweenTarget == null || !tweenTarget.IsExist)
			{
				return;
			}
			float t = direction == PlayDirection.Forward ? normalizeTime : 1f - normalizeTime;
			if (curve != null)
			{
				t = curve.Evaluate(t);
			}
			_current = Mathf.LerpUnclamped(_from, _to, Mathf.Clamp01(t));
			tweenTarget.Alpha = _current;
		}

		protected override void OnReference()
		{
			if (reference?.TweenTarget != null && tweenTarget != null)
			{
				_current = reference.TweenTarget.Alpha;
				tweenTarget.Alpha = _current;
			}
		}

		public TweenAlpha()
		{
		}
	}
}
