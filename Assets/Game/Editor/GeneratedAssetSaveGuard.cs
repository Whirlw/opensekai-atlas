using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenSekai.EditorTools
{
    /// <summary>
    /// Keeps the assets listed in <c>Tools/generated-assets.txt</c> exactly as they were committed.
    ///
    /// TextMeshPro rewrites the dynamic font assets while the game runs - it bakes glyphs, kerning and
    /// ligatures into them on demand, and <c>FontAssetManager</c> clears and refills them at boot - and
    /// the Editor writes those changes back to disk the next time anything saves. This guard does two
    /// things:
    /// <list type="bullet">
    /// <item>it takes the listed files out of every save (<see cref="OnWillSaveAssets"/>), so a play
    /// session or a build can no longer change them on disk;</item>
    /// <item>it throws away what a play session or a build left in memory, by reloading the files from
    /// disk when the Editor returns to edit mode and before a build, so the next build ships the committed
    /// asset and not whatever this machine happened to bake.</item>
    /// </list>
    /// A deliberate re-bake runs inside <see cref="AllowSaves"/>, which lifts the guard for its duration.
    /// </summary>
    public sealed class GeneratedAssetSaveGuard : AssetModificationProcessor
    {
        private const string ManifestRelativePath = "Tools/generated-assets.txt";
        private static readonly Regex Entry = new Regex(@"^(?<hash>[0-9a-f]{64})\s+(?<size>\d+)\s+(?<path>.+?)\s*$");

        private static int _allowDepth;
        private static string[] _paths;

        /// <summary>The guarded asset paths, relative to the project (<c>Assets/...</c>).</summary>
        public static IReadOnlyList<string> GuardedPaths => _paths ??= ReadManifest();

        /// <summary>True while a deliberate re-bake has lifted the guard.</summary>
        public static bool SavesAllowed => _allowDepth > 0;

        /// <summary>Lifts the guard until the returned scope is disposed, for an intentional re-bake.</summary>
        public static IDisposable AllowSaves()
        {
            _allowDepth++;
            return new Scope();
        }

        /// <summary>Whether <paramref name="assetPath"/> is one of the guarded files.</summary>
        public static bool IsGuarded(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            string normalized = assetPath.Replace('\\', '/');
            foreach (string path in GuardedPaths)
            {
                if (string.Equals(path, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The paths of a save request with the guarded ones taken out.</summary>
        public static string[] FilterSave(string[] paths)
        {
            if (paths == null || SavesAllowed)
            {
                return paths;
            }

            var kept = new List<string>(paths.Length);
            foreach (string path in paths)
            {
                if (IsGuarded(path))
                {
                    continue;
                }

                kept.Add(path);
            }

            return kept.Count == paths.Length ? paths : kept.ToArray();
        }

        /// <summary>
        /// Reloads every guarded file from disk, dropping whatever was changed in memory. Returns how many
        /// files had unsaved changes.
        /// </summary>
        public static int DiscardInMemoryChanges()
        {
            if (SavesAllowed)
            {
                return 0;
            }

            int discarded = 0;
            foreach (string path in GuardedPaths)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                bool dirty = false;
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset == null)
                    {
                        continue;
                    }

                    dirty |= EditorUtility.IsDirty(asset);
                    EditorUtility.ClearDirty(asset);

                    // An import does not replace an object that is already loaded, so the loaded copies
                    // are unloaded: the next access reads them from the file again. Every reference to
                    // them (scenes, prefabs, TMP's caches) keeps working, since a persistent object is
                    // reloaded by its file and id.
                    if (!(asset is GameObject) && !(asset is Component))
                    {
                        Resources.UnloadAsset(asset);
                    }
                }
                if (dirty)
                {
                    discarded++;
                }
            }

            return discarded;
        }

        private static string[] OnWillSaveAssets(string[] paths)
        {
            string[] kept = FilterSave(paths);
            if (!ReferenceEquals(kept, paths))
            {
                Debug.Log("[GeneratedAssets] kept " + (paths.Length - kept.Length)
                    + " generated asset(s) out of the save; they stay as committed (" + ManifestRelativePath + ")");
            }

            return kept;
        }

        private static string[] ReadManifest()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string manifest = Path.Combine(projectRoot ?? ".", ManifestRelativePath);
            if (!File.Exists(manifest))
            {
                return Array.Empty<string>();
            }

            var paths = new List<string>();
            foreach (string line in File.ReadAllLines(manifest))
            {
                if (line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                Match match = Entry.Match(line);
                if (match.Success)
                {
                    paths.Add(match.Groups["path"].Value.Replace('\\', '/'));
                }
            }

            return paths.ToArray();
        }

        [InitializeOnLoadMethod]
        private static void HookPlayMode()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            int discarded = DiscardInMemoryChanges();
            if (discarded > 0)
            {
                Debug.Log("[GeneratedAssets] dropped what the play session baked into " + discarded + " generated asset(s)");
            }
        }

        private sealed class Scope : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _allowDepth--;
            }
        }

        /// <summary>
        /// Before a build, and before TextMeshPro's own pre-build step (order 0), the guarded assets are put
        /// back to their committed content, so a build never ships glyphs baked on this machine.
        /// </summary>
        public sealed class BuildStep : IPreprocessBuildWithReport
        {
            public int callbackOrder => -100;

            public void OnPreprocessBuild(BuildReport report)
            {
                int discarded = DiscardInMemoryChanges();
                if (discarded > 0)
                {
                    Debug.Log("[GeneratedAssets] dropped unsaved changes in " + discarded + " generated asset(s) before the build");
                }
            }
        }
    }
}
