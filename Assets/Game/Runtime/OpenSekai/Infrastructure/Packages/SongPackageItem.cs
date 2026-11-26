using System;
using OpenSekai.Shared.Packages;

namespace OpenSekai.Infrastructure.Packages
{
	public sealed class SongPackageItem
	{
		public SongPackage Entry { get; }

		public DateTime LastWriteTime { get; }

		public DateTimeOffset ImportedAt { get; }

		public bool HasManifest { get; }

		public bool HasScore { get; }

		public bool HasAudio { get; }

		public bool HasJacket { get; }

		/// <summary>Content hash of the score file; the key the play history is stored under, shared by packages with the same chart.</summary>
		public string ScoreKey { get; }

		internal SongPackageSource Source { get; }
		internal string RankKey { get; }

		/// <summary>True once the audio has been measured, even when it came out silent or unreadable.</summary>
		public bool LoudnessMeasured { get; internal set; }

		/// <summary>Integrated loudness of the audio in LUFS; null while unmeasured or when it has none.</summary>
		public float? LoudnessLufs { get; internal set; }

		public string StatusText
		{
			get
			{
				if (!HasManifest)
				{
					return "缺少配置";
				}
				if (!HasScore)
				{
					return "缺少谱面";
				}
				if (!HasAudio)
				{
					return "缺少音频";
				}
				if (!HasJacket)
				{
					return "缺少封面";
				}
				return "就绪";
			}
		}

		public bool IsReadyForEdit => HasManifest;

		public SongPackageItem(
			SongPackage entry,
			DateTime lastWriteTime,
			bool hasManifest,
			bool hasScore,
			bool hasAudio,
			bool hasJacket,
			DateTimeOffset importedAt,
			string scoreKey = null,
			SongPackageSource source = null)
		{
			Source = source;
			RankKey = ChartRankCache.Key(source);
			ScoreKey = scoreKey ?? string.Empty;
			Entry = entry;
			LastWriteTime = lastWriteTime;
			ImportedAt = importedAt;
			HasManifest = hasManifest;
			HasScore = hasScore;
			HasAudio = hasAudio;
			HasJacket = hasJacket;
		}
	}
}
