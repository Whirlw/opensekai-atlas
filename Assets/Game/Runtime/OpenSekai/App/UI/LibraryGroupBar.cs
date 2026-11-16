using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// One row of chips above the list: 全部 plus one chip per group, each with its package count.
    /// Picking a chip narrows the list to that group. The row scrolls sideways when the chips do
    /// not fit.
    /// </summary>
    internal sealed class LibraryGroupBar : MonoBehaviour
    {
        internal const float HEIGHT = 48f;

        private const float CHIP_SPACING = 8f;
        private const float CHIP_PADDING = 20f;
        private const string ALL_LABEL = "全部";

        private readonly List<Chip> _chips = new List<Chip>();
        private RectTransform _content;
        private Action<string> _onPick;

        internal static LibraryGroupBar Create(Transform parent)
        {
            var root = new GameObject("GroupBar", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
            root.transform.SetParent(parent, false);
            LibraryGroupBar bar = root.AddComponent<LibraryGroupBar>();

            bar._content = LibraryUi.CreateRect("Content", root.transform);
            bar._content.anchorMin = new Vector2(0f, 0f);
            bar._content.anchorMax = new Vector2(0f, 1f);
            bar._content.pivot = new Vector2(0f, 0.5f);
            bar._content.anchoredPosition = Vector2.zero;

            ScrollRect scroll = root.GetComponent<ScrollRect>();
            scroll.viewport = (RectTransform)root.transform;
            scroll.content = bar._content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            return bar;
        }

        /// <param name="allCount">Packages across every group, shown on the 全部 chip.</param>
        /// <param name="groups">Groups in display order.</param>
        /// <param name="selected">Name of the chosen group; null selects 全部.</param>
        /// <param name="onPick">Called with a group name, or null for 全部.</param>
        internal void Set(int allCount, IReadOnlyList<LibraryGroup> groups, string selected, Action<string> onPick)
        {
            _onPick = onPick;
            int count = groups.Count + 1;
            while (_chips.Count < count)
            {
                _chips.Add(CreateChip());
            }

            float x = 0f;
            for (int i = 0; i < _chips.Count; i++)
            {
                Chip chip = _chips[i];
                bool used = i < count;
                chip.Root.gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                chip.Group = i == 0 ? null : groups[i - 1].Name;
                bool current = string.Equals(chip.Group, selected, StringComparison.Ordinal);
                string name = i == 0 ? ALL_LABEL : groups[i - 1].Name;
                int size = i == 0 ? allCount : groups[i - 1].Count;
                chip.Label.text = name;
                chip.Count.text = size.ToString();
                chip.Count.color = current ? LibraryTheme.Colors.SurfaceSelected : LibraryTheme.Colors.TextMuted;
                chip.Label.color = current ? LibraryTheme.Colors.TextOnAccent : LibraryTheme.Colors.TextPrimary;
                chip.Background.color = current ? LibraryTheme.Colors.TextPrimary : LibraryTheme.Colors.Control;

                float labelWidth = Mathf.Ceil(chip.Label.GetPreferredValues(name).x);
                float countWidth = Mathf.Ceil(chip.Count.GetPreferredValues(chip.Count.text).x);
                float countOffset = CHIP_PADDING + labelWidth + CHIP_SPACING;
                float width = countOffset + countWidth + CHIP_PADDING;
                Vector2 left = new Vector2(0f, 0.5f);
                LibraryWidgets.Place(chip.Label.rectTransform, left, left, new Vector2(CHIP_PADDING, 0f),
                    new Vector2(labelWidth, HEIGHT));
                LibraryWidgets.Place(chip.Count.rectTransform, left, left, new Vector2(countOffset, 0f),
                    new Vector2(countWidth, HEIGHT));
                chip.Root.sizeDelta = new Vector2(width, HEIGHT);
                chip.Root.anchoredPosition = new Vector2(x, 0f);
                x += width + CHIP_SPACING;
            }

            _content.sizeDelta = new Vector2(Mathf.Max(0f, x - CHIP_SPACING), 0f);
        }

        private Chip CreateChip()
        {
            Image background = LibraryWidgets.Panel("Chip", _content, HEIGHT * 0.5f, LibraryTheme.Colors.Control);
            background.raycastTarget = true;
            RectTransform root = background.rectTransform;
            root.anchorMin = new Vector2(0f, 0.5f);
            root.anchorMax = new Vector2(0f, 0.5f);
            root.pivot = new Vector2(0f, 0.5f);

            TextMeshProUGUI label = LibraryWidgets.Label(
                "Label", root, string.Empty, 19f, LibraryTheme.Colors.TextPrimary);
            label.overflowMode = TextOverflowModes.Overflow;
            TextMeshProUGUI count = LibraryWidgets.Label("Count", root, string.Empty, 20f, LibraryTheme.Colors.TextMuted);
            LibraryUi.UseCondensedFont(count);
            count.overflowMode = TextOverflowModes.Overflow;

            var chip = new Chip { Root = root, Background = background, Label = label, Count = count };
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => _onPick?.Invoke(chip.Group));
            LibraryPressFeedback.Attach(background.gameObject);
            return chip;
        }

        private sealed class Chip
        {
            internal RectTransform Root;
            internal Image Background;
            internal TextMeshProUGUI Label;
            internal TextMeshProUGUI Count;
            internal string Group;
        }
    }
}
