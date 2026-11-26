using System;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// 段位认定: what a pass ends with. The page dims, rays in the plate's color fan out and turn,
    /// the plate slams down from above with a flash, a ring and a spray of sparks, a light sweeps
    /// across it and the words come up: 取得「…」. 佩戴段位牌 wears it at once; 继续 goes back to the
    /// result list. The whole sequence is a pure function of time, so a tap skips to its end and a
    /// screenshot can stop at any frame.
    /// </summary>
    internal sealed class DanAwardCeremony : IDisposable
    {
        private const float LENGTH = 2.1f;
        private const int RAYS = 12;
        private const int SPARKS = 18;
        private const float IMPACT = 0.62f;
        private const float SHINE_AT = 1.2f;
        private const float BUTTON_HEIGHT = 84f;

        private readonly RectTransform _root;
        private readonly Image _scrim;
        private readonly RectTransform _rays;
        private readonly CanvasGroup _rayGroup;
        private readonly Image[] _rayImages = new Image[RAYS];
        private readonly Image _halo;
        private readonly Image _ring;
        private readonly Image _flash;
        private readonly RectTransform[] _sparks = new RectTransform[SPARKS];
        private readonly Image[] _sparkImages = new Image[SPARKS];
        private readonly Vector2[] _sparkPaths = new Vector2[SPARKS];
        private readonly MedalPlate _plate;
        private readonly CanvasGroup _plateGroup;
        private readonly RectTransform _copy;
        private readonly CanvasGroup _copyGroup;
        private readonly TextMeshProUGUI _eyebrow;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _note;
        private readonly CanvasGroup _buttons;
        private readonly Button _equip;
        private readonly Action _onEquip;
        private readonly Action _onClose;
        private MotionHandle _motion;
        private MotionHandle _spin;
        private bool _shone;
        private bool _impacted;
        private bool _equipped;

        internal DanAwardCeremony(RectTransform parent, Action onEquip, Action onClose)
        {
            _onEquip = onEquip;
            _onClose = onClose;
            _root = LibraryUi.CreateRect("DanAward", parent);
            LibraryUi.Stretch(_root);
            _scrim = LibraryUi.CreateImage("Scrim", _root, LibraryTheme.Colors.Ground);
            _scrim.raycastTarget = true;
            LibraryUi.Stretch(_scrim.rectTransform);
            _root.gameObject.AddComponent<SkipArea>().Owner = this;

            Vector2 center = new Vector2(0.5f, 0.5f);
            Vector2 focus = new Vector2(0f, 90f);
            _halo = LibraryUi.CreateImage("Halo", _root, Color.clear);
            _halo.sprite = LibraryShapes.Glow();
            _halo.raycastTarget = false;
            LibraryWidgets.Place(_halo.rectTransform, center, center, focus, new Vector2(1500f, 1500f));

            _rays = LibraryUi.CreateRect("Rays", _root);
            LibraryWidgets.Place(_rays, center, center, focus, new Vector2(10f, 10f));
            _rayGroup = _rays.gameObject.AddComponent<CanvasGroup>();
            for (int i = 0; i < RAYS; i++)
            {
                RectTransform arm = LibraryUi.CreateRect("Ray" + i, _rays);
                LibraryWidgets.Place(arm, center, center, Vector2.zero, new Vector2(10f, 10f));
                arm.localEulerAngles = new Vector3(0f, 0f, i * (360f / RAYS));
                // Bright at the plate, fading outward.
                Image ray = LibraryWidgets.Shape("Beam", arm, HomeShapes.FadeDown(), Color.white);
                ray.type = Image.Type.Simple;
                LibraryWidgets.Place(ray.rectTransform, center, new Vector2(0.5f, 0f), new Vector2(0f, 60f),
                    new Vector2(i % 2 == 0 ? 54f : 26f, i % 2 == 0 ? 760f : 560f));
                _rayImages[i] = ray;
            }

            _ring = LibraryWidgets.Shape("Ring", _root, LibraryShapes.Ring(200f, 6f), Color.clear);
            LibraryWidgets.Place(_ring.rectTransform, center, center, focus, new Vector2(400f, 400f));

            var random = new System.Random(20261122);
            for (int i = 0; i < SPARKS; i++)
            {
                float angle = (i / (float)SPARKS + (float)random.NextDouble() * 0.04f) * Mathf.PI * 2f;
                float distance = 360f + (float)random.NextDouble() * 260f;
                _sparkPaths[i] = new Vector2(Mathf.Cos(angle) * distance * 1.25f, Mathf.Sin(angle) * distance * 0.8f);
                Image spark = LibraryWidgets.Panel("Spark" + i, _root, 3f, Color.white);
                float size = 10f + (float)random.NextDouble() * 12f;
                LibraryWidgets.Place(spark.rectTransform, center, center, focus, new Vector2(size, size));
                spark.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
                _sparks[i] = spark.rectTransform;
                _sparkImages[i] = spark;
            }

            _plate = new MedalPlate(_root, "Plate", new Vector2(760f, 160f), glow: true);
            LibraryWidgets.Place(_plate.Root, center, center, focus, _plate.Root.sizeDelta);
            _plateGroup = _plate.Root.gameObject.AddComponent<CanvasGroup>();

            _flash = LibraryUi.CreateImage("Flash", _root, Color.clear);
            _flash.sprite = LibraryShapes.Glow();
            _flash.raycastTarget = false;
            LibraryWidgets.Place(_flash.rectTransform, center, center, focus, new Vector2(1200f, 700f));

            _copy = LibraryUi.CreateRect("Copy", _root);
            LibraryWidgets.Place(_copy, center, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1400f, 200f));
            _copyGroup = _copy.gameObject.AddComponent<CanvasGroup>();
            _eyebrow = LibraryWidgets.Label("Eyebrow", _copy, "段位认定  ·  DAN CERTIFIED", 22f, DanCopy.Pass, FontStyles.Normal,
                TextAlignmentOptions.Center);
            LibraryUi.UseSpacedCondensedFont(_eyebrow, 20f);
            LibraryWidgets.TopStrip(_eyebrow.rectTransform, 0f, 0f, 0f, 32f);
            _title = PackWidgets.UserLabel("Title", _copy, string.Empty, 56f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold,
                TextAlignmentOptions.Center);
            LibraryWidgets.TopStrip(_title.rectTransform, 0f, 0f, 38f, 76f);
            _note = LibraryWidgets.Label("Note", _copy, string.Empty, 22f, LibraryTheme.Colors.TextSecondary, FontStyles.Normal,
                TextAlignmentOptions.Center);
            LibraryWidgets.TopStrip(_note.rectTransform, 0f, 0f, 122f, 32f);

            RectTransform buttons = LibraryUi.CreateRect("Buttons", _root);
            LibraryWidgets.Place(buttons, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(620f, BUTTON_HEIGHT));
            _buttons = buttons.gameObject.AddComponent<CanvasGroup>();
            _equip = ServerWidgets.LabelledButton(buttons, "Equip", "佩戴段位牌", LibraryIcon.Tag, () => _onEquip?.Invoke(), 320f, BUTTON_HEIGHT,
                DanCopy.Pass, LibraryTheme.Colors.TextOnAccent, UiCue.Success, 24f);
            LibraryWidgets.Place((RectTransform)_equip.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(320f, BUTTON_HEIGHT));
            Button close = LibraryWidgets.PillButton("Continue", buttons, "继续", Hide, 280f, BUTTON_HEIGHT,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, 24f, FontStyles.Bold, UiCue.Close);
            LibraryWidgets.Outline((Image)close.targetGraphic, BUTTON_HEIGHT * 0.5f);
            LibraryWidgets.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(280f, BUTTON_HEIGHT));
            _root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _root.gameObject.activeSelf;

        internal void Show(DanResultModel model, bool animate)
        {
            Color32 tint = DanStyles.ParseColor(model.Definition.Color);
            Color32 light = DanStyles.Light(tint);
            _plate.SetLook(MedalLook.Of(model.Definition));
            _plate.SetLocked(false);
            _halo.color = HomeTheme.Colors.WithAlpha(tint, 90);
            for (int i = 0; i < RAYS; i++)
            {
                _rayImages[i].color = HomeTheme.Colors.WithAlpha(i % 2 == 0 ? light : (Color32)Color.Lerp(light, DanCopy.Pass, 0.5f),
                    (byte)(i % 2 == 0 ? 60 : 34));
            }

            for (int i = 0; i < SPARKS; i++) _sparkImages[i].color = i % 3 == 0 ? DanCopy.Pass : (Color32)light;
            _title.text = "取得「" + model.Definition.Label + "」";
            _note.text = model.NewlyAwarded ? "首次通过，段位牌已收入个人信息，可以随时佩戴展示" : "再次通过这个段位，段位牌已在你的收藏中";
            _equipped = model.Equipped;
            BindEquip();
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            _shone = false;
            _impacted = false;
            _motion.TryCancel();
            _spin.TryCancel();
            _spin = LMotion.Create(0f, 360f, 60f)
                .WithLoops(-1)
                .WithScheduler(HomeMotion.Scheduler)
                .Bind(angle => _rays.localEulerAngles = new Vector3(0f, 0f, -angle));
            if (!animate)
            {
                Apply(LENGTH);
                return;
            }

            Apply(0f);
            _motion = LMotion.Create(0f, LENGTH, LENGTH)
                .WithScheduler(HomeMotion.Scheduler)
                .Bind(Apply);
        }

        /// <summary>Holds the sequence at <paramref name="seconds"/>, for screenshots.</summary>
        internal void Seek(float seconds)
        {
            _motion.TryCancel();
            _spin.TryCancel();
            _rays.localEulerAngles = new Vector3(0f, 0f, -seconds * 6f);
            _impacted = true;
            _shone = true;
            Apply(Mathf.Clamp(seconds, 0f, LENGTH));
        }

        internal void MarkEquipped()
        {
            _equipped = true;
            BindEquip();
        }

        internal void Hide()
        {
            if (!IsOpen) return;
            _motion.TryCancel();
            _spin.TryCancel();
            _root.gameObject.SetActive(false);
            _onClose?.Invoke();
        }

        public void Dispose()
        {
            _motion.TryCancel();
            _spin.TryCancel();
            _plate.Dispose();
        }

        private void BindEquip()
        {
            ServerWidgets.SetLabelledButton(_equip, _equipped ? "已佩戴" : "佩戴段位牌", _equipped ? LibraryIcon.Check : LibraryIcon.Tag,
                _equipped ? LibraryTheme.Colors.Control : DanCopy.Pass, _equipped ? DanCopy.Pass : LibraryTheme.Colors.TextOnAccent);
            PackWidgets.SetEnabled(_equip, !_equipped);
        }

        private void Skip()
        {
            if (!_motion.IsActive()) return;
            _motion.TryCancel();
            Apply(LENGTH);
        }

        private void Apply(float time)
        {
            Color scrim = LibraryTheme.Colors.Ground;
            scrim.a = 0.92f * HomeMotion.Span(time, 0f, 0.25f, Ease.OutQuad);
            _scrim.color = scrim;
            _rayGroup.alpha = HomeMotion.Span(time, 0.1f, 0.5f, Ease.OutQuad);
            float rayScale = Mathf.Lerp(0.6f, 1f, HomeMotion.Span(time, 0.1f, 0.9f, Ease.OutCubic));
            _rays.localScale = new Vector3(rayScale, rayScale, 1f);
            Color halo = _halo.color;
            halo.a = 0.35f * HomeMotion.Span(time, 0.2f, 0.6f, Ease.OutQuad);
            _halo.color = halo;

            // The plate falls from far above the screen's plane onto it.
            float fall = HomeMotion.Span(time, 0.2f, IMPACT - 0.2f, Ease.InCubic);
            float settle = HomeMotion.Span(time, IMPACT, 0.3f, Ease.OutBack);
            float scale = time < IMPACT ? Mathf.Lerp(2.6f, 1f, fall) : Mathf.LerpUnclamped(0.94f, 1f, settle);
            _plate.Root.localScale = new Vector3(scale, scale, 1f);
            _plate.Root.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-9f, 0f, fall));
            _plateGroup.alpha = HomeMotion.Span(time, 0.2f, 0.2f, Ease.OutQuad);

            float flash = time < IMPACT ? 0f : 1f - HomeMotion.Span(time, IMPACT, 0.45f, Ease.OutQuad);
            _flash.color = new Color(1f, 1f, 1f, 0.75f * flash);
            float burst = HomeMotion.Span(time, IMPACT, 0.75f, Ease.OutCubic);
            // Scaled, not resized: a sliced ring keeps its corner radius, so only scaling stays round.
            float ringScale = Mathf.Lerp(0.95f, 3.6f, burst);
            _ring.rectTransform.localScale = new Vector3(ringScale, ringScale * 0.7f, 1f);
            _ring.color = new Color(1f, 1f, 1f, time < IMPACT ? 0f : 0.55f * (1f - burst));

            float spray = HomeMotion.Span(time, IMPACT, 0.95f, Ease.OutCubic);
            for (int i = 0; i < SPARKS; i++)
            {
                _sparks[i].anchoredPosition = new Vector2(0f, 90f) + _sparkPaths[i] * spray;
                Color color = _sparkImages[i].color;
                color.a = time < IMPACT ? 0f : 1f - HomeMotion.Span(time, IMPACT + 0.35f, 0.6f, Ease.InQuad);
                _sparkImages[i].color = color;
                float spin = 45f + spray * (i % 2 == 0 ? 180f : -180f);
                _sparks[i].localEulerAngles = new Vector3(0f, 0f, spin);
            }

            if (!_impacted && time >= IMPACT)
            {
                _impacted = true;
                UiSound.Play(UiCue.Achievement);
            }

            if (!_shone && time >= SHINE_AT)
            {
                _shone = true;
                _plate.Shine(0f, 0.8f);
            }

            float copy = HomeMotion.Span(time, 0.95f, 0.4f, Ease.OutCubic);
            _copyGroup.alpha = copy;
            _copy.anchoredPosition = new Vector2(0f, -20f - 24f * (1f - copy));
            float buttons = HomeMotion.Span(time, 1.45f, 0.4f, Ease.OutQuad);
            _buttons.alpha = buttons;
            _buttons.blocksRaycasts = buttons > 0.5f;
        }

        /// <summary>A tap on the dimmed page skips to the end of the sequence.</summary>
        private sealed class SkipArea : MonoBehaviour, IPointerClickHandler
        {
            internal DanAwardCeremony Owner { get; set; }

            public void OnPointerClick(PointerEventData eventData) => Owner?.Skip();
        }
    }
}
