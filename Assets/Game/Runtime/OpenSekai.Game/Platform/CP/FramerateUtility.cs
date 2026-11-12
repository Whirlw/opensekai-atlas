using System;
using UnityEngine;

namespace CP
{
	public static class FramerateUtility
	{
		public const int DEFAULT_FRAME_RATE = 60;

		/// <summary>
		/// Frame rate limit that means "no explicit cap": follow the display refresh rate.
		/// </summary>
		public const int FOLLOW_DISPLAY = 0;

		/// <summary>
		/// Limits selectable from the settings dialog. Keep this in sync with the UI.
		/// </summary>
		public static readonly int[] SupportedLimits = { FOLLOW_DISPLAY, 120, 144, 165, 185, 240 };

		public static void SetFrameRate(int targetFrameRate = -1)
		{
			Application.targetFrameRate = targetFrameRate > 0 ? targetFrameRate : GetFeasibleFramerate();
		}

		public static bool IsSupportedLimit(int limit)
		{
			return Array.IndexOf(SupportedLimits, limit) >= 0;
		}

		public static int SanitizeLimit(int limit)
		{
			return IsSupportedLimit(limit) ? limit : FOLLOW_DISPLAY;
		}

		public static string GetLimitLabel(int limit)
		{
			return limit > 0 ? limit + " FPS" : "跟随屏幕";
		}

		/// <summary>
		/// Applies the user selected frame rate cap. A positive value is used verbatim so the game
		/// keeps a predictable frame interval (a rhythm game cares more about consistent latency
		/// than about the biggest possible number). Zero follows the display refresh rate.
		/// Returns the frame rate that was actually requested from Unity.
		/// </summary>
		public static int ApplyFrameRateLimit(int limit)
		{
			limit = SanitizeLimit(limit);
			QualitySettings.vSyncCount = 0;

			float appliedDisplayRefreshRate = RequestDisplayRefreshRate(limit);

			int targetFrameRate = limit > 0 ? limit : GetFeasibleFramerate();
			if (limit > 0 && appliedDisplayRefreshRate > 0f && appliedDisplayRefreshRate + 0.5f < limit)
			{
				// The panel cannot go faster than this mode. Asking Unity for more only burns power
				// and makes the frame interval less predictable, so clamp to what the display shows.
				targetFrameRate = Mathf.Max(1, Mathf.RoundToInt(appliedDisplayRefreshRate));
			}

			Application.targetFrameRate = targetFrameRate;
			Debug.Log($"[FramerateUtility] targetFrameRate={targetFrameRate} limit={(limit > 0 ? limit.ToString() : "follow")} display={GetDisplayRefreshRate():0.##}Hz appliedMode={appliedDisplayRefreshRate:0.##}Hz vSync={QualitySettings.vSyncCount}");
			return targetFrameRate;
		}

		/// <summary>
		/// Current display refresh rate in Hz. Returns 0 when the platform does not report it.
		/// </summary>
		public static float GetDisplayRefreshRate()
		{
#if UNITY_2022_2_OR_NEWER
			RefreshRate refreshRateRatio = Screen.currentResolution.refreshRateRatio;
			if (refreshRateRatio.numerator > 0 && refreshRateRatio.denominator > 0)
			{
				return (float)((double)refreshRateRatio.numerator / refreshRateRatio.denominator);
			}
#endif

#pragma warning disable CS0618
			return Screen.currentResolution.refreshRate;
#pragma warning restore CS0618
		}

		private static int GetFeasibleFramerate()
		{
			float refreshRate = GetDisplayRefreshRate();
			return refreshRate > 0f ? Mathf.Max(1, Mathf.RoundToInt(refreshRate)) : DEFAULT_FRAME_RATE;
		}

		/// <summary>
		/// Android compositors keep the panel in its default mode (usually 60Hz) unless the app asks
		/// for a specific display mode, so setting Application.targetFrameRate alone is not enough on
		/// a 120Hz+ phone. Returns the refresh rate that was requested, or 0 when nothing changed.
		/// </summary>
		private static float RequestDisplayRefreshRate(int limit)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (limit <= 0)
			{
				return 0f;
			}

