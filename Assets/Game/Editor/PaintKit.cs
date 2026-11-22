using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenSekai.EditorTools
{
    /// <summary>
    /// Two-way bridge between the game's packed art and files a person can paint.
    ///
    /// Most of the in-live art is not a loose PNG any more: it was packed into a sprite atlas, so the
    /// sprite assets point at a 2048x2048 <c>sactx-*</c> texture and the source picture exists only as
    /// a rectangle inside it. Export cuts every referenced sprite out to its own PNG of exactly the rect
    /// size; Apply pastes the painted file back at the same rectangle. The atlas canvas size, the sprite
    /// rects and every prefab reference stay untouched, which is what makes this reversible.
    ///
    /// Loose textures (the 2D-mode background, lane sheets, judge line) are copied whole instead - they
    /// never went through an atlas.
    /// </summary>
    public static class PaintKit
    {
        private const string KIT_ROOT = "LocalPacks/paint-kit";
        private const string MANIFEST_PATH = KIT_ROOT + "/manifest.csv";

        /// <summary>
        /// Set before calling <see cref="ApplyPainted"/> to apply a delivery that came back with its own
        /// manifest (a whole HUD repaint arrives as one), leaving the exported kit manifest untouched.
        /// </summary>
        public static string OverrideManifestPath;

        private static string Manifest =>
            string.IsNullOrEmpty(OverrideManifestPath) ? MANIFEST_PATH : OverrideManifestPath;

        private static readonly string[] _liveRoots =
        {
            "Assets/Graphics/Resources/live",
            "Assets/Graphics/Resources/effects"
        };

        [MenuItem("OpenSekai/Paint Kit/1. Export live sprites", false, 200)]
        public static void ExportLive()
        {
            Export(_liveRoots, "hud");
        }

        /// <summary>
        /// 2D-mode backdrop and the large UI backgrounds live outside the live prefabs, so they never
        /// reached the first export. They are single big textures, not atlas sprites.
        /// </summary>
        private static readonly string[] _backgroundRoots =
        {
            "Assets/Game/AssetBundles/resources/startapp/live/2dmode",
            "Assets/Graphics/Resources/commonbg",
            "Assets/Graphics/Resources/Common/bg",
            "Assets/Graphics/Resources/background"
        };

        [MenuItem("OpenSekai/Paint Kit/3. Export backgrounds", false, 202)]
        public static void ExportBackgrounds()
        {
            ExportFolder(_backgroundRoots, "backgrounds", KIT_ROOT + "/manifest_backgrounds.csv");
        }

        /// <summary>
        /// The result banners and the live effect sheets are bundle-only art: no prefab in Resources
        /// points at them, so they too arrive by name at runtime.
        /// </summary>
        private static readonly string[] _effectRoots =
        {
            "Assets/Game/AssetBundles/resources/tutorial/effect_asset",
            "Assets/Game/AssetBundles/resources/startapp/live/note_preview"
        };

        [MenuItem("OpenSekai/Paint Kit/4. Export result and effect art", false, 203)]
        public static void ExportEffects()
        {
            ExportFolder(_effectRoots, "effects", KIT_ROOT + "/manifest_effects.csv");
        }

        /// <summary>
        /// Backdrops are plain textures that no prefab points at - they are resolved by name at runtime -
        /// so a prefab walk finds nothing. This export takes the folders literally.
        /// </summary>
        private static void ExportFolder(string[] roots, string group, string manifestPath)
        {
            string outDir = Path.Combine(KIT_ROOT, group);
            Directory.CreateDirectory(outDir);
            var manifest = new StringBuilder();
            manifest.AppendLine("file\ttexture\trectX\trectY\twidth\theight\tborderX\tborderY\tborderZ\tborderW\tppu\tmode");
            var used = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            int written = 0;

            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (string path in Directory.GetFiles(root, "*.png", SearchOption.AllDirectories))
                {
                    string assetPath = path.Replace('\\', '/');
                    if (assetPath.Contains("/sactx-")) continue;
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (tex == null) continue;

                    // Two folders can hold the same file name (tap_effect/0 and /1 are twins), so the name
                    // keeps the folder trail instead of letting Unique() mint a meaningless "_2".
                    string relative = assetPath.Substring(root.Length).TrimStart('/');
                    string key = relative.Substring(0, relative.Length - 4).Replace('/', '_').Replace("textures_", "").Replace("texture_", "");
                    string file = Path.Combine(outDir, Unique(used, key) + ".png");
                    var pixels = ReadRegion(assetPath, 0, 0, tex.width, tex.height);
                    if (pixels == null) continue;

                    var whole = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                    whole.SetPixels(pixels);
                    File.WriteAllBytes(file, whole.EncodeToPNG());
                    Object.DestroyImmediate(whole);

                    manifest.AppendLine(string.Join("\t",
                        file.Replace('\\', '/'), assetPath, 0, 0, tex.width, tex.height, 0, 0, 0, 0, 1, "whole"));
                    written++;
                }
            }

            File.WriteAllText(manifestPath, manifest.ToString(), new UTF8Encoding(false));
            Debug.Log($"[PaintKit] exported {written} files to {outDir}, manifest {manifestPath}");
        }

        [MenuItem("OpenSekai/Paint Kit/2. Apply painted sprites", false, 201)]
        public static void ApplyPainted()
        {
            if (!File.Exists(Manifest))
            {
                Debug.LogError("[PaintKit] no manifest yet - run Export first");
                return;
            }

            string[] lines = File.ReadAllLines(Manifest);
            var rows = new List<(int line, string source, string target, RectInt rect)>();
            var errors = new List<string>();
            int missing = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] f = lines[i].Split('\t');
                try
                {
                    if (f.Length < 12 || (f[11] != "whole" && f[11] != "rect"))
                        throw new InvalidDataException("invalid manifest row");
                    if (!File.Exists(f[0])) { missing++; continue; }
                    var rect = new RectInt(int.Parse(f[2], CultureInfo.InvariantCulture),
                        int.Parse(f[3], CultureInfo.InvariantCulture), int.Parse(f[4], CultureInfo.InvariantCulture),
                        int.Parse(f[5], CultureInfo.InvariantCulture));
                    var importer = AssetImporter.GetAtPath(f[1]) as TextureImporter;
                    if (importer == null) throw new InvalidDataException("missing texture " + f[1]);
                    importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                    if (rect.x < 0 || rect.y < 0 || rect.width <= 0 || rect.height <= 0 ||
                        rect.x > width - rect.width || rect.y > height - rect.height)
                        throw new InvalidDataException("rectangle outside " + f[1]);
                    var piece = ReadPaintedTexture(f[0]);
                    try
                    {
                        int tolerance = f[11] == "whole" ? 0 : 2;
                        if (Mathf.Abs(rect.width - piece.width) > tolerance || Mathf.Abs(rect.height - piece.height) > tolerance)
                            throw new InvalidDataException($"painted {piece.width}x{piece.height}, expected {rect.width}x{rect.height}");
                        rows.Add((i + 1, f[0], f[1], rect));
                    }
                    finally { Object.DestroyImmediate(piece); }
                }
                catch (System.Exception e) { errors.Add($"line {i + 1} ({f[0]}): {e.Message}"); }
            }

            if (errors.Count > 0)
            {
                Debug.LogError("[PaintKit] preflight failed before writing:\n" + string.Join("\n", errors));
                return;
            }

            int painted = 0;
            var changed = new HashSet<string>();
            foreach (var row in rows)
            {
                Texture2D piece = null;
                try
                {
                    piece = ReadPaintedTexture(row.source);
                    Paste(row.target, row.rect.x, row.rect.y, row.rect.width, row.rect.height, piece);
                    changed.Add(row.target);
                    painted++;
                }
                catch (System.Exception e) { errors.Add($"line {row.line} ({row.source} -> {row.target}): {e.Message}"); }
                finally { if (piece != null) Object.DestroyImmediate(piece); }
            }
            foreach (string target in changed)
            {
                try { AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport); }
                catch (System.Exception error) { errors.Add($"import {target}: {error.Message}"); }
            }
            Debug.Log($"[PaintKit] applied {painted}, not painted {missing}, failed {errors.Count}");
            if (errors.Count > 0) Debug.LogError("[PaintKit] write failures:\n" + string.Join("\n", errors));
        }

        private static Texture2D ReadPaintedTexture(string file)
        {
            byte[] bytes = File.ReadAllBytes(file);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (texture.LoadImage(bytes)) return texture;
            Object.DestroyImmediate(texture);
            throw new InvalidDataException("invalid image " + file);
        }

        private static void Paste(string texturePath, int rx, int ry, int rw, int rh, Texture2D piece)
        {
            var atlas = ReadPaintedTexture(texturePath);
            // A hand-drawn file is usually a pixel or two off the rect it has to fill. Writing it at its
            // own size spills the extra column into the neighbouring sprite and still leaves this one
            // short - the glyph comes out shaved. Resample to the rect instead of trusting the file.
            Texture2D fitted = piece;
            try
            {
                if (piece.width != rw || piece.height != rh)
                {
                    fitted = new Texture2D(rw, rh, TextureFormat.RGBA32, false);
                    var src = piece.GetPixels();
                    var dst = new Color[rw * rh];
                    for (int y = 0; y < rh; y++)
                    {
                        float sy = (y + 0.5f) * piece.height / rh - 0.5f;
                        int y0 = Mathf.Clamp(Mathf.FloorToInt(sy), 0, piece.height - 1);
                        int y1 = Mathf.Clamp(y0 + 1, 0, piece.height - 1);
                        float ty = Mathf.Clamp01(sy - y0);
                        for (int x = 0; x < rw; x++)
                        {
                            float sx = (x + 0.5f) * piece.width / rw - 0.5f;
                            int x0 = Mathf.Clamp(Mathf.FloorToInt(sx), 0, piece.width - 1);
                            int x1 = Mathf.Clamp(x0 + 1, 0, piece.width - 1);
                            float tx = Mathf.Clamp01(sx - x0);
                            Color c0 = Color.Lerp(src[y0 * piece.width + x0], src[y0 * piece.width + x1], tx);
                            Color c1 = Color.Lerp(src[y1 * piece.width + x0], src[y1 * piece.width + x1], tx);
                            dst[y * rw + x] = Color.Lerp(c0, c1, ty);
                        }
                    }
                    fitted.SetPixels(dst);
                    Debug.LogWarning($"[PaintKit] {Path.GetFileName(texturePath)}: painted {piece.width}x{piece.height}, rect {rw}x{rh} - resampled");
                }
                // Unity's sprite rect origin is bottom-left; Texture2D rows start at the bottom too, so the
                // rect can be used as-is.
                atlas.SetPixels(rx, ry, fitted.width, fitted.height, fitted.GetPixels());
                OpenSekai.Shared.Storage.AtomicFile.Write(texturePath, temp => File.WriteAllBytes(temp, atlas.EncodeToPNG()));
            }
            finally
            {
                if (fitted != piece) Object.DestroyImmediate(fitted);
                Object.DestroyImmediate(atlas);
            }

        }

        // Two sprites can share a name (one per atlas). The painter needs one file per picture, so the
        // second and later ones get a numeric suffix instead of silently overwriting the first.
        private static string Unique(HashSet<string> used, string name)
        {
            if (used.Add(name)) return name;
            for (int i = 2; ; i++)
            {
                string candidate = name + "_" + i;
                if (used.Add(candidate)) return candidate;
            }
        }

        private static void Export(string[] roots, string group)
        {
            var prefabs = new List<string>();
            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (string f in Directory.GetFiles(root, "*.prefab", SearchOption.AllDirectories))
                {
                    prefabs.Add(f.Replace('\\', '/'));
                }
            }

            var sprites = new HashSet<string>();
            foreach (string p in prefabs)
            {
                foreach (string dep in AssetDatabase.GetDependencies(p, true))
                {
                    if (AssetDatabase.LoadAssetAtPath<Sprite>(dep) != null) sprites.Add(dep);
                }
            }

            string outDir = Path.Combine(KIT_ROOT, group);
            Directory.CreateDirectory(outDir);
            var manifest = new StringBuilder();
            manifest.AppendLine("file\ttexture\trectX\trectY\twidth\theight\tborderX\tborderY\tborderZ\tborderW\tppu\tmode");

            var loose = new HashSet<string>(LooseTextures(prefabs));
            var used = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            int written = 0;

            // Loose textures first, and only whole. A texture that carries sprites is still ONE picture for the
            // painter: exporting the sprites as separate cropped files would both duplicate it and leave the
            // parts of the texture no sprite covers unpainted. Whole-file rows win, so the rect rows for these
            // textures are skipped below.
            foreach (string texturePath in loose)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (tex == null) continue;
                string file = Path.Combine(outDir, Unique(used, Path.GetFileNameWithoutExtension(texturePath)) + ".png");
                var pixels = ReadRegion(texturePath, 0, 0, tex.width, tex.height);
                if (pixels == null) continue;

                var whole = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                whole.SetPixels(pixels);
                File.WriteAllBytes(file, whole.EncodeToPNG());
                Object.DestroyImmediate(whole);

                manifest.AppendLine(string.Join("\t",
                    file.Replace('\\', '/'), texturePath, 0, 0, tex.width, tex.height, 0, 0, 0, 0, 1, "whole"));
                written++;
            }

            // Atlas sprites: cropped to their rect, then pasted back into the same rect when painted.
            foreach (string spritePath in sprites)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite == null || sprite.texture == null) continue;

                string texturePath = AssetDatabase.GetAssetPath(sprite.texture);
                if (string.IsNullOrEmpty(texturePath)) continue;
                if (loose.Contains(texturePath)) continue;

                Rect tr = sprite.textureRect;
                int rx = Mathf.RoundToInt(tr.x);
                int ry = Mathf.RoundToInt(tr.y);
                int rw = Mathf.RoundToInt(tr.width);
                int rh = Mathf.RoundToInt(tr.height);
                if (rw <= 0 || rh <= 0) continue;

                string file = Path.Combine(outDir, Unique(used, sprite.name) + ".png");
                var pixels = ReadRegion(texturePath, rx, ry, rw, rh);
                if (pixels == null) continue;

                var piece = new Texture2D(rw, rh, TextureFormat.RGBA32, false);
                piece.SetPixels(pixels);
                File.WriteAllBytes(file, piece.EncodeToPNG());
                Object.DestroyImmediate(piece);

                Vector4 b = sprite.border;
                manifest.AppendLine(string.Join("\t",
                    file.Replace('\\', '/'), texturePath, rx, ry, rw, rh,
                    b.x.ToString("F1", CultureInfo.InvariantCulture), b.y.ToString("F1", CultureInfo.InvariantCulture),
                    b.z.ToString("F1", CultureInfo.InvariantCulture), b.w.ToString("F1", CultureInfo.InvariantCulture),
                    sprite.pixelsPerUnit.ToString("F2", CultureInfo.InvariantCulture), "rect"));
                written++;
            }

            Directory.CreateDirectory(KIT_ROOT);
            File.WriteAllText(Manifest, manifest.ToString(), new UTF8Encoding(false));
            Debug.Log($"[PaintKit] exported {written} files to {outDir}, manifest {Manifest}");
        }

        private static IEnumerable<string> LooseTextures(List<string> prefabs)
        {
            var seen = new HashSet<string>();
            foreach (string p in prefabs)
            {
                foreach (string dep in AssetDatabase.GetDependencies(p, true))
                {
                    if (!dep.StartsWith("Assets/Graphics/Textures/", System.StringComparison.Ordinal)) continue;
                    if (!dep.EndsWith(".png", System.StringComparison.Ordinal)) continue;
                    if (dep.Contains("sactx-")) continue;
                    if (seen.Add(dep)) yield return dep;
                }
            }
        }

        private static Color[] ReadRegion(string texturePath, int rx, int ry, int rw, int rh)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(texturePath)))
            {
                Object.DestroyImmediate(tex);
                return null;
            }

            bool fits = rx + rw <= tex.width && ry + rh <= tex.height;
            Color[] pixels = fits ? tex.GetPixels(rx, ry, rw, rh) : null;
            Object.DestroyImmediate(tex);

            return pixels;
        }
    }
}








