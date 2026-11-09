using System;
using System.Collections.Generic;
using Sekai;
using Sekai.Live;
using OpenSekai.Charts;
using System.IO;
using System.Linq;
namespace OpenSekai.Live.Conversion
{
    /// <summary>Instantiates fresh live notes from chart data. No editor model or global settings reads.</summary>
    public sealed class RuntimeChartConverter
    {
        private readonly ChartDocument _document;
        private readonly bool _showPair;
        private int _idCount;
        private RuntimeChartConverter(ChartDocument _document, bool _showPair)
        {
            this._document = _document; this._showPair = _showPair;
            foreach (var note in _document.NoteList ?? new List<ChartNote>()) if (note != null) _idCount = Math.Max(_idCount,note.id);
            foreach (var e in _document.MusicScoreEventDataList ?? new List<ChartEvent>()) if (e != null) _idCount = Math.Max(_idCount,e.id);
        }
        public static MusicScore Convert(ChartDocument _document, LiveBundleBuildData config,
            bool _showPair = true, bool isMirror = false, long? filterBeforeTicks = null)
        {
            if (_document == null) throw new ArgumentNullException(nameof(_document));
            ValidateConnections(_document.NoteList);
            return new RuntimeChartConverter(_document,_showPair).ConvertCore(config,filterBeforeTicks,isMirror);
        }
        private static Dictionary<int,ChartNote> BuildNoteLookup(List<ChartNote> notes)
        {
            var lookup = new Dictionary<int,ChartNote>();
            if(notes != null) foreach(var note in notes) if(note != null) lookup[note.id] = note;
            return lookup;
        }
        private static void ValidateConnections(List<ChartNote> notes)
        {
            var lookup = BuildNoteLookup(notes);
            foreach (bool backwards in new[] { false, true })
            {
                var complete = new HashSet<int>();
                foreach(var start in lookup.Values)
                {
                    var chain = new HashSet<int>(); var note = start;
                    while(note != null && !complete.Contains(note.id))
                    {
                        if(!chain.Add(note.id)) throw new InvalidDataException("谱面音符连接存在循环。");
                        int next = backwards ? note.previousConnectionId : note.nextConnectionId;
                        if(next == -1 || !lookup.TryGetValue(next,out note)) break;
                    }
                    complete.UnionWith(chain);
                }
            }
        }
		private MusicScore ConvertCore(LiveBundleBuildData liveBundleBuildData, long? filterBeforeTicks, bool isMirror)
		{
			ChartDocument conversionData = _document.Copy();
			conversionData.MusicScoreEventDataList ??= new List<ChartEvent>();
			ChartEvent existingTimeSignature = conversionData.MusicScoreEventDataList.Find(e => e != null && e.eventType == ChartEventType.TimeSignature);
			int timeSignatureId = existingTimeSignature != null ? existingTimeSignature.id : ++_idCount;
			conversionData.MusicScoreEventDataList.RemoveAll(e => e != null && e.eventType == ChartEventType.TimeSignature);
			conversionData.MusicScoreEventDataList.Add(new ChartEvent
			{
				id = timeSignatureId,
				eventType = ChartEventType.TimeSignature,
				ticks = 0L,
				changeValue = "4/4"
			});
			MusicScoreInfo[] musicScoreInfos = ChartTimeline.ConvertMusicScoreInfo(conversionData.MusicScoreEventDataList);
			NoteBase[] noteArray = ConvertNoteArray(conversionData.NoteList, musicScoreInfos, liveBundleBuildData, conversionData, filterBeforeTicks);
			EventBase[] eventArray = FilterEventArray(conversionData.EventArray, filterBeforeTicks, musicScoreInfos);
			MusicScore musicScore = new MusicScore(musicScoreInfos, eventArray, noteArray);
			if (filterBeforeTicks.HasValue && filterBeforeTicks.Value >= 1L)
			{
				musicScore.FilterBeforeTimeSec = ChartTimeline.GetTimeFromTicks(filterBeforeTicks.Value, musicScoreInfos);
			}
			if (isMirror)
			{
				ApplyMirrorToNoteArray(noteArray);
			}
			return musicScore;
		}

