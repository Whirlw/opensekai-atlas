using Beebyte.Obfuscator;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Sekai.Core.Live
{
	public class SkillTextViewAnimation : MonoBehaviour
	{
		private static readonly float DURATION;

		[SerializeField]
		private Transform _root;

		[SerializeField]
		private Transform _levelRoot;

		[SerializeField]
		private Transform _skillEffectRoot;

		[SerializeField]
		private TextMeshPro _skillEffectText;

		[SerializeField]
		private Transform _iconRoot;

		[SerializeField]
		private SpriteRenderer _iconSprite;

		[SerializeField]
		private Transform _referenceTargetRoot;

		[SerializeField]
		private SkillTextViewThumbnail _referenceTargetThumbnail;

		[SerializeField]
		private SkillTextViewThumbnail _referenceTargetThumbnailForEncore;

		[SerializeField]
		private SpriteRenderer _referenceSprite;

		private Tween[] _tweens;

		private Sequence _animationSequence;

		private bool _isReferenceSkill;

		private bool _isEncore;

		private SpriteRenderer[] _spriteRenderers;

		[SerializeField]
		private SpriteRenderer[] _excludeSpriteRenderers;

		private SpriteRenderer[] _levelSpriteRenderers;

		public void Initialize(bool _isReferenceSkill = false)
		{
			this._isReferenceSkill = _isReferenceSkill;
			_spriteRenderers = _root == null ? GetComponentsInChildren<SpriteRenderer>(true) : _root.GetComponentsInChildren<SpriteRenderer>(true);
			_levelSpriteRenderers = _levelRoot == null ? new SpriteRenderer[0] : _levelRoot.GetComponentsInChildren<SpriteRenderer>(true);
			if (_referenceTargetRoot != null)
			{
				_referenceTargetRoot.gameObject.SetActive(_isReferenceSkill);
			}
			Clear();
		}

		public void Execute(bool _isEncore = false)
		{
			this._isEncore = _isEncore;
			CreateAnimation();
			_animationSequence?.Restart();
		}

		[Skip]
		public void Clear()
		{
			_animationSequence?.Kill(false);
			_animationSequence = null;
			if (_tweens != null)
			{
				for (int i = 0; i < _tweens.Length; i++)
				{
					_tweens[i]?.Kill(false);
				}
			}
			Fade(_spriteRenderers, 0f);
			if (_root != null)
			{
				_root.localScale = Vector3.one;
			}
			if (_iconRoot != null)
			{
				_iconRoot.localScale = Vector3.one;
			}
			if (_skillEffectRoot != null)
			{
				_skillEffectRoot.localScale = Vector3.one;
			}
			if (_referenceSprite != null)
			{
				Color color = _referenceSprite.color;
				color.a = 0f;
				_referenceSprite.color = color;
			}
			_referenceTargetThumbnail?.FadeOutImmediate();
			_referenceTargetThumbnailForEncore?.FadeOutImmediate();
		}

		private void Fade(SpriteRenderer[] _spriteRenderers, float value)
		{
			if (_spriteRenderers == null)
			{
				return;
			}

			for (int i = 0; i < _spriteRenderers.Length; i++)
			{
				SpriteRenderer renderer = _spriteRenderers[i];
				if (renderer == null || IsExcluded(renderer))
				{
					continue;
				}

				Color color = renderer.color;
				color.a = value;
				renderer.color = color;
			}
		}

		private void CreateAnimation()
		{
			_animationSequence?.Kill(false);
			_animationSequence = DOTween.Sequence();

			Fade(_spriteRenderers, 0f);
			if (_root != null)
			{
				_root.localScale = Vector3.one * 0.9f;
				_animationSequence.Join(_root.DOScale(1f, DURATION * 0.35f).SetEase(Ease.OutBack));
			}

			_animationSequence.Join(DOTween.To(() => 0f, value => Fade(_spriteRenderers, value), 1f, DURATION * 0.25f));
			if (_iconRoot != null)
			{
				_iconRoot.localScale = Vector3.one * 0.7f;
				_animationSequence.Join(_iconRoot.DOScale(1f, DURATION * 0.35f).SetEase(Ease.OutBack));
			}
			if (_skillEffectRoot != null)
			{
				_skillEffectRoot.localScale = Vector3.one * 0.8f;
				_animationSequence.Join(_skillEffectRoot.DOScale(1f, DURATION * 0.35f).SetEase(Ease.OutBack));
			}

			SkillTextViewThumbnail thumbnail = _isEncore ? _referenceTargetThumbnailForEncore : _referenceTargetThumbnail;
			if (_isReferenceSkill && thumbnail != null)
			{
				_animationSequence.Join(thumbnail.FadeIn(DURATION * 0.2f));
			}

			_animationSequence.AppendInterval(DURATION * 0.65f);
			_animationSequence.Append(DOTween.To(() => 1f, value => Fade(_spriteRenderers, value), 0f, DURATION * 0.25f));
			_animationSequence.SetAutoKill(false);
		}

		public SkillTextViewAnimation()
		{
		}

		static SkillTextViewAnimation()
		{
			DURATION = 1.5f;
		}

		private bool IsExcluded(SpriteRenderer renderer)
		{
			if (_excludeSpriteRenderers == null)
			{
				return false;
			}

			for (int i = 0; i < _excludeSpriteRenderers.Length; i++)
			{
				if (_excludeSpriteRenderers[i] == renderer)
				{
					return true;
				}
			}
			return false;
		}
	}
}
