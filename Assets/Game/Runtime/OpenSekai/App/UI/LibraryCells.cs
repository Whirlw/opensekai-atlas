using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using OpenSekai.App.Library;
using OpenSekai.Infrastructure.Packages;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// One pooled cell of the library list. The list positions <see cref="Root"/>; everything that
    /// animates (selection, press, entrance) moves <see cref="Body"/> inside it, so the two never
    /// fight over the same transform.
    /// </summary>
    internal abstract class LibraryCell
    {
        private const float ENTER_OFFSET = 14f;
        private const float UNPLAYABLE_ALPHA = 0.6f;

        private CancellationTokenSource _coverLoad;
        private MotionHandle _enterMotion;
        private bool _playable;

        protected LibraryCell(RectTransform parent, string name, Action<LibraryCell> onClick)
        {
            Root = LibraryUi.CreateRect(name, parent);
            Body = LibraryUi.CreateRect("Body", Root);
            LibraryUi.Stretch(Body);
            Group = Body.gameObject.AddComponent<CanvasGroup>();

            Image hit = Root.gameObject.AddComponent<Image>();
            hit.color = new Color32(0, 0, 0, 0);
            Button button = Root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick(this));
            LibraryPressFeedback.Attach(Root.gameObject, Body);
        }

        internal RectTransform Root { get; }
        internal SongPackageItem Item { get; private set; }
        protected RectTransform Body { get; }
        protected CanvasGroup Group { get; }
        protected bool Selected { get; private set; }
        protected JacketPalette Palette { get; private set; } = JacketPalette.Default;

        /// <summary>The image that shows the jacket; disabled while there is none to show.</summary>
        protected abstract Image Cover { get; }

        internal void Bind(
            SongPackageItem item, CustomMusicScorePlayHistorySummary summary, bool selected, JacketThumbnails thumbnails)
        {
            CancelCover();
            KillEnter();
            Item = item;
            Selected = selected;
            _playable = LibraryModel.IsPlayable(item);
            Palette = JacketPalette.Default;
            Cover.enabled = false;
            Cover.sprite = null;
            OnBind(item, summary);
            ApplySelection(false);
            Group.alpha = RestAlpha;

            if (!item.HasJacket || item.Entry == null)
            {
                return;
            }

            string jacketPath = item.Entry.JacketPath;
            long version = item.LastWriteTime.Ticks;
            if (thumbnails.TryGetLoaded(jacketPath, version, out JacketThumbnail loaded))
            {
                ShowThumbnail(loaded);
                return;
            }

            _coverLoad = new CancellationTokenSource();
            LoadCoverAsync(thumbnails, jacketPath, version, _coverLoad.Token).Forget();
        }

        /// <summary>
        /// Redraws the labels for a new play-history summary or Ranked verdict; the cover and motion
        /// are left alone.
        /// </summary>
        internal void UpdateLabels(CustomMusicScorePlayHistorySummary summary)
        {
            if (Item != null)
            {
                OnBind(Item, summary);
            }
        }

        internal void SetSelected(bool selected, bool animate)
        {
            if (Selected == selected)
            {
                return;
            }

            Selected = selected;
            ApplySelection(animate);
            Group.alpha = RestAlpha;
        }

        /// <summary>Fades and slides the cell in; used once when a new list appears, never while scrolling.</summary>
        internal void PlayEnter(float delay)
        {
            KillEnter();
            Group.alpha = 0f;
            Body.anchoredPosition = new Vector2(Body.anchoredPosition.x, -ENTER_OFFSET);
            _enterMotion = LSequence.Create()
                .AppendInterval(delay)
                .Append(LMotion.Create(Group.alpha, RestAlpha, LibraryTheme.Motion.STANDARD)
                    .WithEase(LibraryTheme.Motion.FADE_EASE)
                    .BindToAlpha(Group))
                .Join(LMotion.Create(Body.anchoredPosition.y, 0f, LibraryTheme.Motion.STANDARD)
                    .WithEase(LibraryTheme.Motion.ENTER_EASE)
                    .BindToAnchoredPositionY(Body))
                .Run();
        }

        /// <summary>Stops everything in flight before the cell is pooled or destroyed.</summary>
        internal void Release()
        {
            CancelCover();
            KillEnter();
            KillSelection();
            Cover.sprite = null;
            Cover.enabled = false;
            Item = null;
        }

        protected abstract void OnBind(SongPackageItem item, CustomMusicScorePlayHistorySummary summary);

        /// <summary>Shows the selected or unselected look, using <see cref="Palette"/> for the accent.</summary>
        protected abstract void ApplySelection(bool animate);

        protected abstract void KillSelection();

        private float RestAlpha => _playable || Selected ? 1f : UNPLAYABLE_ALPHA;

        private async UniTaskVoid LoadCoverAsync(
            JacketThumbnails thumbnails, string jacketPath, long version, CancellationToken cancellationToken)
        {
            try
            {
                JacketThumbnail thumbnail = await thumbnails.LoadAsync(jacketPath, version, cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    ShowThumbnail(thumbnail);
                }
            }
            catch (OperationCanceledException)
            {
                // The cell was recycled or the page closed before the jacket arrived.
            }
        }

        private void ShowThumbnail(JacketThumbnail thumbnail)
        {
            Cover.sprite = thumbnail.Sprite;
            Cover.enabled = thumbnail.Sprite != null;
            Palette = thumbnail.Palette;
            ApplySelection(false);
        }

        private void CancelCover()
        {
            if (_coverLoad == null)
            {
                return;
            }

            _coverLoad.Cancel();
            _coverLoad.Dispose();
            _coverLoad = null;
        }

        private void KillEnter()
        {
            _enterMotion.TryCancel();
            _enterMotion = default;
            Body.anchoredPosition = new Vector2(Body.anchoredPosition.x, 0f);
        }

        /// <summary>Rounded frame that clips a jacket; its own color is the placeholder.</summary>
        protected static Image CreateCover(Transform parent, float radius, out Image cover)
        {
            Image frame = LibraryWidgets.Panel("Cover", parent, radius, LibraryTheme.Colors.CoverPlaceholder);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            cover = LibraryUi.CreateImage("Jacket", frame.transform, Color.white);
            cover.raycastTarget = false;
            cover.enabled = false;
            LibraryUi.Stretch(cover.rectTransform);
            return frame;
        }
    }

    /// <summary>
    /// Full-width row: jacket, title and credits, then from the right edge the level, the
    /// difficulty chip, the RANKED tag and the AP / FC badge.
    /// </summary>
    internal sealed class LibraryRowCell : LibraryCell
    {
        private const float PADDING = 14f;
        private const float LEVEL_WIDTH = 52f;
        private const float CHIP_HEIGHT = 32f;
        private const float CHIP_PADDING = 12f;
        private const float CHIP_MAX_WIDTH = 104f;
        private const float LABEL_SPACING = 1.5f;
        private const float COLUMN_GAP = 20f;
        private const float TAG_GAP = 10f;
        private static readonly Color32 _rankedBackground = new Color32(255, 210, 122, 22);

        private readonly Image _background;
        private readonly Image _shadow;
        private readonly Image _ring;
        private readonly Image _cover;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _meta;
        private readonly LibraryAwardBadge _award;
        private readonly LibraryRankedTag _ranked;
        private readonly TextMeshProUGUI _difficulty;
        private readonly RectTransform _difficultyChip;
        private readonly TextMeshProUGUI _level;
        private MotionHandle _shiftMotion;

        internal LibraryRowCell(RectTransform parent, Action<LibraryCell> onClick) : base(parent, "Row", onClick)
        {
            _background = LibraryWidgets.Panel("Background", Body, LibraryTheme.Radius.ROW, LibraryTheme.Colors.Surface);
            LibraryUi.Stretch(_background.rectTransform);
            _shadow = LibraryWidgets.Shadow(Body, LibraryTheme.Radius.ROW, 24f, 8f, 100);
            _ring = LibraryWidgets.Shape(
                "Ring", Body, LibraryShapes.Ring(LibraryTheme.Radius.ROW, 2f), LibraryTheme.Colors.DefaultAccent);
            LibraryUi.Stretch(_ring.rectTransform);

            float coverSize = LibraryTheme.Layout.ROW_COVER_SIZE;
            Image frame = CreateCover(Body, LibraryTheme.Radius.COVER_SMALL, out _cover);
            LibraryWidgets.Place(
                frame.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(PADDING, 0f), new Vector2(coverSize, coverSize));

            _level = LibraryWidgets.Label(
                "Level", Body, string.Empty, 44f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold, TextAlignmentOptions.Right);
            LibraryUi.UseCondensedFont(_level);
            _level.overflowMode = TextOverflowModes.Overflow;
            LibraryWidgets.Place(
                _level.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-LibraryTheme.Spacing.L, 0f), new Vector2(LEVEL_WIDTH, 52f));

            float chipRight = LibraryTheme.Spacing.L + LEVEL_WIDTH + COLUMN_GAP;
            Image chip = LibraryWidgets.Panel("Difficulty", Body, LibraryTheme.Radius.CHIP, new Color32(255, 255, 255, 18));
            _difficultyChip = chip.rectTransform;
            LibraryWidgets.Place(
                _difficultyChip, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-chipRight, 0f), new Vector2(CHIP_MAX_WIDTH, CHIP_HEIGHT));
            _difficulty = LibraryWidgets.Label(
                "Label", chip.transform, string.Empty, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextSecondary,
                FontStyles.Bold, TextAlignmentOptions.Center);
            LibraryUi.UseSpacedCondensedFont(_difficulty, LABEL_SPACING * 100f / _difficulty.fontSize);
            LibraryWidgets.Inset(_difficulty.rectTransform, CHIP_PADDING, 0f, CHIP_PADDING, 0f);

            _ranked = new LibraryRankedTag(Body, CHIP_HEIGHT, 16f, _rankedBackground);
            _award = new LibraryAwardBadge(Body, CHIP_HEIGHT, LibraryTheme.Radius.CHIP);

            _title = LibraryWidgets.Label(
                "Title", Body, string.Empty, LibraryTheme.FontSize.ROW_TITLE, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            _meta = LibraryWidgets.Label(
                "Meta", Body, string.Empty, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted);
        }

        protected override Image Cover => _cover;

        protected override void OnBind(SongPackageItem item, CustomMusicScorePlayHistorySummary summary)
        {
            _title.text = LibraryModel.BuildTitle(item);
            _meta.text = LibraryModel.BuildMeta(item);
            _meta.color = LibraryModel.IsPlayable(item) ? LibraryTheme.Colors.TextMuted : LibraryTheme.Colors.Warning;
            _award.Show(LibraryModel.ClearBadge(summary));
            _ranked.Show(LibraryModel.IsRanked(item));
            _difficulty.text = LibraryModel.DifficultyLabel(item);
            _difficulty.color = LibraryTheme.Colors.Difficulty(item.Entry?.Manifest?.musicDifficultyType);
            int level = LibraryModel.PlayLevelOf(item);
            _level.text = level > 0 ? level.ToString() : "—";
            LayoutLabels();
        }

        private void LayoutLabels()
        {
            float chipWidth = Mathf.Min(CHIP_MAX_WIDTH,
                Mathf.Ceil(_difficulty.GetPreferredValues().x) + CHIP_PADDING * 2f);
            _difficultyChip.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, chipWidth);

            // Tags line up leftward from the chip: RANKED first, then AP / FC.
            float right = -_difficultyChip.anchoredPosition.x + chipWidth;
            Vector2 anchor = new Vector2(1f, 0.5f);
            if (_ranked.Visible)
            {
                right += TAG_GAP;
                LibraryWidgets.Place(_ranked.Root, anchor, anchor, new Vector2(-right, 0f), _ranked.Root.sizeDelta);
                right += _ranked.Width;
            }

            if (_award.Visible)
            {
                right += TAG_GAP;
                LibraryWidgets.Place(_award.Root, anchor, anchor, new Vector2(-right, 0f), _award.Root.sizeDelta);
                right += _award.Width;
            }

            float textRight = right + COLUMN_GAP;

            float textLeft = PADDING + LibraryTheme.Layout.ROW_COVER_SIZE + COLUMN_GAP;
            LibraryWidgets.TopStrip(_title.rectTransform, textLeft, textRight, 16f, 36f);
            LibraryWidgets.BottomStrip(_meta.rectTransform, textLeft, textRight, 16f, 26f);
        }

        protected override void ApplySelection(bool animate)
        {
            _background.color = Selected ? LibraryTheme.Colors.SurfaceSelected : LibraryTheme.Colors.Surface;
            _shadow.enabled = Selected;
            _ring.sprite = LibraryShapes.Ring(LibraryTheme.Radius.ROW, Selected ? 2f : 1f);
            _ring.color = Selected ? Palette.Accent : new Color32(255, 255, 255, 13);
            _level.color = Selected ? Palette.Accent : LibraryTheme.Colors.TextPrimary;

            KillSelection();
            float shift = Selected ? LibraryTheme.Motion.SELECTED_ROW_SHIFT : 0f;
            if (animate)
            {
                _shiftMotion = LMotion.Create(Body.anchoredPosition.x, shift, LibraryTheme.Motion.STANDARD)
                    .WithEase(LibraryTheme.Motion.SELECT_EASE)
                    .BindToAnchoredPositionX(Body);
            }
            else
            {
                Body.anchoredPosition = new Vector2(shift, Body.anchoredPosition.y);
            }
        }

        protected override void KillSelection()
        {
            _shiftMotion.TryCancel();
            _shiftMotion = default;
        }
    }

    /// <summary>
    /// Grid tile: a square jacket with RANKED in its top-left corner, AP / FC in its bottom-left
    /// corner, difficulty and level in its bottom-right corner, and the title under it.
    /// </summary>
    internal sealed class LibraryTileCell : LibraryCell
    {
        private const float CORNER = 10f;
        private const float RING_WIDTH = 4f;
        private const float COVER_RADIUS = 18f;
        private const float CHIP_HEIGHT = 32f;
        private const float CHIP_RADIUS = 9f;
        private const float CHIP_PADDING = 10f;
        private const float CHIP_GAP = 8f;
        private const float CHIP_MAX_WIDTH = 126f;
        private const float MISSING_PADDING = 12f;
        private const float MISSING_MAX_WIDTH = 120f;
        private static readonly Color32 _cornerBackground = new Color32(12, 15, 20, 220);

        private readonly RectTransform _holder;
        private readonly Image _shadow;
        private readonly Image _ring;
        private readonly Image _cover;
        private readonly RectTransform _corner;
        private readonly TextMeshProUGUI _difficulty;
        private readonly TextMeshProUGUI _level;
        private readonly LibraryAwardBadge _award;
        private readonly LibraryRankedTag _ranked;
        private readonly RectTransform _missing;
        private readonly TextMeshProUGUI _missingLabel;
        private readonly TextMeshProUGUI _title;
        private MotionHandle _scaleMotion;

        internal LibraryTileCell(RectTransform parent, Action<LibraryCell> onClick) : base(parent, "Tile", onClick)
        {
            _holder = LibraryUi.CreateRect("Holder", Body);
            LibraryWidgets.Inset(_holder, 0f, LibraryTheme.Layout.GRID_TITLE_HEIGHT, 0f, 0f);
            _shadow = LibraryWidgets.Shadow(_holder, COVER_RADIUS, 28f, 10f, 135);

            // The ring sits just outside the jacket so it never covers the art.
            _ring = LibraryWidgets.Shape(
                "Ring", _holder, LibraryShapes.Ring(COVER_RADIUS + RING_WIDTH, RING_WIDTH), LibraryTheme.Colors.DefaultAccent);
            LibraryWidgets.Inset(_ring.rectTransform, -RING_WIDTH, -RING_WIDTH, -RING_WIDTH, -RING_WIDTH);

            Image frame = CreateCover(_holder, COVER_RADIUS, out _cover);
            LibraryUi.Stretch(frame.rectTransform);

            Image corner = LibraryWidgets.Panel("Difficulty", _holder, CHIP_RADIUS, _cornerBackground);
            _corner = corner.rectTransform;
            LibraryWidgets.Place(
                _corner, Vector2.right, Vector2.right, new Vector2(-CORNER, CORNER), new Vector2(CHIP_MAX_WIDTH, CHIP_HEIGHT));
            _difficulty = LibraryWidgets.Label(
                "Label", corner.transform, string.Empty, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextSecondary,
                FontStyles.Bold);
            LibraryUi.UseSpacedCondensedFont(_difficulty, 0f);
            _level = LibraryWidgets.Label(
                "Level", corner.transform, string.Empty, 21f, LibraryTheme.Colors.TextPrimary,
                FontStyles.Bold, TextAlignmentOptions.Right);
            LibraryUi.UseCondensedFont(_level);

            // Pinned by its corner, so it stays put when the badge resizes to its letters.
            _award = new LibraryAwardBadge(_holder, CHIP_HEIGHT, CHIP_RADIUS);
            LibraryWidgets.Place(_award.Root, Vector2.zero, Vector2.zero, new Vector2(CORNER, CORNER), _award.Root.sizeDelta);
            _ranked = new LibraryRankedTag(_holder, 28f, 15f, _cornerBackground);
            LibraryWidgets.Place(_ranked.Root, Vector2.up, Vector2.up, new Vector2(CORNER, -CORNER), _ranked.Root.sizeDelta);

            Image missing = LibraryWidgets.Panel("Missing", _holder, 14f, _cornerBackground);
            _missing = missing.rectTransform;
            LibraryWidgets.Place(_missing, Vector2.one, Vector2.one, new Vector2(-CORNER, -CORNER), new Vector2(96f, 28f));
            _missingLabel = LibraryWidgets.Label(
                "Label", missing.transform, string.Empty, 15f, LibraryTheme.Colors.Warning,
                FontStyles.Normal, TextAlignmentOptions.Center);
            LibraryUi.Stretch(_missingLabel.rectTransform);

            _title = LibraryWidgets.Label(
                "Title", Body, string.Empty, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.BottomStrip(_title.rectTransform, 2f, 2f, 0f, 28f);
        }

        protected override Image Cover => _cover;

        protected override void OnBind(SongPackageItem item, CustomMusicScorePlayHistorySummary summary)
        {
            _title.text = LibraryModel.BuildTitle(item);
            _difficulty.text = LibraryModel.DifficultyLabel(item);
            _difficulty.color = LibraryTheme.Colors.Difficulty(item.Entry?.Manifest?.musicDifficultyType);
            int level = LibraryModel.PlayLevelOf(item);
            _level.text = level > 0 ? level.ToString() : "—";
            _ranked.Show(LibraryModel.IsRanked(item));
            _award.Show(LibraryModel.ClearBadge(summary));

            bool missing = !LibraryModel.IsPlayable(item);
            _missing.gameObject.SetActive(missing);
            _missingLabel.text = missing ? item.StatusText : string.Empty;
            if (missing)
            {
                float width = Mathf.Ceil(_missingLabel.GetPreferredValues(_missingLabel.text).x) + MISSING_PADDING * 2f;
                _missing.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(MISSING_MAX_WIDTH, width));
            }

            LayoutCorner();
        }

        /// <summary>Sizes the difficulty chip to its text.</summary>
        private void LayoutCorner()
        {
            float levelWidth = Mathf.Ceil(_level.GetPreferredValues(_level.text).x);
            float difficultyWidth = Mathf.Ceil(_difficulty.GetPreferredValues(_difficulty.text).x);
            float width = Mathf.Min(CHIP_MAX_WIDTH, CHIP_PADDING * 2f + difficultyWidth + CHIP_GAP + levelWidth);
            _corner.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            LibraryWidgets.Inset(_difficulty.rectTransform, CHIP_PADDING, 0f, CHIP_PADDING + levelWidth + CHIP_GAP, 0f);
            LibraryWidgets.Inset(_level.rectTransform, width - CHIP_PADDING - levelWidth, 0f, CHIP_PADDING, 0f);
        }

        protected override void ApplySelection(bool animate)
        {
            _shadow.enabled = Selected;
            float stroke = Selected ? RING_WIDTH : 1f;
            _ring.sprite = LibraryShapes.Ring(COVER_RADIUS + stroke, stroke);
            LibraryWidgets.Inset(_ring.rectTransform, -stroke, -stroke, -stroke, -stroke);
            _ring.color = Selected ? Palette.Accent : new Color32(255, 255, 255, 15);
            _title.color = Selected ? Palette.Accent : LibraryTheme.Colors.TextPrimary;

            KillSelection();
            float scale = Selected ? LibraryTheme.Motion.SELECTED_TILE_SCALE : 1f;
            if (animate)
            {
                _scaleMotion = LMotion.Create(_holder.localScale, Vector3.one * scale, LibraryTheme.Motion.STANDARD)
                    .WithEase(LibraryTheme.Motion.SELECT_EASE)
                    .BindToLocalScale(_holder);
            }
            else
            {
                _holder.localScale = new Vector3(scale, scale, 1f);
            }
        }

        protected override void KillSelection()
        {
            _scaleMotion.TryCancel();
            _scaleMotion = default;
        }
    }
}
