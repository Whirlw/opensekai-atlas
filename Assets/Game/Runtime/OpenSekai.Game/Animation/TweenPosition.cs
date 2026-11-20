using LitMotion;
using UnityEngine;

namespace Sekai
{
	public class TweenPosition : TweenBase
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_from")]
		[SerializeField]
		private Vector3 from;

		[UnityEngine.Serialization.FormerlySerializedAs("_to")]
		[SerializeField]
		private Vector3 to;

		public Vector3 From
		{
			get
			{
				return from;
			}
			set
			{
				from = value;
			}
		}

		public Vector3 To
		{
			get
			{
				return to;
			}
			set
			{
				to = value;
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
			Vector3 start = playDirection == PlayDirection.Forward ? from : to;
			Vector3 end = playDirection == PlayDirection.Forward ? to : from;
			tweenTarget.LocalPosition = start;
			PlayMotion(LMotion.Create(start, end, Mathf.Max(0f, duration)),
				static (value, owner) => owner.TweenTarget.LocalPosition = value);
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
			tweenTarget.LocalPosition = Vector3.LerpUnclamped(from, to, Mathf.Clamp01(t));
		}

		protected override void OnReference()
		{
			if (reference?.TweenTarget != null && tweenTarget != null)
			{
				tweenTarget.LocalPosition = reference.TweenTarget.LocalPosition;
			}
		}

		public TweenPosition()
		{
		}
	}
}
