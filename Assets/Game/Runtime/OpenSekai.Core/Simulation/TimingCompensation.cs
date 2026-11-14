using System;

namespace OpenSekai.Shared.Live.Simulation
{
	/// <summary>
	/// The three timing compensations a player can set, in milliseconds, and the two clocks they produce.
	///
	/// <list type="bullet">
	/// <item><b>Audio latency</b> L: how long the music takes to reach the ear. The whole game is moved
	/// back by it, so notes and judgement both line up with what is heard.</item>
	/// <item><b>Visual latency</b> V: how long a drawn frame takes to reach the eye. Only the picture is
	/// moved: notes are drawn V ahead, judgement is untouched.</item>
	/// <item><b>Judge offset</b> J: a player's own habit. Positive means "I hit late": only judgement is
	/// moved, a press J ms after the beat counts as on it, and the picture is untouched.</item>
	/// </list>
	///
	/// Every judgement site - presses, lifts, miss windows, interior points - runs on one logic clock, so
	/// moving judgement by J is done by moving that clock, and the picture is put back by drawing J ahead
	/// as well. That keeps one clock for all of judgement instead of an offset at each site:
	/// <c>logic = audio - (L + J)</c>, <c>view = logic + (V + J) = audio - L + V</c>.
	/// The hit sound is not compensated: it can only start once the press is known.
	/// </summary>
	public readonly struct TimingCompensation
	{
		public const float MinAudioLatencyMs = -200f;
		public const float MaxAudioLatencyMs = 500f;
		public const float MinVisualLatencyMs = -200f;
		public const float MaxVisualLatencyMs = 300f;
		public const float MinJudgeOffsetMs = -200f;
		public const float MaxJudgeOffsetMs = 200f;

		/// <summary>The old setting moved the clock forward by its value in 1/60 s frames.</summary>
		public const float LegacyFrameMs = 1000f / 60f;

		public TimingCompensation(float audioLatencyMs, float visualLatencyMs, float judgeOffsetMs)
		{
			AudioLatencyMs = ClampAudioLatency(audioLatencyMs);
			VisualLatencyMs = ClampVisualLatency(visualLatencyMs);
			JudgeOffsetMs = ClampJudgeOffset(judgeOffsetMs);
		}

		public float AudioLatencyMs { get; }
		public float VisualLatencyMs { get; }
		public float JudgeOffsetMs { get; }

		/// <summary>Seconds the logic clock runs behind the audio clock.</summary>
		public float LogicDelaySeconds => (AudioLatencyMs + JudgeOffsetMs) / 1000f;

		/// <summary>Seconds the picture is drawn ahead of the logic clock.</summary>
		public float ViewLeadSeconds => (VisualLatencyMs + JudgeOffsetMs) / 1000f;

		/// <summary>The clock notes are executed and judged on.</summary>
		public float LogicTime(float audioTime) => Math.Max(audioTime - LogicDelaySeconds, 0f);

		/// <summary>The clock the views draw with.</summary>
		public float ViewTime(float audioTime) => Math.Max(audioTime - (AudioLatencyMs - VisualLatencyMs) / 1000f, 0f);

		public static float ClampAudioLatency(float ms) => Clamp(ms, MinAudioLatencyMs, MaxAudioLatencyMs);
		public static float ClampVisualLatency(float ms) => Clamp(ms, MinVisualLatencyMs, MaxVisualLatencyMs);
		public static float ClampJudgeOffset(float ms) => Clamp(ms, MinJudgeOffsetMs, MaxJudgeOffsetMs);

		/// <summary>
		/// The old "判定延迟" moved the whole clock forward by its value in frames, which is a negative
		/// audio latency: <c>audio + frames/60 = audio - L</c>.
		/// </summary>
		public static float AudioLatencyFromLegacyFrames(float frames) => ClampAudioLatency(-frames * LegacyFrameMs);

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
