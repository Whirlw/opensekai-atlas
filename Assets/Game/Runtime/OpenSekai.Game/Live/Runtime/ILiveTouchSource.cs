namespace Sekai.Core.Live
{
    internal interface ILiveTouchSource
    {
        int Count { get; }
        LiveTouchSample this[int index] { get; }
    }
}
