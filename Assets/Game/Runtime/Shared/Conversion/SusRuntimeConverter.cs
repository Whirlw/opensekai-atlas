using System.Collections.Generic;
using System.Linq;
using Sekai.Live;

namespace OpenSekai.Shared.Conversion
{
	/// <summary>
	/// The entry point for turning a SUS chart into a playable <see cref="MusicScore"/>.
	/// </summary>
	/// <remarks>
	/// The importer is a pipeline of four stages, and this class only wires them in order:
	/// <see cref="SusParser"/> (text to DTOs, in Core), <see cref="SusTimingConverter"/> (DTOs to frames,
	/// in Core), <see cref="SusNoteConverter"/> (DTOs to runtime notes) and
	/// <see cref="SusEventConverter"/> (DTOs to runtime events). The public surface is unchanged, so
	/// callers keep constructing this type and calling <see cref="Convert"/>.
	/// The stage order is load-bearing: the frames exist before any note is placed, the notes exist
	/// before the fever events count them, and the score starts with a fresh <see cref="MusicScore"/>
	/// because its constructor seeds the shared current frame.
	/// </remarks>
	public class SusRuntimeConverter
	{
		public MusicScore Convert(string musicScoreData, LiveBundleBuildData config, bool isNeedCombo = true, bool _isMirror = false, bool showPair = true)
		{
			MusicScore musicScore = new MusicScore();
			SusChartData chart = new SusParser().Parse(musicScoreData ?? string.Empty);
			MusicScoreInfo[] frames = SusTimingConverter.Convert(chart);
			musicScore.SetMusicScoreInfo(frames);

			List<NoteBase> notes = new SusNoteConverter().Convert(chart, frames, config, isNeedCombo, _isMirror, showPair);
			musicScore.AddNoteArray(notes.ToArray());

			List<EventBase> events = new SusEventConverter().Convert(chart.EventInfos, notes, frames);
			musicScore.AddEventArray(events.ToArray());
			return musicScore;
		}
	}
}
