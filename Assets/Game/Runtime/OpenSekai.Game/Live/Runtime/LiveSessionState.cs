using System;
using System.Linq;
using Sekai.Live;

namespace Sekai.Core.Live
{
	internal sealed class LiveSessionState
	{
		private EventBase[] _events = Array.Empty<EventBase>();
		private readonly LiveTimeline _timeline = new LiveTimeline();

		internal LiveBootDataBase BootData { get; private set; }
		internal LiveViewBase[] Views { get; set; }
		internal MusicScore MusicScore { get; set; }
		internal LiveNoteSession Notes { get; } = new LiveNoteSession();
		internal ScoreLogic ScoreLogic { get; set; }
		internal SkillLogic SkillLogic { get; set; } = new NullSkillLogic();
		internal float NoteDisplayTimeOffset { get; set; }
		internal int TapCount { get; set; }

		/// <summary>
		/// True when nobody is playing: the session answers every note itself. The frame loop needs it
		/// because its own walk turns anything past its window into a Miss, which must not outrun the
		/// auto judgement pass.
		/// </summary>
		internal bool IsAuto { get; set; }

		internal LiveScore Score => ScoreLogic?.Score ?? default;

		internal bool AreNotesFinished =>
			Notes.NoteIndex >= Notes.Notes.Length && Notes.HighSpeedIndex >= Notes.HighSpeedNotes.Length
			&& Notes.PendingHeads.Count == 0 && Notes.PendingTailCount == 0;

		internal void Bind(LiveBootDataBase bootData, LiveViewBase[] views)
		{
			BootData = bootData;
			IsAuto = bootData?.IsAuto == true;
			Views = views;
			ApplicationLocalSettings.LoadFromStorage();
			NoteDisplayTimeOffset = LiveConfig.GetNoteDisplayOffsetTime(bootData?.LiveSettingData?.NoteSpeed ?? 6f);
			MusicScore = bootData?.MusicData?.MusicScore ?? new MusicScore();
			if (bootData?.MusicData != null)
			{
				bootData.MusicData.MusicScore = MusicScore;
			}

			MusicScore.InjectSkillFeverForCreatorScore(bootData?.MusicData?.Vocal?.musicId ?? bootData?.MusicData?.Music?.id ?? 0);
			Notes.Bind(SortNotes(MusicScore.NoteArray));
			_events = SortEvents(MusicScore.EventArray);
		}

		internal void Reset(LivePresentationBridge presentation)
		{
			// Notes.Reset clears the cursors AND the per-frame tick lists (open heads, open end notes).
			// Setting only the two cursors left a retry ticking notes from the take that had just ended.
			Notes.Reset();
			_timeline.Reset();
			TapCount = 0;
			presentation.Reset();
			MusicScore?.ResetNote();
			MusicScore?.ResetEvent();
		}

		internal void UpdateEvents(MusicScoreInfo frame) => _timeline.AdvanceEvents(frame, _events, ExecuteEvent);

		internal void AdvanceAutoJudgment(MusicScoreInfo frame, bool skipDecoration = false) =>
			_timeline.AdvanceAutoJudgment(frame, Notes, skipDecoration);

		private void ExecuteEvent(EventBase eventBase)
		{
			ScoreLogic?.ExecuteEvent(eventBase);
			SkillLogic?.ExecuteEvent(eventBase);
		}

		private static NoteBase[] SortNotes(NoteBase[] notes)
		{
			return notes == null
				? Array.Empty<NoteBase>()
				: notes.Where(note => note != null).OrderBy(note => note.MusicScoreInfo.time).ToArray();
		}

		private static EventBase[] SortEvents(EventBase[] events)
		{
			return events == null
				? Array.Empty<EventBase>()
				: events.Where(eventBase => eventBase != null).OrderBy(eventBase => eventBase.MusicScoreInfo.time).ToArray();
		}
	}
}
