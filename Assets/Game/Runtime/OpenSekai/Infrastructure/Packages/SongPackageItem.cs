using System;
using OpenSekai.Shared.Packages;

namespace OpenSekai.Infrastructure.Packages
{
	public sealed class SongPackageItem
	{
		public SongPackage Entry { get; }

		public DateTime LastWriteTime { get; }

		public bool HasManifest { get; }

		public bool HasScore { get; }

		public bool HasAudio { get; }

		public bool HasJacket { get; }

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
			bool hasJacket)
		{
			Entry = entry;
			LastWriteTime = lastWriteTime;
			HasManifest = hasManifest;
			HasScore = hasScore;
			HasAudio = hasAudio;
			HasJacket = hasJacket;
		}
	}
}
