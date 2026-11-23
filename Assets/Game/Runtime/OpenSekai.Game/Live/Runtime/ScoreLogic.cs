using OpenSekai.Shared.Settings;
using Sekai.Live;
using OpenSekai.Shared.Scoring;
using Sekai;

namespace Sekai.Core.Live
{
	public class ScoreLogic
	{
		private const int DEFAULT_TOTAL_SCORE = ScoreGauge.MAXIMUM_SCORE;
		private const int DAMAGE_REFERENCE_NOTE_COUNT = 1000;
		private const int MAX_STANDARD_NOTE_DAMAGE = 500;

		private readonly LiveBundleBuildData _liveBundleBuildData;
		private ScoreGauge _gauge = ScoreGauge.Standard;

		/// <summary>
		/// 已经分配过分数的音符数（不含装饰音符）。用于把 1,000,000 均匀摊到每个音符上：
		/// 第 i 个音符拿 round(1e6*(i+1)/n) - round(1e6*i/n)，n 个音符全 Perfect 时总分正好 =
		/// 1,000,000。直接按 1e6/n 取整会让每音符少一点，n=2019 的谱面满分只有 999,405。
		/// </summary>
		private int _scoredNoteCount;
		internal int RemainingNoteCount { get; private set; }
		internal bool UseSubtractiveScore { get; private set; }

		internal LiveScore HudScore
		{
			get
			{
				if (!UseSubtractiveScore) return _score;
				// A timed-out hold head consumes a judgement but no additive score slot.
				// Price the remaining Perfects starting at the next actual score slot.
				var hud = _score;
				hud.totalScore += _score.totalComboCount > 0
					? CumulativeBaseScore(_scoredNoteCount + RemainingNoteCount) - CumulativeBaseScore(_scoredNoteCount)
					: DEFAULT_TOTAL_SCORE;
				hud.rank = _gauge.RankOf(hud.totalScore);
				return hud;
			}
		}

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

		private bool _fakePerfect;
		private OpenSekai.Shared.Playback.DanLiveSong _dan;

		public ScoreLogic(LiveBundleBuildData data)
		{
			_liveBundleBuildData = data;
			_score.life = 1000;
			_score.rank = ScoreRank.D;
		}

		public virtual void Setup(LiveBootDataBase bootData, MusicScore musicScore)
		{
			_score = default;
			_gauge = ScoreGauge.Create(bootData?.MusicData?.Score);
			// The live's own settings snapshot decides; the stored settings only when there is none.
			var settings = bootData?.LiveSettingData ?? LiveSettingsStore.Shared.Load();
			_fakePerfect = settings.UsesAutoFakePerfectMode;
			UseSubtractiveScore = settings.UseSubtractiveScore;
			int totalCombo = LiveUtility.CalculateTotalComboCount(musicScore);
			if (totalCombo <= 0)
			{
				totalCombo = bootData?.MusicData?.TotalNoteCount ?? 0;
			}
			_score.totalComboCount = totalCombo;
			RemainingNoteCount = totalCombo;
			_dan = (bootData as FreeLiveBootData)?.DanSong;
			_score.life = _dan?.StartingLife ?? musicScore?.InitialLife
				?? (_liveBundleBuildData != null && _liveBundleBuildData.Life > 0 ? _liveBundleBuildData.Life : 1000);
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

			RemainingNoteCount = System.Math.Max(0, RemainingNoteCount - 1);
			NoteResult effectiveResult = note.Result;
			// In Fake Perfect mode, treat Auto as Perfect for combo counting
			if (effectiveResult == NoteResult.Auto && _fakePerfect)
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
				NoteResult.Perfect => noteBaseScore,
				NoteResult.Great => UnityEngine.Mathf.RoundToInt(noteBaseScore * 0.85f),
				NoteResult.Good => UnityEngine.Mathf.RoundToInt(noteBaseScore * 0.5f),
				NoteResult.Bad => UnityEngine.Mathf.RoundToInt(noteBaseScore * 0.2f),
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
		/// 这样 n 个音符全 Perfect 时最后一次正好补上前面所有截断误差，总分严格等于 1,000,000。
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
			_scoredNoteCount = nextIndex;
			return System.Math.Max(0, CumulativeBaseScore(nextIndex) - CumulativeBaseScore(index));
		}

		private int CumulativeBaseScore(int count) =>
			(int)(((long)DEFAULT_TOTAL_SCORE * count + _score.totalComboCount / 2) / _score.totalComboCount);

		public virtual void Damage(NoteBase note)
		{
			if (note == null)
			{
				return;
			}

			if (_dan != null)
			{
				if (!note.HasJudgment || note is NextRushDamageNote && note.Result != NoteResult.Miss) return;
				int life = System.Math.Max(0, _score.life - _dan.Damage.For(note.Result));
				if (life == _score.life) return;
				_score.life = life;
				_dan.LifeChanged?.Invoke(life);
				return;
			}

			if (note is NextRushDamageNote)
			{
				if (note.Result == NoteResult.Miss)
					_score.life = UnityEngine.Mathf.Max(0, _score.life - NextRushDamageNote.LIFE_PENALTY);
				return;
			}

			int totalCombo = _score.totalComboCount;
			if (totalCombo <= 0) return;

			int referenceDamage = note.Result switch
			{
				NoteResult.Bad => 15,
				NoteResult.Miss => 20,
				_ => 0
			};
			int damage = (int)System.Math.Ceiling((double)referenceDamage * DAMAGE_REFERENCE_NOTE_COUNT / totalCombo);
			damage = System.Math.Min(MAX_STANDARD_NOTE_DAMAGE, damage);
			_score.life = UnityEngine.Mathf.Max(0, _score.life - damage);
		}

		public virtual void UpdateScoreRank()
		{
			_score.rank = _gauge.RankOf(_score.totalScore);
		}
	}
}