			try
			{
				return TryRequestAndroidDisplayMode(limit);
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[FramerateUtility] failed to request Android display mode: " + exception);
			}
#endif
			return 0f;
		}

#if UNITY_ANDROID && !UNITY_EDITOR
		private static float TryRequestAndroidDisplayMode(int limit)
		{
			int sdkInt = GetAndroidSdkInt();
			if (sdkInt < 23)
			{
				Debug.LogWarning($"[FramerateUtility] Android SDK {sdkInt} does not support display modes.");
				return 0f;
			}
			
			using (AndroidJavaClass playerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
			using (AndroidJavaObject activity = playerClass.GetStatic<AndroidJavaObject>("currentActivity"))
			{
				if (activity == null)
				{
					return 0f;
				}
				
				AndroidJavaObject display = GetAndroidDisplay(activity, sdkInt);
				if (display == null)
				{
					Debug.LogWarning("[FramerateUtility] could not resolve the Android Display.");
					return 0f;
				}
				
				int targetModeId;
				float targetRefreshRate;
				try
				{
					targetModeId = FindBestDisplayModeId(display, limit, out targetRefreshRate);
				}
				finally
				{
					display.Dispose();
				}
				
				if (targetModeId < 0)
				{
					Debug.LogWarning($"[FramerateUtility] no display mode at or below {limit}Hz; keeping the current mode.");
					return 0f;
				}
				
				// Window attributes belong to the Android UI thread. Unity's main thread is a different thread,
				// so calling setAttributes there throws CalledFromWrongThreadException and the display silently
				// keeps its default mode. Post the mutation to the UI thread instead of calling it inline.
				int modeId = targetModeId;
				float refreshRate = targetRefreshRate;
				int requestedLimit = limit;
				activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
				{
					try
					{
						using (AndroidJavaClass uiPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
						using (AndroidJavaObject uiActivity = uiPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
						using (AndroidJavaObject uiWindow = uiActivity.Call<AndroidJavaObject>("getWindow"))
						using (AndroidJavaObject uiAttributes = uiWindow.Call<AndroidJavaObject>("getAttributes"))
						{
							if (uiAttributes == null)
							{
								return;
							}
							
							uiAttributes.Set("preferredDisplayModeId", modeId);
							uiWindow.Call("setAttributes", uiAttributes);
							Debug.Log($"[FramerateUtility] Android display mode -> id={modeId} {refreshRate:0.##}Hz (requested {requestedLimit}Hz)");
						}
					}
					catch (Exception exception)
					{
						Debug.LogWarning("[FramerateUtility] failed to apply Android display mode: " + exception);
					}
				}));
				
				return targetRefreshRate;
			}
		}

		private static AndroidJavaObject GetAndroidDisplay(AndroidJavaObject activity, int sdkInt)
		{
			if (sdkInt >= 30)
			{
				AndroidJavaObject activityDisplay = activity.Call<AndroidJavaObject>("getDisplay");
				if (activityDisplay != null)
				{
					return activityDisplay;
				}
			}

			using (AndroidJavaObject windowManager = activity.Call<AndroidJavaObject>("getSystemService", "window"))
			{
				return windowManager?.Call<AndroidJavaObject>("getDefaultDisplay");
			}
		}

		/// <summary>
		/// Picks the highest refresh rate that does not exceed <paramref name="limit"/> while keeping
		/// the current resolution, so changing the frame rate never changes the render size.
		/// </summary>
		private static int FindBestDisplayModeId(AndroidJavaObject display, int limit, out float targetRefreshRate)
		{
			targetRefreshRate = 0f;
			int targetModeId = -1;

			int currentWidth = 0;
			int currentHeight = 0;
			using (AndroidJavaObject currentMode = display.Call<AndroidJavaObject>("getMode"))
			{
				if (currentMode != null)
				{
					currentWidth = currentMode.Call<int>("getPhysicalWidth");
					currentHeight = currentMode.Call<int>("getPhysicalHeight");
				}
			}

			AndroidJavaObject[] modes = display.Call<AndroidJavaObject[]>("getSupportedModes");
			if (modes == null)
			{
				return -1;
			}

			foreach (AndroidJavaObject mode in modes)
			{
				if (mode == null)
				{
					continue;
				}

				try
				{
					if (currentWidth > 0 && mode.Call<int>("getPhysicalWidth") != currentWidth)
					{
						continue;
					}
					if (currentHeight > 0 && mode.Call<int>("getPhysicalHeight") != currentHeight)
					{
						continue;
					}

					float refreshRate = mode.Call<float>("getRefreshRate");
					if (refreshRate > limit + 0.5f || refreshRate <= targetRefreshRate)
					{
						continue;
					}

					targetRefreshRate = refreshRate;
					targetModeId = mode.Call<int>("getModeId");
				}
				finally
				{
					mode.Dispose();
				}
			}

			return targetModeId;
		}

		private static int GetAndroidSdkInt()
		{
			using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
			{
				return version.GetStatic<int>("SDK_INT");
			}
		}
#endif
	}
}
