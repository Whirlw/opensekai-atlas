namespace Sekai.Live
{
	public class GuideNote : LongNote
	{
		/// <summary>
		/// A decorative bar never grades the note at its end: that note belongs to the player.
		/// </summary>
		protected override bool SkipsTailJudgement => childNote != null && childNote.HasJudgment;

		public override bool HasJudgment
		{
			get
			{
				return false;
			}
		}

		public override NoteState State
		{
			get
			{
				return state;
			}
			protected set
			{
				if (value == NoteState.Done && state == NoteState.Done) return;
				state = value;
				if (value == NoteState.Done)
				{
					OnUnSpawnNote();
				}
				else if (value == NoteState.Playing)
				{
					OnSpawnNote();
				}
			}
		}

		public GuideNote()
		{
			Category = NoteCategory.Guide;
		}

		public GuideNote(MusicScoreInfo info, int id, int laneStart, int laneEnd, NoteCategory category, NoteType type, LiveBundleBuildData bundleBuildData, float speedRatio, NoteLineType lineType = NoteLineType.Linear)
			: base(info, id, laneStart, laneEnd, category, type, speedRatio, LiveUtility.GetLaneOffset(category, bundleBuildData), lineType)
		{
		}

		public override void Excute(MusicScoreInfo currentFrameInfo, float offsetTime)
		{
			this.offsetTime = offsetTime;
			if (State == NoteState.Wait)
			{
				State = NoteState.Playing;
			}

			if (State == NoteState.Last)
			{
				JudgeInfo = (NoteResult.Pass, NoteResultDescription.None);
			}

			Progress = CalcProgress(currentFrameInfo, offsetTime);
			if (NoteList != null)
			{
				for (var i = 1; i < NoteList.Count; i++)
				{
					var note = NoteList[i];
					var childOffsetTime = CalcTimeOffset?.Invoke(note) ?? offsetTime;
					// Same boundary as LongNote.Excute: a note becomes live on the frame its lead time
					// is consumed, not one frame later. The tail key is spawned from here, and a bar
					// that waits an extra frame shows its key an extra frame late.
					if (currentFrameInfo.time >= note.MusicScoreInfo.time - childOffsetTime)
					{
						note.Excute(currentFrameInfo, childOffsetTime);
					}
				}
			}

			var currentNote = (INote)this;
			var nextNote = (INote)childNote;
			LiveUtility.CalcExcuteNoteLane(this, ref currentNote, ref nextNote, ref currentFrameInfo, MusicScoreInfo, childNote);

			if (State == NoteState.Playing && LiveConfig.JudgeTime < currentFrameInfo.time - MusicScoreInfo.time)
			{
				State = NoteState.Last;
			}
		}

		public override bool IsJudgment(ref LiveTouch touch, float lane)
		{
			return false;
		}

		public override bool IsJudgmentChild(ref LiveTouch touch, float lane)
		{
			return false;
		}

		public override bool Judgment(ref LiveTouch touch, float lane)
		{
			return false;
		}

		protected override void OnSpawnNote()
		{
			onSpawn?.Invoke(this);
		}

		protected override void OnUnSpawnNote()
		{
			ConnectionNoteTerminate();
			RemoveView();
		}

		public override bool AutoJudgment(MusicScoreInfo currentFrameInfo)
		{
			// A guide note is not scored, but a bar that starts with one can still end with a real key.
			// Autoplay has no finger to swipe with, so the tail has to be judged here or that key is
			// the only note in the chart which autoplay can never hit.
			if (childNote == null || !childNote.HasJudgment)
			{
				return false;
			}

			if (!childNote.AutoJudgment(currentFrameInfo))
			{
				return false;
			}

			State = NoteState.Done;
			return true;
		}
	}
}
