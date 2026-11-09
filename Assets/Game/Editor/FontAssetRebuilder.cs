using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace OpenSekai.EditorTools
{
    /// <summary>
    /// Rebuilds the project's TMP font assets from the bundled open-source Source Han Sans SC
    /// weights so the proprietary Rodin fonts can be removed. The rebuild keeps the existing
    /// asset paths, then remaps every serialized reference from the old GUIDs to the new ones.
    /// </summary>
    public static class FontAssetRebuilder
    {
        private const string FontFolder = "Assets/Game/Resources/Fonts";
        private const string AssetFolder = "Assets/Graphics/Resources/font";

        private const string MediumFontPath = FontFolder + "/SourceHanSansSC-Medium.otf";
        private const string BoldFontPath = FontFolder + "/SourceHanSansSC-Bold.otf";

        // Old slots: DB (demi bold) and EB (extra bold), each with a shell asset and a dynamic asset.
        private const string OldDbBase = AssetFolder + "/FOT-RodinNTLGPro-DB SDF_Base.asset";
        private const string OldDbDynamic = AssetFolder + "/FOT-RodinNTLGPro-DB SDF_Dynamic.asset";
        private const string OldEbBase = AssetFolder + "/FOT-RodinNTLGPro-EB SDF_Base.asset";
        private const string OldEbDynamic = AssetFolder + "/FOT-RodinNTLGPro-EB SDF_Dynamic.asset";
        private const string OldDbBuiltin = AssetFolder + "/FOT-RodinNTLGPro-DB-Builtin.asset";
        private const string OldEbBuiltin = AssetFolder + "/FOT-RodinNTLGPro-EB-Builtin.asset";
        private const string OldDbOtf = AssetFolder + "/FOT-RodinNTLGPro-DB.otf";
        private const string OldEbOtf = AssetFolder + "/FOT-RodinNTLGPro-EB.otf";

        private const string NewMediumBase = AssetFolder + "/SourceHanSansSC-Medium SDF_Base.asset";
        private const string NewMediumDynamic = AssetFolder + "/SourceHanSansSC-Medium SDF_Dynamic.asset";
        private const string NewBoldBase = AssetFolder + "/SourceHanSansSC-Bold SDF_Base.asset";
        private const string NewBoldDynamic = AssetFolder + "/SourceHanSansSC-Bold SDF_Dynamic.asset";

        [MenuItem("OpenSekai/Dev/Fonts/Build Source Han Sans Assets")]
        public static void BuildAssets()
        {
            Font medium = LoadFont(MediumFontPath);
            Font bold = LoadFont(BoldFontPath);

            CreateFontAsset(medium, NewMediumBase, "SourceHanSansSC-Medium SDF_Base");
            CreateFontAsset(medium, NewMediumDynamic, "SourceHanSansSC-Medium SDF_Dynamic");
            CreateFontAsset(bold, NewBoldBase, "SourceHanSansSC-Bold SDF_Base");
            CreateFontAsset(bold, NewBoldDynamic, "SourceHanSansSC-Bold SDF_Dynamic");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[FontRebuild] created fonts:\n" + Describe(NewMediumBase) + Describe(NewMediumDynamic) +
                      Describe(NewBoldBase) + Describe(NewBoldDynamic));
        }

        [MenuItem("OpenSekai/Dev/Fonts/Remap References And Delete Rodin")]
        public static void RemapAndDelete()
        {
            var mapping = new Dictionary<string, string>
            {
                { GuidOf(OldDbBase), GuidOf(NewMediumBase) },
                { GuidOf(OldDbDynamic), GuidOf(NewMediumDynamic) },
                { GuidOf(OldEbBase), GuidOf(NewBoldBase) },
                { GuidOf(OldEbDynamic), GuidOf(NewBoldDynamic) },
            };

            foreach (var pair in mapping)
            {
                if (string.IsNullOrEmpty(pair.Key) || string.IsNullOrEmpty(pair.Value))
                {
                    throw new InvalidOperationException("Missing GUID for " + pair.Key + " -> " + pair.Value);
                }
            }

            int files = 0;
            int replacements = 0;
            string[] extensions = { ".prefab", ".unity", ".asset", ".mat" };
            foreach (string path in Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(path);
                if (Array.IndexOf(extensions, extension) < 0)
                {
                    continue;
                }

                string text = File.ReadAllText(path);
                string updated = text;
                foreach (var pair in mapping)
                {
                    updated = updated.Replace(pair.Key, pair.Value);
                }

                if (!string.Equals(text, updated, StringComparison.Ordinal))
                {
                    replacements += CountOccurrences(text, mapping);
                    File.WriteAllText(path, updated);
                    files++;
                }
            }

            Debug.Log("[FontRebuild] remapped " + replacements + " references in " + files + " files");

            foreach (string stale in new[] { OldDbBase, OldDbDynamic, OldEbBase, OldEbDynamic, OldDbBuiltin, OldEbBuiltin, OldDbOtf, OldEbOtf })
            {
                if (File.Exists(stale))
                {
                    AssetDatabase.DeleteAsset(stale);
                    Debug.Log("[FontRebuild] deleted " + stale);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("OpenSekai/Dev/Fonts/Report")]
        public static void Report()
        {
            Debug.Log("[FontRebuild] " + Describe(NewMediumBase) + Describe(NewMediumDynamic) +
                      Describe(NewBoldBase) + Describe(NewBoldDynamic));
        }

        private static Font LoadFont(string path)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (font == null)
            {
                throw new InvalidOperationException("Font not found or not imported: " + path);
            }

            return font;
        }

        private static void CreateFontAsset(Font source, string path, string assetName)
        {
            if (File.Exists(path))
            {
                AssetDatabase.DeleteAsset(path);
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                source,
                90,
                9,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);

            if (asset == null)
            {
                throw new InvalidOperationException("TMP_FontAsset.CreateFontAsset returned null for " + source.name);
            }

            asset.name = assetName;

            if (asset.material != null)
            {
                asset.material.name = assetName + " Material";
            }

            AssetDatabase.CreateAsset(asset, path);

            if (asset.material != null)
            {
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            if (asset.atlasTextures != null)
            {
                for (int i = 0; i < asset.atlasTextures.Length; i++)
                {
                    if (asset.atlasTextures[i] != null)
                    {
                        asset.atlasTextures[i].name = assetName + " Atlas " + i;
                        AssetDatabase.AddObjectToAsset(asset.atlasTextures[i], asset);
                    }
                }
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static string GuidOf(string path)
        {
            string meta = path + ".meta";
            if (!File.Exists(meta))
            {
                return string.Empty;
            }

            Match match = Regex.Match(File.ReadAllText(meta), "^guid: ([0-9a-f]{32})", RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static int CountOccurrences(string text, Dictionary<string, string> mapping)
        {
            int total = 0;
            foreach (string key in mapping.Keys)
            {
                int index = 0;
                while ((index = text.IndexOf(key, index, StringComparison.Ordinal)) >= 0)
                {
                    total++;
                    index += key.Length;
                }
            }

            return total;
        }

        private static string Describe(string path)
        {
            TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (asset == null)
            {
                return "  " + path + " -> missing\n";
            }

            string atlas = asset.atlasTextures != null && asset.atlasTextures.Length > 0 && asset.atlasTextures[0] != null
                ? asset.atlasTextures[0].width + "x" + asset.atlasTextures[0].height
                : "none";

            return "  " + Path.GetFileName(path) + " -> guid=" + GuidOf(path).Substring(0, 8) +
                   " source=" + (asset.sourceFontFile != null ? asset.sourceFontFile.name : "null") +
                   " mode=" + asset.atlasPopulationMode + " atlas=" + atlas +
                   " glyphs=" + asset.glyphTable.Count + "\n";
        }
    }
}
