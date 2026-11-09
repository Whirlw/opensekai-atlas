using System.Runtime.CompilerServices;
using UnityEngine;

namespace Sekai
{
	public class TweenTargetColorNormal : ITweenTargetComponent
	{
		private SpriteRenderer _sprite;
		private Vector3 _localPosition;
		private Vector3 _localRotation;
		private Vector3 _localScale;

		public bool IsExist
		{
			get
			{
				return _sprite != null;
			}
		}

		public Vector3 LocalPosition
		{
			[CompilerGenerated]
			get
			{
				return _localPosition;
			}
			[CompilerGenerated]
			set
			{
				_localPosition = value;
			}
		}

		public Vector3 LocalRotation
		{
			[CompilerGenerated]
			get
			{
				return _localRotation;
			}
			[CompilerGenerated]
			set
			{
				_localRotation = value;
			}
		}

		public Vector3 LocalScale
		{
			[CompilerGenerated]
			get
			{
				return _localScale;
			}
			[CompilerGenerated]
			set
			{
				_localScale = value;
			}
		}

		public Color Color
		{
			get
			{
				return _sprite != null ? _sprite.color : Color.white;
			}
			set
			{
				if (_sprite != null)
				{
					_sprite.color = value;
				}
			}
		}

		public float Alpha
		{
			get
			{
				return Color.a;
			}
			set
			{
				Color current = Color;
				current.a = value;
				Color = current;
			}
		}

		public void Initialize(GameObject targetObject)
		{
			_sprite = targetObject != null ? targetObject.GetComponent<SpriteRenderer>() : null;
		}

		public TweenTargetColorNormal()
		{
			_localScale = Vector3.one;
		}
	}
}
