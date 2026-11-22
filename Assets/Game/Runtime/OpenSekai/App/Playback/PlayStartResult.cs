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
        public System.Collections.Generic.IReadOnlyList<string> MaterialPackDiagnostics { get; }
        public bool Started => Status == PlayStartStatus.Started;

        internal PlayStartResult(PlayStartStatus status, string message, string[] materialPackDiagnostics = null)
        {
            Status = status;
            Message = message;
            MaterialPackDiagnostics = materialPackDiagnostics ?? System.Array.Empty<string>();
        }
    }
}
