using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// 商店: a little shopfront in the coins' gold: a striped awning with a scalloped hem, a sign
    /// hung above it with the shop page's coin turning on it, lit windows (an avatar in one,
    /// a theme's swatches in the other) and a door left ajar. What it sells circles it on a tilted
    /// orbit: an avatar, a theme and a background; those behind the shop are smaller and dimmer than
    /// those passing in front. A tap makes the shop hop, spins the coin twice and sends the goods a
    /// third of the way round.
    /// </summary>
    internal sealed class HomeShopArt : IHomeCardArt
    {
        private const float STORE = 160f;
        private const float RISE = 10f;
        private const float FLOAT = 3f;
        private const float FLOAT_SECONDS = 4.8f;
        private const float ORBIT_X = 128f;
        private const float ORBIT_Y = 34f;
        private const float ORBIT_DROP = 18f;
        private const float TILT = -10f;
        private const float ORBIT_SECONDS = 18f;
        private const float HOP_SECONDS = 0.7f;
        private const float SIGN_COIN = 22f;
        private const int STRIPES = 6;
        private const float STRIPE = 24f;
        private const int DOTS = 40;

        private static readonly Color32 _cream = new Color32(255, 240, 214, 255);

        private static readonly Vector2[] _sparklePositions =
        {
            new Vector2(-108f, 96f), new Vector2(100f, 104f), new Vector2(128f, 22f), new Vector2(-130f, -6f), new Vector2(20f, 132f)
        };

        private readonly RectTransform _body;
        private readonly RectTransform _behind;
        private readonly RectTransform _store;
        private readonly RectTransform _ahead;
        private readonly ShopCoin _signCoin;
        private readonly Image[] _windows = new Image[2];
        private readonly Image _door;
        private readonly Good[] _goods = new Good[3];
        private readonly Image[] _sparkles = new Image[_sparklePositions.Length];
        private readonly Image[] _stars = new Image[_sparklePositions.Length];
        private float _extraTurn;
        private float _orbitOffset;
        private float _hop = 1f;
        private MotionHandle _poke;
        private MotionHandle _hopMotion;

        internal HomeShopArt(RectTransform root)
        {
            Vector2 center = new Vector2(0.5f, 0.5f);
            _body = LibraryUi.CreateRect("Body", root);
            LibraryWidgets.Place(_body, center, center, new Vector2(0f, RISE), new Vector2(STORE, STORE));

            // Back to front: the far half of the orbit and the goods on it, the shop, then the near half.
            _behind = Layer("Behind");
            _store = Layer("Store");
            // It hops from where it stands.
            _store.pivot = new Vector2(0.5f, 0f);
            _ahead = Layer("Ahead");
            for (int i = 0; i < DOTS; i++)
            {
                float angle = i * Mathf.PI * 2f / DOTS;
                bool near = Mathf.Sin(angle) < 0f;
                Vector2 position = Orbit(angle);
                // In front of the building the path stays faint, so the shopfront reads.
                byte alpha = (byte)(!near ? 50 : Mathf.Abs(position.x) < 66f ? 45 : 110);
                Image dot = LibraryWidgets.Panel("Dot" + i, near ? _ahead : _behind, 2f, HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Gold, alpha));
                LibraryWidgets.Place(dot.rectTransform, center, center, position, new Vector2(4f, 4f));
            }

            BuildStore(out _signCoin, out _door);

            _goods[0] = Good.Avatar(_behind);
            _goods[1] = Good.Picture(_behind, ShopCategory.Theme, new Vector2(62f, 62f), 16f);
            _goods[2] = Good.Picture(_behind, ShopCategory.Background, new Vector2(78f, 54f), 12f);

            for (int i = 0; i < _sparkles.Length; i++)
            {
                Image sparkle = LibraryUi.CreateImage("Sparkle" + i, root, Color.white);
                sparkle.sprite = LibraryShapes.Glow();
                sparkle.raycastTarget = false;
                float size = i % 2 == 0 ? 28f : 20f;
                LibraryWidgets.Place(sparkle.rectTransform, center, center, _sparklePositions[i], new Vector2(size, size));
                _sparkles[i] = sparkle;
                _stars[i] = LibraryWidgets.Icon(sparkle.transform, LibraryIcon.Star, Color.Lerp(HomeTheme.Colors.Gold, Color.white, 0.55f), size);
            }

            Tick(0d);
        }

        public void Tick(double time)
        {
            float bob = Mathf.Sin((float)(time % FLOAT_SECONDS / FLOAT_SECONDS) * Mathf.PI * 2f);
            _body.anchoredPosition = new Vector2(0f, RISE + FLOAT * bob);
            ShopCoin.Idle(time, out float turn, out float sweep);
            _signCoin.Apply(turn + _extraTurn, sweep);

            // The windows' light flickers a little, each in its own time.
            for (int i = 0; i < _windows.Length; i++)
            {
                float flicker = 0.5f + 0.5f * Mathf.Sin((float)((time + i * 1.3d) % 2.6d / 2.6d) * Mathf.PI * 2f);
                _windows[i].color = HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Gold, (byte)Mathf.RoundToInt(Mathf.Lerp(120f, 190f, flicker)));
            }

            float door = 0.5f + 0.5f * Mathf.Sin((float)(time % 3.4d / 3.4d) * Mathf.PI * 2f);
            _door.color = HomeTheme.Colors.WithAlpha(HomeTheme.Colors.Gold, (byte)Mathf.RoundToInt(Mathf.Lerp(70f, 120f, door)));

            // A hop: down, up past where it stands, and settling.
            float squash = Mathf.Sin(_hop * Mathf.PI * 2f) * (1f - _hop);
            _store.localScale = new Vector3(1f + 0.05f * squash, 1f - 0.07f * squash, 1f);

            float phase = (float)(time % ORBIT_SECONDS / ORBIT_SECONDS) + _orbitOffset;
            for (int i = 0; i < _goods.Length; i++)
            {
                float angle = (phase + i / (float)_goods.Length) * Mathf.PI * 2f;
                _goods[i].Apply(Orbit(angle), -Mathf.Sin(angle), _behind, _ahead);
            }

            for (int i = 0; i < _sparkles.Length; i++)
            {
                float p = (float)((time + i * 0.61d) % 2.6d / 2.6d);
                float shown = Mathf.Sin(p * Mathf.PI);
                Color color = _sparkles[i].color;
                color.a = shown * shown * 0.7f;
                _sparkles[i].color = color;
                Color star = _stars[i].color;
                star.a = shown * shown;
                _stars[i].color = star;
                float scale = 0.6f + 0.5f * shown;
                _sparkles[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        public void Poke()
        {
            _poke.TryCancel();
            _extraTurn = 0f;
            float from = _orbitOffset;
            _poke = LMotion.Create(0f, 1f, 1f)
                .WithEase(Ease.OutCubic)
                .WithScheduler(HomeMotion.Scheduler)
                .Bind(progress =>
                {
                    // Two turns end where they began, so nothing jumps when the spin is over.
                    _extraTurn = progress * 2f;
                    _orbitOffset = from + progress / 3f;
                });
            _hopMotion.TryCancel();
            _hopMotion = LMotion.Create(0f, 1f, HOP_SECONDS)
                .WithScheduler(HomeMotion.Scheduler)
                .Bind(this, static (progress, art) => art._hop = progress);
        }

        public void Dispose()
        {
            _poke.TryCancel();
            _hopMotion.TryCancel();
        }

        /// <summary>A point of the orbit: an ellipse around the shop's middle, tilted; the near half is below.</summary>
        private static Vector2 Orbit(float angle)
        {
            var point = new Vector2(ORBIT_X * Mathf.Cos(angle), ORBIT_Y * Mathf.Sin(angle));
            float tilt = TILT * Mathf.Deg2Rad;
            return new Vector2(point.x * Mathf.Cos(tilt) - point.y * Mathf.Sin(tilt),
                point.x * Mathf.Sin(tilt) + point.y * Mathf.Cos(tilt) - ORBIT_DROP);
        }

        private RectTransform Layer(string name)
        {
            RectTransform layer = LibraryUi.CreateRect(name, _body);
            LibraryUi.Stretch(layer);
            return layer;
        }

        /// <summary>The shop, drawn about the middle of the body: building, awning, sign, windows and door.</summary>
        private void BuildStore(out ShopCoin signCoin, out Image doorLight)
        {
            Color32 gold = HomeTheme.Colors.Gold;
            Color32 wall = Color.Lerp(HomeTheme.Colors.Card, gold, 0.2f);
            Color32 inside = Color.Lerp(LibraryTheme.Colors.Ground, gold, 0.12f);
            Vector2 center = new Vector2(0.5f, 0.5f);

            Image glow = LibraryUi.CreateImage("Glow", _store, HomeTheme.Colors.WithAlpha(gold, 60));
            glow.sprite = LibraryShapes.Glow();
            glow.raycastTarget = false;
            LibraryWidgets.Place(glow.rectTransform, center, center, new Vector2(0f, 4f), new Vector2(STORE * 2f, STORE * 2f));

            // The building, on its own shadow.
            RectTransform footprint = LibraryUi.CreateRect("Footprint", _store);
            LibraryWidgets.Place(footprint, center, center, new Vector2(0f, -32f), new Vector2(124f, 84f));
            LibraryWidgets.Shadow(footprint, 14f, 22f, 8f, 120);
            Image building = Piece("Building", new Vector2(0f, -32f), new Vector2(124f, 84f), 14f, wall);
            Image wallLine = LibraryWidgets.Shape("Outline", building.transform, LibraryShapes.Ring(14f, 1.5f), HomeTheme.Colors.WithAlpha(gold, 110));
            LibraryUi.Stretch(wallLine.rectTransform);
            Piece("Step", new Vector2(0f, -76f), new Vector2(144f, 7f), 3.5f, Color.Lerp(HomeTheme.Colors.Card, gold, 0.5f));

            // Two lit windows, with something on show in each.
            for (int i = 0; i < _windows.Length; i++)
            {
                float x = i == 0 ? -38f : 38f;
                Image frame = Piece("Window" + i, new Vector2(x, -28f), new Vector2(30f, 28f), 7f, inside);
                frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                _windows[i] = LibraryWidgets.Shape("Light", frame.transform, HomeShapes.FadeUp(), HomeTheme.Colors.WithAlpha(gold, 150));
                _windows[i].type = Image.Type.Simple;
                _windows[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
                LibraryUi.Stretch(_windows[i].rectTransform);
                Image mullion = LibraryUi.CreateImage("Mullion", frame.transform, HomeTheme.Colors.WithAlpha(wall, 220));
                mullion.raycastTarget = false;
                LibraryWidgets.Place(mullion.rectTransform, center, center, new Vector2(0f, 3f), new Vector2(30f, 2f));
            }

            Image avatar = LibraryWidgets.Panel("WindowAvatar", _windows[0].transform.parent, 5.5f, HomeTheme.Colors.Violet);
            LibraryWidgets.Place(avatar.rectTransform, center, center, new Vector2(0f, -7f), new Vector2(11f, 11f));
            Color32[] swatches = { HomeTheme.Colors.Rose, HomeTheme.Colors.Violet, HomeTheme.Colors.Periwinkle };
            for (int i = 0; i < swatches.Length; i++)
            {
                Image swatch = LibraryWidgets.Panel("WindowSwatch" + i, _windows[1].transform.parent, 2.5f, swatches[i]);
                LibraryWidgets.Place(swatch.rectTransform, center, center, new Vector2((i - 1) * 7f, -7f), new Vector2(5f, 5f));
            }

            // The door, ajar: warm light inside and a knob.
            Image door = Piece("Door", new Vector2(0f, -50f), new Vector2(32f, 46f), 8f, inside);
            doorLight = LibraryWidgets.Panel("DoorLight", door.transform, 6f, HomeTheme.Colors.WithAlpha(gold, 90));
            LibraryWidgets.Inset(doorLight.rectTransform, 4f, 0f, 10f, 4f);
            Image knob = LibraryWidgets.Panel("Knob", door.transform, 2f, gold);
            LibraryWidgets.Place(knob.rectTransform, center, center, new Vector2(9f, -2f), new Vector2(4f, 4f));

            // The awning: stripes under a rounded top edge, a scalloped hem, a shadow on the wall.
            Image shade = Piece("AwningShade", new Vector2(0f, 6f), new Vector2(124f, 8f), 4f, new Color32(0, 0, 0, 70));
            shade.transform.SetSiblingIndex(building.transform.GetSiblingIndex() + 1);
            Image awning = Piece("Awning", new Vector2(0f, 26f), new Vector2(STRIPE * STRIPES, 28f), 8f, Color.white);
            awning.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            for (int i = 0; i < STRIPES; i++)
            {
                Color32 color = i % 2 == 0 ? gold : _cream;
                float x = (i - (STRIPES - 1) * 0.5f) * STRIPE;
                Image stripe = LibraryUi.CreateImage("Stripe" + i, awning.transform, color);
                stripe.raycastTarget = false;
                LibraryWidgets.Place(stripe.rectTransform, center, center, new Vector2(x, 0f), new Vector2(STRIPE + 0.5f, 28f));
                Piece("Scallop" + i, new Vector2(x, 12f), new Vector2(STRIPE, STRIPE), STRIPE * 0.5f, color);
            }

            Image fold = LibraryWidgets.Shape("Fold", awning.transform, HomeShapes.FadeUp(), HomeTheme.Colors.WithAlpha(Color.white, 90));
            fold.type = Image.Type.Simple;
            LibraryWidgets.TopStrip(fold.rectTransform, 0f, 0f, 0f, 12f);

            // The sign, hung above the awning, with the coin turning on it.
            Piece("PostLeft", new Vector2(-30f, 44f), new Vector2(3f, 8f), 1.5f, Color.Lerp(HomeTheme.Colors.Card, gold, 0.6f));
            Piece("PostRight", new Vector2(30f, 44f), new Vector2(3f, 8f), 1.5f, Color.Lerp(HomeTheme.Colors.Card, gold, 0.6f));
            Image sign = Piece("Sign", new Vector2(0f, 60f), new Vector2(96f, 30f), 15f, Color.Lerp(HomeTheme.Colors.Card, gold, 0.14f));
            Image signLine = LibraryWidgets.Shape("Outline", sign.transform, LibraryShapes.Ring(15f, 1.5f), HomeTheme.Colors.WithAlpha(gold, 170));
            LibraryUi.Stretch(signLine.rectTransform);
            for (int i = 0; i < 2; i++)
            {
                Image star = LibraryWidgets.Icon(sign.transform, LibraryIcon.Star, HomeTheme.Colors.WithAlpha(gold, 200), 11f);
                star.rectTransform.anchoredPosition = new Vector2(i == 0 ? -28f : 28f, 0f);
            }

            signCoin = new ShopCoin((RectTransform)sign.transform, "SignCoin", SIGN_COIN, glow: false);
            LibraryWidgets.Place(signCoin.Root, center, center, Vector2.zero, new Vector2(SIGN_COIN, SIGN_COIN));
        }

        /// <summary>A rounded piece of the shop at <paramref name="position"/> from the middle of the body.</summary>
        private Image Piece(string name, Vector2 position, Vector2 size, float radius, Color32 color)
        {
            Image piece = LibraryWidgets.Panel(name, _store, radius, color);
            Vector2 center = new Vector2(0.5f, 0.5f);
            LibraryWidgets.Place(piece.rectTransform, center, center, position, size);
            return piece;
        }

        /// <summary>One of the goods on the orbit; it moves to the near layer while it passes in front of the shop.</summary>
        private sealed class Good
        {
            private readonly RectTransform _root;
            private readonly CanvasGroup _group;
            private bool _near;

            private Good(RectTransform parent, string name, Vector2 size, float radius)
            {
                _root = LibraryUi.CreateRect(name, parent);
                Vector2 center = new Vector2(0.5f, 0.5f);
                LibraryWidgets.Place(_root, center, center, Vector2.zero, size);
                _group = _root.gameObject.AddComponent<CanvasGroup>();
                LibraryWidgets.Shadow(_root, radius, 16f, 6f, 110);
            }

            internal static Good Avatar(RectTransform parent)
            {
                var good = new Good(parent, "Avatar", new Vector2(60f, 60f), 30f);
                var avatar = new ProfileAvatarView(good._root, "Disc", 60f, glow: false);
                LibraryUi.Stretch(avatar.Root);
                avatar.SetAvatar(ProfileAvatars.DEFAULT_ID);
                return good;
            }

            internal static Good Picture(RectTransform parent, ShopCategory category, Vector2 size, float radius)
            {
                var good = new Good(parent, category.ToString(), size, radius);
                var preview = new ShopPreview(good._root, "Picture", size.y, radius);
                preview.BindCategory(category);
                Image outline = LibraryWidgets.Shape("Outline", good._root, LibraryShapes.Ring(radius, 1.5f),
                    HomeTheme.Colors.WithAlpha(ShopCopy.Tint(category), 170));
                LibraryUi.Stretch(outline.rectTransform);
                return good;
            }

            /// <param name="depth">-1 at the back of the orbit, 1 at the front.</param>
            internal void Apply(Vector2 position, float depth, RectTransform behind, RectTransform ahead)
            {
                bool near = depth > 0f;
                if (near != _near)
                {
                    _near = near;
                    _root.SetParent(near ? ahead : behind, false);
                }

                float front = (depth + 1f) * 0.5f;
                float scale = Mathf.Lerp(0.7f, 1.02f, front);
                _root.anchoredPosition = position;
                _root.localScale = new Vector3(scale, scale, 1f);
                _group.alpha = Mathf.Lerp(0.42f, 1f, front);
            }
        }
    }
}
