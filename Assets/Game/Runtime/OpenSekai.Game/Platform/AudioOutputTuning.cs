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
		private const int TypeWiredHeadset = 3;
		private const int TypeWiredHeadphones = 4;
		private const int TypeBluetoothA2dp = 8;
		private const int TypeUsbHeadset = 22;
		private const int TypeBleHeadset = 26;
		private const int TypeBleSpeaker = 27;
		private const int GetDevicesOutputs = 2;

		public static string Report { get; private set; } = "audio output not tuned";

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
			bool applied = false;
			if (decision.Changed)
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
				.Append(" plan=").Append(decision.Reason)
				.Append(decision.Changed ? (applied ? " (applied)" : " (reset refused)") : "");
			Report = builder.ToString();
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

			Debug.Log("[AudioOut] route changed: rate=" + AudioSettings.outputSampleRate + " outputs=" + outputs);
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

				AndroidJavaObject[] devices = audioManager.Call<AndroidJavaObject[]>("getDevices", GetDevicesOutputs);
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
							if (type == TypeBluetoothA2dp || type == TypeBleHeadset || type == TypeBleSpeaker)
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
				case TypeWiredHeadset: return "wired-headset";
				case TypeWiredHeadphones: return "wired-headphones";
				case TypeBluetoothA2dp: return "bluetooth-a2dp";
				case TypeUsbHeadset: return "usb-headset";
				case TypeBleHeadset: return "ble-headset";
				case TypeBleSpeaker: return "ble-speaker";
				default: return "type" + type;
			}
		}
	}
}
