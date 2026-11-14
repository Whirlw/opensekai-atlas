namespace OpenSekai.Shared.Live.Simulation
{
	/// <summary>What the audio output should be configured to, and why.</summary>
	public readonly struct AudioOutputDecision
	{
		public AudioOutputDecision(int sampleRate, int dspBufferSize, bool changed, string reason)
		{
			SampleRate = sampleRate;
			DspBufferSize = dspBufferSize;
			Changed = changed;
			Reason = reason;
		}

		public int SampleRate { get; }
		public int DspBufferSize { get; }
		public bool Changed { get; }
		public string Reason { get; }
	}

	/// <summary>
	/// Chooses the audio output configuration that keeps Android's output latency low.
	///
	/// Android's own guidance: run the output at the device's native sample rate, so the system mixer
	/// does not have to resample (a resampled stream is denied the low-latency path), and keep the
	/// mixing block a whole multiple of the hardware burst, so the stream does not wait for partial
	/// bursts. Both numbers come from AudioManager's OUTPUT_SAMPLE_RATE and OUTPUT_FRAMES_PER_BUFFER.
	///
	/// The plan never makes the block larger than it already is: a bigger block is more latency, and a
	/// block smaller than <see cref="MinDspBufferSize"/> is where weak devices start to crackle. A device
	/// that reports nothing (or nonsense) keeps the configuration it has.
	/// </summary>
	public static class AudioOutputPlan
	{
		public const int MinSampleRate = 22050;
		public const int MaxSampleRate = 192000;
		public const int MinDspBufferSize = 128;
		public const int MaxBurstFrames = 1024;

		public static AudioOutputDecision Choose(int currentSampleRate, int currentDspBufferSize,
			int nativeSampleRate, int nativeFramesPerBuffer)
		{
			int sampleRate = currentSampleRate;
			string rateReason;
			if (nativeSampleRate >= MinSampleRate && nativeSampleRate <= MaxSampleRate)
			{
				sampleRate = nativeSampleRate;
				rateReason = nativeSampleRate == currentSampleRate ? "rate already native" : "rate set to native";
			}
			else
			{
				rateReason = "native rate unknown";
			}

			int buffer = currentDspBufferSize;
			string bufferReason;
			if (nativeFramesPerBuffer > 0 && nativeFramesPerBuffer <= MaxBurstFrames)
			{
				int aligned = nativeFramesPerBuffer;
				while (aligned < MinDspBufferSize)
				{
					aligned += nativeFramesPerBuffer;
				}

				if (aligned == currentDspBufferSize)
				{
					bufferReason = "buffer already aligned";
				}
				else if (aligned < currentDspBufferSize || currentDspBufferSize <= 0)
				{
					buffer = aligned;
					bufferReason = "buffer aligned to burst";
				}
				else
				{
					bufferReason = "aligned buffer would be larger, kept";
				}
			}
			else
			{
				bufferReason = "burst unknown";
			}

			bool changed = sampleRate != currentSampleRate || buffer != currentDspBufferSize;
			return new AudioOutputDecision(sampleRate, buffer, changed, rateReason + "; " + bufferReason);
		}
	}
}
