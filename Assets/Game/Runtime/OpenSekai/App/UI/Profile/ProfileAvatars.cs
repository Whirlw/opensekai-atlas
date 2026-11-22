using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The built-in avatars. The profile stores only the stable id; this table turns it into a
    /// drawing: a disc in the avatar's color with one emblem in the middle, built from the
    /// library's shapes so no image ships with it. Ids are never renamed or reused. An id this
    /// table does not know (a newer version's, or one from a backup) is kept as it is and drawn as
    /// the default.
    /// </summary>
    internal static class ProfileAvatars
    {
        internal const string DEFAULT_ID = "default";

        /// <summary>What sits in the middle of the disc.</summary>
        internal enum Emblem
        {
            Icon,
            Note,
            Flick,
            Hexagon,
            Diamond,
            Halo,
            Orbit
        }

        internal readonly struct Avatar
        {
            internal Avatar(string id, Color32 color, Emblem emblem, LibraryIcon icon = LibraryIcon.User)
            {
                Id = id;
                Color = color;
                Kind = emblem;
                Icon = icon;
            }

            internal string Id { get; }
            internal Color32 Color { get; }
            internal Emblem Kind { get; }
            internal LibraryIcon Icon { get; }
        }

        private static readonly Avatar[] _all =
        {
            new Avatar(DEFAULT_ID, new Color32(182, 146, 255, 255), Emblem.Icon, LibraryIcon.User),
            new Avatar("note", new Color32(255, 140, 186, 255), Emblem.Note),
            new Avatar("flick", new Color32(122, 182, 255, 255), Emblem.Flick),
            new Avatar("hexagon", new Color32(255, 198, 112, 255), Emblem.Hexagon),
            new Avatar("diamond", new Color32(110, 214, 192, 255), Emblem.Diamond),
            new Avatar("halo", new Color32(194, 174, 255, 255), Emblem.Halo),
            new Avatar("wave", new Color32(124, 200, 245, 255), Emblem.Icon, LibraryIcon.Wave),
            new Avatar("heart", new Color32(242, 128, 160, 255), Emblem.Icon, LibraryIcon.Heart),
            new Avatar("globe", new Color32(157, 184, 255, 255), Emblem.Icon, LibraryIcon.Globe),
            new Avatar("trophy", new Color32(245, 196, 81, 255), Emblem.Icon, LibraryIcon.Trophy),
            new Avatar("orbit", new Color32(242, 176, 222, 255), Emblem.Orbit),
            new Avatar("play", new Color32(123, 216, 143, 255), Emblem.Icon, LibraryIcon.Play)
        };

        internal static IReadOnlyList<Avatar> All => _all;

        /// <summary>The avatar for <paramref name="id"/>; the default for an id this table does not know.</summary>
        internal static Avatar Find(string id)
        {
            for (int i = 0; i < _all.Length; i++)
            {
                if (string.Equals(_all[i].Id, id, StringComparison.Ordinal))
                {
                    return _all[i];
                }
            }

            return _all[0];
        }

        internal static bool IsKnown(string id) => Array.Exists(_all, avatar => string.Equals(avatar.Id, id, StringComparison.Ordinal));
    }

    /// <summary>
    /// One avatar disc at a fixed size: a dark disc washed with the avatar's color from below, a
    /// rim in that color and the emblem. <see cref="SetAvatar"/> redraws it for another id.
    /// </summary>
    internal sealed class ProfileAvatarView
    {
        private readonly float _size;
        private readonly Image _glow;
        private readonly Image _disc;
        private readonly Image _wash;
        private readonly Image _rim;
        private readonly RectTransform _emblem;
        private string _id;

        internal ProfileAvatarView(Transform parent, string name, float size, bool glow = true)
        {
            _size = size;
            Root = LibraryUi.CreateRect(name, parent);
            Root.sizeDelta = new Vector2(size, size);
            float radius = size * 0.5f;

            _glow = LibraryUi.CreateImage("Glow", Root, Color.clear);
            _glow.sprite = LibraryShapes.Glow();
            _glow.raycastTarget = false;
            _glow.enabled = glow;
            LibraryWidgets.Place(_glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(size * 1.9f, size * 1.9f));

            _disc = LibraryWidgets.Panel("Disc", Root, radius, Color.white);
            LibraryUi.Stretch(_disc.rectTransform);
            _disc.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _wash = LibraryWidgets.Shape("Wash", _disc.transform, HomeShapes.FadeUp(), Color.white);
            _wash.type = Image.Type.Simple;
            _wash.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
            LibraryUi.Stretch(_wash.rectTransform);
            _emblem = LibraryUi.CreateRect("Emblem", _disc.transform);
            LibraryUi.Stretch(_emblem);
            _rim = LibraryWidgets.Shape("Rim", Root, LibraryShapes.Ring(radius, Mathf.Max(2f, size * 0.025f)), Color.white);
            LibraryUi.Stretch(_rim.rectTransform);
        }

        internal RectTransform Root { get; }

        internal string Id => _id;

        internal void SetAvatar(string id)
        {
            ProfileAvatars.Avatar avatar = ProfileAvatars.Find(id);
            if (string.Equals(_id, avatar.Id, StringComparison.Ordinal))
            {
                return;
            }

            _id = avatar.Id;
            Color32 color = avatar.Color;
            _disc.color = Color.Lerp(LibraryTheme.Colors.Surface, color, 0.16f);
            _wash.color = HomeTheme.Colors.WithAlpha(color, 92);
            _rim.color = HomeTheme.Colors.WithAlpha(color, 210);
            _glow.color = HomeTheme.Colors.WithAlpha(color, 56);
            for (int i = _emblem.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(_emblem.GetChild(i).gameObject);
            }

            DrawEmblem(avatar);
        }

        private void DrawEmblem(ProfileAvatars.Avatar avatar)
        {
            Color32 color = avatar.Color;
            Color32 light = Color.Lerp(color, Color.white, 0.55f);
            float s = _size;
            switch (avatar.Kind)
            {
                case ProfileAvatars.Emblem.Note:
                {
                    Glow(color, new Vector2(0f, 0f), new Vector2(s * 0.95f, s * 0.55f), 110);
                    Image note = Block("Note", light, s * 0.09f, new Vector2(s * 0.58f, s * 0.18f));
                    note.rectTransform.anchoredPosition = new Vector2(0f, -s * 0.02f);
                    Image line = Block("Line", HomeTheme.Colors.WithAlpha(Color.white, 120), s * 0.01f, new Vector2(s * 0.78f, s * 0.02f));
                    line.rectTransform.anchoredPosition = new Vector2(0f, -s * 0.2f);
                    break;
                }
                case ProfileAvatars.Emblem.Flick:
                {
                    Glow(color, Vector2.zero, new Vector2(s * 0.9f, s * 0.7f), 100);
                    Image note = Block("Note", light, s * 0.08f, new Vector2(s * 0.5f, s * 0.16f));
                    note.rectTransform.anchoredPosition = new Vector2(0f, -s * 0.12f);
                    for (int i = 0; i < 2; i++)
                    {
                        Image arrow = LibraryWidgets.Shape("Arrow" + i, _emblem, LibraryShapes.Chevron(),
                            HomeTheme.Colors.WithAlpha(light, (byte)(i == 0 ? 255 : 150)));
                        arrow.type = Image.Type.Simple;
                        LibraryWidgets.Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, s * (0.1f + i * 0.13f)), new Vector2(s * 0.3f, s * 0.15f));
                        arrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
                    }

                    break;
                }
                case ProfileAvatars.Emblem.Hexagon:
                {
                    Glow(color, Vector2.zero, new Vector2(s * 0.9f, s * 0.9f), 90);
                    Shape("Outer", LibraryShapes.Hexagon(0.06f), light, s * 0.56f, 0f);
                    Shape("Inner", LibraryShapes.Hexagon(), HomeTheme.Colors.WithAlpha(light, 200), s * 0.26f, 0f);
                    break;
                }
                case ProfileAvatars.Emblem.Diamond:
                {
                    Glow(color, Vector2.zero, new Vector2(s * 0.9f, s * 0.9f), 90);
                    Image outer = LibraryWidgets.Shape("Outer", _emblem, LibraryShapes.Ring(s * 0.05f, Mathf.Max(2f, s * 0.03f)), light);
                    Center(outer.rectTransform, s * 0.38f, 45f);
                    Image inner = Block("Inner", light, s * 0.03f, new Vector2(s * 0.16f, s * 0.16f));
                    inner.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
                    break;
                }
                case ProfileAvatars.Emblem.Halo:
                {
                    Glow(color, Vector2.zero, new Vector2(s * 0.8f, s * 0.8f), 120);
                    for (int i = 0; i < 3; i++)
                    {
                        float diameter = s * (0.24f + i * 0.17f);
                        Image ring = LibraryWidgets.Shape("Ring" + i, _emblem,
                            LibraryShapes.Ring(diameter * 0.5f, Mathf.Max(1.5f, s * (0.03f - i * 0.007f))),
                            HomeTheme.Colors.WithAlpha(light, (byte)(255 - i * 70)));
                        Center(ring.rectTransform, diameter, 0f);
                    }

                    break;
                }
                case ProfileAvatars.Emblem.Orbit:
                {
                    Glow(color, Vector2.zero, new Vector2(s * 0.8f, s * 0.8f), 100);
                    Image planet = Block("Planet", light, s * 0.15f, new Vector2(s * 0.3f, s * 0.3f));
                    planet.rectTransform.anchoredPosition = Vector2.zero;
                    RectTransform orbit = LibraryUi.CreateRect("Orbit", _emblem);
                    Center(orbit, s * 0.7f, -24f);
                    Image path = LibraryWidgets.Shape("Path", orbit, LibraryShapes.Ring(s * 0.35f, Mathf.Max(1.5f, s * 0.018f)),
                        HomeTheme.Colors.WithAlpha(light, 190));
                    LibraryUi.Stretch(path.rectTransform);
                    orbit.localScale = new Vector3(1f, 0.36f, 1f);
                    Image moon = Block("Moon", Color.white, s * 0.04f, new Vector2(s * 0.08f, s * 0.08f));
                    moon.rectTransform.anchoredPosition = new Vector2(s * 0.3f, s * 0.06f);
                    break;
                }
                default:
                {
                    Glow(color, Vector2.zero, new Vector2(s * 0.8f, s * 0.8f), 80);
                    Image icon = LibraryWidgets.Icon(_emblem, avatar.Icon, light, s * 0.46f);
                    icon.name = "Icon";
                    break;
                }
            }
        }

        private Image Block(string name, Color32 color, float radius, Vector2 size)
        {
            Image block = LibraryWidgets.Panel(name, _emblem, radius, color);
            LibraryWidgets.Place(block.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return block;
        }

        private void Shape(string name, Sprite sprite, Color32 color, float size, float angle)
        {
            Image image = LibraryWidgets.Shape(name, _emblem, sprite, color);
            image.type = Image.Type.Simple;
            Center(image.rectTransform, size, angle);
        }

        private void Glow(Color32 color, Vector2 position, Vector2 size, byte alpha)
        {
            Image glow = LibraryUi.CreateImage("Glow", _emblem, HomeTheme.Colors.WithAlpha(color, alpha));
            glow.sprite = LibraryShapes.Glow();
            glow.raycastTarget = false;
            LibraryWidgets.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
        }

        private static void Center(RectTransform rect, float size, float angle)
        {
            LibraryWidgets.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            rect.localEulerAngles = new Vector3(0f, 0f, angle);
        }
    }
}
