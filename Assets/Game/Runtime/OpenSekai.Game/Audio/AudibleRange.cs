using System;
using UnityEngine;

namespace Sekai
{
	/// <summary>
	/// Finds where a clip's sound actually starts and stops, so the silence a song file carries at either
	/// end does not count as music. Scans in blocks from each end and stops at the first audible sample,
	/// so a clip with little padding costs only a block or two.
	/// </summary>
	internal static class AudibleRange
	{
		/// <summary>About -60 dBFS: well under anything a listener would call sound, well over dither.</summary>
		internal const float SILENCE_THRESHOLD = 0.001f;

		private const int BLOCK_FRAMES = 16384;

		internal static bool TryFind(AudioClip clip, out float startSeconds, out float endSeconds)
		{
			startSeconds = 0f;
			endSeconds = 0f;
			if (clip == null || clip.samples <= 0 || clip.channels <= 0 || clip.frequency <= 0)
			{
				return false;
			}

			try
			{
				if (clip.loadState != AudioDataLoadState.Loaded && !clip.LoadAudioData())
				{
					return false;
				}

				int frames = clip.samples;
				int channels = clip.channels;
				float[] block = new float[BLOCK_FRAMES * channels];

				int first = -1;
				for (int offset = 0; offset < frames && first < 0; offset += BLOCK_FRAMES)
				{
					float[] buffer = Read(clip, offset, Math.Min(BLOCK_FRAMES, frames - offset), block);
					if (buffer == null)
					{
						return false;
					}

					for (int i = 0; i < buffer.Length; i++)
					{
						if (Math.Abs(buffer[i]) > SILENCE_THRESHOLD)
						{
							first = offset + i / channels;
							break;
						}
					}
				}

				if (first < 0)
				{
					return false;
				}

				int last = first;
				for (int offset = (frames - 1) / BLOCK_FRAMES * BLOCK_FRAMES; offset >= first; offset -= BLOCK_FRAMES)
				{
					float[] buffer = Read(clip, offset, Math.Min(BLOCK_FRAMES, frames - offset), block);
					if (buffer == null)
					{
						return false;
					}

					int found = -1;
					for (int i = buffer.Length - 1; i >= 0; i--)
					{
						if (Math.Abs(buffer[i]) > SILENCE_THRESHOLD)
						{
							found = offset + i / channels;
							break;
						}
					}

					if (found >= 0)
					{
						last = found;
						break;
					}
				}

				startSeconds = (float)first / clip.frequency;
				endSeconds = (float)(last + 1) / clip.frequency;
				return true;
			}
			catch (Exception exception)
			{
				// A clip kept compressed in memory cannot hand out samples: run over the whole clip.
				Debug.LogWarning("AudibleRange: cannot read samples of " + clip.name + ": " + exception.Message);
				return false;
			}
		}

		// GetData fills the whole array and wraps past the end of the clip, so the last, shorter block
		// gets an array of its own size.
		private static float[] Read(AudioClip clip, int offset, int frameCount, float[] block)
		{
			float[] buffer = frameCount == BLOCK_FRAMES ? block : new float[frameCount * clip.channels];
			return clip.GetData(buffer, offset) ? buffer : null;
		}
	}
}
