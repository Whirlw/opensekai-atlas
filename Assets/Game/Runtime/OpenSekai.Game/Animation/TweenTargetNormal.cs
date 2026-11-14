using System.Runtime.CompilerServices;
using UnityEngine;

namespace Sekai
{
	public class TweenTargetNormal : ITweenTargetComponent
	{
		private Transform _transform;
		private Color _color;
		private float _alpha;

		public bool IsExist
		{
			get
			{
				return _transform != null;
			}
		}

		public Vector3 LocalPosition
		{
			get
			{
				return _transform != null ? _transform.localPosition : Vector3.zero;
			}
			set
			{
				if (_transform != null)
				{
					_transform.localPosition = value;
				}
			}
		}

		public Vector3 LocalRotation
		{
			get
			{
				return _transform != null ? _transform.localEulerAngles : Vector3.zero;
			}
			set
			{
				if (_transform != null)
				{
					_transform.localEulerAngles = value;
				}
			}
		}

		public Vector3 LocalScale
		{
			get
			{
				return _transform != null ? _transform.localScale : Vector3.one;
			}
			set
			{
				if (_transform != null)
				{
					_transform.localScale = value;
				}
			}
		}

		public Color Color
		{
			[CompilerGenerated]
			get
			{
				return _color;
			}
			[CompilerGenerated]
			set
			{
				_color = value;
			}
		}

		public float Alpha
		{
			[CompilerGenerated]
			get
			{
				return _alpha;
			}
			[CompilerGenerated]
			set
			{
				_alpha = value;
			}
		}

		public void Initialize(GameObject targetObject)
		{
			_transform = targetObject != null ? targetObject.transform : null;
		}

		public TweenTargetNormal()
		{
			_color = Color.white;
			_alpha = 1f;
		}
	}
}