		private static void ApplyMirrorToNoteArray(NoteBase[] noteArray)
		{
			if (noteArray == null)
			{
				return;
			}
			foreach (NoteBase rootNote in noteArray)
			{
				if (rootNote?.NoteList == null)
				{
					continue;
				}
				foreach (NoteBase note in rootNote.NoteList)
				{
					ApplyMirrorToNote(note);
				}
			}
		}

		private static void ApplyMirrorToNote(NoteBase note)
		{
			if (note == null)
			{
				return;
			}
			int defaultLeftLane = note.DefaultLeftLane;
			int defaultRightLane = note.DefaultRightLane;
			note.DefaultLeftLane = 11 - defaultRightLane;
			note.DefaultRightLane = 11 - defaultLeftLane;
			int laneStart = note.LaneStart;
			int laneEnd = note.LaneEnd;
			note.LaneStart = 11 - laneEnd;
			note.LaneEnd = 11 - laneStart;
			float laneStartF = note.LaneStartF;
			float laneEndF = note.LaneEndF;
			note.LaneStartF = 11 - laneEndF;
			note.LaneEndF = 11 - laneStartF;
			float judgeLaneStart = note.JudgeLaneStart;
			float judgeLaneEnd = note.JudgeLaneEnd;
			note.JudgeLaneStart = 11 - judgeLaneEnd;
			note.JudgeLaneEnd = 11 - judgeLaneStart;
			if (note.Direction == NoteDirection.Left)
			{
				note.Direction = NoteDirection.Right;
			}
			else if (note.Direction == NoteDirection.Right)
			{
				note.Direction = NoteDirection.Left;
			}
		}

		private NoteBase[] ConvertNoteArray(List<ChartNote> noteArray, MusicScoreInfo[] eventBaseArray, LiveBundleBuildData liveBundleBuildData, ChartDocument conversionData, long? filterBeforeTicks = null)
		{
			if (noteArray == null)
			{
				return Array.Empty<NoteBase>();
			}
			NormalizeLongEndNoteBaseTypes(noteArray);
			HashSet<int> validStartNoteIds = new HashSet<int>();
			if (filterBeforeTicks.HasValue && filterBeforeTicks.Value >= 1L && conversionData != null)
			{
				Dictionary<int, ChartNote> noteById = BuildNoteLookup(conversionData.NoteList);
				foreach (ChartNote note in noteArray)
				{
					if (note == null)
					{
						continue;
					}
					if (GetEndTicks(note, noteById) >= filterBeforeTicks.Value)
					{
						ChartNote startNote = GetStartNote(note, noteById);
						if (startNote != null)
						{
							validStartNoteIds.Add(startNote.id);
						}
					}
				}
			}
			List<ChartNote> startNotes = new List<ChartNote>();
			foreach (ChartNote note in noteArray)
			{
				if (note == null || note.previousConnectionId != -1)
				{
					continue;
				}
				if (filterBeforeTicks.HasValue && filterBeforeTicks.Value >= 1L && !validStartNoteIds.Contains(note.id))
				{
					continue;
				}
				startNotes.Add(note);
			}
			startNotes.Sort(CompareNoteByTicks);
			List<NoteBase> liveNotes = new List<NoteBase>(startNotes.Count);
			foreach (ChartNote note in startNotes)
			{
				NoteBase liveNote = SetupNote(note, noteArray, eventBaseArray, liveBundleBuildData, conversionData);
				if (liveNote != null)
				{
					liveNotes.Add(liveNote);
				}
			}
			foreach (NoteBase note in liveNotes)
			{
				if (note?.NoteList == null)
				{
					continue;
				}
				foreach (NoteBase childNote in note.NoteList)
				{
					if (childNote != null)
					{
						childNote.Id = ++_idCount;
					}
				}
			}
			NoteBase[] result = liveNotes.ToArray();
			SetPairNotes(result);
			return result;
		}

		private static long GetEndTicks(ChartNote note, Dictionary<int, ChartNote> noteById)
		{
			if (note == null)
			{
				throw new ArgumentNullException(nameof(note));
			}
			ChartNote current = note;
			while (current.nextConnectionId != -1 && noteById != null && noteById.TryGetValue(current.nextConnectionId, out ChartNote next))
			{
				current = next;
			}
			return current.ticks;
		}

