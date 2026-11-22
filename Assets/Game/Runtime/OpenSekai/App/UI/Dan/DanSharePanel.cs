using System;
using System.Collections.Generic;
using OpenSekai.App.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// 粘贴分享: the text a friend shared, read from the clipboard into an editable field, and what
    /// it describes beside it (the plate, the songs in order, the life and the damage table), or why
    /// it cannot be read. 保存到本机 keeps it; a 段位 that is already here offers 查看 instead.
    /// </summary>
    internal sealed class DanSharePanel : IDisposable
    {
        private const float CARD_WIDTH = 1500f;
        private const float CARD_HEIGHT = 860f;
        private const float LEFT_WIDTH = 600f;
        private const float LINE_HEIGHT = 72f;
        private const float BUTTON_WIDTH = 240f;

        private readonly RectTransform _root;
        private readonly LibraryModal _modal;
        private readonly TMP_InputField _text;
        private readonly RectTransform _preview;
        private readonly DanPlate _plate;
        private readonly TextMeshProUGUI _shape;
        private readonly DanJudgeTable _damage;
        private readonly RectTransform _songContent;
        private readonly List<DanSongLine> _lines = new List<DanSongLine>();
        private readonly RectTransform _problem;
        private readonly TextMeshProUGUI _problemText;
        private readonly Button _save;
        private readonly Func<DanDefinition, bool> _isSaved;
        private readonly Action<DanDefinition> _onSave;
        private readonly Action<DanDefinition> _onShow;
        private DanDefinition _definition;

        /// <param name="isSaved">True when a definition with this id is already on this device.</param>
        internal DanSharePanel(RectTransform layer, Func<DanDefinition, bool> isSaved, Action<DanDefinition> onSave, Action<DanDefinition> onShow)
        {
            _isSaved = isSaved;
            _onSave = onSave;
            _onShow = onShow;
            _root = LibraryUi.CreateRect("DanShare", layer);
            LibraryUi.Stretch(_root);
            _modal = new LibraryModal(_root, new Vector2(CARD_WIDTH, CARD_HEIGHT), "粘贴段位", Close, withSubtitle: true);
            _modal.Subtitle.text = "好友复制的段位分享文本以 " + DanDefinition.PREFIX + " 开头，保存后即可挑战同一个段位";
            RectTransform body = _modal.Body;
            float footer = LibraryTheme.Layout.MODAL_BUTTON_HEIGHT + 20f;

            RectTransform left = LibraryUi.CreateRect("Text", body);
            left.anchorMin = Vector2.zero;
            left.anchorMax = new Vector2(0f, 1f);
            left.pivot = new Vector2(0f, 0.5f);
            left.offsetMin = new Vector2(0f, footer);
            left.offsetMax = new Vector2(LEFT_WIDTH, 0f);
            DanWidgets.Caption(left, "分享文本", 4f, 0f);
            _text = DanWidgets.MultiLineField(left, "Share", "把分享文本粘贴到这里", 17f);
            LibraryWidgets.Inset((RectTransform)_text.transform, 0f, 72f, 0f, 30f);
            _text.onValueChanged.AddListener(_ => Evaluate());
            Button paste = ServerWidgets.LabelledButton(left, "Paste", "从剪贴板粘贴", LibraryIcon.Download, PasteClipboard, LEFT_WIDTH, 60f,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.None, 19f);
            LibraryWidgets.Outline((Image)paste.targetGraphic, 30f);
            LibraryWidgets.Place((RectTransform)paste.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(LEFT_WIDTH, 60f));

            RectTransform right = LibraryUi.CreateRect("Preview", body);
            LibraryWidgets.Inset(right, LEFT_WIDTH + 48f, footer, 0f, 0f);
            _preview = LibraryUi.CreateRect("Content", right);
            LibraryUi.Stretch(_preview);
            float width = CARD_WIDTH - LibraryTheme.Layout.MODAL_PADDING * 2f - LEFT_WIDTH - 48f;
            _plate = new DanPlate(_preview, "Plate", new Vector2(width - 40f, 92f), glow: true);
            LibraryWidgets.Place(_plate.Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), _plate.Root.sizeDelta);
            _shape = LibraryWidgets.Label("Shape", _preview, string.Empty, 20f, LibraryTheme.Colors.TextSecondary);
            LibraryWidgets.TopStrip(_shape.rectTransform, 4f, 0f, 116f, 30f);
            DanWidgets.Caption(_preview, "每个判定的扣血", 4f, 156f);
            _damage = new DanJudgeTable(_preview, "Damage", 26f);
            LibraryWidgets.TopStrip(_damage.Root, 0f, 0f, 184f, 72f);
            DanWidgets.Caption(_preview, "曲目顺序", 4f, 276f);
            RectTransform songs = LibraryUi.CreateRect("Songs", _preview);
            LibraryWidgets.Inset(songs, 0f, 0f, 0f, 306f);
            _songContent = PackWidgets.Scroll(songs, out _);

            _problem = LibraryUi.CreateRect("Problem", right);
            LibraryUi.Stretch(_problem);
            Image icon = LibraryWidgets.Icon(_problem, LibraryIcon.Info, LibraryTheme.Colors.TextMuted, 64f);
            icon.rectTransform.anchoredPosition = new Vector2(0f, 70f);
            _problemText = LibraryWidgets.Label("Message", _problem, string.Empty, 22f, LibraryTheme.Colors.TextSecondary,
                FontStyles.Normal, TextAlignmentOptions.Top);
            _problemText.textWrappingMode = TextWrappingModes.Normal;
            _problemText.richText = false;
            LibraryWidgets.Place(_problemText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 10f),
                new Vector2(width - 80f, 160f));

            float height = LibraryTheme.Layout.MODAL_BUTTON_HEIGHT;
            _save = ServerWidgets.LabelledButton(body, "Save", "保存到本机", LibraryIcon.Check, OnSaveClicked, BUTTON_WIDTH, height,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.None);
            LibraryWidgets.Place((RectTransform)_save.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(BUTTON_WIDTH, height));
            Button cancel = LibraryWidgets.PillButton("Cancel", body, "取消", Close, 200f, height,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, sound: UiCue.Close);
            LibraryWidgets.Outline((Image)cancel.targetGraphic, height * 0.5f);
            LibraryWidgets.Place((RectTransform)cancel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-(BUTTON_WIDTH + 16f), 0f), new Vector2(200f, height));
            _root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        /// <summary>Opens on <paramref name="text"/>, or on the clipboard when it holds a shared 段位.</summary>
        internal void Open(string text = null)
        {
            if (text == null)
            {
                string clipboard = GUIUtility.systemCopyBuffer;
                text = DanCopy.IsShareText(clipboard) ? clipboard : string.Empty;
            }

            _text.SetTextWithoutNotify(text);
            Evaluate();
            _modal.Open();
        }

        internal void Close()
        {
            _text.DeactivateInputField();
            _modal.Close();
        }

        public void Dispose()
        {
            _plate.Dispose();
            _modal.Dispose();
        }

        private void PasteClipboard()
        {
            string clipboard = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(clipboard))
            {
                UiSound.Play(UiCue.Blocked);
                ShowProblem("剪贴板是空的。请先复制好友发来的段位分享文本");
                return;
            }

            UiSound.Play(UiCue.Select);
            _text.text = clipboard;
        }

        private void Evaluate()
        {
            string text = (_text.text ?? string.Empty).Trim();
            _definition = null;
            if (text.Length == 0)
            {
                ShowProblem("粘贴分享文本后，这里会显示段位牌、曲目顺序和扣血");
                return;
            }

            try
            {
                _definition = DanDefinition.Parse(text);
            }
            catch (DanFieldException exception)
            {
                ShowProblem(DanCopy.Field(exception));
                return;
            }

            _problem.gameObject.SetActive(false);
            _preview.gameObject.SetActive(true);
            DanDefinition definition = _definition;
            _plate.SetStyle(definition.Label, definition.Emblem, definition.Color);
            _plate.SetLocked(false);
            _shape.text = DanCopy.Shape(definition);
            for (int i = 0; i < 5; i++) _damage.Set(i, DanCopy.Cost(DanCopy.Damage(definition.Damage, i)));
            BindSongs(definition);
            bool saved = _isSaved(definition);
            ServerWidgets.SetLabelledButton(_save, saved ? "已在本机 · 查看" : "保存到本机", saved ? LibraryIcon.ChevronRight : LibraryIcon.Check,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent);
            PackWidgets.SetEnabled(_save, true);
        }

        private void BindSongs(DanDefinition definition)
        {
            const float GAP = 8f;
            while (_lines.Count < definition.Songs.Count)
            {
                var line = new DanSongLine(_songContent, "Song" + _lines.Count, LINE_HEIGHT);
                line.Root.anchorMin = new Vector2(0f, 1f);
                line.Root.anchorMax = new Vector2(1f, 1f);
                line.Root.pivot = new Vector2(0.5f, 1f);
                line.Root.sizeDelta = new Vector2(0f, LINE_HEIGHT);
                line.Root.anchoredPosition = new Vector2(0f, -_lines.Count * (LINE_HEIGHT + GAP));
                _lines.Add(line);
            }

            for (int i = 0; i < _lines.Count; i++)
            {
                bool used = i < definition.Songs.Count;
                _lines[i].Root.gameObject.SetActive(used);
                if (!used) continue;
                DanSongInfo info = DanSongInfo.Resolve(definition.Songs[i]);
                _lines[i].Bind(i + 1, info.Title, info.Host + "  ·  " + info.LevelName,
                    info.Installed ? "本机已有" : "开打前下载", info.Installed ? DanCopy.Life : LibraryTheme.Colors.TextMuted, HomeTheme.Colors.Chip);
            }

            _songContent.sizeDelta = new Vector2(0f, definition.Songs.Count * (LINE_HEIGHT + GAP));
        }

        private void ShowProblem(string message)
        {
            _definition = null;
            _preview.gameObject.SetActive(false);
            _problem.gameObject.SetActive(true);
            _problemText.text = message;
            ServerWidgets.SetLabelledButton(_save, "保存到本机", LibraryIcon.Check, LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent);
            PackWidgets.SetEnabled(_save, false);
        }

        private void OnSaveClicked()
        {
            DanDefinition definition = _definition;
            if (definition == null)
            {
                UiSound.Play(UiCue.Blocked);
                return;
            }

            Close();
            if (_isSaved(definition)) _onShow(definition);
            else _onSave(definition);
        }
    }
}
