using OpenSekai.Shared.Scoring;
using Sekai.Core.Live;
using LitMotion;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class ScoreView : MonoBehaviour
	{
		[SerializeField]
		private SpriteRenderer _scoreGauge;

		[SerializeField]
		private SpriteRenderer _rankSpriteRenderer;

		[SerializeField]
		private Sprite[] _rankSprites;

		[SerializeField]
		private SpriteRenderer _rankTextSpriteRenderer;

		// "RANK" drawn in each rank's colours, indexed like _rankSprites. When set it replaces tinting the label.
		[SerializeField]
		private Sprite[] _rankTextSprites;

		[SerializeField]
		private Transform _gaugeRankS;

		[SerializeField]
		private Transform _gaugeRankA;

		[SerializeField]
		private Transform _gaugeRankB;

		[SerializeField]
		private Transform _gaugeRankC;

		[SerializeField]
		private NumberView _number;

		[SerializeField]
		private NumberView _outlineNumber;

		[SerializeField]
		private Transform _addScoreRoot;

		[SerializeField]
		private NumberView _addScoreNumber;

		[SerializeField]
		private NumberView _addScoreOutlineNumber;

		private int _lastScoreRank;

		private MotionHandle _addScoreMotion;

		private ScoreGauge _scoreGaugeCalculator;

		private Vector2 _scoreGaugeSizeCache = new Vector2(5.648148f, 0.222222f);

		private bool _inverseScale;

		public void Setup(LiveMusicData musicData)
		{
			if (_scoreGauge != null)
			{
				_scoreGaugeSizeCache = _scoreGauge.size;
			}
			SetupGaugeRankPosition(_gaugeRankS, ScoreGauge.RANK_RATE_S);
			SetupGaugeRankPosition(_gaugeRankA, ScoreGauge.RANK_RATE_A);
			SetupGaugeRankPosition(_gaugeRankB, ScoreGauge.RANK_RATE_B);
			SetupGaugeRankPosition(_gaugeRankC, ScoreGauge.RANK_RATE_C);
			_scoreGaugeCalculator = ScoreGauge.Create(musicData?.Score);
			_inverseScale = _addScoreRoot != null && _addScoreRoot.localScale.y < 0f;
			Clear();
		}

		public void ChangeScoreCalculator(ScoreGauge newCalculator)
		{
			_scoreGaugeCalculator = newCalculator ?? ScoreGauge.Create(1);
		}

		public void SetupScore(LiveScore score)
		{
			UpdateScore(ref score, 0);
		}

		public void Clear()
		{
			_addScoreMotion.TryCancel();
			_lastScoreRank = -1;
			if (_scoreGauge != null)
			{
				_scoreGauge.size = new Vector2(0f, _scoreGaugeSizeCache.y);
			}
			if (_rankSpriteRenderer != null)
			{
				_rankSpriteRenderer.sprite = null;
			}
			if (_number != null)
			{
				_number.Setup("#ffffff", "#CCCCEE");
			}
			if (_outlineNumber != null)
			{
				_outlineNumber.Setup("#444466", "#444466");
			}
			if (_addScoreNumber != null)
			{
				_addScoreNumber.Setup("#ffffff", "#ffffff00");
			}
			if (_addScoreOutlineNumber != null)
			{
				_addScoreOutlineNumber.Setup("#444466", "#ffffff00");
			}
			if (_addScoreRoot != null)
			{
				_addScoreRoot.localScale = Vector3.zero;
			}
		}

		public float CalculateGaugeProgress(LiveScore score)
		{
			return _scoreGaugeCalculator.ProgressOf(score.totalScore);
		}

		public void UpdateScore(ref LiveScore score, int addScore)
		{
			if (_scoreGaugeCalculator == null)
			{
				_scoreGaugeCalculator = ScoreGauge.Create(1);
			}
			if (_scoreGauge != null)
			{
				float progress = CalculateGaugeProgress(score);
				if (float.IsNaN(progress))
				{
					progress = 0f;
				}
				Vector2 size = _scoreGauge.size;
				size.x = progress * _scoreGaugeSizeCache.x;
				size.y = _scoreGaugeSizeCache.y;
				_scoreGauge.size = size;
			}
			if (_number != null)
			{
				_number.UpdateNumber(score.totalScore);
			}
			if (_outlineNumber != null)
			{
				_outlineNumber.UpdateNumber(score.totalScore);
			}

			if (_rankSpriteRenderer != null && _lastScoreRank != (int)score.rank)
			{
				int rankIndex = (int)score.rank;
				_rankSpriteRenderer.sprite = _rankSprites != null && rankIndex >= 0 && rankIndex < _rankSprites.Length ? _rankSprites[rankIndex] : null;
				if (_rankTextSpriteRenderer != null)
				{
					if (_rankTextSprites != null && rankIndex >= 0 && rankIndex < _rankTextSprites.Length && _rankTextSprites[rankIndex] != null)
					{
						_rankTextSpriteRenderer.sprite = _rankTextSprites[rankIndex];
						_rankTextSpriteRenderer.color = Color.white;
					}
					else
					{
						_rankTextSpriteRenderer.color = ColorUtility.GetScoreRankColor(score.rank);
					}
				}
				_lastScoreRank = rankIndex;
			}

			if (addScore > 0 && _addScoreRoot != null)
			{
				PlayAddScoreAnimation();
				if (_addScoreNumber != null)
				{
					_addScoreNumber.UpdateNumber(addScore);
				}
				if (_addScoreOutlineNumber != null)
				{
					_addScoreOutlineNumber.UpdateNumber(addScore);
				}
			}
		}

		private void SetupGaugeRankPosition(Transform t, float value)
		{
			if (t == null || _scoreGauge == null)
			{
				return;
			}
			Vector3 gaugePosition = _scoreGauge.transform.localPosition;
			Vector3 position = t.localPosition;
			position.x = gaugePosition.x + Mathf.Clamp01(value) * _scoreGaugeSizeCache.x;
			t.localPosition = position;
		}

		private void PlayAddScoreAnimation()
		{
			const float slideDuration = 0.2f;
			const float holdDuration = 0.5f;
			const float duration = slideDuration + holdDuration;

			_addScoreMotion.TryCancel();
			Vector3 position = _addScoreRoot.localPosition;
			_addScoreRoot.localScale = _inverseScale ? new Vector3(1f, -1f, 1f) : Vector3.one;
			_addScoreMotion = LMotion.Create(0f, duration, duration)
				.WithScheduler(LiveMotion.Scheduler())
				.WithOnComplete(() => _addScoreRoot.localScale = Vector3.zero)
				.Bind(elapsed =>
				{
					float progress = EaseUtility.Evaluate(Mathf.Clamp01(elapsed / slideDuration), Ease.OutQuad);
					position.x = Mathf.LerpUnclamped(-0.5f, 0f, progress);
					_addScoreRoot.localPosition = position;
					UpdateAddNumberAlpha(progress);
				});
		}

		private void OnDisable()
		{
			if (_addScoreMotion.TryCancel() && _addScoreRoot != null)
			{
				_addScoreRoot.localScale = Vector3.zero;
			}
		}

		private void UpdateAddNumberAlpha(float alpha)
		{
			if (_addScoreNumber != null)
			{
				_addScoreNumber.UpdateAlpha(alpha);
			}
			if (_addScoreOutlineNumber != null)
			{
				_addScoreOutlineNumber.UpdateAlpha(alpha);
			}
		}
	}
}