		private static ChartNote GetStartNote(ChartNote note, Dictionary<int, ChartNote> noteById)
		{
			if (note == null)
			{
				throw new ArgumentNullException(nameof(note));
			}
			ChartNote current = note;
			while (current.previousConnectionId != -1 && noteById != null && noteById.TryGetValue(current.previousConnectionId, out ChartNote previous))
			{
				current = previous;
			}
			return current;
		}

		private static EventBase[] FilterEventArray(ChartPlaybackEvent[] eventArray, long? filterBeforeTicks, MusicScoreInfo[] musicScoreInfoArray)
		{
			if (eventArray == null)
			{
				return Array.Empty<EventBase>();
			}
			if (!filterBeforeTicks.HasValue)
			{
				return eventArray.Where(e => e != null).Select(e => new EventBase(e.MusicScoreInfo)).ToArray();
			}
			List<EventBase> result = new List<EventBase>(eventArray.Length);
			foreach (ChartPlaybackEvent eventBase in eventArray)
			{
				if (eventBase == null)
				{
					continue;
				}
				long ticks = ChartTimeline.GetTicksFromTime(eventBase.MusicScoreInfo.time, musicScoreInfoArray);
				if (ticks < filterBeforeTicks.Value)
				{
					result.Add(new EventBase(eventBase.MusicScoreInfo));
				}
			}
			return result.ToArray();
		}

		private static void NormalizeLongEndNoteBaseTypes(List<ChartNote> noteArray)
		{
			if (noteArray == null)
			{
				return;
			}

			foreach (ChartNote note in noteArray)
			{
				if (note == null || note.previousConnectionId == -1 || note.nextConnectionId != -1)
				{
					continue;
				}

				switch (note.category)
				{
					case NoteCategory.Long:
						note.noteBaseType = ChartNote.NoteBaseType.Normal;
						break;
					case NoteCategory.FrictionLong:
						note.category = NoteCategory.Friction;
						note.noteBaseType = ChartNote.NoteBaseType.Friction;
						break;
					case NoteCategory.FrictionHideLong:
						note.category = NoteCategory.FrictionHide;
						note.noteBaseType = ChartNote.NoteBaseType.FrictionHide;
						break;
				}
			}
		}

		private NoteBase SetupNote(ChartNote note, List<ChartNote> noteArray, MusicScoreInfo[] musicScoreInfos, LiveBundleBuildData liveBundleBuildData, ChartDocument chart)
		{
			if (note == null)
			{
				return null;
			}
			NoteBase liveNote = ToNoteBase(note, liveBundleBuildData, noteArray, musicScoreInfos, chart);
			if (liveNote == null)
			{
				return null;
			}
			if (liveNote.NoteList != null)
			{
				for (int i = 1; i < liveNote.NoteList.Count; i++)
				{
					NoteBase childNote = liveNote.NoteList[i];
					if (childNote != null && childNote.IsSkip)
					{
						CalcSkipNoteLane(liveBundleBuildData, childNote);
					}
				}
			}
			if (liveNote.Category != NoteCategory.Long && liveNote.Category != NoteCategory.FrictionLong && liveNote.Category != NoteCategory.FrictionHideLong)
			{
				return liveNote;
			}
			if (liveNote is LongNote longNote && liveNote.NoteList != null && liveNote.NoteList.Count > 0 && liveBundleBuildData != null)
			{
				NoteBase lastNote = liveNote.NoteList[liveNote.NoteList.Count - 1];
				AddComboNote(lastNote, longNote, liveBundleBuildData.LongNoteComboBeat, liveBundleBuildData, musicScoreInfos, liveNote.speedRatio);
			}
			return liveNote;
		}

		private static void CalcSkipNoteLane(LiveBundleBuildData liveBundleBuildData, NoteBase childNote)
		{
			if (childNote == null)
			{
				return;
			}
			(float laneStart, float laneEnd) = RuntimeNoteGeometry.CalcSkipNoteLane(childNote);
			childNote.LaneStart = (int)Math.Floor(laneStart);
			childNote.LaneEnd = (int)Math.Floor(laneEnd);
			childNote.LaneStartF = laneStart;
			childNote.LaneEndF = laneEnd;
			float laneOffset = LiveUtility.GetLaneOffset(NoteCategory.Combo, liveBundleBuildData);
			OpenSekai.Shared.Scoring.JudgeTuning.WidenLaneRange(laneStart, laneEnd, laneOffset, out float judgeStart, out float judgeEnd);
			childNote.JudgeLaneStart = judgeStart;
			childNote.JudgeLaneEnd = judgeEnd;
		}

