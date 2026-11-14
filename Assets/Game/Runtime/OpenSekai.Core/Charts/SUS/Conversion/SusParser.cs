using System.Collections.Generic;
using System.Text.RegularExpressions;
using Sekai.Live;
using Sekai.SUS;
using EventType = Sekai.SUS.EventType;

namespace OpenSekai.Shared.Conversion
{
	/// <summary>
	/// Reads the authoring form of a SUS chart: the <c>#xxx</c> text rows, header directives and note
	/// data rows, resolved into the DTO tables of <see cref="SusChartData"/>.
	/// </summary>
	/// <remarks>
	/// Pure text to DTO work: no Unity types and no runtime notes. The row semantics are the shipped
	/// importer's, including the parts that look accidental — a second row at the same bar position and
	/// lane overlays the first through <see cref="NoteInfo.Update"/>, guide rows inherit their shape from
	/// the note they decorate, and rows that describe no playable note are dropped silently.
	/// </remarks>
	public sealed class SusParser
	{
		private const int LaneColumn = 13;

		private SusChartData _chart;

		/// <summary>Parses one chart. The returned data is a fresh instance owned by the caller.</summary>
		public SusChartData Parse(string musicScoreData)
		{
			_chart = new SusChartData();
			Load(musicScoreData ?? string.Empty);
			return _chart;
		}

		private void Load(string musicScoreData)
		{
			string[] lines = musicScoreData.Split('\n');
			foreach (string line in lines)
			{
				if (string.IsNullOrWhiteSpace(line))
				{
					continue;
				}
				if (!LeadHeader(line))
				{
					LoadEvent(line);
				}
			}
		}

		// Returns true when the line was fully consumed, so the caller must not read it as a note row.
		private bool LeadHeader(string line)
		{
			if (line.Contains("#REQUEST \"ticks_per_beat"))
			{
				Match match = SusTokens.TicksPerBeatRegex.Match(line.Replace("\"", string.Empty));
				if (match.Success)
				{
					_chart.TicksPerBeat = SusTokens.ParseInt(match.Groups["ticks_per_beat"].Value, _chart.TicksPerBeat);
				}
				return false;
			}
			if (line.Contains("#TIL00"))
			{
				LoadHighSpeedInfo(line);
				return true;
			}
			if (line.Contains("#VOLUME"))
			{
				LoadVolumeInfo(line);
				return true;
			}
			if (line.Contains("#BPM"))
			{
				LoadBpmInfo(line);
				return false;
			}
			return false;
		}

		private void LoadBpmInfo(string lineStr)
		{
			Match match = SusTokens.BpmRegex.Match(lineStr);
			if (match.Success)
			{
				_chart.BpmTable[match.Groups["bpm_id"].Value] = SusTokens.ParseFloat(match.Groups["bpm"].Value, SusChartData.DefaultBpm);
			}
		}

		private void LoadHighSpeedInfo(string lineStr)
		{
			string normalized = lineStr.Replace("\"", string.Empty).Replace("#TIL00:", string.Empty).Replace(",", "\r").Trim();
			foreach (Match match in SusTokens.HighSpeedRegex.Matches(normalized))
			{
				_chart.HighSpeedInfos.Add(new HighSpeedInfo(
					SusTokens.ParseInt(match.Groups["barIndex"].Value, 0),
					SusTokens.ParseInt(match.Groups["tickOffset"].Value, 0),
					SusTokens.ParseFloat(match.Groups["speedRatio"].Value, SusChartData.DefaultSpeedRatio)));
			}
		}

		private void LoadVolumeInfo(string lineStr)
		{
			string normalized = lineStr.Replace("\"", string.Empty).Replace("#VOLUME:", string.Empty).Replace(",", "\r").Trim();
			foreach (Match match in SusTokens.VolumeRegex.Matches(normalized))
			{
				_chart.VolumeInfos.Add(new VolumeInfo(
					SusTokens.ParseInt(match.Groups["barIndex"].Value, 0),
					SusTokens.ParseInt(match.Groups["tickOffset"].Value, 0),
					SusTokens.ParseFloat(match.Groups["volume"].Value, SusChartData.DefaultSeVolume)));
			}
		}

