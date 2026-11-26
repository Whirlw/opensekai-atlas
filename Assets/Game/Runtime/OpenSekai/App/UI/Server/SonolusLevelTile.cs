using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using OpenSekai.Infrastructure.Sonolus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// One level as a tile: the cover with its level in the corner (and a mark when it is already
    /// in the library), title and artists under it. The library's grid tile is the model; this
    /// one reads a <see cref="SonolusLevel"/> and loads its cover from the server.
    /// </summary>
    internal sealed class SonolusLevelTile
    {
        /// <summary>Height under the cover for the two text lines.</summary>
        internal const float TEXT_HEIGHT = 74f;

        private const float COVER_RADIUS = 18f;
        private const float CORNER = 10f;
        private const float ENTER_OFFSET = 16f;
        private static readonly Color32 _cornerBackground = new Color32(12, 15, 20, 220);

        private readonly RectTransform _holder;
        private readonly Image _cover;
        private readonly Image _ring;
        private readonly TextMeshProUGUI _level;
        private readonly RectTransform _levelChip;
        private readonly GameObject _installed;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _artists;
        private readonly CanvasGroup _group;
        private CancellationTokenSource _coverLoad;
        private MotionHandle _enter;

        internal SonolusLevelTile(RectTransform parent, Action<SonolusLevelTile> onClick)
        {
            Root = LibraryUi.CreateRect("Level", parent);
            Body = LibraryUi.CreateRect("Body", Root);
            LibraryUi.Stretch(Body);
            _group = Body.gameObject.AddComponent<CanvasGroup>();
            Image hit = Root.gameObject.AddComponent<Image>();
            hit.color = new Color32(0, 0, 0, 0);
            Button button = Root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick(this));
            LibraryPressFeedback.Attach(Root.gameObject, Body);

            _holder = LibraryUi.CreateRect("Holder", Body);
            LibraryWidgets.Inset(_holder, 0f, TEXT_HEIGHT, 0f, 0f);
            LibraryWidgets.Shadow(_holder, COVER_RADIUS, LibraryTheme.CoverShadow.BLUR, LibraryTheme.CoverShadow.DROP, LibraryTheme.CoverShadow.OPACITY);
            Image frame = LibraryWidgets.Panel("Cover", _holder, COVER_RADIUS, LibraryTheme.Colors.CoverPlaceholder);
            LibraryUi.Stretch(frame.rectTransform);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Image placeholder = LibraryWidgets.Icon(frame.transform, LibraryIcon.Wave, LibraryTheme.Colors.SurfaceSelected, 64f);
            placeholder.name = "Placeholder";
            _cover = LibraryUi.CreateImage("Jacket", frame.transform, Color.white);
            _cover.raycastTarget = false;
            _cover.enabled = false;
            LibraryUi.Stretch(_cover.rectTransform);
            _ring = LibraryWidgets.Shape("Ring", _holder, LibraryShapes.Ring(COVER_RADIUS, 1f), new Color32(255, 255, 255, 15));
            LibraryUi.Stretch(_ring.rectTransform);

            Image chip = LibraryWidgets.Panel("Level", _holder, 9f, _cornerBackground);
            _levelChip = chip.rectTransform;
            LibraryWidgets.Place(_levelChip, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(CORNER, -CORNER), new Vector2(84f, 32f));
            _level = LibraryWidgets.Label("Label", chip.transform, string.Empty, 21f, LibraryTheme.Colors.TextPrimary,
                FontStyles.Bold, TextAlignmentOptions.Center);
            LibraryUi.UseSpacedCondensedFont(_level, 4f);
            LibraryUi.Stretch(_level.rectTransform);

            Image installed = LibraryWidgets.Panel("Installed", _holder, 16f, _cornerBackground);
            LibraryWidgets.Place(installed.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-CORNER, -CORNER), new Vector2(32f, 32f));
            LibraryWidgets.Icon(installed.transform, LibraryIcon.Check, new Color32(123, 216, 143, 255), 22f);
            _installed = installed.gameObject;

            _title = LibraryWidgets.Label("Title", Body, string.Empty, LibraryTheme.FontSize.BODY, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.BottomStrip(_title.rectTransform, 2f, 2f, 30f, 30f);
            _artists = LibraryWidgets.Label("Artists", Body, string.Empty, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.BottomStrip(_artists.rectTransform, 2f, 2f, 4f, 24f);
        }

        internal RectTransform Root { get; }
        internal RectTransform Body { get; }
        internal SonolusLevel Level { get; private set; }

        internal void Bind(SonolusSession session, SonolusLevel level, RemoteCovers covers, bool installed)
        {
            CancelCover();
            KillEnter();
            Level = level;
            _title.text = level.Title;
            _artists.text = string.IsNullOrEmpty(level.Artists) ? level.Author : level.Artists;
            _level.text = "LV " + level.RatingText;
            float width = Mathf.Ceil(_level.GetPreferredValues(_level.text).x) + 24f;
            _levelChip.sizeDelta = new Vector2(Mathf.Max(64f, width), 32f);
            _installed.SetActive(installed);
            _cover.enabled = false;
            _cover.sprite = null;
            _group.alpha = 1f;

            if (level.Cover == null)
            {
                return;
            }

            if (covers.TryGetLoaded(session.Address, level.Cover, out JacketThumbnail loaded))
            {
                ShowCover(loaded);
                return;
            }

            _coverLoad = new CancellationTokenSource();
            LoadCoverAsync(session, level, covers, _coverLoad.Token).Forget();
        }

        internal void SetInstalled(bool installed) => _installed.SetActive(installed);

        internal void PlayEnter(float delay)
        {
            KillEnter();
            _group.alpha = 0f;
            Body.anchoredPosition = new Vector2(0f, -ENTER_OFFSET);
            _enter = LSequence.Create()
                .AppendInterval(delay)
                .Append(LMotion.Create(0f, 1f, LibraryTheme.Motion.STANDARD).WithEase(LibraryTheme.Motion.FADE_EASE).BindToAlpha(_group))
                .Join(LMotion.Create(-ENTER_OFFSET, 0f, LibraryTheme.Motion.STANDARD).WithEase(LibraryTheme.Motion.ENTER_EASE).BindToAnchoredPositionY(Body))
                .Run();
        }

        internal void Release()
        {
            CancelCover();
            KillEnter();
            _cover.sprite = null;
            _cover.enabled = false;
            Level = null;
        }

        private async UniTaskVoid LoadCoverAsync(SonolusSession session, SonolusLevel level, RemoteCovers covers, CancellationToken cancellationToken)
        {
            try
            {
                JacketThumbnail thumbnail = await covers.LoadAsync(session.Address, level.Cover, false, cancellationToken);
                if (!cancellationToken.IsCancellationRequested && ReferenceEquals(Level, level))
                {
                    ShowCover(thumbnail);
                }
            }
            catch (OperationCanceledException)
            {
                // Recycled or closed before the cover arrived.
            }
        }

        private void ShowCover(JacketThumbnail thumbnail)
        {
            _cover.sprite = thumbnail.Sprite;
            _cover.enabled = thumbnail.Sprite != null;
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
            _enter.TryCancel();
            _enter = default;
            _group.alpha = 1f;
            Body.anchoredPosition = Vector2.zero;
        }
    }
}
