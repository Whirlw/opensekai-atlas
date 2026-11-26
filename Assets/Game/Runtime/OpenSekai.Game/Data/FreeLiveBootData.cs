namespace Sekai
{
	public class FreeLiveBootData : LiveBootDataBase
	{
		public OpenSekai.Shared.Playback.DanLiveSong DanSong { get; set; }

		public OpenSekai.Shared.Playback.PracticePlan Practice { get; set; }

		internal double PracticeMinimum { get; private set; }
		internal double PracticeMaximum { get; private set; }

		public bool ResolvePracticeRange(double audioLengthSeconds)
		{
			var notes = LiveMusicData.ChartNoteBounds(MusicData.MusicScore);
			PracticeMinimum = System.Math.Min(0, MusicData.AudioToChartTime(0));
			PracticeMaximum = MusicData.AudioToChartTime(audioLengthSeconds);
			if (!float.IsNaN(notes.first))
			{
				PracticeMinimum = System.Math.Min(PracticeMinimum, notes.first);
				// Let the last note be judged when the chart continues past the audio.
				PracticeMaximum = System.Math.Max(PracticeMaximum, notes.last +
					OpenSekai.Shared.Live.Simulation.LiveTimelineSpan.RUN_OUT_SECONDS);
			}
			return System.Math.Max(Practice.StartSeconds, PracticeMinimum) <
				System.Math.Min(Practice.EndSeconds ?? PracticeMaximum, PracticeMaximum);
		}

		public string RetrySourceId { get; set; }
		public string RetrySource { get; set; }
		public System.Action<string, string, string, string> RecordRetryStart { get; set; }

		public int[] DeckFormationIndexes { get; set; }

		public string CustomMusicScoreId { get; set; }

		public string CustomMusicScorePath { get; set; }
		public byte[] TemporaryJacketData { get; set; }
		public LiveMaterialAudio MaterialAudio { get; set; }
		public LiveComboSkin ComboSkin { get; set; }
		public LiveSpriteSkin JudgmentSkin { get; set; }
		public LiveBackgroundSkin BackgroundSkin { get; set; }
		public LiveResultEffectSkin ResultFxSkin { get; set; }
		public LiveHudSkin HudSkin { get; set; }
		public LiveSpriteSkin LaneSkin { get; set; }
		public LiveNoteSkin NoteSkin { get; set; }
		public LiveHitEffectSkin HitFxSkin { get; set; }

		internal void ReleaseMaterialAudio()
		{
			BackgroundSkin?.Dispose();
			BackgroundSkin = null;
			ResultFxSkin?.Dispose();
			ResultFxSkin = null;
			HudSkin?.Dispose();
			HudSkin = null;
			LaneSkin?.Dispose();
			LaneSkin = null;
			NoteSkin?.Dispose();
			NoteSkin = null;
			HitFxSkin?.Dispose();
			HitFxSkin = null;
			JudgmentSkin?.Dispose();
			JudgmentSkin = null;
			ComboSkin?.Dispose();
			ComboSkin = null;
			MaterialAudio?.Dispose();
			MaterialAudio = null;
		}
#if UNITY_EDITOR
		internal int? ResultAnimationPreviewOverride { get; set; }
#endif

		/// <summary>Content hash of the package's score file; the play history key.</summary>
		public string CustomMusicScoreKey { get; set; }
		public bool CustomMusicScoreRanked { get; set; }

		public bool IsOfficialMusicScore { get; set; }

		public string CustomMusicScoreTitle { get; set; }

		public string CustomMusicScoreAuthorName { get; set; }

		public string CustomMusicScoreCollaborationLabel { get; set; }

		public MenuScreenType? ReturnScreenType { get; set; }

		public SceneManager.Scene? ReturnScene { get; set; }

		public FreeLiveBootData(int musicId, string difficulty, int vocalId, int deckId, int[] formationIndexes, LiveMusicData.CollaborationModeState collaboModeState, bool isOriginalMember = false, LiveScoreMode liveScoreMode = LiveScoreMode.Normal, LiveTournamentMode liveTournamentMode = LiveTournamentMode.None)
			: base(musicId, difficulty, vocalId, deckId, LiveType.solo, collaboModeState)
		{
			DeckFormationIndexes = formationIndexes ?? System.Array.Empty<int>();
			LiveScoreMode = liveScoreMode;
			LiveTournamentMode = liveTournamentMode;
			LiveModelData = new LiveModelData(isOriginalMember);
		}

		public FreeLiveBootData(int musicId, string difficulty, int vocalId)
			: base(musicId, difficulty, vocalId, LiveType.solo, LiveMusicData.CollaborationModeState.Off)
		{
			DeckFormationIndexes = System.Array.Empty<int>();
		}

		public FreeLiveBootData(int musicId, string difficulty, int vocalId, int deckId, LivePlayMode playMode, LiveMusicData.CollaborationModeState collaboModeState, MusicCategory category = MusicCategory.original)
			: base(musicId, difficulty, vocalId, deckId, LiveType.solo, collaboModeState)
		{
			DeckFormationIndexes = System.Array.Empty<int>();
			LivePlayMode = playMode;
			MusicCategory = category;
		}
	}
}
