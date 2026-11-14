using System.Runtime.CompilerServices;
using UnityEngine;

namespace Sekai
{
	public class TweenTargetCanvasAlpha : ITweenTargetComponent
	{
		private CanvasGroup _canvasGroup;
		private Vector3 _localPosition;
		private Vector3 _localRotation;
		private Vector3 _localScale;
		private Color _color;

		public bool IsExist
		{
			get
			{
				return _canvasGroup != null;
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
			get
			{
				return _canvasGroup != null ? _canvasGroup.alpha : 0f;
			}
			set
			{
				if (_canvasGroup != null)
				{
					_canvasGroup.alpha = value;
				}
			}
		}

		public void Initialize(GameObject targetObject)
		{
			_canvasGroup = targetObject != null ? targetObject.GetComponent<CanvasGroup>() : null;
		}

		public TweenTargetCanvasAlpha()
		{
			_localScale = Vector3.one;
			_color = Color.white;
		}
	}
}
