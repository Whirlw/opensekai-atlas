using System;
using CP;
using Sekai;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OpenSekai.Shared.Diagnostics;
using OpenSekai.Diagnostics;
using OpenSekai.Shared.Settings;
using OpenSekai.Shared.Live.Simulation;
using Sekai.Live;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Lightweight settings overlay for the player library: timing, speed, simultaneous line,
    /// frame-rate cap and the two live volume channels. It edits the existing settings objects
    /// (<see cref="LiveSettingData"/> / <see cref="ApplicationLocalSettings"/>) so nothing about
    /// the stored format changes, and never touches editor-only preferences.
    /// </summary>
    public sealed class LibrarySettingsPanel : MonoBehaviour
    {
        private const float PANEL_WIDTH = 960f;
        private const float PANEL_HEIGHT = 896f;
        private const float ROW_HEIGHT = 56f;

        // LiveConfig clamps the note skin to 0..1 and both skins ship as custom01/custom02.
        private const int SKIN_OPTION_COUNT = 2;
        private static readonly string[] SkinLabels = { "样式 1", "样式 2" };
        private static readonly string[] NoteSeLabels = { "音效 1", "音效 2" };
        private static readonly string[] NoteEffectLabels = { "特效 1", "特效 2" };

        private static readonly Color32 OverlayColor = new Color32(0, 0, 0, 170);
        private static readonly Color32 PanelColor = new Color32(28, 34, 42, 255);
        private static readonly Color32 RowColor = new Color32(38, 46, 56, 255);

        private LiveSettingData _settings;
        private ApplicationLocalSettings _localSettings;
        private Action _onChanged;

        private TMP_InputField _audioLatencyInput;
        private TMP_InputField _visualLatencyInput;
        private TMP_InputField _judgeOffsetInput;
        private TextMeshProUGUI _speedValue;
        private TextMeshProUGUI _lineValue;
        private TextMeshProUGUI _frameRateValue;
        private TextMeshProUGUI _judgeTimingValue;

        private const string TouchJudgeTimingLabel = "触摸时刻";
        private const string LegacyJudgeTimingLabel = "下一帧(旧式)";
        private const string TraceOnLabel = "已开启";
        private const string TraceOffLabel = "关闭";
        private TextMeshProUGUI _traceValue;
        private GameObject _tracePanel;
        private TextMeshProUGUI _performanceHudValue;
        private TextMeshProUGUI _highlightLastValue;
        private readonly TextMeshProUGUI[] _traceCategoryValues = new TextMeshProUGUI[11];
        private static readonly LiveTraceCategory[] TraceCategories =
        {
            LiveTraceCategory.RawTouch, LiveTraceCategory.Press, LiveTraceCategory.Resident,
            LiveTraceCategory.Release, LiveTraceCategory.Judge, LiveTraceCategory.Hold, LiveTraceCategory.Tail,
            LiveTraceCategory.Swipe, LiveTraceCategory.Pool, LiveTraceCategory.View, LiveTraceCategory.Frame
        };
        private TextMeshProUGUI _bgmValue;
        private TextMeshProUGUI _seValue;
        private TextMeshProUGUI _skinValue;
        private TextMeshProUGUI _noteSeValue;
        private TextMeshProUGUI _noteEffectValue;

        public static LibrarySettingsPanel Create(Transform parent, Action _onChanged)
        {
            RectTransform root = LibraryUi.CreateRect("SettingsPanel", parent);
            LibraryUi.Stretch(root);
            LibrarySettingsPanel panel = root.gameObject.AddComponent<LibrarySettingsPanel>();
            panel._onChanged = _onChanged;
            panel.Build();
            panel.Hide();
            return panel;
        }

        public void Show()
        {
            _settings = LiveSettingsStore.Shared.Load();
            _localSettings = ApplicationLocalSettings.LoadFromStorage();
            if (_localSettings.LiveVolume == null)
            {
                _localSettings.LiveVolume = _localSettings.SetupLiveVolume();
            }

            RefreshValues();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            if (_tracePanel != null) _tracePanel.SetActive(false);
            gameObject.SetActive(false);
        }

        private void Build()
        {
            Image overlay = LibraryUi.CreateImage("Overlay", transform, OverlayColor);
            LibraryUi.Stretch(overlay.rectTransform);

            RectTransform panel = LibraryUi.CreateRect("Panel", transform);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = PanelColor;

            TextMeshProUGUI title = LibraryUi.CreateText(
                "Title", panel, "设置", 36f, FontStyles.Bold, TextAlignmentOptions.Left);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(0.6f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.offsetMin = new Vector2(36f, -84f);
            title.rectTransform.offsetMax = new Vector2(0f, -28f);

            Button close = LibraryUi.CreateButton("Close", panel, "关闭", Hide, 140f, 52f);
            RectTransform closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-28f, -28f);

            Button traceOptions = LibraryUi.CreateButton("TraceOptions", panel, "追踪选项", () => _tracePanel.SetActive(true), 180f, 52f);
            RectTransform traceOptionsRect = (RectTransform)traceOptions.transform;
            traceOptionsRect.anchorMin = traceOptionsRect.anchorMax = new Vector2(1f, 1f);
            traceOptionsRect.pivot = new Vector2(1f, 1f);
            traceOptionsRect.anchoredPosition = new Vector2(-184f, -28f);

            RectTransform rows = LibraryUi.CreateRect("Rows", panel);
            rows.anchorMin = new Vector2(0f, 0f);
            rows.anchorMax = new Vector2(1f, 1f);
            rows.offsetMin = new Vector2(32f, 60f);
            rows.offsetMax = new Vector2(-32f, -96f);

            int index = 0;
            // Temporary rows: whole milliseconds typed in. See TimingCompensation for what each moves.
            _audioLatencyInput = AddMillisecondsRow(rows, ref index, "音频延迟 (ms)",
                value => { _settings.AudioLatencyMs = TimingCompensation.ClampAudioLatency(value); SaveSettings(); });
            _visualLatencyInput = AddMillisecondsRow(rows, ref index, "画面延迟 (ms)",
                value => { _settings.VisualLatencyMs = TimingCompensation.ClampVisualLatency(value); SaveSettings(); });
            _judgeOffsetInput = AddMillisecondsRow(rows, ref index, "判定偏移 (ms, 正=打得晚)",
                value => { _settings.JudgeOffsetMs = TimingCompensation.ClampJudgeOffset(value); SaveSettings(); });

            _speedValue = AddStepperRow(rows, ref index, "音符流速",
                () => _settings.NoteSpeed,
                value => { _settings.NoteSpeed = value; SaveSettings(); },
                0.5f, 12f, 0.1f, "0.0");

            _lineValue = AddToggleRow(rows, ref index, "同时押线",
                () => _settings.UseSimultaneousPushingLine,
                value => { _settings.UseSimultaneousPushingLine = value; SaveSettings(); });

            _frameRateValue = AddToggleRow(rows, ref index, "帧率",
                () => _settings.FrameRateLimit > 0,
                _ => CycleFrameRate(),
                () => FramerateUtility.GetLimitLabel(_settings.FrameRateLimit));

            _judgeTimingValue = AddToggleRow(rows, ref index, "判定时机",
                () => _settings.UseLegacyJudgeTiming,
                value => { _settings.UseLegacyJudgeTiming = value; SaveSettings(); },
                () => _settings.UseLegacyJudgeTiming ? LegacyJudgeTimingLabel : TouchJudgeTimingLabel);

            _bgmValue = AddStepperRow(rows, ref index, "音乐音量",
                () => _localSettings.LiveVolume.Bgm,
                value => { _localSettings.LiveVolume.Bgm = value; SaveLocal(); },
                0f, 1f, 0.1f, "0.0");

            _seValue = AddStepperRow(rows, ref index, "音效音量",
                () => _localSettings.LiveVolume.Se,
                value => { _localSettings.LiveVolume.Se = value; SaveLocal(); },
                0f, 1f, 0.1f, "0.0");

            _skinValue = AddCycleRow(rows, ref index, "音符皮肤",
                () => _settings.NoteSkinIndex,
                value => { _settings.NoteSkinIndex = value; SaveSettings(); },
                SkinLabels, SKIN_OPTION_COUNT);

            _noteSeValue = AddCycleRow(rows, ref index, "打击音",
                () => _settings.NoteSeIndex,
                value => { _settings.NoteSeIndex = value; SaveSettings(); },
                NoteSeLabels, SKIN_OPTION_COUNT);

            _noteEffectValue = AddCycleRow(rows, ref index, "打击特效",
                () => _settings.NoteEffect,
                value => { _settings.NoteEffect = value; SaveSettings(); },
                NoteEffectLabels, SKIN_OPTION_COUNT);

            _traceValue = AddToggleRow(rows, ref index, "测试模式",
                () => LiveTrace.Enabled,
                value => { LiveTrace.Enabled = value; SaveSettings(); },
                () => LiveTrace.Enabled ? TraceOnLabel : TraceOffLabel);

            TextMeshProUGUI hint = LibraryUi.CreateText(
                "Hint", panel, "皮肤、打击音与打击特效在下一次开局时生效。", 20f,
                FontStyles.Normal, TextAlignmentOptions.Left);
            hint.color = new Color32(150, 160, 174, 255);
            hint.rectTransform.anchorMin = new Vector2(0f, 0f);
            hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            hint.rectTransform.pivot = new Vector2(0.5f, 0f);
            hint.rectTransform.offsetMin = new Vector2(32f, 22f);
            hint.rectTransform.offsetMax = new Vector2(-32f, 50f);
            BuildTracePanel(panel);
        }

        private void BuildTracePanel(Transform parent)
        {
            RectTransform panel = LibraryUi.CreateRect("TracePanel", parent);
            LibraryUi.Stretch(panel);
            panel.gameObject.AddComponent<Image>().color = PanelColor;
            _tracePanel = panel.gameObject;

            TextMeshProUGUI title = LibraryUi.CreateText("Title", panel, "追踪选项", 36f,
                FontStyles.Bold, TextAlignmentOptions.Left);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(0.6f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.offsetMin = new Vector2(36f, -84f);
            title.rectTransform.offsetMax = new Vector2(0f, -28f);
            Button back = LibraryUi.CreateButton("Back", panel, "返回", () => _tracePanel.SetActive(false), 140f, 52f);
            RectTransform backRect = (RectTransform)back.transform;
            backRect.anchorMin = backRect.anchorMax = new Vector2(1f, 1f);
            backRect.pivot = new Vector2(1f, 1f);
            backRect.anchoredPosition = new Vector2(-28f, -28f);

            RectTransform rows = LibraryUi.CreateRect("Rows", panel);
            rows.anchorMin = Vector2.zero;
            rows.anchorMax = Vector2.one;
            rows.offsetMin = new Vector2(32f, 60f);
            rows.offsetMax = new Vector2(-32f, -96f);
            string[] labels = { "原始触摸（高频）", "按下事件", "常驻通道（高频）", "抬起事件", "判定结果", "长条持握", "长条尾键", "划动", "对象回收", "画面表现", "画面逐帧（高频）" };
            int index = 0;
            _performanceHudValue = AddToggleRow(rows, ref index, "性能 HUD",
                () => PerformanceHud.Enabled, value => PerformanceHud.Enabled = value);
            _highlightLastValue = AddToggleRow(rows, ref index, "Last 长条标红",
                () => LiveTrace.HighlightLast, value => LiveTrace.HighlightLast = value);
            for (int i = 0; i < TraceCategories.Length; i++)
            {
                LiveTraceCategory category = TraceCategories[i];
                _traceCategoryValues[i] = AddToggleRow(rows, ref index, labels[i],
                    () => (LiveTrace.Categories & category) != 0,
                    enabled => LiveTrace.Categories = enabled
                        ? LiveTrace.Categories | category : LiveTrace.Categories & ~category);
            }
            TextMeshProUGUI hint = LibraryUi.CreateText("Hint", panel,
                "追踪需开启测试模式；高频记录可能影响性能。", 20f,
                FontStyles.Normal, TextAlignmentOptions.Left);
            hint.color = new Color32(150, 160, 174, 255);
            hint.rectTransform.anchorMin = new Vector2(0f, 0f);
            hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            hint.rectTransform.offsetMin = new Vector2(32f, 22f);
            hint.rectTransform.offsetMax = new Vector2(-32f, 50f);
            _tracePanel.SetActive(false);
        }

        private TextMeshProUGUI AddStepperRow(
            Transform parent, ref int index, string label,
            Func<float> get, Action<float> set,
            float min, float max, float step, string format)
        {
            RectTransform row = CreateRow(parent, index++);

            TextMeshProUGUI labelText = LibraryUi.CreateText(
                "Label", row, label, 26f, FontStyles.Normal, TextAlignmentOptions.Left);
            labelText.rectTransform.anchorMin = new Vector2(0f, 0f);
            labelText.rectTransform.anchorMax = new Vector2(0.55f, 1f);
            labelText.rectTransform.offsetMin = new Vector2(20f, 0f);
            labelText.rectTransform.offsetMax = Vector2.zero;

            TextMeshProUGUI valueText = LibraryUi.CreateText(
                "Value", row, string.Empty, 26f, FontStyles.Bold, TextAlignmentOptions.Right);
            valueText.rectTransform.anchorMin = new Vector2(0.45f, 0f);
            valueText.rectTransform.anchorMax = new Vector2(0.72f, 1f);
            valueText.rectTransform.offsetMin = Vector2.zero;
            valueText.rectTransform.offsetMax = Vector2.zero;

            Button minus = LibraryUi.CreateButton("Minus", row, "－", () =>
            {
                float next = Mathf.Clamp(get() - step, min, max);
                set(next);
                RefreshValues();
            }, 64f, 44f);
            Button plus = LibraryUi.CreateButton("Plus", row, "＋", () =>
            {
                float next = Mathf.Clamp(get() + step, min, max);
                set(next);
                RefreshValues();
            }, 64f, 44f);
            PlaceRowButton((RectTransform)minus.transform, 184f);
            PlaceRowButton((RectTransform)plus.transform, 104f);

            // Values are filled by RefreshValues() on Show(); reading the model here would run
            // before Show() assigns it.
            return valueText;
        }

        /// <summary>A row with a whole-millisecond input field; the value is clamped and saved on end edit.</summary>
        private TMP_InputField AddMillisecondsRow(Transform parent, ref int index, string label, Action<float> set)
        {
            RectTransform row = CreateRow(parent, index++);

            TextMeshProUGUI labelText = LibraryUi.CreateText(
                "Label", row, label, 26f, FontStyles.Normal, TextAlignmentOptions.Left);
            labelText.rectTransform.anchorMin = new Vector2(0f, 0f);
            labelText.rectTransform.anchorMax = new Vector2(0.6f, 1f);
            labelText.rectTransform.offsetMin = new Vector2(20f, 0f);
            labelText.rectTransform.offsetMax = Vector2.zero;

            RectTransform field = LibraryUi.CreateRect("Input", row);
            // Configured while inactive: TMP_InputField builds its caret on enable and needs the text
            // component by then.
            field.gameObject.SetActive(false);
            field.anchorMin = new Vector2(1f, 0.5f);
            field.anchorMax = new Vector2(1f, 0.5f);
            field.pivot = new Vector2(1f, 0.5f);
            field.sizeDelta = new Vector2(224f, 44f);
            field.anchoredPosition = new Vector2(-24f, 0f);
            Image background = field.gameObject.AddComponent<Image>();
            background.color = new Color32(30, 36, 46, 255);

            RectTransform area = LibraryUi.CreateRect("TextArea", field);
            LibraryUi.Stretch(area);
            area.offsetMin = new Vector2(12f, 4f);
            area.offsetMax = new Vector2(-12f, -4f);
            area.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI text = LibraryUi.CreateText(
                "Text", area, string.Empty, 26f, FontStyles.Bold, TextAlignmentOptions.Right);
            LibraryUi.Stretch(text.rectTransform);

            TMP_InputField input = field.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.targetGraphic = background;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.characterLimit = 5;
            input.caretColor = Color.white;
            input.onEndEdit.AddListener(raw =>
            {
                if (int.TryParse(raw, out int ms))
                {
                    set(ms);
                }

                RefreshValues();
            });
            field.gameObject.SetActive(true);

            // Filled by RefreshValues() on Show(); see AddStepperRow.
            return input;
        }

        private static void SetMilliseconds(TMP_InputField input, float ms)
        {
            if (input != null && !input.isFocused)
            {
                input.SetTextWithoutNotify(Mathf.RoundToInt(ms).ToString());
            }
        }

        private TextMeshProUGUI AddToggleRow(
            Transform parent, ref int index, string label,
            Func<bool> get, Action<bool> set,
            Func<string> describe = null)
        {
            RectTransform row = CreateRow(parent, index++);

            TextMeshProUGUI labelText = LibraryUi.CreateText(
                "Label", row, label, 26f, FontStyles.Normal, TextAlignmentOptions.Left);
            labelText.rectTransform.anchorMin = new Vector2(0f, 0f);
            labelText.rectTransform.anchorMax = new Vector2(0.55f, 1f);
            labelText.rectTransform.offsetMin = new Vector2(20f, 0f);
            labelText.rectTransform.offsetMax = Vector2.zero;

            TextMeshProUGUI valueText = LibraryUi.CreateText(
                "Value", row, string.Empty, 26f, FontStyles.Bold, TextAlignmentOptions.Right);
            valueText.rectTransform.anchorMin = new Vector2(0.45f, 0f);
            valueText.rectTransform.anchorMax = new Vector2(0.72f, 1f);
            valueText.rectTransform.offsetMin = Vector2.zero;
            valueText.rectTransform.offsetMax = Vector2.zero;

            Button toggle = LibraryUi.CreateButton("Toggle", row, "切换", () =>
            {
                set(!get());
                RefreshValues();
            }, 144f, 44f);
            PlaceRowButton((RectTransform)toggle.transform, 104f);

            // Filled by RefreshValues() on Show(); see AddStepperRow.
            return valueText;
        }

        /// <summary>Cycles an index through a small fixed option set (skins, note SE, tap effect).</summary>
        private TextMeshProUGUI AddCycleRow(
            Transform parent, ref int index, string label,
            Func<int> get, Action<int> set, string[] labels, int optionCount)
        {
            RectTransform row = CreateRow(parent, index++);

            TextMeshProUGUI labelText = LibraryUi.CreateText(
                "Label", row, label, 26f, FontStyles.Normal, TextAlignmentOptions.Left);
            labelText.rectTransform.anchorMin = new Vector2(0f, 0f);
            labelText.rectTransform.anchorMax = new Vector2(0.55f, 1f);
            labelText.rectTransform.offsetMin = new Vector2(20f, 0f);
            labelText.rectTransform.offsetMax = Vector2.zero;

            TextMeshProUGUI valueText = LibraryUi.CreateText(
                "Value", row, string.Empty, 26f, FontStyles.Bold, TextAlignmentOptions.Right);
            valueText.rectTransform.anchorMin = new Vector2(0.45f, 0f);
            valueText.rectTransform.anchorMax = new Vector2(0.72f, 1f);
            valueText.rectTransform.offsetMin = Vector2.zero;
            valueText.rectTransform.offsetMax = Vector2.zero;

            Button toggle = LibraryUi.CreateButton("Cycle", row, "切换", () =>
            {
                int next = Mathf.Clamp(get(), 0, optionCount - 1) + 1;
                set(next >= optionCount ? 0 : next);
                RefreshValues();
            }, 144f, 44f);
            PlaceRowButton((RectTransform)toggle.transform, 104f);

            return valueText;
        }

        private static RectTransform CreateRow(Transform parent, int index)        {
            RectTransform row = LibraryUi.CreateRect("Row" + index, parent);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(0f, -(index + 1) * ROW_HEIGHT);
            row.offsetMax = new Vector2(0f, -index * ROW_HEIGHT + 8f);
            Image background = row.gameObject.AddComponent<Image>();
            background.color = RowColor;
            return row;
        }

        private static void PlaceRowButton(RectTransform target, float rightOffset)
        {
            target.anchorMin = new Vector2(1f, 0.5f);
            target.anchorMax = new Vector2(1f, 0.5f);
            target.pivot = new Vector2(1f, 0.5f);
            target.anchoredPosition = new Vector2(-rightOffset, 0f);
        }

        private void CycleFrameRate()
        {
            int[] limits = FramerateUtility.SupportedLimits;
            int current = Array.IndexOf(limits, FramerateUtility.SanitizeLimit(_settings.FrameRateLimit));
            int next = limits[(current + 1) % limits.Length];
            _settings.FrameRateLimit = next;
            FramerateUtility.ApplyFrameRateLimit(next);
            SaveSettings();
        }

        private void RefreshValues()
        {
            if (_settings == null)
            {
                return;
            }

            SetMilliseconds(_audioLatencyInput, _settings.AudioLatencyMs);
            SetMilliseconds(_visualLatencyInput, _settings.VisualLatencyMs);
            SetMilliseconds(_judgeOffsetInput, _settings.JudgeOffsetMs);
            if (_speedValue != null) _speedValue.text = _settings.NoteSpeed.ToString("0.0");
            if (_lineValue != null) _lineValue.text = _settings.UseSimultaneousPushingLine ? "开" : "关";
            if (_frameRateValue != null) _frameRateValue.text = FramerateUtility.GetLimitLabel(_settings.FrameRateLimit);
            if (_judgeTimingValue != null) _judgeTimingValue.text = _settings.UseLegacyJudgeTiming ? LegacyJudgeTimingLabel : TouchJudgeTimingLabel;
            if (_bgmValue != null && _localSettings?.LiveVolume != null)
            {
                _bgmValue.text = _localSettings.LiveVolume.Bgm.ToString("0.0");
            }
            if (_seValue != null && _localSettings?.LiveVolume != null)
            {
                _seValue.text = _localSettings.LiveVolume.Se.ToString("0.0");
            }
            if (_skinValue != null) _skinValue.text = Describe(SkinLabels, _settings.NoteSkinIndex);
            if (_noteSeValue != null) _noteSeValue.text = Describe(NoteSeLabels, _settings.NoteSeIndex);
            if (_noteEffectValue != null) _noteEffectValue.text = Describe(NoteEffectLabels, _settings.NoteEffect);
            if (_traceValue != null) _traceValue.text = LiveTrace.Enabled ? TraceOnLabel : TraceOffLabel;
            if (_performanceHudValue != null) _performanceHudValue.text = PerformanceHud.Enabled ? "开" : "关";
            if (_highlightLastValue != null) _highlightLastValue.text = LiveTrace.HighlightLast ? "开" : "关";
            for (int i = 0; i < _traceCategoryValues.Length; i++)
            {
                if (_traceCategoryValues[i] != null)
                    _traceCategoryValues[i].text = (LiveTrace.Categories & TraceCategories[i]) != 0 ? "开" : "关";
            }
        }

        private static string Describe(string[] labels, int index)
        {
            int clamped = Mathf.Clamp(index, 0, labels.Length - 1);
            return labels[clamped];
        }

        private void SaveSettings()
        {
            LiveSettingsStore.Shared.Save(_settings);
            _onChanged?.Invoke();
        }

        private void SaveLocal()
        {
            ApplicationLocalSettings.SaveToStorage(_localSettings);
            if (SoundManager.Instance != null && _localSettings?.LiveVolume != null)
            {
                SoundManager.Instance.SetupVolume(1f, _localSettings.LiveVolume.Bgm, _localSettings.LiveVolume.Se, _localSettings.LiveVolume.Voice);
            }
            _onChanged?.Invoke();
        }
    }
}