		private void LoadEvent(string lineStr)
		{
			Match longMatch = SusTokens.EventLongNotesRegex.Match(lineStr);
			if (longMatch.Success)
			{
				int bar = SusTokens.ParseInt(longMatch.Groups["bar"].Value, 0);
				int laneType = SusTokens.ParseSusDigit(longMatch.Groups["noteType"].Value[0]);
				int lane = SusTokens.GetNoteLine(longMatch.Groups["lineNo"].Value);
				int parsedLongNo = SusTokens.ParseSusDigit(longMatch.Groups["longNo"].Value[0]);
				SusTokens.ParseNoteValues(longMatch.Groups["data"].Value, out string[] values, out float[] speedRatios);
				CreateNoteBarData(bar, laneType, lane, values, speedRatios, parsedLongNo);
				return;
			}

			Match eventMatch = SusTokens.EventNotesRegex.Match(lineStr);
			if (!eventMatch.Success)
			{
				return;
			}

			int eventBar = SusTokens.ParseInt(eventMatch.Groups["bar"].Value, 0);
			string eventType = eventMatch.Groups["event_type"].Value;
			string data = eventMatch.Groups["data"].Value;
			if (eventType == "02")
			{
				CreateTimeSignaturesEventBarData(eventBar, data);
				return;
			}
			if (eventType == "08")
			{
				SusTokens.ParseNoteValues(data, out string[] bpmValues, out _);
				CreateBpmEventBarData(eventBar, bpmValues);
				return;
			}

			if (eventType.Length < 2)
			{
				return;
			}
			int laneTypeValue = SusTokens.ParseSusDigit(eventType[0]);
			int laneValue = SusTokens.GetNoteLine(eventType[1].ToString());
			SusTokens.ParseNoteValues(data, out string[] noteValues, out float[] noteSpeedRatios);
			CreateNoteBarData(eventBar, laneTypeValue, laneValue, noteValues, noteSpeedRatios);
		}

		private void CreateTimeSignaturesEventBarData(int bar, string data)
		{
			if (string.IsNullOrWhiteSpace(data))
			{
				return;
			}
			_chart.TimeSignatures.Add(new TimeSignatureInfo(SusTokens.ParseFloat(data, SusChartData.DefaultTimeSignature), bar));
		}

		private void CreateBpmEventBarData(int bar, string[] values)
		{
			if (values == null || values.Length == 0)
			{
				return;
			}
			for (int i = 0; i < values.Length; i++)
			{
				string value = values[i];
				if (value == "00")
				{
					continue;
				}
				if (_chart.BpmTable.TryGetValue(value.ToUpperInvariant(), out float bpm) || _chart.BpmTable.TryGetValue(value, out bpm))
				{
					_chart.BpmInfos.Add(new BpmInfo(bpm, bar, i / (float)values.Length));
				}
			}
		}

		// Expands one note data row; lane and width are checked against the 13-column playfield.
		private void CreateNoteBarData(int bar, int laneType, int lane, string[] values, float[] speedRatios, int longNo = -1)
		{
			if (values == null || values.Length == 0)
			{
				return;
			}
			for (int i = 0; i < values.Length; i++)
			{
				string value = values[i];
				if (string.IsNullOrEmpty(value) || value == "00")
				{
					continue;
				}
				int noteCode = SusTokens.ParseSusDigit(value[0]);
				int width = value.Length > 1 ? SusTokens.GetNoteWidth(value[1].ToString()) : -1;
				float barProgress = i / (float)values.Length;
				float speedRatio = speedRatios != null && i < speedRatios.Length ? speedRatios[i] : SusChartData.DefaultSpeedRatio;

				if (lane == LaneColumn && laneType == 1)
				{
					if (noteCode == 1)
					{
						AddEventInfo(bar, barProgress, EventType.FeverBegin);
					}
					else if (noteCode == 2)
					{
						AddEventInfo(bar, barProgress, EventType.FeverStart);
					}
					continue;
				}

				if (longNo == -1 && laneType == 1)
				{
					AddPlainNote(bar, barProgress, lane, width, noteCode, speedRatio);
				}
				else if (longNo == -1 && laneType == 5)
				{
					AddFlickNote(bar, barProgress, lane, width, noteCode, speedRatio);
				}
				else if (longNo != -1 && laneType == 3)
				{
					AddLongNote(bar, barProgress, lane, width, noteCode, speedRatio, longNo);
				}
				else if (longNo != -1 && laneType == 9)
				{
					AddGuideNote(bar, barProgress, lane, width, noteCode, speedRatio, longNo);
				}
			}
		}

