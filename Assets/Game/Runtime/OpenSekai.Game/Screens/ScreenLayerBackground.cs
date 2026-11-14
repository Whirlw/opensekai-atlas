using System;

namespace Sekai
{
	public class ScreenLayerBackground : ScreenLayer
	{
		public enum BackgroundType
		{
			Unknown = 0,
			Default = 1
		}

		public BackgroundType CurrentBackgroundType { get; private set; } = BackgroundType.Unknown;

		public void Show(BackgroundType type, Action<CrossFader.FinishedStatus> onFinished = null)
		{
			// TODO: load and crossfade the background bundle.
			CurrentBackgroundType = type;
			onFinished?.Invoke(CrossFader.FinishedStatus.Complete);
		}

		public void Hide(Action<CrossFader.FinishedStatus> onFinished = null)
		{
			onFinished?.Invoke(CrossFader.FinishedStatus.Complete);
		}
	}
}
