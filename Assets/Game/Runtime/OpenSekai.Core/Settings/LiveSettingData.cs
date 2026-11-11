using Beebyte.Obfuscator;
using Newtonsoft.Json;

namespace Sekai
{
	[Skip]
	public class LiveSettingData
	{
		public enum LiveModeType
		{
			High3D = 0,
			Default3D = 1,
			Mode2D = 2,
			Low = 3,
			OriginalMV = 4
		}

		public const int MUSIC_INFO_DISPLAY_MODE_NORMAL = 0;

		public const int MUSIC_INFO_DISPLAY_MODE_CUSTOM_SCORE = 1;

		public const int MUSIC_INFO_DISPLAY_MODE_SKIP = 2;

		public const int AUTO_FAKE_PERFECT_MODE_AUTO = 0;

		public const int AUTO_FAKE_PERFECT_MODE_FAKE_PERFECT = 1;

		public const int AUTO_RESULT_ANIMATION_NONE = 0;

		public const int AUTO_RESULT_ANIMATION_ALL_PERFECT = 1;

		public const int AUTO_RESULT_ANIMATION_FULL_COMBO = 2;

		public const int AUTO_RESULT_ANIMATION_CLEAR = 3;

		public const int AUTO_RESULT_ANIMATION_FINISH = 4;

		public const int CUSTOM_MUSIC_SCORE_BACKGROUND_MODE_JACKET = 0;

		public const int CUSTOM_MUSIC_SCORE_BACKGROUND_MODE2_DMV = 1;

		public float NoteSpeed { get; set; }

		public float TimingAdjustData { get; set; }

		public float Brightness { get; set; }

		public float LaneTransparent { get; set; }

		public bool UseCutIn { get; set; }

		public bool HiddenSkillAndPraise { get; set; }

		public bool UseSimultaneousPushingLine { get; set; }

		public bool UseVibration { get; set; }

		public bool UseAllPerfectEffect { get; set; }

		public LiveModeType LiveMode { get; set; }

		public float NoteAlpha { get; set; }

		public float GuideAlpha { get; set; }

		public float JudgeLineAlpha { get; set; }

		public int NoteSkinIndex { get; set; }

		public int AutoSaveIntervalIndex { get; set; }

		public int ScoreMakerPreviewModeIndex { get; set; }

		public int NoteSeIndex { get; set; }

		public bool IsMirror { get; set; }

		public MVQualityType QualityType { get; set; }

		public bool IsFastLateFlick { get; set; }

		public bool Use120FPS { get; set; }

		/// <summary>
		/// Frame rate cap for live play, in Hz. 0 follows the display refresh rate.
		/// The runtime limits this to supported refresh rates.
		/// </summary>
		public int FrameRateLimit { get; set; }

		/// <summary>
		/// Judge an input at the frame it was read (legacy) instead of at the moment the touch was sampled.
		/// The legacy path multiplies the touch latency by one frame time and therefore shrinks it 60x;
		/// it is kept for players who prefer that feel.
		/// </summary>
		public bool UseLegacyJudgeTiming { get; set; }

		public bool? UseVSync { get; set; }

		public int NoteEffect { get; set; }

		public float _noteShowRate { get; set; }

		[JsonIgnore]
		public float NoteShowRate
		{
			get
			{
				return 1f - _noteShowRate;
			}
			set
			{
				_noteShowRate = 1f - value;
			}
		}

		public int FeverEffectTypeIndex { get; set; }

		public int? TotalPowerUpperLimit { get; set; }

		public int? TotalPowerLowerLimit { get; set; }

		public int? CustomRoomTotalPowerUpperLimit { get; set; }

		public int? CustomRoomTotalPowerLowerLimit { get; set; }

		public bool ShowsRoomId { get; set; }

		public int CustomRoomScoreSettingIndex { get; set; }

		public bool CustomRoomIsDisplayPlayerInfo { get; set; }

		public int[] CustomRoomSelectedDifficulties { get; set; }

		public int CustomRoomSelectedMusicType { get; set; }

		public int ScoreSelectType { get; set; }

		public int? CustomMusicScoreMusicInfoDisplayMode { get; set; }

		public int? CustomMusicScoreLiveBackgroundMode { get; set; }

		public int? CustomMusicScoreAutoFakePerfectMode { get; set; }

		public int? CustomMusicScoreAutoResultAnimation { get; set; }

