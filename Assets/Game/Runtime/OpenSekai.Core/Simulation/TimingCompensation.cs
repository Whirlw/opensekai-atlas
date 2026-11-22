using System;

namespace OpenSekai.Shared.Live.Simulation
{
	/// <summary>Independent offsets from the live timeline. Positive audio and visual latency
	/// advance playback and drawing. Positive judge offset accepts a later input.</summary>
	public readonly struct TimingCompensation
	{
		public const float MIN_AUDIO_LATENCY_MS = -200f;
		public const float MAX_AUDIO_LATENCY_MS = 500f;
		public const float MIN_VISUAL_LATENCY_MS = -200f;
		public const float MAX_VISUAL_LATENCY_MS = 300f;
		public const float MIN_JUDGE_OFFSET_MS = -200f;
		public const float MAX_JUDGE_OFFSET_MS = 200f;

		/// <summary>The old setting moved the clock forward by its value in 1/60 s frames.</summary>
		public const float LEGACY_FRAME_MS = 1000f / 60f;

		public TimingCompensation(float audioLatencyMs, float visualLatencyMs, float judgeOffsetMs)
		{
			AudioLatencyMs = ClampAudioLatency(audioLatencyMs);
			VisualLatencyMs = ClampVisualLatency(visualLatencyMs);
			JudgeOffsetMs = ClampJudgeOffset(judgeOffsetMs);
		}

		public float AudioLatencyMs { get; }
		public long AudioAdvanceMs => (long)Math.Round(AudioLatencyMs);
		public long PlaybackAt(long timelineMs) => timelineMs + AudioAdvanceMs;
		public long TimelineAtPlayback(long playbackMs) => playbackMs - AudioAdvanceMs;
		public float VisualLatencyMs { get; }
		public float JudgeOffsetMs { get; }

		/// <summary>Seconds the logic clock runs behind the timeline.</summary>
		public float LogicDelaySeconds => JudgeOffsetMs / 1000f;

		/// <summary>Seconds the picture is drawn ahead of the logic clock.</summary>
		public float ViewLeadSeconds => (VisualLatencyMs + JudgeOffsetMs) / 1000f;

		/// <summary>The clock notes are executed and judged on.</summary>
		public float LogicTime(float audioTime) => Math.Max(audioTime - LogicDelaySeconds, 0f);

		/// <summary>The clock the views draw with.</summary>
		public float ViewTime(float audioTime) => Math.Max(audioTime + VisualLatencyMs / 1000f, 0f);

		public static float ClampAudioLatency(float ms) => Clamp(ms, MIN_AUDIO_LATENCY_MS, MAX_AUDIO_LATENCY_MS);
		public static float ClampVisualLatency(float ms) => Clamp(ms, MIN_VISUAL_LATENCY_MS, MAX_VISUAL_LATENCY_MS);
		public static float ClampJudgeOffset(float ms) => Clamp(ms, MIN_JUDGE_OFFSET_MS, MAX_JUDGE_OFFSET_MS);

		/// <summary>
		/// The old "判定延迟" moved the whole clock forward by its value in frames, which is a negative
		/// audio latency: <c>audio + frames/60 = audio - L</c>.
		/// </summary>
		public static float AudioLatencyFromLegacyFrames(float frames) => ClampAudioLatency(-frames * LEGACY_FRAME_MS);

		/// <summary>
		/// Where a key is drawn, from its logic progress and the lead. A key still travelling to the line
		/// is moved by the lead; a key the logic has put on or past the line (pinned, held, passing) is
		/// drawn where the logic says. A key drawn ahead therefore reaches the line and waits there until
		/// the logic catches up, instead of overshooting a pin.
		/// </summary>
		public static float ViewProgress(float progress, float offsetTime, float leadSeconds)
		{
			if (leadSeconds == 0f || offsetTime <= 0f || progress >= 1f)
			{
				return progress;
			}

			return Math.Min(progress + leadSeconds / offsetTime, 1f);
		}

		private static float Clamp(float value, float min, float max)
		{
			if (float.IsNaN(value))
			{
				return 0f;
			}

			return value < min ? min : value > max ? max : value;
		}
	}
}
