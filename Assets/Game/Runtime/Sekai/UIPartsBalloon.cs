using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public class UIPartsBalloon : MonoBehaviour
	{
		[SerializeField]
		private bool _enableHideOnAwake;

		[SerializeField]
		private CanvasGroup _canvasGroup;

		[SerializeField]
		private CustomTextMesh _message;

		[SerializeField]
		private Vector2 _minSize;

		public bool Enable
		{
			set
			{
				throw null;
			}
		}

		public string Message
		{
			get
			{
				throw null;
			}
			set
			{
				throw null;
			}
		}

		public string MessageText
		{
			get
			{
				throw null;
			}
			set
			{
				throw null;
			}
		}

		private void Awake()
		{
			if (_enableHideOnAwake && _canvasGroup != null)
			{
				_canvasGroup.alpha = 0f;
			}
		}

		public void Fade(float endValue, float duration)
		{
			throw null;
		}

		public void SetTextPosition(Vector2 pos)
		{
			throw null;
		}

		private void FitText()
		{
			throw null;
		}

		public UIPartsBalloon()
		{
			_enableHideOnAwake = true;
			_minSize = Vector2.zero;
		}
	}
}
