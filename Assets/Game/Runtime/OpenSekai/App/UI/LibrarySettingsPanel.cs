using System;
using System.Collections.Generic;
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
    /// Settings card of the player library, in the page's look: grouped rows in two columns for
    /// timing, display, sound, appearance and developer options, and a 追踪选项 sub-page for the
    /// trace categories. It edits the existing settings objects (<see cref="LiveSettingData"/> /
    /// <see cref="ApplicationLocalSettings"/>) so nothing about the stored format changes, and
    /// never touches editor-only preferences.
    /// </summary>
    public sealed class LibrarySettingsPanel : MonoBehaviour
    {
        private const float CARD_WIDTH = 1360f;
        private const float CARD_HEIGHT = 880f;
        private const float COLUMN_GAP = 40f;
        private const float FOOTER_HEIGHT = 44f;

        // LiveConfig clamps the note skin to 0..1 and both skins ship as custom01/custom02.
        private static readonly string[] _skinLabels = { "样式 1", "样式 2" };
        private static readonly string[] _noteSeLabels = { "音效 1", "音效 2" };
        private static readonly string[] _noteEffectLabels = { "特效 1", "特效 2" };
        private static readonly string[] _judgeTimingLabels = { "触摸时刻", "下一帧(旧式)" };
        private static readonly string[] _pressMatchingLabels = { "按时间顺序", "同帧候选(旧式)" };

        private static readonly LiveTraceCategory[] _inputCategories =
        {
            LiveTraceCategory.RawTouch, LiveTraceCategory.Press, LiveTraceCategory.Resident,
            LiveTraceCategory.Release, LiveTraceCategory.Judge
        };
        private static readonly string[] _inputCategoryLabels = { "原始触摸（高频）", "按下事件", "常驻通道（高频）", "抬起事件", "判定结果" };
        private static readonly LiveTraceCategory[] _viewCategories =
        {
            LiveTraceCategory.Hold, LiveTraceCategory.Tail, LiveTraceCategory.Swipe, LiveTraceCategory.Pool,
            LiveTraceCategory.View, LiveTraceCategory.Frame, LiveTraceCategory.Audio
        };
        private static readonly string[] _viewCategoryLabels = { "长条持握", "长条尾键", "划动", "对象回收", "画面表现", "画面逐帧（高频）", "音频同步" };

        private LiveSettingData _settings;
        private ApplicationLocalSettings _localSettings;
        private Action _onChanged;

        private LibraryModal _modal;
        private readonly LibrarySettingRows _rows = new LibrarySettingRows();
        private GameObject _tracePanel;
        private ScrollRect _mainScroll;

        public static LibrarySettingsPanel Create(Transform parent, Action onChanged)
        {
            RectTransform root = LibraryUi.CreateRect("SettingsPanel", parent);
            LibraryUi.Stretch(root);
            LibrarySettingsPanel panel = root.gameObject.AddComponent<LibrarySettingsPanel>();
            panel._onChanged = onChanged;
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

            _rows.Refresh();
            if (_tracePanel != null) _tracePanel.SetActive(false);
            if (_mainScroll != null) _mainScroll.verticalNormalizedPosition = 1f;
            _modal.Open();
        }

        public void Hide()
        {
            if (_tracePanel != null) _tracePanel.SetActive(false);
            _modal.Close();
        }

        internal bool IsOpen => _modal != null && _modal.IsOpen;

        /// <summary>The Android back key: leaves the trace page first, then closes the panel.</summary>
        internal void Back()
        {
            if (_tracePanel != null && _tracePanel.activeSelf)
            {
                CloseTrace();
                UiSound.Play(UiCue.Back);
                return;
            }

            Hide();
            UiSound.Play(UiCue.Close);
        }

        /// <summary>Color of switches and the current choice; follows the selected jacket.</summary>
        internal void SetAccent(Color32 accent) => _rows.SetAccent(accent);

        private void OnDestroy()
        {
            _modal?.Dispose();
            _rows.Dispose();
        }

        private void Build()
        {
            var root = (RectTransform)transform;
            _modal = new LibraryModal(root, new Vector2(CARD_WIDTH, CARD_HEIGHT), "设置", Hide);

            // Dropdown lists draw above the card, so they get their own layer on top of it.
            RectTransform overlay = LibraryUi.CreateRect("Overlay", root);
            LibraryUi.Stretch(overlay);

            RectTransform content = ScrollBody(_modal.Body, out _mainScroll);
            float columnWidth = (CARD_WIDTH - LibraryTheme.Layout.MODAL_PADDING * 2f - COLUMN_GAP) * 0.5f;
            float rightStart = columnWidth + COLUMN_GAP;
            float gap = LibraryTheme.Layout.SECTION_GAP;

            // Left column: timing, then display.
            float top = 0f;
            LibrarySettingRows.Section timing = _rows.AddSection(content, "判定与延迟", 5, 0f, rightStart, top);
            // See TimingCompensation for what each one moves.
            _rows.AddMilliseconds(timing, "音频延迟",
                () => _settings.AudioLatencyMs,
                value => { _settings.AudioLatencyMs = TimingCompensation.ClampAudioLatency(value); SaveSettings(); });
            _rows.AddMilliseconds(timing, "画面延迟",
                () => _settings.VisualLatencyMs,
                value => { _settings.VisualLatencyMs = TimingCompensation.ClampVisualLatency(value); SaveSettings(); });
            _rows.AddMilliseconds(timing, "判定偏移（正 = 打得晚）",
                () => _settings.JudgeOffsetMs,
                value => { _settings.JudgeOffsetMs = TimingCompensation.ClampJudgeOffset(value); SaveSettings(); });
            _rows.AddSegmented(timing, "判定时机", _judgeTimingLabels,
                () => _settings.UseLegacyJudgeTiming ? 1 : 0,
                index => { _settings.UseLegacyJudgeTiming = index == 1; SaveSettings(); }, 150f);
            // Legacy judge timing flattens timestamps, so it locks the old per-frame matching; the player's own
            // choice is kept and comes back when legacy timing is turned off.
            _rows.AddSegmented(timing, "多押分配", _pressMatchingLabels,
                () => _settings.UseLegacyJudgeTiming || _settings.UseFramePressMatching ? 1 : 0,
                index => { _settings.UseFramePressMatching = index == 1; SaveSettings(); }, 150f,
                locked: () => _settings.UseLegacyJudgeTiming);
            top += LibrarySettingRows.SectionHeight(5) + gap;

            LibrarySettingRows.Section display = _rows.AddSection(content, "画面", 5, 0f, rightStart, top);
            _rows.AddSwitch(display, "播放开场动画",
                () => _localSettings.PlayStartupAnimation,
                value => { _localSettings.PlayStartupAnimation = value; SaveLocal(); });
            _rows.AddStepper(display, "音符流速",
                () => _settings.NoteSpeed,
                value => { _settings.NoteSpeed = value; SaveSettings(); },
                0.5f, 12f, 0.1f, "0.0");
            _rows.AddSwitch(display, "同时押线",
                () => _settings.UseSimultaneousPushingLine,
                value => { _settings.UseSimultaneousPushingLine = value; SaveSettings(); });
            int[] limits = FramerateUtility.SupportedLimits;
            var limitLabels = new List<string>(limits.Length);
            foreach (int limit in limits)
            {
                limitLabels.Add(FramerateUtility.GetLimitLabel(limit));
            }
            _rows.AddDropdown(display, "帧率上限", overlay, limitLabels,
                () => Array.IndexOf(limits, FramerateUtility.SanitizeLimit(_settings.FrameRateLimit)),
                index =>
                {
                    _settings.FrameRateLimit = limits[index];
                    FramerateUtility.ApplyFrameRateLimit(limits[index]);
                    SaveSettings();
                });
            _rows.AddStepper(display, "2D MV 亮度",
                () => Mathf.Clamp01(_settings.MovieBrightness),
                value => { _settings.MovieBrightness = value; SaveSettings(); },
                0f, 1f, 0.1f, "0.0", Percent);
            float leftHeight = top + LibrarySettingRows.SectionHeight(5);

            // Right column: sound, appearance, developer.
            top = 0f;
            LibrarySettingRows.Section sound = _rows.AddSection(content, "声音", 4, rightStart, 0f, top);
            _rows.AddStepper(sound, "音乐音量",
                () => _localSettings.LiveVolume.Bgm,
                value => { _localSettings.LiveVolume.Bgm = value; SaveLocal(); },
                0f, 1f, 0.1f, "0.0", Percent);
            _rows.AddStepper(sound, "音效音量",
                () => _localSettings.LiveVolume.Se,
                value => { _localSettings.LiveVolume.Se = value; SaveLocal(); },
                0f, 1f, 0.1f, "0.0", Percent);
            // Menu pages only; each step plays at the new level, so the row is its own preview.
            _rows.AddStepper(sound, "界面音量",
                () => _localSettings.UiVolume,
                value => { _localSettings.UiVolume = value; UiSound.Volume = value; SaveLocal(); },
                0f, 1f, 0.1f, "0.0", Percent);
            // Preview and live music alike; a playing preview follows the switch at once.
            _rows.AddSwitch(sound, "响度平均",
                () => _localSettings.LoudnessNormalization,
                value => { _localSettings.LoudnessNormalization = value; SaveLocal(); });
            top += LibrarySettingRows.SectionHeight(4) + gap;

            LibrarySettingRows.Section look = _rows.AddSection(content, "外观（下一次开局生效）", 3, rightStart, 0f, top);
            _rows.AddSegmented(look, "音符皮肤", _skinLabels,
                () => _settings.NoteSkinIndex, value => { _settings.NoteSkinIndex = value; SaveSettings(); });
            _rows.AddSegmented(look, "打击音", _noteSeLabels,
                () => _settings.NoteSeIndex, value => { _settings.NoteSeIndex = value; SaveSettings(); });
            _rows.AddSegmented(look, "打击特效", _noteEffectLabels,
                () => _settings.NoteEffect, value => { _settings.NoteEffect = value; SaveSettings(); });
            top += LibrarySettingRows.SectionHeight(3) + gap;

            LibrarySettingRows.Section developer = _rows.AddSection(content, "开发", 2, rightStart, 0f, top);
            _rows.AddSwitch(developer, "测试模式",
                () => LiveTrace.Enabled,
                value => { LiveTrace.Enabled = value; SaveSettings(); });
            _rows.AddLink(developer, "追踪选项", DescribeTrace, OpenTrace);
            float rightHeight = top + LibrarySettingRows.SectionHeight(2);

            content.sizeDelta = new Vector2(0f, Mathf.Max(leftHeight, rightHeight) + 8f);

            BuildTracePanel(_modal.Card);
            overlay.SetAsLastSibling();
        }

        private void BuildTracePanel(RectTransform card)
        {
            // Covers the whole card, header included, so the sub-page reads as its own screen.
            Image page = LibraryWidgets.Panel("TracePanel", card, LibraryTheme.Radius.PANEL, LibraryTheme.Colors.Surface);
            page.raycastTarget = true;
            LibraryUi.Stretch(page.rectTransform);
            LibraryWidgets.Outline(page, LibraryTheme.Radius.PANEL);
            _tracePanel = page.gameObject;
            RectTransform pageRect = page.rectTransform;

            float padding = LibraryTheme.Layout.MODAL_PADDING;
            float header = LibraryTheme.Layout.MODAL_HEADER_HEIGHT;
            const float BACK_SIZE = 64f;
            Button back = LibraryWidgets.PillButton(
                "Back", pageRect, string.Empty, CloseTrace, BACK_SIZE, BACK_SIZE,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextSecondary, sound: UiCue.Back);
            LibraryWidgets.Icon(back.transform, LibraryIcon.Back, LibraryTheme.Colors.TextSecondary, 26f);
            LibraryWidgets.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(28f, -header * 0.5f), new Vector2(BACK_SIZE, BACK_SIZE));

            TextMeshProUGUI title = LibraryWidgets.Label(
                "Title", pageRect, "追踪选项", 34f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(title.rectTransform, 28f + BACK_SIZE + 20f, padding, 26f, 46f);
            TextMeshProUGUI hint = LibraryWidgets.Label(
                "Hint", pageRect, "分类追踪需开启测试模式。延迟采样在退出打歌时保存。",
                LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(hint.rectTransform, 28f + BACK_SIZE + 20f, padding, 72f, 28f);

            RectTransform body = LibraryUi.CreateRect("Body", pageRect);
            LibraryWidgets.Inset(body, padding, padding, padding, header);
            RectTransform content = ScrollBody(body, out _);
            float columnWidth = (CARD_WIDTH - padding * 2f - COLUMN_GAP) * 0.5f;
            float rightStart = columnWidth + COLUMN_GAP;
            float gap = LibraryTheme.Layout.SECTION_GAP;

            LibrarySettingRows.Section shown = _rows.AddSection(content, "诊断工具", 4, 0f, rightStart, 0f);
            _rows.AddSwitch(shown, "性能 HUD", () => PerformanceHud.Enabled, value => PerformanceHud.Enabled = value);
            _rows.AddSwitch(shown, "Last 长条标红", () => LiveTrace.HighlightLast, value => LiveTrace.HighlightLast = value);
            _rows.AddSwitch(shown, "延迟采样", () => LatencyCapture.Enabled, value => LatencyCapture.Enabled = value);
            _rows.AddSwitch(shown, "打击音混音采样", () => LatencyCapture.MixProbeEnabled,
                value => LatencyCapture.MixProbeEnabled = value);

            float top = LibrarySettingRows.SectionHeight(4) + gap;
            LibrarySettingRows.Section input = _rows.AddSection(content, "输入与判定", _inputCategories.Length, 0f, rightStart, top);
            AddCategories(input, _inputCategories, _inputCategoryLabels);
            float leftHeight = top + LibrarySettingRows.SectionHeight(_inputCategories.Length);

            LibrarySettingRows.Section view = _rows.AddSection(content, "长条与画面", _viewCategories.Length, rightStart, 0f, 0f);
            AddCategories(view, _viewCategories, _viewCategoryLabels);
            float rightHeight = LibrarySettingRows.SectionHeight(_viewCategories.Length);

            content.sizeDelta = new Vector2(0f, Mathf.Max(leftHeight, rightHeight) + 8f);
            _tracePanel.SetActive(false);
        }

        private void AddCategories(LibrarySettingRows.Section section, LiveTraceCategory[] categories, string[] labels)
        {
            for (int i = 0; i < categories.Length; i++)
            {
                LiveTraceCategory category = categories[i];
                _rows.AddSwitch(section, labels[i],
                    () => (LiveTrace.Categories & category) != 0,
                    enabled => LiveTrace.Categories = enabled
                        ? LiveTrace.Categories | category
                        : LiveTrace.Categories & ~category);
            }
        }

        /// <summary>A vertical scroll view filling <paramref name="parent"/>; returns its top-anchored content.</summary>
        private static RectTransform ScrollBody(RectTransform parent, out ScrollRect scroll)
        {
            RectTransform viewport = LibraryUi.CreateRect("Viewport", parent);
            LibraryUi.Stretch(viewport);
            // Room under the content for the card's bottom padding to scroll into view.
            viewport.offsetMin = new Vector2(0f, -FOOTER_HEIGHT * 0.5f);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color32(0, 0, 0, 0);

            RectTransform content = LibraryUi.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;
            return content;
        }

        private void OpenTrace()
        {
            _rows.Refresh();
            _tracePanel.SetActive(true);
            _tracePanel.transform.SetAsLastSibling();
        }

        private void CloseTrace()
        {
            _tracePanel.SetActive(false);
            _rows.Refresh();
        }

        private static string DescribeTrace()
        {
            if (!LiveTrace.Enabled)
            {
                return "测试模式未开启";
            }

            int count = 0;
            foreach (LiveTraceCategory category in _inputCategories) if ((LiveTrace.Categories & category) != 0) count++;
            foreach (LiveTraceCategory category in _viewCategories) if ((LiveTrace.Categories & category) != 0) count++;
            return count + " 项已开启";
        }

        private static string Percent(float value)
        {
            return Mathf.RoundToInt(value * 100f) + "%";
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