		/// <summary>Lane type 1 without a long number: taps, criticals, skips, friction tap variants.</summary>
		private void AddPlainNote(int bar, float barProgress, int lane, int width, int noteCode, float speedRatio)
		{
			switch (noteCode)
			{
			case 1:
				AddNoteInfo(bar, barProgress, lane, width, NoteCategory.Normal, speedRatio);
				break;
			case 2:
				AddNoteInfo(bar, barProgress, lane, width, NoteCategory.Normal, speedRatio, NoteType.Critical);
				break;
			case 3:
				AddNoteInfo(bar, barProgress, lane, width, NoteCategory.Skip, speedRatio);
				break;
			case 4:
				AddEventInfo(bar, barProgress, EventType.Skill);
				break;
			case 5:
				AddNoteInfo(bar, barProgress, lane, width, NoteCategory.Friction, speedRatio);
				break;
			case 6:
				AddNoteInfo(bar, barProgress, lane, width, NoteCategory.Friction, speedRatio, NoteType.Critical);
				break;
			case 7:
				AddNoteInfo(bar, barProgress, lane, width, NoteCategory.FrictionHide, speedRatio);
				break;
			case 8:
				AddNoteInfo(bar, barProgress, lane, width, NoteCategory.FrictionHide, speedRatio, NoteType.Critical);
				break;
			}
		}

		/// <summary>Lane type 5 without a long number: flicks and eased taps.</summary>
		private void AddFlickNote(int bar, float barProgress, int lane, int width, int noteCode, float speedRatio)
		{
			switch (noteCode)
			{
			case 1:
				AddNoteInfo(bar, barProgress, lane, width, NoteCategory.Flick, speedRatio);
				break;
			case 2:
				AddNoteInfo(bar, barProgress, lane, width, NoteCategory.Normal, speedRatio, lineType: NoteLineType.EaseIn);
				break;
			case 3:
				AddNoteInfo(bar, barProgress, lane, width, NoteCategory.Flick, speedRatio, direction: NoteDirection.Left);
				break;
			case 4:
				AddNoteInfo(bar, barProgress, lane, width, NoteCategory.Flick, speedRatio, direction: NoteDirection.Right);
				break;
			case 5:
			case 6:
				AddNoteInfo(bar, barProgress, lane, width, NoteCategory.Normal, speedRatio, lineType: NoteLineType.EaseOut);
				break;
			}
		}

		/// <summary>Lane type 3 with a long number: the head, body, connection and hidden parts of a hold.</summary>
		private void AddLongNote(int bar, float barProgress, int lane, int width, int noteCode, float speedRatio, int longNo)
		{
			switch (noteCode)
			{
			case 1:
				AddNoteInfo(bar, barProgress, lane, width, NoteCategory.Long, speedRatio, longNo: longNo);
				break;
			case 2:
				AddNoteInfo(bar, barProgress, lane, width, NoteCategory.Normal, speedRatio, longNo: longNo);
				break;
			case 3:
				AddNoteInfo(bar, barProgress, lane, width, NoteCategory.Connection, speedRatio, longNo: longNo);
				break;
			case 5:
				AddNoteInfo(bar, barProgress, lane, width, NoteCategory.Hidden, speedRatio, longNo: longNo);
				break;
			}
		}

		/// <summary>Lane type 9 with a long number: the guide carrier chain.</summary>
		private void AddGuideNote(int bar, float barProgress, int lane, int width, int noteCode, float speedRatio, int longNo)
		{
			switch (noteCode)
			{
			case 1:
				AddNoteInfo(bar, barProgress, lane, width, NoteCategory.Guide, speedRatio, longNo: longNo);
				break;
			case 2:
				AddNoteInfo(bar, barProgress, lane, width, NoteCategory.GuideEnd, speedRatio, longNo: longNo);
				break;
			case 3:
			case 5:
				AddNoteInfo(bar, barProgress, lane, width, NoteCategory.GuideHidden, speedRatio, longNo: longNo);
				break;
			}
		}

