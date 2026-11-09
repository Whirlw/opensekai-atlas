using System;
using System.Collections.Generic;
using Sekai;
using Sekai.Live;
using OpenSekai.Charts;
using System.Linq;
namespace OpenSekai.Live.Conversion
{
    public sealed class RuntimeChartImporter
    {
        private int _nextId;
        private int GetNewId() => ++_nextId;
        public static ChartDocument FromMusicScore(MusicScore score)
        {
            var importer = new RuntimeChartImporter();
            var document = new ChartDocument();
            if(score == null) return document;
            document.MusicScoreEventDataList = importer.ConvertEventList(score.musicScoreInfoArray);
            document.EventArray = (score.EventArray ?? Array.Empty<EventBase>()).Where(e => e != null
                && !(e is SkillEvent) && !(e is FeverStartEvent) && !(e is FeverBeginEvent) && !(e is FeverEndEvent))
                .Select(e => new ChartPlaybackEvent { MusicScoreInfo = e.MusicScoreInfo }).ToArray();
            var infos = ChartTimeline.CreateSortedMusicScoreInfoList(score.musicScoreInfoArray);
            foreach(var note in score.NoteArray ?? Array.Empty<NoteBase>())
                if(note != null) document.NoteList.AddRange(FromNoteBaseArray(note,importer.GetNewId,infos));
            document.NoteList.Sort((a,b) => a.ticks != b.ticks ? a.ticks.CompareTo(b.ticks) : a.id.CompareTo(b.id));
            document.MusicScoreTicksMax = Math.Max(document.NoteList.Count == 0 ? 0 : document.NoteList.Max(n=>n.ticks),
                document.MusicScoreEventDataList.Count == 0 ? 0 : document.MusicScoreEventDataList.Max(e=>e.ticks));
            document.Migrate();
            return document;
        }
		private List<ChartEvent> ConvertEventList(MusicScoreInfo[] infos)
		{
			List<ChartEvent> result = new List<ChartEvent>();
			List<MusicScoreInfo> sortedInfos = ChartTimeline.CreateSortedMusicScoreInfoList(infos);
			SetFirstEvents(infos, result, sortedInfos);
			if (infos == null)
			{
				return result;
			}
			for (int i = 1; i < infos.Length; i++)
			{
				AddChangedEvent(infos[i], infos[i - 1], result, sortedInfos);
			}
			return result;
		}

		private void SetFirstEvents(MusicScoreInfo[] infos, List<ChartEvent> list, List<MusicScoreInfo> sortedInfos)
		{
			if (infos == null || infos.Length == 0)
			{
				return;
			}
			MusicScoreInfo info = infos[0];
			long ticks = ChartTimeline.CalculateTicksFromBarAndProgressSorted(info.bar, info.barProgress, sortedInfos);
			list.Add(CreateEvent(GetNewId(), ChartEventType.BPM, ticks, info.bpm));
			list.Add(CreateEvent(GetNewId(), ChartEventType.TimeSignature, ticks, ChartTimeline.FormatTimeSignatureText(info.timeSignature)));
			list.Add(CreateEvent(GetNewId(), ChartEventType.HighSpeed, ticks, info.speedRatio));
			list.Add(CreateEvent(GetNewId(), ChartEventType.SeVolume, ticks, info.seVolume));
		}

		private void AddChangedEvent(MusicScoreInfo musicScoreInfo, MusicScoreInfo beforeScoreInfo, List<ChartEvent> result, List<MusicScoreInfo> sortedInfos)
		{
			long ticks = ChartTimeline.CalculateTicksFromBarAndProgressSorted(musicScoreInfo.bar, musicScoreInfo.barProgress, sortedInfos);
			if (musicScoreInfo.bpm != beforeScoreInfo.bpm)
			{
				result.Add(CreateEvent(GetNewId(), ChartEventType.BPM, ticks, musicScoreInfo.bpm));
			}
			if (musicScoreInfo.timeSignature != beforeScoreInfo.timeSignature)
			{
				result.Add(CreateEvent(GetNewId(), ChartEventType.TimeSignature, ticks, ChartTimeline.FormatTimeSignatureText(musicScoreInfo.timeSignature)));
			}
			if (musicScoreInfo.speedRatio != beforeScoreInfo.speedRatio)
			{
				result.Add(CreateEvent(GetNewId(), ChartEventType.HighSpeed, ticks, musicScoreInfo.speedRatio));
			}
			if (musicScoreInfo.seVolume != beforeScoreInfo.seVolume)
			{
				result.Add(CreateEvent(GetNewId(), ChartEventType.SeVolume, ticks, musicScoreInfo.seVolume));
			}
		}

