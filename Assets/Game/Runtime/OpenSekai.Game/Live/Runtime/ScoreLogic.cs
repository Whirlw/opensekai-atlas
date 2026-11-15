using OpenSekai.Shared.Settings;
using Sekai.Live;
using OpenSekai.Shared.Scoring;
using Sekai;

namespace Sekai.Core.Live
{
	public class ScoreLogic
	{
		private const int DEFAULT_TOTAL_SCORE = 1000000;

		private readonly LiveBundleBuildData _liveBundleBuildData;
		private MasterPlayLevelScore _scoreInfo;

		/// <summary>
		/// 已经分配过分数的音符数（不含装饰音符）。用于把 1,000,000 均匀摊到每个音符上：
		/// 第 i 个音符拿 round(1e6*(i+1)/n) - round(1e6*i/n)，n 个音符全 JustPerfect 时总分正好 =
		/// 1,000,000。直接按 1e6/n 取整会让每音符少一点，n=2019 的谱面满分只有 999,405。
		/// </summary>
		private int _scoredNoteCount;

		/// <summary>Live snapshot of the running session. Read-only from outside: only the board may write it.</summary>
		public LiveScore Score => _score;

		private LiveScore _score;

		public int BaseNoteScore { get; set; }

		public bool IsPerfectCombo
		{
			get
			{
				if (_score.badCount != 0 || _score.missCount != 0 || _score.goodCount != 0)
				{
					return false;
				}
				// If all notes are Auto, it's not a perfect combo
				if (_score.autoCount == _score.totalComboCount)
				{
					return false;
				}
				return true;
			}
		}

		public bool IsAllPerfectCombo
		{
			get
			{
				if (_score.badCount != 0 || _score.missCount != 0 || _score.goodCount != 0 || _score.greatCount != 0)
				{
					return false;
				}
				// If all notes are Auto, it's not an all perfect combo
				if (_score.autoCount == _score.totalComboCount)
				{
					return false;
				}
				return _score.IsAllPerfect;
			}
		}

		public ScoreLogic(LiveBundleBuildData data)
		{
			_liveBundleBuildData = data;
			_score.life = 1000;
			_score.rank = ScoreRank.D;
		}

		public virtual void Setup(LiveBootDataBase bootData, MusicScore musicScore)
		{
			_score = default;
			_scoreInfo = bootData?.MusicData?.Score;
			int totalCombo = LiveUtility.CalculateTotalComboCount(musicScore);
			if (totalCombo <= 0)
			{
				totalCombo = bootData?.MusicData?.TotalNoteCount ?? 0;
			}
			_score.totalComboCount = totalCombo;
			_score.life = _liveBundleBuildData != null && _liveBundleBuildData.Life > 0 ? _liveBundleBuildData.Life : 1000;
			_score.rank = ScoreRank.D;
			_score.totalScore = 0;
			_scoredNoteCount = 0;
			BaseNoteScore = totalCombo > 0 ? UnityEngine.Mathf.Max(1, DEFAULT_TOTAL_SCORE / totalCombo) : 0;
		}

		public virtual void ExecuteEvent(EventBase eventBase)
		{
		}

		public virtual void UpdateCombo(NoteBase note)
		{
			// Decoration notes are auto-judged and should not affect combo
			if (note == null || note.Result == NoteResult.None || !note.HasJudgment)
			{
				return;
			}

			NoteResult effectiveResult = note.Result;
			// In Fake Perfect mode, treat Auto as Perfect for combo counting
			if (effectiveResult == NoteResult.Auto && LiveSettingsStore.Shared.Load()?.UsesAutoFakePerfectMode == true)
			{
				effectiveResult = NoteResult.Perfect;
			}

			_score.combo = effectiveResult < NoteResult.Great ? 0 : _score.combo + 1;
			if (_score.combo > _score.maxCombo)
			{
				_score.maxCombo = _score.combo;
			}
		}

		public virtual void UpdateNoteResult(NoteBase note)
		{
			// Decoration notes are auto-judged and should not affect score counts
			if (note != null && !note.HasJudgment)
			{
				return;
			}

			switch (note?.Result)
			{
				case NoteResult.JustPerfect:
					_score.justPerfectCount++;
					_score.perfectCount++;
					break;
				case NoteResult.Perfect:
					_score.perfectCount++;
					break;
				case NoteResult.Great:
					_score.greatCount++;
					break;
				case NoteResult.Good:
					_score.goodCount++;
					break;
				case NoteResult.Auto:
					_score.autoCount++;
					break;
				case NoteResult.Bad:
					_score.badCount++;
					break;
				case NoteResult.Miss:
					_score.missCount++;
					break;
			}

			if (note != null && note.Description == NoteResultDescription.Fast)
			{
				_score.fastCount++;
			}
			else if (note != null && note.Description == NoteResultDescription.Late)
			{
				_score.lateCount++;
			}
			else if (note != null && note.Description == NoteResultDescription.FlickMiss)
			{
				_score.flickCount++;
			}
		}

