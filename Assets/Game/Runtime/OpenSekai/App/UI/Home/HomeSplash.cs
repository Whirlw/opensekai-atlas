using System;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The opening: a note highway draws itself down the screen from a vanishing point, one note
    /// falls and lands on the judgement line, and on the hit the highway pulls together into the
    /// emblem — the outer rails become the legs of the "A" and the judgement line rises into its
    /// crossbar — before the orbit sweeps round, the wordmark settles, and the emblem flies into
    /// the home page's header as the cards come in.
    ///
    /// The whole sequence, home entrance included, is one function of time (<see cref="Evaluate"/>)
    /// advanced once per frame, so a tap can jump ahead and a screenshot can stop on any frame.
    /// </summary>
    internal sealed class HomeSplash : IDisposable
    {
        // ---------------------------------------------------------------- timeline (seconds)

        private const float LANES_START = 0.1f;
        private const float LANES_DURATION = 0.55f;
        private const float LANE_STAGGER = 0.04f;
        private const float JUDGE_START = 0.35f;
        private const float NOTE_START = 0.5f;
        internal const float HIT = 1.05f;
        private const float MORPH_START = 1.12f;
        private const float MORPH_DURATION = 0.55f;
        /// <summary>The solid legs take over once the thin rails have all but closed onto them.</summary>
        private const float RAILS_START = 1.48f;
        private const float ORBIT_START = 1.6f;
        private const float NOTE_DROP_START = 1.8f;
        private const float MOON_START = 2.15f;
        private const float WORDMARK_START = 1.9f;
        private const float TAGLINE_START = 2.25f;

        /// <summary>Where a tap on the splash jumps to: the start of the flight into the header.</summary>
        internal const float HANDOFF = 3.15f;
        private const float HANDOFF_DURATION = 0.75f;
        private const float HOME_START = 3.3f;

        /// <summary>Length of the whole sequence, home entrance included.</summary>
        internal static float Length => HOME_START + HomeView.EnterDuration;

        // ---------------------------------------------------------------- geometry (canvas units)

        private const int LANE_LINES = 7;
        private const float EMBLEM_Y = 64f;
        private const float EMBLEM_SIZE = HomeTheme.Layout.SPLASH_EMBLEM_SIZE;

        /// <summary>How much wider than the emblem's legs the highway opens at the bottom of the screen.</summary>
        private const float HIGHWAY_SPREAD = 1.75f;

        /// <summary>Judgement line's place down the highway, from the vanishing point (0) to the screen's foot (1).</summary>
        private const float JUDGE_DEPTH = 0.72f;
        private const float NOTE_HEIGHT = 34f;

        private readonly RectTransform _stage;
        private readonly CanvasGroup _stageGroup;
        private readonly RectTransform _highway;
        private readonly Image[] _lanes = new Image[LANE_LINES];
        private readonly Image _judge;
        private readonly Image _judgeGlow;
        private readonly Image _note;
        private readonly Image _noteGlow;
        private readonly Image _flare;
        private readonly Image[] _rings = new Image[2];
        private readonly Image[] _sparks = new Image[16];
        private readonly Vector2[] _sparkDirections = new Vector2[16];
        private readonly float[] _sparkReach = new float[16];
        private readonly RectTransform _emblemHost;
        private readonly TextMeshProUGUI _wordmark;
        private readonly TextMeshProUGUI _tagline;
        private readonly Button _skip;

        private readonly HomeBackdrop _backdrop;
        private readonly BrandEmblem _emblem;
        private readonly HomeView _home;

        private const float MAX_FRAME_SECONDS = 0.05f;
        private bool _paused;
        private float _time = -1f;
        private bool _docked = true;
        private Action _onDone;

        internal HomeSplash(RectTransform parent, HomeBackdrop backdrop, BrandEmblem emblem, HomeView home)
        {
            _backdrop = backdrop;
            _emblem = emblem;
            _home = home;

            _stage = LibraryUi.CreateRect("Splash", parent);
            LibraryUi.Stretch(_stage);
            // Nearly everything on the stage moves every frame; a canvas of its own (with its own
            // raycaster for the tap that skips) keeps that away from the home page's batches.
            _stage.gameObject.AddComponent<Canvas>();
            _stage.gameObject.AddComponent<GraphicRaycaster>();
            _stageGroup = _stage.gameObject.AddComponent<CanvasGroup>();

            Vector2 center = new Vector2(0.5f, 0.5f);
            _highway = LibraryUi.CreateRect("Highway", _stage);
            LibraryUi.Stretch(_highway);

            _judgeGlow = Glow(_highway, "JudgeGlow", HomeTheme.Colors.Violet);
            Sprite fade = HomeShapes.FadeDown();
            for (int i = 0; i < LANE_LINES; i++)
            {
                bool rail = i == 0 || i == LANE_LINES - 1;
                Color32 color = rail ? HomeTheme.Colors.Periwinkle : new Color32(200, 212, 255, 255);
                Image lane = LibraryUi.CreateImage(rail ? "Rail" : "Lane" + i, _highway, color);
                lane.sprite = fade;
                lane.raycastTarget = false;
                lane.rectTransform.anchorMin = center;
                lane.rectTransform.anchorMax = center;
                lane.rectTransform.pivot = new Vector2(0.5f, 1f);
                _lanes[i] = lane;
            }

            _judge = LibraryWidgets.Panel("Judge", _highway, 9f, Color.white);
            Center(_judge.rectTransform);
            _noteGlow = Glow(_highway, "NoteGlow", HomeTheme.Colors.Rose);
            _note = LibraryWidgets.Panel("Note", _highway, NOTE_HEIGHT * 0.5f, new Color32(255, 236, 247, 255));
            Center(_note.rectTransform);

            _flare = Glow(_stage, "Flare", new Color32(226, 220, 255, 255));
            Sprite ring = LibraryShapes.Ring(150f, 1.5f);
            for (int i = 0; i < _rings.Length; i++)
            {
                Image shock = LibraryUi.CreateImage("Shock" + i, _stage, i == 0 ? Color.white : (Color)HomeTheme.Colors.Rose);
                shock.sprite = ring;
                shock.type = Image.Type.Simple;
                shock.raycastTarget = false;
                LibraryWidgets.Place(shock.rectTransform, center, center, Vector2.zero, new Vector2(260f, 260f));
                _rings[i] = shock;
            }

            // A fixed seed keeps the burst identical between runs, so screenshots stay comparable.
            var random = new System.Random(1105);
            Sprite pill = LibraryShapes.RoundedRect(3f);
            for (int i = 0; i < _sparks.Length; i++)
            {
                float angle = Mathf.Lerp(12f, 168f, (i + (float)random.NextDouble() * 0.8f) / _sparks.Length) * Mathf.Deg2Rad;
                _sparkDirections[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.75f);
                _sparkReach[i] = 180f + (float)random.NextDouble() * 360f;
                Image spark = LibraryUi.CreateImage("Spark" + i, _stage, i % 3 == 0 ? (Color)HomeTheme.Colors.Rose : Color.white);
                spark.sprite = pill;
                spark.type = Image.Type.Sliced;
                spark.raycastTarget = false;
                Center(spark.rectTransform);
                spark.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle * Mathf.Rad2Deg);
                _sparks[i] = spark;
            }

            _emblemHost = LibraryUi.CreateRect("EmblemHost", _stage);
            LibraryUi.Stretch(_emblemHost);

            _wordmark = LibraryWidgets.Label("Wordmark", _stage, HomeView.Wordmark, HomeTheme.Layout.SPLASH_WORDMARK_SIZE,
                LibraryTheme.Colors.TextPrimary, FontStyles.Normal, TextAlignmentOptions.Center);
            LibraryUi.UseCondensedFont(_wordmark);
            _wordmark.richText = true;
            _wordmark.overflowMode = TextOverflowModes.Overflow;
            LibraryWidgets.Place(_wordmark.rectTransform, center, center, Vector2.zero, new Vector2(1400f, 110f));

            _tagline = LibraryWidgets.Label("Tagline", _stage, "开源下落式音游播放器", 24f, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Center);
            _tagline.characterSpacing = 24f;
            LibraryWidgets.Place(_tagline.rectTransform, center, center, Vector2.zero, new Vector2(900f, 40f));

            Image catcher = LibraryUi.CreateImage("Skip", _stage, new Color32(0, 0, 0, 0));
            LibraryUi.Stretch(catcher.rectTransform);
            _skip = catcher.gameObject.AddComponent<Button>();
            _skip.transition = Selectable.Transition.None;
            _skip.targetGraphic = catcher;
            _skip.onClick.AddListener(Skip);

            _stage.gameObject.SetActive(false);
        }

        internal bool IsPlaying { get; private set; }

        /// <summary>Plays the sequence from the start; <paramref name="onDone"/> runs when the home page has settled.</summary>
        internal void Play(Action onDone)
        {
            Stop();
            _onDone = onDone;
            Evaluate(0f);
            IsPlaying = true;
        }

        internal void Tick(float deltaTime)
        {
            if (!IsPlaying || _paused) return;
            // Cold-start stalls should stretch the animation instead of skipping its reveal.
            float next = _time + Mathf.Min(deltaTime, MAX_FRAME_SECONDS);
            if (next >= Length) Complete();
            else Advance(next);
        }

        /// <summary>A tap: jumps to the emblem's flight into the header unless it is already under way.</summary>
        internal void Skip()
        {
            if (!IsPlaying || _time >= HANDOFF)
            {
                return;
            }

            Evaluate(HANDOFF);
        }

        /// <summary>Shows the frame at <paramref name="time"/> and holds it there.</summary>
        internal void Seek(float time)
        {
            if (!IsPlaying)
            {
                Play(_onDone);
            }

            _paused = true;
            Evaluate(Mathf.Clamp(time, 0f, Length));
        }

        internal void Stop()
        {
            IsPlaying = false;
            _paused = false;
            _onDone = null;
        }

        public void Dispose()
        {
            Stop();
        }

        private void Advance(float time)
        {
            // The hit's sound plays only when the timeline runs across it, not when it jumps.
            if (_time < HIT && time >= HIT && time - _time < 0.1f)
            {
                UiSound.Play(UiCue.Start);
            }

            Evaluate(time);
        }

        private void Complete()
        {
            IsPlaying = false;
            Evaluate(Length);
            _stage.gameObject.SetActive(false);
            Action done = _onDone;
            _onDone = null;
            done?.Invoke();
        }

        /// <summary>Sets every part of the stage, the emblem, the backdrop and the home page for one moment.</summary>
        internal void Evaluate(float time)
        {
            _time = time;
            _stage.gameObject.SetActive(time < Length);
            _skip.gameObject.SetActive(time < HANDOFF);

            Rect area = _stage.rect;
            float apexY = EMBLEM_Y + (BrandArt.APEX_Y - 0.5f) * EMBLEM_SIZE;
            float footY = EMBLEM_Y + (BrandArt.FOOT_Y - 0.5f) * EMBLEM_SIZE;
            float footX = BrandArt.FOOT_SPREAD * EMBLEM_SIZE;
            float slope = footX / (apexY - footY);
            float bottomY = -area.height * 0.5f - 40f;
            float judgeY = Mathf.Lerp(apexY, bottomY, JUDGE_DEPTH);

            // Before the hit the highway is wide open; through the morph it narrows to the
            // emblem's legs while its far end pulls up to their feet.
            float morph = HomeMotion.Span(time, MORPH_START, MORPH_DURATION, Ease.InOutCubic);
            float spread = Mathf.Lerp(HIGHWAY_SPREAD, 1f, morph);
            float lineBottom = Mathf.Lerp(bottomY, footY, morph);
            float outerX = slope * spread * (apexY - lineBottom);

            float hitPulse = time >= HIT ? 1f - HomeMotion.Span(time, HIT, 0.5f, Ease.OutQuad) : 0f;
            float punch = time >= HIT ? Mathf.Sin(Mathf.PI * HomeMotion.Span(time, HIT, 0.32f)) : 0f;
            float stageScale = 1f + 0.028f * punch;
            _highway.localScale = new Vector3(stageScale, stageScale, 1f);

            float linesFade = 1f - HomeMotion.Span(time, RAILS_START + 0.04f, 0.36f, Ease.InQuad);
            for (int i = 0; i < LANE_LINES; i++)
            {
                int fromCenter = Mathf.Abs(i - LANE_LINES / 2);
                bool rail = i == 0 || i == LANE_LINES - 1;
                float draw = HomeMotion.Span(time, LANES_START + fromCenter * LANE_STAGGER, LANES_DURATION, Ease.OutCubic);
                float laneX = Mathf.Lerp(-outerX, outerX, i / (float)(LANE_LINES - 1));
                var top = new Vector2(0f, apexY);
                var bottom = new Vector2(laneX, lineBottom);
                Vector2 span = bottom - top;
                float length = span.magnitude * draw;
                // Inner lanes fold away into the middle as the rails become legs.
                float laneAlpha = rail
                    ? 0.9f * linesFade
                    : 0.32f * (1f - HomeMotion.Span(time, MORPH_START, 0.35f, Ease.OutQuad));
                RectTransform rect = _lanes[i].rectTransform;
                rect.anchoredPosition = top;
                rect.sizeDelta = new Vector2(rail ? 5f + 3f * hitPulse : 2.4f, Mathf.Max(0f, length));
                rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(span.x, -span.y) * Mathf.Rad2Deg);
                SetAlpha(_lanes[i], laneAlpha * (draw > 0f ? 1f : 0f) * (1f + 0.6f * hitPulse));
            }

            // Judgement line: opens across the highway, flares on the hit, then rises and shrinks
            // into the crossbar while the emblem's own crossbar takes over from it.
            float barY = EMBLEM_Y + (BrandArt.BAR_Y - 0.5f) * EMBLEM_SIZE;
            float barHalf = (BrandArt.BAR_HALF_WIDTH + BrandArt.BAR_HALF_HEIGHT) * EMBLEM_SIZE;
            float barHeight = BrandArt.BAR_HALF_HEIGHT * 2f * EMBLEM_SIZE;
            float judgeOpen = HomeMotion.Span(time, JUDGE_START, 0.45f, Ease.OutCubic);
            float judgeHalf = slope * HIGHWAY_SPREAD * (apexY - judgeY) * 1.06f * judgeOpen;
            float judgeHeight = 8f + 12f * hitPulse;
            float rise = HomeMotion.Span(time, MORPH_START, MORPH_DURATION, Ease.InOutCubic);
            float y = Mathf.Lerp(judgeY, barY, rise);
            float half = Mathf.Lerp(judgeHalf, barHalf, rise);
            float height = Mathf.Lerp(judgeHeight, barHeight, rise);
            _judge.rectTransform.anchoredPosition = new Vector2(0f, y);
            _judge.rectTransform.sizeDelta = new Vector2(half * 2f, height);
            float crossbar = HomeMotion.Span(time, MORPH_START + MORPH_DURATION - 0.08f, 0.1f);
            SetAlpha(_judge, judgeOpen * (1f - crossbar));
            _judgeGlow.rectTransform.anchoredPosition = new Vector2(0f, y);
            _judgeGlow.rectTransform.sizeDelta = new Vector2(half * 2.6f + 120f, 150f + 120f * hitPulse);
            SetAlpha(_judgeGlow, judgeOpen * (0.22f + 0.6f * hitPulse) * (1f - crossbar));

            // The falling note grows as it comes down the highway, the way a note nears the player.
            float fall = HomeMotion.Span(time, NOTE_START, HIT - NOTE_START, Ease.InQuad);
            bool falling = time >= NOTE_START && time < HIT;
            float noteY = Mathf.Lerp(apexY - 6f, judgeY, fall);
            float near = Mathf.Clamp01((apexY - noteY) / (apexY - judgeY));
            float noteWidth = judgeHalf / Mathf.Max(0.001f, judgeOpen) * 2f / 3f * 0.94f * near;
            float noteHeight = NOTE_HEIGHT * (0.3f + 0.7f * near);
            _note.rectTransform.anchoredPosition = new Vector2(0f, noteY + noteHeight * 0.5f);
            _note.rectTransform.sizeDelta = new Vector2(Mathf.Max(noteHeight, noteWidth), noteHeight);
            SetAlpha(_note, falling ? HomeMotion.Span(time, NOTE_START, 0.12f) : 0f);
            _noteGlow.rectTransform.anchoredPosition = _note.rectTransform.anchoredPosition;
            _noteGlow.rectTransform.sizeDelta = new Vector2(noteWidth * 2.2f + 40f, noteHeight * 5f);
            SetAlpha(_noteGlow, falling ? 0.55f * near : 0f);

            // The hit: a flare along the line, two shock rings and a spray of sparks.
            var hitPoint = new Vector2(0f, judgeY);
            _flare.rectTransform.anchoredPosition = hitPoint;
            _flare.rectTransform.sizeDelta = new Vector2(1800f, 320f);
            SetAlpha(_flare, 0.85f * hitPulse);
            for (int i = 0; i < _rings.Length; i++)
            {
                float start = HIT + i * 0.08f;
                float grow = HomeMotion.Span(time, start, 0.75f, Ease.OutCubic);
                float scale = 0.4f + 5.6f * grow;
                RectTransform rect = _rings[i].rectTransform;
                rect.anchoredPosition = hitPoint;
                rect.localScale = new Vector3(scale, scale * 0.3f, 1f);
                SetAlpha(_rings[i], time >= start ? 0.8f * (1f - HomeMotion.Span(time, start, 0.75f)) : 0f);
            }

            for (int i = 0; i < _sparks.Length; i++)
            {
                float travel = HomeMotion.Span(time, HIT, 0.7f, Ease.OutCubic);
                float fade = time >= HIT ? 1f - HomeMotion.Span(time, HIT + 0.08f, 0.6f, Ease.InQuad) : 0f;
                RectTransform rect = _sparks[i].rectTransform;
                rect.anchoredPosition = hitPoint + _sparkDirections[i] * (_sparkReach[i] * travel);
                rect.sizeDelta = new Vector2(10f + 34f * (1f - travel), 5f);
                SetAlpha(_sparks[i], fade);
            }

            // Backdrop: a faint glow while the highway draws, full light after the hit.
            float dawn = 0.32f * HomeMotion.Span(time, 0.1f, 0.8f, Ease.OutQuad);
            float bloom = HomeMotion.Span(time, HIT, 0.7f, Ease.OutCubic);
            _backdrop.SetIntensity(Mathf.Lerp(dawn, 1f, bloom));

            // The emblem itself.
            _emblem.Reveal(
                HomeMotion.Span(time, RAILS_START, 0.32f, Ease.OutQuad),
                crossbar,
                HomeMotion.Span(time, NOTE_DROP_START, 0.45f, Ease.OutBack),
                HomeMotion.Span(time, ORBIT_START, 0.65f, Ease.InOutCubic),
                HomeMotion.Span(time, MOON_START, 0.4f, Ease.OutBack),
                HomeMotion.Span(time, MORPH_START, 0.8f, Ease.OutQuad));
            PlaceEmblem(time);

            // Wordmark and tagline, which give way to the header's own once the emblem flies.
            float leave = HomeMotion.Span(time, HANDOFF, 0.28f, Ease.InQuad);
            float word = HomeMotion.Span(time, WORDMARK_START, 0.7f, Ease.OutCubic);
            float wordY = EMBLEM_Y - EMBLEM_SIZE * 0.5f - 66f;
            _wordmark.alpha = HomeMotion.Span(time, WORDMARK_START, 0.5f, Ease.OutQuad) * (1f - leave);
            _wordmark.characterSpacing = Mathf.Lerp(64f, 18f, word);
            _wordmark.rectTransform.anchoredPosition = new Vector2(0f, wordY - 16f * (1f - word) + 14f * leave);
            float tag = HomeMotion.Span(time, TAGLINE_START, 0.55f, Ease.OutCubic);
            _tagline.alpha = tag * (1f - leave);
            _tagline.rectTransform.anchoredPosition = new Vector2(0f, wordY - 72f - 10f * (1f - tag) + 14f * leave);

            _home.ApplyEnter(time - HOME_START);
            _home.Interactable = time >= HANDOFF + HANDOFF_DURATION;
            _stageGroup.alpha = 1f;
        }

        /// <summary>
        /// Centers the emblem on the stage until the hand-over, flies it to the header's slot
        /// through it, and docks it in the slot afterwards so it follows the header's layout.
        /// </summary>
        private void PlaceEmblem(float time)
        {
            float flight = HomeMotion.Span(time, HANDOFF, HANDOFF_DURATION, Ease.InOutCubic);
            bool docked = flight >= 1f;
            RectTransform root = _emblem.Root;
            RectTransform slot = _home.BrandSlot;
            if (docked != _docked)
            {
                _docked = docked;
                root.SetParent(docked ? slot : _emblemHost, false);
                if (docked)
                {
                    LibraryUi.Stretch(root);
                }
                else
                {
                    var center = new Vector2(0.5f, 0.5f);
                    root.anchorMin = center;
                    root.anchorMax = center;
                    root.pivot = center;
                }
            }

            if (docked)
            {
                return;
            }

            Vector3 world = slot.TransformPoint(slot.rect.center);
            Vector2 target = _emblemHost.InverseTransformPoint(world);
            float targetSize = slot.rect.width;
            var start = new Vector2(0f, EMBLEM_Y);
            // A slight arc: the emblem lifts before it heads for the corner.
            Vector2 position = Vector2.Lerp(start, target, flight) + new Vector2(0f, Mathf.Sin(flight * Mathf.PI) * 40f);
            float size = Mathf.Lerp(EMBLEM_SIZE, targetSize, flight);
            root.anchoredPosition = position;
            root.sizeDelta = new Vector2(size, size);
        }

        private static Image Glow(Transform parent, string name, Color32 color)
        {
            Image glow = LibraryUi.CreateImage(name, parent, color);
            glow.sprite = LibraryShapes.Glow();
            glow.raycastTarget = false;
            Center(glow.rectTransform);
            SetAlpha(glow, 0f);
            return glow;
        }

        private static void Center(RectTransform rect)
        {
            var center = new Vector2(0.5f, 0.5f);
            rect.anchorMin = center;
            rect.anchorMax = center;
            rect.pivot = center;
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = Mathf.Clamp01(alpha);
            graphic.color = color;
        }
    }
}
