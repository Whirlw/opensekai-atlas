using System;
using OpenSekai.Shared.Playback;

namespace Sekai.Core.Live
{
    public partial class SoloLiveController
    {
        private PracticePlan PracticePlan => (BootData as FreeLiveBootData)?.Practice;
        private PracticePlayback _practice;

        private void StartPractice()
        {
            var music = BootData.MusicData;
            double first = timelineSpan.HasNotes ? timelineSpan.FirstNoteSeconds : 0;
            double min = Math.Min(0, music.AudioToChartTime(Math.Min(0, first)));
            double endAudio = Math.Max(timelineSpan.EndSeconds, timelineSpan.PlaybackEndSeconds);
            double max = Math.Max(min, music.AudioToChartTime(endAudio));
            _practice = new PracticePlayback(PracticePlan, min, max,
                () => music.AudioToChartTime(currentMusicTimeMs / 1000d),
                PracticeJump, PracticeRate, PracticeBoundary, PracticeHold, PracticeContinue, QuitPractice);
            _practice.Restart();
            PracticePlan.CreateControls(transform, _practice);
            _liveLogic.Input.PracticeInputArea = GetComponentInChildren<IPracticeInputArea>();
        }

        protected override void OnBackKey()
        {
            if (_practice == null) base.OnBackKey();
            else _practice.TogglePause();
        }

        private void QuitPractice()
        {
            if (state == LiveControllerState.Finish) return;
            state = LiveControllerState.Finish;
            Exit();
        }
    }
}