		private static void AddComboNote(NoteBase note, LongNote longNote, int longNoteComboBeat, LiveBundleBuildData liveBundleBuildData, MusicScoreInfo[] musicScoreInfoArray, float speedRatio)
		{
			if (note == null || longNote == null || longNoteComboBeat <= 0)
			{
				return;
			}
			int startIndex = (int)Math.Floor(longNote.MusicScoreInfo.barProgress * longNoteComboBeat) + 1 + longNote.MusicScoreInfo.bar * longNoteComboBeat;
			int endIndex = (int)Math.Ceiling(note.MusicScoreInfo.barProgress * longNoteComboBeat) - 1 + note.MusicScoreInfo.bar * longNoteComboBeat;
			for (int i = startIndex; i <= endIndex; i++)
			{
				MusicScoreInfo musicScoreInfo = MusicScore.GenerateNoteMusicScoreInfo(i / longNoteComboBeat, i % longNoteComboBeat / (float)longNoteComboBeat, musicScoreInfoArray);
				LongHoldCombo combo = new LongHoldCombo(musicScoreInfo, longNote.Type, speedRatio, LiveUtility.GetLaneOffset(NoteCategory.Combo, liveBundleBuildData));
				longNote.AddHoldCombo(combo);
				combo.SetSkip(true);
			}
			longNote.SortNoteList();
		}

		private void SetPairNotes(NoteBase[] noteArray)
		{
			if (noteArray == null)
			{
				return;
			}
			bool isShowPair = _showPair;
			Dictionary<float, List<NoteBase>> notesByPosition = new Dictionary<float, List<NoteBase>>();
			foreach (NoteBase rootNote in noteArray)
			{
				if (rootNote?.NoteList == null)
				{
					continue;
				}
				foreach (NoteBase note in rootNote.NoteList)
				{
					if (note == null || IsNotPairNoteTarget(note))
					{
						continue;
					}
					float position = note.MusicScoreInfo.bar + note.MusicScoreInfo.barProgress;
					if (!notesByPosition.TryGetValue(position, out List<NoteBase> notes))
					{
						notes = new List<NoteBase>();
						notesByPosition[position] = notes;
					}
					notes.Add(note);
				}
			}
			foreach (List<NoteBase> notes in notesByPosition.Values)
			{
				if (notes.Count < 2)
				{
					continue;
				}
				notes.Sort((a, b) => a.DefaultLeftLane.CompareTo(b.DefaultLeftLane));
				SetPairNote(notes[0], notes[notes.Count - 1], isShowPair);
			}
		}

		public static bool IsNotPairNoteTarget(NoteBase note)
		{
			if (note == null)
			{
				return true;
			}
			return note is FrictionHideNote
				|| note is FrictionHideLongNote
				|| note.Category == NoteCategory.Guide
				|| note is GuideHiddenConnectionNote
				|| note.Category == NoteCategory.Combo
				|| note.IsDecoration;
		}

		private static void SetPairNote(NoteBase first, NoteBase last, bool isShowPair)
		{
			if (!isShowPair || first == null || last == null)
			{
				return;
			}
			if (first.PairNote is NoteBase firstPair)
			{
				first.SetPairNote(null);
				firstPair.SetPairNote(null);
			}
			if (last.PairNote is NoteBase lastPair)
			{
				last.SetPairNote(null);
				lastPair.SetPairNote(null);
			}
			first.SetPairNote(last);
			last.SetPairNote(first);
		}

		private static int CompareNoteByTicks(ChartNote a, ChartNote b)
		{
			int result = a.ticks.CompareTo(b.ticks);
			return result != 0 ? result : a.id.CompareTo(b.id);
		}

