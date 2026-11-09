using System;
using System.IO;
using System.Text;
using UnityEngine;
using CP;

namespace Sekai
{
	public static class UnityLogFileWriter
	{
		private const string LOG_DIRECTORY_NAME = "OpenSekaiLogs";
		private static readonly object SyncRoot = new object();
		private static StreamWriter _writer;
		private static bool _initialized;

		public static string CurrentLogFilePath { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Initialize()
		{
			if (_initialized)
			{
				return;
			}

			_initialized = true;
			try
			{
				string directory = Path.Combine(Application.persistentDataPath, LOG_DIRECTORY_NAME);
				Directory.CreateDirectory(directory);
				CurrentLogFilePath = Path.Combine(directory, $"unity_{DateTime.Now:yyyyMMdd_HHmmss}.log");
				_writer = new StreamWriter(CurrentLogFilePath, append: false, Encoding.UTF8)
				{
					AutoFlush = true
				};
				_writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Info] Unity log file started.");
				_writer.WriteLine($"LogPath: {CurrentLogFilePath}");
				_writer.WriteLine($"Unity: {Application.unityVersion}");
				_writer.WriteLine($"Platform: {Application.platform}");
				_writer.WriteLine($"Device: {SystemInfo.deviceModel} / {SystemInfo.operatingSystem}");
				_writer.WriteLine();

				Application.logMessageReceivedThreaded += OnLogMessageReceived;
				Application.quitting += Close;
				Debug.Log($"Unity log file: {CurrentLogFilePath}");
			}
			catch
			{
				Close();
			}
		}

		private static void OnLogMessageReceived(string condition, string stackTrace, UnityEngine.LogType type)
		{
			lock (SyncRoot)
			{
				if (_writer == null)
				{
					return;
				}

				_writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{type}] {condition}");
				if (!string.IsNullOrEmpty(stackTrace) && type != UnityEngine.LogType.Log)
				{
					_writer.WriteLine(stackTrace);
				}
			}
		}

		private static void Close()
		{
			lock (SyncRoot)
			{
				if (_writer == null)
				{
					return;
				}

				_writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Info] Unity log file closed.");
				_writer.Dispose();
				_writer = null;
			}
		}
	}
}
