using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	public class TweenTargetColorUnityUI : ITweenTargetComponent
	{
		private Graphic _graphic;
		private Vector3 _localPosition;
		private Vector3 _localRotation;
		private Vector3 _localScale;

		public bool IsExist
		{
			get
			{
				return _graphic != null;
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
				return _graphic != null ? _graphic.color : Color.white;
			}
			set
			{
				if (_graphic != null)
				{
					_graphic.color = value;
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
			_graphic = targetObject != null ? targetObject.GetComponent<Graphic>() : null;
		}

		public TweenTargetColorUnityUI()
		{
			_localScale = Vector3.one;
		}
	}
}
