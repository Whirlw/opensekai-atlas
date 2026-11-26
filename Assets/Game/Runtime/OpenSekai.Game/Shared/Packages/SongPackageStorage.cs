using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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


		public static string RootDirectory => Path.Combine(Application.persistentDataPath, DIRECTORY_NAME);

		public static string PlayHistoryPath => Path.Combine(ActivePlayerDirectory(RootDirectory), PLAY_HISTORY_FILE_NAME);

		// Before the first restore, games continue appending to the original history file.
		public static string ActivePlayerDirectory(string root)
		{
			string pointer = Path.Combine(root, "PlayerArchives", "active");
			if (!File.Exists(pointer)) return root;
			string generation = File.ReadAllText(pointer);
			if (!Guid.TryParseExact(generation, "N", out _))
				throw new InvalidDataException("Invalid player archive identifier.");
			string directory = Path.Combine(root, "PlayerArchives", generation);
			if (!File.Exists(Path.Combine(directory, PLAY_HISTORY_FILE_NAME)))
				throw new InvalidDataException("Active player archive is missing.");
			return directory;
		}

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
			try
			{
				RejectReparsePoint(rootDirectory);
				RejectReparsePoint(manifestPath);
				if (!File.Exists(manifestPath)) return null;
				SongPackageManifest manifest = JsonConvert.DeserializeObject<SongPackageManifest>(File.ReadAllText(manifestPath));
				if (manifest == null)
				{
					return null;
				}

				manifest.Normalize();
				ValidateManifestPaths(manifest);
				return new SongPackage(rootDirectory, manifest);
			}
			catch (Exception exception)
			{
				LogUtility.LogWarning("Failed to load custom music score manifest. path:{0} error:{1}", manifestPath, exception.Message);
				return null;
			}
		}



		public static string GenerateShortId() => SongPackageManifest.GenerateShortId();

		public static string CreateFolderName(SongPackageManifest manifest)
		{
			if (manifest == null)
			{
				return GenerateShortId();
			}

			manifest.Normalize();
			ValidatePathComponent(manifest.id, nameof(manifest.id));
			string title = SanitizeFileName(manifest.title);
			if (string.IsNullOrEmpty(title))
			{
				title = "Untitled";
			}
			string name = title + "_" + manifest.id;
			return IsReservedDeviceName(name) ? "_" + name : name;
		}

		public static void ValidateManifestPaths(SongPackageManifest manifest)
		{
			ValidatePathComponent(manifest.id, nameof(manifest.id));
			ValidateOptionalFileName(manifest.scoreFileName, nameof(manifest.scoreFileName));
			ValidateOptionalFileName(manifest.audioFileName, nameof(manifest.audioFileName));
			ValidateOptionalFileName(manifest.jacketFileName, nameof(manifest.jacketFileName));
			ValidateOptionalFileName(manifest.videoFileName, nameof(manifest.videoFileName));
		}

		private static void ValidateOptionalFileName(string value, string field)
		{
			if (!string.IsNullOrEmpty(value)) ValidatePathComponent(value, field);
		}

		public static void ValidatePathComponent(string value, string field)
		{
			if (string.IsNullOrWhiteSpace(value) || value.EndsWith(".", StringComparison.Ordinal)
				|| value.EndsWith(" ", StringComparison.Ordinal) || value.Any(IsInvalidFileNameChar)
				|| IsReservedDeviceName(value))
			{
				throw new InvalidDataException("曲包路径字段必须是单个有效的文件名：" + field);
			}
		}

		public static bool IsInvalidFileNameChar(char value) => char.IsControl(value) || "<>:\"/\\|?*".IndexOf(value) >= 0;

		private static bool IsReservedDeviceName(string value)
		{
			int dot = value.IndexOf('.');
			string name = (dot < 0 ? value : value.Substring(0, dot)).TrimEnd(' ').ToUpperInvariant();
			return name is "CON" or "PRN" or "AUX" or "NUL"
				|| name.Length == 4 && (name.StartsWith("COM", StringComparison.Ordinal) || name.StartsWith("LPT", StringComparison.Ordinal))
					&& "123456789¹²³".IndexOf(name[3]) >= 0;
		}

		public static void RejectReparsePoint(string path)
		{
			try
			{
				string fullPath = Path.GetFullPath(path);
				if (fullPath.Length > Path.GetPathRoot(fullPath).Length)
					fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
					throw new InvalidDataException("曲包路径不支持符号链接或目录联接。");
			}
			catch (FileNotFoundException) { }
			catch (DirectoryNotFoundException) { }
		}

		private static string SanitizeFileName(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return string.Empty;
			}

			char[] chars = value.Trim().Select(c => IsInvalidFileNameChar(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray();
			string result = new string(chars);
			while (result.Contains("__"))
			{
				result = result.Replace("__", "_");
			}
			return result.Trim('_');
		}
	}
}
