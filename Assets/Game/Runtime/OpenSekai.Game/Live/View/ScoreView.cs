using OpenSekai.Shared.Scoring;
using Sekai.Core.Live;
using LitMotion;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class ScoreView : MonoBehaviour
	{
		[UnityEngine.Serialization.FormerlySerializedAs("_scoreGauge")]
		[SerializeField]
		private SpriteRenderer scoreGauge;

		[UnityEngine.Serialization.FormerlySerializedAs("_rankSpriteRenderer")]
		[SerializeField]
		private SpriteRenderer rankSpriteRenderer;

		[UnityEngine.Serialization.FormerlySerializedAs("_rankSprites")]
		[SerializeField]
		private Sprite[] rankSprites;

		[UnityEngine.Serialization.FormerlySerializedAs("_rankTextSpriteRenderer")]
		[SerializeField]
		private SpriteRenderer rankTextSpriteRenderer;

		// "RANK" drawn in each rank's colours, indexed like _rankSprites. When set it replaces tinting the label.
		[UnityEngine.Serialization.FormerlySerializedAs("_rankTextSprites")]
		[SerializeField]
		private Sprite[] rankTextSprites;

		[UnityEngine.Serialization.FormerlySerializedAs("_gaugeRankS")]
		[SerializeField]
		private Transform gaugeRankS;

		[UnityEngine.Serialization.FormerlySerializedAs("_gaugeRankA")]
		[SerializeField]
		private Transform gaugeRankA;

		[UnityEngine.Serialization.FormerlySerializedAs("_gaugeRankB")]
		[SerializeField]
		private Transform gaugeRankB;

		[UnityEngine.Serialization.FormerlySerializedAs("_gaugeRankC")]
		[SerializeField]
		private Transform gaugeRankC;

		[UnityEngine.Serialization.FormerlySerializedAs("_number")]
		[SerializeField]
		private NumberView number;

		[UnityEngine.Serialization.FormerlySerializedAs("_outlineNumber")]
		[SerializeField]
		private NumberView outlineNumber;

		[UnityEngine.Serialization.FormerlySerializedAs("_addScoreRoot")]
		[SerializeField]
		private Transform addScoreRoot;

		[UnityEngine.Serialization.FormerlySerializedAs("_addScoreNumber")]
		[SerializeField]
		private NumberView addScoreNumber;

		[UnityEngine.Serialization.FormerlySerializedAs("_addScoreOutlineNumber")]
		[SerializeField]
		private NumberView addScoreOutlineNumber;

		private int _lastScoreRank;

		private MotionHandle _addScoreMotion;

		private ScoreGauge _scoreGaugeCalculator;

		private Vector2 _scoreGaugeSizeCache = new Vector2(5.648148f, 0.222222f);

		private bool _inverseScale;

		private LiveHudSkin _hudSkin;
		private SpriteRendererBaseline _rankBaseline;
		private SpriteRendererBaseline _rankTextBaseline;
		private static readonly string[] _rankKeys = { "hud.score.rank.S", "hud.score.rank.A", "hud.score.rank.B", "hud.score.rank.C", "hud.score.rank.D" };
		private static readonly string[] _rankLabelKeys = { "hud.score.rankLabel.S", "hud.score.rankLabel.A", "hud.score.rankLabel.B", "hud.score.rankLabel.C", "hud.score.rankLabel.D" };

		public void Setup(LiveMusicData musicData, LiveHudSkin skin = null)
		{
			_hudSkin = skin;
			if (rankSpriteRenderer != null) _rankBaseline ??= new SpriteRendererBaseline(rankSpriteRenderer);
			if (rankTextSpriteRenderer != null)
			{
				_rankTextBaseline ??= new SpriteRendererBaseline(rankTextSpriteRenderer);
				_rankTextBaseline.Draw(rankTextSpriteRenderer, null, _rankTextBaseline.Sprite, true);
			}
			if (scoreGauge != null)
			{
				_scoreGaugeSizeCache = scoreGauge.size;
			}
			SetupGaugeRankPosition(gaugeRankS, ScoreGauge.RANK_RATE_S, "hud.score.marker.S.base");
			SetupGaugeRankPosition(gaugeRankA, ScoreGauge.RANK_RATE_A, "hud.score.marker.A.base");
			SetupGaugeRankPosition(gaugeRankB, ScoreGauge.RANK_RATE_B, "hud.score.marker.B.base");
			SetupGaugeRankPosition(gaugeRankC, ScoreGauge.RANK_RATE_C, "hud.score.marker.C.base");
			_scoreGaugeCalculator = ScoreGauge.Create(musicData?.Score);
			_inverseScale = addScoreRoot != null && addScoreRoot.localScale.y < 0f;
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
			if (scoreGauge != null)
			{
				scoreGauge.size = new Vector2(0f, _scoreGaugeSizeCache.y);
			}
			if (rankSpriteRenderer != null)
			{
				rankSpriteRenderer.sprite = null;
			}
			if (number != null)
			{
				number.Setup("#ffffff", "#CCCCEE");
			}
			if (outlineNumber != null)
			{
				outlineNumber.Setup("#444466", "#444466");
			}
			if (addScoreNumber != null)
			{
				addScoreNumber.Setup("#ffffff", "#ffffff00");
			}
			if (addScoreOutlineNumber != null)
			{
				addScoreOutlineNumber.Setup("#444466", "#ffffff00");
			}
			if (addScoreRoot != null)
			{
				addScoreRoot.localScale = Vector3.zero;
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
			if (scoreGauge != null)
			{
				float progress = CalculateGaugeProgress(score);
				if (float.IsNaN(progress))
				{
					progress = 0f;
				}
				Vector2 size = scoreGauge.size;
				size.x = progress * _scoreGaugeSizeCache.x;
				size.y = _scoreGaugeSizeCache.y;
				scoreGauge.size = size;
			}
			if (number != null)
			{
				number.UpdateNumber(score.totalScore);
			}
			if (outlineNumber != null)
			{
				outlineNumber.UpdateNumber(score.totalScore);
			}

			if (rankSpriteRenderer != null && _lastScoreRank != (int)score.rank)
			{
				int rankIndex = (int)score.rank;
				Sprite rank = rankSprites != null && rankIndex >= 0 && rankIndex < rankSprites.Length ? rankSprites[rankIndex] : null;
				_rankBaseline ??= new SpriteRendererBaseline(rankSpriteRenderer);
				DrawRank(rankSpriteRenderer, _rankBaseline, rank, _rankKeys, rankIndex);
				if (rankTextSpriteRenderer != null)
				{
					_rankTextBaseline ??= new SpriteRendererBaseline(rankTextSpriteRenderer);
					Sprite label = rankTextSprites != null && rankIndex >= 0 && rankIndex < rankTextSprites.Length ? rankTextSprites[rankIndex] : null;
					bool custom = DrawRank(rankTextSpriteRenderer, _rankTextBaseline, label ?? _rankTextBaseline.Sprite, _rankLabelKeys, rankIndex);
					rankTextSpriteRenderer.color = custom || label != null ? Color.white : ColorUtility.GetScoreRankColor(score.rank);
				}
				_lastScoreRank = rankIndex;
			}

			if (addScore > 0 && addScoreRoot != null)
			{
				PlayAddScoreAnimation();
				if (addScoreNumber != null)
				{
					addScoreNumber.UpdateNumber(addScore);
				}
				if (addScoreOutlineNumber != null)
				{
					addScoreOutlineNumber.UpdateNumber(addScore);
				}
			}
		}

		private bool DrawRank(SpriteRenderer renderer, SpriteRendererBaseline baseline, Sprite fallback, string[] keys, int index)
		{
			if (index >= 0 && index < keys.Length && _hudSkin?.Draw(keys[index], renderer, baseline, fallback) == true) return true;
			baseline.Draw(renderer, null, fallback, true);
			return false;
		}

		private void SetupGaugeRankPosition(Transform t, float value, string key)
		{
			if (t == null || scoreGauge == null)
			{
				return;
			}
			Vector3 gaugePosition = scoreGauge.transform.localPosition;
			Vector3 position = t.localPosition;
			position.x = gaugePosition.x + Mathf.Clamp01(value) * _scoreGaugeSizeCache.x;
			t.localPosition = _hudSkin?.Position(key, position) ?? position;
		}

		private void PlayAddScoreAnimation()
		{
			const float SLIDE_DURATION = 0.2f;
			const float HOLD_DURATION = 0.5f;
			const float DURATION = SLIDE_DURATION + HOLD_DURATION;

			_addScoreMotion.TryCancel();
			Vector3 position = addScoreRoot.localPosition;
			addScoreRoot.localScale = _inverseScale ? new Vector3(1f, -1f, 1f) : Vector3.one;
			_addScoreMotion = LMotion.Create(0f, DURATION, DURATION)
				.WithScheduler(LiveMotion.Scheduler())
				.WithOnComplete(() => addScoreRoot.localScale = Vector3.zero)
				.Bind(elapsed =>
				{
					float progress = EaseUtility.Evaluate(Mathf.Clamp01(elapsed / SLIDE_DURATION), Ease.OutQuad);
					position.x = Mathf.LerpUnclamped(-0.5f, 0f, progress);
					addScoreRoot.localPosition = position;
					UpdateAddNumberAlpha(progress);
				});
		}

		private void OnDisable()
		{
			if (_addScoreMotion.TryCancel() && addScoreRoot != null)
			{
				addScoreRoot.localScale = Vector3.zero;
			}
		}

		private void UpdateAddNumberAlpha(float alpha)
		{
			if (addScoreNumber != null)
			{
				addScoreNumber.UpdateAlpha(alpha);
			}
			if (addScoreOutlineNumber != null)
			{
				addScoreOutlineNumber.UpdateAlpha(alpha);
			}
		}
	}
}
