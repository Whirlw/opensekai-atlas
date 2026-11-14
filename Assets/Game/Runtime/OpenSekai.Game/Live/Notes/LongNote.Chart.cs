using System;
using System.Collections.Generic;
using System.Linq;
using OpenSekai.Shared.Live;

namespace Sekai.Live
{
	public partial class LongNote
	{
		public override Action<NoteBase> OnUpdateScore
		{
			set
			{
				onUpdateScore = value;
				ApplyToChildNotes(note => note.OnUpdateScore = value);
			}
		}

		public override Action<NoteBase> OnUpdateCombo
		{
			set
			{
				onUpdateCombo = value;
				ApplyToChildNotes(note => note.OnUpdateCombo = value);
			}
		}

		public override Action<NoteBase> OnDamage
		{
			set
			{
				onDamage = value;
				ApplyToChildNotes(note => note.OnDamage = value);
			}
		}

		public override Action<NoteBase> OnSpawn
		{
			set
			{
				onSpawn = value;
				ApplyToChildNotes(note => note.OnSpawn = value);
			}
		}

		public override Action<NoteBase> OnJudgment
		{
			set
			{
				onJudgment = value;
				ApplyToChildNotes(note => note.OnJudgment = value);
			}
		}

		public override Action<NoteBase> OnUnspawn
		{
			set
			{
				onUnspawn = value;
				ApplyToChildNotes(note => note.OnUnspawn = value);
			}
		}

		public override Func<(NoteResult result, NoteResultDescription description), (NoteResult result, NoteResultDescription description)> OnUpdateResult
		{
			set
			{
				onUpdateResult = value;
				ApplyToChildNotes(note => note.OnUpdateResult = value);
			}
		}

		public LongNote()
		{
			NoteList = new List<NoteBase> { this };
			ViewNoteList = new List<INote> { this };
			LineNoteList.Add(this);
			Category = NoteCategory.Long;
			Type = NoteType.Default;
		}

		public LongNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, NoteType type, float speedRatio, float laneOffset, NoteLineType lineType = NoteLineType.Linear)
			: base(info, id, laneStart, laneEnd, category, speedRatio, laneOffset, type)
		{
			LineType = lineType;
			LaneOffset = laneOffset;
			LineNoteList.Add(this);
		}

		public void AddConnectionNote(NoteBase connectionNote)
		{
			if (connectionNote == null)
			{
				return;
			}

			NoteList ??= new List<NoteBase> { this };
			ViewNoteList ??= new List<INote> { this };
			connectionNote.speedRatio = speedRatio;
			// 继承父note的装饰属性
			connectionNote.SetDecoration(IsDecoration);
			NoteList.Add(connectionNote);
			ViewNoteList.Add(connectionNote);
			LineNoteList.Add(connectionNote);
			connectionNote.SetParentNote(this);
		}

		public void AddHoldCombo(LongHoldCombo longHoldCombo)
		{
			if (longHoldCombo == null)
			{
				return;
			}

			NoteList ??= new List<NoteBase> { this };
			longHoldCombo.speedRatio = speedRatio;
			// 继承父note的装饰属性
			longHoldCombo.SetDecoration(IsDecoration);
			// The point sits on the bar's lane, so it has to judge against the bar's lane too: the
			// constructor left it at lane zero, which is why points on any other lane never matched.
			longHoldCombo.LaneStart = LaneStart;
			longHoldCombo.LaneEnd = LaneEnd;
			longHoldCombo.LaneStartF = LaneStartF;
			longHoldCombo.LaneEndF = LaneEndF;
			longHoldCombo.JudgeLaneStart = JudgeLaneStart;
			longHoldCombo.JudgeLaneEnd = JudgeLaneEnd;
			NoteList.Add(longHoldCombo);
			longHoldCombo.SetParentNote(this);
		}

		public void AddNoteListAndSetupChildNote(NoteBase note)
		{
			if (note == null)
			{
				return;
			}

			bool independentTail = IsIndependentTail(note);
			// A per-hold scroll multiplier applies to the full chain, including its endpoint.
			note.speedRatio = speedRatio;
			// 装饰属性属于长条自己：尾部不继承。装饰长条自己滚过去、不接受输入，但它的结束音
			// 仍然是一个真正的音符（划键、滑动、摩擦……），要按它本来的类型来玩。
			note.SetDecoration(false);
			// The converter hands us every note it walks: the head, combo ticks, hidden markers and the
			// real end note. Only the end note may become the child, otherwise a hidden marker that sits
			// after the tail would take its place and the bar would never learn how it ends.
			if (childNote == null || IsTailCandidate(note))
			{
				childNote = note;
			}

			_lastBoundNote = note;
			NoteList ??= new List<NoteBase> { this };
			ViewNoteList ??= new List<INote> { this };
			// The draw list holds every note the bar owns. Whether a note shows anything is the note's own
			// business; settlement is decided separately, in SettleLeftoverAsPerfect, where only the points
			// the bar itself ends are graded.
			if (independentTail)
			{
				note.BindGeometryParent(this);
			}
			else
			{
				note.SetParentNote(this);
			}
			NoteList.Add(note);

			if (!note.IsSkip && !ViewNoteList.Contains(note))
			{
				ViewNoteList.Add(note);
			}

			LineNoteList.Add(note);
			SortNoteList();
		}

		public void SortNoteList()
		{
			if (NoteList != null)
			{
				NoteList.Sort((a, b) => a.MusicScoreInfo.time.CompareTo(b.MusicScoreInfo.time));
			}

			LineNoteList = LineNoteList.OrderBy(note => note.MusicScoreInfo.time).ToList();

			if (ViewNoteList != null)
			{
				ViewNoteList = ViewNoteList.OrderBy(note => note.MusicScoreInfo.time).ToList();
			}

			// The judged-point list is derived from NoteList and its ordering.
			_judgedPoints = null;
		}

		private void ApplyToChildNotes(Action<NoteBase> apply)
		{
			if (NoteList == null || apply == null)
			{
				return;
			}

			foreach (var note in NoteList)
			{
				if (!ReferenceEquals(note, this))
				{
					apply(note);
				}
			}
		}
	}
}
