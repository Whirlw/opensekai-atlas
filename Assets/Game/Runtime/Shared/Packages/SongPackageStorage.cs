using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CP;
using Newtonsoft.Json;

using UnityEngine;
using OpenSekai.Shared.Packages;

namespace OpenSekai.Shared.Packages
{
	public static class SongPackageStorage
	{
		public const string DIRECTORY_NAME = "CustomMusicScores";
		public const string MANIFEST_FILE_NAME = "manifest.json";
		public const string PLAY_HISTORY_FILE_NAME = "PlayHistory.jsonl";

		private const string SHORT_ID_CHARS = "0123456789abcdefghijklmnopqrstuvwxyz";

		public static string RootDirectory => Path.Combine(Application.persistentDataPath, DIRECTORY_NAME);

		public static string PlayHistoryPath => Path.Combine(RootDirectory, PLAY_HISTORY_FILE_NAME);

		public static SongPackage[] LoadAllEntries()
		{
			if (!Directory.Exists(RootDirectory))
			{
				return Array.Empty<SongPackage>();
			}

			List<SongPackage> entries = new List<SongPackage>();
			foreach (string directory in Directory.GetDirectories(RootDirectory))
			{
				SongPackage entry = LoadEntry(directory);
				if (entry != null)
				{
					entries.Add(entry);
				}
			}

			return entries
				.OrderByDescending(x => File.GetLastWriteTimeUtc(x.ManifestPath))
				.ThenBy(x => x.Manifest.scoreTitle, StringComparer.OrdinalIgnoreCase)
				.ToArray();
		}

		public static SongPackage LoadFirstEntry()
		{
			SongPackage[] entries = LoadAllEntries();
			return entries.Length > 0 ? entries[0] : null;
		}

		public static SongPackage LoadEntry(string rootDirectory)
		{
			if (string.IsNullOrEmpty(rootDirectory))
			{
				return null;
			}

			string manifestPath = Path.Combine(rootDirectory, MANIFEST_FILE_NAME);
			if (!File.Exists(manifestPath))
			{
				return null;
			}

			try
			{
				SongPackageManifest manifest = JsonConvert.DeserializeObject<SongPackageManifest>(File.ReadAllText(manifestPath));
				if (manifest == null)
				{
					return null;
				}

				manifest.Normalize();
				return new SongPackage(rootDirectory, manifest);
			}
			catch (Exception exception)
			{
				LogUtility.LogWarning("Failed to load custom music score manifest. path:{0} error:{1}", manifestPath, exception.Message);
				return null;
			}
		}



		public static string GenerateShortId()
		{
			byte[] bytes = new byte[12];
			using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
			{
				rng.GetBytes(bytes);
			}

			char[] chars = new char[12];
			for (int i = 0; i < chars.Length; i++)
			{
				chars[i] = SHORT_ID_CHARS[bytes[i] % SHORT_ID_CHARS.Length];
			}
			return new string(chars);
		}

		public static string CreateFolderName(SongPackageManifest manifest)
		{
			if (manifest == null)
			{
				return GenerateShortId();
			}

			manifest.Normalize();
			string title = SanitizeFileName(manifest.title);
			if (string.IsNullOrEmpty(title))
			{
				title = "Untitled";
			}
			return title + "_" + manifest.id;
		}

		private static string SanitizeFileName(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return string.Empty;
			}

			HashSet<char> invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());
			char[] chars = value.Trim().Select(c => invalidChars.Contains(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray();
			string result = new string(chars);
			while (result.Contains("__"))
			{
				result = result.Replace("__", "_");
			}
			return result.Trim('_');
		}
	}
}
