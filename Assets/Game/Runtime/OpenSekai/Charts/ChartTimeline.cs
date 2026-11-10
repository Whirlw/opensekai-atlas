using System;
using System.Collections.Generic;
using Sekai;
using Sekai.Live;
using System.Globalization;
using UnityEngine;
namespace OpenSekai.Charts
{
    public static class ChartTimeline
    {
        public const long TICKS_PER_BAR = 1920L;

		/// <summary>How many ticks one bar of this meter takes. 480 ticks to the beat, times the beats in a bar.</summary>
		private static long BarTicks(float timeSignature)
		{
			return timeSignature > 0f ? (long)Math.Round(timeSignature * TICKS_PER_BEAT) : TICKS_PER_BAR;
		}
        public const long TICKS_PER_BEAT = 480L;
        private const float DEFAULT_BPM = 120f, DEFAULT_TIME_SIGNATURE = 4f;
        private const float DEFAULT_SPEED_RATIO = 1f, DEFAULT_SE_VOLUME = 1f, SECONDS_PER_MINUTES = 60f;
		public static string FormatTimeSignatureText(float timeSignature)
		{
			if (Mathf.Approximately(timeSignature, 2f))
			{
				return "2/4";
			}
			if (Mathf.Approximately(timeSignature, 3f))
			{
				return "3/4";
			}
			if (Mathf.Approximately(timeSignature, 4f))
			{
				return "4/4";
			}
			if (Mathf.Approximately(timeSignature, 5f))
			{
				return "5/4";
			}
			if (Mathf.Approximately(timeSignature, 6f))
			{
				return "6/4";
			}
			if (Mathf.Approximately(timeSignature, 7f))
			{
				return "7/4";
			}
			if (Mathf.Approximately(timeSignature, 8f))
			{
				return "8/4";
			}
			return ConvertFloatToTimeSignatureString(timeSignature);
		}

		private static string ConvertFloatToTimeSignatureString(float timeSignature)
		{
			if (timeSignature <= 0f)
			{
				return "4/4";
			}
			int[] denominators = { 4, 8, 16, 2, 1, 32 };
			foreach (int denominator in denominators)
			{
				float numerator = denominator * timeSignature * 0.25f;
				int roundedNumerator = (int)Math.Round(numerator);
				if (roundedNumerator >= 1 && Math.Abs(numerator - roundedNumerator) < 0.001f)
				{
					return $"{roundedNumerator}/{denominator}";
				}
			}
			return "4/4";
		}

		public static long CalculateTicksFromBarAndProgressSorted(int bar, float barProgress, List<MusicScoreInfo> sortedInfos)
		{
			if (sortedInfos == null || sortedInfos.Count == 0)
			{
				return 1920L * bar + (long)Math.Round(barProgress * 1920f);
			}
			long ticks = 0L;
			int currentBar = 0;
			float currentBarLength = TICKS_PER_BAR;
			for (int i = 0; i < sortedInfos.Count; i++)
			{
				MusicScoreInfo info = sortedInfos[i];
				int nextBar = Math.Min(bar, info.bar);
				if (nextBar - currentBar >= 1)
				{
					ticks += (long)(currentBarLength * (nextBar - currentBar));
					currentBar = nextBar;
				}
				if (info.bar > bar || (info.bar == bar && info.barProgress > barProgress))
				{
					break;
				}
				currentBarLength = info.timeSignature * 0.25f * TICKS_PER_BAR;
			}
			if (bar - currentBar >= 1)
			{
				ticks += (long)(currentBarLength * (bar - currentBar));
			}
			return ticks + (long)Math.Round(currentBarLength * barProgress);
		}

		public static List<MusicScoreInfo> CreateSortedMusicScoreInfoList(MusicScoreInfo[] infos)
		{
			List<MusicScoreInfo> result = infos == null ? new List<MusicScoreInfo>() : new List<MusicScoreInfo>(infos);
			result.Sort((a, b) =>
			{
				int compare = a.bar.CompareTo(b.bar);
				return compare != 0 ? compare : a.barProgress.CompareTo(b.barProgress);
			});
			return result;
		}

