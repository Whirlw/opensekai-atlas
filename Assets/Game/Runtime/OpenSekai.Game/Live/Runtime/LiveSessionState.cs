using System;
using System.Linq;
using Sekai.Live;

namespace Sekai.Core.Live
{
	internal sealed class LiveSessionState
	{
		private double _endExclusive = double.PositiveInfinity;
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
			Notes.BindNativeVisuals(NoteDisplayTimeOffset);
			_events = SortEvents(MusicScore.EventArray);
		}

		internal void Reset(LivePresentationBridge presentation)
		{
			// Notes.Reset clears the cursors AND the per-frame tick lists (open heads, open end notes).
			// Setting only the two cursors left a retry ticking notes from the take that had just ended.
			Notes.Reset();
			_timeline.Reset();
			_endExclusive = double.PositiveInfinity;
			TapCount = 0;
			presentation.Reset();
			MusicScore?.ResetNote();
			MusicScore?.ResetEvent();
		}

		internal void SelectPracticeRange(float chartSeconds, double endChartSeconds)
		{
			_endExclusive = endChartSeconds;
			_timeline.SkipEventsBefore(_events, chartSeconds);
			foreach (NoteBase note in Notes.Notes)
			{
				if (note.MusicScoreInfo.time >= endChartSeconds)
				{
					// A future note's early window must not take an in-segment press.
					note.SetStateUnnotice(NoteState.Done);
					continue;
				}

				if (note is LongNote clipped && clipped.NoteList != null)
				{
					foreach (NoteBase part in clipped.NoteList)
						if (part.MusicScoreInfo.time >= endChartSeconds) part.ClaimPublication();
				}
				if (note.MusicScoreInfo.time >= chartSeconds) continue;
				if (note is LongNote bar && bar.TailNote != null && bar.TailNote.MusicScoreInfo.time >= chartSeconds)
				{
					// A held note may cross the cut. Keep its body and future ticks playable,
					// without demanding a press at a head that occurred before this take.
					bar.Head.SkipForPractice();
					foreach (NoteBase child in bar.NoteList)
						if (child != bar && child.MusicScoreInfo.time < chartSeconds)
							child.SetStateUnnotice(NoteState.Done);
				}
				else
				{
					foreach (NoteBase child in note.NoteList)
						child.SetStateUnnotice(NoteState.Done);
				}
			}
		}

		internal void UpdateEvents(MusicScoreInfo frame) => _timeline.AdvanceEvents(frame, _events, ExecuteEvent, _endExclusive);

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
