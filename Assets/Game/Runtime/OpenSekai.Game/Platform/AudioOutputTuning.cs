using System;
using System.Text;
using OpenSekai.Shared.Live.Simulation;
using UnityEngine;

namespace Sekai
{
	/// <summary>
	/// Configures Unity's audio output for low latency before the first scene plays anything.
	///
	/// The output latency L is everything between "Unity mixed this block" and "the player hears it".
	/// Unity cannot shorten the device's own path, but it can stop adding to it: on Android the output
	/// is moved to the device's native sample rate (a resampled stream is refused the low-latency path)
	/// and the mixing block is aligned to the hardware burst (see <see cref="AudioOutputPlan"/>).
	///
	/// <see cref="AudioSettings.Reset"/> stops every playing sound, so this runs once, at startup, and a
	/// later route change (headphones plugged in, Bluetooth connecting) is only reported, never re-applied
	/// mid-song. <see cref="Report"/> is the one-line summary the diagnostics print: a Bluetooth output
	/// adds 100 ms or more that nothing in the app can remove.
	/// </summary>
	public static class AudioOutputTuning
	{
		// android.media.AudioDeviceInfo types.
		private const int TYPE_WIRED_HEADSET = 3;
		private const int TYPE_WIRED_HEADPHONES = 4;
		private const int TYPE_BLUETOOTH_A_2_DP = 8;
		private const int TYPE_USB_HEADSET = 22;
		private const int TYPE_BLE_HEADSET = 26;
		private const int TYPE_BLE_SPEAKER = 27;
		private const int GET_DEVICES_OUTPUTS = 2;

		public static string Report { get; private set; } = "audio output not tuned";
		internal static volatile string LatestRouteReport;

		/// <summary>True when an output that adds its own large latency (Bluetooth) is connected.</summary>
		public static bool HasBluetoothOutput { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Apply()
		{
			AudioConfiguration config = AudioSettings.GetConfiguration();
			int nativeRate = 0;
			int nativeBurst = 0;
			bool lowLatencyFeature = false;
			string outputs = "unknown";
			try
			{
				QueryDevice(out nativeRate, out nativeBurst, out lowLatencyFeature, out outputs);
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[AudioOut] device query failed: " + exception.Message);
			}

			AudioOutputDecision decision = AudioOutputPlan.Choose(config.sampleRate, config.dspBufferSize, nativeRate, nativeBurst);
			bool resetRequested = decision.Changed;
			bool applied = false;
			if (resetRequested)
			{
				config.sampleRate = decision.SampleRate;
				config.dspBufferSize = decision.DspBufferSize;
				applied = AudioSettings.Reset(config);
			}

			AudioSettings.GetDSPBufferSize(out int bufferLength, out int bufferCount);
			var builder = new StringBuilder();
			builder.Append("[AudioOut] rate=").Append(AudioSettings.outputSampleRate)
				.Append(" dsp=").Append(bufferLength).Append('x').Append(bufferCount)
				.Append(" native=").Append(nativeRate).Append('/').Append(nativeBurst)
				.Append(" lowLatencyFeature=").Append(lowLatencyFeature)
				.Append(" outputs=").Append(outputs)
				.Append(" requested=").Append(decision.SampleRate).Append('/').Append(decision.DspBufferSize)
				.Append(" reset=").Append(!resetRequested ? "not_needed" : applied ? "accepted" : "refused")
				.Append(" plan=").Append(decision.Reason);
			Report = builder.ToString();
			LatestRouteReport = Report;
			Debug.Log(Report);

			AudioSettings.OnAudioConfigurationChanged -= OnConfigurationChanged;
			AudioSettings.OnAudioConfigurationChanged += OnConfigurationChanged;
		}

		private static void OnConfigurationChanged(bool deviceWasChanged)
		{
			if (!deviceWasChanged)
			{
				return;
			}

			string outputs = "unknown";
			try
			{
				QueryDevice(out _, out _, out _, out outputs);
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[AudioOut] device query failed: " + exception.Message);
			}

			AudioSettings.GetDSPBufferSize(out int bufferLength, out int bufferCount);
			string report = $"[AudioOut] route changed: rate={AudioSettings.outputSampleRate} dsp={bufferLength}x{bufferCount} outputs={outputs}";
			LatestRouteReport = report;
			Debug.Log(report);
		}

		private static void QueryDevice(out int nativeRate, out int nativeBurst, out bool lowLatencyFeature, out string outputs)
		{
			nativeRate = 0;
			nativeBurst = 0;
			lowLatencyFeature = false;
			outputs = "n/a";
			HasBluetoothOutput = false;
#if UNITY_ANDROID && !UNITY_EDITOR
			// The activity belongs to the Unity runtime and must not be disposed here; the objects this
			// call creates are ours.
			AndroidJavaObject activity = UnityEngine.Android.AndroidApplication.currentActivity;
			using (AndroidJavaObject audioManager = activity.Call<AndroidJavaObject>("getSystemService", "audio"))
			using (AndroidJavaObject packageManager = activity.Call<AndroidJavaObject>("getPackageManager"))
			{
				int.TryParse(audioManager.Call<string>("getProperty", "android.media.property.OUTPUT_SAMPLE_RATE"), out nativeRate);
				int.TryParse(audioManager.Call<string>("getProperty", "android.media.property.OUTPUT_FRAMES_PER_BUFFER"), out nativeBurst);
				lowLatencyFeature = packageManager.Call<bool>("hasSystemFeature", "android.hardware.audio.low_latency");

				AndroidJavaObject[] devices = audioManager.Call<AndroidJavaObject[]>("getDevices", GET_DEVICES_OUTPUTS);
				var builder = new StringBuilder();
				if (devices != null)
				{
					foreach (AndroidJavaObject device in devices)
					{
						using (device)
						{
							int type = device.Call<int>("getType");
							if (builder.Length > 0) builder.Append(',');
							builder.Append(Describe(type));
							if (type == TYPE_BLUETOOTH_A_2_DP || type == TYPE_BLE_HEADSET || type == TYPE_BLE_SPEAKER)
							{
								HasBluetoothOutput = true;
							}
						}
					}
				}

				outputs = builder.Length > 0 ? builder.ToString() : "none";
			}
#endif
		}

		private static string Describe(int type)
		{
			switch (type)
			{
				case 1: return "earpiece";
				case 2: return "speaker";
				case 18: return "telephony";
				case TYPE_WIRED_HEADSET: return "wired-headset";
				case TYPE_WIRED_HEADPHONES: return "wired-headphones";
				case TYPE_BLUETOOTH_A_2_DP: return "bluetooth-a2dp";
				case TYPE_USB_HEADSET: return "usb-headset";
				case TYPE_BLE_HEADSET: return "ble-headset";
				case TYPE_BLE_SPEAKER: return "ble-speaker";
				default: return "type" + type;
			}
		}
	}
}
