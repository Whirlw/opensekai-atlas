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
        private LibraryModal _timingHelp;
        private TextMeshProUGUI _timingDescription;
        private readonly LibrarySettingRows _rows = new LibrarySettingRows();
        private LibrarySettingRows.Section _developerSection;
        private GameObject _traceLink;
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

            RefreshTraceLink();
            _rows.Refresh();
            if (_tracePanel != null) _tracePanel.SetActive(false);
            if (_mainScroll != null) _mainScroll.verticalNormalizedPosition = 1f;
            _modal.Open();
        }

        public void Hide()
        {
            if (_tracePanel != null) _tracePanel.SetActive(false);
            _timingHelp.Close();
            _modal.Close();
        }

        internal bool IsOpen => _modal != null && _modal.IsOpen;

        /// <summary>The Android back key: leaves the trace page first, then closes the panel.</summary>
        internal void Back()
        {
            if (_timingHelp.IsOpen)
            {
                _timingHelp.Close();
                UiSound.Play(UiCue.Back);
                return;
            }

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
            _timingHelp?.Dispose();
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
            AddTimingRow(timing, "音频延迟",
                "调整打歌时背景音乐的播放时间。如果音符先到线、鼓点随后才响，可以增大这个值。\n\n+30 ms 会让音乐提前 30 毫秒，−30 ms 会让音乐推迟 30 毫秒。打击音仍在触发判定时播放。",
                () => _settings.AudioLatencyMs,
                value => { _settings.AudioLatencyMs = TimingCompensation.ClampAudioLatency(value); SaveSettings(); });
            AddTimingRow(timing, "画面延迟",
                "调整音符到达判定线的时间。如果听到鼓点时音符还在判定线上方，可以增大这个值。\n\n+30 ms 会让音符提前 30 毫秒到线，−30 ms 会让音符推迟 30 毫秒到线。",
                () => _settings.VisualLatencyMs,
                value => { _settings.VisualLatencyMs = TimingCompensation.ClampVisualLatency(value); SaveSettings(); });
            AddTimingRow(timing, "判定延迟",
                "调整手动游玩时的判定窗口。如果打在节拍上却经常显示 LATE，可以增大这个值；经常显示 FAST 则可以减小。\n\n+30 ms 会让判定中心推迟 30 毫秒，−30 ms 会让它提前 30 毫秒。自动游玩忽略此项。",
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

            const int DISPLAY_ROWS = 11;
            LibrarySettingRows.Section display = _rows.AddSection(content, "画面", DISPLAY_ROWS, 0f, rightStart, top);
            _rows.AddSwitch(display, "播放开场动画",
                () => _localSettings.PlayStartupAnimation,
                value => { _localSettings.PlayStartupAnimation = value; SaveLocal(); });
            _rows.AddStepper(display, "音符流速",
                () => _settings.NoteSpeed,
                value => { _settings.NoteSpeed = value; SaveSettings(); },
                0.5f, 12f, 0.1f, "0.0");
            _rows.AddStepper(display, "长条条身不透明度",
                () => _settings.LongNoteBodyOpacity,
                value => { _settings.LongNoteBodyOpacity = value; SaveSettings(); },
                0f, 1f, 0.1f, "0.0", Percent);
            _rows.AddSwitch(display, "同时押线",
                () => _settings.UseSimultaneousPushingLine,
                value => { _settings.UseSimultaneousPushingLine = value; SaveSettings(); });
            _rows.AddSwitch(display, "显示准度条",
                () => _settings.ShowAccuracyBar,
                value => { _settings.ShowAccuracyBar = value; SaveSettings(); });
            _rows.AddStepper(display, "准度条高度",
                () => Mathf.Clamp01(_settings.AccuracyBarHeight),
                value => { _settings.AccuracyBarHeight = value; SaveSettings(); },
                0f, 1f, 0.05f, "0.00", Percent);
            _rows.AddSwitch(display, "显示 FAST / LATE",
                () => _settings.ShowFastLate,
                value => { _settings.ShowFastLate = value; SaveSettings(); });
            _rows.AddSwitch(display, "显示滑反提示",
                () => _settings.ShowFlickMiss,
                value => { _settings.ShowFlickMiss = value; SaveSettings(); });
            _rows.AddSwitch(display, "减法分数",
                () => _settings.UseSubtractiveScore,
                value => { _settings.UseSubtractiveScore = value; SaveSettings(); });
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
            float leftHeight = top + LibrarySettingRows.SectionHeight(DISPLAY_ROWS);

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

            // Note skin, hit sound and hit effect sets belong to the resource pack page now.
            LibrarySettingRows.Section look = _rows.AddSection(content, "外观（下一次开局生效）", 1, rightStart, 0f, top);
            _rows.AddLink(look, "音符皮肤、打击音与特效", DescribeResourcePacks, OpenResourcePacks);
            top += LibrarySettingRows.SectionHeight(1) + gap;

            _developerSection = _rows.AddSection(content, "开发", 2, rightStart, 0f, top);
            _rows.AddSwitch(_developerSection, "测试模式",
                () => LiveTrace.Enabled,
                value =>
                {
                    LiveTrace.Enabled = value;
                    RefreshTraceLink();
                    SaveSettings();
                });
            _traceLink = _rows.AddLink(_developerSection, "追踪选项", DescribeTrace, OpenTrace);
            RefreshTraceLink();
            float rightHeight = top + LibrarySettingRows.SectionHeight(2);

            content.sizeDelta = new Vector2(0f, Mathf.Max(leftHeight, rightHeight) + 8f);

            BuildTracePanel(_modal.Card);
            overlay.SetAsLastSibling();

            RectTransform helpRoot = LibraryUi.CreateRect("TimingHelp", root);
            LibraryUi.Stretch(helpRoot);
            _timingHelp = new LibraryModal(helpRoot, new Vector2(760f, 420f), string.Empty, () => _timingHelp.Close());
            _timingDescription = LibraryWidgets.Label("Description", _timingHelp.Body, string.Empty, 24f,
                LibraryTheme.Colors.TextPrimary, alignment: TextAlignmentOptions.TopLeft);
            _timingDescription.textWrappingMode = TextWrappingModes.Normal;
            LibraryUi.Stretch(_timingDescription.rectTransform);
            _timingHelp.Close();
        }

        private void AddTimingRow(LibrarySettingRows.Section section, string title, string description,
            Func<float> get, Action<float> set)
        {
            _rows.AddMilliseconds(section, title, get, set, () =>
            {
                _timingHelp.Title.text = title;
                _timingDescription.text = description + "\n\n设为 0 可取消此项调整。修改从下次开局生效。";
                _timingHelp.Open();
            });
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
                "Hint", pageRect, "延迟采样在退出打歌时保存。",
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

        private void RefreshTraceLink()
        {
            bool enabled = LiveTrace.Enabled;
            _traceLink.SetActive(enabled);
            _developerSection.SetVisibleRowCount(enabled ? 2 : 1);
        }

        private void OpenTrace()
        {
            if (!LiveTrace.Enabled) return;

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
            int count = 0;
            foreach (LiveTraceCategory category in _inputCategories) if ((LiveTrace.Categories & category) != 0) count++;
            foreach (LiveTraceCategory category in _viewCategories) if ((LiveTrace.Categories & category) != 0) count++;
            return count + " 项已开启";
        }

        /// <summary>What the next live uses, as the link to the resource pack page shows it.</summary>
        private static string DescribeResourcePacks()
        {
            try
            {
                var state = OpenSekai.Infrastructure.Packages.MaterialPackStore.Shared.List();
                if (!state.IsPresetMode) return "资源包 · 混搭";
                var pack = state.PresetPack;
                string name = state.Preset.IsBuiltin ? PackScreen.BUILTIN_BUNDLE_NAME : pack?.Preset?.Name ?? state.Preset.PackId;
                return "资源包 · 预设「" + name + "」";
            }
            catch (Exception)
            {
                return "在资源包页设置";
            }
        }

        private void OpenResourcePacks()
        {
            Hide();
            HomeScreen.Current?.OpenResourcePacks();
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