		private static ChartEvent CreateEvent(int id, ChartEventType type, long ticks, object changeValue)
		{
			return new ChartEvent
			{
				id = id,
				eventType = type,
				ticks = ticks,
				changeValue = changeValue
			};
		}
		public static ChartNote[] FromNoteBaseArray(NoteBase noteBase, Func<int> getNewId, List<MusicScoreInfo> sortedMusicScoreInfos)
		{
			if (noteBase == null)
			{
				return Array.Empty<ChartNote>();
			}
			ChartNote firstNote = FromNoteBase(noteBase, getNewId, sortedMusicScoreInfos);
			if (noteBase.NoteList == null || noteBase.NoteList.Count < 2)
			{
				return new[] { firstNote };
			}
			ChartNote[] result = new ChartNote[noteBase.NoteList.Count];
			result[0] = firstNote;
			ChartNote previousNote = firstNote;
			for (int i = 1; i < noteBase.NoteList.Count; i++)
			{
				ChartNote currentNote = FromNoteBase(noteBase.NoteList[i], getNewId, sortedMusicScoreInfos);
				previousNote.nextConnectionId = currentNote.id;
				currentNote.previousConnectionId = previousNote.id;
				result[i] = currentNote;
				previousNote = currentNote;
			}
			return result;
		}

		private static ChartNote FromNoteBase(NoteBase noteBase, Func<int> newId, List<MusicScoreInfo> sortedMusicScoreInfos)
		{
			if (noteBase == null)
			{
				throw new ArgumentNullException(nameof(noteBase));
			}
			if (newId == null)
			{
				throw new ArgumentNullException(nameof(newId));
			}
			long ticks = ChartTimeline.CalculateTicksFromBarAndProgressSorted(noteBase.MusicScoreInfo.bar, noteBase.MusicScoreInfo.barProgress, sortedMusicScoreInfos);
			ChartNote.NoteBaseType baseType = noteBase switch
			{
				GuideEndNote => ChartNote.NoteBaseType.GuideEnd,
				FrictionHideNote => ChartNote.NoteBaseType.FrictionHide,
				FrictionNote => ChartNote.NoteBaseType.Friction,
				GuideHiddenConnectionNote => ChartNote.NoteBaseType.GuideHiddenConnection,
				HiddenConnectionNote => ChartNote.NoteBaseType.HiddenConnection,
				FrictionHideLongNote => ChartNote.NoteBaseType.FrictionHideLong,
				GuideNote => ChartNote.NoteBaseType.Guide,
				FrictionLongNote => ChartNote.NoteBaseType.FrictionLong,
				LongNote => ChartNote.NoteBaseType.Long,
				ConnectionNote => ChartNote.NoteBaseType.Connection,
				FrictionFlickNote => ChartNote.NoteBaseType.FrictionFlick,
				FlickNote => ChartNote.NoteBaseType.Flick,
				LongHoldCombo => ChartNote.NoteBaseType.LongHoldCombo,
				NormalNote => ChartNote.NoteBaseType.Normal,
				_ => ChartNote.NoteBaseType.Normal
			};
			NoteCategory noteCategory = noteBase.Category;
			if (noteBase is FrictionHideNote && noteCategory == NoteCategory.FrictionHideLong)
			{
				noteCategory = NoteCategory.FrictionHide;
			}
			else if (noteBase is FrictionNote && noteCategory == NoteCategory.FrictionLong)
			{
				noteCategory = NoteCategory.Friction;
			}
			return new ChartNote(
				newId(),
				ticks,
				noteBase.LaneStart,
				noteBase.LaneEnd,
				noteCategory,
				noteBase.Type,
				noteBase.speedRatio,
				noteBase.LineType,
				baseType,
				noteBase.IsSkip,
				noteBase.Direction,
				-1,
				-1,
				noteBase.IsDecoration);
		}
    }
}
