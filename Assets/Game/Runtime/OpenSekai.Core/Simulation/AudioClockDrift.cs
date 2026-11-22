using System;

namespace OpenSekai.Shared.Live.Simulation
{
	/// <summary>What the playback clock should do after one window of audio position readings.</summary>
	public enum AudioClockCorrectionKind
	{
		/// <summary>The error is inside the dead band: run at normal rate.</summary>
		None = 0,

		/// <summary>A small, steady error: run slightly fast or slow until it is gone.</summary>
		Slew = 1,

		/// <summary>The clock is far behind the audio: jump it forward onto the audio.</summary>
		JumpForward = 2,

		/// <summary>
		/// The clock is far ahead of the audio (the audio stalled): stand still until the audio catches
		/// up. The clock is never moved back, since everything that reads it assumes it only advances.
		/// </summary>
		Wait = 3
	}

	/// <summary>One decision of <see cref="AudioClockDrift"/>.</summary>
	public readonly struct AudioClockCorrection
	{
		public AudioClockCorrection(AudioClockCorrectionKind kind, float errorSeconds, float spreadSeconds, float rateScale)
		{
			Kind = kind;
			ErrorSeconds = errorSeconds;
			SpreadSeconds = spreadSeconds;
			RateScale = rateScale;
		}

		public AudioClockCorrectionKind Kind { get; }

		/// <summary>The estimated clock minus audio, in seconds. Positive: the clock is ahead.</summary>
		public float ErrorSeconds { get; }

		/// <summary>How scattered the window's readings were (interquartile range), in seconds.</summary>
		public float SpreadSeconds { get; }

		/// <summary>The rate the clock runs at from now on, 1 being real time.</summary>
		public float RateScale { get; }

		/// <summary>How far to move the clock forward now. Only for <see cref="AudioClockCorrectionKind.JumpForward"/>.</summary>
		public float JumpSeconds => Kind == AudioClockCorrectionKind.JumpForward ? -ErrorSeconds : 0f;

		/// <summary>How long to hold the clock still, in audio seconds. Only for <see cref="AudioClockCorrectionKind.Wait"/>.</summary>
		public float WaitSeconds => Kind == AudioClockCorrectionKind.Wait ? ErrorSeconds : 0f;
	}

	/// <summary>
	/// Keeps a monotonic playback clock on its audio without making it jitter.
	///
	/// The clock runs on a stopwatch, because the audio position on some devices is reported in chunks
	/// tens of milliseconds long. Each time the reported position moves, the clock minus that position is
	/// fed in here; a window of those readings makes one decision:
	///
	/// <list type="bullet">
	/// <item>The estimate is the window's <b>lower quartile</b>, not its median. A reported position is
	/// only ever stale (it lags the real one), which makes the clock look ahead, never behind, so the
	/// error is one-sided and the freshest readings are the low ones. The quartile still ignores a
	/// stray low reading.</item>
	/// <item>Inside a <b>dead band</b> nothing is done. The band widens with the window's scatter, so a
	/// device with chunky reports is not chased around by its own noise, and narrows to
	/// <see cref="MinDeadBandSeconds"/> on a clean one.</item>
	/// <item>Up to <see cref="HardResyncSeconds"/> the clock is <b>slewed</b>: run up to
	/// <see cref="MaxSlewRate"/> fast or slow, aiming to close the gap over
	/// <see cref="SlewTimeConstantSeconds"/>. Nothing jumps, so neither notes nor judgement see a step.</item>
	/// <item>Past it, a clock behind the audio <b>jumps forward</b>; a clock ahead of it (the audio stalled)
	/// <b>waits</b> for the audio. It never steps back.</item>
	/// </list>
	/// </summary>
	public sealed class AudioClockDrift
	{
		/// <summary>Readings per decision.</summary>
		public const int WINDOW_SIZE = 9;

		/// <summary>The narrowest dead band: errors under this are treated as noise however clean the readings.</summary>
		public const float MIN_DEAD_BAND_SECONDS = 0.015f;

		/// <summary>Errors this size or larger are corrected at once rather than slewed.</summary>
		public const float HARD_RESYNC_SECONDS = 0.06f;

		/// <summary>The most the clock is sped up or slowed down by while slewing (1%).</summary>
		public const float MAX_SLEW_RATE = 0.01f;

		/// <summary>The time a slew aims to close its error in, before <see cref="MaxSlewRate"/> caps it.</summary>
		public const float SLEW_TIME_CONSTANT_SECONDS = 2f;

		private readonly float[] _window = new float[WINDOW_SIZE];
		private readonly float[] _sorted = new float[WINDOW_SIZE];
		private int _count;

		/// <summary>Readings collected toward the next decision.</summary>
		public int Count => _count;

		/// <summary>Drops the readings so far. Call whenever the clock or the audio is moved.</summary>
		public void Reset() => _count = 0;

		/// <summary>
		/// Adds one clock minus audio reading, in seconds. Returns true, with the decision, when it completes
		/// a window; the window then starts over.
		/// </summary>
		public bool Add(float clockMinusAudioSeconds, out AudioClockCorrection correction)
		{
			correction = default;
			if (float.IsNaN(clockMinusAudioSeconds) || float.IsInfinity(clockMinusAudioSeconds))
			{
				return false;
			}

			_window[_count++] = clockMinusAudioSeconds;
			if (_count < WINDOW_SIZE)
			{
				return false;
			}

			_count = 0;
			Array.Copy(_window, _sorted, WINDOW_SIZE);
			Array.Sort(_sorted);
			float lower = _sorted[WINDOW_SIZE / 4];
			float upper = _sorted[WINDOW_SIZE - 1 - WINDOW_SIZE / 4];
			correction = Decide(lower, upper - lower);
			return true;
		}

		/// <summary>The decision for an estimated error and the scatter of the readings behind it.</summary>
		public static AudioClockCorrection Decide(float errorSeconds, float spreadSeconds)
		{
			float spread = Math.Max(0f, spreadSeconds);
			float magnitude = Math.Abs(errorSeconds);
			if (magnitude >= HARD_RESYNC_SECONDS)
			{
				AudioClockCorrectionKind kind = errorSeconds < 0f ? AudioClockCorrectionKind.JumpForward : AudioClockCorrectionKind.Wait;
				return new AudioClockCorrection(kind, errorSeconds, spread, 1f);
			}

			if (magnitude < DeadBand(spread))
			{
				return new AudioClockCorrection(AudioClockCorrectionKind.None, errorSeconds, spread, 1f);
			}

			// Ahead (positive error) runs slow, behind runs fast.
			float rate = -errorSeconds / SLEW_TIME_CONSTANT_SECONDS;
			rate = rate < -MAX_SLEW_RATE ? -MAX_SLEW_RATE : rate > MAX_SLEW_RATE ? MAX_SLEW_RATE : rate;
			return new AudioClockCorrection(AudioClockCorrectionKind.Slew, errorSeconds, spread, 1f + rate);
		}

		/// <summary>
		/// The dead band for a scatter: the scatter itself, kept between <see cref="MinDeadBandSeconds"/> and
		/// half of <see cref="HardResyncSeconds"/> so there is always room to slew.
		/// </summary>
		public static float DeadBand(float spreadSeconds)
		{
			float band = Math.Max(MIN_DEAD_BAND_SECONDS, spreadSeconds);
			return Math.Min(band, HARD_RESYNC_SECONDS * 0.5f);
		}
	}
}
