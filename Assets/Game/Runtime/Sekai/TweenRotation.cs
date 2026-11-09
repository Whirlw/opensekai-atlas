using UnityEngine;

namespace Sekai
{
	public class TweenRotation : TweenBase
	{
		[SerializeField]
		private Vector3 _from;

		[SerializeField]
		private Vector3 _to;

		public Vector3 From
		{
			get
			{
				return _from;
			}
			set
			{
				_from = value;
			}
		}

		public Vector3 To
		{
			get
			{
				return _to;
			}
			set
			{
				_to = value;
			}
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
			Vector3 start = playDirection == PlayDirection.Forward ? _from : _to;
			Vector3 end = playDirection == PlayDirection.Forward ? _to : _from;
			tweenTarget.LocalRotation = start;
			tweener = DG.Tweening.DOTween.To(() => tweenTarget.LocalRotation, value => tweenTarget.LocalRotation = value, end, duration);
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
			tweenTarget.LocalRotation = Vector3.LerpUnclamped(_from, _to, Mathf.Clamp01(t));
		}

		protected override void OnReference()
		{
			if (reference?.TweenTarget != null && tweenTarget != null)
			{
				tweenTarget.LocalRotation = reference.TweenTarget.LocalRotation;
			}
		}

		public TweenRotation()
		{
		}
	}
}
