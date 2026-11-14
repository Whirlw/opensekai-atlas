using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace OpenSekai.Shared.Conversion
{
	/// <summary>
	/// The lexemes of the SUS text format: row patterns, digit/width tables and number parsing.
	/// </summary>
	/// <remarks>
	/// Kept separate from <see cref="SusParser"/> so the chart-level reader stays about rows and tables
	/// while the fiddly per-character rules live in one place. Everything here is a pure string
	/// transform.
	/// </remarks>
	internal static class SusTokens
	{
		private const string FloatPattern = @"[-+]?(?:\d+(?:\.\d*)?|\.\d+)";

		internal static readonly Regex EventNotesRegex =
			new Regex("^#(?<bar>\\d{3})(?<event_type>[0-9a-z]{2}):(?<data>.+)", RegexOptions.Compiled);

		internal static readonly Regex EventLongNotesRegex =
			new Regex("^#(?<bar>\\d{3})(?<noteType>\\d{1})(?<lineNo>[0-9a-z]{1})(?<longNo>[0-9a-z]{1}):(?<data>.+)", RegexOptions.Compiled);

		internal static readonly Regex BpmRegex =
			new Regex("^#BPM(?<bpm_id>[0-9A-Z]{2}):(?<bpm>.+)", RegexOptions.Compiled);

		internal static readonly Regex VolumeRegex =
			new Regex($@"(?<barIndex>\d+)'(?<tickOffset>\d+):(?<volume>{FloatPattern})(?:\r|$)", RegexOptions.Compiled);

		internal static readonly Regex TicksPerBeatRegex =
			new Regex("^#REQUEST ticks_per_beat (?<ticks_per_beat>\\d+)", RegexOptions.Compiled);

		internal static readonly Regex HighSpeedRegex =
			new Regex($@"(?<barIndex>\d+)'(?<tickOffset>\d+):(?<speedRatio>{FloatPattern})(?:\r|$)", RegexOptions.Compiled);

		/// <summary>Lane of the lane column character; <c>f</c> is the effect column, <c>e</c> is unused.</summary>
		internal static int GetNoteLine(string lineNo)
		{
			return lineNo switch
			{
				"2" => 0,
				"3" => 1,
				"4" => 2,
				"5" => 3,
				"6" => 4,
				"7" => 5,
				"8" => 6,
				"9" => 7,
				"a" => 8,
				"b" => 9,
				"c" => 10,
				"d" => 11,
				"f" => 13,
				"e" => -2,
				_ => -1
			};
		}

		/// <summary>Lane width of the width column character; -1 means the note is dropped.</summary>
		internal static int GetNoteWidth(string noteWidth)
		{
			return noteWidth switch
			{
				"1" => 1,
				"2" => 2,
				"3" => 3,
				"4" => 4,
				"5" => 5,
				"6" => 6,
				"7" => 7,
				"8" => 8,
				"9" => 9,
				"a" => 10,
				"b" => 11,
				"c" => 12,
				"d" => 13,
				"e" => 14,
				_ => -1
			};
		}

		/// <summary>
		/// Splits a note data row into two-character note codes and the optional per-note speed ratio
		/// that follows a code as <c>,1.5</c>.
		/// </summary>
		internal static void ParseNoteValues(string data, out string[] values, out float[] speedRatios)
		{
			List<string> valueList = new List<string>();
			List<float> speedList = new List<float>();
			if (string.IsNullOrEmpty(data))
			{
				values = Array.Empty<string>();
				speedRatios = Array.Empty<float>();
				return;
			}

			int i = 0;
			while (i < data.Length)
			{
				while (i < data.Length && char.IsWhiteSpace(data[i]))
				{
					i++;
				}
				if (i + 1 >= data.Length || !IsSusChar(data[i]) || !IsSusChar(data[i + 1]))
				{
					i++;
					continue;
				}

				valueList.Add(data.Substring(i, 2).ToLowerInvariant());
				i += 2;
				float speedRatio = SusChartData.DefaultSpeedRatio;
				if (i < data.Length && data[i] == ',')
				{
					i++;
					int start = i;
					while (i < data.Length && !char.IsWhiteSpace(data[i]))
					{
						i++;
					}
					speedRatio = ParseFloat(data.Substring(start, i - start), SusChartData.DefaultSpeedRatio);
				}
				speedList.Add(speedRatio);
			}

			values = valueList.ToArray();
			speedRatios = speedList.ToArray();
		}

		internal static bool IsSusChar(char value)
		{
			value = char.ToLowerInvariant(value);
			return (value >= '0' && value <= '9') || (value >= 'a' && value <= 'z');
		}

		/// <summary>Single SUS digit: <c>0-9</c> then <c>a-z</c> continue at 10.</summary>
		internal static int ParseSusDigit(char value)
		{
			value = char.ToLowerInvariant(value);
			if (value >= '0' && value <= '9')
			{
				return value - '0';
			}
			if (value >= 'a' && value <= 'z')
			{
				return value - 'a' + 10;
			}
			return -1;
		}

		internal static int ParseInt(string value, int defaultValue)
		{
			return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : defaultValue;
		}

		internal static float ParseFloat(string value, float defaultValue)
		{
			value = value?.Trim();
			return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ? result : defaultValue;
		}
	}
}