		private void AddEventInfo(int bar, float barProgress, EventType eventType)
		{
			float key = bar + barProgress;
			if (!_chart.EventInfos.TryGetValue(key, out List<EventInfo> infos))
			{
				infos = new List<EventInfo>();
				_chart.EventInfos[key] = infos;
			}
			infos.Add(new EventInfo(bar, barProgress, eventType));
		}

		private void AddNoteInfo(int bar, float barProgress, int lane, int width, NoteCategory category, float speedRatio, NoteType type = NoteType.Default, NoteDirection direction = NoteDirection.Default, NoteLineType lineType = NoteLineType.Linear, int longNo = -1)
		{
			if (width == -1 || lane < 0 || width + lane >= LaneColumn)
			{
				return;
			}
			if (IsGuideCategory(category))
			{
				AddGuideInfo(bar, barProgress, lane, width, category, speedRatio, type, direction, lineType, longNo);
			}
			else
			{
				AddNoteInfoToDictionary(_chart.NoteInfos, bar, barProgress, lane, width, category, speedRatio, type, direction, lineType, longNo);
				UpdateGuideFromNormalNote(bar, barProgress, lane, category, type, direction, lineType, speedRatio, longNo);
			}
		}

		/// <summary>Guide rows inherit type and direction from the note row at the same key.</summary>
		private void AddGuideInfo(int bar, float barProgress, int lane, int width, NoteCategory category, float speedRatio, NoteType type = NoteType.Default, NoteDirection direction = NoteDirection.Default, NoteLineType lineType = NoteLineType.Linear, int longNo = -1)
		{
			NoteInfo inherited = FindNoteInfo(_chart.NoteInfos, bar + barProgress, lane);
			if (inherited != null)
			{
				if (type == NoteType.Default)
				{
					type = inherited.Type;
				}
				if (direction == NoteDirection.Default)
				{
					direction = inherited.Direction;
				}
				if (lineType == NoteLineType.Linear)
				{
					lineType = inherited.LineType;
				}
			}
			AddNoteInfoToDictionary(_chart.GuideInfos, bar, barProgress, lane, width, category, speedRatio, type, direction, lineType, longNo);
		}

		private void UpdateGuideFromNormalNote(int bar, float barProgress, int lane, NoteCategory category, NoteType type, NoteDirection direction, NoteLineType lineType, float speedRatio, int longNoValue)
		{
			NoteInfo guideInfo = FindNoteInfo(_chart.GuideInfos, bar + barProgress, lane);
			guideInfo?.Update(category, type, direction, lineType, speedRatio, longNoValue);
		}

		private static void AddNoteInfoToDictionary(Dictionary<float, Dictionary<int, NoteInfo>> target, int bar, float barProgress, int lane, int width, NoteCategory category, float speedRatio, NoteType type, NoteDirection direction, NoteLineType lineType, int longNoValue)
		{
			float key = bar + barProgress;
			if (!target.TryGetValue(key, out Dictionary<int, NoteInfo> laneDict))
			{
				laneDict = new Dictionary<int, NoteInfo>();
				target[key] = laneDict;
			}
			if (laneDict.TryGetValue(lane, out NoteInfo noteInfo))
			{
				noteInfo.Update(category, type, direction, lineType, speedRatio, longNoValue);
			}
			else
			{
				laneDict[lane] = new NoteInfo(bar, barProgress, lane, width, category, type, direction, lineType, speedRatio, longNoValue);
			}
		}

		private static NoteInfo FindNoteInfo(Dictionary<float, Dictionary<int, NoteInfo>> source, float key, int lane)
		{
			return source.TryGetValue(key, out Dictionary<int, NoteInfo> laneDict) && laneDict.TryGetValue(lane, out NoteInfo noteInfo) ? noteInfo : null;
		}

		private static bool IsGuideCategory(NoteCategory category)
		{
			return category == NoteCategory.Guide || category == NoteCategory.GuideEnd || category == NoteCategory.GuideHidden;
		}
	}
}
