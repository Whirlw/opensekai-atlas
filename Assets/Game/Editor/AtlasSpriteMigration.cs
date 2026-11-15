using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace OpenSekai.EditorTools
{
    /// <summary>
    /// Moves the sprites of one baked atlas onto a Unity Sprite Atlas.
    ///
    /// The in-live sprites came over as native <c>Sprite</c> assets whose rects point into a pre-packed
    /// <c>sactx-*</c> texture, so new art had to fit the old rect and a new sprite needed a hand-picked
    /// free spot in the atlas. After migration every sprite is its own PNG under
    /// <c>Assets/Graphics/AtlasSprites/&lt;atlas&gt;/</c> and a Sprite Atlas V2 packs that folder.
    ///
    /// Each PNG reuses the GUID of the asset it replaces. The sprite inside keeps fileID 21300000, but
    /// Unity also checks the reference type: a reference written against the native asset says
    /// <c>type: 2</c> and stays null against an imported texture until it says <c>type: 3</c>. So every
    /// text asset that references a migrated GUID gets that one field rewritten; nothing else changes.
    /// </summary>
    public static class AtlasSpriteMigration
    {
        private const string SpriteFolder = "Assets/Graphics/Sprites";
        private const string TextureFolder = "Assets/Graphics/Textures";
        private const string OutputRoot = "Assets/Graphics/AtlasSprites";
        private const string AtlasFolder = "Assets/Graphics/SpriteAtlases";
        private const string ArtSourceHud = "ArtSource/HUD";
        private const string ReportFolder = "TestResults";

        private static readonly string[] ReferenceExtensions =
        {
            ".prefab", ".unity", ".asset", ".anim", ".controller", ".overrideController", ".mat",
            ".playable", ".spriteatlas", ".spriteatlasv2"
        };

        private sealed class Entry
        {
            public string Guid;
            public string OldPath;
            public string Name;
            public string NewPath;
            public Rect OldRect;
            public Vector2 PivotNormalized;
            public Vector4 Border;
            public float PixelsPerUnit;
            public string AtlasTexturePath;
            public string Source;
        }

        [MenuItem("OpenSekai/Atlas Sprites/Migrate JudgeAtlas", false, 220)]
        public static void MigrateJudgeAtlas()
        {
            Debug.Log(Migrate("JudgeAtlas"));
        }

        [MenuItem("OpenSekai/Atlas Sprites/Migrate LiveAtlas", false, 221)]
        public static void MigrateLiveAtlas()
        {
            Debug.Log(Migrate("LiveAtlas"));
        }

        [MenuItem("OpenSekai/Atlas Sprites/Migrate CommonAtlas", false, 222)]
        public static void MigrateCommonAtlas()
        {
            Debug.Log(Migrate("CommonAtlas"));
        }

        /// <summary>
        /// The Sprite Atlas that packs <c>AtlasSprites/&lt;atlasName&gt;</c>. CommonAtlas keeps the atlas the
        /// UI already loads by name (<c>AtlasImage</c> references its GUID); the others get a V2 atlas.
        /// </summary>
        public static string AtlasPathFor(string atlasName)
        {
            var rebuilt = $"{AtlasFolder}/Rebuilt/{atlasName}.spriteatlas";
            return File.Exists(rebuilt) ? rebuilt : $"{AtlasFolder}/{atlasName}.spriteatlasv2";
        }

        /// <summary>Migrates every sprite that points into a <c>sactx-*-&lt;atlasName&gt;-*.png</c> page.</summary>
        public static string Migrate(string atlasName)
        {
            var report = new StringBuilder();
            report.AppendLine($"[AtlasSpriteMigration] {atlasName}");

            var atlasPages = Directory.GetFiles(TextureFolder, $"sactx-*-{atlasName}-*.png")
                .Select(p => p.Replace('\\', '/'))
                .ToDictionary(p => AssetDatabase.AssetPathToGUID(p), p => p);
            if (atlasPages.Count == 0)
            {
                return report.AppendLine("no sactx page found").ToString();
            }

            var outputFolder = $"{OutputRoot}/{atlasName}";
            var entries = CollectSprites(atlasPages, outputFolder);
            if (entries.Count == 0)
            {
                return report.AppendLine("no sprites left to migrate").ToString();
            }

            var guids = new HashSet<string>(entries.Select(e => e.Guid));
            var referencingFiles = FindReferencingFiles(guids, entries.Select(e => e.OldPath));
            var atlasPath = AtlasPathFor(atlasName);
            var atlasGuid = AssetDatabase.AssetPathToGUID(atlasPath);
            var lookupFiles = string.IsNullOrEmpty(atlasGuid)
                ? new List<string>()
                : FindReferencingFiles(new HashSet<string> { atlasGuid }, new[] { atlasPath });
            var checkedFiles = referencingFiles.Union(lookupFiles).ToList();
            var before = SnapshotReferences(checkedFiles, guids);

            Directory.CreateDirectory(outputFolder);
            var pages = new Dictionary<string, Texture2D>();
            foreach (var entry in entries)
            {
                WritePixels(entry, pages);
                File.WriteAllText(entry.NewPath + ".meta", MinimalMeta(entry.Guid));
                File.Delete(entry.OldPath);
                File.Delete(entry.OldPath + ".meta");
            }
            foreach (var page in pages.Values)
            {
                Object.DestroyImmediate(page);
            }

            var rewritten = RewriteReferenceTypes(referencingFiles, guids);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (var entry in entries)
            {
                ConfigureImporter(entry);
            }

            // Any other atlas that listed these sprites one by one would now pack them a second time.
            var otherAtlases = referencingFiles
                .Where(f => (f.EndsWith(".spriteatlas") || f.EndsWith(".spriteatlasv2")) && f != atlasPath)
                .ToList();
            foreach (var other in otherAtlases)
            {
                report.AppendLine("  " + DetachFromAtlas(other, guids));
            }

            EnsureSpriteAtlas(atlasPath, outputFolder);

            // Read the references back from the re-imported files and compare them with the snapshot.
            checkedFiles = checkedFiles.Where(File.Exists).ToList();
            foreach (var file in checkedFiles)
            {
                AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceUpdate);
            }
            var after = SnapshotReferences(checkedFiles, guids);

            report.AppendLine($"sprites={entries.Count} referencingFiles={referencingFiles.Count} lookupFiles={lookupFiles.Count} rewrittenRefs={rewritten} atlas={atlasPath}");
            foreach (var entry in entries)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.NewPath);
                if (sprite == null)
                {
                    report.AppendLine($"  FAIL {entry.Name}: no sprite at {entry.NewPath}");
                    continue;
                }
                var oldSize = entry.OldRect.size / entry.PixelsPerUnit;
                var newSize = sprite.rect.size / sprite.pixelsPerUnit;
                report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0,-20} {1,-9} world {2:F4}x{3:F4} -> {4:F4}x{5:F4}  ppu {6}",
                    entry.Name, entry.Source, oldSize.x, oldSize.y, newSize.x, newSize.y, sprite.pixelsPerUnit));
            }

            var broken = 0;
            foreach (var pair in before)
            {
                after.TryGetValue(pair.Key, out var now);
                if (now != pair.Value)
                {
                    broken++;
                    report.AppendLine($"  BROKEN {pair.Key}: {pair.Value} -> {now ?? "NULL"}");
                }
            }
            report.AppendLine($"references checked={before.Count} broken={broken}");

            Directory.CreateDirectory(ReportFolder);
            File.WriteAllText($"{ReportFolder}/atlas-migration-{atlasName}.txt", report.ToString());
            return report.ToString();
        }

        /// <summary>
        /// Reads each sprite straight from its YAML. A sprite that an old Sprite Atlas already packs
        /// reports the packed texture through <c>sprite.texture</c>, so only the serialized
        /// <c>m_RD.texture</c> says which sactx page (and which rect on it) holds the source pixels.
        /// </summary>
        private static List<Entry> CollectSprites(Dictionary<string, string> atlasPages, string outputFolder)
        {
            var entries = new List<Entry>();
            var takenNames = new HashSet<string>();
            foreach (var path in Directory.GetFiles(SpriteFolder, "*.asset").Select(p => p.Replace('\\', '/')).OrderBy(p => p))
            {
                var yaml = File.ReadAllText(path);
                var tex = TextureRx.Match(yaml);
                if (!tex.Success || !atlasPages.TryGetValue(tex.Groups[1].Value, out var page))
                {
                    continue;
                }
                var rect = RectRx.Match(yaml);
                var pivot = PivotRx.Match(yaml);
                var border = BorderRx.Match(yaml);
                var ppu = PpuRx.Match(yaml);
                var spriteName = NameRx.Match(yaml).Groups[1].Value.Trim();

                // AtlasImage looks sprites up by name, so the file keeps the sprite's own name. Two
                // assets share gauge_base_h16_dbl; they take the names their ArtSource files use.
                var fileName = Path.GetFileName(path);
                string name;
                if (!NameOverrides.TryGetValue(fileName, out name))
                {
                    name = spriteName;
                    if (takenNames.Contains(name) || File.Exists($"{outputFolder}/{name}.png"))
                    {
                        name = Path.GetFileNameWithoutExtension(path);
                    }
                }
                takenNames.Add(name);

                entries.Add(new Entry
                {
                    Guid = AssetDatabase.AssetPathToGUID(path),
                    OldPath = path,
                    Name = name,
                    NewPath = $"{outputFolder}/{name}.png",
                    OldRect = new Rect(F(rect, 1), F(rect, 2), F(rect, 3), F(rect, 4)),
                    PivotNormalized = new Vector2(F(pivot, 1), F(pivot, 2)),
                    Border = new Vector4(F(border, 1), F(border, 2), F(border, 3), F(border, 4)),
                    PixelsPerUnit = F(ppu, 1),
                    AtlasTexturePath = page
                });
            }
            return entries;
        }

        private static readonly Dictionary<string, string> NameOverrides = new Dictionary<string, string>
        {
            // Pinned the same way in Tools/apply-atlas-art.ps1: the score trough and the life trough.
            { "gauge_base_h16_dbl_0.asset", "gauge_base_h16_dbl" },
            { "gauge_base_h16_dbl.asset", "gauge_base_h16_dbl_2" }
        };

        private static readonly Regex NameRx = new Regex(@"^  m_Name: (.*)$", RegexOptions.Multiline);
        private static readonly Regex TextureRx = new Regex(@"m_RD:\s*\r?\n\s*serializedVersion: \d+\s*\r?\n\s*texture: \{fileID: 2800000, guid: ([0-9a-f]{32})");
        private static readonly Regex RectRx = new Regex(@"m_Rect:\s*\r?\n\s*serializedVersion: 2\s*\r?\n\s*x: (\S+)\s*\r?\n\s*y: (\S+)\s*\r?\n\s*width: (\S+)\s*\r?\n\s*height: (\S+)");
        private static readonly Regex PivotRx = new Regex(@"m_Pivot: \{x: ([^,]+), y: ([^}]+)\}");
        private static readonly Regex BorderRx = new Regex(@"m_Border: \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)\}");
        private static readonly Regex PpuRx = new Regex(@"m_PixelsToUnits: (\S+)");

        private static float F(Match match, int group)
        {
            return float.Parse(match.Groups[group].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The painted file in ArtSource/HUD wins when it has the rect's pixel size (that is what was
        /// pasted into the atlas); otherwise the rect is cut out of the atlas page itself.
        /// </summary>
        private static void WritePixels(Entry entry, Dictionary<string, Texture2D> pages)
        {
            var width = Mathf.CeilToInt(entry.OldRect.width - 0.01f);
            var height = Mathf.CeilToInt(entry.OldRect.height - 0.01f);

            foreach (var candidate in new[] { entry.Name, Path.GetFileNameWithoutExtension(entry.OldPath) })
            {
                var art = $"{ArtSourceHud}/{candidate}.png";
                if (!File.Exists(art))
                {
                    continue;
                }
                var probe = new Texture2D(2, 2);
                probe.LoadImage(File.ReadAllBytes(art));
                var matches = probe.width == width && probe.height == height;
                Object.DestroyImmediate(probe);
                if (matches)
                {
                    File.Copy(art, entry.NewPath, true);
                    entry.Source = "artsource";
                    return;
                }
            }

            if (!pages.TryGetValue(entry.AtlasTexturePath, out var page))
            {
                page = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                page.LoadImage(File.ReadAllBytes(entry.AtlasTexturePath));
                pages[entry.AtlasTexturePath] = page;
            }
            var x = Mathf.Clamp(Mathf.FloorToInt(entry.OldRect.x), 0, page.width - width);
            var y = Mathf.Clamp(Mathf.FloorToInt(entry.OldRect.y), 0, page.height - height);
            var cut = new Texture2D(width, height, TextureFormat.RGBA32, false);
            cut.SetPixels(page.GetPixels(x, y, width, height));
            cut.Apply();
            File.WriteAllBytes(entry.NewPath, cut.EncodeToPNG());
            Object.DestroyImmediate(cut);
            entry.Source = "atlas-cut";
        }

        private static string MinimalMeta(string guid)
        {
            return "fileFormatVersion: 2\n" +
                   $"guid: {guid}\n" +
                   "TextureImporter:\n" +
                   "  serializedVersion: 13\n" +
                   "  textureType: 8\n" +
                   "  spriteMode: 1\n" +
                   "  userData: \n" +
                   "  assetBundleName: \n" +
                   "  assetBundleVariant: \n";
        }

        private static void ConfigureImporter(Entry entry)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(entry.NewPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            // The atlas compresses; the source stays exact so a repack never compounds artefacts.
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = entry.PivotNormalized;
            settings.spritePixelsPerUnit = entry.PixelsPerUnit;
            settings.spriteBorder = entry.Border;
            settings.spriteExtrude = 1;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static readonly SpriteAtlasPackingSettings Packing = new SpriteAtlasPackingSettings
        {
            padding = 4,
            blockOffset = 1,
            enableRotation = false,
            enableTightPacking = false,
            enableAlphaDilation = true
        };

        private static void EnsureSpriteAtlas(string path, string folder)
        {
            if (path.EndsWith(".spriteatlas"))
            {
                // A V1 atlas the UI already references: keep the asset and its GUID, pack the folder.
                var existing = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                existing.Remove(existing.GetPackables());
                existing.Add(new Object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder) });
                existing.SetPackingSettings(Packing);
                existing.SetIncludeInBuild(true);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                SpriteAtlasUtility.PackAtlases(new[] { existing }, EditorUserBuildSettings.activeBuildTarget);
                return;
            }

            if (!File.Exists(path))
            {
                var asset = new SpriteAtlasAsset();
                asset.Add(new Object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder) });
                SpriteAtlasAsset.Save(asset, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
            importer.includeInBuild = true;
            importer.packingSettings = Packing;
            importer.textureSettings = new SpriteAtlasTextureSettings
            {
                readable = false,
                generateMipMaps = false,
                sRGB = true,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1
            };
            var platform = importer.GetPlatformSettings("DefaultTexturePlatform");
            platform.maxTextureSize = 2048;
            platform.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformSettings(platform);
            importer.SaveAndReimport();

            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
            SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);
        }

        /// <summary>
        /// Takes migrated sprites out of an atlas that listed them individually. An atlas left empty
        /// that nothing references is deleted; one that still packs other sprites is kept.
        /// </summary>
        private static string DetachFromAtlas(string path, HashSet<string> guids)
        {
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
            if (atlas == null)
            {
                return $"atlas {path}: could not load";
            }
            var mine = atlas.GetPackables()
                .Where(o => o != null && guids.Contains(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(o))))
                .ToArray();
            atlas.Remove(mine);
            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();

            var guid = AssetDatabase.AssetPathToGUID(path);
            var users = FindReferencingFiles(new HashSet<string> { guid }, new[] { path });
            if (atlas.GetPackables().Length == 0 && users.Count == 0)
            {
                AssetDatabase.DeleteAsset(path);
                return $"atlas {path}: removed {mine.Length} packables, now empty and unreferenced, deleted";
            }
            return $"atlas {path}: removed {mine.Length} packables, {atlas.GetPackables().Length} left, {users.Count} users";
        }

        private static List<string> FindReferencingFiles(HashSet<string> guids, IEnumerable<string> excluded)
        {
            var skip = new HashSet<string>(excluded);
            var files = new List<string>();
            foreach (var file in Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories))
            {
                var path = file.Replace('\\', '/');
                if (skip.Contains(path) || !ReferenceExtensions.Contains(Path.GetExtension(path)))
                {
                    continue;
                }
                var text = File.ReadAllText(path);
                if (text.StartsWith("%YAML") && guids.Any(g => text.Contains(g)))
                {
                    files.Add(path);
                }
            }
            return files;
        }

        private static int RewriteReferenceTypes(List<string> files, HashSet<string> guids)
        {
            var count = 0;
            foreach (var path in files)
            {
                var text = File.ReadAllText(path);
                var updated = text;
                foreach (var guid in guids)
                {
                    var from = $"guid: {guid}, type: 2}}";
                    var hits = CountOf(updated, from);
                    if (hits == 0)
                    {
                        continue;
                    }
                    updated = updated.Replace(from, $"guid: {guid}, type: 3}}");
                    count += hits;
                }
                if (updated != text)
                {
                    File.WriteAllText(path, updated);
                }
            }
            return count;
        }

        private static int CountOf(string text, string value)
        {
            var count = 0;
            for (var i = text.IndexOf(value, System.StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, System.StringComparison.Ordinal))
            {
                count++;
            }
            return count;
        }

        /// <summary>
        /// Every serialized reference to a migrated sprite, keyed by where it sits, valued by the sprite
        /// name. Prefabs and scriptable assets are read directly; scenes would need opening, so their
        /// references are only rewritten, not compared.
        /// </summary>
        private static Dictionary<string, string> SnapshotReferences(List<string> files, HashSet<string> guids)
        {
            var result = new Dictionary<string, string>();
            foreach (var file in files)
            {
                if (file.EndsWith(".unity"))
                {
                    continue;
                }
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(file))
                {
                    // An atlas's own packables are replaced on purpose; what matters is what the UI sees.
                    if (obj == null || obj is GameObject || obj is Sprite || obj is Texture || obj is SpriteAtlas)
                    {
                        continue;
                    }
                    var owner = obj is Component c ? $"{PathOf(c.transform)}:{c.GetType().Name}" : $"{obj.name}:{obj.GetType().Name}";
                    var so = new SerializedObject(obj);

                    // AtlasImage and friends: a SpriteAtlas plus a sprite name resolved at runtime.
                    var atlasProp = so.FindProperty("atlas");
                    var nameProp = so.FindProperty("spriteName");
                    if (atlasProp != null && nameProp != null && atlasProp.propertyType == SerializedPropertyType.ObjectReference &&
                        nameProp.propertyType == SerializedPropertyType.String && !string.IsNullOrEmpty(nameProp.stringValue) &&
                        atlasProp.objectReferenceValue is SpriteAtlas lookupAtlas)
                    {
                        var found = lookupAtlas.GetSprite(nameProp.stringValue);
                        result[$"{file}|{owner}.atlas[{nameProp.stringValue}]"] = found != null ? "found" : "missing";
                        if (found != null)
                        {
                            Object.DestroyImmediate(found);
                        }
                    }

                    var it = so.GetIterator();
                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference)
                        {
                            continue;
                        }
                        var value = it.objectReferenceValue;
                        var key = $"{file}|{owner}.{it.propertyPath}";
                        if (value is Sprite sprite)
                        {
                            var g = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sprite));
                            if (guids.Contains(g))
                            {
                                // Compared by GUID: a sprite renamed on purpose (gauge_base_h16_dbl_2) is still the same sprite.
                                result[key] = g;
                            }
                        }
                    }
                }
            }
            return result;
        }

        private static string PathOf(Transform t)
        {
            var path = t.name;
            for (var p = t.parent; p != null; p = p.parent)
            {
                path = p.name + "/" + path;
            }
            return path;
        }
    }
}
