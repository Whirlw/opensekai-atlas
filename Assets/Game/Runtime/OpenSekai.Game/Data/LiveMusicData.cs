using JetBrains.Annotations;
using Sekai.Live;

namespace Sekai
{
	public class LiveMusicData
	{
		public enum CollaborationModeState
		{
			Off = 0,
			On = 1
		}

		private MusicScore _musicScore;
		private int _cachedCustomTotalNoteCount;

		public MasterMusic Music { get; set; }

		public MasterMusicDifficulty Difficulty { get; set; }

		public MusicDifficulty DifficultyEnum { get; private set; }

		public string DifficultyString { get; private set; }

		public MasterMusicVocal Vocal { get; set; }

		public MasterPlayLevelScore Score { get; set; }

		public bool IsTestPlay { get; set; }

		public MusicScore MusicScore
		{
			get => _musicScore;
			set
			{
				_musicScore = value;
				_cachedCustomTotalNoteCount = LiveUtility.CalculateTotalComboCount(value);
			}
		}

		internal static (float first, float last) ChartNoteBounds(MusicScore musicScore)
		{
			float first = float.NaN;
			float last = float.NaN;
			if (musicScore?.HiddenSimLines?.Length > 0)
			{
				first = musicScore.FirstHiddenLineTime;
				last = musicScore.LastHiddenLineTime;
			}
			NoteBase[] roots = musicScore?.NoteArray;
			if (roots != null)
			{
				foreach (NoteBase root in roots)
				{
					if (root?.NoteList == null)
					{
						continue;
					}

					foreach (NoteBase note in root.NoteList)
					{
						if (note == null)
						{
							continue;
						}

						float time = note.MusicScoreInfo.time;
						if (float.IsNaN(first) || time < first)
						{
							first = time;
						}
						if (float.IsNaN(last) || time > last)
						{
							last = time;
						}
					}
				}
			}

			return (first, last);
		}

		public long StartMusicTimeMs { get; set; }

		/// <summary>For a NextRUSH+ chart, the audio-file time of chart second zero.</summary>
		public double? NativeBgmOffsetSeconds { get; set; }

		private double AudioTimeAtChartZero => NativeBgmOffsetSeconds ?? (Music?.fillerSec ?? 0f);

		public double AudioToChartTime(double audioSeconds) => audioSeconds - AudioTimeAtChartZero;

		public double ChartToAudioTime(double chartSeconds) => chartSeconds + AudioTimeAtChartZero;

		public bool PlayStartEffectEnabled { get; set; }

		public CollaborationModeState CollaboModeState { get; private set; }

		public bool IsUseCustomScore { get; set; }

		public int CustomPlayLevel { get; set; }

		public int PlayLevel => CustomPlayLevel > 0 ? CustomPlayLevel : Difficulty?.playLevel ?? 0;

		public int TotalNoteCount => _cachedCustomTotalNoteCount > 0 ? _cachedCustomTotalNoteCount : Difficulty?.totalNoteCount ?? 0;

		public LiveMusicData(int musicId, string difficulty, int vocalId, string liveType, CollaborationModeState collaboModeState)
		{
			DifficultyString = string.IsNullOrEmpty(difficulty) ? "master" : difficulty.ToLowerInvariant();
			DifficultyEnum = ParseDifficulty(DifficultyString);
			CollaboModeState = collaboModeState;
			Music = new MasterMusic
			{
				id = musicId,
				title = string.Empty,
				assetbundleName = musicId > 0 ? $"music_{musicId}" : string.Empty,
				fillerSec = 0f
			};
			Difficulty = new MasterMusicDifficulty
			{
				musicId = musicId,
				musicDifficulty = DifficultyString
			};
			Vocal = new MasterMusicVocal
			{
				id = vocalId,
				musicId = musicId,
				assetbundleName = musicId > 0 ? $"music_{musicId}_{vocalId}" : string.Empty
			};
			Score = new MasterPlayLevelScore { liveType = liveType, playLevel = 0 };
			PlayStartEffectEnabled = true;
		}

		private static MusicDifficulty ParseDifficulty(string value)
		{
			return System.Enum.TryParse(value, true, out MusicDifficulty result) ? result : MusicDifficulty.master;
		}
	}
}
