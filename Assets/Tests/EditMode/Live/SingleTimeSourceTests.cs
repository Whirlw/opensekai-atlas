using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// Two bugs in a row came from reading a value that was not the clock: a cached
	/// <c>OffsetJudgeTime</c> that had never been refreshed, and <c>Time.frameCount</c> which counts
	/// engine frames instead of seconds. This test is a ratchet: it forbids the patterns from coming
	/// back, so the next person cannot reintroduce them by accident.
	/// </summary>
	[TestFixture]
	public class SingleTimeSourceTests
	{
		private static readonly string[] OffsetJudgeTimeReaders =
		{
			// The declaration, the refresh in Excute, the pool reset, and the pair note helper that
			// keeps the cache for the view. Nothing on the judging path may read it.
			"NoteBase.cs",
			"LiveUtility.cs",
		};

		private static readonly string[] FrameCountUsers =
		{
			// ReferenceFrame documents the constant; the two views only use the frame number to dedupe
			// feedback inside a single displayed frame, which is a rendering concern.
			"ReferenceFrame.cs",
			"JudgmentView.cs",
			"TapEffectView.cs",
		};

		private static IEnumerable<FileInfo> RuntimeSources()
		{
			string root = Path.Combine(Application_dataPath(), "Game", "Runtime");
			foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
			{
				yield return new FileInfo(path);
			}
		}

		private static string Application_dataPath()
		{
			return UnityEngine.Application.dataPath;
		}

		[Test]
		public void OnlyTheOwnerRefreshesTheCachedJudgeOffset()
		{
			var offenders = new List<string>();
			foreach (FileInfo file in RuntimeSources())
			{
				if (Array.IndexOf(OffsetJudgeTimeReaders, file.Name) >= 0)
				{
					continue;
				}

				string text = File.ReadAllText(file.FullName);
				if (text.Contains(".OffsetJudgeTime"))
				{
					offenders.Add(file.Name);
				}
			}

			Assert.That(offenders, Is.Empty,
				"Judge against the clock (NoteBase.JudgeOffsetAt / MusicScoreInfo.time), not the cached OffsetJudgeTime: " +
				string.Join(", ", offenders));
		}

		[Test]
		public void JudgingNeverCountsEngineFrames()
		{
			var offenders = new List<string>();
			foreach (FileInfo file in RuntimeSources())
			{
				if (Array.IndexOf(FrameCountUsers, file.Name) >= 0)
				{
					continue;
				}

				string text = File.ReadAllText(file.FullName);
				if (text.Contains("Time.frameCount"))
				{
					offenders.Add(file.Name);
				}
			}

			Assert.That(offenders, Is.Empty,
				"Engine frames are not a time base (1 frame is 6 ms at 165 Hz and 17 ms at 60). Use ReferenceFrame: " +
				string.Join(", ", offenders));
		}
	}
}
