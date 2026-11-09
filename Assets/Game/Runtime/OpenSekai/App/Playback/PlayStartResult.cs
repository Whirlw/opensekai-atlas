namespace OpenSekai.App.Playback
{
    public enum PlayStartStatus
    {
        Started,
        Failed,
        Cancelled,
        Busy
    }

    public sealed class PlayStartResult
    {
        public PlayStartStatus Status { get; }
        public string Message { get; }
        public bool Started => Status == PlayStartStatus.Started;

        internal PlayStartResult(PlayStartStatus status, string message)
        {
            Status = status;
            Message = message;
        }
    }
}
