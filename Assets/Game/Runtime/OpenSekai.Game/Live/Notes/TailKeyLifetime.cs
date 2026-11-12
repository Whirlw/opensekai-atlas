using Sekai.Live;

namespace Sekai
{
    public enum TailKeyPhase
    {
        Hidden,
        Following,
        SlidingOut,
        Retired
    }

    /// <summary>尾键的视图相位；不持有音符、视图或跨帧状态。判定窗口不等于视图窗口。</summary>
    public static class TailKeyLifetime
    {
        // visibleFrom 是调用者提供的视图出场时间，通常为 tailTime 减去显示提前量。
        // 依据：原 ReleaseTailKey 的 currentTime < MusicScoreInfo.time，以尾音符自身时间释放。
        // 跟随区间为 [visibleFrom, tailTime)，到尾音符自己的时间点即开始滑出。
        // 判定窗口不等于视图窗口；这里不持有判定表或任何毫秒常量。
        public static TailKeyPhase Of(float tailTime, float visibleFrom, float now, NoteResult result, bool barEnded)
        {
            // 结束信号优先于时间，已结算的尾键不能因时间回摆重新出现。
            if (result != NoteResult.None || barEnded) return TailKeyPhase.Retired;
            if (now < visibleFrom) return TailKeyPhase.Hidden;
            if (now < tailTime) return TailKeyPhase.Following;
            // 未判定的尾键继续滑出，离屏回收由调用者负责，不在此自行超时。
            return TailKeyPhase.SlidingOut;
        }
    }
}
