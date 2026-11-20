using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using OpenSekai.Infrastructure.Sonolus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The server's advanced search as a card of setting rows: one row per field the server
    /// declares (text, number, switch, choice, several choices), with the server's defaults.
    /// 搜索 sends every field; 重置 puts the defaults back. Nothing about any one server is
    /// assumed: the rows come from <c>/sonolus/levels/info</c>.
    /// </summary>
    internal sealed class SonolusSearchPanel : IDisposable
    {
        private const float WIDTH = 1240f;
        private const float HEIGHT = 900f;
        private const float FOOTER = 104f;

        private readonly ServerScreen _screen;
        private readonly Action<List<KeyValuePair<string, string>>, bool> _run;
        private readonly RectTransform _root;
        private readonly LibraryModal _modal;
        private readonly RectTransform _content;
        private readonly ScrollRect _scroll;
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool[]> _flags = new Dictionary<string, bool[]>(StringComparer.Ordinal);
        private LibrarySettingRows _rows = new LibrarySettingRows();
        private SonolusSearch _search;

        /// <param name="run">Runs the search; the flag says whether any field differs from its default.</param>
        internal SonolusSearchPanel(ServerScreen screen, Action<List<KeyValuePair<string, string>>, bool> run)
        {
            _screen = screen;
            _run = run;
            _root = LibraryUi.CreateRect("AdvancedSearch", screen.ModalLayer);
            LibraryUi.Stretch(_root);
            _modal = new LibraryModal(_root, new Vector2(WIDTH, HEIGHT), "高级搜索", Close, withSubtitle: true);
            _modal.Subtitle.text = "条件由服务器提供";

            RectTransform area = LibraryUi.CreateRect("Fields", _modal.Body);
            LibraryWidgets.Inset(area, 0f, FOOTER, 0f, 0f);
            _content = ServerWidgets.VerticalList(area, out _scroll, 0f, stack: false);

            float button = LibraryTheme.Layout.MODAL_BUTTON_HEIGHT;
            Button reset = ServerWidgets.LabelledButton(_modal.Body, "Reset", "重置", LibraryIcon.Refresh, Reset, 200f, button,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary);
            LibraryWidgets.Outline((Image)reset.targetGraphic, button * 0.5f);
            LibraryWidgets.Place((RectTransform)reset.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(200f, button));
            Button search = ServerWidgets.LabelledButton(_modal.Body, "Search", "搜索", LibraryIcon.Search, Submit, 260f, button,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.Forward);
            LibraryWidgets.Place((RectTransform)search.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(260f, button));
            _root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        internal void SetSearch(SonolusSearch search, JObject saved = null)
        {
            _search = search;
            _modal.Title.text = search != null ? ServerWidgets.Localize(search.Title) : "高级搜索";
            ResetValues();
            RestoreValues(saved);
            Rebuild();
        }

        internal void Open()
        {
            if (_search == null)
            {
                return;
            }

            _rows.SetAccent(_screen.Accent);
            _rows.Refresh();
            _scroll.verticalNormalizedPosition = 1f;
            _modal.Open();
        }

        internal void Close() => _modal.Close();

        /// <summary>Every field back to the server's default.</summary>
        internal void ResetValues()
        {
            _values.Clear();
            _flags.Clear();
            if (_search == null)
            {
                return;
            }

            foreach (SonolusSearchOption option in _search.Options)
            {
                if (option.Type == "multi")
                {
                    var flags = new bool[option.Values.Count];
                    for (int i = 0; i < flags.Length && i < option.DefaultFlags.Count; i++)
                    {
                        flags[i] = option.DefaultFlags[i];
                    }

                    _flags[option.Query] = flags;
                }
                else
                {
                    _values[option.Query] = DefaultOf(option);
                }
            }
        }

        private void RestoreValues(JObject saved)
        {
            if (_search == null || saved?["type"] is not JValue type || type.Type != JTokenType.String
                || (string)type != _search.Type || saved["values"] is not JObject values) return;

            foreach (SonolusSearchOption option in _search.Options)
            {
                if (!option.IsSupported || string.IsNullOrEmpty(option.Query)
                    || values[option.Query] is not JValue field || field.Type != JTokenType.String) continue;

                string value = (string)field;
                switch (option.Type)
                {
                    case "text":
                    case "textArea":
                        _values[option.Query] = option.Limit > 0 && value.Length > option.Limit
                            ? value.Substring(0, option.Limit) : value;
                        break;
                    case "toggle":
                        if (value == "0" || value == "1") _values[option.Query] = value;
                        break;
                    case "slider":
                        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                            && !double.IsNaN(number) && !double.IsInfinity(number))
                        {
                            double max = option.Max > option.Min ? option.Max : option.Min + 1d;
                            _values[option.Query] = FormatNumber(Math.Min(max, Math.Max(option.Min, number)));
                        }
                        break;
                    case "select":
                        if (IndexOf(option, value) >= 0) _values[option.Query] = value;
                        break;
                    case "multi":
                        var chosen = new HashSet<string>(value.Split(','), StringComparer.Ordinal);
                        bool[] flags = _flags[option.Query];
                        for (int i = 0; i < flags.Length; i++) flags[i] = chosen.Contains(option.Values[i].Name);
                        break;
                }
            }
        }

        public void Dispose()
        {
            _rows.Dispose();
            _modal.Dispose();
        }

        /// <summary>The query a search with the current values sends.</summary>
        internal List<KeyValuePair<string, string>> BuildQuery(out bool changed)
        {
            changed = false;
            var query = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("type", _search.Type) };
            foreach (SonolusSearchOption option in _search.Options)
            {
                if (!option.IsSupported || string.IsNullOrEmpty(option.Query))
                {
                    continue;
                }

                string value;
                if (option.Type == "multi")
                {
                    var chosen = new List<string>();
                    bool[] flags = _flags[option.Query];
                    for (int i = 0; i < flags.Length; i++)
                    {
                        if (flags[i])
                        {
                            chosen.Add(option.Values[i].Name);
                        }

                        bool fallback = i < option.DefaultFlags.Count && option.DefaultFlags[i];
                        changed |= flags[i] != fallback;
                    }

                    value = string.Join(",", chosen);
                }
                else
                {
                    value = _values.TryGetValue(option.Query, out string current) ? current : DefaultOf(option);
                    changed |= !string.Equals(value ?? string.Empty, DefaultOf(option) ?? string.Empty, StringComparison.Ordinal);
                }

                query.Add(new KeyValuePair<string, string>(option.Query, value ?? string.Empty));
            }

            return query;
        }

        private void Submit()
        {
            if (_search == null)
            {
                return;
            }

            List<KeyValuePair<string, string>> query = BuildQuery(out bool changed);
            _modal.Close();
            _run?.Invoke(query, changed);
        }

        private void Reset()
        {
            ResetValues();
            _rows.Refresh(true);
        }

        private void Rebuild()
        {
            _rows.Dispose();
            _rows = new LibrarySettingRows();
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);
            }

            if (_search == null)
            {
                return;
            }

            var supported = new List<SonolusSearchOption>();
            foreach (SonolusSearchOption option in _search.Options)
            {
                if (option.IsSupported && !string.IsNullOrEmpty(option.Query))
                {
                    supported.Add(option);
                }
            }

            if (supported.Count == 0)
            {
                _content.sizeDelta = Vector2.zero;
                return;
            }

            LibrarySettingRows.Section section = _rows.AddSection(_content, "搜索条件", supported.Count, 0f, 12f, 0f);
            foreach (SonolusSearchOption option in supported)
            {
                AddRow(section, option);
            }

            _content.sizeDelta = new Vector2(0f, LibrarySettingRows.SectionHeight(supported.Count) + 24f);
            _rows.Refresh();
        }

        private void AddRow(LibrarySettingRows.Section section, SonolusSearchOption option)
        {
            string label = ServerWidgets.Localize(option.Name);
            if (!string.IsNullOrEmpty(option.Unit))
            {
                label += "（" + ServerWidgets.Localize(option.Unit) + "）";
            }

            string key = option.Query;
            switch (option.Type)
            {
                case "text":
                case "textArea":
                    _rows.AddText(section, label, ServerWidgets.Localize(option.Placeholder), option.Limit,
                        () => Get(key), value => _values[key] = value ?? string.Empty);
                    break;
                case "toggle":
                    _rows.AddSwitch(section, label, () => Get(key) == "1", on => _values[key] = on ? "1" : "0");
                    break;
                case "slider":
                    double min = option.Min;
                    double max = option.Max > option.Min ? option.Max : option.Min + 1d;
                    _rows.AddNumber(section, label,
                        () => double.TryParse(Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : min,
                        value => _values[key] = FormatNumber(value), min, max, option.Step);
                    break;
                case "select":
                {
                    var titles = new List<string>();
                    foreach (SonolusOptionValue value in option.Values)
                    {
                        titles.Add(ServerWidgets.Localize(value.Title));
                    }

                    _rows.AddDropdown(section, label, _screen.Overlay, titles,
                        () => Mathf.Max(0, IndexOf(option, Get(key))),
                        index => _values[key] = option.Values[Mathf.Clamp(index, 0, option.Values.Count - 1)].Name, 380f);
                    break;
                }
                case "multi":
                {
                    var titles = new List<string>();
                    foreach (SonolusOptionValue value in option.Values)
                    {
                        titles.Add(ServerWidgets.Localize(value.Title));
                    }

                    _rows.AddChips(section, label, titles, index => _flags[key][index], (index, on) => _flags[key][index] = on);
                    break;
                }
            }
        }

        private string Get(string key) => _values.TryGetValue(key, out string value) ? value : string.Empty;

        private static int IndexOf(SonolusSearchOption option, string name)
        {
            for (int i = 0; i < option.Values.Count; i++)
            {
                if (option.Values[i].Name == name)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string DefaultOf(SonolusSearchOption option)
        {
            switch (option.Type)
            {
                case "toggle":
                    return option.Default == "1" || string.Equals(option.Default, "true", StringComparison.OrdinalIgnoreCase) ? "1" : "0";
                case "slider":
                    return option.Default != null && double.TryParse(option.Default, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                        ? FormatNumber(number)
                        : FormatNumber(option.Min);
                case "select":
                    if (option.Default != null && IndexOf(option, option.Default) >= 0)
                    {
                        return option.Default;
                    }

                    // Older servers give the default as an index.
                    if (option.Default != null && int.TryParse(option.Default, out int index) && index >= 0 && index < option.Values.Count)
                    {
                        return option.Values[index].Name;
                    }

                    return option.Values.Count > 0 ? option.Values[0].Name : string.Empty;
                default:
                    return option.Default ?? string.Empty;
            }
        }

        private static string FormatNumber(double value) =>
            Math.Abs(value - Math.Round(value)) < 1e-9
                ? Math.Round(value).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