		private static NoteBase ToNoteBase(ChartNote source, LiveBundleBuildData bundleBuildData, List<ChartNote> noteArray, MusicScoreInfo[] musicScoreInfos, ChartDocument chart)
		{
			if (source.previousConnectionId != -1)
			{
				return null;
			}
			MusicScoreInfo musicScoreInfo = ChartTimeline.GenerateMusicScoreInfoFromTicks(source.ticks, musicScoreInfos);
			NoteBase rootNote = GenerateNote(source, bundleBuildData, musicScoreInfo);
			if (source.nextConnectionId == -1)
			{
				return rootNote;
			}
			if (rootNote is not LongNote longNote || noteArray == null)
			{
				return rootNote;
			}
			int targetConnectionId = source.nextConnectionId;
			while (targetConnectionId != -1)
			{
				ChartNote childNote = noteArray.Find(note => note != null && note.id == targetConnectionId);
				if (childNote == null)
				{
					return rootNote;
				}
				musicScoreInfo = ChartTimeline.GenerateMusicScoreInfoFromTicks(childNote.ticks, musicScoreInfos);
				GenerateNote(childNote, bundleBuildData, musicScoreInfo, longNote);
				targetConnectionId = childNote.nextConnectionId;
			}
			return rootNote;
		}

		private static NoteBase GenerateNote(ChartNote noteBase, LiveBundleBuildData bundleBuildData, MusicScoreInfo musicScoreInfo, LongNote longNote = null)
		{
			if (noteBase == null)
			{
				throw new ArgumentNullException(nameof(noteBase));
			}
			NoteBase note;
			switch (noteBase.noteBaseType)
			{
			case ChartNote.NoteBaseType.Normal:
				note = new NormalNote(musicScoreInfo, 0, noteBase.laneStart, noteBase.laneEnd, noteBase.category, noteBase.speedRatio, noteBase.type, LiveUtility.GetLaneOffset(noteBase.category, bundleBuildData));
				break;
			case ChartNote.NoteBaseType.Long:
				note = new LongNote(musicScoreInfo, 0, noteBase.laneStart, noteBase.laneEnd, noteBase.category, noteBase.type, noteBase.speedRatio, LiveUtility.GetLaneOffset(noteBase.category, bundleBuildData), noteBase.noteLineType);
				break;
			case ChartNote.NoteBaseType.Flick:
				note = new FlickNote(musicScoreInfo, 0, noteBase.laneStart, noteBase.laneEnd, noteBase.category, noteBase.speedRatio, noteBase.type, noteBase.direction, LiveUtility.GetLaneOffset(noteBase.category, bundleBuildData), LiveUtility.ScreenDpiToInch(bundleBuildData == null ? 0f : bundleBuildData.FlickDistance));
				break;
			case ChartNote.NoteBaseType.FrictionFlick:
				note = new FrictionFlickNote(musicScoreInfo, 0, noteBase.laneStart, noteBase.laneEnd, noteBase.category, noteBase.speedRatio, noteBase.type, noteBase.direction, LiveUtility.ScreenDpiToInch(bundleBuildData == null ? 0f : bundleBuildData.FlickDistance), LiveUtility.GetLaneOffset(noteBase.category, bundleBuildData));
				break;
			case ChartNote.NoteBaseType.Connection:
				note = new ConnectionNote(musicScoreInfo, 0, noteBase.laneStart, noteBase.laneEnd, noteBase.category, noteBase.type, noteBase.speedRatio, LiveUtility.GetLaneOffset(noteBase.category, bundleBuildData), noteBase.noteLineType);
				note.SetSkip(noteBase.isSkip);
				note.SetDecoration(noteBase.isDecoration);
				if (note is LongNote connectionLongNote && !string.IsNullOrEmpty(noteBase.guideColor))
				{
					connectionLongNote.SetGuideColor(noteBase.guideColor);
				}
				longNote?.AddConnectionNote(note);
				return note;
			case ChartNote.NoteBaseType.HiddenConnection:
				note = new HiddenConnectionNote(musicScoreInfo, 0, noteBase.laneStart, noteBase.laneEnd, noteBase.category, noteBase.type, noteBase.speedRatio, LiveUtility.GetLaneOffset(noteBase.category, bundleBuildData), noteBase.noteLineType);
				note.SetSkip(noteBase.isSkip);
				note.SetDecoration(noteBase.isDecoration);
				if (note is LongNote hiddenConnectionLongNote && !string.IsNullOrEmpty(noteBase.guideColor))
				{
					hiddenConnectionLongNote.SetGuideColor(noteBase.guideColor);
				}
				longNote?.AddConnectionNote(note);
				return note;
			case ChartNote.NoteBaseType.LongHoldCombo:
				note = new LongHoldCombo(musicScoreInfo, noteBase.type, noteBase.speedRatio, LiveUtility.GetLaneOffset(noteBase.category, bundleBuildData));
				note.SetSkip(noteBase.isSkip);
				note.SetDecoration(noteBase.isDecoration);
				longNote?.AddHoldCombo((LongHoldCombo)note);
				return note;
			case ChartNote.NoteBaseType.FrictionLong:
				note = new FrictionLongNote(musicScoreInfo, 0, noteBase.laneStart, noteBase.laneEnd, noteBase.category, noteBase.type, bundleBuildData, noteBase.speedRatio, noteBase.noteLineType);
				break;
			case ChartNote.NoteBaseType.FrictionHideLong:
				note = new FrictionHideLongNote(musicScoreInfo, 0, noteBase.laneStart, noteBase.laneEnd, noteBase.category, noteBase.type, bundleBuildData, noteBase.speedRatio, noteBase.noteLineType);
				break;
			case ChartNote.NoteBaseType.Guide:
				if (noteBase.previousConnectionId != -1 && noteBase.nextConnectionId == -1)
				{
					note = new GuideEndNote(musicScoreInfo, 0, noteBase.laneStart, noteBase.laneEnd, noteBase.category, bundleBuildData, noteBase.type, noteBase.speedRatio, noteBase.noteLineType);
				}
				else
				{
					note = new GuideNote(musicScoreInfo, 0, noteBase.laneStart, noteBase.laneEnd, noteBase.category, noteBase.type, bundleBuildData, noteBase.speedRatio, noteBase.noteLineType);
				}
				break;
			case ChartNote.NoteBaseType.Friction:
				note = new FrictionNote(musicScoreInfo, 0, noteBase.laneStart, noteBase.laneEnd, noteBase.category, bundleBuildData, noteBase.type, noteBase.speedRatio, noteBase.noteLineType);
				break;
			case ChartNote.NoteBaseType.FrictionHide:
				note = new FrictionHideNote(musicScoreInfo, 0, noteBase.laneStart, noteBase.laneEnd, noteBase.category, bundleBuildData, noteBase.type, noteBase.speedRatio, noteBase.noteLineType);
				break;
			case ChartNote.NoteBaseType.GuideEnd:
				note = new GuideEndNote(musicScoreInfo, 0, noteBase.laneStart, noteBase.laneEnd, noteBase.category, bundleBuildData, noteBase.type, noteBase.speedRatio, noteBase.noteLineType);
				break;
			case ChartNote.NoteBaseType.GuideHiddenConnection:
				note = new GuideHiddenConnectionNote(musicScoreInfo, 0, noteBase.laneStart, noteBase.laneEnd, noteBase.category, bundleBuildData, noteBase.type, noteBase.speedRatio, noteBase.noteLineType);
				note.SetSkip(noteBase.isSkip);
				note.SetDecoration(noteBase.isDecoration);
				if (note is LongNote guideHiddenLongNote && !string.IsNullOrEmpty(noteBase.guideColor))
				{
					guideHiddenLongNote.SetGuideColor(noteBase.guideColor);
				}
				longNote?.AddConnectionNote(note);
				return note;
			default:
				throw new ArgumentOutOfRangeException(nameof(noteBase.noteBaseType), noteBase.noteBaseType, null);
			}
			note.SetSkip(noteBase.isSkip);
			note.SetDecoration(noteBase.isDecoration);
			// 设置引导线自定义颜色
			if (note is LongNote longNoteForColor && !string.IsNullOrEmpty(noteBase.guideColor))
			{
				longNoteForColor.SetGuideColor(noteBase.guideColor);
			}
			SetParent(longNote, note);
			return note;
		}

		private static void SetParent(LongNote longNote, NoteBase note)
		{
			if (longNote == null)
			{
				return;
			}
			longNote.AddNoteListAndSetupChildNote(note);
			note?.SetParentNote(longNote);
			// A decorative bar only moves the lane around: the note at its end is played by
			// the player, so the bar must not grade it. It just stops when that note passes.
			if (longNote.IsDecoration && note != null && !note.IsDecoration && note.HasJudgment)
			{
				longNote.SkipTailJudgement();
			}
		}
    }
}