		public static (int, int) GetTimeSignatureFromChangeValue(object changeValue)
		{
			if (TryParseTimeSignature(changeValue, out int numerator, out int denominator))
			{
				return (numerator, denominator);
			}
			float value = ConvertToFloat(changeValue, DEFAULT_TIME_SIGNATURE);
			string text = FormatTimeSignatureText(value);
			if (TryParseTimeSignature(text, out numerator, out denominator))
			{
				return (numerator, denominator);
			}
			return (4, 4);
		}

		private static bool TryParseTimeSignature(object changeValue, out int numerator, out int denominator)
		{
			numerator = 4;
			denominator = 4;
			if (changeValue == null)
			{
				return false;
			}
			string text = changeValue.ToString();
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}
			string[] parts = text.Split('/');
			if (parts.Length == 2
				&& int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out numerator)
				&& int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out denominator)
				&& numerator > 0
				&& denominator > 0)
			{
				return true;
			}
			if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
			{
				numerator = Mathf.RoundToInt(value);
				denominator = 4;
				return numerator > 0;
			}
			return false;
		}

		private static long GetTicksFromTimeBpmOnly(float currentTimeSec, (long ticks, float bpm)[] bpmEvents, int count)
		{
			if (bpmEvents == null || count == 0)
			{
				return 0L;
			}
			float elapsed = 0f;
			for (int i = 0; i < count; i++)
			{
				long startTicks = bpmEvents[i].ticks;
				float bpm = bpmEvents[i].bpm <= 0f ? DEFAULT_BPM : bpmEvents[i].bpm;
				if (i + 1 < count)
				{
					long endTicks = bpmEvents[i + 1].ticks;
					float segmentSec = (endTicks - startTicks) / (float)TICKS_PER_BEAT * SECONDS_PER_MINUTES / bpm;
					if (currentTimeSec <= elapsed + segmentSec)
					{
						return startTicks + (long)Math.Round((currentTimeSec - elapsed) * bpm / SECONDS_PER_MINUTES * TICKS_PER_BEAT);
					}
					elapsed += segmentSec;
				}
				else
				{
					return startTicks + (long)Math.Round((currentTimeSec - elapsed) * bpm / SECONDS_PER_MINUTES * TICKS_PER_BEAT);
				}
			}
			return 0L;
		}

		private static float GetTimeFromTicksBpmOnly(long ticks, (long ticks, float bpm)[] bpmEvents, int count)
		{
			if (bpmEvents == null || count == 0)
			{
				return 0f;
			}
			float time = 0f;
			for (int i = 0; i < count; i++)
			{
				long startTicks = bpmEvents[i].ticks;
				float bpm = bpmEvents[i].bpm <= 0f ? DEFAULT_BPM : bpmEvents[i].bpm;
				long endTicks = i + 1 < count ? bpmEvents[i + 1].ticks : ticks;
				if (ticks <= endTicks || i + 1 == count)
				{
					return time + (ticks - startTicks) / (float)TICKS_PER_BEAT * SECONDS_PER_MINUTES / bpm;
				}
				time += (endTicks - startTicks) / (float)TICKS_PER_BEAT * SECONDS_PER_MINUTES / bpm;
			}
			return time;
		}

		public static MusicScoreInfo[] ConvertMusicScoreInfo(List<ChartEvent> eventArray)
		{
			if (eventArray == null || eventArray.Count == 0)
			{
				return new[] { new MusicScoreInfo(0, 0f, 0f, DEFAULT_BPM, DEFAULT_TIME_SIGNATURE, DEFAULT_SPEED_RATIO, DEFAULT_SE_VOLUME) };
			}
			List<ChartEvent> sorted = new List<ChartEvent>(eventArray);
			sorted.Sort((a, b) => a.ticks.CompareTo(b.ticks));
			List<MusicScoreInfo> infos = new List<MusicScoreInfo>(sorted.Count);
			float bpm = DEFAULT_BPM;
			float timeSignature = DEFAULT_TIME_SIGNATURE;
			float speedRatio = DEFAULT_SPEED_RATIO;
			float seVolume = DEFAULT_SE_VOLUME;
			int index = 0;
			long barStartTicks = 0L;
			int barIndex = 0;
			float previousTimeSignature = DEFAULT_TIME_SIGNATURE;
			long previousTicks = -1L;
			float previousTime = 0f;
			float previousBpm = DEFAULT_BPM;
			while (index < sorted.Count)
			{
				long ticks = sorted[index].ticks;
				while (index < sorted.Count && sorted[index].ticks == ticks)
				{
					ChartEvent evt = sorted[index++];
					switch (evt.eventType)
					{
					case ChartEventType.BPM:
						bpm = ConvertToFloat(evt.changeValue, bpm);
						break;
					case ChartEventType.HighSpeed:
						speedRatio = ConvertToFloat(evt.changeValue, speedRatio);
						break;
					case ChartEventType.SeVolume:
						seVolume = ConvertToFloat(evt.changeValue, seVolume);
						break;
					case ChartEventType.TimeSignature:
					{
						(int numerator, int denominator) = GetTimeSignatureFromChangeValue(evt.changeValue);
						timeSignature = numerator / (float)denominator * 4f;
						break;
					}
					default:
						throw new ArgumentOutOfRangeException();
					}
				}
				// A bar is as long as the meter in force says: 1920 ticks in 4/4, 1440 in 3/4. A meter
				// change is only ever inserted between two whole bars, so it starts a new one.
				if (timeSignature != previousTimeSignature)
				{
					long leaveTicks = BarTicks(previousTimeSignature);
					while (leaveTicks > 0 && ticks - barStartTicks >= leaveTicks)
					{
						barStartTicks += leaveTicks;
						barIndex++;
						leaveTicks = BarTicks(previousTimeSignature);
					}
					barStartTicks = ticks;
					if (ticks > barStartTicks) { barIndex++; }
					previousTimeSignature = timeSignature;
				}

				long barTicks = BarTicks(timeSignature);
				while (barTicks > 0 && ticks - barStartTicks >= barTicks)
				{
					barStartTicks += barTicks;
					barIndex++;
				}

				float barProgress = barTicks > 0 ? (ticks - barStartTicks) / (float)barTicks : 0f;
				float time = previousTicks < 0L
					? 0f
					: previousTime + (ticks - previousTicks) / (float)TICKS_PER_BEAT * SECONDS_PER_MINUTES / previousBpm;
				infos.Add(new MusicScoreInfo(barIndex, barProgress, time, bpm, timeSignature, speedRatio, seVolume));
				previousTicks = ticks;
				previousTime = time;
				previousBpm = bpm;
			}
			return infos.ToArray();
		}

		private static void CalcTime(List<MusicScoreInfo> scoreInfoList)
		{
			if (scoreInfoList == null || scoreInfoList.Count == 0)
			{
				return;
			}
			scoreInfoList.Sort((a, b) =>
			{
				long at = TicksFromBarProgress(a.bar, a.barProgress);
				long bt = TicksFromBarProgress(b.bar, b.barProgress);
				return at.CompareTo(bt);
			});
			float time = 0f;
			long previousTicks = TicksFromBarProgress(scoreInfoList[0].bar, scoreInfoList[0].barProgress);
			float previousBpm = scoreInfoList[0].bpm <= 0f ? DEFAULT_BPM : scoreInfoList[0].bpm;
			for (int i = 0; i < scoreInfoList.Count; i++)
			{
				MusicScoreInfo info = scoreInfoList[i];
				long ticks = TicksFromBarProgress(info.bar, info.barProgress);
				if (i > 0)
				{
					time += (ticks - previousTicks) / (float)TICKS_PER_BEAT * SECONDS_PER_MINUTES / previousBpm;
				}
				info.time = time;
				scoreInfoList[i] = info;
				previousTicks = ticks;
				previousBpm = info.bpm <= 0f ? DEFAULT_BPM : info.bpm;
			}
		}

		public static float GetTimeFromTicks(long ticks, MusicScoreInfo[] musicScoreInfoArray)
		{
			(long ticks, float bpm)[] bpmEvents = BuildBpmEvents(musicScoreInfoArray);
			return GetTimeFromTicksBpmOnly(ticks, bpmEvents, bpmEvents.Length);
		}

		private static float ConvertToFloat(object value, float defaultValue)
		{
			if (value == null)
			{
				return defaultValue;
			}
			if (value is float floatValue)
			{
				return floatValue;
			}
			if (value is double doubleValue)
			{
				return (float)doubleValue;
			}
			if (value is int intValue)
			{
				return intValue;
			}
			if (float.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
			{
				return parsed;
			}
			return defaultValue;
		}

		private static long TicksFromBarProgress(int bar, float barProgress)
		{
			return TICKS_PER_BAR * bar + (long)Math.Round(barProgress * TICKS_PER_BAR);
		}



		private static (long ticks, float bpm)[] BuildBpmEvents(MusicScoreInfo[] musicScoreInfoArray)
		{
			if (musicScoreInfoArray == null || musicScoreInfoArray.Length == 0)
			{
				return new[] { (0L, DEFAULT_BPM) };
			}
			List<(long ticks, float bpm)> events = new List<(long ticks, float bpm)>(musicScoreInfoArray.Length + 1);
			foreach (MusicScoreInfo info in musicScoreInfoArray)
			{
				events.Add((TicksFromBarProgress(info.bar, info.barProgress), info.bpm <= 0f ? DEFAULT_BPM : info.bpm));
			}
			events.Sort((a,b) => a.ticks.CompareTo(b.ticks));
			int write = 0;
			for (int read = 0; read < events.Count; read++)
			{
				if (read + 1 < events.Count && events[read + 1].ticks == events[read].ticks)
				{
					continue;
				}
				events[write++] = events[read];
			}
			if (write < events.Count)
			{
				events.RemoveRange(write, events.Count - write);
			}
			if (events.Count == 0 || events[0].ticks > 0L)
			{
				events.Insert(0, (0L, DEFAULT_BPM));
			}
			return events.ToArray();
		}



		public static long GetTicksFromTime(float currentTimeSec, MusicScoreInfo[] musicScoreInfoArray)
		{
			(long ticks, float bpm)[] bpmEvents = BuildBpmEvents(musicScoreInfoArray);
			return GetTicksFromTimeBpmOnly(currentTimeSec, bpmEvents, bpmEvents.Length);
		}
		public static MusicScoreInfo GenerateMusicScoreInfoFromTicks(long ticks, MusicScoreInfo[] infos)
		{
			if (infos == null || infos.Length == 0)
			{
				return default(MusicScoreInfo);
			}

			// Time advances with tempo only, so it is the meter-free coordinate; the bar the note sits in
			// is then read off the segment that was in force at that time, one bar of that meter at a time.
			float time = GetTimeFromTicks(ticks, infos);
			MusicScoreInfo segment = infos[0];
			for (int i = 0; i < infos.Length; i++)
			{
				if (infos[i].time <= time + 0.0001f)
				{
					segment = infos[i];
				}
				else
				{
					break;
				}
			}

			float bpm = segment.bpm > 0f ? segment.bpm : DEFAULT_BPM;
			float signature = segment.timeSignature > 0f ? segment.timeSignature : DEFAULT_TIME_SIGNATURE;
			float barSeconds = signature * SECONDS_PER_MINUTES / bpm;
			float elapsed = time - segment.time;
			int bars = barSeconds > 0f ? (int)Math.Floor(elapsed / barSeconds + 0.0001f) : 0;
			float progress = barSeconds > 0f ? (elapsed - bars * barSeconds) / barSeconds : 0f;
			return new MusicScoreInfo(segment.bar + bars, progress, time, bpm, signature, segment.speedRatio, segment.seVolume);
		}
    }
}