using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public class UIPartsLoadingCircle : MonoBehaviour
	{
		public enum ColorType
		{
			Green = 0,
			Gray = 1,
			White = 2,
			LightGray = 3
		}

		[SerializeField]
		private TweenScale[] _circleTweens;

		[SerializeField]
		private CustomImage[] _circleImages;

		[SerializeField]
		private ColorType _colorType;

		[SerializeField]
		private CanvasGroup _canvasGroup;

		public TweenBase.BehaviourType TweenBehaviour
		{
			set
			{
				if (_circleTweens == null)
				{
					return;
				}

				foreach (TweenScale circleTween in _circleTweens)
				{
					if (circleTween != null)
					{
						circleTween.Behaviour = value;
					}
				}
			}
		}

		public bool Enable
		{
			set
			{
				if (_canvasGroup != null)
				{
					_canvasGroup.alpha = value ? 1f : 0f;
				}
			}
		}

		private void Start()
		{
		}

		private void OnValidate()
		{
			UpdateView();
		}

		private void UpdateView()
		{
			if (_circleImages == null)
			{
				return;
			}

			Color color = _colorType switch
			{
				ColorType.Gray => new Color(0.45f, 0.45f, 0.45f, 1f),
				ColorType.White => Color.white,
				ColorType.LightGray => new Color(0.8f, 0.8f, 0.8f, 1f),
				_ => new Color(0f, 0.73f, 0.83f, 1f)
			};

			foreach (CustomImage image in _circleImages)
			{
				if (image != null)
				{
					image.color = color;
				}
			}
		}

		public void SetReferenceTween(UIPartsLoadingCircle masterTween)
		{
			if (masterTween == null || _circleTweens == null || masterTween._circleTweens == null)
			{
				return;
			}

			for (int i = 0; i < _circleTweens.Length && i < masterTween._circleTweens.Length; i++)
			{
				if (_circleTweens[i] != null)
				{
					_circleTweens[i].ReferenceTween = masterTween._circleTweens[i];
					_circleTweens[i].Behaviour = TweenBase.BehaviourType.Reference;
				}
			}
		}

		public void Show()
		{
			Enable = true;
		}

		public void Hide()
		{
			Enable = false;
		}

		public UIPartsLoadingCircle()
		{
		}
	}
}
