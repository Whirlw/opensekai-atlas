using System.Collections.Generic;
using LitMotion;
using LitMotion.Extensions;
using OpenSekai.App.Server;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The first page of the server part: one card per saved server (a Sonolus server opens in
    /// the game's own pages, a website in the WebView), a button to add one, and a way to remove
    /// one. Cards use the home page's card frame, so the part looks like where it was opened from.
    /// </summary>
    internal sealed class ServerListPage : ServerPage
    {
        private const int COLUMNS = 3;
        private const float CARD_HEIGHT = 312f;
        private const float GAP = 28f;
        private const float ART_SIZE = 150f;

        private readonly RectTransform _content;
        private readonly ScrollRect _scroll;
        private readonly TextMeshProUGUI _count;
        private readonly RectTransform _empty;
        private readonly List<Card> _cards = new List<Card>();
        private readonly ServerAddPanel _addPanel;
        private readonly ServerActionsPanel _actions;
        private MotionHandle _enter;

        internal ServerListPage(ServerScreen screen) : base(screen, "ServerList")
        {
            RectTransform bar = ServerWidgets.TopBar(Root, "服务器", screen.Back, out _, out _count);
            float height = LibraryTheme.Layout.CONTROL_HEIGHT;

            const float addWidth = 220f;
            Button add = ServerWidgets.LabelledButton(bar, "Add", "添加服务器", LibraryIcon.Add, OpenAdd, addWidth, height,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.None);
            LibraryWidgets.Place((RectTransform)add.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(addWidth, height));

            float margin = LibraryTheme.Spacing.PAGE_MARGIN;
            RectTransform body = LibraryUi.CreateRect("Body", Root);
            LibraryWidgets.Inset(body, margin - 24f, 0f, margin - 24f, LibraryTheme.Layout.TOP_BAR_HEIGHT + 8f);
            _content = ServerWidgets.VerticalList(body, out _scroll, 0f, stack: false);

            TextMeshProUGUI hint = LibraryWidgets.Label("Hint", Root,
                "Sonolus 服务器在游戏内浏览、搜索和下载；网页服务器在内置浏览器中打开，下载的曲包自动导入曲库。",
                LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted, FontStyles.Normal, TextAlignmentOptions.Center);
            LibraryWidgets.BottomStrip(hint.rectTransform, margin, margin, 18f, 28f);

            _empty = BuildEmptyState(body);
            _addPanel = new ServerAddPanel(screen, OnAdded);
            _actions = new ServerActionsPanel(screen);
            screen.Registry.Changed += Rebuild;
            Rebuild();
        }

        internal override void OnShown()
        {
            Rebuild();
        }

        internal override void PlayEnter()
        {
            _enter.TryCancel();
            var targets = new List<Card>(_cards);
            _enter = LMotion.Create(0f, 1f, 0.6f)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(() => targets.ForEach(card => card.SetEnter(1f)))
                .WithOnCancel(() => targets.ForEach(card => card.SetEnter(1f)))
                .Bind(time =>
                {
                    for (int i = 0; i < targets.Count; i++)
                    {
                        targets[i].SetEnter(HomeMotion.Span(time, 0.05f + i * 0.05f, 0.4f, Ease.OutCubic));
                    }
                });
        }

        internal override bool TryHandleBack()
        {
            if (_addPanel.IsOpen)
            {
                _addPanel.Close();
                UiSound.Play(UiCue.Close);
                return true;
            }

            if (_actions.IsOpen)
            {
                _actions.Close();
                UiSound.Play(UiCue.Close);
                return true;
            }

            return false;
        }

        protected override void OnDispose()
        {
            _enter.TryCancel();
            Screen.Registry.Changed -= Rebuild;
            _addPanel.Dispose();
            _actions.Dispose();
        }

        private void OpenAdd() => _addPanel.Open();

        /// <summary>Opens the 添加服务器 card, for screenshots.</summary>
        internal void OpenAddPanel() => OpenAdd();

        internal void CloseAddPanel() => _addPanel.Close();

        private void OnAdded(ServerEntry entry)
        {
            Rebuild();
            Screen.ShowToast("已添加 " + entry.Name);
        }

        /// <summary>
        /// What the page shows before the first server is added: the globe, a line on what a
        /// server is for, and the add button again where the eye already is.
        /// </summary>
        private RectTransform BuildEmptyState(RectTransform body)
        {
            RectTransform empty = LibraryUi.CreateRect("Empty", body);
            LibraryUi.Stretch(empty);
            Vector2 center = new Vector2(0.5f, 0.5f);
            Image globe = LibraryWidgets.Icon(empty, LibraryIcon.Globe, HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Periwinkle, 150), 120f);
            LibraryWidgets.Place(globe.rectTransform, center, center, new Vector2(0f, 150f), new Vector2(120f, 120f));
            TextMeshProUGUI title = LibraryWidgets.Label("Title", empty, "还没有服务器", 38f, LibraryTheme.Colors.TextPrimary,
                FontStyles.Bold, TextAlignmentOptions.Center);
            LibraryWidgets.Place(title.rectTransform, center, center, new Vector2(0f, 46f), new Vector2(1200f, 56f));
            TextMeshProUGUI hint = LibraryWidgets.Label("Hint", empty,
                "添加一个 Sonolus 服务器在游戏内浏览和下载谱面，或添加一个谱面网站在内置浏览器中打开。",
                LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextMuted, FontStyles.Normal, TextAlignmentOptions.Center);
            LibraryWidgets.Place(hint.rectTransform, center, center, new Vector2(0f, -8f), new Vector2(1300f, 34f));
            const float width = 260f;
            float height = LibraryTheme.Layout.PRIMARY_BUTTON_HEIGHT;
            Button add = ServerWidgets.LabelledButton(empty, "Add", "添加服务器", LibraryIcon.Add, OpenAdd, width, height,
                LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.None, 24f);
            LibraryWidgets.Place((RectTransform)add.transform, center, center, new Vector2(0f, -100f), new Vector2(width, height));
            empty.gameObject.SetActive(false);
            return empty;
        }

        private void Rebuild()
        {
            IReadOnlyList<ServerEntry> servers = Screen.Registry.Servers;
            _count.text = servers.Count > 0 ? servers.Count + " 个服务器" : string.Empty;
            _empty.gameObject.SetActive(servers.Count == 0);
            foreach (Card card in _cards)
            {
                Object.Destroy(card.Root.gameObject);
            }

            _cards.Clear();
            // Columns are fractions of the width, so the grid follows the screen without a relayout.
            const float pad = 24f;
            for (int i = 0; i < servers.Count; i++)
            {
                var card = new Card(_content, servers[i], Screen.OpenServer, _actions.Open);
                int row = i / COLUMNS;
                int column = i % COLUMNS;
                RectTransform rect = card.Root;
                float top = -(pad + row * (CARD_HEIGHT + GAP));
                rect.anchorMin = new Vector2((float)column / COLUMNS, 1f);
                rect.anchorMax = new Vector2((float)(column + 1) / COLUMNS, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                float left = column == 0 ? pad : GAP * 0.5f;
                float right = column == COLUMNS - 1 ? pad : GAP * 0.5f;
                rect.offsetMin = new Vector2(left, top - CARD_HEIGHT);
                rect.offsetMax = new Vector2(-right, top);
                _cards.Add(card);
            }

            int rows = (servers.Count + COLUMNS - 1) / COLUMNS;
            _content.sizeDelta = new Vector2(0f, pad * 2f + rows * CARD_HEIGHT + Mathf.Max(0, rows - 1) * GAP);
        }

        /// <summary>One server, as a card in the home page's look.</summary>
        private sealed class Card
        {
            private readonly HomeCardFrame _frame;

            internal Card(RectTransform parent, ServerEntry entry, System.Action<ServerEntry> open, System.Action<ServerEntry> more)
            {
                bool sonolus = entry.Kind == ServerKind.Sonolus;
                Color32 tint = sonolus ? HomeTheme.Colors.Periwinkle : HomeTheme.Colors.Rose;
                Root = LibraryUi.CreateRect("Server-" + entry.Id, parent);
                _frame = new HomeCardFrame(Root, "Card", HomeTheme.Layout.CARD_RADIUS, () => open(entry), 0.975f);
                LibraryUi.Stretch(_frame.Root);
                RectTransform content = _frame.Content;

                Image glow = LibraryUi.CreateImage("Glow", content, HomeTheme.Colors.WithAlpha(tint, 36));
                glow.sprite = LibraryShapes.Glow();
                glow.raycastTarget = false;
                LibraryWidgets.Place(glow.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-90f, -70f), new Vector2(520f, 520f));
                Image art = LibraryWidgets.Icon(content, sonolus ? LibraryIcon.Globe : LibraryIcon.Browser, HomeTheme.Colors.WithAlpha(tint, 70), ART_SIZE);
                LibraryWidgets.Place(art.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -70f), new Vector2(ART_SIZE, ART_SIZE));

                float pad = 36f;
                TextMeshProUGUI eyebrow = LibraryWidgets.Label("Eyebrow", content, sonolus ? "SONOLUS" : "WEB", 20f, tint);
                LibraryUi.UseCondensedFont(eyebrow);
                eyebrow.characterSpacing = 26f;
                LibraryWidgets.TopStrip(eyebrow.rectTransform, pad, pad + 120f, pad - 4f, 28f);

                TextMeshProUGUI title = LibraryWidgets.Label("Title", content, entry.Name, 40f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
                LibraryWidgets.TopStrip(title.rectTransform, pad, pad + 100f, pad + 34f, 54f);
                TextMeshProUGUI address = LibraryWidgets.Label("Address", content, entry.DisplayAddress, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextSecondary);
                LibraryWidgets.TopStrip(address.rectTransform, pad, pad + 100f, pad + 94f, 30f);

                string what = sonolus ? "游戏内浏览 · 高级搜索 · 下载" : "内置网页 · 下载后自动导入";
                RectTransform chip = ServerWidgets.Chip(content, what, sonolus ? LibraryIcon.Search : LibraryIcon.Download,
                    HomeTheme.Colors.Chip, LibraryTheme.Colors.TextSecondary, out float chipWidth, 44f);
                LibraryWidgets.Place(chip, Vector2.zero, Vector2.zero, new Vector2(pad, pad - 6f), new Vector2(chipWidth, 44f));

                Button moreButton = ServerWidgets.IconButton(content, "More", LibraryIcon.More, () => more(entry), UiCue.None, 60f);
                LibraryWidgets.Place((RectTransform)moreButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-pad + 8f, pad - 14f), new Vector2(60f, 60f));
            }

            internal RectTransform Root { get; }

            internal void SetEnter(float progress)
            {
                _frame.Group.alpha = progress;
                _frame.Root.anchoredPosition = new Vector2(0f, -HomeTheme.Motion.ENTER_RISE * 0.6f * (1f - progress));
            }
        }
    }

    /// <summary>The card a server's ⋯ button opens: open it, or remove it from the list.</summary>
    internal sealed class ServerActionsPanel : System.IDisposable
    {
        private readonly ServerScreen _screen;
        private readonly LibraryModal _modal;
        private readonly Button _remove;
        private readonly TextMeshProUGUI _removeLabel;
        private ServerEntry _entry;
        private bool _confirming;

        internal ServerActionsPanel(ServerScreen screen)
        {
            _screen = screen;
            RectTransform root = LibraryUi.CreateRect("ServerActions", screen.ModalLayer);
            LibraryUi.Stretch(root);
            _modal = new LibraryModal(root, new Vector2(760f, 420f), string.Empty, Close, withSubtitle: true);
            float button = LibraryTheme.Layout.MODAL_BUTTON_HEIGHT;

            Button open = ServerWidgets.LabelledButton(_modal.Body, "Open", "打开", LibraryIcon.Forward, () =>
            {
                ServerEntry entry = _entry;
                Close();
                _screen.OpenServer(entry);
            }, 300f, button, LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.None);
            LibraryWidgets.TopStrip((RectTransform)open.transform, 0f, 0f, 30f, button);

            _remove = ServerWidgets.LabelledButton(_modal.Body, "Remove", "从列表中移除", LibraryIcon.Trash, OnRemove, 300f, button,
                LibraryTheme.Colors.DangerSurface, LibraryTheme.Colors.Danger, UiCue.None);
            LibraryWidgets.TopStrip((RectTransform)_remove.transform, 0f, 0f, 30f + button + 16f, button);
            _removeLabel = _remove.GetComponentInChildren<TextMeshProUGUI>();
            root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _modal.IsOpen;

        internal void Open(ServerEntry entry)
        {
            _entry = entry;
            _confirming = false;
            _modal.Title.text = entry.Name;
            _modal.Subtitle.text = (entry.Kind == ServerKind.Sonolus ? "Sonolus 服务器" : "网页服务器") + "  ·  " + entry.DisplayAddress;
            ServerWidgets.SetLabelledButton(_remove, "从列表中移除", LibraryIcon.Trash,
                LibraryTheme.Colors.DangerSurface, LibraryTheme.Colors.Danger);
            _modal.Open();
        }

        internal void Close() => _modal.Close();

        public void Dispose() => _modal.Dispose();

        private void OnRemove()
        {
            if (!_confirming)
            {
                _confirming = true;
                UiSound.Play(UiCue.Warning);
                ServerWidgets.SetLabelledButton(_remove, "再点一次确认", LibraryIcon.Trash, LibraryTheme.Colors.Danger, LibraryTheme.Colors.TextOnAccent);
                return;
            }

            ServerEntry entry = _entry;
            if (!_screen.Registry.TryRemove(entry, out string error))
            {
                UiSound.Play(UiCue.Error);
                _screen.ShowToast(error);
                return;
            }

            Close();
            UiSound.Play(UiCue.Delete);
            _screen.ShowToast("已移除 " + entry.Name);
        }
    }
}