		public virtual int CalculateAddScore(NoteBase note, float factor = 1f)
		{
			// Decoration notes do not contribute to score
			if (note == null || !note.HasJudgment)
			{
				return 0;
			}

			int noteBaseScore = TakeNextNoteBaseScore();
			int addScore = note.Result switch
			{
				// A just-perfect hit and a perfect hit pay the same: the tighter window is a visual
				// nuance, not a scoring one. Paying perfect only 90% made every all-PERFECT play fall
				// short of 1,000,000.
				NoteResult.JustPerfect => noteBaseScore,
				NoteResult.Perfect => noteBaseScore,
				NoteResult.Great => UnityEngine.Mathf.RoundToInt(noteBaseScore * 0.7f),
				NoteResult.Good => UnityEngine.Mathf.RoundToInt(noteBaseScore * 0.5f),
				NoteResult.Auto => noteBaseScore,
				NoteResult.Pass => 0,
				_ => 0
			};
			int actualAddScore = UnityEngine.Mathf.RoundToInt(addScore * factor);
			_score.totalScore += actualAddScore;
			UpdateScoreRank();
			return actualAddScore;
		}

		/// <summary>
		/// 取当前音符应得的基分：把 1,000,000 累积摊分，第 i 个音符 = round(1e6*(i+1)/n) - round(1e6*i/n)。
		/// 这样 n 个音符全 JustPerfect 时最后一次正好补上前面所有截断误差，总分严格等于 1,000,000。
		/// </summary>
		private int TakeNextNoteBaseScore()
		{
			int total = _score.totalComboCount;
			if (total <= 0)
			{
				_scoredNoteCount++;
				return BaseNoteScore;
			}

			int index = _scoredNoteCount;
			int nextIndex = index + 1;
			long weightNow = (long)DEFAULT_TOTAL_SCORE * index;
			long weightNext = (long)DEFAULT_TOTAL_SCORE * nextIndex;
			// 四舍五入到最近的整数分值，避免误差全部堆在最后一个音符上
			int cumulativeNow = (int)((weightNow + total / 2) / total);
			int cumulativeNext = (int)((weightNext + total / 2) / total);
			int baseScore = cumulativeNext - cumulativeNow;
			if (baseScore < 0)
			{
				baseScore = 0;
			}

			_scoredNoteCount = nextIndex;
			return baseScore;
		}

		public virtual void Damage(NoteBase note)
		{
			if (note == null)
			{
				return;
			}

			if (LiveConfig.Damages.TryGetValue(note.Result, out int damage))
			{
				_score.life = UnityEngine.Mathf.Max(0, _score.life - damage);
			}
		}

		public virtual void UpdateScoreRank()
		{
			if (_scoreInfo != null && (_scoreInfo.s > 0 || _scoreInfo.a > 0 || _scoreInfo.b > 0 || _scoreInfo.c > 0))
			{
				_score.rank = ScoreGauge.GetRank(_scoreInfo, _score.totalScore);
				return;
			}

			// OpenSekai fallback for custom scores: keep rank thresholds aligned with ScoreGaugeCalculator.Create(1000000).
			int rankS = UnityEngine.Mathf.FloorToInt(ScoreGauge.RANK_RATE_S * DEFAULT_TOTAL_SCORE);
			int rankA = UnityEngine.Mathf.FloorToInt(ScoreGauge.RANK_RATE_A * DEFAULT_TOTAL_SCORE);
			int rankB = UnityEngine.Mathf.FloorToInt(ScoreGauge.RANK_RATE_B * DEFAULT_TOTAL_SCORE);
			int rankC = UnityEngine.Mathf.FloorToInt(ScoreGauge.RANK_RATE_C * DEFAULT_TOTAL_SCORE);
			_score.rank = _score.totalScore >= rankS ? ScoreRank.S
				: _score.totalScore >= rankA ? ScoreRank.A
				: _score.totalScore >= rankB ? ScoreRank.B
				: _score.totalScore >= rankC ? ScoreRank.C
				: ScoreRank.D;
		}
	}
}
