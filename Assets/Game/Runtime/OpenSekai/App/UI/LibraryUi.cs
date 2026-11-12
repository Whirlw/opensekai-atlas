using System;
using System.Collections.Generic;
using System.IO;
using OpenSekai.Shared.Packages;
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
        private static bool _cjkFontResolved;

        private static readonly Dictionary<string, Sprite> CoverCache = new Dictionary<string, Sprite>();

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
        /// The project font is a Japanese Rodin SDF asset; simplified Chinese labels would fall
        /// back to missing glyphs. Resolve one OS font that actually contains CJK and use it for
        /// this page. Returns null when the platform has no usable font, leaving TMP's default.
        /// </summary>
        private static TMP_FontAsset ResolveCjkFont()
        {
            if (_cjkFontResolved)
            {
                return _cjkFontAsset;
            }

            _cjkFontResolved = true;

            // Preferred: the bundled UI font (Noto Sans SC subset, OFL). It is the only source
            // that is guaranteed to exist on every target and cannot be affected by OS fonts.
            _cjkFontAsset = LoadBundledFont();
            if (_cjkFontAsset != null)
            {
                Debug.Log("[LibraryScreen] CJK font: " + _cjkFontAsset.name);
                return _cjkFontAsset;
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
            return _cjkFontAsset;
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
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;

            TMP_FontAsset font = ResolveCjkFont();
            if (font != null)
            {
                text.font = font;
            }

            return text;
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

        /// <summary>Loads (and caches) a package jacket as a sprite; null when the package has none.</summary>
        internal static Sprite LoadCover(SongPackage package)
        {
            if (package == null)
            {
                return null;
            }

            string path = package.JacketPath;
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (CoverCache.TryGetValue(path, out Sprite cached))
            {
                return cached;
            }

            Sprite sprite = null;
            try
            {
                if (File.Exists(path))
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (texture.LoadImage(bytes))
                    {
                        texture.wrapMode = TextureWrapMode.Clamp;
                        sprite = Sprite.Create(
                            texture,
                            new Rect(0f, 0f, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f),
                            100f);
                        sprite.name = Path.GetFileNameWithoutExtension(path);
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(texture);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LibraryScreen] Could not load jacket " + path + ": " + exception.Message);
            }

            CoverCache[path] = sprite;
            return sprite;
        }
    }
}
