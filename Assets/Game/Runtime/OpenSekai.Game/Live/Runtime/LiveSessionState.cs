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

		private float _maxDisplayLead;
		private float _maxDisplayLeadForOffset = float.NaN;

		/// <summary>
		/// How far ahead of the playhead a note can ever be drawn, in seconds of music time. The scan is a
		/// time window of this width: notes are sorted by time, so the frame loop can read everything inside
		/// the window instead of walking until a note's own lead says stop - a lead that collapses (the 325x
		/// sections of the shipped chart give 0.0018 s) would otherwise park the cursor and starve the notes
		/// behind it, which is what created a dozen notes in one frame part way down the screen.
		///
		/// Recomputes whenever the display window changes, which is the only input that scales it.
		/// </summary>
		internal float MaxDisplayLead
		{
			get
			{
				if (_maxDisplayLead > 0f && _maxDisplayLeadForOffset.Equals(NoteDisplayTimeOffset))
				{
					return _maxDisplayLead;
				}

				_maxDisplayLeadForOffset = NoteDisplayTimeOffset;
				_maxDisplayLead = ComputeMaxDisplayLead(NoteDisplayTimeOffset, MusicScore, Notes.Notes);
				return _maxDisplayLead;
			}
		}

		/// <summary>
		/// A note is drawn over <c>window / speed</c> seconds, so the widest window any note needs is the
		/// window divided by the slowest speed the chart uses. Sign flips can cancel the average and ask for
		/// more than that, so the answer is capped: a lead beyond half a minute is a chart that is not
		/// drawing anything anyway.
		/// </summary>
		private static float ComputeMaxDisplayLead(float window, MusicScore score, NoteBase[] notes)
		{
			float minSpeed = float.MaxValue;
			MusicScoreInfo[] infos = score?.musicScoreInfoArray;
			if (infos != null)
			{
				foreach (MusicScoreInfo info in infos)
				{
					float speed = Math.Abs(info.speedRatio);
					if (speed > 1e-4f)
					{
						minSpeed = Math.Min(minSpeed, speed);
					}
				}
			}

			if (notes != null)
			{
				foreach (NoteBase note in notes)
				{
					if (note == null)
					{
						continue;
					}

					float speed = Math.Abs(note.speedRatio);
					if (speed > 1e-4f)
					{
						minSpeed = Math.Min(minSpeed, speed);
					}
				}
			}

			if (window <= 0f || minSpeed == float.MaxValue)
			{
				return Math.Max(window, 0.5f);
			}

			return Math.Min(Math.Max(window / minSpeed, window), 30f);
		}

		internal int TapCount { get; set; }

		internal LiveScore Score => ScoreLogic?.Score ?? default;

		internal bool AreNotesFinished =>
			Notes.NoteIndex >= Notes.Notes.Length && Notes.HighSpeedIndex >= Notes.HighSpeedNotes.Length;

		internal void Bind(LiveBootDataBase bootData, LiveViewBase[] views)
		{
			BootData = bootData;
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
			Notes.NoteIndex = 0;
			Notes.HighSpeedIndex = 0;
			_timeline.Reset();
			TapCount = 0;
			presentation.Reset();
			MusicScore?.ResetNote();
			MusicScore?.ResetEvent();
		}

		internal void UpdateEvents(MusicScoreInfo frame) => _timeline.AdvanceEvents(frame, _events, ExecuteEvent);

		internal void AdvanceAutoJudgment(MusicScoreInfo frame) => _timeline.AdvanceAutoJudgment(frame, Notes);

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
