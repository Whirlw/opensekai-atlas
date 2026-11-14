namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// The frame length the judgement tables are written in.
    /// </summary>
    /// <remarks>
    /// Judgement windows are defined as frame counts at 60 fps (<c>LiveConfig.AddJudgeFrame</c> uses
    /// 2.5 / 5 / 7.5 frames and so on). The device runs at 120 or 165 Hz, so "one frame" is 8.3 or
    /// 6.1 ms of wall time, and any liveness check written as <c>Time.frameCount</c> arithmetic
    /// silently means something different on each device. Everything that has to express "one frame
    /// of tolerance" uses this constant instead, so the answer does not depend on the refresh rate.
    /// </remarks>
    public static class ReferenceFrame
    {
        /// <summary>Seconds in one frame of the judgement tables (60 fps).</summary>
        public const float Seconds = 1f / 60f;

        /// <summary>Converts a frame count from the judgement tables into seconds.</summary>
        public static float FromFrames(float frames)
        {
            return frames * Seconds;
        }

        /// <summary>
        /// True while an input recorded at <paramref name="lastInputTime"/> still counts as present
        /// at <paramref name="currentTime"/>, allowing <paramref name="toleranceSeconds"/> of slack.
        /// </summary>
        public static bool IsStillFresh(float lastInputTime, float currentTime, float toleranceSeconds)
        {
            return lastInputTime + toleranceSeconds >= currentTime;
        }
    }
}
