using System.Collections.Generic;
using System.Linq;
using Sekai;
using Sekai.Live;
using Sekai.SUS;
using UnityEngine;

namespace OpenSekai.Shared.Conversion
{
	/// <summary>
	/// Builds the runtime note objects of a chart from the parsed SUS note tables.
	/// </summary>
	/// <remarks>
	/// This is the half of the importer that has to know the concrete note classes: the category of each
	/// <see cref="NoteInfo"/> decides which <see cref="NoteBase"/> subclass is built and how the hold and
	/// guide chains are linked. It never reads SUS text — the chart arrives as resolved DTOs and frames.
	/// Visiting order and id assignment are the shipped importer's: notes by bar position, then lane, ids
	/// continuing across the normal and guide passes.
	/// </remarks>
	public sealed class SusNoteConverter
	{
		private const int LaneMaxIndex = 11;

		private List<NoteBase> _notes;
		private Dictionary<int, LongNote> _longNotes;
		private Dictionary<int, GuideNote> _guideNotes;
		private MusicScoreInfo[] _frames;
		private LiveBundleBuildData _config;
		private bool _isMirror;
		private bool _isShowPair;
		private NoteBase _pair;
		private Dictionary<int, List<float>> _chainPositions;

		/// <summary>
		/// Converts the note tables of <paramref name="chart"/> into note roots, sorted by time and
		/// already linked. The returned list is the score's <c>NoteArray</c>.
		/// </summary>
		public List<NoteBase> Convert(SusChartData chart, MusicScoreInfo[] frames, LiveBundleBuildData config, bool isNeedCombo, bool isMirror, bool showPair)
		{
			_notes = new List<NoteBase>();
			_longNotes = new Dictionary<int, LongNote>();
			_guideNotes = new Dictionary<int, GuideNote>();
			_frames = frames;
			_config = config;
			_isMirror = isMirror;
			_isShowPair = showPair;
			_pair = null;
			_chainPositions = new Dictionary<int, List<float>>();

			ConvertNoteTable(chart.NoteInfos, isNeedCombo);
			ConvertNoteTable(chart.GuideInfos, false);
			foreach (LongNote note in _longNotes.Values)
			{
				note.SortNoteList();
			}
			foreach (GuideNote note in _guideNotes.Values)
			{
				note.SortNoteList();
			}
			_notes.Sort((left, right) => left.MusicScoreInfo.time.CompareTo(right.MusicScoreInfo.time));
			SetPairNotes();
			SusSkipNoteResolver.Resolve(_notes, config);
			return _notes;
		}

