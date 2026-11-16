using System;
using System.Collections.Generic;
using Sekai;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    internal static class LibraryUi
    {
        private static readonly Color32 ButtonColor = new Color32(54, 67, 80, 255);
        private static readonly Color32 AccentColor = new Color32(72, 148, 214, 255);
        private static readonly string[] CjkFontCandidates =
        {
            "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Noto Sans CJK SC",
            "Source Han Sans SC", "PingFang SC", "Hiragino Sans GB", "Arial Unicode MS"
        };

        private static TMP_FontAsset _cjkFontAsset;
        private static TMP_FontAsset _boldCjkFontAsset;
        private static TMP_FontAsset _condensedFontAsset;
        private static bool _cjkFontResolved;

        internal static void UseCondensedFont(TextMeshProUGUI label)
        {
            if (_condensedFontAsset == null)
            {
                Font font = Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold");
                if (font != null)
                {
                    _condensedFontAsset = TMP_FontAsset.CreateFontAsset(font);
                    TMP_FontAsset fallback = ResolveCjkFont();
                    if (fallback != null)
                    {
                        _condensedFontAsset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
                    }
                }
            }

            if (_condensedFontAsset != null)
            {
                label.font = _condensedFontAsset;
                label.fontStyle &= ~FontStyles.Bold;
            }
        }

        internal static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        internal static Image CreateImage(string name, Transform parent, Color32 color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>
        /// The library labels mix simplified Chinese with the project's Japanese typeface, so this page
        /// resolves a CJK font itself: the project's dynamic asset first (it ships with the ellipsis
        /// glyph already baked), then the bundled TTF, then an installed OS family. Every source still
        /// goes through <see cref="EnsureRequiredGlyphs"/> as a belt-and-braces guard.
        /// </summary>
        private static TMP_FontAsset ResolveCjkFont()
        {
            if (_cjkFontResolved)
            {
                return _cjkFontAsset;
            }

            _cjkFontResolved = true;

            // Preferred: the project's own dynamic asset. It is a real serialized TMP asset, and the
            // ellipsis glyph is baked into it at edit time, so TMP's overflow mode finds the character
            // without any runtime rasterization. That mattered: building an asset from the TTF with
            // CreateFontAsset at runtime only produced the character entry, not its glyph, on device —
            // the seed logged "has=True chars=1" and TMP still truncated every label 0.8 s later.
            _cjkFontAsset = LoadProjectFontFallback();

            // Second: the bundled TTF, for a build whose font asset is missing from Resources.
            if (_cjkFontAsset == null)
            {
                _cjkFontAsset = LoadBundledFont();
            }

            string[] installed = Font.GetOSInstalledFontNames();
            for (int i = 0; i < CjkFontCandidates.Length && _cjkFontAsset == null; i++)
            {
                string candidate = FindInstalledFont(installed, CjkFontCandidates[i]);
                if (candidate == null)
                {
                    continue;
                }

                _cjkFontAsset = CreateFontAsset(candidate);
            }

            // GDI may report localized family names (for example "微软雅黑" on a zh-CN system),
            // so fall back to substring markers in both languages.
            string[] markers = { "YaHei", "雅黑", "SimHei", "黑体", "MSYH", "DengXian", "等线", "SimSun", "宋体", "Noto Sans SC", "Noto Sans CJK", "Source Han" };
            for (int m = 0; m < markers.Length && _cjkFontAsset == null; m++)
            {
                for (int i = 0; i < installed.Length && _cjkFontAsset == null; i++)
                {
                    if (installed[i].IndexOf(markers[m], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _cjkFontAsset = CreateFontAsset(installed[i]);
                    }
                }
            }

            if (_cjkFontAsset == null)
            {
                _cjkFontAsset = LoadProjectFontFallback();
            }

            Debug.Log("[LibraryScreen] CJK font: " + (_cjkFontAsset != null ? _cjkFontAsset.name : "<none>")
                + " / os fonts: " + installed.Length
                + " / first: " + string.Join(" | ", installed, 0, Math.Min(12, installed.Length)));
            return EnsureRequiredGlyphs(_cjkFontAsset);
        }

        /// <summary>Horizontal ellipsis, the only character TMP asks for that no label spells out.</summary>
        private const char EllipsisCharacter = '\u2026';

        /// <summary>
        /// Seeds the glyph TMP's ellipsis overflow asks for. TMP only calls HasCharacter when it decides
        /// how to overflow a label, so a font asset without the character truncates instead of
        /// ellipsizing. The project asset now ships the glyph baked in, which makes this a no-op on the
        /// normal path; it still covers the bundled-TTF and OS-font fallbacks, whose assets start with an
        /// empty character table.
        /// </summary>
        internal static TMP_FontAsset EnsureRequiredGlyphs(TMP_FontAsset asset)
        {
            if (asset == null)
            {
                return null;
            }

            if (!asset.HasCharacter(EllipsisCharacter) && !asset.TryAddCharacters("\u2026"))
            {
                Debug.LogWarning("[LibraryScreen] Font asset " + asset.name + " cannot add the ellipsis glyph.");
            }

            return asset;
        }

        private static TMP_FontAsset LoadBundledFont()
        {
            Font font;
            try
            {
                font = ResourceCatalog.MediumTtfFont.Load();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LibraryScreen] Could not load bundled font: " + exception.Message);
                return null;
            }

            if (font == null)
            {
                return null;
            }

            try
            {
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font);
                if (asset != null)
                {
                    asset.name = "SourceHanSansSC-Medium";
                }
                return asset;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LibraryScreen] Could not build TMP asset from bundled font: " + exception.Message);
                return null;
            }
        }

        /// <summary>
        /// Fallback when the OS exposes no CJK family (some Windows installs only register a
        /// subset through GDI). The project's dynamic Rodin asset covers Japanese kanji, so
        /// labels stay readable even though simplified-only glyphs may still be missing.
        /// </summary>
        private static TMP_FontAsset LoadProjectFontFallback()
        {
            TMP_FontAsset asset = ResourceCatalog.MediumDynamicFont.Load();
            if (asset == null)
            {
                asset = ResourceCatalog.BoldDynamicFont.Load();
            }
            return asset;
        }

        private static string FindInstalledFont(string[] installed, string preferred)
        {
            for (int i = 0; i < installed.Length; i++)
            {
                if (string.Equals(installed[i], preferred, StringComparison.OrdinalIgnoreCase))
                {
                    return installed[i];
                }
            }
            return null;
        }

        private static TMP_FontAsset CreateFontAsset(string osFontName)
        {
            try
            {
                Font osFont = Font.CreateDynamicFontFromOSFont(osFontName, 36);
                if (osFont == null)
                {
                    return null;
                }

                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(osFont);
                if (asset != null)
                {
                    asset.name = "LibraryScreen-" + osFontName;
                }
                return asset;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LibraryScreen] Could not build font asset from " + osFontName + ": " + exception.Message);
                return null;
            }
        }

        internal static TextMeshProUGUI CreateText(
            string name, Transform parent, string value, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.text = value ?? string.Empty;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            ApplyCjkTextStyle(text, style);
            return text;
        }

        internal static void ApplyCjkTextStyle(TextMeshProUGUI text, FontStyles style)
        {
            TMP_FontAsset font = ResolveCjkFont();
            if ((style & FontStyles.Bold) != 0)
            {
                if (_boldCjkFontAsset == null)
                {
                    _boldCjkFontAsset = EnsureRequiredGlyphs(ResourceCatalog.BoldDynamicFont.Load());
                }
                if (_boldCjkFontAsset != null)
                {
                    font = _boldCjkFontAsset;
                    style &= ~FontStyles.Bold;
                }
            }

            if (font != null)
            {
                text.font = font;
            }
            text.fontStyle = style;
        }

        internal static Button CreateButton(
            string name, Transform parent, string label, UnityEngine.Events.UnityAction onClick, float width, float height)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, height);

            Image image = go.GetComponent<Image>();
            image.color = ButtonColor;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(220, 238, 255, 255);
            colors.pressedColor = new Color32(180, 213, 242, 255);
            colors.disabledColor = new Color32(120, 126, 132, 120);
            button.colors = colors;
            button.onClick.AddListener(onClick);

            TextMeshProUGUI text = CreateText("Label", rect, label, 24f, FontStyles.Bold, TextAlignmentOptions.Center);
            text.color = AccentColor;
            Stretch(text.rectTransform);
            return button;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
