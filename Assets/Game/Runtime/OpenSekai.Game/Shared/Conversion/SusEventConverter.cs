using System.Collections.Generic;
using System.Linq;
using Sekai.Live;
using Sekai.SUS;
using EventType = Sekai.SUS.EventType;

namespace OpenSekai.Shared.Conversion
{
	/// <summary>
	/// Builds the runtime skill and fever events of a chart from the parsed SUS event rows.
	/// </summary>
	/// <remarks>
	/// The fever pair is stateful: <c>FeverBegin</c> stays open until the matching <c>FeverStart</c>
	/// arrives and is then told how many judged notes fall inside the fever, which is why this stage
	/// runs after the notes are built. Order is bar position, then bar progress, and the result is
	/// finally sorted by time — the same sequence the shipped importer published.
	/// </remarks>
	public sealed class SusEventConverter
	{
		private List<EventBase> _events;
		private IReadOnlyList<NoteBase> _notes;
		private MusicScoreInfo[] _frames;
		private FeverBeginEvent _feverBegin;

		/// <summary>Converts the event rows into runtime events sorted by time.</summary>
		public List<EventBase> Convert(Dictionary<float, List<EventInfo>> eventInfos, IReadOnlyList<NoteBase> notes, MusicScoreInfo[] frames)
		{
			_events = new List<EventBase>();
			_notes = notes;
			_frames = frames;
			_feverBegin = null;

			foreach (float key in eventInfos.Keys.OrderBy(value => value))
			{
				foreach (EventInfo eventInfo in eventInfos[key].OrderBy(value => value.BarProgress))
				{
					MusicScoreInfo info = MusicScore.GenerateNoteMusicScoreInfo(eventInfo.Bar, eventInfo.BarProgress, _frames);
					switch (eventInfo.EventType)
					{
					case EventType.Skill:
						_events.Add(new SkillEvent(info));
						break;
					case EventType.FeverBegin:
						_feverBegin = new FeverBeginEvent(info);
						_events.Add(_feverBegin);
						break;
					case EventType.FeverStart:
						if (_feverBegin != null)
						{
							_feverBegin.Setup(CountJudgmentNotesBetween(_feverBegin.MusicScoreInfo.time, info.time));
							_feverBegin = null;
						}
						_events.Add(new FeverStartEvent(info));
						break;
					}
				}
			}
			_events.Sort((left, right) => left.MusicScoreInfo.time.CompareTo(right.MusicScoreInfo.time));
			return _events;
		}

		private int CountJudgmentNotesBetween(float startTime, float endTime)
		{
			int count = 0;
			if (_notes == null)
			{
				return count;
			}
			foreach (NoteBase root in _notes)
			{
				if (root?.NoteList == null)
				{
					continue;
				}
				foreach (NoteBase note in root.NoteList)
				{
					if (note != null && note.HasJudgment && note.MusicScoreInfo.time >= startTime && note.MusicScoreInfo.time < endTime)
					{
						count++;
					}
				}
			}
			return count;
		}
	}
}
