using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using OpenSekai.Charts;
using OpenSekai.Charts.Serialization;
using OpenSekai.Infrastructure.Charts;
using UnityEngine;
using OpenSekai.Shared.Packages;

namespace OpenSekai.Infrastructure.Packages
{
	public sealed class SongPackageManager
    {
        private static SongPackageManager _shared;
        public static SongPackageManager Shared => _shared ?? (_shared = new SongPackageManager(SongPackageStorage.RootDirectory));
        public string RootDirectory { get; }
        private readonly string _temporaryDirectory;
        public SongPackageManager(string rootDirectory, string _temporaryDirectory = null)
        {
            RootDirectory = Path.GetFullPath(rootDirectory);
            this._temporaryDirectory = _temporaryDirectory ?? Path.Combine(Application.temporaryCachePath, "CustomMusicScoreManager");
        }
		private const string EXPORT_DIRECTORY_NAME = "Exports";

		private const int JACKET_TEXTURE_SIZE = 740;

		private readonly HashSet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			".ogg",
			".mp3",
			".wav"
		};

		private readonly HashSet<string> JacketExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			".png",
			".jpg",
			".jpeg"
		};

		private readonly HashSet<string> ScoreExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			".json",
			".txt",
			".sus"
		};

		private readonly HashSet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			".mp4"
		};

		public IReadOnlyList<SongPackageItem> LoadItems()
		{
			Directory.CreateDirectory(RootDirectory);
			List<SongPackageItem> items = new List<SongPackageItem>();
			foreach (string directory in Directory.GetDirectories(RootDirectory))
			{
				SongPackage entry = SongPackageStorage.LoadEntry(directory);
				if (entry == null)
				{
					continue;
				}

				items.Add(CreateItem(entry));
			}

			return items
				.OrderByDescending(x => x.LastWriteTime)
				.ThenBy(x => x.Entry.Manifest.scoreTitle, StringComparer.OrdinalIgnoreCase)
				.ToArray();
		}

		public SongPackage CreateNewEntry()
		{
			Directory.CreateDirectory(RootDirectory);
			SongPackageManifest manifest = new SongPackageManifest
			{
				id = SongPackageStorage.GenerateShortId(),
				title = "未命名",
				scoreTitle = "未命名谱面",
				userName = Environment.UserName,
				composer = string.Empty,
				lyricist = string.Empty,
				arranger = string.Empty,
				singer = string.Empty,
				collaborationLabel = string.Empty,
				audioFileName = "audio.ogg",
				jacketFileName = "jacket.png",
				scoreFileName = "score.json",
				videoFileName = string.Empty,
				fillerSec = 0f,
				secForMusicScoreMaker = 120,
				previewStartTimeSec = 0f,
				musicDifficultyType = "master",
				playLevel = 1
			};
			manifest.Normalize();

			string directory = GetUniqueDirectory(Path.Combine(RootDirectory, SongPackageStorage.CreateFolderName(manifest)));
			Directory.CreateDirectory(directory);
			WriteManifest(directory, manifest);
			WriteDefaultScore(directory, manifest);
			return SongPackageStorage.LoadEntry(directory);
		}

		public SongPackage DuplicateEntry(SongPackage source)
		{
			if (source == null || !Directory.Exists(source.RootDirectory))
			{
				return null;
			}

			SongPackageManifest manifest = CloneManifest(source.Manifest);
			manifest.id = SongPackageStorage.GenerateShortId();
			manifest.scoreTitle = string.IsNullOrWhiteSpace(manifest.scoreTitle) ? "副本" : manifest.scoreTitle + " 副本";
			manifest.Normalize();

			string destination = GetUniqueDirectory(Path.Combine(RootDirectory, SongPackageStorage.CreateFolderName(manifest)));
			CopyDirectory(source.RootDirectory, destination);
			WriteManifest(destination, manifest);
			return SongPackageStorage.LoadEntry(destination);
		}

		public void DeleteEntry(SongPackage entry)
		{
			if (entry == null || string.IsNullOrEmpty(entry.RootDirectory) || !Directory.Exists(entry.RootDirectory))
			{
				return;
			}

			string root = Path.GetFullPath(RootDirectory);
			string target = Path.GetFullPath(entry.RootDirectory);
			if (!IsPathInsideDirectory(root, target))
			{
				throw new InvalidOperationException("拒绝删除 CustomMusicScores 外部的文件夹。");
			}

			Directory.Delete(target, true);
		}

		public SongPackage ImportFolder(string sourceDirectory)
		{
			if (string.IsNullOrEmpty(sourceDirectory) || !Directory.Exists(sourceDirectory))
			{
				return null;
			}

			SongPackage source = SongPackageStorage.LoadEntry(sourceDirectory);
			if (source == null)
			{
				return null;
			}

			SongPackageManifest manifest = CloneManifest(source.Manifest);
			manifest.Normalize();
			string destination = GetUniqueDirectory(Path.Combine(RootDirectory, SongPackageStorage.CreateFolderName(manifest)));
			CopyDirectory(source.RootDirectory, destination);
			WriteManifest(destination, manifest);
			return SongPackageStorage.LoadEntry(destination);
		}

		public SongPackage ImportZip(string zipPath)
		{
			if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
			{
				return null;
			}

			string tempRoot = CreateTemporaryPath("OpenSekaiCustomScore_");
			Directory.CreateDirectory(tempRoot);
			try
			{
				ZipFile.ExtractToDirectory(zipPath, tempRoot);
				string entryRoot = FindEntryRoot(tempRoot);
				return ImportFolder(entryRoot);
			}
			finally
			{
				if (Directory.Exists(tempRoot))
				{
					Directory.Delete(tempRoot, true);
				}
			}
		}

		public string ExportZip(SongPackage entry, string destinationPath = null)
		{
			if (entry == null || !Directory.Exists(entry.RootDirectory))
			{
				return null;
			}

			if (string.IsNullOrEmpty(destinationPath))
			{
				string exportDirectory = Path.Combine(RootDirectory, EXPORT_DIRECTORY_NAME);
				Directory.CreateDirectory(exportDirectory);
				string safeTitle = SanitizeFileName(entry.Manifest.scoreTitle);
				if (string.IsNullOrEmpty(safeTitle))
				{
					safeTitle = "未命名";
				}
				destinationPath = Path.Combine(exportDirectory, safeTitle + "_" + entry.Manifest.id + ".zip");
			}

			string directory = Path.GetDirectoryName(destinationPath);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}
			if (File.Exists(destinationPath))
			{
				File.Delete(destinationPath);
			}

			ZipFile.CreateFromDirectory(entry.RootDirectory, destinationPath, System.IO.Compression.CompressionLevel.Optimal, false);
			return destinationPath;
		}

		public SongPackage SaveManifest(SongPackage entry, SongPackageManifest manifest)
		{
			if (entry == null || manifest == null)
			{
				return entry;
			}

			manifest.Normalize();
			string directory = ResolveManifestSaveDirectory(entry, manifest);
			WriteManifest(directory, manifest);
			return SongPackageStorage.LoadEntry(directory);
		}

		public SongPackage ReplaceAudioFile(SongPackage entry, string sourcePath)
		{
			return ReplaceEntryFile(
				entry,
				sourcePath,
				"audio",
				AudioExtensions,
				manifest => manifest.audioFileName,
				(manifest, fileName) => manifest.audioFileName = fileName);
		}

		public SongPackage ReplaceJacketFile(SongPackage entry, string sourcePath)
		{
			return ReplaceEntryFile(
				entry,
				sourcePath,
				"jacket",
				JacketExtensions,
				manifest => manifest.jacketFileName,
				(manifest, fileName) => manifest.jacketFileName = fileName,
				ResizeAndWriteJacketFile);
		}

		public SongPackage ReplaceScoreFile(SongPackage entry, string sourcePath)
		{
			if (entry == null || string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
			{
				return entry;
			}

			string extension = Path.GetExtension(sourcePath);
			if (string.IsNullOrEmpty(extension) || !ScoreExtensions.Contains(extension))
			{
				throw new InvalidOperationException("不支持的谱面文件扩展名：" + extension);
			}

			Directory.CreateDirectory(entry.RootDirectory);
			SongPackageManifest manifest = CloneManifest(entry.Manifest);
			string destinationPath = Path.Combine(entry.RootDirectory, "score.json");
			string tempCopyPath = null;
			string readSourcePath = sourcePath;
			if (IsPathInsideDirectory(entry.RootDirectory, sourcePath))
			{
				tempCopyPath = CreateTemporaryPath("OpenSekaiCustomScoreFile_", extension);
				File.Copy(sourcePath, tempCopyPath, true);
				readSourcePath = tempCopyPath;
			}

			try
			{
				// Validate/convert before removing the existing score. Keep JSON bytes unchanged.
				var data = ChartFileReader.ReadFile(readSourcePath, entry.MusicId);
				if (data == null) throw new InvalidDataException("无法读取谱面文件。");
				string convertedJson = extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ? null : ChartJson.Write(data);
				DeleteExistingScoreFiles(entry.RootDirectory, manifest.scoreFileName);
				if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
				{
					File.Copy(readSourcePath, destinationPath, true);
				}
				else
				{
					File.WriteAllText(destinationPath, convertedJson);
				}

				manifest.scoreFileName = "score.json";
				manifest.Normalize();
				WriteManifest(entry.RootDirectory, manifest);
				return SongPackageStorage.LoadEntry(entry.RootDirectory);
			}
			finally
			{
				if (!string.IsNullOrEmpty(tempCopyPath) && File.Exists(tempCopyPath))
				{
					File.Delete(tempCopyPath);
				}
			}
		}

		public SongPackage ReplaceVideoFile(SongPackage entry, string sourcePath)
		{
			return ReplaceEntryFile(
				entry,
				sourcePath,
				"video",
				VideoExtensions,
				manifest => manifest.videoFileName,
				(manifest, fileName) => manifest.videoFileName = fileName);
		}

		private SongPackageItem CreateItem(SongPackage entry)
		{
			string manifestPath = entry.ManifestPath;
			string scorePath = entry.ScorePath;
			string audioPath = entry.AudioPath;
			string jacketPath = entry.JacketPath;
			DateTime lastWriteTime = Directory.GetLastWriteTime(entry.RootDirectory);
			if (File.Exists(manifestPath))
			{
				lastWriteTime = Max(lastWriteTime, File.GetLastWriteTime(manifestPath));
			}
			if (File.Exists(scorePath))
			{
				lastWriteTime = Max(lastWriteTime, File.GetLastWriteTime(scorePath));
			}
			if (File.Exists(entry.VideoPath))
			{
				lastWriteTime = Max(lastWriteTime, File.GetLastWriteTime(entry.VideoPath));
			}

			return new SongPackageItem(
				entry,
				lastWriteTime,
				File.Exists(manifestPath),
				File.Exists(scorePath),
				File.Exists(audioPath),
				File.Exists(jacketPath));
		}

		private void WriteDefaultScore(string directory, SongPackageManifest manifest)
		{
			ChartDocument data = new ChartDocument();
			data.MusicScoreEventDataList.Add(new ChartEvent { id = 1, eventType = ChartEventType.BPM, ticks = 0L, changeValue = 120f });
			data.MusicScoreEventDataList.Add(new ChartEvent { id = 2, eventType = ChartEventType.TimeSignature, ticks = 0L, changeValue = "4/4" });
			data.MusicScoreEventDataList.Add(new ChartEvent { id = 3, eventType = ChartEventType.HighSpeed, ticks = 0L, changeValue = 1f });
			data.MusicScoreEventDataList.Add(new ChartEvent { id = 4, eventType = ChartEventType.SeVolume, ticks = 0L, changeValue = 1f });
			string path = Path.Combine(directory, manifest.scoreFileName);
			File.WriteAllText(path, ChartJson.Write(data));
		}

		private void WriteManifest(string directory, SongPackageManifest manifest)
		{
			Directory.CreateDirectory(directory);
			string json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
			File.WriteAllText(Path.Combine(directory, SongPackageStorage.MANIFEST_FILE_NAME), json);
		}

		private string ResolveManifestSaveDirectory(SongPackage entry, SongPackageManifest manifest)
		{
			string currentDirectory = Path.GetFullPath(entry.RootDirectory);
			if (!Directory.Exists(currentDirectory))
			{
				return currentDirectory;
			}

			string rootDirectory = Path.GetFullPath(RootDirectory);
			string normalizedRoot = rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
			if (!currentDirectory.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
			{
				return currentDirectory;
			}

			string desiredDirectory = Path.GetFullPath(Path.Combine(rootDirectory, SongPackageStorage.CreateFolderName(manifest)));
			if (PathsEqual(currentDirectory, desiredDirectory))
			{
				return currentDirectory;
			}

			string destination = Directory.Exists(desiredDirectory) ? GetUniqueDirectory(desiredDirectory) : desiredDirectory;
			Directory.Move(currentDirectory, destination);
			return destination;
		}

		private SongPackage ReplaceEntryFile(
			SongPackage entry,
			string sourcePath,
			string fixedBaseName,
			HashSet<string> allowedExtensions,
			Func<SongPackageManifest, string> getCurrentFileName,
			Action<SongPackageManifest, string> setFileName,
			Action<string, string, string> writeFile = null)
		{
			if (entry == null || string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
			{
				return entry;
			}

			string extension = ResolveSupportedExtension(sourcePath, allowedExtensions);
			if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
			{
				throw new InvalidOperationException("不支持的文件扩展名：" + extension);
			}

			Directory.CreateDirectory(entry.RootDirectory);
			SongPackageManifest manifest = CloneManifest(entry.Manifest);
			string fileName = fixedBaseName + extension.ToLowerInvariant();
			string destinationPath = Path.Combine(entry.RootDirectory, fileName);
			string copySourcePath = sourcePath;
			string tempCopyPath = null;
			if (IsPathInsideDirectory(entry.RootDirectory, sourcePath))
			{
				tempCopyPath = CreateTemporaryPath("OpenSekaiCustomScoreFile_", extension);
				File.Copy(sourcePath, tempCopyPath, true);
				copySourcePath = tempCopyPath;
			}

			try
			{
				DeleteExistingEntrySlotFiles(entry.RootDirectory, fixedBaseName, allowedExtensions, getCurrentFileName?.Invoke(manifest));
				if (writeFile != null)
				{
					writeFile(copySourcePath, destinationPath, extension);
				}
				else
				{
					File.Copy(copySourcePath, destinationPath, true);
				}
				setFileName(manifest, Path.GetFileName(destinationPath));
				manifest.Normalize();
				WriteManifest(entry.RootDirectory, manifest);
				return SongPackageStorage.LoadEntry(entry.RootDirectory);
			}
			finally
			{
				if (!string.IsNullOrEmpty(tempCopyPath) && File.Exists(tempCopyPath))
				{
					File.Delete(tempCopyPath);
				}
			}
		}

		private void ResizeAndWriteJacketFile(string sourcePath, string destinationPath, string extension)
		{
			byte[] sourceBytes = File.ReadAllBytes(sourcePath);
			Texture2D sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			if (!sourceTexture.LoadImage(sourceBytes))
			{
				UnityEngine.Object.Destroy(sourceTexture);
				throw new InvalidOperationException("不支持的封面图片数据。");
			}

			RenderTexture previous = RenderTexture.active;
			RenderTexture renderTexture = RenderTexture.GetTemporary(JACKET_TEXTURE_SIZE, JACKET_TEXTURE_SIZE, 0, RenderTextureFormat.ARGB32);
			Texture2D resizedTexture = new Texture2D(JACKET_TEXTURE_SIZE, JACKET_TEXTURE_SIZE, TextureFormat.RGBA32, false);
			try
			{
				Graphics.Blit(sourceTexture, renderTexture);
				RenderTexture.active = renderTexture;
				resizedTexture.ReadPixels(new Rect(0f, 0f, JACKET_TEXTURE_SIZE, JACKET_TEXTURE_SIZE), 0, 0);
				resizedTexture.Apply(false, false);

				byte[] resizedBytes = extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
					? resizedTexture.EncodeToPNG()
					: resizedTexture.EncodeToJPG(95);
				File.WriteAllBytes(destinationPath, resizedBytes);
			}
			finally
			{
				RenderTexture.active = previous;
				RenderTexture.ReleaseTemporary(renderTexture);
				UnityEngine.Object.Destroy(sourceTexture);
				UnityEngine.Object.Destroy(resizedTexture);
			}
		}

		private string ResolveSupportedExtension(string path, HashSet<string> allowedExtensions)
		{
			string extension = Path.GetExtension(path);
			if (!string.IsNullOrEmpty(extension) && allowedExtensions.Contains(extension))
			{
				return extension;
			}

			string detectedExtension = DetectExtensionFromHeader(path);
			return !string.IsNullOrEmpty(detectedExtension) ? detectedExtension : extension;
		}

		private string DetectExtensionFromHeader(string path)
		{
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				return string.Empty;
			}

			byte[] header = new byte[12];
			int readCount;
			using (FileStream stream = File.OpenRead(path))
			{
				readCount = stream.Read(header, 0, header.Length);
			}

			if (readCount >= 8
				&& header[0] == 0x89
				&& header[1] == 0x50
				&& header[2] == 0x4E
				&& header[3] == 0x47
				&& header[4] == 0x0D
				&& header[5] == 0x0A
				&& header[6] == 0x1A
				&& header[7] == 0x0A)
			{
				return ".png";
			}

			if (readCount >= 2 && header[0] == 0xFF && header[1] == 0xD8)
			{
				return ".jpg";
			}

			if (readCount >= 4 && header[0] == 0x4F && header[1] == 0x67 && header[2] == 0x67 && header[3] == 0x53)
			{
				return ".ogg";
			}

			if (readCount >= 12
				&& header[0] == 0x52
				&& header[1] == 0x49
				&& header[2] == 0x46
				&& header[3] == 0x46
				&& header[8] == 0x57
				&& header[9] == 0x41
				&& header[10] == 0x56
				&& header[11] == 0x45)
			{
				return ".wav";
			}

			if (readCount >= 3 && header[0] == 0x49 && header[1] == 0x44 && header[2] == 0x33)
			{
				return ".mp3";
			}

			if (readCount >= 2 && header[0] == 0xFF && (header[1] & 0xE0) == 0xE0)
			{
				return ".mp3";
			}

			return string.Empty;
		}

		private void DeleteExistingEntrySlotFiles(
			string rootDirectory,
			string fixedBaseName,
			HashSet<string> allowedExtensions,
			string currentFileName)
		{
			HashSet<string> deletePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (!string.IsNullOrWhiteSpace(currentFileName))
			{
				string fileName = Path.GetFileName(currentFileName);
				if (!string.IsNullOrEmpty(fileName))
				{
					string extension = Path.GetExtension(fileName);
					if (allowedExtensions.Contains(extension))
					{
						deletePaths.Add(Path.Combine(rootDirectory, fileName));
					}
				}
			}

			foreach (string extension in allowedExtensions)
			{
				deletePaths.Add(Path.Combine(rootDirectory, fixedBaseName + extension.ToLowerInvariant()));
			}

			foreach (string path in Directory.GetFiles(rootDirectory))
			{
				if (allowedExtensions.Contains(Path.GetExtension(path)))
				{
					deletePaths.Add(path);
				}
			}

			foreach (string path in deletePaths)
			{
				if (File.Exists(path))
				{
					File.Delete(path);
				}
			}
		}

		private void DeleteExistingScoreFiles(string rootDirectory, string currentFileName)
		{
			HashSet<string> deletePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{
				Path.Combine(rootDirectory, "score.json"),
				Path.Combine(rootDirectory, "score.txt"),
				Path.Combine(rootDirectory, "score.sus")
			};

			if (!string.IsNullOrWhiteSpace(currentFileName))
			{
				string fileName = Path.GetFileName(currentFileName);
				if (!string.IsNullOrEmpty(fileName) && !string.Equals(fileName, SongPackageStorage.MANIFEST_FILE_NAME, StringComparison.OrdinalIgnoreCase))
				{
					string extension = Path.GetExtension(fileName);
					if (ScoreExtensions.Contains(extension))
					{
						deletePaths.Add(Path.Combine(rootDirectory, fileName));
					}
				}
			}

			foreach (string path in Directory.GetFiles(rootDirectory, "score.*"))
			{
				if (ScoreExtensions.Contains(Path.GetExtension(path)))
				{
					deletePaths.Add(path);
				}
			}

			foreach (string path in deletePaths)
			{
				if (File.Exists(path))
				{
					File.Delete(path);
				}
			}
		}

		private SongPackageManifest CloneManifest(SongPackageManifest source)
		{
			if (source == null)
			{
				return new SongPackageManifest();
			}

			return JsonConvert.DeserializeObject<SongPackageManifest>(
				JsonConvert.SerializeObject(source)) ?? new SongPackageManifest();
		}

		private string FindEntryRoot(string root)
		{
			if (File.Exists(Path.Combine(root, SongPackageStorage.MANIFEST_FILE_NAME)))
			{
				return root;
			}

			foreach (string directory in Directory.GetDirectories(root))
			{
				string result = FindEntryRoot(directory);
				if (!string.IsNullOrEmpty(result))
				{
					return result;
				}
			}
			return null;
		}

		private void CopyDirectory(string source, string destination)
		{
			Directory.CreateDirectory(destination);
			foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
			{
				Directory.CreateDirectory(Path.Combine(destination, GetRelativePath(source, directory)));
			}
			foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
			{
				File.Copy(file, Path.Combine(destination, GetRelativePath(source, file)), true);
			}
		}

		private string GetRelativePath(string root, string path)
		{
			string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			string normalizedPath = Path.GetFullPath(path);
			if (normalizedPath.Length <= normalizedRoot.Length)
			{
				return string.Empty;
			}

			return normalizedPath.Substring(normalizedRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		}

		private string GetUniqueDirectory(string directory)
		{
			if (!Directory.Exists(directory))
			{
				return directory;
			}

			for (int i = 2; i < 1000; i++)
			{
				string candidate = directory + "_" + i;
				if (!Directory.Exists(candidate))
				{
					return candidate;
				}
			}

			return directory + "_" + Guid.NewGuid().ToString("N");
		}

		private bool PathsEqual(string left, string right)
		{
			string normalizedLeft = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			string normalizedRight = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
		}

		private bool IsPathInsideDirectory(string directory, string path)
		{
			string normalizedDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
			string normalizedPath = Path.GetFullPath(path);
			return normalizedPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
		}

		private string CreateTemporaryPath(string prefix, string extension = null)
		{
			string tempDirectory = _temporaryDirectory;
			Directory.CreateDirectory(tempDirectory);
			return Path.Combine(tempDirectory, prefix + Guid.NewGuid().ToString("N") + (extension ?? string.Empty));
		}

		private string SanitizeFileName(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return string.Empty;
			}

			HashSet<char> invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());
			char[] chars = value.Trim().Select(c => invalidChars.Contains(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray();
			return new string(chars).Trim('_');
		}

		private DateTime Max(DateTime left, DateTime right)
		{
			return left >= right ? left : right;
		}
	}
}
