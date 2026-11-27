using System;
using System.Collections.Generic;
using System.IO;
using LitMotion;
using OpenSekai.App.Library;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The 商店 part: the coin balance and what it has been spent on on the left, the goods on sale
    /// on the right, by kind (头像, 主题, 游戏背景). The balance and the purchases come from
    /// <see cref="PlayerProfileStore.ReadCoins"/> and <see cref="PlayerProfileStore.ReadPurchases"/>
    /// on the main thread each time the part opens; an item is owned once a purchase records it.
    /// Tapping an item opens its exchange card. Like the other parts it builds its own camera and
    /// canvas in the Library scene.
    /// </summary>
    public sealed class ShopScreen : MonoBehaviour
    {
        private readonly List<CoinPurchase> _purchases = new List<CoinPurchase>();
        private readonly Dictionary<string, CoinPurchase> _owned = new Dictionary<string, CoinPurchase>(StringComparer.Ordinal);
        private PlayerProfileStore _store;
        private ShopCatalog _catalog;
        private Camera _camera;
        private GameObject _canvasRoot;
        private GraphicRaycaster _raycaster;
        private Image _curtain;
        private MotionHandle _curtainMotion;
        private LibrarySettingsPanel _settings;
        private ShopPurchasePanel _purchase;
        private UiToast _toast;
        private bool _presented;

        /// <summary>The back button (and back with nothing open): leave the part.</summary>
        internal event Action BackRequested;

        internal bool IsPresented => _presented;

        /// <summary>The store the page reads and spends from; the local profile unless a tool swapped in another.</summary>
        internal PlayerProfileStore Store
        {
            get => _store ?? PlayerProfileStore.Local;
            set => _store = value;
        }

        /// <summary>What is on sale; the game's own list unless a tool swapped in another.</summary>
        internal ShopCatalog Catalog
        {
            get => _catalog ?? ShopCatalog.Listed;
            set => _catalog = value;
        }

        internal ShopView View { get; private set; }

        internal RectTransform ModalLayer { get; private set; }

        internal ShopPurchasePanel PurchasePanel => _purchase;

        /// <summary>Coins to spend; below zero when deleted plays took back coins already spent. 0 when they could not be read.</summary>
        internal long Balance { get; private set; }

        /// <summary>Every purchase on record, latest first.</summary>
        internal IReadOnlyList<CoinPurchase> Purchases => _purchases;

        /// <summary>Coins spent in all, over every purchase on record.</summary>
        internal long Spent { get; private set; }

        internal ShopCategory Filter { get; private set; }

        /// <summary>Why the coins could not be read; null when they were.</summary>
        internal string LoadError { get; private set; }

        internal static ShopScreen Create()
        {
            var host = new GameObject("ShopScreen");
            ShopScreen screen = host.AddComponent<ShopScreen>();
            screen.Build();
            return screen;
        }

        // ---------------------------------------------------------------- presentation

        internal void SetPresented(bool presented, bool animate)
        {
            _presented = presented;
            _curtainMotion.TryCancel();
            _curtainMotion = default;
            _curtain.gameObject.SetActive(false);
            _camera.enabled = presented;
            _raycaster.enabled = presented;
            if (!presented)
            {
                _settings?.Hide();
                _purchase.Close();
                _toast.Hide();
                View.Stop();
            }

            _canvasRoot.SetActive(presented);
            if (!presented) return;
            Reload();
            if (!animate)
            {
                View.SkipEnter();
                return;
            }

            _curtain.gameObject.SetActive(true);
            _curtain.transform.SetAsLastSibling();
            _curtainMotion = LMotion.Create(1f, 0f, LibraryTheme.Motion.SLOW)
                .WithEase(LibraryTheme.Motion.FADE_EASE)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(() => _curtain.gameObject.SetActive(false))
                .Bind(alpha => _curtain.color = WithAlpha(LibraryTheme.Colors.Ground, alpha));
            View.PlayEnter();
        }

        internal void PlayExit(Action onCovered)
        {
            if (!_presented)
            {
                onCovered?.Invoke();
                return;
            }

            _curtainMotion.TryCancel();
            _curtain.gameObject.SetActive(true);
            _curtain.transform.SetAsLastSibling();
            _curtain.raycastTarget = true;
            _curtainMotion = LMotion.Create(0f, 1f, LibraryTheme.Motion.STANDARD)
                .WithEase(LibraryTheme.Motion.EXIT_EASE)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(() =>
                {
                    _curtainMotion = default;
                    onCovered?.Invoke();
                })
                .Bind(alpha => _curtain.color = WithAlpha(LibraryTheme.Colors.Ground, alpha));
        }

        /// <summary>The Android back key: closes the exchange card or the settings when they are open. False with nothing open.</summary>
        internal bool TryHandleBack()
        {
            if (_curtainMotion.IsActive()) return true;
            if (_purchase.IsOpen)
            {
                _purchase.Close();
                UiSound.Play(UiCue.Close);
                return true;
            }

            if (_settings != null && _settings.IsOpen)
            {
                _settings.Back();
                return true;
            }

            return false;
        }

        internal void Back() => BackRequested?.Invoke();

        internal void OpenSettings()
        {
            if (_settings == null) _settings = LibrarySettingsPanel.Create(ModalLayer, null);
            _settings.transform.SetAsLastSibling();
            _settings.Show();
        }

        // ---------------------------------------------------------------- data

        internal void Reload()
        {
            try
            {
                ReadWallet();
                LoadError = null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Shop] Could not read the coins: " + exception);
                ClearWallet();
                LoadError = ShopCopy.LoadError(exception);
            }

            View.Bind();
        }

        /// <summary>Shows only the goods of <paramref name="category"/>; <see cref="ShopCategory.All"/> shows every one.</summary>
        internal void SetFilter(ShopCategory category)
        {
            if (category == Filter) return;
            Filter = category;
            UiSound.Play(UiCue.Select);
            View.BindGoods(true);
        }

        /// <summary>The goods the current filter lets through, in shelf order.</summary>
        internal IReadOnlyList<ShopItem> Filtered() => Catalog.In(Filter);

        internal bool IsOwned(ShopItem item) => item != null && _owned.ContainsKey(item.Id);

        /// <summary>The purchase that bought <paramref name="item"/>; null while it is not owned.</summary>
        internal CoinPurchase PurchaseOf(ShopItem item) => item != null && _owned.TryGetValue(item.Id, out CoinPurchase purchase) ? purchase : null;

        /// <summary>How many of the goods on sale are owned.</summary>
        internal int OwnedCount(IReadOnlyList<ShopItem> items)
        {
            int count = 0;
            foreach (ShopItem item in items)
            {
                if (IsOwned(item)) count++;
            }

            return count;
        }

        /// <summary>What a purchase bought, as the shelf names it; the record's own text for anything no longer on sale.</summary>
        internal string Describe(CoinPurchase purchase, out ShopItem item)
        {
            item = Catalog.Find(purchase.Item);
            return item != null ? item.Title : purchase.Item;
        }

        /// <summary>Opens the exchange card of <paramref name="item"/>.</summary>
        internal void OpenItem(ShopItem item)
        {
            if (item == null) return;
            _purchase.Open(item);
        }

        /// <summary>
        /// Spends the coins on <paramref name="item"/> and reads the wallet again. False when it could
        /// not be bought, which the page says; nothing has changed then. Buying what is already owned
        /// is true and spends nothing.
        /// </summary>
        internal bool Buy(ShopItem item)
        {
            if (item == null || LoadError != null) return false;
            if (IsOwned(item)) return true;
            if (Balance < item.Price)
            {
                UiSound.Play(UiCue.Blocked);
                _toast.Show(ShopCopy.Short(item.Price - Balance));
                return false;
            }

            try
            {
                // False: the same purchase is already on record (a retry after a crash); it is owned all the same.
                Store.SpendCoins(ShopCopy.TransactionId(item), item.Id, item.Price);
                ReadWallet();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                exception is InvalidOperationException || exception is InvalidDataException)
            {
                Debug.LogWarning("[Shop] Could not buy " + item.Id + ": " + exception.Message);
                bool insufficient = false;
                try
                {
                    ReadWallet();
                    insufficient = Balance < item.Price;
                }
                catch (Exception reread)
                {
                    Debug.LogWarning("[Shop] Could not read the coins again: " + reread.Message);
                }

                UiSound.Play(UiCue.Error);
                _toast.Show(ShopCopy.BuyError(exception, insufficient));
                View.Bind();
                return false;
            }

            return true;
        }

        /// <summary>
        /// The exchange card's 兑换: buys <paramref name="item"/>, closes the card and plays the
        /// purchase on the page. When it could not be bought the card stays open, redrawn as the
        /// wallet stands.
        /// </summary>
        internal void Exchange(ShopItem item)
        {
            if (item == null) return;
            if (IsOwned(item))
            {
                _purchase.Close();
                return;
            }

            long before = Balance;
            if (!Buy(item))
            {
                if (_purchase.IsOpen) _purchase.Bind(item);
                return;
            }

            _purchase.Close();
            UiSound.Play(UiCue.Success);
            View.Bought(item, before);
            Announce(item);
        }

        /// <summary>Says what was just bought.</summary>
        internal void Announce(ShopItem item) => _toast.Show(ShopCopy.Bought(item), 2.6f);

        private void ReadWallet()
        {
            long balance = Store.ReadCoins();
            IReadOnlyList<CoinPurchase> purchases = Store.ReadPurchases();
            ClearWallet();
            Balance = balance;
            _purchases.AddRange(purchases);
            foreach (CoinPurchase purchase in _purchases)
            {
                Spent += purchase.Amount;
                // Latest first: the earliest purchase of an item is the one that bought it.
                _owned[purchase.Item] = purchase;
            }
        }

        private void ClearWallet()
        {
            Balance = 0;
            Spent = 0;
            _purchases.Clear();
            _owned.Clear();
        }

        // ---------------------------------------------------------------- building

        private void Build()
        {
            _camera = CreateCamera();
            _camera.transform.SetParent(transform, false);
            _canvasRoot = new GameObject("ShopCanvas", typeof(RectTransform));
            _canvasRoot.transform.SetParent(transform, false);
            Canvas canvas = _canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = _canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _raycaster = _canvasRoot.AddComponent<GraphicRaycaster>();

            var root = (RectTransform)_canvasRoot.transform;
            Image background = LibraryUi.CreateImage("Background", root, LibraryTheme.Colors.Ground);
            LibraryUi.Stretch(background.rectTransform);
            // The coins' gold behind the wallet; the goods' colors, violet and rose, on the far side.
            AddGlow(root, "GlowGold", HomeTheme.Colors.Gold, 0.14f, new Vector2(0f, 1f), new Vector2(340f, -280f), 1600f);
            AddGlow(root, "GlowViolet", HomeTheme.Colors.Violet, 0.1f, new Vector2(1f, 0f), new Vector2(-300f, 140f), 1600f);
            AddGlow(root, "GlowRose", HomeTheme.Colors.Rose, 0.06f, new Vector2(0.7f, 1f), new Vector2(0f, 60f), 1100f);

            View = new ShopView(root, this);
            ModalLayer = LibraryUi.CreateRect("Modals", root);
            LibraryUi.Stretch(ModalLayer);
            _purchase = new ShopPurchasePanel(ModalLayer, this);
            _toast = new UiToast(root, 48f, HomeTheme.Colors.Gold);

            _curtain = LibraryUi.CreateImage("Curtain", root, LibraryTheme.Colors.Ground);
            LibraryUi.Stretch(_curtain.rectTransform);
            _curtain.raycastTarget = true;
            _curtain.gameObject.SetActive(false);

            _camera.enabled = false;
            _raycaster.enabled = false;
            _canvasRoot.SetActive(false);
        }

        private static void AddGlow(RectTransform root, string name, Color32 tint, float alpha, Vector2 anchor, Vector2 position, float size)
        {
            Image glow = LibraryUi.CreateImage(name, root, new Color(tint.r / 255f, tint.g / 255f, tint.b / 255f, alpha));
            glow.sprite = LibraryShapes.Glow();
            glow.raycastTarget = false;
            LibraryWidgets.Place(glow.rectTransform, anchor, new Vector2(0.5f, 0.5f), position, new Vector2(size, size));
        }

        private static Color WithAlpha(Color32 color, float alpha)
        {
            Color value = color;
            value.a = alpha;
            return value;
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("ShopScreenCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = LibraryTheme.Colors.Ground;
            camera.cullingMask = ~0;
            camera.depth = 107f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            return camera;
        }

        private void OnDestroy()
        {
            _curtainMotion.TryCancel();
            _purchase?.Dispose();
            _toast?.Dispose();
            View?.Dispose();
            // The camera is a child of this object and goes with it.
        }
    }
}
