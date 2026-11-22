using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The demo on a page: the live drawn small in the screen's shape, a title saying which choice
    /// it plays, a line under it, and the sound switch. Every page shares the screen's one
    /// <see cref="PackDemo"/>; only the page on top runs it.
    /// </summary>
    internal sealed class PackDemoView : IDisposable
    {
        private const float PAD = 26f;
        private const float HEADER = 74f;
        private const float FOOTER = 58f;
        private const float SOUND_WIDTH = 168f;
        private const float SOUND_HEIGHT = 52f;

        private readonly PackDemo _demo;
        private readonly RawImage _image;
        private readonly AspectRatioFitter _fitter;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _footer;
        private readonly TextMeshProUGUI _status;
        private readonly RectTransform _busy;
        private readonly Button _sound;

        internal PackDemoView(RectTransform parent, PackDemo demo, Color32 tint)
        {
            _demo = demo;
            Frame = new HomeCardFrame(parent, "Demo", 26f, null, 1f);
            RectTransform content = Frame.Content;

            TextMeshProUGUI eyebrow = PackWidgets.Eyebrow(content, "LIVE DEMO", tint, 18f, 22f);
            LibraryWidgets.TopStrip(eyebrow.rectTransform, PAD, SOUND_WIDTH + PAD + 20f, PAD - 8f, 24f);
            _title = PackWidgets.UserLabel("Title", content, string.Empty, 26f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(_title.rectTransform, PAD, SOUND_WIDTH + PAD + 20f, PAD + 16f, 38f);
            _sound = ServerWidgets.LabelledButton(content, "Sound", "声音 关", LibraryIcon.Wave, ToggleSound, SOUND_WIDTH, SOUND_HEIGHT,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextSecondary, UiCue.None, LibraryTheme.FontSize.CAPTION);
            LibraryWidgets.Place((RectTransform)_sound.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-PAD + 6f, -PAD + 8f),
                new Vector2(SOUND_WIDTH, SOUND_HEIGHT));

            // The screen keeps the live's shape inside whatever room the card leaves it.
            RectTransform area = LibraryUi.CreateRect("ScreenArea", content);
            LibraryWidgets.Inset(area, PAD, PAD + FOOTER, PAD, PAD + HEADER);
            RectTransform screen = LibraryUi.CreateRect("Screen", area);
            LibraryUi.Stretch(screen);
            _fitter = screen.gameObject.AddComponent<AspectRatioFitter>();
            _fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            _fitter.aspectRatio = demo.Aspect;
            Image mask = LibraryWidgets.Panel("Mask", screen, 18f, new Color32(8, 10, 16, 255));
            LibraryUi.Stretch(mask.rectTransform);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _image = new GameObject("Live", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            _image.transform.SetParent(mask.transform, false);
            LibraryUi.Stretch(_image.rectTransform);
            _image.raycastTarget = false;
            Image ring = LibraryWidgets.Shape("Ring", screen, LibraryShapes.Ring(18f, 1f), LibraryTheme.Colors.Hairline);
            LibraryUi.Stretch(ring.rectTransform);
            ring.raycastTarget = false;

            _status = PackWidgets.Paragraph(screen, "Status", string.Empty, 20f, LibraryTheme.Colors.TextSecondary);
            _status.alignment = TextAlignmentOptions.Center;
            LibraryWidgets.Inset(_status.rectTransform, 40f, 0f, 40f, 0f);
            _busy = ServerWidgets.Chip(screen, "正在更新演示", LibraryIcon.Clock, new Color32(0, 0, 0, 150), LibraryTheme.Colors.TextPrimary,
                out float busyWidth, 40f);
            LibraryWidgets.Place(_busy, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(busyWidth, 40f));

            _footer = PackWidgets.Paragraph(content, "Footer", string.Empty, 18f, LibraryTheme.Colors.TextMuted);
            _footer.alignment = TextAlignmentOptions.TopLeft;
            // Two lines (the source, then a note) fit the strip only without the paragraph's extra spacing.
            _footer.lineSpacing = 0f;
            LibraryWidgets.BottomStrip(_footer.rectTransform, PAD, PAD, PAD - 6f, FOOTER - 4f);

            demo.Changed += Refresh;
            Refresh();
        }

        internal HomeCardFrame Frame { get; }
        internal RectTransform Root => Frame.Root;

        /// <summary>What the demo plays, and a line under the screen.</summary>
        internal void SetText(string title, string footer)
        {
            _title.text = title ?? string.Empty;
            _footer.text = footer ?? string.Empty;
        }

        internal void Refresh()
        {
            if (_image == null) return;
            _image.texture = _demo.Texture;
            _image.enabled = _demo.Texture != null;
            _fitter.aspectRatio = _demo.Aspect;
            bool running = _demo.IsRunning;
            _status.text = running ? string.Empty
                : _demo.Error != null ? "演示暂时无法播放：" + _demo.Error
                : "正在准备演示…";
            _status.gameObject.SetActive(!running);
            _busy.gameObject.SetActive(running && _demo.IsLoading);
            bool audible = _demo.Audible;
            ServerWidgets.SetLabelledButton(_sound, audible ? "声音 开" : "声音 关", LibraryIcon.Wave,
                audible ? PackWidgets.Accent : LibraryTheme.Colors.Control,
                audible ? LibraryTheme.Colors.TextOnAccent : LibraryTheme.Colors.TextSecondary);
        }

        public void Dispose()
        {
            _demo.Changed -= Refresh;
        }

        private void ToggleSound()
        {
            _demo.Audible = !_demo.Audible;
            UiSound.Play(UiCue.Select);
        }
    }
}
