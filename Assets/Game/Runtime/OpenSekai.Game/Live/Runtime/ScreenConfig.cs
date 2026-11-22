using UnityEngine;
using Sekai;

namespace Sekai.Live
{
	public enum MVQualityType
	{
		Low = 0,
		Middle = 1,
		High = 2
	}

	public static class ScreenConfig
	{
		public enum ResolutionQuality
		{
			High = 0,
			Middle = 1,
			Low = 2,
			Default = 3
		}

		public struct ScreenSize
		{
			public int width;
			public int height;

			public ScreenSize(int width, int height)
			{
				this.width = width;
				this.height = height;
			}
		}

		public static readonly int MinHeight = 640;
		public static readonly int MaxHeight = 1080;
		public static readonly int MinHeightForIngame3DMV = 560;
		public static readonly int MaxHeightForIngame3DMV = 1080;
		private static readonly float _targetLowDpi = 350f;
		private static readonly float _targetHighDpi = 500f;
		private static readonly float _targetLowRenderingScaleForIngame3DMV = 0.6f;
		private static readonly float _targetHighRenderingScaleForIngame3DMV = 0.8f;
		private static readonly float _virtualLiveDefaultTargetDpi = 350f;
		private static readonly float _streamingLiveHighDpi = 500f;
		private static readonly float _streamingLiveDefaultDpi = 320f;
		private static readonly float _streamingLiveLowDpi = 250f;
		public static readonly ScreenSize DefaultSize = new ScreenSize(Screen.width, Screen.height);
		public static readonly float Aspect = DefaultSize.height > 0 ? (float)DefaultSize.width / DefaultSize.height : 16f / 9f;
		public static readonly ScreenSize LowDPISize = CalculateDpiSize(_targetLowDpi, MinHeight, MaxHeight, true);
		public static readonly ScreenSize HighDPISize = CalculateDpiSize(_targetHighDpi, MinHeight, MaxHeight, false);
		public static readonly ScreenSize LowDPISizeForIngame3DMV = CalculateScaleSize(_targetLowRenderingScaleForIngame3DMV, MinHeightForIngame3DMV, MaxHeightForIngame3DMV, true);
		public static readonly ScreenSize HighDPISizeForIngame3DMV = CalculateScaleSize(_targetHighRenderingScaleForIngame3DMV, MinHeightForIngame3DMV, MaxHeightForIngame3DMV, false);
		public static readonly ScreenSize VirtualLiveDefaultDPISize = CalculateDpiSize(_virtualLiveDefaultTargetDpi, MinHeight, MaxHeight, true);
		public static readonly ScreenSize StreamingLiveHighDPISize = CalculateDpiSize(_streamingLiveHighDpi, MinHeight, MaxHeight, true);
		public static readonly ScreenSize StreamingLiveDefaultDPISize = CalculateDpiSize(_streamingLiveDefaultDpi, MinHeight, MaxHeight, true);
		public static readonly ScreenSize StreamingLiveLowDPISize = CalculateDpiSize(_streamingLiveLowDpi, MinHeight, MaxHeight, true);

		public static ResolutionQuality CurrentResolutionQuality { get; private set; } = ResolutionQuality.Default;

		public static void DownResolution(MVQualityType qualityType)
		{
			DownResolution(qualityType == MVQualityType.High ? ResolutionQuality.High : qualityType == MVQualityType.Middle ? ResolutionQuality.Middle : ResolutionQuality.Low);
		}

		public static void DownResolution(ResolutionQuality resolutionQuality)
		{
			CurrentResolutionQuality = resolutionQuality;
			var size = resolutionQuality == ResolutionQuality.Low ? LowDPISize :
				resolutionQuality == ResolutionQuality.Middle ? VirtualLiveDefaultDPISize :
				HighDPISize;
			if (ShouldApplyMobileResolution())
			{
				Screen.SetResolution(size.width, size.height, Screen.fullScreenMode);
			}
		}

		public static void DownResolutionStreaming(ResolutionQuality resolutionQuality)
		{
			DownResolution(resolutionQuality);
		}

		public static void ResetResolution()
		{
			CurrentResolutionQuality = ResolutionQuality.Default;
			if (ShouldApplyMobileResolution())
			{
				Screen.SetResolution(DefaultSize.width, DefaultSize.height, Screen.fullScreenMode);
			}
		}

		private static ScreenSize CalculateDpiSize(float targetDpi, int minHeight, int maxHeight, bool clampMaxHeight)
		{
			float dpi = Screen.dpi;
			float scale = dpi > 0f ? targetDpi / dpi : float.PositiveInfinity;
			return CalculateScaleSize(scale, minHeight, maxHeight, clampMaxHeight);
		}

		private static ScreenSize CalculateScaleSize(float scale, int minHeight, int maxHeight, bool clampMaxHeight)
		{
			float height = Mathf.Max(DefaultSize.height, 1);
			float minScale = Mathf.Min(minHeight / height, 1f);
			float maxScale = clampMaxHeight ? Mathf.Min(maxHeight / height, 1f) : 1f;
			float clampedScale = Mathf.Clamp(scale, minScale, maxScale);
			return new ScreenSize((int)(DefaultSize.width * clampedScale), (int)(DefaultSize.height * clampedScale));
		}

		private static bool ShouldApplyMobileResolution()
		{
#if UNITY_ANDROID || UNITY_IOS
			return true;
#else
			return false;
#endif
		}


	}
}
