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
				else if (value == NoteState.Scrolling)
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
				State = NoteState.Scrolling;
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

			if (State == NoteState.Scrolling && LiveConfig.JudgeTime < currentFrameInfo.time - MusicScoreInfo.time)
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

		/// <summary>
		/// The time of the bar's last point: the place its body is drawn to. A guide bar owns no
		/// verdict, so this is the only fact that says how long the bar is on screen.
		/// </summary>
		private float BodyEndTime
		{
			get
			{
				if (NoteList == null || NoteList.Count == 0)
				{
					return MusicScoreInfo.time;
				}

				return NoteList[NoteList.Count - 1].MusicScoreInfo.time;
			}
		}

		public override bool AutoJudgment(MusicScoreInfo currentFrameInfo)
		{
			if (MusicScoreInfo.time > currentFrameInfo.time)
			{
				return false;
			}

			// A guide note is not scored, but a bar that starts with one can still end with a real key.
			// Autoplay has no finger to swipe with, so the tail has to be judged here or that key is
			// the only note in the chart which autoplay can never hit.
			if (childNote == null || !childNote.HasJudgment)
			{
				// Nothing at the end can be played, so there is no verdict to wait for: the bar simply
				// ends where it ends. Without this the note stayed alive forever and the session's cursor
				// never walked past it - autoplay scored full marks and then never settled.
				//
				// "Where it ends" is the bar's last point, not its head. Ending the bar the frame its
				// head crossed the line took the whole body off the screen at once: on Test16 the
				// decorative bars vanished on the judgement line while their tails were still up to a
				// second away. The bar is drawn until that last point passes, so it now ends there.
				IsAutoPlaying = true;
				if (currentFrameInfo.time < BodyEndTime)
				{
					return false;
				}

				State = NoteState.Done;
				return true;
			}

			// The end key may have settled itself already (a rub, a hidden carrier): a second
			// AutoJudgment on it answers "no", and waiting for a "yes" left the bar alive forever.
			if (childNote.State != NoteState.Done && !childNote.AutoJudgment(currentFrameInfo))
			{
				return false;
			}

			IsAutoPlaying = true;
			State = NoteState.Done;
			return true;
		}
	}
}
