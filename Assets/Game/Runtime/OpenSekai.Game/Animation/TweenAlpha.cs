using LitMotion;
using UnityEngine;

namespace Sekai
{
	public class TweenAlpha : TweenBase
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_from")]
		[Range(0f, 1f)]
		[SerializeField]
		private float from;

		[UnityEngine.Serialization.FormerlySerializedAs("_to")]
		[Range(0f, 1f)]
		[SerializeField]
		private float to;

		public float From
		{
			get
			{
				return from;
			}
			set
			{
				from = Mathf.Clamp01(value);
			}
		}

		public float To
		{
			get
			{
				return to;
			}
			set
			{
				to = Mathf.Clamp01(value);
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
			float start = playDirection == PlayDirection.Forward ? from : to;
			float end = playDirection == PlayDirection.Forward ? to : from;
			tweenTarget.Alpha = start;
			PlayMotion(LMotion.Create(start, end, Mathf.Max(0f, duration)),
				static (value, owner) => owner.TweenTarget.Alpha = value);
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
			tweenTarget.Alpha = Mathf.LerpUnclamped(from, to, Mathf.Clamp01(t));
		}

		protected override void OnReference()
		{
			if (reference?.TweenTarget != null && tweenTarget != null)
			{
				tweenTarget.Alpha = reference.TweenTarget.Alpha;
			}
		}

		public TweenAlpha()
		{
		}
	}
}