		private void ConvertNoteTable(Dictionary<float, Dictionary<int, NoteInfo>> table, bool isNeedCombo)
		{
			int id = _notes.Count;
			foreach (float key in table.Keys.OrderBy(value => value).ToArray())
			{
				Dictionary<int, NoteInfo> laneDict = table[key];
				foreach (int laneKey in laneDict.Keys.OrderBy(value => value).ToArray())
				{
					NoteInfo noteInfo = laneDict[laneKey];
					if (noteInfo != null && noteInfo.LongNo != -1)
					{
						// Only the bar's own chain occupies a position; another bar at the same moment
						// is a different note and must not suppress this bar's middle point.
						if (!_chainPositions.TryGetValue(noteInfo.LongNo, out List<float> chain))
						{
							chain = new List<float>();
							_chainPositions[noteInfo.LongNo] = chain;
						}

						chain.Add(key);
					}
					MusicScoreInfo info = MusicScore.GenerateNoteMusicScoreInfo(noteInfo.Bar, noteInfo.BarProgress, _frames);
					int laneStart = noteInfo.Lane;
					int laneEnd = noteInfo.Lane + noteInfo.Width - 1;
					if (_isMirror)
					{
						int mirroredStart = LaneMaxIndex - laneEnd;
						int mirroredEnd = LaneMaxIndex - laneStart;
						laneStart = mirroredStart;
						laneEnd = mirroredEnd;
					}
					NoteCategory category = noteInfo.Category;
					switch (category)
					{
					case NoteCategory.Normal:
					case NoteCategory.Skip:
						ConvertNormalNote(ref id, noteInfo, info, laneStart, laneEnd, category, isNeedCombo);
						break;
					case NoteCategory.Flick:
						ConvertFlickNote(ref id, noteInfo, info, laneStart, laneEnd, category, isNeedCombo);
						break;
					case NoteCategory.Long:
						ConvertLongNote(ref id, noteInfo, info, laneStart, laneEnd, category, isNeedCombo);
						break;
					case NoteCategory.Connection:
						ConvertConnectionNote(ref id, noteInfo, info, laneStart, laneEnd, category);
						break;
					case NoteCategory.Hidden:
						ConvertHiddenNote(ref id, noteInfo, info, laneStart, laneEnd, category);
						break;
					case NoteCategory.Friction:
						ConvertFrictionNote(ref id, noteInfo, info, laneStart, laneEnd, category, isNeedCombo);
						break;
					case NoteCategory.FrictionHide:
						ConvertFrictionHideNote(ref id, noteInfo, info, laneStart, laneEnd, category, isNeedCombo);
						break;
					case NoteCategory.FrictionLong:
						ConvertFrictionLongNote(ref id, noteInfo, info, laneStart, laneEnd, category, isNeedCombo);
						break;
					case NoteCategory.FrictionHideLong:
						ConvertFrictionHideLongNote(ref id, noteInfo, info, laneStart, laneEnd, category, isNeedCombo);
						break;
					case NoteCategory.FrictionFlick:
						ConvertFrictionFlickNote(ref id, noteInfo, info, laneStart, laneEnd, category, isNeedCombo);
						break;
					case NoteCategory.Guide:
						ConvertGuideNote(ref id, noteInfo, info, laneStart, laneEnd, category);
						break;
					case NoteCategory.GuideEnd:
						ConvertGuideEndNote(ref id, noteInfo, info, laneStart, laneEnd, category);
						break;
					case NoteCategory.GuideHidden:
						ConvertGuideHiddenNote(ref id, noteInfo, info, laneStart, laneEnd, category);
						break;
					}
				}
			}
		}

		private void ConvertNormalNote(ref int id, NoteInfo noteInfo, MusicScoreInfo info, int laneStart, int laneEnd, NoteCategory category, bool isNeedCombo)
		{
			NoteBase note = noteInfo.IsSkip
				? new HiddenConnectionNote(info, id++, laneStart, laneEnd, category, noteInfo.Type, noteInfo.SpeedRatio, LiveUtility.GetLaneOffset(category, _config), noteInfo.LineType)
				: new NormalNote(info, id++, laneStart, laneEnd, category, noteInfo.SpeedRatio, noteInfo.Type, LiveUtility.GetLaneOffset(category, _config));
			if (noteInfo.IsSkip)
			{
				note.SetSkip(true);
			}
			LongJoinCheck(noteInfo, note, isNeedCombo);
		}

		private void ConvertFlickNote(ref int id, NoteInfo noteInfo, MusicScoreInfo info, int laneStart, int laneEnd, NoteCategory category, bool isNeedCombo)
		{
			float laneOffset = LiveUtility.GetLaneOffset(category, _config);
			float flickDistance = LiveUtility.ScreenDpiToInch(_config == null ? 0f : _config.FlickDistance);
			NoteBase note = new FlickNote(info, id++, laneStart, laneEnd, category, noteInfo.SpeedRatio, noteInfo.Type, noteInfo.Direction, laneOffset, flickDistance);
			LongJoinCheck(noteInfo, note, isNeedCombo);
		}

