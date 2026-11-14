using Sekai;
using Sekai.Core;

namespace OpenSekai.App.Playback
{
    /// <summary>
    /// The boot handoff the Live scene reads: which live to run and with what data. Captured before
    /// a start overwrites it so a refused start can put the previous values back.
    /// </summary>
    public readonly struct LiveBootState
    {
        public LiveBootState(FreeLiveBootData boot, PlayMode mode)
        {
            Boot = boot;
            Mode = mode;
        }

        public FreeLiveBootData Boot { get; }

        public PlayMode Mode { get; }
    }

    /// <summary>
    /// Publishes one live attempt across the boot boundary and restores the previous state when the
    /// start is refused. Behind an interface so the rollback can be exercised without the live scene.
    /// </summary>
    public interface IPlayBootPublisher
    {
        LiveBootState Capture();

        void Publish(FreeLiveBootData boot);

        void Restore(LiveBootState state);
    }
}
