using System;
using System.Collections.Generic;
using System.Globalization;
using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Grouped setting rows in the library's look: a muted caption over a rounded group whose
    /// lines are split by hairlines, each line a label on the left and its control on the right
    /// (switch, stepper, millisecond field, segmented choice, dropdown or a link to a sub-page).
    /// Every control reads its value through a getter, so one <see cref="Refresh"/> redraws them
    /// all after any change.
    /// </summary>
    internal sealed class LibrarySettingRows : IDisposable
    {
        private const float CONTROL_HEIGHT = 52f;
        private const float STEP_BUTTON = 52f;
        private const float LABEL_INSET = 24f;
        private const float CONTROL_INSET = 14f;
        private const float SWITCH_WIDTH = 64f;
        private const float SWITCH_HEIGHT = 36f;
        private const float KNOB = 28f;

        private readonly List<Action> _refreshers = new List<Action>();
        private readonly List<Action<Color32>> _accentListeners = new List<Action<Color32>>();
        private readonly List<IDisposable> _motions = new List<IDisposable>();
        private Color32 _accent = LibraryTheme.Colors.DefaultAccent;
        private bool _animate;

        internal Color32 Accent => _accent;

        internal void SetAccent(Color32 accent)
        {
            _accent = accent;
            foreach (Action<Color32> listener in _accentListeners)
            {
                listener(accent);
            }
        }

        /// <summary>Redraws every control from its getter; animated only for a change the user made.</summary>
        internal void Refresh(bool animate = false)
        {
            _animate = animate;
            foreach (Action refresh in _refreshers)
            {
                refresh();
            }
            _animate = false;
        }

        public void Dispose()
        {
            foreach (IDisposable motion in _motions)
            {
                motion.Dispose();
            }
        }

        // ------------------------------------------------------------------ layout

        /// <summary>Height a section of <paramref name="rows"/> lines takes, caption included.</summary>
        internal static float SectionHeight(int rows)
        {
            return LibraryTheme.Layout.SECTION_CAPTION_HEIGHT + rows * LibraryTheme.Layout.SETTING_ROW_HEIGHT;
        }

        /// <summary>A caption and a rounded group, anchored to the top of <paramref name="parent"/>.</summary>
        internal Section AddSection(RectTransform parent, string caption, int rows, float left, float right, float top)
        {
            TextMeshProUGUI captionText = LibraryWidgets.Label(
                "Caption-" + caption, parent, caption, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted, FontStyles.Bold);
            LibraryWidgets.TopStrip(captionText.rectTransform, left + 8f, right, top, LibraryTheme.Layout.SECTION_CAPTION_HEIGHT - 10f);
            captionText.alignment = TextAlignmentOptions.BottomLeft;

            Image group = LibraryWidgets.Panel("Section-" + caption, parent, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
            LibraryWidgets.TopStrip(group.rectTransform, left, right,
                top + LibraryTheme.Layout.SECTION_CAPTION_HEIGHT, rows * LibraryTheme.Layout.SETTING_ROW_HEIGHT);
            LibraryWidgets.Outline(group, LibraryTheme.Radius.ROW);
            return new Section(group.rectTransform);
        }

        internal sealed class Section
        {
            private readonly RectTransform _group;
            private int _count;

            internal Section(RectTransform group)
            {
                _group = group;
            }

            /// <summary>Next line of the group, with its label; the control goes on its right.</summary>
            internal RectTransform AddRow(string label, out TextMeshProUGUI labelText)
            {
                float height = LibraryTheme.Layout.SETTING_ROW_HEIGHT;
                RectTransform row = LibraryUi.CreateRect("Row-" + label, _group);
                LibraryWidgets.TopStrip(row, 0f, 0f, _count * height, height);
                if (_count > 0)
                {
                    Image divider = LibraryUi.CreateImage("Divider", row, LibraryTheme.Colors.Hairline);
                    divider.raycastTarget = false;
                    LibraryWidgets.TopStrip(divider.rectTransform, LABEL_INSET, 0f, 0f, 1f);
                }

                labelText = LibraryWidgets.Label(
                    "Label", row, label, 22f, LibraryTheme.Colors.TextPrimary);
                LibraryWidgets.Inset(labelText.rectTransform, LABEL_INSET, 0f, 0f, 0f);
                _count++;
                return row;
            }
        }

        // ------------------------------------------------------------------ controls

        /// <summary>On/off switch; the whole line toggles it.</summary>
        internal void AddSwitch(Section section, string label, Func<bool> get, Action<bool> set)
        {
            RectTransform row = section.AddRow(label, out TextMeshProUGUI labelText);
            labelText.rectTransform.offsetMax = new Vector2(-(SWITCH_WIDTH + CONTROL_INSET * 2f), 0f);
            Button button = MakeRowButton(row);

            Image track = LibraryWidgets.Panel("Switch", row, SWITCH_HEIGHT * 0.5f, LibraryTheme.Colors.ControlActive);
            LibraryWidgets.Place(track.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-CONTROL_INSET - 10f, 0f), new Vector2(SWITCH_WIDTH, SWITCH_HEIGHT));
            Image knob = LibraryWidgets.Panel("Knob", track.transform, KNOB * 0.5f, LibraryTheme.Colors.TextPrimary);
            RectTransform knobRect = knob.rectTransform;
            LibraryWidgets.Place(knobRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(KNOB, KNOB));

            var motion = new MotionSlot();
            _motions.Add(motion);
            bool on = false;
            void Apply(bool value, bool animate)
            {
                on = value;
                track.color = value ? _accent : (Color32)LibraryTheme.Colors.ControlActive;
                float x = value ? SWITCH_WIDTH - KNOB - 4f : 4f;
                motion.Handle.TryCancel();
                if (animate)
                {
                    motion.Handle = LMotion.Create(knobRect.anchoredPosition.x, x, LibraryTheme.Motion.STANDARD)
                        .WithEase(LibraryTheme.Motion.SELECT_EASE)
                        .BindToAnchoredPositionX(knobRect);
                }
                else
                {
                    knobRect.anchoredPosition = new Vector2(x, 0f);
                }
            }

            button.onClick.AddListener(() =>
            {
                set(!get());
                Refresh(true);
                UiSound.Play(get() ? UiCue.ToggleOn : UiCue.ToggleOff);
            });
            _refreshers.Add(() => Apply(get(), _animate && on != get()));
            _accentListeners.Add(_ => Apply(on, false));
        }

        /// <summary>A − value + stepper for a number clamped to [min, max].</summary>
        internal void AddStepper(
            Section section, string label, Func<float> get, Action<float> set,
            float min, float max, float step, string format, Func<float, string> display = null)
        {
            RectTransform row = section.AddRow(label, out TextMeshProUGUI labelText);
            const float valueWidth = 104f;
            float width = STEP_BUTTON * 2f + valueWidth;
            labelText.rectTransform.offsetMax = new Vector2(-(width + CONTROL_INSET * 2f), 0f);

            RectTransform group = ControlGroup(row, width);
            StepButton(group, LibraryIcon.Minus, new Vector2(0f, 0.5f), () => Step(-step), UiCue.None);
            StepButton(group, LibraryIcon.Add, new Vector2(1f, 0.5f), () => Step(step), UiCue.None);
            TextMeshProUGUI value = LibraryWidgets.Label(
                "Value", group, string.Empty, 30f, LibraryTheme.Colors.TextPrimary, FontStyles.Normal, TextAlignmentOptions.Center);
            LibraryUi.UseCondensedFont(value);
            LibraryWidgets.Inset(value.rectTransform, STEP_BUTTON, 0f, STEP_BUTTON, 0f);

            void Step(float delta)
            {
                // Round to the step so repeated float additions never drift off the grid.
                float next = Mathf.Round((get() + delta) / step) * step;
                float before = get();
                set(Mathf.Clamp(next, min, max));
                Refresh(true);
                // After set, so a volume row is heard at its new level; at either end it says so.
                UiSound.Play(Mathf.Approximately(before, get()) ? UiCue.Blocked : UiCue.Press);
            }

            _refreshers.Add(() => value.text = display != null
                ? display(get())
                : get().ToString(format, CultureInfo.InvariantCulture));
        }

        /// <summary>A − field + group for whole milliseconds: tap to type, or nudge by one.</summary>
        internal void AddMilliseconds(Section section, string label, Func<float> get, Action<float> set)
        {
            RectTransform row = section.AddRow(label, out TextMeshProUGUI labelText);
            const float fieldWidth = 136f;
            float width = STEP_BUTTON * 2f + fieldWidth + 16f;
            labelText.rectTransform.offsetMax = new Vector2(-(width + CONTROL_INSET * 2f), 0f);

            RectTransform group = ControlGroup(row, width);
            StepButton(group, LibraryIcon.Minus, new Vector2(0f, 0.5f), () => Nudge(-1f));
            StepButton(group, LibraryIcon.Add, new Vector2(1f, 0.5f), () => Nudge(1f));

            Image background = LibraryWidgets.Panel("Field", group, CONTROL_HEIGHT * 0.5f, LibraryTheme.Colors.Control);
            background.raycastTarget = true;
            LibraryWidgets.Place(background.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(fieldWidth, CONTROL_HEIGHT));
            LibraryWidgets.Outline(background, CONTROL_HEIGHT * 0.5f);

            TextMeshProUGUI unit = LibraryWidgets.Label(
                "Unit", background.transform, "ms", LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Right);
            LibraryWidgets.Inset(unit.rectTransform, 0f, 0f, 18f, 0f);

            // Configured while inactive: TMP_InputField builds its caret on enable and needs the
            // text component by then.
            background.gameObject.SetActive(false);
            RectTransform area = LibraryUi.CreateRect("TextArea", background.transform);
            LibraryWidgets.Inset(area, 14f, 4f, 48f, 4f);
            area.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI text = LibraryWidgets.Label(
                "Text", area, string.Empty, 30f, LibraryTheme.Colors.TextPrimary, FontStyles.Normal, TextAlignmentOptions.Right);
            LibraryUi.UseCondensedFont(text);
            text.overflowMode = TextOverflowModes.Overflow;
            LibraryUi.Stretch(text.rectTransform);

            TMP_InputField input = background.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.targetGraphic = background;
            input.transition = Selectable.Transition.None;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.characterLimit = 5;
            input.fontAsset = text.font;
            input.pointSize = 30f;
            input.caretColor = LibraryTheme.Colors.TextPrimary;
            input.customCaretColor = true;
            input.selectionColor = new Color32(157, 184, 255, 96);
            input.onSelect.AddListener(_ => background.color = LibraryTheme.Colors.ControlActive);
            input.onEndEdit.AddListener(raw =>
            {
                background.color = LibraryTheme.Colors.Control;
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ms))
                {
                    set(ms);
                }
                Refresh();
            });
            background.gameObject.SetActive(true);

            void Nudge(float delta)
            {
                set(Mathf.Round(get()) + delta);
                Refresh(true);
            }

            _refreshers.Add(() =>
            {
                if (!input.isFocused)
                {
                    input.SetTextWithoutNotify(Mathf.RoundToInt(get()).ToString(CultureInfo.InvariantCulture));
                }
            });
        }

        /// <summary>A pill of equal segments, one per choice; the chosen one is filled.</summary>
        /// <param name="locked">When it answers true the control shows <paramref name="get"/> dimmed and ignores taps.</param>
        internal void AddSegmented(Section section, string label, string[] options, Func<int> get, Action<int> set,
            float segmentWidth = 132f, Func<bool> locked = null)
        {
            RectTransform row = section.AddRow(label, out TextMeshProUGUI labelText);
            float width = segmentWidth * options.Length + 8f;
            labelText.rectTransform.offsetMax = new Vector2(-(width + CONTROL_INSET * 2f), 0f);

            RectTransform group = ControlGroup(row, width);
            CanvasGroup lockFade = locked != null ? group.gameObject.AddComponent<CanvasGroup>() : null;
            Image track = LibraryWidgets.Panel("Track", group, CONTROL_HEIGHT * 0.5f, LibraryTheme.Colors.Control);
            LibraryUi.Stretch(track.rectTransform);
            LibraryWidgets.Outline(track, CONTROL_HEIGHT * 0.5f);

            float inner = CONTROL_HEIGHT - 8f;
            Image highlight = LibraryWidgets.Panel("Selected", group, inner * 0.5f, LibraryTheme.Colors.TextPrimary);
            RectTransform highlightRect = highlight.rectTransform;
            LibraryWidgets.Place(highlightRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(4f, 0f), new Vector2(segmentWidth, inner));

            var labels = new TextMeshProUGUI[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                Image hit = LibraryUi.CreateImage("Option" + i, group, new Color32(0, 0, 0, 0));
                LibraryWidgets.Place(hit.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(4f + segmentWidth * i, 0f), new Vector2(segmentWidth, inner));
                Button button = hit.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = hit;
                button.onClick.AddListener(() =>
                {
                    if (locked?.Invoke() == true)
                    {
                        return;
                    }

                    if (get() != index)
                    {
                        set(index);
                        Refresh(true);
                        UiSound.Play(UiCue.Select);
                    }
                });
                labels[i] = LibraryWidgets.Label(
                    "Label", hit.transform, options[i], LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextSecondary,
                    FontStyles.Normal, TextAlignmentOptions.Center);
                LibraryWidgets.Inset(labels[i].rectTransform, 6f, 0f, 6f, 0f);
            }

            var motion = new MotionSlot();
            _motions.Add(motion);
            _refreshers.Add(() =>
            {
                if (lockFade != null)
                {
                    lockFade.alpha = locked() ? 0.45f : 1f;
                }

                int selected = Mathf.Clamp(get(), 0, options.Length - 1);
                for (int i = 0; i < labels.Length; i++)
                {
                    bool current = i == selected;
                    labels[i].color = current ? LibraryTheme.Colors.TextOnAccent : LibraryTheme.Colors.TextSecondary;
                    LibraryUi.ApplyCjkTextStyle(labels[i], current ? FontStyles.Bold : FontStyles.Normal);
                }

                float x = 4f + segmentWidth * selected;
                motion.Handle.TryCancel();
                if (_animate)
                {
                    motion.Handle = LMotion.Create(highlightRect.anchoredPosition.x, x, LibraryTheme.Motion.STANDARD)
                        .WithEase(LibraryTheme.Motion.ENTER_EASE)
                        .BindToAnchoredPositionX(highlightRect);
                }
                else
                {
                    highlightRect.anchoredPosition = new Vector2(x, 0f);
                }
            });
        }

        /// <summary>A dropdown of choices, for a list too long for segments.</summary>
        internal void AddDropdown(
            Section section, string label, RectTransform overlay, IReadOnlyList<string> options,
            Func<int> get, Action<int> set, float width = 236f)
        {
            RectTransform row = section.AddRow(label, out TextMeshProUGUI labelText);
            labelText.rectTransform.offsetMax = new Vector2(-(width + CONTROL_INSET * 2f), 0f);
            RectTransform group = ControlGroup(row, width);
            LibraryDropdown dropdown = LibraryDropdown.Create(group, overlay, string.Empty, width, CONTROL_HEIGHT);
            LibraryUi.Stretch((RectTransform)dropdown.transform);

            void Bind()
            {
                dropdown.SetOptions(options, get(), index =>
                {
                    set(index);
                    Refresh(true);
                });
            }

            _refreshers.Add(Bind);
            _accentListeners.Add(dropdown.SetAccent);
        }

        /// <summary>A line that opens a sub-page: a muted summary and a chevron.</summary>
        internal void AddLink(Section section, string label, Func<string> describe, Action open)
        {
            RectTransform row = section.AddRow(label, out TextMeshProUGUI labelText);
            labelText.rectTransform.offsetMax = new Vector2(-260f, 0f);
            Button button = MakeRowButton(row);
            button.onClick.AddListener(() => open());
            UiSound.Bind(button, UiCue.Forward);

            Image chevron = LibraryWidgets.Icon(row, LibraryIcon.ChevronRight, LibraryTheme.Colors.TextMuted, 26f);
            LibraryWidgets.Place(chevron.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-CONTROL_INSET - 6f, 0f), new Vector2(26f, 26f));
            TextMeshProUGUI value = LibraryWidgets.Label(
                "Value", row, string.Empty, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Right);
            LibraryWidgets.Place(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-CONTROL_INSET - 44f, 0f), new Vector2(220f, CONTROL_HEIGHT));
            if (describe != null)
            {
                _refreshers.Add(() => value.text = describe());
            }
        }

        // ------------------------------------------------------------------ pieces

        private static RectTransform ControlGroup(RectTransform row, float width)
        {
            RectTransform group = LibraryUi.CreateRect("Control", row);
            LibraryWidgets.Place(group, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-CONTROL_INSET, 0f), new Vector2(width, CONTROL_HEIGHT));
            return group;
        }

        private static void StepButton(
            RectTransform group, LibraryIcon icon, Vector2 anchor, Action onClick, UiCue sound = UiCue.Press)
        {
            Button button = LibraryWidgets.PillButton(
                icon.ToString(), group, string.Empty, () => onClick(), STEP_BUTTON, STEP_BUTTON,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, sound: sound);
            LibraryWidgets.Icon(button.transform, icon, LibraryTheme.Colors.TextPrimary, 24f);
            LibraryWidgets.Place((RectTransform)button.transform, anchor, anchor, Vector2.zero,
                new Vector2(STEP_BUTTON, STEP_BUTTON));
        }

        /// <summary>Makes the whole line a button that lights up while pressed.</summary>
        private static Button MakeRowButton(RectTransform row)
        {
            Image hit = row.gameObject.AddComponent<Image>();
            hit.color = new Color32(255, 255, 255, 0);
            Button button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.06f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = LibraryTheme.Motion.FAST;
            button.colors = colors;
            hit.color = Color.white;
            return button;
        }

        private sealed class MotionSlot : IDisposable
        {
            internal MotionHandle Handle;

            public void Dispose() => Handle.TryCancel();
        }
    }
}
