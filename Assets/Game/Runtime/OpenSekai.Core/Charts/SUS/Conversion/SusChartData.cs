using System.Collections.Generic;
using Sekai.SUS;

namespace OpenSekai.Shared.Conversion
{
	/// <summary>
	/// The parsed form of a SUS chart: the authoring rows resolved into plain DTO tables.
	/// </summary>
	/// <remarks>
	/// This is the boundary between the SUS text format and everything else. It holds no runtime note
	/// objects and no Unity types, so it can be produced and inspected without an editor or a scene.
	/// The tables keep the importer's original shapes on purpose: notes are keyed by
	/// <c>bar + barProgress</c> and then by lane so a later row can overlay an earlier one, which is how
	/// the official charts mark relay points and friction variants.
	/// </remarks>
	public sealed class SusChartData
	{
		public const float DefaultBpm = 120f;

		public const float DefaultTimeSignature = 4f;

		public const float DefaultSpeedRatio = 1f;

		public const float DefaultSeVolume = 1f;

		public const int DefaultTicksPerBeat = 480;

		/// <summary>Ticks per beat from <c>#REQUEST "ticks_per_beat"</c>; tick offsets are read against it.</summary>
		public int TicksPerBeat;

		/// <summary>Time signature rows, in the order the chart declares them.</summary>
		public List<TimeSignatureInfo> TimeSignatures;

		/// <summary>Named BPM definitions from <c>#BPMxx</c> rows, keyed by the two-character id.</summary>
		public Dictionary<string, float> BpmTable;

		/// <summary>BPM changes resolved against <see cref="BpmTable"/>.</summary>
		public List<BpmInfo> BpmInfos;

		/// <summary>Scroll-speed rows from <c>#TIL00</c>.</summary>
		public List<HighSpeedInfo> HighSpeedInfos;

		/// <summary>SE volume rows from <c>#VOLUME</c>.</summary>
		public List<VolumeInfo> VolumeInfos;

		/// <summary>Judged notes, keyed by bar position and then lane.</summary>
		public Dictionary<float, Dictionary<int, NoteInfo>> NoteInfos;

		/// <summary>Guide (decorative carrier) notes, keyed by bar position and then lane.</summary>
		public Dictionary<float, Dictionary<int, NoteInfo>> GuideInfos;

		/// <summary>Skill and fever rows, keyed by bar position.</summary>
		public Dictionary<float, List<EventInfo>> EventInfos;

		public SusChartData()
		{
			Reset();
		}

		public void Reset()
		{
			TicksPerBeat = DefaultTicksPerBeat;
			TimeSignatures = new List<TimeSignatureInfo>();
			BpmTable = new Dictionary<string, float>();
			BpmInfos = new List<BpmInfo>();
			HighSpeedInfos = new List<HighSpeedInfo>();
			VolumeInfos = new List<VolumeInfo>();
			NoteInfos = new Dictionary<float, Dictionary<int, NoteInfo>>();
			GuideInfos = new Dictionary<float, Dictionary<int, NoteInfo>>();
			EventInfos = new Dictionary<float, List<EventInfo>>();
		}
	}
}
