using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Sekai;

namespace Sekai.Core.Live
{
	public class SkillTextViewThumbnail : MonoBehaviour
	{
		[SerializeField]
		private SpriteRenderer _thumbnail;

		[SerializeField]
		private SpriteRenderer _frame;

		[SerializeField]
		private SpriteRenderer _rarityFrame;

		[SerializeField]
		private Sprite[] _raritySprites;

		public void Initialize(Sprite thumbnailSprite, CardRarityType rarityType, float size)
		{
			if (_thumbnail != null)
			{
				_thumbnail.sprite = thumbnailSprite;
				_thumbnail.enabled = thumbnailSprite != null;
			}

			if (_rarityFrame != null)
			{
				Sprite raritySprite = GetRaritySprite(rarityType);
				_rarityFrame.sprite = raritySprite;
				_rarityFrame.enabled = raritySprite != null;
			}

			transform.localScale = Vector3.one * size;
			SetActive(true);
			FadeInImmediate();
		}

		public void SetFrameColor(Color color)
		{
			if (_frame != null)
			{
				_frame.color = color;
			}
		}

		private Sprite GetRaritySprite(CardRarityType rarityType)
		{
			if (_raritySprites == null || _raritySprites.Length == 0)
			{
				return null;
			}

			int index = Mathf.Clamp((int)rarityType - 1, 0, _raritySprites.Length - 1);
			return _raritySprites[index];
		}

		public void SetActive(bool isActive)
		{
			gameObject.SetActive(isActive);
		}

		public Sequence FadeOut(float duration)
		{
			return FadeTo(0f, duration);
		}

		public Sequence FadeIn(float duration)
		{
			gameObject.SetActive(true);
			return FadeTo(1f, duration);
		}

		public void FadeInImmediate()
		{
			ChangeAllAlpha(1f);
		}

		public void FadeOutImmediate()
		{
			ChangeAllAlpha(0f);
		}

		private void ChangeAllAlpha(float a)
		{
			foreach (SpriteRenderer renderer in GetRenderers())
			{
				Color color = renderer.color;
				color.a = a;
				renderer.color = color;
			}
		}

		public SkillTextViewThumbnail()
		{
		}

		private Sequence FadeTo(float alpha, float duration)
		{
			Sequence sequence = DOTween.Sequence();
			foreach (SpriteRenderer renderer in GetRenderers())
			{
				renderer.DOKill();
				sequence.Join(renderer.DOFade(alpha, duration));
			}
			return sequence;
		}

		private SpriteRenderer[] GetRenderers()
		{
			List<SpriteRenderer> renderers = new List<SpriteRenderer>(3);
			if (_thumbnail != null)
			{
				renderers.Add(_thumbnail);
			}
			if (_frame != null)
			{
				renderers.Add(_frame);
			}
			if (_rarityFrame != null)
			{
				renderers.Add(_rarityFrame);
			}
			return renderers.ToArray();
		}
	}
}
