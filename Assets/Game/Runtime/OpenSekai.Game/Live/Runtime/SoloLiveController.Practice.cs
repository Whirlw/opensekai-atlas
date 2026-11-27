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
            var boot = (FreeLiveBootData)BootData;
            _practice = new PracticePlayback(PracticePlan, boot.PracticeMinimum, boot.PracticeMaximum,
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
            if (PracticePlan.RecordResult)
            {
                _retiredByPlayer = !_practice.Completed;
                result = _retiredByPlayer ? 1 : 3;
                PreExit(0f, 0f);
                return;
            }
            state = LiveControllerState.Finish;
            Exit();
        }
    }
}
