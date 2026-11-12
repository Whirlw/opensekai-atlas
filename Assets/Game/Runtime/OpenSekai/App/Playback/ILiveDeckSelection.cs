namespace OpenSekai.App.Playback
{
    /// <summary>
    /// Which deck the next live runs with. Split out of the boot handoff because it is player state
    /// read at start time, not part of what gets published to the live scene.
    /// </summary>
    public interface ILiveDeckSelection
    {
        int SelectedDeckId { get; }
    }
}
