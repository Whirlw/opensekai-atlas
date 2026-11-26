using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenSekai.App.Playback;
using OpenSekai.Infrastructure.Packages;
using OpenSekai.Shared.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    internal sealed class PracticeSetupPanel : IDisposable
    {
        private readonly LibraryModal _modal;
        private readonly PlayService _playback;
        private readonly DanLibraryPicker _picker;
        private readonly TMP_InputField _from;
        private readonly TMP_InputField _to;
        private readonly TMP_InputField _name;
        private readonly TMP_InputField _sequence;
        private readonly TextMeshProUGUI _song;
        private readonly TextMeshProUGUI _saved;
        private readonly TextMeshProUGUI _message;
        private readonly Button _start;
        private SongPackageItem _selected;
        private IReadOnlyList<PracticeTemplate> _templates = Array.Empty<PracticeTemplate>();
        private int _templateIndex = -1;
        private CancellationTokenSource _starting;

        internal bool IsOpen => _modal.IsOpen;

        internal PracticeSetupPanel(RectTransform parent, PlayService playback)
        {
            _playback = playback;
            var root = LibraryUi.CreateRect("PracticeSetup", parent);
            LibraryUi.Stretch(root);
            _modal = new LibraryModal(root, new Vector2(1120f, 800f), "练习模式", Close);
            RectTransform body = _modal.Body;
            var chart = PracticeWidgets.Button(body, "Song", "选择谱面", () => _picker.Show());
            PracticeWidgets.Place(chart.transform, 0f, 0.84f, 1f, 0.99f);
            _song = chart.GetComponentInChildren<TextMeshProUGUI>();
            _song.richText = false;
            _from = Field("From", "开始秒数（留空为 0）", 0f, 0.70f, 0.5f);
            _to = Field("To", "结束秒数（留空到谱尾）", 0.5f, 0.70f, 1f);

            var hint = LibraryWidgets.Label("Instructions", body, "序列用逗号分隔。x 表示播放倍速，s 表示休息秒数。", 22f, LibraryTheme.Colors.TextSecondary);
            PracticeWidgets.Place(hint.transform, 0f, 0.60f, 1f, 0.70f);
            _sequence = Field("Sequence", "0.5x, 2s, 0.7x, 2s", 0f, 0.47f, 1f);
            _sequence.SetTextWithoutNotify("1x");
            _name = Field("TemplateName", "模板名称", 0f, 0.32f, 0.72f);
            var save = PracticeWidgets.Button(body, "Save", "保存模板", SaveTemplate);
            PracticeWidgets.Place(save.transform, 0.72f, 0.32f, 1f, 0.45f);
            var previous = PracticeWidgets.Button(body, "Previous", "上一模板", () => SelectTemplate(-1));
            PracticeWidgets.Place(previous.transform, 0f, 0.17f, 0.23f, 0.30f);
            var next = PracticeWidgets.Button(body, "Next", "下一模板", () => SelectTemplate(1));
            PracticeWidgets.Place(next.transform, 0.77f, 0.17f, 1f, 0.30f);
            _saved = LibraryWidgets.Label("Selection", body, "", 22f, LibraryTheme.Colors.TextSecondary,
                alignment: TextAlignmentOptions.Center);
            _saved.richText = false;
            PracticeWidgets.Place(_saved.transform, 0.23f, 0.17f, 0.77f, 0.30f);

            _message = LibraryWidgets.Label("Status", body, "", 21f, LibraryTheme.Colors.TextPrimary);
            _message.richText = false;
            PracticeWidgets.Place(_message.transform, 0f, 0f, 0.72f, 0.15f);
            _start = PracticeWidgets.Button(body, "Start", "开始练习", () => StartAsync().Forget());
            PracticeWidgets.Place(_start.transform, 0.72f, 0f, 1f, 0.15f);
            _picker = new DanLibraryPicker(root, Pick, item => item.HasManifest && item.HasAudio && item.HasScore, "选择练习谱面");
            _modal.Close();
        }

        private TMP_InputField Field(string id, string placeholder, float left, float bottom, float right)
        {
            var field = LibraryWidgets.TextField(id, _modal.Body, 64f, placeholder);
            field.textComponent.richText = false;
            PracticeWidgets.Place(field.transform, left, bottom, right, bottom + 0.13f);
            return field;
        }

        private void Pick(SongPackageItem item)
        {
            _selected = item;
            _song.text = LibraryModel.BuildTitle(item);
            _message.text = string.Empty;
            _picker.Close();
        }

        internal void Open()
        {
            _modal.Open();
            _message.text = string.Empty;
            RefreshTemplates();
        }

        private void RefreshTemplates()
        {
            try
            {
                _templates = PracticeTemplateStore.Shared.Load();
                _templateIndex = -1;
                _saved.text = $"已保存 {_templates.Count} 个模板";
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
            {
                _templates = Array.Empty<PracticeTemplate>();
                _saved.text = "模板读取失败";
                _message.text = e.Message;
            }
        }

        private void SelectTemplate(int direction)
        {
            if (_templates.Count == 0) return;
            if (_templateIndex < 0) _templateIndex = direction > 0 ? 0 : _templates.Count - 1;
            else _templateIndex = (_templateIndex + direction + _templates.Count) % _templates.Count;
            var template = _templates[_templateIndex];
            _name.SetTextWithoutNotify(template.Name);
            _sequence.SetTextWithoutNotify(PracticeForm.FormatSequence(template));
            _saved.text = $"{_templateIndex + 1} / {_templates.Count}  {template.Name}";
            _message.text = string.Empty;
        }

        private void SaveTemplate()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_name.text)) throw new FormatException("请先输入模板名称。");
                var template = PracticeForm.ReadSequence(_name.text.Trim(), _sequence.text);
                PracticeTemplateStore.Shared.Save(template);
                RefreshTemplates();
                _message.text = "已保存「" + template.Name + "」";
            }
            catch (Exception e) when (e is FormatException or ArgumentException or IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
            {
                _message.text = e.Message;
            }
        }

        private async UniTaskVoid StartAsync()
        {
            if (_starting != null) return;
            if (_selected == null)
            {
                _message.text = "请先选择谱面。";
                return;
            }
            var cancel = new CancellationTokenSource();
            _starting = cancel;
            _start.interactable = false;
            try
            {
                var (start, end) = PracticeForm.ReadRange(_from.text, _to.text);
                string name = string.IsNullOrWhiteSpace(_name.text) ? "当前练习" : _name.text.Trim();
                var plan = new PracticePlan(start, end, PracticeForm.ReadSequence(name, _sequence.text), PracticeLiveControls.Create);
                var request = new PlayRequest(_selected.Entry.RootDirectory, false, Sekai.SceneManager.Scene.Library) { Practice = plan };
                var result = await _playback.StartAsync(request, cancel.Token, SetMessage);
                if (!result.Started) SetMessage(result.Message);
            }
            catch (Exception e) when (e is FormatException or ArgumentException)
            {
                SetMessage(e.Message);
            }
            finally
            {
                _starting = null;
                cancel.Dispose();
                if (_start != null) _start.interactable = true;
            }
        }

        private void SetMessage(string message)
        {
            if (_message != null) _message.text = message;
        }

        internal bool TryHandleBack()
        {
            if (!IsOpen) return false;
            if (_picker.IsOpen) _picker.Close();
            else Close();
            return true;
        }

        private void Close()
        {
            _starting?.Cancel();
            _picker.Close();
            _modal.Close();
        }

        public void Dispose()
        {
            _starting?.Cancel();
            _picker.Dispose();
            _modal.Dispose();
        }
    }

    internal static class PracticeWidgets
    {
        internal static void Place(Transform control, float left, float bottom, float right, float top)
        {
            var rect = (RectTransform)control;
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = new Vector2(4f, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);
        }

        internal static Button Button(Transform parent, string id, string text, UnityEngine.Events.UnityAction action) =>
            LibraryWidgets.PillButton(id, parent, text, action, 160f, 54f, LibraryTheme.Colors.Control,
                LibraryTheme.Colors.TextPrimary, 22f);
    }
}
