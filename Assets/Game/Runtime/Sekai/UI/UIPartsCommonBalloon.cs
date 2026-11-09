using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Sekai;

namespace Sekai.UI
{
	public class UIPartsCommonBalloon : MonoBehaviour
	{
		[Header("外部タップで閉じることが可能か")]
		[SerializeField]
		private bool _isAllowedClose;

		[SerializeField]
		private CanvasGroup _canvasGroup;

		[SerializeField]
		private CustomTextMesh _mainText;

		[SerializeField]
		private TouchController _touchController;

		[Space(24f)]
		[Header("ランタイムで自動サイズ調整をするか")]
		[SerializeField]
		private bool _adjustSize;

		[Header("吹き出し付のベース画像")]
		[SerializeField]
		private RectTransform _baseCenterRt;

		[SerializeField]
		[Header("スライスする左右のベース画像")]
		private RectTransform[] _baseSideRts;

		[Header("テキスト表示のパディング")]
		[SerializeField]
		private RectOffset _padding;

		[Header("バルーン自体の最大サイズ")]
		[SerializeField]
		private Vector2 _maxSize;

		[Header("テキスト自動サイズ調整用")]
		[SerializeField]
		private ContentSizeFitter _textSizeFitter;

		[Header("テキスト自動レイアウト用")]
		[SerializeField]
		private LayoutElement _textLayoutElement;

		private bool _needsCanvas;

		private Tweener _tween;

		protected RectTransform MainTextRt
		{
			get
			{
				return _mainText != null ? _mainText.RectTransform : null;
			}
		}

		public bool IsHide
		{
			get
			{
				return _canvasGroup == null || _canvasGroup.alpha <= 0f;
			}
		}

		public void Initialize()
		{
			if (_isAllowedClose && _touchController != null)
			{
				_touchController.SetHitTarget(gameObject);
				_touchController.OnHit = CheckClose;
			}

			Hide(false);
		}

		public void Setup(string text, bool needsCanvas = true, bool useShowAnimation = true)
		{
			_needsCanvas = needsCanvas;
			if (_mainText != null)
			{
				_mainText.SetText(text);
			}

			if (_adjustSize)
			{
				AdjustSize(_padding);
			}

			Show(useShowAnimation);
		}

		public void SetupFontAsset()
		{
			_mainText?.SetDefaultFontDB();
		}

		private void AdjustSize()
		{
			AdjustSize(_padding);
		}

		protected virtual void AdjustSize(RectOffset _padding)
		{
			if (_mainText == null || _padding == null)
			{
				return;
			}

			Canvas.ForceUpdateCanvases();
			if (_textSizeFitter != null)
			{
				_textSizeFitter.enabled = false;
			}

			RectTransform textRt = _mainText.RectTransform;
			if (_textLayoutElement != null && _maxSize.x > 0f)
			{
				_textLayoutElement.preferredWidth = Mathf.Min(textRt.rect.width, Mathf.Max(0f, _maxSize.x - _padding.horizontal));
			}

			if (_textLayoutElement != null && _maxSize.y > 0f)
			{
				_textLayoutElement.preferredHeight = Mathf.Min(textRt.rect.height, Mathf.Max(0f, _maxSize.y - _padding.vertical));
			}

			if (_textSizeFitter != null)
			{
				_textSizeFitter.enabled = true;
			}

			Canvas.ForceUpdateCanvases();
			Vector2 size = new Vector2(textRt.rect.width + _padding.horizontal, textRt.rect.height + _padding.vertical);
			RectTransform selfRt = transform as RectTransform;
			if (selfRt != null)
			{
				selfRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
				selfRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
			}

			if (_baseCenterRt != null && _baseSideRts != null)
			{
				float sideWidth = Mathf.Max(0f, (size.x - _baseCenterRt.rect.width) * 0.5f);
				foreach (RectTransform sideRt in _baseSideRts)
				{
					if (sideRt != null)
					{
						sideRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, sideWidth);
					}
				}
			}
		}

		private void CheckClose(bool isHit)
		{
			if (!isHit)
			{
				Hide();
			}
		}

		public void Show(bool useAnimation = true)
		{
			SetCanvas();
			if (_canvasGroup == null)
			{
				return;
			}

			_canvasGroup.blocksRaycasts = true;
			_tween?.Kill(false);
			if (useAnimation)
			{
				_tween = _canvasGroup.DOFade(1f, 0.1f);
			}
			else
			{
				_canvasGroup.alpha = 1f;
			}
		}

		public void Hide(bool useAnimation = true)
		{
			if (_canvasGroup == null)
			{
				RemoveCanvas();
				return;
			}

			_tween?.Kill(false);
			if (useAnimation)
			{
				_tween = _canvasGroup.DOFade(0f, 0.1f).OnComplete(() =>
				{
					_canvasGroup.blocksRaycasts = false;
					RemoveCanvas();
				});
			}
			else
			{
				_canvasGroup.alpha = 0f;
				_canvasGroup.blocksRaycasts = false;
				RemoveCanvas();
			}
		}

		private void SetCanvas()
		{
			if (!_needsCanvas || GetComponent<Canvas>() != null)
			{
				return;
			}

			Canvas canvas = gameObject.AddComponent<Canvas>();
			canvas.overrideSorting = true;
		}

		private void RemoveCanvas()
		{
			if (!_needsCanvas)
			{
				return;
			}

			Canvas canvas = GetComponent<Canvas>();
			if (canvas != null)
			{
				Destroy(canvas);
			}
		}

		public UIPartsCommonBalloon()
		{
		}
	}
}
