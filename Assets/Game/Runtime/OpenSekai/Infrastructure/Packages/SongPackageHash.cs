using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace OpenSekai.Infrastructure.Packages
{
	/// <summary>
	/// A chart's content hash: SHA-256 of the package's score file alone. It is the key play history
	/// is stored under, so scores follow the chart: packages with byte-identical score files share
	/// them (a re-import gets its old scores back), and editing a package's info keeps them.
	/// </summary>
	public static class SongPackageHash
	{
		/// <summary>Empty when there is no score file to hash, so a package without a chart never shares a key.</summary>
		public static string Compute(string scorePath)
		{
			if (string.IsNullOrEmpty(scorePath) || !File.Exists(scorePath))
			{
				return string.Empty;
			}

			try
			{
				using (SHA256 sha = SHA256.Create())
				using (FileStream stream = File.OpenRead(scorePath))
				{
					return ToHex(sha.ComputeHash(stream));
				}
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
			{
				Debug.LogWarning("[Library] Could not hash chart: " + exception.Message);
				return string.Empty;
			}
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
	}

	/// <summary>Size and write time of one file; lets a rescan reuse a hash whose inputs did not change.</summary>
	public struct FileStamp : IEquatable<FileStamp>
	{
		public long length;
		public long writeTicksUtc;

		public static FileStamp Of(string path)
		{
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				return new FileStamp { length = -1, writeTicksUtc = 0 };
			}

			var info = new FileInfo(path);
			return new FileStamp { length = info.Length, writeTicksUtc = info.LastWriteTimeUtc.Ticks };
		}

		public bool Equals(FileStamp other) => length == other.length && writeTicksUtc == other.writeTicksUtc;

		public override bool Equals(object obj) => obj is FileStamp other && Equals(other);

		public override int GetHashCode() => (length.GetHashCode() * 397) ^ writeTicksUtc.GetHashCode();
	}

	/// <summary>Writes a file through a temporary sibling, so a crash mid-write never leaves half a file.</summary>
	internal static class AtomicFile
	{
		internal static void WriteAllText(string path, string text)
		{
			string directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}

			string tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
			try
			{
				File.WriteAllText(tempPath, text);
				if (File.Exists(path))
				{
					File.Replace(tempPath, path, null);
				}
				else
				{
					File.Move(tempPath, path);
				}
			}
			finally
			{
				if (File.Exists(tempPath))
				{
					File.Delete(tempPath);
				}
			}
		}
	}
}