		public bool UseMaimaiMusicInfoSe { get; set; }

		[JsonIgnore]
		public bool UsesCustomMusicScoreMusicInfoDisplay
		{
			get
			{
				return (CustomMusicScoreMusicInfoDisplayMode ?? MUSIC_INFO_DISPLAY_MODE_CUSTOM_SCORE) == MUSIC_INFO_DISPLAY_MODE_CUSTOM_SCORE;
			}
		}

		[JsonIgnore]
		public bool SkipsCustomMusicScoreMusicInfo
		{
			get
			{
				return (CustomMusicScoreMusicInfoDisplayMode ?? MUSIC_INFO_DISPLAY_MODE_CUSTOM_SCORE) == MUSIC_INFO_DISPLAY_MODE_SKIP;
			}
		}

		[JsonIgnore]
		public bool UsesCustomMusicScore2DMVBackground
		{
			get
			{
				return (CustomMusicScoreLiveBackgroundMode ?? CUSTOM_MUSIC_SCORE_BACKGROUND_MODE2_DMV) == CUSTOM_MUSIC_SCORE_BACKGROUND_MODE2_DMV;
			}
		}

		[JsonIgnore]
		public bool UsesAutoFakePerfectMode
		{
			get
			{
				return (CustomMusicScoreAutoFakePerfectMode ?? AUTO_FAKE_PERFECT_MODE_AUTO) == AUTO_FAKE_PERFECT_MODE_FAKE_PERFECT;
			}
		}

		[JsonIgnore]
		public int AutoResultAnimationMode
		{
			get
			{
				return CustomMusicScoreAutoResultAnimation ?? AUTO_RESULT_ANIMATION_NONE;
			}
		}

		public LiveSettingData()
		{
			ShowsRoomId = true;
			NoteAlpha = 1f;
			GuideAlpha = 0.6f;
			JudgeLineAlpha = 1f;
			AutoSaveIntervalIndex = 0;
			ScoreMakerPreviewModeIndex = 0;
			CustomRoomIsDisplayPlayerInfo = true;
			UseCutIn = true;
			LiveMode = LiveModeType.Default3D;
			UseAllPerfectEffect = true;
			QualityType = MVQualityType.Default;
			Use120FPS = false;
			FrameRateLimit = 0;
			UseLegacyJudgeTiming = false;
			NoteEffect = 0;
			_noteShowRate = 0f;
			FeverEffectTypeIndex = 0;
			UseSimultaneousPushingLine = true;
			UseVibration = true;
			NoteSpeed = 6f;
			TimingAdjustData = 0f;
			Brightness = 1f;
			LaneTransparent = 1f;
			CustomMusicScoreMusicInfoDisplayMode = MUSIC_INFO_DISPLAY_MODE_CUSTOM_SCORE;
			CustomMusicScoreLiveBackgroundMode = CUSTOM_MUSIC_SCORE_BACKGROUND_MODE2_DMV;
			UseMaimaiMusicInfoSe = false;
		}

        internal LiveSettingData CopyForPlayback()
        {
            var copy = (LiveSettingData)MemberwiseClone();
            copy.CustomRoomSelectedDifficulties = CustomRoomSelectedDifficulties == null ? null : (int[])CustomRoomSelectedDifficulties.Clone();
            return copy;
        }

		public static string GetLiveModeTextKey(LiveModeType mode)
		{
			switch (mode)
			{
				case LiveModeType.High3D:
				case LiveModeType.Default3D:
					return "WORD_3DMV";
				case LiveModeType.Mode2D:
					return "WORD_2DMV";
				case LiveModeType.Low:
					return "WORD_2D";
				case LiveModeType.OriginalMV:
					return "WORD_ORIGINAL_MV";
				default:
					return "WORD_3DMV";
			}
		}

		public float GetNoteAlpha()
		{
			return NoteAlpha == 0f ? 1f : NoteAlpha;
		}

		public float GetGuideAlpha()
		{
			return GuideAlpha == 0f ? 0.6f : GuideAlpha;
		}

		public float GetJudgeLineAlpha()
		{
			return JudgeLineAlpha == 0f ? 1f : JudgeLineAlpha;
		}

		public void SetNoteShowRate(float optionValue)
		{
			_noteShowRate = optionValue / 100f;
		}

		public static float CalculateNoteShowRate(float optionValue)
		{
			return 1f - optionValue / 100f;
		}
	}
}
