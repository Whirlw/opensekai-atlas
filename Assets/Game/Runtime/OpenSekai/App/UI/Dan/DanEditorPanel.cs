using System;
using System.Collections.Generic;
using System.Globalization;
using OpenSekai.App.Playback;
using OpenSekai.Shared.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// 新建段位 / 编辑段位: the songs in order (a share link each, moved with the arrows), the
    /// starting life and what each judgment costs, and the plate (its text, emblem and color) with a
    /// live preview. Nothing is saved until 保存; the definition itself checks every field, and the
    /// field it refuses is outlined with the reason under the form. 粘贴链接 adds every level link
    /// on the clipboard, or loads a whole shared 段位.
    /// </summary>
    internal sealed class DanEditorPanel : IDisposable
    {
        private const float CARD_WIDTH = 1640f;
        private const float CARD_HEIGHT = 880f;
        private const float LEFT_WIDTH = 790f;
        private const float RIGHT_LEFT = 850f;
        private const float ROW_HEIGHT = 100f;
        private const float ROW_GAP = 10f;
        private const float FIELD_HEIGHT = 60f;
        private const float TILE = 70f;
        private const float SWATCH = 44f;
        private const float BUTTON_WIDTH = 200f;

        private readonly RectTransform _root;
        private readonly LibraryModal _modal;
        private readonly RectTransform _songContent;
        private readonly ScrollRect _songScroll;
        private readonly TextMeshProUGUI _songCount;
        private readonly List<SongRow> _rows = new List<SongRow>();
        private readonly List<string> _links = new List<string>();
        private readonly DanPlate _preview;
        private readonly TMP_InputField _label;
        private readonly TMP_InputField _color;
        private readonly TMP_InputField _life;
        private readonly TMP_InputField[] _damage = new TMP_InputField[5];
        private readonly List<(string Id, Image Ring, RectTransform Art)> _emblems = new List<(string, Image, RectTransform)>();
        private readonly List<(string Hex, Image Ring)> _swatches = new List<(string, Image)>();
        private readonly TextMeshProUGUI _message;
        private readonly Button _save;
        private readonly Func<DanDefinition, DanDefinition, bool> _onSave;
        private readonly Action<string> _toast;
        private DanDefinition _source;
        private string _emblem = DanStyles.DEFAULT_EMBLEM;
        private string _colorHex = DanStyles.DEFAULT_COLOR;

        /// <param name="onSave">Gets the definition being edited (null for a new one) and the new one; true closes the card.</param>
        internal DanEditorPanel(RectTransform layer, Func<DanDefinition, DanDefinition, bool> onSave, Action<string> toast)
        {
            _onSave = onSave;
            _toast = toast;
            _root = LibraryUi.CreateRect("DanEditor", layer);
            LibraryUi.Stretch(_root);
            _modal = new LibraryModal(_root, new Vector2(CARD_WIDTH, CARD_HEIGHT), "新建段位", Close, withSubtitle: true);
            RectTransform body = _modal.Body;
            Vector2 topLeft = new Vector2(0f, 1f);

            // ---------------------------------------------------------------- songs
            RectTransform left = LibraryUi.CreateRect("Songs", body);
            left.anchorMin = Vector2.zero;
            left.anchorMax = new Vector2(0f, 1f);
            left.pivot = new Vector2(0f, 0.5f);
            left.offsetMin = new Vector2(0f, LibraryTheme.Layout.MODAL_BUTTON_HEIGHT + 20f);
            left.offsetMax = new Vector2(LEFT_WIDTH, 0f);
            TextMeshProUGUI songsCaption = LibraryWidgets.Label("Caption", left, "曲目", 24f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.Place(songsCaption.rectTransform, topLeft, topLeft, new Vector2(4f, -4f), new Vector2(80f, 44f));
            _songCount = LibraryWidgets.Label("Count", left, string.Empty, 18f, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.Place(_songCount.rectTransform, topLeft, topLeft, new Vector2(70f, -12f), new Vector2(240f, 32f));
            const float SMALL_HEIGHT = 52f;
            Button add = ServerWidgets.LabelledButton(left, "Add", "添加一首", LibraryIcon.Add, AddEmpty, 168f, SMALL_HEIGHT,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.Press, 18f);
            LibraryWidgets.Outline((Image)add.targetGraphic, SMALL_HEIGHT * 0.5f);
            LibraryWidgets.Place((RectTransform)add.transform, Vector2.one, Vector2.one, new Vector2(0f, 0f), new Vector2(168f, SMALL_HEIGHT));
            Button paste = ServerWidgets.LabelledButton(left, "Paste", "粘贴链接", LibraryIcon.Download, PasteLinks, 168f, SMALL_HEIGHT,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.None, 18f);
            LibraryWidgets.Outline((Image)paste.targetGraphic, SMALL_HEIGHT * 0.5f);
            LibraryWidgets.Place((RectTransform)paste.transform, Vector2.one, Vector2.one, new Vector2(-180f, 0f), new Vector2(168f, SMALL_HEIGHT));

            Image well = PackWidgets.Group(left, "Well");
            LibraryWidgets.Inset(well.rectTransform, 0f, 0f, 0f, SMALL_HEIGHT + 16f);
            RectTransform listArea = LibraryUi.CreateRect("ListArea", well.transform);
            LibraryWidgets.Inset(listArea, 12f, 12f, 12f, 12f);
            _songContent = PackWidgets.Scroll(listArea, out _songScroll);

            // ---------------------------------------------------------------- plate
            RectTransform right = LibraryUi.CreateRect("Style", body);
            LibraryWidgets.Inset(right, RIGHT_LEFT, LibraryTheme.Layout.MODAL_BUTTON_HEIGHT + 20f, 0f, 0f);
            float width = CARD_WIDTH - LibraryTheme.Layout.MODAL_PADDING * 2f - RIGHT_LEFT;
            _preview = new DanPlate(right, "Preview", new Vector2(width - 40f, 96f), glow: true);
            LibraryWidgets.Place(_preview.Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), _preview.Root.sizeDelta);

            float top = 128f;
            DanWidgets.Caption(right, "段位名（即牌面文字）", 4f, top);
            _label = LibraryWidgets.TextField("Label", right, FIELD_HEIGHT, "例如：三曲耐久", LibraryIcon.Tag, 22f);
            LibraryWidgets.TopStrip((RectTransform)_label.transform, 0f, 0f, top + 28f, FIELD_HEIGHT);
            _label.characterLimit = DanStyles.LABEL_LIMIT;
            _label.textComponent.richText = false;
            _label.onValidateInput = (text, index, character) => char.IsControl(character) ? '\0' : character;
            _label.onValueChanged.AddListener(_ => OnStyleChanged());

            top += 28f + FIELD_HEIGHT + 16f;
            DanWidgets.Caption(right, "图案", 4f, top);
            RectTransform tiles = LibraryUi.CreateRect("Emblems", right);
            LibraryWidgets.TopStrip(tiles, 0f, 0f, top + 28f, TILE);
            IReadOnlyList<DanStyles.Emblem> emblems = DanStyles.Emblems;
            float tileGap = (width - TILE * emblems.Count) / (emblems.Count - 1);
            for (int i = 0; i < emblems.Count; i++)
            {
                string id = emblems[i].Id;
                Image tile = LibraryWidgets.Panel("Emblem" + id, tiles, 16f, HomeTheme.Colors.Chip);
                tile.raycastTarget = true;
                LibraryWidgets.Place(tile.rectTransform, topLeft, topLeft, new Vector2(i * (TILE + tileGap), 0f), new Vector2(TILE, TILE));
                RectTransform art = LibraryUi.CreateRect("Art", tile.transform);
                LibraryWidgets.Place(art, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TILE * 0.6f, TILE * 0.6f));
                Image ring = LibraryWidgets.Shape("Ring", tile.transform, LibraryShapes.Ring(16f, 2.5f), Color.clear);
                LibraryUi.Stretch(ring.rectTransform);
                Button button = tile.gameObject.AddComponent<Button>();
                button.targetGraphic = tile;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => ChooseEmblem(id, true));
                LibraryPressFeedback.Attach(tile.gameObject);
                _emblems.Add((id, ring, art));
            }

            top += 28f + TILE + 16f;
            DanWidgets.Caption(right, "颜色", 4f, top);
            RectTransform swatches = LibraryUi.CreateRect("Swatches", right);
            LibraryWidgets.TopStrip(swatches, 0f, 0f, top + 28f, FIELD_HEIGHT);
            const float HEX_WIDTH = 168f;
            float swatchGap = (width - HEX_WIDTH - 20f - SWATCH * DanStyles.Swatches.Length) / (DanStyles.Swatches.Length - 1);
            for (int i = 0; i < DanStyles.Swatches.Length; i++)
            {
                string hex = DanStyles.Swatches[i];
                Image ring = LibraryWidgets.Shape("Ring" + i, swatches, LibraryShapes.Ring((SWATCH + 10f) * 0.5f, 2.5f), Color.clear);
                LibraryWidgets.Place(ring.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(SWATCH * 0.5f + i * (SWATCH + swatchGap), 0f), new Vector2(SWATCH + 10f, SWATCH + 10f));
                Image swatch = LibraryWidgets.Panel("Swatch" + i, swatches, SWATCH * 0.5f, DanStyles.ParseColor(hex));
                swatch.raycastTarget = true;
                LibraryWidgets.Place(swatch.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(SWATCH * 0.5f + i * (SWATCH + swatchGap), 0f), new Vector2(SWATCH, SWATCH));
                Button button = swatch.gameObject.AddComponent<Button>();
                button.targetGraphic = swatch;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => ChooseColor(hex, true));
                LibraryPressFeedback.Attach(swatch.gameObject);
                _swatches.Add((hex, ring));
            }

            _color = LibraryWidgets.TextField("Hex", swatches, FIELD_HEIGHT, "#RRGGBB", null, 22f);
            LibraryWidgets.Place((RectTransform)_color.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero,
                new Vector2(HEX_WIDTH, FIELD_HEIGHT));
            _color.characterLimit = 7;
            _color.textComponent.richText = false;
            _color.onValidateInput = (text, index, character) => character == '#' || Uri.IsHexDigit(character) ? char.ToUpperInvariant(character) : '\0';
            _color.onValueChanged.AddListener(OnHexChanged);

            top += 28f + FIELD_HEIGHT + 18f;
            DanWidgets.Caption(right, "初始生命与每个判定的扣血", 4f, top);
            RectTransform lifeRow = LibraryUi.CreateRect("Life", right);
            LibraryWidgets.TopStrip(lifeRow, 0f, 0f, top + 28f, FIELD_HEIGHT + 24f);
            const float LIFE_WIDTH = 150f;
            TextMeshProUGUI lifeName = LibraryWidgets.Label("LifeName", lifeRow, "初始生命", 14f, DanCopy.Life, FontStyles.Bold,
                TextAlignmentOptions.Center);
            LibraryWidgets.Place(lifeName.rectTransform, topLeft, topLeft, Vector2.zero, new Vector2(LIFE_WIDTH, 20f));
            _life = DanWidgets.NumberField(lifeRow, "InitialLife", FIELD_HEIGHT);
            LibraryWidgets.Place((RectTransform)_life.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero,
                new Vector2(LIFE_WIDTH, FIELD_HEIGHT));
            _life.onValueChanged.AddListener(_ => ClearMessage());
            float damageLeft = LIFE_WIDTH + 30f;
            float damageWidth = (width - damageLeft - 4f * 10f) / 5f;
            for (int i = 0; i < 5; i++)
            {
                float x = damageLeft + i * (damageWidth + 10f);
                TextMeshProUGUI name = LibraryWidgets.Label("Name" + i, lifeRow, DanCopy.JudgeNames[i], 14f, DanCopy.JudgeColors[i],
                    FontStyles.Bold, TextAlignmentOptions.Center);
                LibraryUi.UseSpacedCondensedFont(name, 4f);
                LibraryWidgets.Place(name.rectTransform, topLeft, topLeft, new Vector2(x, 0f), new Vector2(damageWidth, 20f));
                _damage[i] = DanWidgets.NumberField(lifeRow, "Damage" + DanCopy.JudgeNames[i], FIELD_HEIGHT);
                LibraryWidgets.Place((RectTransform)_damage[i].transform, Vector2.zero, Vector2.zero, new Vector2(x, 0f),
                    new Vector2(damageWidth, FIELD_HEIGHT));
                _damage[i].onValueChanged.AddListener(_ => ClearMessage());
            }

            // ---------------------------------------------------------------- footer
            float height = LibraryTheme.Layout.MODAL_BUTTON_HEIGHT;
            _message = LibraryWidgets.Label("Message", body, string.Empty, 19f, LibraryTheme.Colors.TextMuted);
            _message.richText = false;
            LibraryWidgets.BottomStrip(_message.rectTransform, 4f, BUTTON_WIDTH * 2f + 40f, 0f, height);
            _save = ServerWidgets.LabelledButton(body, "Save", "保存", LibraryIcon.Check, Save, BUTTON_WIDTH, height,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.None);
            LibraryWidgets.Place((RectTransform)_save.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero,
                new Vector2(BUTTON_WIDTH, height));
            Button cancel = LibraryWidgets.PillButton("Cancel", body, "取消", Close, BUTTON_WIDTH, height,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, sound: UiCue.Close);
            LibraryWidgets.Outline((Image)cancel.targetGraphic, height * 0.5f);
            LibraryWidgets.Place((RectTransform)cancel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-(BUTTON_WIDTH + 16f), 0f), new Vector2(BUTTON_WIDTH, height));
            _root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        /// <summary>Opens the card on <paramref name="source"/>'s fields, or on a fresh two-song 段位 when null.</summary>
        internal void Open(DanDefinition source)
        {
            _source = source;
            _modal.Title.text = source == null ? "新建段位" : "编辑段位";
            _modal.Subtitle.text = source == null
                ? "依次添加谱面的分享链接，设定生命与扣血，再设计一块段位牌"
                : "保存后成为一个新的段位；原来的段位、通关记录和已取得的段位牌都会保留";
            if (source != null) Load(source);
            else LoadFresh();
            _modal.Open();
        }

        internal void Close()
        {
            DeactivateFields();
            _modal.Close();
        }

        public void Dispose()
        {
            _preview.Dispose();
            _modal.Dispose();
        }

        // ---------------------------------------------------------------- the form

        private void LoadFresh()
        {
            _links.Clear();
            _links.Add(string.Empty);
            _links.Add(string.Empty);
            _label.SetTextWithoutNotify(string.Empty);
            _life.SetTextWithoutNotify("100");
            int[] costs = { 0, 1, 3, 6, 10 };
            for (int i = 0; i < 5; i++) _damage[i].SetTextWithoutNotify(costs[i].ToString(CultureInfo.InvariantCulture));
            ChooseEmblem(DanStyles.DEFAULT_EMBLEM, false);
            ChooseColor(DanStyles.DEFAULT_COLOR, false);
            RebuildRows();
            ClearMessage();
        }

        private void Load(DanDefinition definition)
        {
            _links.Clear();
            _links.AddRange(definition.Songs);
            _label.SetTextWithoutNotify(definition.Label);
            _life.SetTextWithoutNotify(definition.InitialLife.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < 5; i++)
            {
                _damage[i].SetTextWithoutNotify(DanCopy.Damage(definition.Damage, i).ToString(CultureInfo.InvariantCulture));
            }

            ChooseEmblem(definition.Emblem, false);
            ChooseColor(definition.Color, false);
            RebuildRows();
            ClearMessage();
        }

        private void ChooseEmblem(string id, bool sound)
        {
            _emblem = id;
            Color32 tint = DanStyles.ParseColor(_colorHex);
            foreach ((string tileId, Image ring, RectTransform art) in _emblems)
            {
                bool chosen = string.Equals(tileId, id, StringComparison.Ordinal);
                ring.color = chosen ? (Color)tint : Color.clear;
            }

            if (sound) UiSound.Play(UiCue.Select);
            OnStyleChanged();
        }

        private void ChooseColor(string hex, bool sound)
        {
            _colorHex = hex.ToUpperInvariant();
            _color.SetTextWithoutNotify(_colorHex);
            DanWidgets.SetFieldError(_color, false);
            if (sound) UiSound.Play(UiCue.Select);
            RecolorChoices();
            OnStyleChanged();
        }

        private void OnHexChanged(string text)
        {
            string hex = (text ?? string.Empty).Trim();
            if (hex.Length > 0 && hex[0] != '#') hex = "#" + hex;
            bool valid = hex.Length == 7 && hex.IndexOf('#', 1) < 0;
            DanWidgets.SetFieldError(_color, !valid && hex.Length > 0);
            if (!valid) return;
            _colorHex = hex.ToUpperInvariant();
            RecolorChoices();
            OnStyleChanged();
        }

        private void RecolorChoices()
        {
            Color32 tint = DanStyles.ParseColor(_colorHex);
            foreach ((string hex, Image ring) in _swatches)
            {
                ring.color = string.Equals(hex, _colorHex, StringComparison.OrdinalIgnoreCase) ? (Color)Color.white : Color.clear;
            }

            foreach ((string id, Image ring, RectTransform art) in _emblems)
            {
                DanEmblemArt.Draw(art, id, DanStyles.Light(tint), TILE * 0.6f);
                ring.color = string.Equals(id, _emblem, StringComparison.Ordinal) ? (Color)tint : Color.clear;
            }
        }

        private void OnStyleChanged()
        {
            string label = (_label.text ?? string.Empty).Trim();
            _preview.SetStyle(label.Length > 0 ? label : "段位名", _emblem, _colorHex);
            _preview.SetLocked(label.Length == 0);
            DanWidgets.SetFieldError(_label, false);
        }

        // ---------------------------------------------------------------- songs

        private void AddEmpty()
        {
            _links.Add(string.Empty);
            RebuildRows();
            ScrollToEnd();
            ClearMessage();
        }

        private void PasteLinks()
        {
            string clipboard = GUIUtility.systemCopyBuffer;
            if (DanCopy.IsShareText(clipboard))
            {
                try
                {
                    Load(DanDefinition.Parse(clipboard.TrimStart()));
                    UiSound.Play(UiCue.Success);
                    _toast?.Invoke("已载入剪贴板里的段位，可以在保存前修改");
                    return;
                }
                catch (DanFieldException exception)
                {
                    UiSound.Play(UiCue.Error);
                    ShowError(DanCopy.Field(exception));
                    return;
                }
            }

            List<string> links = DanCopy.LevelLinks(clipboard);
            if (links.Count == 0)
            {
                UiSound.Play(UiCue.Blocked);
                _toast?.Invoke("剪贴板里没有谱面链接。请先在服务器页或浏览器复制谱面的分享链接");
                return;
            }

            int next = 0;
            foreach (string link in links)
            {
                while (next < _links.Count && !string.IsNullOrWhiteSpace(_links[next])) next++;
                if (next < _links.Count) _links[next] = link;
                else _links.Add(link);
            }

            RebuildRows();
            ScrollToEnd();
            ClearMessage();
            UiSound.Play(UiCue.Success);
            _toast?.Invoke("已添加 " + links.Count + " 首谱面");
        }

        private void Move(int index, int direction)
        {
            int target = index + direction;
            if (target < 0 || target >= _links.Count) return;
            (_links[index], _links[target]) = (_links[target], _links[index]);
            UiSound.Play(UiCue.Select);
            RebuildRows();
            ClearMessage();
        }

        private void Remove(int index)
        {
            if (index < 0 || index >= _links.Count) return;
            _links.RemoveAt(index);
            if (_links.Count == 0) _links.Add(string.Empty);
            UiSound.Play(UiCue.Delete);
            RebuildRows();
            ClearMessage();
        }

        private void RebuildRows()
        {
            while (_rows.Count < _links.Count) _rows.Add(new SongRow(this, _songContent, _rows.Count));
            for (int i = 0; i < _rows.Count; i++)
            {
                SongRow row = _rows[i];
                bool used = i < _links.Count;
                row.Root.gameObject.SetActive(used);
                if (!used) continue;
                row.Root.anchoredPosition = new Vector2(0f, -i * (ROW_HEIGHT + ROW_GAP));
                row.Bind(i, _links[i], _links.Count);
            }

            _songContent.sizeDelta = new Vector2(0f, Mathf.Max(0f, _links.Count * (ROW_HEIGHT + ROW_GAP) - ROW_GAP));
            UpdateCount();
        }

        private void UpdateCount()
        {
            int filled = 0;
            foreach (string link in _links)
            {
                if (!string.IsNullOrWhiteSpace(link)) filled++;
            }

            _songCount.text = filled + " 首" + (filled < 2 ? "  ·  至少两首" : string.Empty);
        }

        private void ScrollToEnd()
        {
            Canvas.ForceUpdateCanvases();
            _songScroll.verticalNormalizedPosition = 0f;
        }

        // ---------------------------------------------------------------- saving

        private void Save()
        {
            DanDefinition definition = Build();
            if (definition == null)
            {
                UiSound.Play(UiCue.Error);
                return;
            }

            if (_source != null && string.Equals(_source.ShareText, definition.ShareText, StringComparison.Ordinal))
            {
                Close();
                _toast?.Invoke("内容没有变化");
                return;
            }

            if (_onSave(_source, definition)) Close();
        }

        /// <summary>The definition the form describes; null after marking what is wrong.</summary>
        private DanDefinition Build()
        {
            ClearMessage();
            var links = new List<string>();
            var rows = new List<int>();
            for (int i = 0; i < _links.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(_links[i])) continue;
                links.Add(_links[i].Trim());
                rows.Add(i);
            }

            int? life = DanWidgets.ReadNumber(_life);
            if (life == null || life.Value <= 0)
            {
                DanWidgets.SetFieldError(_life, true);
                ShowError("初始生命需要是大于 0 的整数");
                return null;
            }

            var costs = new int[5];
            for (int i = 0; i < 5; i++)
            {
                int? cost = DanWidgets.ReadNumber(_damage[i]);
                if (cost == null)
                {
                    DanWidgets.SetFieldError(_damage[i], true);
                    ShowError("请填写 " + DanCopy.JudgeNames[i] + " 的扣血，不扣血就填 0");
                    return null;
                }

                costs[i] = cost.Value;
            }

            try
            {
                return new DanDefinition(life.Value, new DanDamage(costs[0], costs[1], costs[2], costs[3], costs[4]), links,
                    _label.text, _emblem, _colorHex);
            }
            catch (DanFieldException exception)
            {
                MarkField(exception.Field, rows);
                ShowError(DanCopy.Field(exception, links.Count));
                return null;
            }
        }

        private void MarkField(string field, List<int> rows)
        {
            if (field == "initialLife") DanWidgets.SetFieldError(_life, true);
            else if (field == "style.label") DanWidgets.SetFieldError(_label, true);
            else if (field == "style.color") DanWidgets.SetFieldError(_color, true);
            else if (field.StartsWith("damage.", StringComparison.Ordinal))
            {
                int judge = Array.IndexOf(DanCopy.JudgeNames, field.Substring(7).ToUpperInvariant());
                if (judge >= 0) DanWidgets.SetFieldError(_damage[judge], true);
            }
            else if (field.StartsWith("songs[", StringComparison.Ordinal))
            {
                int close = field.IndexOf(']');
                if (close > 6 && int.TryParse(field.Substring(6, close - 6), NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                    && index < rows.Count)
                {
                    _rows[rows[index]].SetError(true);
                }
            }
        }

        private void ShowError(string message)
        {
            _message.text = message;
            _message.color = LibraryTheme.Colors.Danger;
        }

        private void ClearMessage()
        {
            _message.text = "每次判定按上表扣血，生命在哪一首归零就止步在哪一首";
            _message.color = LibraryTheme.Colors.TextMuted;
            DanWidgets.SetFieldError(_life, false);
            foreach (TMP_InputField field in _damage) DanWidgets.SetFieldError(field, false);
            DanWidgets.SetFieldError(_label, false);
            foreach (SongRow row in _rows) row.SetError(false);
        }

        private void DeactivateFields()
        {
            _label.DeactivateInputField();
            _color.DeactivateInputField();
            _life.DeactivateInputField();
            foreach (TMP_InputField field in _damage) field.DeactivateInputField();
            foreach (SongRow row in _rows) row.Link.DeactivateInputField();
        }

        /// <summary>One song of the form: its number, the link, what the link names, and the move and remove buttons.</summary>
        private sealed class SongRow
        {
            private const float BUTTON = 48f;

            private readonly DanEditorPanel _owner;
            private readonly Image _background;
            private readonly TextMeshProUGUI _number;
            private readonly TextMeshProUGUI _status;
            private readonly Button _up;
            private readonly Button _down;
            private int _index;

            internal SongRow(DanEditorPanel owner, RectTransform content, int index)
            {
                _owner = owner;
                Root = LibraryUi.CreateRect("Song" + index, content);
                Root.anchorMin = new Vector2(0f, 1f);
                Root.anchorMax = new Vector2(1f, 1f);
                Root.pivot = new Vector2(0.5f, 1f);
                Root.sizeDelta = new Vector2(0f, ROW_HEIGHT);
                _background = LibraryWidgets.Panel("Background", Root, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
                LibraryUi.Stretch(_background.rectTransform);

                Image disc = LibraryWidgets.Panel("Disc", Root, 22f, HomeTheme.Colors.Chip);
                LibraryWidgets.Place(disc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -16f), new Vector2(44f, 44f));
                _number = LibraryWidgets.Label("Number", disc.transform, "1", 24f, LibraryTheme.Colors.TextPrimary, FontStyles.Normal,
                    TextAlignmentOptions.Center);
                LibraryUi.UseCondensedFont(_number);
                _number.overflowMode = TextOverflowModes.Overflow;
                LibraryUi.Stretch(_number.rectTransform);

                float right = 14f + BUTTON * 3f + 16f;
                Link = LibraryWidgets.TextField("Link", Root, 52f, "粘贴谱面分享链接", null, 19f);
                Link.textComponent.richText = false;
                Link.characterLimit = 2048;
                Link.contentType = TMP_InputField.ContentType.Standard;
                Link.keyboardType = TouchScreenKeyboardType.URL;
                LibraryWidgets.TopStrip((RectTransform)Link.transform, 72f, right, 12f, 52f);
                Link.onValueChanged.AddListener(OnChanged);
                Link.onEndEdit.AddListener(_ => Describe(true));
                _status = PackWidgets.UserLabel("Status", Root, string.Empty, 16f, LibraryTheme.Colors.TextMuted);
                LibraryWidgets.TopStrip(_status.rectTransform, 92f, right, 68f, 24f);

                _up = PackWidgets.SmallIconButton(Root, "Up", LibraryIcon.ChevronRight, () => _owner.Move(_index, -1), BUTTON, UiCue.None);
                ((RectTransform)_up.transform.Find("Glyph")).localEulerAngles = new Vector3(0f, 0f, 90f);
                _down = PackWidgets.SmallIconButton(Root, "Down", LibraryIcon.ChevronRight, () => _owner.Move(_index, 1), BUTTON, UiCue.None);
                ((RectTransform)_down.transform.Find("Glyph")).localEulerAngles = new Vector3(0f, 0f, -90f);
                Button remove = PackWidgets.SmallIconButton(Root, "Remove", LibraryIcon.Trash, () => _owner.Remove(_index), BUTTON, UiCue.None);
                Vector2 center = new Vector2(1f, 0.5f);
                LibraryWidgets.Place((RectTransform)remove.transform, center, center, new Vector2(-14f, 0f), new Vector2(BUTTON, BUTTON));
                LibraryWidgets.Place((RectTransform)_down.transform, center, center, new Vector2(-14f - (BUTTON + 6f), 0f), new Vector2(BUTTON, BUTTON));
                LibraryWidgets.Place((RectTransform)_up.transform, center, center, new Vector2(-14f - (BUTTON + 6f) * 2f, 0f), new Vector2(BUTTON, BUTTON));
            }

            internal RectTransform Root { get; }
            internal TMP_InputField Link { get; }

            internal void Bind(int index, string link, int count)
            {
                _index = index;
                _number.text = (index + 1).ToString(CultureInfo.InvariantCulture);
                Link.SetTextWithoutNotify(link ?? string.Empty);
                PackWidgets.SetEnabled(_up, index > 0);
                PackWidgets.SetEnabled(_down, index < count - 1);
                SetError(false);
                Describe(true);
            }

            internal void SetError(bool error)
            {
                DanWidgets.SetFieldError(Link, error);
                _background.color = error ? LibraryTheme.Colors.DangerSurface : LibraryTheme.Colors.Control;
            }

            private void OnChanged(string text)
            {
                if (_index < _owner._links.Count) _owner._links[_index] = text ?? string.Empty;
                SetError(false);
                _owner.UpdateCount();
                Describe(false);
            }

            /// <param name="lookUp">Also check whether the chart is already on this device (reads the install index).</param>
            private void Describe(bool lookUp)
            {
                string link = Link.text;
                if (string.IsNullOrWhiteSpace(link))
                {
                    _status.text = "支持 HTTPS、sonolus:// 与 open.sonolus.com 链接";
                    _status.color = LibraryTheme.Colors.TextMuted;
                    return;
                }

                DanSongInfo info = lookUp ? DanSongInfo.Resolve(link) : null;
                if (!lookUp)
                {
                    bool parsed = OpenSekai.Infrastructure.Sonolus.SonolusServerAddress.TryParse(link, out var server, out string level, out _)
                        && !string.IsNullOrEmpty(level);
                    _status.text = parsed ? server.Host + "  ·  " + level : "无法识别这个链接";
                    _status.color = parsed ? LibraryTheme.Colors.TextMuted : LibraryTheme.Colors.Warning;
                    return;
                }

                if (!info.Valid)
                {
                    _status.text = "无法识别这个链接";
                    _status.color = LibraryTheme.Colors.Warning;
                    return;
                }

                _status.text = info.Installed
                    ? "本机已有  ·  " + info.Title + (info.Difficulty != null ? "  ·  " + info.Difficulty : string.Empty)
                    : info.Host + "  ·  " + info.LevelName + "  ·  开打前下载";
                _status.color = info.Installed ? DanCopy.Life : LibraryTheme.Colors.TextMuted;
            }
        }
    }
}
