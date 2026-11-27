using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
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
        private readonly GameObject _screen;
        private readonly RectTransform _root;
        private readonly RectTransform _body;
        private readonly RectTransform _songs;
        private readonly RectTransform _saved;
        private readonly Func<IReadOnlyList<SongPackageItem>> _library;
        private readonly PlayService _playback;
        private readonly Action _closed;
        private readonly DanLibraryPicker _picker;
        private readonly TMP_InputField _from, _to, _name, _sequence;
        private readonly TextMeshProUGUI _song, _message;
        private readonly Button _start;
        private readonly HashSet<string> _expanded = new();
        private SongPackageItem _selected;
        private PracticeClip _editing;
        private CancellationTokenSource _starting;

        internal bool IsOpen => _screen.activeSelf;

        internal PracticeSetupPanel(Transform parent, Camera homeCamera, PlayService playback, Func<IReadOnlyList<SongPackageItem>> library, Action closed)
        {
            _playback = playback;
            _closed = closed;
            _library = library;
            _screen = new GameObject("ClipsScreen");
            _screen.transform.SetParent(parent, false);
            var cameraObject = new GameObject("ClipsCamera", typeof(Camera));
            cameraObject.transform.SetParent(_screen.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.CopyFrom(homeCamera);
            camera.depth = 107f;
            _root = LibraryUi.CreateRect("PracticeSetup", _screen.transform);
            var canvas = _root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 10;
            var scaler = _root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _root.gameObject.AddComponent<GraphicRaycaster>();
            Image background = LibraryUi.CreateImage("Background", _root, LibraryTheme.Colors.Ground);
            LibraryUi.Stretch(background.rectTransform);
            background.raycastTarget = true;
            Image glow = LibraryWidgets.Shape("Glow", _root, LibraryShapes.Glow(), HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Periwinkle, 45));
            LibraryWidgets.Place(glow.rectTransform, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(-380f, -340f), new Vector2(1900f, 1900f));
            var title = LibraryWidgets.Label("Title", _root, "剪辑", LibraryTheme.FontSize.PAGE_TITLE, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(title.rectTransform, 64f, 230f, 36f, 72f);
            var back = PracticeWidgets.Button(_root, "Close", "返回", Close);
            LibraryWidgets.Place((RectTransform)back.transform, Vector2.one, Vector2.one, new Vector2(-64f,-36f), new Vector2(152f,72f));

            var page = LibraryUi.CreateRect("Columns", _root);
            LibraryWidgets.Inset(page, 64f, 48f, 64f, 132f);
            var left = Column(page, "Clips", 0f, 0.40f);
            var right = Column(page, "Editor", 0.42f, 1f);
            var heading = LibraryWidgets.Label("Heading", left, "已剪辑的歌曲", 26f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(heading.rectTransform, 32f, 32f, 24f, 52f);
            var songList = LibraryUi.CreateRect("Songs", left);
            LibraryWidgets.Inset(songList, 24f, 24f, 24f, 92f);
            _songs = PackWidgets.Scroll(songList, out _);
            _body = LibraryUi.CreateRect("Body", right);
            LibraryWidgets.Inset(_body, 24f, 24f, 24f, 24f);
            var chart = PracticeWidgets.Button(_body, "Song", "选择谱面", () => _picker.Show(_library));
            PracticeWidgets.Place(chart.transform, 0f, 0.89f, 0.66f, 1f);
            var create = PracticeWidgets.Button(_body, "NewClip", "新建剪辑", () =>
            {
                _editing = null;
                _from.SetTextWithoutNotify("");
                _to.SetTextWithoutNotify("");
                SetMessage("设置起止时间后保存为新剪辑");
            });
            PracticeWidgets.Place(create.transform, 0.66f, 0.89f, 1f, 1f);
            _song = chart.GetComponentInChildren<TextMeshProUGUI>();
            _song.richText = false;
            _from = Field("From", "开始秒数（默认 0）", 0f, 0.78f, 0.5f);
            _to = Field("To", "结束秒数（默认谱尾）", 0.5f, 0.78f, 1f);
            _name = Field("TemplateName", "模板名称", 0f, 0.67f, 0.62f);
            var save = PracticeWidgets.Button(_body, "Save", "保存模板", SaveTemplate);
            PracticeWidgets.Place(save.transform, 0.62f, 0.67f, 1f, 0.77f);
            var sequenceLabel = LibraryWidgets.Label("SequenceLabel", _body, "播放序列 · 倍速 x / 休息 s", 20f, LibraryTheme.Colors.TextSecondary);
            PracticeWidgets.Place(sequenceLabel.transform, 0f, 0.56f, 0.44f, 0.66f);
            _sequence = Field("Sequence", "0.5x, 2s, 1x", 0.44f, 0.56f, 1f);
            _sequence.SetTextWithoutNotify("1x");
            var caption = LibraryWidgets.Label("Templates", _body, "已存模板 · 点击应用", 20f, LibraryTheme.Colors.TextSecondary);
            PracticeWidgets.Place(caption.transform, 0f, 0.48f, 1f, 0.56f);
            var templates = LibraryUi.CreateRect("TemplateList", _body);
            PracticeWidgets.Place(templates, 0f, 0.30f, 1f, 0.48f);
            _saved = PackWidgets.Scroll(templates, out _);
            AddFollow("SpeedFollows", "下落速度跟随倍速", 0f, true);
            AddFollow("JudgmentFollows", "判定窗口跟随倍速", 0.5f, false);
            _message = LibraryWidgets.Label("Status", _body, "", 18f, LibraryTheme.Colors.TextSecondary);
            _message.richText = false;
            PracticeWidgets.Place(_message.transform, 0f, 0.11f, 1f, 0.19f);
            var saveClip = PracticeWidgets.Button(_body, "SaveClip", "保存剪辑", SaveClip);
            PracticeWidgets.Place(saveClip.transform, 0f, 0f, 0.48f, 0.11f);
            _start = LibraryWidgets.PillButton("Start", _body, "开始练习", () => StartAsync().Forget(),
                200f, 72f, LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, 24f);
            PracticeWidgets.Place(_start.transform, 0.5f, 0f, 1f, 0.11f);
            _picker = new DanLibraryPicker(_root, Pick, item => item.HasManifest && item.HasAudio && item.HasScore, "选择练习谱面");
            _screen.SetActive(false);
        }

        private readonly List<Action> _refreshSwitches = new();

        private void AddFollow(string id, string title, float left, bool speed)
        {
            var button = PracticeWidgets.Button(_body, id, title, () =>
            {
                if (speed) LibraryModes.SpeedFollows = !LibraryModes.SpeedFollows;
                else LibraryModes.JudgmentFollows = !LibraryModes.JudgmentFollows;
                foreach (var refresh in _refreshSwitches) refresh();
            });
            PracticeWidgets.Place(button.transform, left, 0.19f, left + 0.5f, 0.30f);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            _refreshSwitches.Add(() => label.text = title + ((speed ? LibraryModes.SpeedFollows : LibraryModes.JudgmentFollows) ? " · 开" : " · 关"));
        }

        private static RectTransform Column(RectTransform page, string id, float left, float right)
        {
            var panel = LibraryWidgets.Panel(id, page, LibraryTheme.Radius.PANEL, LibraryTheme.Colors.Surface);
            PracticeWidgets.Place(panel.transform, left, 0f, right, 1f);
            return panel.rectTransform;
        }

        private TMP_InputField Field(string id, string placeholder, float left, float bottom, float right)
        {
            var field = LibraryWidgets.TextField(id, _body, 64f, placeholder);
            field.textComponent.richText = false;
            PracticeWidgets.Place(field.transform, left, bottom, right, bottom + 0.10f);
            return field;
        }

        private void Pick(SongPackageItem item)
        {
            _editing = null;
            _selected = item;
            _song.text = LibraryModel.BuildTitle(item);
            _message.text = string.Empty;
            _picker.Close();
        }

        internal void Open()
        {
            _screen.SetActive(true);
            _message.text = string.Empty;
            foreach (var refresh in _refreshSwitches) refresh();
            RefreshLists();
        }

        private static string ChartKey(SongPackageItem song) =>
            string.IsNullOrEmpty(song.ScoreKey) ? Path.GetFileName(song.Entry.RootDirectory) : song.ScoreKey;

        private static void Clear(RectTransform content)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i);
                child.SetParent(null, false);
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
            content.sizeDelta = Vector2.zero;
        }

        private static void Row(RectTransform content, string title, Action select, float indent = 0f)
        {
            var button = PracticeWidgets.Button(content, "Row", title, () => select?.Invoke());
            button.GetComponentInChildren<TextMeshProUGUI>().richText = false;
            button.interactable = select != null;
            float top = content.sizeDelta.y;
            LibraryWidgets.TopStrip((RectTransform)button.transform, indent, 0f, top, 52f);
            content.sizeDelta = new Vector2(0f, top + 60f);
        }

        private void RefreshLists()
        {
            Clear(_songs);
            Clear(_saved);
            try
            {
                var clips = PracticeClipStore.Shared.Load();
                if (clips.Count == 0) Row(_songs, "尚未保存剪辑", null);
                foreach (var group in clips.GroupBy(c => c.Song))
                {
                    bool open = _expanded.Contains(group.Key);
                    Row(_songs, (open ? "▾  " : "▸  ") + group.Key + "  ·  " + group.Count(), () =>
                    {
                        if (!_expanded.Add(group.Key)) _expanded.Remove(group.Key);
                        RefreshLists();
                    });
                    if (open) foreach (var clip in group)
                        Row(_songs, $"{clip.From:g} – {clip.To?.ToString("g") ?? "谱尾"} 秒  ·  {clip.Template.Name}",
                            () => SelectClip(clip), 24f);
                }
                foreach (var template in PracticeTemplateStore.Shared.Load())
                    Row(_saved, template.Name, () => Apply(template));
            }
            catch (Exception e) when (StorageError(e)) { SetMessage(e.Message); }
        }

        private static bool StorageError(Exception e) => e is IOException or UnauthorizedAccessException or InvalidDataException
            or JsonException or FormatException or ArgumentException;

        private void Apply(PracticeTemplate template)
        {
            _name.SetTextWithoutNotify(template.Name);
            _sequence.SetTextWithoutNotify(PracticeForm.FormatSequence(template));
            SetMessage("已应用「" + template.Name + "」");
        }

        private void SelectClip(PracticeClip clip)
        {
            var song = _library().FirstOrDefault(item => item.Entry != null && ChartKey(item) == clip.ChartKey);
            _editing = clip;
            _selected = song;
            _song.text = song == null ? clip.Song : LibraryModel.BuildTitle(song);
            _from.SetTextWithoutNotify(clip.From.ToString("R", CultureInfo.InvariantCulture));
            _to.SetTextWithoutNotify(clip.To?.ToString("R", CultureInfo.InvariantCulture) ?? "");
            Apply(clip.Template);
            if (song == null) SetMessage("曲库中找不到此谱面，请重新选择。");
        }

        private void SaveTemplate()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_name.text)) throw new FormatException("请先输入模板名称。");
                PracticeTemplateStore.Shared.Save(PracticeForm.ReadSequence(_name.text.Trim(), _sequence.text));
                RefreshLists();
                SetMessage("已保存模板");
            }
            catch (Exception e) when (StorageError(e)) { SetMessage(e.Message); }
        }

        private void SaveClip()
        {
            if (_selected == null) { SetMessage("请先选择谱面。"); return; }
            try
            {
                var (from, to) = PracticeForm.ReadRange(_from.text, _to.text);
                var clip = new PracticeClip
                {
                    Id = _editing?.Id ?? Guid.NewGuid().ToString("N"), Song = LibraryModel.BuildTitle(_selected),
                    ChartKey = ChartKey(_selected), From = from, To = to,
                    Template = PracticeForm.ReadSequence(string.IsNullOrWhiteSpace(_name.text) ? "当前剪辑" : _name.text.Trim(), _sequence.text)
                };
                PracticeClipStore.Shared.Save(clip);
                _editing = clip;
                _expanded.Add(clip.Song);
                RefreshLists();
                SetMessage("已保存剪辑");
            }
            catch (Exception e) when (StorageError(e)) { SetMessage(e.Message); }
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
                var plan = LibraryModes.Plan(start, end, PracticeForm.ReadSequence(name, _sequence.text));
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

        internal void Close()
        {
            _starting?.Cancel();
            _picker.Close();
            _screen.SetActive(false);
            _closed();
        }

        public void Dispose()
        {
            _starting?.Cancel();
            _picker.Dispose();
            if (_screen != null) UnityEngine.Object.Destroy(_screen);
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
