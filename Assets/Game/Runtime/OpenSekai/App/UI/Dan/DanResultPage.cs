using System;
using System.Collections.Generic;
using System.Globalization;
using LitMotion;
using OpenSekai.App.Library;
using OpenSekai.App.Playback;
using OpenSekai.Shared.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>One finished (or abandoned) attempt, as the result page shows it.</summary>
    internal sealed class DanResultModel
    {
        /// <param name="unlocks">Achievements the attempt unlocked; announced once the result has played.</param>
        internal DanResultModel(DanDefinition definition, DanAttempt attempt, bool newlyAwarded, bool equipped, string error,
            IReadOnlyList<DanSongInfo> songs, IReadOnlyList<string> unlocks = null)
        {
            UnlockedAchievementIds = unlocks ?? Array.Empty<string>();
            Definition = definition;
            Attempt = attempt;
            NewlyAwarded = newlyAwarded;
            Equipped = equipped;
            Error = error;
            Songs = songs;
        }

        internal DanDefinition Definition { get; }
        internal DanAttempt Attempt { get; }
        internal bool NewlyAwarded { get; }
        internal bool Equipped { get; set; }

        /// <summary>Why the course stopped when a song could not start; null otherwise.</summary>
        internal string Error { get; }

        internal IReadOnlyList<DanSongInfo> Songs { get; }

        /// <summary>Achievements this attempt unlocked, for the card after the result; empty when none.</summary>
        internal IReadOnlyList<string> UnlockedAchievementIds { get; }

        internal bool Passed => Attempt.Outcome == DanOutcome.Passed;
    }

    /// <summary>
    /// 段位结算: whether the course was passed, every song in a list with its score, judgments and
    /// what it cost in life, and the life left at the end. The songs come in one after another with
    /// their life draining, then the verdict stamps in; a pass goes on into the award ceremony
    /// (<see cref="DanAwardCeremony"/>). Achievements the attempt unlocked get their card once that
    /// is over (after the ceremony on a pass). A tap skips to the end. 再次挑战 starts the same 段位 again.
    /// </summary>
    internal sealed class DanResultPage : IDisposable
    {
        private const float LEFT_WIDTH = 600f;
        private const float ROW_HEIGHT = 112f;
        private const float ROW_GAP = 10f;
        private const float ROW_STAGGER = 0.16f;
        private const float ROW_DRAIN = 0.4f;
        private const float VERDICT = 0.35f;

        /// <summary>Attempts whose unlocked achievements have been announced in this run of the app.</summary>
        private static readonly HashSet<string> _announced = new HashSet<string>(StringComparer.Ordinal);

        private readonly RectTransform _root;
        private readonly Image _glow;
        private readonly TextMeshProUGUI _subtitle;
        private readonly CanvasGroup _leftGroup;
        private readonly DanPlate _plate;
        private readonly RectTransform _verdictRoot;
        private readonly CanvasGroup _verdictGroup;
        private readonly TextMeshProUGUI _verdict;
        private readonly TextMeshProUGUI _verdictLatin;
        private readonly TextMeshProUGUI _reason;
        private readonly TextMeshProUGUI _life;
        private readonly TextMeshProUGUI _lifeMax;
        private readonly Image _lifeFill;
        private readonly TextMeshProUGUI _award;
        private readonly Button _equip;
        private readonly TextMeshProUGUI _error;
        private readonly TextMeshProUGUI _listSummary;
        private readonly RectTransform _rowContent;
        private readonly List<Row> _rows = new List<Row>();
        private readonly DanAwardCeremony _ceremony;
        private readonly AchievementUnlockPopup _unlock;
        private readonly Action _onBack;
        private readonly Action<DanDefinition> _onRetry;
        private readonly Func<DanDefinition, bool> _onEquip;
        private DanResultModel _model;
        private MotionHandle _reveal;
        private float _revealLength;
        private bool _revealDone;
        private bool _unlockPending;

        internal DanResultPage(RectTransform parent, Action onBack, Action<DanDefinition> onRetry, Func<DanDefinition, bool> onEquip)
        {
            _onBack = onBack;
            _onRetry = onRetry;
            _onEquip = onEquip;
            _root = LibraryUi.CreateRect("DanResult", parent);
            LibraryUi.Stretch(_root);
            Image ground = LibraryUi.CreateImage("Ground", _root, LibraryTheme.Colors.Ground);
            ground.raycastTarget = true;
            LibraryUi.Stretch(ground.rectTransform);
            _root.gameObject.AddComponent<SkipArea>().Owner = this;
            _glow = LibraryUi.CreateImage("Glow", _root, Color.clear);
            _glow.sprite = LibraryShapes.Glow();
            _glow.raycastTarget = false;
            LibraryWidgets.Place(_glow.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(420f, -380f), new Vector2(1800f, 1800f));

            RectTransform bar = LibraryUi.CreateRect("TopBar", _root);
            float margin = LibraryTheme.Spacing.PAGE_MARGIN;
            LibraryWidgets.TopStrip(bar, margin, margin, 0f, LibraryTheme.Layout.TOP_BAR_HEIGHT);
            float control = LibraryTheme.Layout.CONTROL_HEIGHT;
            Button back = ServerWidgets.IconButton(bar, "Back", LibraryIcon.Back, () => _onBack?.Invoke(), UiCue.Back);
            LibraryWidgets.Place((RectTransform)back.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(control, control));
            TextMeshProUGUI title = LibraryWidgets.Label("Title", bar, "段位结算", LibraryTheme.FontSize.PAGE_TITLE, LibraryTheme.Colors.TextPrimary,
                FontStyles.Bold);
            LibraryWidgets.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(control + 20f, -6f), new Vector2(800f, 52f));
            _subtitle = LibraryWidgets.Label("Subtitle", bar, string.Empty, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.Place(_subtitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(control + 22f, -8f), new Vector2(900f, 28f));

            RectTransform body = LibraryUi.CreateRect("Body", _root);
            LibraryWidgets.Inset(body, margin, 40f, margin, LibraryTheme.Layout.TOP_BAR_HEIGHT + 8f);

            // ---------------------------------------------------------------- the verdict
            RectTransform left = LibraryUi.CreateRect("Left", body);
            left.anchorMin = Vector2.zero;
            left.anchorMax = new Vector2(0f, 1f);
            left.pivot = new Vector2(0f, 0.5f);
            left.offsetMin = Vector2.zero;
            left.offsetMax = new Vector2(LEFT_WIDTH, 0f);
            _leftGroup = left.gameObject.AddComponent<CanvasGroup>();
            Vector2 topLeft = new Vector2(0f, 1f);
            _plate = new DanPlate(left, "Plate", new Vector2(LEFT_WIDTH - 40f, 108f), glow: true);
            LibraryWidgets.Place(_plate.Root, topLeft, topLeft, new Vector2(10f, -12f), _plate.Root.sizeDelta);

            _verdictRoot = LibraryUi.CreateRect("Verdict", left);
            LibraryWidgets.Place(_verdictRoot, topLeft, new Vector2(0f, 0.5f), new Vector2(10f, -210f), new Vector2(LEFT_WIDTH, 110f));
            _verdictGroup = _verdictRoot.gameObject.AddComponent<CanvasGroup>();
            _verdict = LibraryWidgets.Label("Outcome", _verdictRoot, string.Empty, 84f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            _verdict.overflowMode = TextOverflowModes.Overflow;
            LibraryWidgets.Place(_verdict.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(320f, 110f));
            _verdictLatin = LibraryWidgets.Label("Latin", _verdictRoot, string.Empty, 30f, LibraryTheme.Colors.TextPrimary);
            LibraryUi.UseSpacedCondensedFont(_verdictLatin, 30f);
            LibraryWidgets.Place(_verdictLatin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(240f, -14f), new Vector2(340f, 40f));
            _reason = LibraryWidgets.Label("Reason", left, string.Empty, 21f, LibraryTheme.Colors.TextSecondary);
            _reason.richText = false;
            LibraryWidgets.TopStrip(_reason.rectTransform, 12f, 0f, 272f, 32f);

            DanWidgets.Caption(left, "剩余生命", 12f, 330f);
            _life = LibraryWidgets.Label("Life", left, "0", 96f, LibraryTheme.Colors.TextPrimary);
            LibraryUi.UseCondensedFont(_life);
            _life.overflowMode = TextOverflowModes.Overflow;
            _life.alignment = TextAlignmentOptions.BottomLeft;
            LibraryWidgets.Place(_life.rectTransform, topLeft, topLeft, new Vector2(8f, -352f), new Vector2(300f, 104f));
            _lifeMax = LibraryWidgets.Label("Max", left, string.Empty, 34f, LibraryTheme.Colors.TextMuted);
            LibraryUi.UseCondensedFont(_lifeMax);
            _lifeMax.overflowMode = TextOverflowModes.Overflow;
            _lifeMax.alignment = TextAlignmentOptions.BottomLeft;
            LibraryWidgets.Place(_lifeMax.rectTransform, topLeft, topLeft, new Vector2(8f, -368f), new Vector2(LEFT_WIDTH, 84f));
            Image track = LibraryWidgets.Panel("LifeTrack", left, 7f, HomeTheme.Colors.Chip);
            LibraryWidgets.TopStrip(track.rectTransform, 12f, 40f, 470f, 14f);
            _lifeFill = LibraryWidgets.Panel("LifeFill", track.transform, 7f, DanCopy.Life);
            _lifeFill.rectTransform.anchorMin = Vector2.zero;
            _lifeFill.rectTransform.anchorMax = new Vector2(1f, 1f);
            _lifeFill.rectTransform.offsetMin = Vector2.zero;
            _lifeFill.rectTransform.offsetMax = Vector2.zero;

            _award = LibraryWidgets.Label("Award", left, string.Empty, 21f, DanCopy.Pass, FontStyles.Bold);
            _award.richText = false;
            LibraryWidgets.TopStrip(_award.rectTransform, 12f, 0f, 508f, 32f);
            const float EQUIP_HEIGHT = 56f;
            _equip = ServerWidgets.LabelledButton(left, "Equip", "佩戴段位牌", LibraryIcon.Tag, OnEquip, 220f, EQUIP_HEIGHT,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, UiCue.None, 19f);
            LibraryWidgets.Outline((Image)_equip.targetGraphic, EQUIP_HEIGHT * 0.5f);
            LibraryWidgets.Place((RectTransform)_equip.transform, topLeft, topLeft, new Vector2(12f, -550f), new Vector2(220f, EQUIP_HEIGHT));

            _error = PackWidgets.Paragraph(left, "Error", string.Empty, 18f, LibraryTheme.Colors.Danger);
            LibraryWidgets.TopStrip(_error.rectTransform, 12f, 20f, 620f, 60f);

            float height = LibraryTheme.Layout.PRIMARY_BUTTON_HEIGHT;
            Button retry = ServerWidgets.LabelledButton(left, "Retry", "再次挑战", LibraryIcon.Refresh, () => _onRetry?.Invoke(_model?.Definition),
                300f, height, LibraryTheme.Colors.TextPrimary, LibraryTheme.Colors.TextOnAccent, UiCue.Retry, 24f);
            LibraryWidgets.Place((RectTransform)retry.transform, Vector2.zero, Vector2.zero, new Vector2(10f, 0f), new Vector2(300f, height));
            Button leave = LibraryWidgets.PillButton("Leave", left, "返回段位", () => _onBack?.Invoke(), 230f, height,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, 22f, FontStyles.Bold, UiCue.Back);
            LibraryWidgets.Outline((Image)leave.targetGraphic, height * 0.5f);
            LibraryWidgets.Place((RectTransform)leave.transform, Vector2.zero, Vector2.zero, new Vector2(326f, 0f), new Vector2(230f, height));

            // ---------------------------------------------------------------- the songs
            RectTransform right = LibraryUi.CreateRect("Right", body);
            LibraryWidgets.Inset(right, LEFT_WIDTH + 40f, 0f, 0f, 0f);
            Image card = LibraryWidgets.Panel("Card", right, 30f, HomeTheme.Colors.Card);
            LibraryUi.Stretch(card.rectTransform);
            LibraryWidgets.Outline(card, 30f);
            TextMeshProUGUI listTitle = LibraryWidgets.Label("ListTitle", card.transform, "逐曲成绩", 26f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.TopStrip(listTitle.rectTransform, 32f, 32f, 26f, 40f);
            _listSummary = LibraryWidgets.Label("ListSummary", card.transform, string.Empty, 18f, LibraryTheme.Colors.TextMuted,
                FontStyles.Normal, TextAlignmentOptions.Right);
            LibraryWidgets.TopStrip(_listSummary.rectTransform, 300f, 32f, 32f, 30f);
            RectTransform list = LibraryUi.CreateRect("List", card.transform);
            LibraryWidgets.Inset(list, 18f, 18f, 18f, 84f);
            _rowContent = PackWidgets.Scroll(list, out _);

            _ceremony = new DanAwardCeremony(_root, OnEquip, AnnounceUnlocks);
            _unlock = new AchievementUnlockPopup(_root, 18f);
            _root.gameObject.SetActive(false);
        }

        internal bool IsOpen => _root.gameObject.activeSelf;

        internal bool IsCeremonyOpen => _ceremony.IsOpen;

        /// <summary>Shows <paramref name="model"/>; <paramref name="animate"/> false shows where the opening settles (and no ceremony).</summary>
        internal void Show(DanResultModel model, bool animate = true)
        {
            _model = model;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            _unlock.Hide();
            _unlockPending = animate && model.UnlockedAchievementIds.Count > 0 && _announced.Add(model.Attempt.Id)
                && _unlock.Bind(model.UnlockedAchievementIds, null);
            Bind();
            _reveal.TryCancel();
            _revealDone = false;
            _revealLength = model.Definition.Songs.Count * ROW_STAGGER + ROW_DRAIN + 0.3f + VERDICT;
            if (!animate)
            {
                ApplyReveal(_revealLength);
                _revealDone = true;
                return;
            }

            ApplyReveal(0f);
            _reveal = LMotion.Create(0f, _revealLength, _revealLength)
                .WithScheduler(HomeMotion.Scheduler)
                .WithOnComplete(FinishReveal)
                .Bind(ApplyReveal);
        }

        /// <summary>Holds the opening at <paramref name="seconds"/>, for screenshots.</summary>
        internal void Seek(float seconds)
        {
            _reveal.TryCancel();
            ApplyReveal(Mathf.Min(seconds, _revealLength));
        }

        /// <summary>The ceremony for the shown pass at <paramref name="seconds"/> in, for screenshots.</summary>
        internal void SeekCeremony(float seconds)
        {
            _reveal.TryCancel();
            ApplyReveal(_revealLength);
            _revealDone = true;
            _ceremony.Show(_model, false);
            _ceremony.Seek(seconds);
        }

        /// <summary>The unlock card for <paramref name="ids"/> over the settled page, held at <paramref name="seconds"/> in, for screenshots.</summary>
        internal void SeekUnlock(IReadOnlyList<string> ids, float seconds)
        {
            _reveal.TryCancel();
            ApplyReveal(_revealLength);
            _revealDone = true;
            _unlockPending = false;
            if (_unlock.Bind(ids, null)) _unlock.Seek(seconds);
        }

        internal void Hide()
        {
            _unlockPending = false;
            _reveal.TryCancel();
            _ceremony.Hide();
            _unlock.Hide();
            _root.gameObject.SetActive(false);
        }

        /// <summary>Back: closes the ceremony first; false when the page itself should go.</summary>
        internal bool TryHandleBack()
        {
            if (_ceremony.IsOpen)
            {
                _ceremony.Hide();
                UiSound.Play(UiCue.Close);
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            _reveal.TryCancel();
            _plate.Dispose();
            _ceremony.Dispose();
            _unlock.Dispose();
        }

        private void Skip()
        {
            if (_revealDone || !_reveal.IsActive()) return;
            _reveal.TryCancel();
            ApplyReveal(_revealLength);
            FinishReveal();
        }

        private void FinishReveal()
        {
            if (_revealDone) return;
            _revealDone = true;
            if (_model == null) return;
            if (_model.Passed)
            {
                _ceremony.Show(_model, true);
            }
            else
            {
                UiSound.Play(_model.Attempt.Outcome == DanOutcome.Failed ? UiCue.Warning : UiCue.Close);
                AnnounceUnlocks();
            }
        }

        /// <summary>The unlock card, once: after the reveal, or after the award ceremony on a pass.</summary>
        private void AnnounceUnlocks()
        {
            if (!_unlockPending || !IsOpen) return;
            _unlockPending = false;
            _unlock.Play();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetAnnouncements() => _announced.Clear();

        private void OnEquip()
        {
            if (_model == null || _model.Equipped) return;
            if (_onEquip(_model.Definition))
            {
                _model.Equipped = true;
                BindAward();
                _ceremony.MarkEquipped();
            }
        }

        // ---------------------------------------------------------------- binding

        private void Bind()
        {
            DanResultModel model = _model;
            DanDefinition definition = model.Definition;
            DanAttempt attempt = model.Attempt;
            Color32 tint = DanStyles.ParseColor(definition.Color);
            _glow.color = HomeTheme.Colors.WithAlpha(tint, 46);
            _subtitle.text = DanCopy.When(attempt.StartedUtc) + "  ·  " + DanCopy.Shape(definition);
            _plate.SetStyle(definition.Label, definition.Emblem, definition.Color);
            _plate.SetLocked(!model.Passed);

            Color32 color = DanCopy.OutcomeColor(attempt.Outcome);
            _verdict.text = DanCopy.Outcome(attempt.Outcome);
            _verdict.color = color;
            float verdictWidth = Mathf.Ceil(_verdict.GetPreferredValues(_verdict.text).x);
            _verdictLatin.text = DanCopy.OutcomeLatin(attempt.Outcome);
            _verdictLatin.color = HomeTheme.Colors.WithAlpha(color, 200);
            _verdictLatin.rectTransform.anchoredPosition = new Vector2(verdictWidth + 24f, -14f);
            _reason.text = Reason(model);
            _lifeMax.text = "/ " + DanCopy.Number(definition.InitialLife);
            _error.text = model.Error != null ? "后续曲目没能开始：" + model.Error : string.Empty;
            int cleared = DanCopy.Cleared(attempt);
            _listSummary.text = "完成 " + cleared + " / " + definition.Songs.Count + " 首  ·  共 "
                + DanCopy.Number(TotalScore(attempt)) + " 分";
            BindAward();
            BindRows();
        }

        private void BindAward()
        {
            bool passed = _model.Passed;
            _award.gameObject.SetActive(passed);
            _equip.gameObject.SetActive(passed);
            if (!passed) return;
            _award.text = _model.NewlyAwarded ? "首次取得这块段位牌" : "再次通过 · 已拥有这块段位牌";
            ServerWidgets.SetLabelledButton(_equip, _model.Equipped ? "已佩戴" : "佩戴段位牌", _model.Equipped ? LibraryIcon.Check : LibraryIcon.Tag,
                LibraryTheme.Colors.Control, _model.Equipped ? DanCopy.Pass : LibraryTheme.Colors.TextPrimary);
            PackWidgets.SetEnabled(_equip, !_model.Equipped);
        }

        private void BindRows()
        {
            DanDefinition definition = _model.Definition;
            int count = definition.Songs.Count;
            while (_rows.Count < count)
            {
                var row = new Row(_rowContent, _rows.Count);
                row.Root.anchoredPosition = new Vector2(0f, -_rows.Count * (ROW_HEIGHT + ROW_GAP));
                _rows.Add(row);
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < count;
                _rows[i].Root.gameObject.SetActive(used);
                if (!used) continue;
                DanSongResult song = i < _model.Attempt.Songs.Count ? _model.Attempt.Songs[i] : null;
                DanSongInfo info = i < _model.Songs.Count ? _model.Songs[i] : null;
                _rows[i].Bind(i + 1, song, info, definition);
            }

            _rowContent.sizeDelta = new Vector2(0f, count * (ROW_HEIGHT + ROW_GAP));
        }

        private void ApplyReveal(float time)
        {
            if (_model == null) return;
            DanDefinition definition = _model.Definition;
            DanAttempt attempt = _model.Attempt;
            _leftGroup.alpha = HomeMotion.Span(time, 0f, 0.3f, Ease.OutQuad);
            float life = definition.InitialLife;
            for (int i = 0; i < _rows.Count && i < definition.Songs.Count; i++)
            {
                float start = 0.15f + i * ROW_STAGGER;
                float arrive = HomeMotion.Span(time, start, 0.32f, Ease.OutCubic);
                float drain = HomeMotion.Span(time, start + 0.12f, ROW_DRAIN, Ease.OutCubic);
                _rows[i].Apply(arrive, drain);
                if (i < attempt.Songs.Count)
                {
                    DanSongResult song = attempt.Songs[i];
                    if (drain > 0f) life = Mathf.Lerp(song.StartingLife, song.Score.life, drain);
                }
            }

            int shown = Mathf.RoundToInt(life);
            _life.text = DanCopy.Number(shown);
            float width = Mathf.Ceil(_life.GetPreferredValues(_life.text).x);
            _lifeMax.rectTransform.anchoredPosition = new Vector2(8f + width + 12f, -368f);
            float fraction = definition.InitialLife > 0 ? Mathf.Clamp01(life / definition.InitialLife) : 0f;
            _lifeFill.rectTransform.anchorMax = new Vector2(fraction, 1f);
            _lifeFill.color = DanCopy.LifeColor(shown, definition.InitialLife);
            _life.color = shown == 0 ? LibraryTheme.Colors.Danger : LibraryTheme.Colors.TextPrimary;

            float verdictStart = _revealLength - VERDICT;
            float verdict = HomeMotion.Span(time, verdictStart, VERDICT, Ease.OutBack);
            _verdictGroup.alpha = HomeMotion.Span(time, verdictStart, VERDICT * 0.5f, Ease.OutQuad);
            float scale = Mathf.LerpUnclamped(1.5f, 1f, verdict);
            _verdictRoot.localScale = new Vector3(scale, scale, 1f);
            float tail = HomeMotion.Span(time, verdictStart, VERDICT, Ease.OutQuad);
            _reason.alpha = tail;
            _award.alpha = tail;
        }

        private static string Reason(DanResultModel model)
        {
            DanAttempt attempt = model.Attempt;
            int count = model.Definition.Songs.Count;
            switch (attempt.Outcome)
            {
                case DanOutcome.Passed:
                    return "完成全部 " + count + " 首";
                case DanOutcome.Failed:
                {
                    DanSongResult last = attempt.Songs[attempt.Songs.Count - 1];
                    return "第 " + attempt.Songs.Count + " 首「" + last.Title + "」生命耗尽  ·  " + DanCopy.Clock(last.PositionMs);
                }
                default:
                {
                    if (attempt.Songs.Count > 0 && attempt.Songs[attempt.Songs.Count - 1].Kind != PlayResultKind.Completed)
                    {
                        DanSongResult last = attempt.Songs[attempt.Songs.Count - 1];
                        return "第 " + attempt.Songs.Count + " 首中途退出  ·  " + DanCopy.Clock(last.PositionMs);
                    }

                    return model.Error != null
                        ? "完成 " + attempt.Songs.Count + " / " + count + " 首后中断"
                        : "完成 " + attempt.Songs.Count + " / " + count + " 首后退出挑战";
                }
            }
        }

        private static long TotalScore(DanAttempt attempt)
        {
            long total = 0;
            foreach (DanSongResult song in attempt.Songs) total += song.Score.totalScore;
            return total;
        }

        /// <summary>One song of the attempt: how it ended, its score, its judgments and its life bar.</summary>
        private sealed class Row
        {
            private readonly CanvasGroup _group;
            private readonly Image _background;
            private readonly Image _disc;
            private readonly TextMeshProUGUI _number;
            private readonly TextMeshProUGUI _title;
            private readonly TextMeshProUGUI _meta;
            private readonly TextMeshProUGUI _score;
            private readonly TextMeshProUGUI _change;
            private readonly TextMeshProUGUI[] _judges = new TextMeshProUGUI[5];
            private readonly RectTransform _track;
            private readonly Image _lost;
            private readonly Image _kept;
            private float _start;
            private float _end;
            private bool _played;

            internal Row(RectTransform content, int index)
            {
                Root = LibraryUi.CreateRect("Song" + index, content);
                Root.anchorMin = new Vector2(0f, 1f);
                Root.anchorMax = new Vector2(1f, 1f);
                Root.pivot = new Vector2(0.5f, 1f);
                Root.sizeDelta = new Vector2(0f, ROW_HEIGHT);
                RectTransform inner = LibraryUi.CreateRect("Inner", Root);
                LibraryUi.Stretch(inner);
                _group = inner.gameObject.AddComponent<CanvasGroup>();
                _background = LibraryWidgets.Panel("Background", inner, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Control);
                LibraryUi.Stretch(_background.rectTransform);
                _disc = LibraryWidgets.Panel("Disc", inner, 26f, HomeTheme.Colors.Chip);
                LibraryWidgets.Place(_disc.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(52f, 52f));
                _number = LibraryWidgets.Label("Number", _disc.transform, "1", 28f, LibraryTheme.Colors.TextPrimary, FontStyles.Normal,
                    TextAlignmentOptions.Center);
                LibraryUi.UseCondensedFont(_number);
                _number.overflowMode = TextOverflowModes.Overflow;
                LibraryUi.Stretch(_number.rectTransform);

                _title = PackWidgets.UserLabel("Title", inner, string.Empty, 24f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
                LibraryWidgets.TopStrip(_title.rectTransform, 92f, 580f, 16f, 34f);
                _meta = PackWidgets.UserLabel("Meta", inner, string.Empty, 17f, LibraryTheme.Colors.TextMuted);
                _meta.richText = true;
                LibraryWidgets.TopStrip(_meta.rectTransform, 92f, 580f, 52f, 26f);

                // Life bar under the title: the full width is the course's starting life.
                Image track = LibraryWidgets.Panel("Track", inner, 4f, HomeTheme.Colors.Chip);
                _track = track.rectTransform;
                LibraryWidgets.TopStrip(_track, 92f, 580f, 86f, 8f);
                _lost = LibraryWidgets.Panel("Lost", _track, 4f, HomeTheme.Colors.WithAlpha(LibraryTheme.Colors.Danger, 150));
                _kept = LibraryWidgets.Panel("Kept", _track, 4f, DanCopy.Life);
                foreach (Image part in new[] { _lost, _kept })
                {
                    part.rectTransform.anchorMin = Vector2.zero;
                    part.rectTransform.anchorMax = new Vector2(0f, 1f);
                    part.rectTransform.offsetMin = Vector2.zero;
                    part.rectTransform.offsetMax = Vector2.zero;
                }

                RectTransform judges = LibraryUi.CreateRect("Judges", inner);
                LibraryWidgets.Place(judges, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-230f, 0f), new Vector2(330f, 70f));
                for (int i = 0; i < 5; i++)
                {
                    RectTransform cell = LibraryUi.CreateRect(DanCopy.JudgeNames[i], judges);
                    cell.anchorMin = new Vector2(i / 5f, 0f);
                    cell.anchorMax = new Vector2((i + 1) / 5f, 1f);
                    cell.offsetMin = Vector2.zero;
                    cell.offsetMax = Vector2.zero;
                    TextMeshProUGUI name = LibraryWidgets.Label("Name", cell, DanCopy.JudgeNames[i], 13f, DanCopy.JudgeColors[i],
                        FontStyles.Bold, TextAlignmentOptions.Center);
                    LibraryUi.UseCondensedFont(name);
                    name.overflowMode = TextOverflowModes.Overflow;
                    LibraryWidgets.TopStrip(name.rectTransform, 0f, 0f, 6f, 20f);
                    _judges[i] = LibraryWidgets.Label("Value", cell, "0", 24f, LibraryTheme.Colors.TextSecondary, FontStyles.Normal,
                        TextAlignmentOptions.Center);
                    LibraryUi.UseCondensedFont(_judges[i]);
                    _judges[i].overflowMode = TextOverflowModes.Overflow;
                    LibraryWidgets.Inset(_judges[i].rectTransform, 0f, 4f, 0f, 26f);
                }

                _score = LibraryWidgets.Label("Score", inner, string.Empty, 40f, LibraryTheme.Colors.TextPrimary, FontStyles.Normal,
                    TextAlignmentOptions.BottomRight);
                LibraryUi.UseCondensedFont(_score);
                _score.overflowMode = TextOverflowModes.Overflow;
                LibraryWidgets.Place(_score.rectTransform, Vector2.one, Vector2.one, new Vector2(-22f, -10f), new Vector2(200f, 52f));
                _change = LibraryWidgets.Label("Change", inner, string.Empty, 18f, LibraryTheme.Colors.TextMuted, FontStyles.Normal,
                    TextAlignmentOptions.TopRight);
                _change.richText = true;
                LibraryWidgets.Place(_change.rectTransform, Vector2.one, Vector2.one, new Vector2(-22f, -64f), new Vector2(200f, 30f));
            }

            internal RectTransform Root { get; }

            internal void Bind(int number, DanSongResult song, DanSongInfo info, DanDefinition definition)
            {
                _number.text = number.ToString(CultureInfo.InvariantCulture);
                _played = song != null;
                string title = song?.Title ?? info?.Title ?? "第 " + number + " 首";
                _title.text = title;
                _title.color = _played ? LibraryTheme.Colors.TextPrimary : LibraryTheme.Colors.TextMuted;
                string difficulty = info?.Difficulty != null ? info.Difficulty + "  ·  " : string.Empty;
                if (!_played)
                {
                    _meta.text = difficulty + "未开始";
                    _score.text = "—";
                    _score.color = LibraryTheme.Colors.TextMuted;
                    _change.text = string.Empty;
                    for (int i = 0; i < 5; i++) _judges[i].text = "—";
                    _disc.color = HomeTheme.Colors.Chip;
                    _background.color = HomeTheme.Colors.WithAlpha(Color.white, 6);
                    _start = 0f;
                    _end = 0f;
                    _track.gameObject.SetActive(false);
                    return;
                }

                Color32 endColor = DanCopy.SongEndColor(song.Kind);
                string end = DanCopy.SongEnd(song.Kind);
                if (song.Kind != PlayResultKind.Completed) end += " " + DanCopy.Clock(song.PositionMs);
                _meta.text = difficulty + "<color=#" + ColorUtility.ToHtmlStringRGB(endColor) + ">" + end + "</color>  ·  最大连击 "
                    + DanCopy.Number(song.Score.maxCombo);
                _score.text = DanCopy.Number(song.Score.totalScore);
                _score.color = LibraryTheme.Colors.TextPrimary;
                int lost = song.StartingLife - song.Score.life;
                _change.text = DanCopy.Number(song.StartingLife) + " → " + DanCopy.Number(song.Score.life)
                    + (lost > 0 ? "  <color=#F58080>−" + DanCopy.Number(lost) + "</color>" : "  <color=#7BD88F>±0</color>");
                for (int i = 0; i < 5; i++)
                {
                    int value = DanCopy.Judgments(song, i);
                    _judges[i].text = DanCopy.Number(value);
                    _judges[i].color = value > 0 && i >= 3 ? LibraryTheme.Colors.TextPrimary : LibraryTheme.Colors.TextSecondary;
                }

                _disc.color = song.Kind == PlayResultKind.Completed ? HomeTheme.Colors.WithAlpha(DanCopy.Life, 50)
                    : HomeTheme.Colors.WithAlpha(endColor, 70);
                _background.color = song.Kind == PlayResultKind.Completed ? LibraryTheme.Colors.Control
                    : HomeTheme.Colors.WithAlpha(endColor, 22);
                float initial = Mathf.Max(1, definition.InitialLife);
                _start = song.StartingLife / initial;
                _end = song.Score.life / initial;
                _track.gameObject.SetActive(true);
            }

            /// <param name="arrive">0 to 1: the row slides and fades in.</param>
            /// <param name="drain">0 to 1: the life bar falls from where the song started to where it ended.</param>
            internal void Apply(float arrive, float drain)
            {
                _group.alpha = _played ? arrive : arrive * 0.7f;
                ((RectTransform)_group.transform).anchoredPosition = new Vector2(48f * (1f - arrive), 0f);
                if (!_played) return;
                float now = Mathf.Lerp(_start, _end, drain);
                _lost.rectTransform.anchorMax = new Vector2(_start, 1f);
                _kept.rectTransform.anchorMax = new Vector2(now, 1f);
            }
        }

        /// <summary>A tap anywhere on the page skips the opening.</summary>
        private sealed class SkipArea : MonoBehaviour, IPointerClickHandler
        {
            internal DanResultPage Owner { get; set; }

            public void OnPointerClick(PointerEventData eventData) => Owner?.Skip();
        }
    }
}