		private void ConvertLongNote(ref int id, NoteInfo noteInfo, MusicScoreInfo info, int laneStart, int laneEnd, NoteCategory category, bool isNeedCombo)
		{
			NoteBase note = new LongNote(info, id++, laneStart, laneEnd, category, noteInfo.Type, noteInfo.SpeedRatio, LiveUtility.GetLaneOffset(category, _config), noteInfo.LineType);
			LongJoinCheck(noteInfo, note, isNeedCombo);
		}

		private void ConvertGuideNote(ref int id, NoteInfo noteInfo, MusicScoreInfo info, int laneStart, int laneEnd, NoteCategory category)
		{
			NoteBase note = new GuideNote(info, id++, laneStart, laneEnd, category, noteInfo.Type, _config, noteInfo.SpeedRatio, noteInfo.LineType);
			GuideJoinCheck(noteInfo, note);
		}

		private void ConvertGuideEndNote(ref int id, NoteInfo noteInfo, MusicScoreInfo info, int laneStart, int laneEnd, NoteCategory category)
		{
			NoteBase note = new GuideEndNote(info, id++, laneStart, laneEnd, category, _config, noteInfo.Type, noteInfo.SpeedRatio, noteInfo.LineType);
			GuideJoinCheck(noteInfo, note);
		}

		private void ConvertGuideHiddenNote(ref int id, NoteInfo noteInfo, MusicScoreInfo info, int laneStart, int laneEnd, NoteCategory category)
		{
			NoteBase note = new GuideHiddenConnectionNote(info, id++, laneStart, laneEnd, category, _config, noteInfo.Type, noteInfo.SpeedRatio, noteInfo.LineType);
			GuideJoinCheck(noteInfo, note);
		}

		private void ConvertConnectionNote(ref int id, NoteInfo noteInfo, MusicScoreInfo info, int laneStart, int laneEnd, NoteCategory category)
		{
			NoteBase note = new ConnectionNote(info, id++, laneStart, laneEnd, category, noteInfo.Type, noteInfo.SpeedRatio, LiveUtility.GetLaneOffset(category, _config), noteInfo.LineType);
			LongConnectionCheck(noteInfo, note);
		}

		private void ConvertHiddenNote(ref int id, NoteInfo noteInfo, MusicScoreInfo info, int laneStart, int laneEnd, NoteCategory category)
		{
			NoteBase note = new HiddenConnectionNote(info, id++, laneStart, laneEnd, category, noteInfo.Type, noteInfo.SpeedRatio, LiveUtility.GetLaneOffset(category, _config), noteInfo.LineType);
			LongConnectionCheck(noteInfo, note);
		}

		private void ConvertFrictionNote(ref int id, NoteInfo noteInfo, MusicScoreInfo info, int laneStart, int laneEnd, NoteCategory category, bool isNeedCombo)
		{
			NoteBase note = new FrictionNote(info, id++, laneStart, laneEnd, category, _config, noteInfo.Type, noteInfo.SpeedRatio, noteInfo.LineType);
			LongJoinCheck(noteInfo, note, isNeedCombo);
		}

		private void ConvertFrictionHideNote(ref int id, NoteInfo noteInfo, MusicScoreInfo info, int laneStart, int laneEnd, NoteCategory category, bool isNeedCombo)
		{
			NoteBase note = new FrictionHideNote(info, id++, laneStart, laneEnd, category, _config, noteInfo.Type, noteInfo.SpeedRatio, noteInfo.LineType);
			LongJoinCheck(noteInfo, note, isNeedCombo);
		}

		private void ConvertFrictionLongNote(ref int id, NoteInfo noteInfo, MusicScoreInfo info, int laneStart, int laneEnd, NoteCategory category, bool isNeedCombo)
		{
			NoteBase note = new FrictionLongNote(info, id++, laneStart, laneEnd, category, noteInfo.Type, _config, noteInfo.SpeedRatio, noteInfo.LineType);
			LongJoinCheck(noteInfo, note, isNeedCombo);
		}

