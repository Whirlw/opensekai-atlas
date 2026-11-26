using System;
using System.Collections.Generic;
using LitMotion;
using OpenSekai.App.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// 准备挑战: before the first song every song is found on this device or downloaded, checked
    /// and pinned, so a course never stops halfway for a missing chart. Each song shows whether it
    /// is ready, being prepared, waiting or failed; a failure names the song and offers 重试.
    /// </summary>
    internal sealed class DanPreparePanel : IDisposable
    {
        private const float CARD_WIDTH = 1040f;
        private const float CARD_HEIGHT = 820f;
        private const float LINE_HEIGHT = 72f;
        private const float BUTTON_WIDTH = 220f;

        private readonly RectTransform _root;
        private readonly LibraryModal _modal;
        private readonly MedalPlate _plate;
        private readonly RectTransform _songContent;
        private readonly List<DanSongLine> _lines = new List<DanSongLine>();
        private readonly TextMeshProUGUI _status;
        private readonly CanvasGroup _pulse;
        private readonly Button _cancel;
        private readonly Button _retry;
        private readonly Button _close;
        private IReadOnlyList<DanSongInfo> _songs = Array.Empty<DanSongInfo>();
        private MotionHandle _breathe;
        private int _failedSong;
        private string _lastError;

        internal DanPreparePanel(RectTransform layer, Action cancel, Action retry, Action close)
        {
            _root = LibraryUi.CreateRect("DanPrepare", layer);
            LibraryUi.Stretch(_root);
            _modal = new LibraryModal(_root, new Vector2(CARD_WIDTH, CARD_HEIGHT), "准备挑战", null, withSubtitle: true);
            _modal.Subtitle.text = "准备全部曲目后开始挑战";
            RectTransform body = _modal.Body;
            float width = CARD_WIDTH - LibraryTheme.Layout.MODAL_PADDING * 2f;
            _plate = new MedalPlate(body, "Plate", new Vector2(width - 120f, 84f), glow: true);
            LibraryWidgets.Place(_plate.Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), _plate.Root.sizeDelta);
            RectTransform songs = LibraryUi.CreateRect("Songs", body);
            LibraryWidgets.Inset(songs, 0f, LibraryTheme.Layout.MODAL_BUTTON_HEIGHT + 70f, 0f, 112f);
            _songContent = PackWidgets.Scroll(songs, out _);

            _status = LibraryWidgets.Label("Status", body, string.Empty, 20f, LibraryTheme.Colors.TextSecondary);
            _status.richText = false;
            LibraryWidgets.BottomStrip(_status.rectTransform, 4f, 0f, LibraryTheme.Layout.MODAL_BUTTON_HEIGHT + 18f, 34f);
            _pulse = _status.gameObject.AddComponent<CanvasGroup>();

            float height = LibraryTheme.Layout.MODAL_BUTTON_HEIGHT;
            _cancel = ServerWidgets.LabelledButton(body, "Cancel", "取消", LibraryIcon.Close, () => cancel?.Invoke(), BUTTON_WIDTH, height,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.Cancel);
            LibraryWidgets.Outline((Image)_cancel.targetGraphic, height * 0.5f);
            LibraryWidgets.Place((RectTransform)_cancel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(BUTTON_WIDTH, height));
            _retry = ServerWidgets.LabelledButton(body, "Retry", "重试", LibraryIcon.Refresh, () => retry?.Invoke(), BUTTON_WIDTH, height,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.Retry);
            LibraryWidgets.Place((RectTransform)_retry.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(BUTTON_WIDTH, height));
            _close = LibraryWidgets.PillButton("Close", body, "关闭", () => close?.Invoke(), BUTTON_WIDTH, height,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, sound: UiCue.Close);
            LibraryWidgets.Outline((Image)_close.targetGraphic, height * 0.5f);
            LibraryWidgets.Place((RectTransform)_close.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-(BUTTON_WIDTH + 16f), 0f),
                new Vector2(BUTTON_WIDTH, height));
            _root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        internal void Show(DanDefinition definition, IReadOnlyList<DanSongInfo> songs)
        {
            _modal.Subtitle.text = DanCopy.Rules(definition);
            _songs = songs ?? Array.Empty<DanSongInfo>();
            _plate.SetLook(MedalLook.Of(definition));
            _plate.SetLocked(false);
            const float GAP = 8f;
            while (_lines.Count < _songs.Count)
            {
                var line = new DanSongLine(_songContent, "Song" + _lines.Count, LINE_HEIGHT, manualDownload: true);
                line.Root.anchorMin = new Vector2(0f, 1f);
                line.Root.anchorMax = new Vector2(1f, 1f);
                line.Root.pivot = new Vector2(0.5f, 1f);
                line.Root.sizeDelta = new Vector2(0f, LINE_HEIGHT);
                line.Root.anchoredPosition = new Vector2(0f, -_lines.Count * (LINE_HEIGHT + GAP));
                _lines.Add(line);
            }

            for (int i = 0; i < _lines.Count; i++) _lines[i].Root.gameObject.SetActive(i < _songs.Count);
            _songContent.sizeDelta = new Vector2(0f, _songs.Count * (LINE_HEIGHT + GAP));
            Apply(DanPhase.Preparing, 1, null);
            if (!_modal.IsOpen) _modal.Open();
        }

        internal void RefreshSongs(IReadOnlyList<DanSongInfo> songs)
        {
            if (_failedSong == 0 || songs.Count != _songs.Count) return;
            _songs = songs;
            Apply(DanPhase.Error, _failedSong, _lastError);
        }

        /// <summary>Redraws from the course's state: which song is being prepared, the first song starting, or an error.</summary>
        internal void Apply(DanPhase phase, int songNumber, string error)
        {
            bool failed = phase == DanPhase.Error || error != null;
            _failedSong = failed ? songNumber : 0;
            _lastError = error;
            bool starting = phase == DanPhase.StartingSong || phase == DanPhase.Playing;
            for (int i = 0; i < _songs.Count; i++)
            {
                DanSongInfo info = _songs[i];
                int number = i + 1;
                string tag;
                Color32 color;
                if (starting || number < songNumber)
                {
                    tag = "就绪";
                    color = DanCopy.Life;
                }
                else if (number == songNumber)
                {
                    tag = failed ? (info.Installed && info.RequiresLocalPackage ? "本机已有" : "失败") : "准备中";
                    color = failed ? LibraryTheme.Colors.Danger : HomeTheme.Colors.Violet;
                }
                else
                {
                    tag = "等待";
                    color = LibraryTheme.Colors.TextMuted;
                }

                string meta = info.Installed
                    ? "本机已有" + (info.Difficulty != null ? "  ·  " + info.Difficulty : string.Empty)
                    : info.Host + "  ·  " + info.Availability;
                _lines[i].Bind(number, info.Title, meta, tag, color,
                    number == songNumber && !starting ? HomeTheme.Colors.WithAlpha(color, 60) : HomeTheme.Colors.Chip,
                    downloadPage: failed ? info.ManualDownloadPage : null);
            }

            _breathe.TryCancel();
            _pulse.alpha = 1f;
            if (failed)
            {
                _status.text = "第 " + songNumber + " 首没有准备好：" + (error ?? "未知错误");
                _status.color = LibraryTheme.Colors.Danger;
            }
            else
            {
                _status.text = starting
                    ? "全部就绪，正在进入第 1 首…"
                    : "正在准备第 " + songNumber + " / " + _songs.Count + " 首…";
                _status.color = LibraryTheme.Colors.TextSecondary;
                _breathe = LMotion.Create(0f, Mathf.PI * 2f, 1.6f)
                    .WithLoops(-1)
                    .WithScheduler(HomeMotion.Scheduler)
                    .Bind(p => _pulse.alpha = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Cos(p)));
            }

            _cancel.gameObject.SetActive(!failed && !starting);
            _retry.gameObject.SetActive(failed);
            _close.gameObject.SetActive(failed);
        }

        internal void Hide()
        {
            _breathe.TryCancel();
            _breathe = default;
            _modal.Close();
        }

        public void Dispose()
        {
            _breathe.TryCancel();
            _plate.Dispose();
            _modal.Dispose();
        }
    }
}
