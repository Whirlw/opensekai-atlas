using System.IO;
using OpenSekai.App.UI;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace OpenSekai.EditorTools
{
    /// <summary>
    /// Draws the app icons from the brand mark (<see cref="BrandArt"/>, the same code the splash
    /// draws its emblem with), writes them into Assets/Graphics/Icons and assigns them in Player
    /// Settings: the desktop icon, and the two layers of Android's adaptive icon. Run it after
    /// changing the mark; the PNGs are generated files, never edited by hand.
    /// </summary>
    internal static class BrandIconExporter
    {
        private const string FOLDER = "Assets/Graphics/Icons";
        internal const string APP_ICON = FOLDER + "/AppIcon.png";
        internal const string ADAPTIVE_BACKGROUND = FOLDER + "/AppIconAdaptiveBackground.png";
        internal const string ADAPTIVE_FOREGROUND = FOLDER + "/AppIconAdaptiveForeground.png";

        /// <summary>The README's picture of the icon.</summary>
        private const string DOCS_PREVIEW = "docs/images/app-icon.png";

        private const int APP_ICON_SIZE = 1024;
        private const int ADAPTIVE_SIZE = 432;
        private const int DOCS_PREVIEW_SIZE = 256;

        [MenuItem("OpenSekai/Brand/Export App Icons")]
        public static void Export()
        {
            Directory.CreateDirectory(FOLDER);
            WritePng(APP_ICON, BrandArt.Render(BrandArt.Icon.Full, APP_ICON_SIZE), APP_ICON_SIZE);
            WritePng(ADAPTIVE_BACKGROUND, BrandArt.Render(BrandArt.Icon.AdaptiveBackground, ADAPTIVE_SIZE), ADAPTIVE_SIZE);
            WritePng(ADAPTIVE_FOREGROUND, BrandArt.Render(BrandArt.Icon.AdaptiveForeground, ADAPTIVE_SIZE), ADAPTIVE_SIZE);

            string docs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", DOCS_PREVIEW));
            Directory.CreateDirectory(Path.GetDirectoryName(docs));
            WritePng(docs, BrandArt.Render(BrandArt.Icon.Full, DOCS_PREVIEW_SIZE), DOCS_PREVIEW_SIZE);

            Texture2D appIcon = Import(APP_ICON, APP_ICON_SIZE);
            Texture2D background = Import(ADAPTIVE_BACKGROUND, ADAPTIVE_SIZE);
            Texture2D foreground = Import(ADAPTIVE_FOREGROUND, ADAPTIVE_SIZE);
            AssignIcons(appIcon, background, foreground);
            AssetDatabase.SaveAssets();
            Debug.Log("[BrandIcons] Exported " + APP_ICON + ", the adaptive layers and " + DOCS_PREVIEW + ", and assigned them in Player Settings.");
        }

        /// <summary>For <c>-executeMethod</c>: exports, then quits the editor.</summary>
        public static void ExportFromCommandLine()
        {
            try
            {
                Export();
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void WritePng(string path, byte[] rgba, int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixelData(rgba, 0);
                texture.Apply(false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static Texture2D Import(string path, int size)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = Mathf.NextPowerOfTwo(size);
            // Uncompressed: the dark gradient bands visibly under block compression.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void AssignIcons(Texture2D appIcon, Texture2D background, Texture2D foreground)
        {
            // Default and desktop icon: the full rounded tile; smaller sizes are scaled from it.
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { appIcon }, IconKind.Any);
            int[] standaloneSizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any);
            var standalone = new Texture2D[standaloneSizes.Length];
            if (standalone.Length > 0)
            {
                standalone[0] = appIcon;
            }

            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, standalone, IconKind.Any);

            // Android 8+ (the project's floor is API 26) always uses the adaptive icon: a full-bleed
            // back layer and the emblem in front, which the launcher masks to its own shape.
            PlatformIcon[] adaptive = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive);
            foreach (PlatformIcon icon in adaptive)
            {
                icon.SetTextures(background, foreground);
            }

            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive, adaptive);
        }
    }
}
