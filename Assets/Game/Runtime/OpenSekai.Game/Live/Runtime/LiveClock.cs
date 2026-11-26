using OpenSekai.Shared.Live.Simulation;
using Sekai.Live;

namespace Sekai.Core.Live
{
	internal sealed class LiveClock
	{
		internal const float REFERENCE_FRAME_SECONDS = 1f / 60f;

		internal PracticeRateHistory Rates { get; set; }
		internal double InputWallOffset { get; set; }
		internal double PlaybackRate { get; set; } = 1d;
		internal double GameTime { get; set; }
		internal MusicScoreInfo Frame { get; private set; }
		internal float PreviousFrameTime { get; private set; }
		private bool _hasFrame;

		// The controller controls pause and resume by deciding when to request a frame.
		// Ended sessions advance visual frames without moving the input clock.
		internal MusicScoreInfo Sample(MusicScore score, float scoreTime, double gameTime, bool advanceInputClock)
		{
			score?.Update(scoreTime);
			if (advanceInputClock)
			{
				GameTime = gameTime;
			}
			var sampled = MusicScore.CurrentFrameInfo;
			PreviousFrameTime = _hasFrame && Frame.time <= sampled.time ? Frame.time : sampled.time - REFERENCE_FRAME_SECONDS;
			Frame = sampled;
			_hasFrame = true;
			return Frame;
		}

		internal void ResetFrameHistory() => _hasFrame = false;

		internal float ResolveInputMusicTime(MusicScoreInfo frame, double inputTime)
		{
			if (Rates != null)
			{
				bool legacy = LiveConfig.UseLegacyJudgeTiming;
				double offset = inputTime - GameTime;
				if (!legacy) offset = System.Math.Clamp(offset, -InputTimeMapping.MAX_INPUT_LATENCY_SECONDS, 0d);
				double end = GameTime + InputWallOffset;
				double elapsed = Rates.Distance(end, end + offset);
				return (float)(frame.time + (legacy ? elapsed / 60d : elapsed));
			}

			return LiveConfig.UseLegacyJudgeTiming
				? InputTimeMapping.FromLegacyClock(frame.time, GameTime, inputTime, PlaybackRate)
				: InputTimeMapping.FromClock(frame.time, GameTime, inputTime, PlaybackRate);
		}
	}
}
