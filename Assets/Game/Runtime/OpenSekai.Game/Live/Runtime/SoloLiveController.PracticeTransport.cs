using System;
using OpenSekai.Shared.Playback;
using Sekai.Live;
using UnityEngine;

namespace Sekai.Core.Live
{
	public partial class SoloLiveController
	{
		private bool _practiceHeld;
		private long _practiceEndMs = long.MaxValue;
		private double _practiceDspEnd = double.PositiveInfinity;

		private long PracticeTimelineMs(double chartSeconds) =>
			(long)Math.Round(BootData.MusicData.ChartToAudioTime(chartSeconds) * 1000d);

		private void PracticeRate(double rate)
		{
			if (!(rate >= PracticeStep.MIN_SPEED && rate <= PracticeStep.MAX_SPEED)) throw new ArgumentOutOfRangeException(nameof(rate));
			ChangePracticeRate(rate, Time.realtimeSinceStartupAsDouble);
			_liveLogic.Clock.Rates = PracticeRates;
			_liveLogic.Clock.InputWallOffset = PracticeInputWallOffset;
			_liveLogic.Clock.PlaybackRate = rate;
			SoundManager.Instance.SetIngameBgmPitch((float)rate);
			ArmPracticeCut();
		}

		private void ResetPracticeRate()
		{
			TimelinePlaybackRate = 1d;
			if (_liveLogic != null)
			{
				_liveLogic.Clock.PlaybackRate = 1d;
				_liveLogic.Clock.Rates = null;
			}
			SoundManager.Instance.StopIngame();
			_practiceDspEnd = double.PositiveInfinity;
			_practiceEndMs = long.MaxValue;
			_practiceHeld = false;
		}

		internal static long PracticeEndTimelineMs(LiveMusicData music, double endChartSeconds)
		{
			// The public timeline is millisecond-resolution. The first reachable tick at
			// or after the user's end must pass the sequence's double-precision comparison.
			if (double.IsPositiveInfinity(endChartSeconds)) return long.MaxValue;
			long endMs = (long)Math.Ceiling(music.ChartToAudioTime(endChartSeconds) * 1000d);
			return music.AudioToChartTime(endMs / 1000d) < endChartSeconds ? endMs + 1 : endMs;
		}

		private void PracticeBoundary(double endChartSeconds)
		{
			_practiceEndMs = PracticeEndTimelineMs(BootData.MusicData, endChartSeconds);
			ArmPracticeCut();
		}

		private void ArmPracticeCut()
		{
			_practiceDspEnd = double.PositiveInfinity;
			if (_practiceHeld || musicPhase != MusicPhase.Playing) return;
			// Even a free take has a finite clip. Replacing a shorter scheduled stop with
			// the physical file end also cancels that old stop when the segment is extended.
			long end = _practiceEndMs == long.MaxValue ? musicLength : _practiceEndMs;
			double stop = SoundManager.Instance.ScheduleIngameEnd(Math.Min(end, musicLength) / 1000d);
			if (_practiceEndMs != long.MaxValue && end <= musicLength) _practiceDspEnd = stop;
		}

		private void PracticeJump(double chartSeconds, bool playing)
		{
			SetTimelinePosition(PracticeTimelineMs(chartSeconds));
			ResetPracticeHistory();
			RefreshPracticeViewLead();
			_practiceHeld = !playing;
			foreach (LiveViewBase view in _liveViews)
				if (view is LiveFrontView front) front.ClearPracticeFrame();
			_liveLogic.SeekPractice((float)chartSeconds);
			// Drawing a paused scrub must never hit the key at the cursor, even in autoplay.
			_liveLogic.PreviewPractice(CurrentLogicChartTime, currentGameTime);
			LiveViewExt.MusicSeek(_liveViews, CurrentViewTimeSeconds);
			LiveViewExt.OnUpdate(_liveViews, CurrentViewTimeSeconds);
			if (playing) PracticeContinue();
			else PracticeHold();
		}

		private void PracticeHold()
		{
			_practiceHeld = true;
			_practiceDspEnd = double.PositiveInfinity;
			SoundManager.Instance.Pause();
			_liveLogic?.ConnectorAudio.Silence();
			LiveViewExt.Pause(_liveViews);
		}

		internal void PracticeContinue()
		{
			_practiceHeld = false;
			ResetPracticeHistory();
			LiveViewExt.Resume(_liveViews, CurrentViewTimeSeconds);
			// Prepare a new transport for every take. The old source may have reached its
			// scheduled stop and returned to the pool, or still have a deadline from before a seek.
			long start = Math.Max(0, BootData.MusicData.StartMusicTimeMs);
			long playback = currentMusicTimeMs;
			if (playback >= musicLength)
			{
				SoundManager.Instance.StopIngame();
				musicPhase = MusicPhase.Ended;
				return;
			}

			musicPhase = playback < start ? MusicPhase.RunIn : MusicPhase.Playing;
			string cue = ResolveMusicCueName();
			if (string.IsNullOrEmpty(cue))
			{
				musicPhase = MusicPhase.Ended;
				return;
			}

			cueId = SoundManager.Instance.PrepareIngameBGM(cue, Math.Max(start, playback) / 1000f);
			SoundManager.Instance.SetIngameBgmPitch((float)TimelinePlaybackRate);
			if (musicPhase == MusicPhase.Playing)
			{
				SoundManager.Instance.ResumePreparedPlaybackIngame();
				ArmPracticeCut();
			}
		}

		protected override void UpdateMusicTime()
		{
			if (_practiceHeld) return;
			MusicPhase previous = musicPhase;
			base.UpdateMusicTime();
			if (_practice == null) return;
			RefreshPracticeViewLead();
			if (previous == MusicPhase.RunIn && musicPhase == MusicPhase.Playing) ArmPracticeCut();
			currentMusicTimeMs = LimitPracticeTimeMs(currentMusicTimeMs, _practiceEndMs, AudioSettings.dspTime, _practiceDspEnd);
		}

		internal static long LimitPracticeTimeMs(long timeMs, long segmentEndMs, double dspNow, double scheduledEnd)
		{
			if (double.IsPositiveInfinity(scheduledEnd)) return Math.Min(segmentEndMs, timeMs);
			// The approximate presentation clock can lead the mixer's actual samples by
			// a buffer. Keep the segment open until its scheduled last sample is played.
			return dspNow >= scheduledEnd ? segmentEndMs : Math.Min(segmentEndMs - 1, timeMs);
		}
	}
}