		private void ConvertFrictionHideLongNote(ref int id, NoteInfo noteInfo, MusicScoreInfo info, int laneStart, int laneEnd, NoteCategory category, bool isNeedCombo)
		{
			NoteBase note = new FrictionHideLongNote(info, id++, laneStart, laneEnd, category, noteInfo.Type, _config, noteInfo.SpeedRatio, noteInfo.LineType);
			LongJoinCheck(noteInfo, note, isNeedCombo);
		}

		private void ConvertFrictionFlickNote(ref int id, NoteInfo noteInfo, MusicScoreInfo info, int laneStart, int laneEnd, NoteCategory category, bool isNeedCombo)
		{
			float laneOffset = LiveUtility.GetLaneOffset(category, _config);
			float flickDistance = LiveUtility.ScreenDpiToInch(_config == null ? 0f : _config.FlickDistance);
			NoteBase note = new FrictionFlickNote(info, id++, laneStart, laneEnd, category, noteInfo.SpeedRatio, noteInfo.Type, noteInfo.Direction, flickDistance, laneOffset);
			LongJoinCheck(noteInfo, note, isNeedCombo);
		}

		// Starts a chain, joins one as its tail, or (with isNeedCombo) fills in generated combo bodies.
		private void LongJoinCheck(NoteInfo noteInfo, NoteBase note, bool isNeedCombo)
		{
			if (noteInfo.LongNo == -1)
			{
				SameTimingCheck(note);
				_notes.Add(note);
				return;
			}

			if (note is LongNote longRoot
				&& (noteInfo.Category == NoteCategory.Long
					|| noteInfo.Category == NoteCategory.FrictionLong
					|| noteInfo.Category == NoteCategory.FrictionHideLong))
			{
				_longNotes[noteInfo.LongNo] = longRoot;
				SameTimingCheck(note);
				_notes.Add(note);
				return;
			}

			LongConnectionCheck(noteInfo, note);
			if (note.ParentNote is LongNote parent)
			{
				AddComboNote(noteInfo, note, isNeedCombo, parent);
			}
		}

		private void AddComboNote(NoteInfo noteInfo, NoteBase note, bool isNeedCombo, LongNote longNote)
		{
			int longNoteComboBeat = _config == null ? 0 : _config.LongNoteComboBeat;
			if (!isNeedCombo || note == null || longNote == null || longNoteComboBeat <= 0)
			{
				return;
			}

			int startIndex = Mathf.FloorToInt(longNote.MusicScoreInfo.barProgress * longNoteComboBeat) + 1 + longNote.MusicScoreInfo.bar * longNoteComboBeat;
			int endIndex = Mathf.CeilToInt(note.MusicScoreInfo.barProgress * longNoteComboBeat) - 1 + note.MusicScoreInfo.bar * longNoteComboBeat;

			// A grid position this bar's own chain already occupies is not a middle: the head, the
			// connection notes and the end own their positions. Other bars at the same moment are
			// separate notes and do not suppress this bar's middles.
			HashSet<int> occupied = ComboOccupiedIndices(noteInfo.LongNo, longNoteComboBeat);

			for (int i = startIndex; i <= endIndex; i++)
			{
				if (occupied.Contains(i))
				{
					continue;
				}

				MusicScoreInfo comboInfo = MusicScore.GenerateNoteMusicScoreInfo(i / longNoteComboBeat, i % longNoteComboBeat / (float)longNoteComboBeat, _frames);
				LongHoldCombo combo = new LongHoldCombo(comboInfo, longNote.Type, 0, longNote.speedRatio, LiveUtility.GetLaneOffset(NoteCategory.Combo, _config));
				combo.SetSkip(true);
				longNote.AddHoldCombo(combo);
			}
			longNote.SortNoteList();
		}

