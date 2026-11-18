using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using OpenSekai.App.Server;
using OpenSekai.Infrastructure.Sonolus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// 添加服务器: choose Sonolus or website, type or paste the address (a Sonolus share link or
    /// <c>sonolus://</c> link works too), optionally name it. A Sonolus server is asked for its
    /// card first, so a wrong address is caught here and the name defaults to the server's title.
    /// </summary>
    internal sealed class ServerAddPanel : IDisposable
    {
        private const float WIDTH = 980f;
        private const float HEIGHT = 700f;
        private const float FIELD_HEIGHT = 72f;
        private const float KIND_WIDTH = 260f;

        private readonly ServerScreen _screen;
        private readonly Action<ServerEntry> _added;
        private readonly LibraryModal _modal;
        private readonly Image _sonolusKind;
        private readonly Image _webKind;
        private readonly TextMeshProUGUI _sonolusLabel;
        private readonly TextMeshProUGUI _webLabel;
        private readonly TMP_InputField _address;
        private readonly TMP_InputField _name;
        private readonly TextMeshProUGUI _hint;
        private readonly TextMeshProUGUI _error;
        private readonly Button _confirm;
        private ServerKind _kind = ServerKind.Sonolus;
        private CancellationTokenSource _probe;

        internal ServerAddPanel(ServerScreen screen, Action<ServerEntry> added)
        {
            _screen = screen;
            _added = added;
            RectTransform root = LibraryUi.CreateRect("AddServer", screen.ModalLayer);
            LibraryUi.Stretch(root);
            _modal = new LibraryModal(root, new Vector2(WIDTH, HEIGHT), "添加服务器", Close, withSubtitle: true);
            _modal.Subtitle.text = "Sonolus 服务器在游戏内浏览；网页服务器在内置浏览器中打开";
            RectTransform body = _modal.Body;

            float top = 8f;
            Caption(body, "类型", top);
            top += 38f;
            Image track = LibraryWidgets.Panel("Kind", body, FIELD_HEIGHT * 0.5f, LibraryTheme.Colors.Control);
            LibraryWidgets.Outline(track, FIELD_HEIGHT * 0.5f);
            LibraryWidgets.Place(track.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -top),
                new Vector2(KIND_WIDTH * 2f + 8f, FIELD_HEIGHT));
            _sonolusKind = KindButton(track.transform, "Sonolus 服务器", 0, () => SetKind(ServerKind.Sonolus), out _sonolusLabel);
            _webKind = KindButton(track.transform, "网页", 1, () => SetKind(ServerKind.WebView), out _webLabel);
            top += FIELD_HEIGHT + 26f;

            Caption(body, "地址", top);
            top += 38f;
            _address = LibraryWidgets.TextField("Address", body, FIELD_HEIGHT, "请输入服务器地址", LibraryIcon.Globe);
            _address.characterLimit = 2048;
            _address.contentType = TMP_InputField.ContentType.Standard;
            LibraryWidgets.TopStrip((RectTransform)_address.transform, 0f, 0f, top, FIELD_HEIGHT);
            _address.onValueChanged.AddListener(_ => _error.text = string.Empty);
            top += FIELD_HEIGHT + 8f;
            _hint = LibraryWidgets.Label("Hint", body, string.Empty, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.TopStrip(_hint.rectTransform, 8f, 0f, top, 28f);
            top += 28f + 22f;

            Caption(body, "名称（可选）", top);
            top += 38f;
            _name = LibraryWidgets.TextField("Name", body, FIELD_HEIGHT, "留空则使用服务器名称");
            _name.characterLimit = 40;
            LibraryWidgets.TopStrip((RectTransform)_name.transform, 0f, 0f, top, FIELD_HEIGHT);

            _error = LibraryWidgets.Label("Error", body, string.Empty, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.Danger);
            LibraryWidgets.BottomStrip(_error.rectTransform, 0f, 0f, LibraryTheme.Layout.MODAL_BUTTON_HEIGHT + 14f, 30f);

            float button = LibraryTheme.Layout.MODAL_BUTTON_HEIGHT;
            Button cancel = LibraryWidgets.PillButton("Cancel", body, "取消", Close, 200f, button,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, sound: UiCue.Close);
            LibraryWidgets.Outline((Image)cancel.targetGraphic, button * 0.5f);
            LibraryWidgets.Place((RectTransform)cancel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-236f, 0f), new Vector2(200f, button));
            _confirm = LibraryWidgets.PillButton("Confirm", body, "添加", Confirm, 220f, button,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, LibraryTheme.FontSize.BODY, FontStyles.Bold, UiCue.None);
            LibraryWidgets.Place((RectTransform)_confirm.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(220f, button));

            SetKind(ServerKind.Sonolus, false);
            root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        internal void Open()
        {
            CancelProbe();
            _address.SetTextWithoutNotify(string.Empty);
            _name.SetTextWithoutNotify(string.Empty);
            _error.text = string.Empty;
            SetBusy(false);
            SetKind(ServerKind.Sonolus, false);
            _modal.Open();
        }

        internal void Close()
        {
            CancelProbe();
            SetBusy(false);
            _modal.Close();
        }

        public void Dispose()
        {
            CancelProbe();
            _modal.Dispose();
        }

        private void SetKind(ServerKind kind) => SetKind(kind, true);

        private void SetKind(ServerKind kind, bool sound)
        {
            if (sound && kind != _kind)
            {
                UiSound.Play(UiCue.Select);
            }

            _kind = kind;
            bool sonolus = kind == ServerKind.Sonolus;
            Paint(_sonolusKind, _sonolusLabel, sonolus);
            Paint(_webKind, _webLabel, !sonolus);
            ((TextMeshProUGUI)_address.placeholder).text = "请输入服务器地址";
            _hint.text = sonolus
                ? "可以输入服务器地址，或直接粘贴谱面分享链接、sonolus:// 链接"
                : "默认以 https:// 开头；在网页里下载的 ZIP / OSKP 曲包会自动导入曲库";
            _error.text = string.Empty;
        }

        private static void Paint(Image background, TextMeshProUGUI label, bool on)
        {
            background.color = on ? LibraryTheme.Colors.TextPrimary : new Color32(255, 255, 255, 0);
            label.color = on ? LibraryTheme.Colors.TextOnAccent : LibraryTheme.Colors.TextSecondary;
            LibraryUi.ApplyCjkTextStyle(label, on ? FontStyles.Bold : FontStyles.Normal);
        }

        private void Confirm()
        {
            if (_probe != null)
            {
                return;
            }

            string address = _address.text;
            string name = _name.text;
            if (_kind == ServerKind.WebView)
            {
                Add(ServerKind.WebView, name, address);
                return;
            }

            if (!SonolusServerAddress.TryParse(address, out SonolusServerAddress parsed, out _, out string error))
            {
                Fail(error);
                return;
            }

            _probe = new CancellationTokenSource();
            ProbeAsync(parsed, name, _probe.Token).Forget();
        }

        private async UniTaskVoid ProbeAsync(SonolusServerAddress address, string name, CancellationToken cancellationToken)
        {
            SetBusy(true);
            UiSound.Play(UiCue.Press);
            try
            {
                SonolusServerInfo info = await new SonolusClient(address).GetServerInfoAsync(cancellationToken);
                if (!info.HasLevels)
                {
                    throw new SonolusException("该服务器没有提供谱面。");
                }

                CancelProbe();
                SetBusy(false);
                Add(ServerKind.Sonolus, string.IsNullOrWhiteSpace(name) ? info.Title : name, address.BaseUrl);
            }
            catch (OperationCanceledException)
            {
                // Closed while connecting.
            }
            catch (SonolusException exception)
            {
                CancelProbe();
                SetBusy(false);
                Fail(exception.Message);
            }
        }

        private void Add(ServerKind kind, string name, string address)
        {
            ServerEntry entry = _screen.Registry.TryAdd(kind, name, address, out string error);
            if (entry == null)
            {
                Fail(error);
                return;
            }

            UiSound.Play(UiCue.Success);
            _modal.Close();
            _added?.Invoke(entry);
        }

        private void Fail(string message)
        {
            _error.text = message ?? "无法添加该服务器。";
            UiSound.Play(UiCue.Error);
        }

        private void SetBusy(bool busy)
        {
            _confirm.interactable = !busy;
            _confirm.GetComponentInChildren<TextMeshProUGUI>().text = busy ? "正在连接…" : "添加";
            ((Image)_confirm.targetGraphic).color = busy ? LibraryTheme.Colors.ControlActive : LibraryTheme.Colors.TextPrimary;
        }

        private void CancelProbe()
        {
            if (_probe == null)
            {
                return;
            }

            _probe.Cancel();
            _probe.Dispose();
            _probe = null;
        }

        private static void Caption(RectTransform body, string text, float top)
        {
            TextMeshProUGUI caption = LibraryWidgets.Label("Caption-" + text, body, text, LibraryTheme.FontSize.CAPTION,
                LibraryTheme.Colors.TextMuted, FontStyles.Bold);
            LibraryWidgets.TopStrip(caption.rectTransform, 8f, 0f, top, 30f);
        }

        private static Image KindButton(Transform track, string label, int index, Action onClick, out TextMeshProUGUI text)
        {
            float inner = FIELD_HEIGHT - 8f;
            Button button = LibraryWidgets.PillButton("Kind" + index, track, label, () => onClick(), KIND_WIDTH, inner,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, LibraryTheme.FontSize.BODY, sound: UiCue.None);
            LibraryWidgets.Place((RectTransform)button.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(4f + index * KIND_WIDTH, 0f), new Vector2(KIND_WIDTH, inner));
            text = button.GetComponentInChildren<TextMeshProUGUI>();
            return (Image)button.targetGraphic;
        }
    }
}
