using System;
using System.IO;

namespace OpenSekai.Shared.Storage
{
	/// <summary>Writes a file through a temporary sibling, so a crash mid-write never leaves half a file.</summary>
	public static class AtomicFile
	{
		public static void WriteAllText(string path, string text)
		{
			Write(path, temporary => File.WriteAllText(temporary, text));
		}

		public static void Write(string path, Action<string> writeTemporary)
		{
			string directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}

			string tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
			try
			{
				writeTemporary(tempPath);
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
