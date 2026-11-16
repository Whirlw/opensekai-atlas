using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;
using OpenSekai.Shared.Packages;
using UnityEngine;

namespace OpenSekai.Infrastructure.Packages
{
	/// <summary>
	/// The list of content hashes for the packages in one library folder. A package's hash is the
	/// SHA-256 of its manifest.json followed by its score file, and it is the key its play history
	/// is stored under: two packages only share scores when both files are byte-identical.
	/// The list lives next to the packages so that a library is never rehashed in full on load; an
	/// entry is reused while both files keep the size and write time they had when it was computed.
	/// </summary>
	public sealed class SongPackageHashIndex
	{
		public const string FILE_NAME = "package_hashes.json";

		private readonly string _path;
		private readonly Dictionary<string, Entry> _entries;
		private bool _dirty;

		private SongPackageHashIndex(string libraryDirectory)
		{
			_path = Path.Combine(libraryDirectory, FILE_NAME);
			_entries = Read(_path);
		}

		/// <summary>Opens the list of the library folder that holds <paramref name="libraryDirectory"/>'s packages.</summary>
		public static SongPackageHashIndex Open(string libraryDirectory)
		{
			return new SongPackageHashIndex(Path.GetFullPath(libraryDirectory));
		}

		/// <summary>
		/// Hash of one package, reusing the list of the folder the package sits in. Read-only: the
		/// list is written by library loads and imports, so playing a package from any folder never
		/// leaves a file there; the computed hash is the same either way.
		/// </summary>
		public static string HashOf(SongPackage entry)
		{
			if (entry == null || string.IsNullOrEmpty(entry.RootDirectory))
			{
				return string.Empty;
			}

			string library = Path.GetDirectoryName(Path.GetFullPath(entry.RootDirectory).TrimEnd(
				Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			if (string.IsNullOrEmpty(library))
			{
				return ComputeHash(entry.ManifestPath, entry.ScorePath);
			}

			return Open(library).GetOrCompute(entry);
		}

		/// <summary>
		/// The stored hash when the package's files have not changed since it was computed, otherwise a
		/// freshly computed one, which replaces the stored entry. Empty when the manifest cannot be read.
		/// </summary>
		public string GetOrCompute(SongPackage entry)
		{
			if (entry == null || string.IsNullOrEmpty(entry.RootDirectory))
			{
				return string.Empty;
			}

			string key = KeyOf(entry.RootDirectory);
			Stamp manifest = Stamp.Of(entry.ManifestPath);
			Stamp score = Stamp.Of(entry.ScorePath);
			if (_entries.TryGetValue(key, out Entry stored)
				&& !string.IsNullOrEmpty(stored.hash)
				&& string.Equals(stored.scoreFileName, entry.Manifest?.scoreFileName ?? string.Empty, StringComparison.Ordinal)
				&& stored.manifest.Equals(manifest)
				&& stored.score.Equals(score))
			{
				return stored.hash;
			}

			string hash = ComputeHash(entry.ManifestPath, entry.ScorePath);
			if (string.IsNullOrEmpty(hash))
			{
				return string.Empty;
			}

			_entries[key] = new Entry
			{
				hash = hash,
				scoreFileName = entry.Manifest?.scoreFileName ?? string.Empty,
				manifest = manifest,
				score = score
			};
			_dirty = true;
			return hash;
		}

		/// <summary>Drops entries whose package folder is gone, so the list does not grow forever.</summary>
		public void Prune(IEnumerable<string> liveRootDirectories)
		{
			var live = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string directory in liveRootDirectories)
			{
				live.Add(KeyOf(directory));
			}

			var stale = new List<string>();
			foreach (string key in _entries.Keys)
			{
				if (!live.Contains(key))
				{
					stale.Add(key);
				}
			}

			foreach (string key in stale)
			{
				_entries.Remove(key);
				_dirty = true;
			}
		}

		public void Save()
		{
			if (!_dirty)
			{
				return;
			}

			string tempPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(_path));
				File.WriteAllText(tempPath, JsonConvert.SerializeObject(_entries, Formatting.Indented));
				if (File.Exists(_path))
				{
					File.Replace(tempPath, _path, null);
				}
				else
				{
					File.Move(tempPath, _path);
				}
				_dirty = false;
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
			{
				Debug.LogWarning("[Library] Could not save package hashes: " + exception.Message);
			}
			finally
			{
				if (File.Exists(tempPath))
				{
					File.Delete(tempPath);
				}
			}
		}

		/// <summary>
		/// SHA-256 over manifest.json then the score file, each prefixed with its byte length so the
		/// boundary between them is part of the hash. A missing score hashes as an empty one.
		/// </summary>
		public static string ComputeHash(string manifestPath, string scorePath)
		{
			if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
			{
				return string.Empty;
			}

			try
			{
				byte[] manifest = File.ReadAllBytes(manifestPath);
				byte[] score = !string.IsNullOrEmpty(scorePath) && File.Exists(scorePath)
					? File.ReadAllBytes(scorePath)
					: Array.Empty<byte>();
				using (SHA256 sha = SHA256.Create())
				{
					AppendBlock(sha, manifest);
					AppendBlock(sha, score);
					sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
					return ToHex(sha.Hash);
				}
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
			{
				Debug.LogWarning("[Library] Could not hash package: " + exception.Message);
				return string.Empty;
			}
		}

		private static void AppendBlock(SHA256 sha, byte[] data)
		{
			byte[] length = BitConverter.GetBytes((long)data.Length);
			if (!BitConverter.IsLittleEndian)
			{
				Array.Reverse(length);
			}
			sha.TransformBlock(length, 0, length.Length, null, 0);
			sha.TransformBlock(data, 0, data.Length, null, 0);
		}

		private static string ToHex(byte[] hash)
		{
			var builder = new System.Text.StringBuilder(hash.Length * 2);
			for (int i = 0; i < hash.Length; i++)
			{
				builder.Append(hash[i].ToString("x2"));
			}
			return builder.ToString();
		}

		/// <summary>The package folder's own name: the list is per library folder, and moving the library keeps it valid.</summary>
		private static string KeyOf(string rootDirectory)
		{
			return Path.GetFileName(Path.GetFullPath(rootDirectory).TrimEnd(
				Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		}

		private static Dictionary<string, Entry> Read(string path)
		{
			var empty = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
			if (!File.Exists(path))
			{
				return empty;
			}

			try
			{
				var stored = JsonConvert.DeserializeObject<Dictionary<string, Entry>>(File.ReadAllText(path));
				return stored != null
					? new Dictionary<string, Entry>(stored, StringComparer.OrdinalIgnoreCase)
					: empty;
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
				|| exception is JsonException)
			{
				// A damaged list only costs a rehash of every package.
				Debug.LogWarning("[Library] Could not read package hashes: " + exception.Message);
				return empty;
			}
		}

		private sealed class Entry
		{
			public string hash;
			public string scoreFileName;
			public Stamp manifest;
			public Stamp score;
		}

		private struct Stamp : IEquatable<Stamp>
		{
			public long length;
			public long writeTicksUtc;

			public static Stamp Of(string path)
			{
				if (string.IsNullOrEmpty(path) || !File.Exists(path))
				{
					return new Stamp { length = -1, writeTicksUtc = 0 };
				}

				var info = new FileInfo(path);
				return new Stamp { length = info.Length, writeTicksUtc = info.LastWriteTimeUtc.Ticks };
			}

			public bool Equals(Stamp other) => length == other.length && writeTicksUtc == other.writeTicksUtc;
		}
	}
}
