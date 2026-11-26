using System;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// An action row in the package action card's shape: icon disc, title, one line of detail and
    /// a chevron. A row that must not fire on a stray tap arms first, like 删除曲包. The first tap
    /// turns it to <c>armedColor</c>, rewrites its text and shakes it. The owner runs the action
    /// on the second tap.
    /// </summary>
    internal sealed class LibraryActionRow : IDisposable
    {
        internal const float HEIGHT = 104f;
        internal const float GAP = 12f;
        private const float ICON_SIZE = 60f;

        private readonly RectTransform _row;
        private readonly Image _background;
        private readonly Image _outline;
        private readonly Image _iconBackground;
        private readonly Image _icon;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _body;
        private readonly Color32 _armedColor;
        private readonly Color32 _idleColor;
        private string _idleTitle;
        private string _idleBody;
        private string _armedTitle;
        private string _armedBody;
        private MotionHandle _armMotion;

        internal bool Armed { get; private set; }

        internal RectTransform Rect => _row;

        /// <param name="index">Rows stack from the top of <paramref name="parent"/>, <see cref="GAP"/> apart.</param>
        /// <param name="idleColor">Title and icon at rest. A row tinted like its armed color keeps a faint disc of it.</param>
        internal LibraryActionRow(
            RectTransform parent, int index, string name, LibraryIcon icon,
            string idleTitle, string idleBody, string armedTitle, string armedBody,
            Color32 armedColor, Color32 idleColor, UnityEngine.Events.UnityAction onClick)
        {
            _armedColor = armedColor;
            _idleColor = idleColor;

            // Each click picks its own sound: the first arms with a warning, the second confirms.
            Button button = LibraryWidgets.PillButton(
                name, parent, string.Empty, onClick, 0f, HEIGHT,
                LibraryTheme.Colors.Control, LibraryTheme.Colors.TextPrimary, sound: UiCue.None);
            _background = (Image)button.targetGraphic;
            _background.sprite = LibraryShapes.RoundedRect(LibraryTheme.Radius.ROW);
            _row = _background.rectTransform;
            LibraryWidgets.TopStrip(_row, 0f, 0f, index * (HEIGHT + GAP), HEIGHT);

            _outline = LibraryWidgets.Shape("Outline", _background.transform,
                LibraryShapes.Ring(LibraryTheme.Radius.ROW, 1f), LibraryTheme.Colors.Hairline);
            LibraryUi.Stretch(_outline.rectTransform);

            _iconBackground = LibraryWidgets.Panel("IconBackground", _background.transform, ICON_SIZE * 0.5f, LibraryTheme.Colors.Control);
            LibraryWidgets.Place(_iconBackground.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(24f, 0f), new Vector2(ICON_SIZE, ICON_SIZE));
            _icon = LibraryWidgets.Icon(_iconBackground.transform, icon, LibraryTheme.Colors.TextPrimary, 28f);

            float textLeft = 24f + ICON_SIZE + 22f;
            _title = LibraryWidgets.Label("Title", _background.transform, idleTitle, 24f, LibraryTheme.Colors.TextPrimary, FontStyles.Bold);
            LibraryWidgets.Place(_title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f),
                new Vector2(textLeft, 2f), new Vector2(520f, 34f));
            _body = LibraryWidgets.Label("Body", _background.transform, idleBody, LibraryTheme.FontSize.CAPTION, LibraryTheme.Colors.TextMuted);
            LibraryWidgets.Place(_body.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 1f),
                new Vector2(textLeft, -4f), new Vector2(520f, 28f));

            Image chevron = LibraryWidgets.Icon(_background.transform, LibraryIcon.ChevronRight, LibraryTheme.Colors.TextMuted, 26f);
            LibraryWidgets.Place(chevron.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-26f, 0f), new Vector2(26f, 26f));

            SetText(idleTitle, idleBody, armedTitle, armedBody);
        }

        /// <summary>Replaces both states' text and disarms the row.</summary>
        internal void SetText(string idleTitle, string idleBody, string armedTitle, string armedBody)
        {
            _idleTitle = idleTitle;
            _idleBody = idleBody;
            _armedTitle = armedTitle;
            _armedBody = armedBody;
            SetArmed(false, false);
        }

        internal void SetArmed(bool armed, bool animate)
        {
            Armed = armed;
            _title.text = armed ? _armedTitle : _idleTitle;
            _body.text = armed ? _armedBody : _idleBody;
            _title.color = armed ? _armedColor : _idleColor;
            _icon.color = armed ? _armedColor : _idleColor;
            _body.color = armed ? _armedColor : LibraryTheme.Colors.TextMuted;
            _background.color = armed ? WithAlpha(_armedColor, 36) : LibraryTheme.Colors.Control;
            _outline.color = armed ? WithAlpha(_armedColor, 110) : LibraryTheme.Colors.Hairline;
            bool tinted = (Color)_idleColor == (Color)_armedColor;
            _iconBackground.color = armed ? WithAlpha(_armedColor, 60)
                : tinted ? WithAlpha(_armedColor, 30) : LibraryTheme.Colors.Control;

            _armMotion.TryCancel();
            _armMotion = default;
            _row.localRotation = Quaternion.identity;
            if (animate)
            {
                _armMotion = Shake(_row);
            }
        }

        /// <summary>The short shake of a row that now needs a second tap.</summary>
        internal static MotionHandle Shake(RectTransform row) =>
            LMotion.Punch.Create(0f, 1.6f, 0.36f)
                .WithFrequency(8)
                .WithDampingRatio(1f)
                .Bind(row, static (z, target) => target.localRotation = Quaternion.Euler(0f, 0f, z));

        public void Dispose()
        {
            _armMotion.TryCancel();
        }

        private static Color32 WithAlpha(Color32 color, byte alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
