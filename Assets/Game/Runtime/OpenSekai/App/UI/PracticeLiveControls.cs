using System.Globalization;
using OpenSekai.Shared.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace OpenSekai.App.UI
{
    internal sealed class PracticeLiveControls : MonoBehaviour, IPracticeInputArea
    {
        private IPracticeControls _controls;
        private TMP_InputField _speed;
        private TextMeshProUGUI _pauseLabel;
        private TextMeshProUGUI _position;
        private TextMeshProUGUI _message;
        private LibraryModal _modal;
        private bool _hideCompleted;
        private RectTransform _transport;
        private RectTransform _seeking;
        private PracticeSeekSlider _slider;
        private float _errorUntil;
        private double _displayedRate = double.NaN;
        private int _displayedSecond = int.MinValue;
        private string _state;

        internal static void Create(Transform parent, IPracticeControls controls)
        {
            Create(parent, controls, false);
        }

        internal static void CreateForSong(Transform parent, IPracticeControls controls) => Create(parent, controls, true);

        private static void Create(Transform parent, IPracticeControls controls, bool hideCompleted)
        {
            var root = new GameObject("PracticeControls", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<PracticeLiveControls>();
            view._hideCompleted = hideCompleted;
            view.Build(controls);
        }

        private void Build(IPracticeControls controls)
        {
            _controls = controls;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 190;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var panel = LibraryUi.CreateRect("PauseMenu", transform);
            LibraryUi.Stretch(panel);
            _modal = new LibraryModal(panel, new Vector2(1220f, 408f), "练习暂停", null);
            RectTransform bar = _transport = LibraryUi.CreateRect("Transport", _modal.Body);
            PracticeWidgets.Place(bar, 0f, 0.55f, 1f, 1f);
            var status = LibraryWidgets.Label("Time", bar, "", 21f, LibraryTheme.Colors.TextPrimary,
                alignment: TextAlignmentOptions.Center);
            _position = status;
            PracticeWidgets.Place(status.transform, 0f, 0f, 0.21f, 1f);
            AddButton(bar, "Slower", "−", 0.21f, 0.29f, () => Nudge(-0.1f));
            _speed = LibraryWidgets.TextField("Rate", bar, 54f, "倍速");
            PracticeWidgets.Place(_speed.transform, 0.29f, 0f, 0.43f, 1f);
            _speed.onEndEdit.AddListener(SubmitSpeed);
            AddButton(bar, "Faster", "+", 0.43f, 0.51f, () => Nudge(0.1f));
            var pause = AddButton(bar, "Pause", "暂停", 0.51f, 0.67f, () =>
            {
                if (_controls.Completed) _controls.Restart();
                else _controls.TogglePause();
            });
            _pauseLabel = pause.GetComponentInChildren<TextMeshProUGUI>();
            AddButton(bar, "Restart", "重练片段", 0.67f, 0.85f, _controls.Restart);
            AddButton(bar, "Quit", "退出练习", 0.85f, 1f, _controls.Quit);
            _seeking = LibraryUi.CreateRect("Scrub", _modal.Body);
            PracticeWidgets.Place(_seeking, 0f, 0f, 1f, 0.53f);
            _slider = CreateSlider(_seeking);
            _message = LibraryWidgets.Label("Hint", _seeking, "", 19f, LibraryTheme.Colors.TextSecondary,
                alignment: TextAlignmentOptions.Center);
            PracticeWidgets.Place(_message.transform, 0f, 0f, 1f, 0.46f);
            _message.text = "拖动进度条定位；拖出片段后可自由练习，重练可恢复片段。";
            // Slider uses normalized coordinates so long charts retain the complete range.
            _slider.onValueChanged.AddListener(value =>
            {
                if (!_slider.Scrubbing && (_controls.Paused || _controls.Completed))
                {
                    double from = _controls.MinimumSeconds;
                    _controls.Seek(from + value * (_controls.MaximumSeconds - from));
                }
            });
            Refresh();
        }

        private static Button AddButton(RectTransform parent, string id, string text, float left, float right,
            UnityEngine.Events.UnityAction action)
        {
            var button = PracticeWidgets.Button(parent, id, text, action);
            PracticeWidgets.Place(button.transform, left, 0f, right, 1f);
            return button;
        }

        private static PracticeSeekSlider CreateSlider(RectTransform parent)
        {
            Image hit = LibraryUi.CreateImage("Progress", parent, Color.clear);
            hit.raycastTarget = true;
            PracticeWidgets.Place(hit.transform, 0.03f, 0.46f, 0.97f, 1f);
            var track = LibraryWidgets.Panel("Track", hit.transform, 4f, LibraryTheme.Colors.Control);
            PracticeWidgets.Place(track.transform, 0f, 0.30f, 1f, 0.70f);
            var travel = LibraryUi.CreateRect("Travel", hit.transform);
            LibraryWidgets.Inset(travel, 16f, 0f, 16f, 0f);
            var handle = LibraryWidgets.Panel("Handle", travel, 14f, LibraryTheme.Colors.DefaultAccent);
            handle.rectTransform.sizeDelta = new Vector2(28f, -8f);
            var slider = hit.gameObject.AddComponent<PracticeSeekSlider>();
            slider.targetGraphic = handle;
            slider.handleRect = handle.rectTransform;
            slider.transition = Selectable.Transition.None;
            return slider;
        }

        private void Nudge(float change)
        {
            _controls.SetRate(Mathf.Clamp((float)System.Math.Round(_controls.Rate + change, 5),
                PracticeStep.MIN_SPEED, PracticeStep.MAX_SPEED));
        }

        private void SubmitSpeed(string value)
        {
            try { _controls.SetRate(PracticeForm.Speed(value)); }
            catch (System.FormatException e)
            {
                _position.text = e.Message;
                _errorUntil = Time.unscaledTime + 2f;
                _displayedSecond = int.MinValue;
            }
            _displayedRate = double.NaN;
        }

        public bool ContainsControl(Vector2 position) =>
            _modal.IsOpen;

        private void OnDestroy() => _modal?.Dispose();

        private void Update() => Refresh();

        private void Refresh()
        {
            if (_controls == null) return;
            bool visible = _controls.Paused || (_controls.Completed && !_hideCompleted);
            if (visible != _modal.IsOpen)
            {
                if (visible) _modal.Open();
                else _modal.Close();
            }
            if (!visible) return;
            _modal.Title.text = _controls.Completed ? "练习完成" : "练习暂停";
            if (!_speed.isFocused && _displayedRate != _controls.Rate)
            {
                _displayedRate = _controls.Rate;
                _speed.SetTextWithoutNotify(((float)_displayedRate).ToString("G", CultureInfo.InvariantCulture));
            }

            string state = _controls.Completed ? "完成" : _controls.Paused ? "暂停" : _controls.Resting ? "休息" : "练习";
            double duration = _controls.MaximumSeconds - _controls.MinimumSeconds;
            double position = _slider.Scrubbing ? _controls.MinimumSeconds + duration * _slider.value : _controls.PositionSeconds;
            int second = Mathf.FloorToInt((float)position);
            if (Time.unscaledTime >= _errorUntil && (_displayedSecond != second || _state != state))
            {
                _displayedSecond = second;
                _state = state;
                _position.text = $"{state}  {second} 秒";
                _pauseLabel.text = _controls.Completed ? "重播" : _controls.Paused ? "继续" : "暂停";
            }

            bool seekable = _controls.Paused || _controls.Completed;
            if (seekable != _seeking.gameObject.activeSelf) _seeking.gameObject.SetActive(seekable);
            if (!seekable) return;
            _slider.interactable = duration > 0d;
            if (!_slider.Scrubbing)
                _slider.SetValueWithoutNotify(duration > 0d ? (float)((position - _controls.MinimumSeconds) / duration) : 0f);
        }
    }

    // Seeking rebuilds judgment state and voices. Preview the thumb while a finger moves
    // and perform one seek on release; keyboard/gamepad slider changes apply directly.
    internal sealed class PracticeSeekSlider : Slider
    {
        private int? _pointer;
        internal bool Scrubbing => _pointer.HasValue;

        public override void OnPointerDown(PointerEventData data)
        {
            if (_pointer.HasValue || data.button != PointerEventData.InputButton.Left || !IsInteractable()) return;
            _pointer = data.pointerId;
            base.OnPointerDown(data);
        }

        public override void OnDrag(PointerEventData data)
        {
            if (_pointer == data.pointerId) base.OnDrag(data);
        }

        public override void OnPointerUp(PointerEventData data)
        {
            if (_pointer != data.pointerId || data.button != PointerEventData.InputButton.Left) return;
            base.OnPointerUp(data);
            _pointer = null;
            onValueChanged.Invoke(value);
        }

        protected override void OnDisable()
        {
            _pointer = null;
            base.OnDisable();
        }
    }
}
