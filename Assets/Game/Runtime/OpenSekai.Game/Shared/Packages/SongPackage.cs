using System;
using System.Collections.Generic;
using Sekai;
using Sekai.Live;
using System.IO;
using UnityEngine;
using OpenSekai.Shared.Packages;
namespace OpenSekai.Shared.Packages
{
    /// <summary>Package identity, metadata and paths. Contains no editor model or AudioClip.</summary>
    public sealed class SongPackage
    {
        public SongPackage(string rootDirectory, SongPackageManifest manifest)
        {
            RootDirectory = rootDirectory; Manifest = manifest ?? new SongPackageManifest();
            Manifest.Normalize(); MusicId = CreateStableMusicId(Manifest.id); AudioCueName = "custom_" + Manifest.id;
        }
		public string RootDirectory { get; }

		public SongPackageManifest Manifest { get; }

		public int MusicId { get; }

		public string AudioCueName { get; }

		public long AudioLengthMs { get; internal set; }

		public string ManifestPath => Path.Combine(RootDirectory, SongPackageStorage.MANIFEST_FILE_NAME);

		public string ScorePath => ResolveEntryPath(Manifest.scoreFileName);

		public string AudioPath => ResolveEntryPath(Manifest.audioFileName);

		public string JacketPath => ResolveEntryPath(Manifest.jacketFileName);

		public string VideoPath => ResolveEntryPath(Manifest.videoFileName);

		public int MusicDurationSec
		{
			get
			{
				if (Manifest.secForMusicScoreMaker > 0)
				{
					return Manifest.secForMusicScoreMaker;
				}
				if (AudioLengthMs > 0L)
				{
					float seconds = AudioLengthMs / 1000f - Manifest.fillerSec;
					return Mathf.Max(1, Mathf.CeilToInt(seconds));
				}
				return 120;
			}
		}


		private string ResolveEntryPath(string fileName)
		{
			string safeFileName = Path.GetFileName(string.IsNullOrEmpty(fileName) ? string.Empty : fileName);
			return string.IsNullOrEmpty(safeFileName) ? RootDirectory : Path.Combine(RootDirectory, safeFileName);
		}
		private static int CreateStableMusicId(string id)
		{
			unchecked
			{
				uint hash = 2166136261U;
				string source = string.IsNullOrEmpty(id) ? "custom" : id.ToLowerInvariant();
				for (int i = 0; i < source.Length; i++)
				{
					hash ^= source[i];
					hash *= 16777619U;
				}

				int value = (int)(hash & 0x7FFFFFFF);
				return value == 0 ? -1 : -value;
			}
		}
    }
}
