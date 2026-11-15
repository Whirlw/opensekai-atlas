using Sekai.Live;
using System.Collections.Generic;
using UnityEngine;
using Sekai;

namespace Sekai.Core.Live
{
	public class LiveViewBase : MonoBehaviour
	{
		public virtual void Setup(BaseLiveController controller)
		{
		}

		public virtual void OnLoad()
		{
		}

		public virtual void OnUnload()
		{
		}

		public virtual void MusicStart(float musicTime)
		{
		}

		public virtual void RhythmGameStart()
		{
		}

		/// <summary>
		/// False while this view still shows the song's info page. The live clock does not start until
		/// every view is done with it.
		/// </summary>
		public virtual bool IsIntroFinished => true;

		/// <summary>
		/// How long the info page this view puts up stays, from <see cref="MusicStart"/> until it is
		/// completely gone. The live starts its clock that far ahead of the start point.
		/// </summary>
		public virtual float IntroDurationSeconds => 0f;

		/// <summary>The audio itself starts, at this music time. It can come after the clock started.</summary>
		public virtual void MusicPlaybackStart(float musicTime)
		{
		}

		/// <summary>The clock and the audio jumped to this music time (the intro skip).</summary>
		public virtual void MusicSeek(float musicTime)
		{
		}

		public virtual void OnUpdate(float musicTime)
		{
		}

		public virtual void CreateNotePool(Dictionary<(NoteCategory, NoteType), int> notePoolCount)
		{
		}

		public virtual void SpawnNote(NoteBase note)
		{
		}

		public virtual void UnspawnNote(NoteBase note)
		{
		}

		public virtual void JudgmentNote(NoteBase note)
		{
		}

		/// <summary>
		/// Refreshes the judgement text only. A note whose result is silent by design (an interior
		/// marker nobody drew) still says what it was judged as; its effect and its sound stay off.
		/// </summary>
		public virtual void JudgmentText(NoteBase note)
		{
		}

		public virtual void InputLane(int lane)
		{
		}

		public virtual void Unpicked(int lane, ref LiveTouch touch)
		{
		}

		public virtual void UpdateCombo(LiveScore score)
		{
		}

		public virtual void SetupScore(LiveScore score)
		{
		}

		public virtual void UpdateScore(LiveScore score)
		{
		}

		public virtual void UpdateScore(ref LiveScore score, int addScore)
		{
			UpdateScore(score);
		}

		public virtual void UpdateLife(LiveScore score)
		{
		}

		public virtual void Result(int result)
		{
		}

		public virtual void OnFailure(float time)
		{
		}

		public virtual void Pause()
		{
		}

		public virtual void Pause(float time)
		{
			Pause();
		}

		public virtual void Countdown()
		{
		}

		public virtual void Resume()
		{
		}

		public virtual void Resume(float time)
		{
		}

		public virtual void Retry()
		{
		}

		public virtual void Finish()
		{
		}

		public virtual void Finish(float duration)
		{
			Finish();
		}

		public virtual void Finish3D()
		{
		}
	}
}
