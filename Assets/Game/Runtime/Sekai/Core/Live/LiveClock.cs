using OpenSekai.Shared.Live.Simulation;
using Sekai.Live;

namespace Sekai.Core.Live
{
	internal sealed class LiveClock
	{
		internal const float ReferenceFrameSeconds = 1f / 60f;

		internal double GameTime { get; set; }
		internal MusicScoreInfo Frame { get; private set; }

		// The controller controls pause and resume by deciding when to request a frame.
		// Ended sessions advance visual frames without moving the input clock.
		internal MusicScoreInfo Sample(MusicScore score, float scoreTime, double gameTime, bool advanceInputClock)
		{
			score?.Update(scoreTime);
			if (advanceInputClock)
			{
				GameTime = gameTime;
			}
			Frame = MusicScore.CurrentFrameInfo;
			return Frame;
		}

		internal float ResolveInputMusicTime(MusicScoreInfo frame, double inputTime)
		{
			return LiveConfig.UseLegacyJudgeTiming
				? InputTimeMapping.FromLegacyClock(frame.time, GameTime, inputTime)
				: InputTimeMapping.FromClock(frame.time, GameTime, inputTime);
		}
	}
}
