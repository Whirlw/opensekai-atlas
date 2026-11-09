using System;
using Sekai;
using Sekai.Live;
using OpenSekai.Shared.Packages;

namespace OpenSekai.App.Playback
{
    /// <summary>Maps package metadata and already-converted notes into the retained Live boot types.</summary>
    internal static class LegacyLiveBootAdapter
    {
        internal static FreeLiveBootData Create(PlaySession session)
        {
            PlayRequest request = session.Request;
            SongPackage entry = session.Package;
            MusicScore score = session.Chart.Score;
            LiveSettingData settings = session.Settings;
            int deckId = UserDataManager.Instance.SelectedDeckId;
            MasterMusicDifficulty difficulty = CreateDifficulty(entry, score);
            MusicCategory category = ResolveCategory(settings);
            var boot = new FreeLiveBootData(entry.MusicId, difficulty.musicDifficulty, 0, deckId,
                LivePlayMode.Free, LiveMusicData.CollaborationModeState.Off, category)
            {
                LiveEventData = new LiveEventData(Array.Empty<IngameLotterySkill>(), Array.Empty<IngameComboCutin>(), deckId, request.IsAuto),
                LiveSettingData = settings,
                MVQualityType = settings.QualityType,
                MusicCategory = category,
                IsAuto = request.IsAuto,
                IsCustomMusicScore = true,
                IsOfficialMusicScore = false,
                ReturnScreenType = MenuScreenType.MusicScoreMakerTop,
                ReturnScene = request.ReturnScene,
                canSkipDisplayMusicInfo = false,
                ReleaseTransitionBeforeMusicStart = true,
                CustomMusicScoreId = entry.Manifest.id,
                CustomMusicScorePath = entry.RootDirectory,
                CustomMusicScoreTitle = entry.Manifest.scoreTitle,
                CustomMusicScoreAuthorName = entry.Manifest.userName,
                CustomMusicScoreCollaborationLabel = entry.Manifest.collaborationLabel
            };
            boot.MusicData.Music = CreateMusic(entry);
            boot.MusicData.Difficulty = difficulty;
            boot.MusicData.Vocal = CreateVocal(entry);
            boot.MusicData.Score = new MasterPlayLevelScore
            {
                liveType = LiveType.solo.ToString(),
                playLevel = entry.Manifest.playLevel
            };
            boot.MusicData.IsTestPlay = false;
            boot.MusicData.IsUseCustomScore = true;
            boot.MusicData.CustomPlayLevel = entry.Manifest.playLevel;
            boot.MusicData.MusicScore = score;
            boot.MusicData.StartMusicTimeMs = 0L;
            boot.MusicData.PlayStartEffectEnabled = !settings.SkipsCustomMusicScoreMusicInfo;
            return boot;
        }

        internal static MusicCategory ResolveCategory(LiveSettingData settings)
        {
            return settings?.UsesCustomMusicScore2DMVBackground == true ? MusicCategory.mv_2d : MusicCategory.image;
        }

        internal static MasterMusicDifficulty CreateDifficulty(SongPackage entry, MusicScore score)
        {
            string difficulty = entry.Manifest.musicDifficultyType;
            return new MasterMusicDifficulty
            {
                id = MasterMusicDifficulty.INVALID_ID,
                musicId = entry.MusicId,
                musicDifficulty = string.IsNullOrWhiteSpace(difficulty) || string.Equals(difficulty, "none", StringComparison.OrdinalIgnoreCase)
                    ? "master" : difficulty.ToLowerInvariant(),
                playLevel = entry.Manifest.playLevel,
                totalNoteCount = LiveUtility.CalculateTotalComboCount(score)
            };
        }

        internal static MasterMusic CreateMusic(SongPackage entry)
        {
            return new MasterMusic
            {
                id = entry.MusicId,
                title = entry.Manifest.title,
                lyricist = entry.Manifest.lyricist,
                composer = entry.Manifest.composer,
                arranger = entry.Manifest.arranger,
                assetbundleName = entry.AudioCueName,
                fillerSec = entry.Manifest.fillerSec,
                secForMusicScoreMaker = entry.MusicDurationSec,
                isAvailableForMusicScoreMaker = true
            };
        }

        internal static MasterMusicVocal CreateVocal(SongPackage entry)
        {
            return new MasterMusicVocal
            {
                id = 0,
                musicId = entry.MusicId,
                musicVocalType = MusicVocalType.original_song.ToString(),
                caption = entry.Manifest.singer,
                assetbundleName = entry.AudioCueName
            };
        }
    }
}