		/// <summary>Grid positions (in combo-beat indices) that this bar's own chain already occupies.</summary>
		private HashSet<int> ComboOccupiedIndices(int longNo, int longNoteComboBeat)
		{
			var indices = new HashSet<int>();
			if (_chainPositions == null || longNoteComboBeat <= 0
				|| !_chainPositions.TryGetValue(longNo, out List<float> chain))
			{
				return indices;
			}

			foreach (float position in chain)
			{
				float beat = position * longNoteComboBeat;
				float rounded = Mathf.Round(beat);
				if (Mathf.Abs(beat - rounded) > 0.01f)
				{
					continue;
				}

				indices.Add(Mathf.RoundToInt(beat));
			}

			return indices;
		}

		private void GuideJoinCheck(NoteInfo noteInfo, NoteBase note)
		{
			if (noteInfo.LongNo == -1)
			{
				_notes.Add(note);
				return;
			}

			if (note is GuideNote guideRoot && noteInfo.Category == NoteCategory.Guide)
			{
				_guideNotes[noteInfo.LongNo] = guideRoot;
				_notes.Add(note);
				return;
			}
			GuideConnectionCheck(noteInfo, note);
		}

		private void LongConnectionCheck(NoteInfo noteInfo, NoteBase note)
		{
			if (noteInfo.LongNo == -1 || !_longNotes.TryGetValue(noteInfo.LongNo, out LongNote parent))
			{
				_notes.Add(note);
				return;
			}

			if (noteInfo.Category == NoteCategory.Connection || noteInfo.Category == NoteCategory.Hidden || noteInfo.Category == NoteCategory.Skip)
			{
				parent.AddConnectionNote(note);
				if (noteInfo.IsSkip)
				{
					note.SetSkip(true);
				}
			}
			else
			{
				note.SetParentNote(parent);
				parent.AddNoteListAndSetupChildNote(note);
			}
			parent.SortNoteList();
		}

		private void GuideConnectionCheck(NoteInfo noteInfo, NoteBase note)
		{
			if (noteInfo.LongNo == -1 || !_guideNotes.TryGetValue(noteInfo.LongNo, out GuideNote parent))
			{
				_notes.Add(note);
				return;
			}

			if (noteInfo.Category == NoteCategory.GuideHidden)
			{
				parent.AddConnectionNote(note);
			}
			else
			{
				note.SetParentNote(parent);
				parent.AddNoteListAndSetupChildNote(note);
			}
			parent.SortNoteList();
		}

		/// <summary>
		/// Pairs consecutive roots that land on exactly the same timestamp, for the doubled-line display.
		/// </summary>
		private void SameTimingCheck(NoteBase note)
		{
			if (!_isShowPair)
			{
				return;
			}
			if (_pair != null)
			{
				PairNotes(note, _pair);
			}
			_pair = note;
		}

		private static void PairNotes(NoteBase note, NoteBase usedNote)
		{
			if (note == null || usedNote == null)
			{
				return;
			}
			if (Mathf.Approximately(note.MusicScoreInfo.time, usedNote.MusicScoreInfo.time))
			{
				note.SetPairNote(usedNote);
				usedNote.SetPairNote(note);
			}
		}

		/// <summary>Second pairing pass: two judged notes sharing a timestamp are a pair.</summary>
		private void SetPairNotes()
		{
			if (!_isShowPair)
			{
				return;
			}
			Dictionary<float, List<NoteBase>> notesByTime = new Dictionary<float, List<NoteBase>>();
			foreach (NoteBase root in _notes)
			{
				if (root?.NoteList == null)
				{
					continue;
				}
				foreach (NoteBase note in root.NoteList)
				{
					if (note == null || note.Category == NoteCategory.Combo || note.IsSkip || !note.HasJudgment)
					{
						continue;
					}
					float key = note.MusicScoreInfo.time;
					if (!notesByTime.TryGetValue(key, out List<NoteBase> list))
					{
						list = new List<NoteBase>();
						notesByTime[key] = list;
					}
					list.Add(note);
				}
			}
			foreach (List<NoteBase> list in notesByTime.Values)
			{
				if (list.Count != 2)
				{
					continue;
				}
				list[0].SetPairNote(list[1]);
				list[1].SetPairNote(list[0]);
			}
		}
	}
}
