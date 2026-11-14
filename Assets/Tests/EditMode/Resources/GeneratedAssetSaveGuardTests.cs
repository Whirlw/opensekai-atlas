using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using OpenSekai.EditorTools;
using TMPro;
using UnityEditor;

namespace OpenSekai.Tests.EditMode.ResourceLoading
{
    /// <summary>
    /// The generated font assets stay as committed: a save leaves them out, what a session baked into them
    /// in memory is dropped on the way back to edit mode, and the build step no longer wipes the glyph
    /// that was baked in on purpose.
    /// </summary>
    public sealed class GeneratedAssetSaveGuardTests
    {
        private const string MediumDynamic = "Assets/Graphics/Resources/font/SourceHanSansSC-Medium SDF_Dynamic.asset";

        private static string Sha(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return string.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2")));
            }
        }

        [Test]
        public void TheManifestFilesAreGuarded()
        {
            Assert.That(GeneratedAssetSaveGuard.GuardedPaths.Count, Is.EqualTo(4));
            Assert.That(GeneratedAssetSaveGuard.IsGuarded(MediumDynamic), Is.True);
            Assert.That(GeneratedAssetSaveGuard.IsGuarded(MediumDynamic.Replace('/', '\\')), Is.True, "either separator");
            Assert.That(GeneratedAssetSaveGuard.IsGuarded("Assets/Scenes/Library.unity"), Is.False);
        }

        [Test]
        public void ASaveLeavesTheGuardedFilesOut()
        {
            string[] request = { "Assets/Scenes/Library.unity", MediumDynamic };
            Assert.That(GeneratedAssetSaveGuard.FilterSave(request), Is.EqualTo(new[] { "Assets/Scenes/Library.unity" }));

            using (GeneratedAssetSaveGuard.AllowSaves())
            {
                Assert.That(GeneratedAssetSaveGuard.FilterSave(request), Is.EqualTo(request), "a re-bake may save them");
            }

            Assert.That(GeneratedAssetSaveGuard.SavesAllowed, Is.False, "the scope puts the guard back");
        }

        [Test]
        public void TheBakedEllipsisSurvivesTheBuildStep()
        {
            // TMP's pre-build step clears every dynamic asset that asks for it; this one carries a glyph
            // baked in on purpose, so it must not ask.
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MediumDynamic);
            Assert.That(font, Is.Not.Null);
            Assert.That(new SerializedObject(font).FindProperty("m_ClearDynamicDataOnBuild").boolValue, Is.False);
        }

        [Test]
        public void WhatASessionBakesNeverReachesTheFileAndIsDroppedAfterwards()
        {
            string before = Sha(MediumDynamic);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MediumDynamic);
            int characters = font.characterTable.Count;
            try
            {
                // What a play session does: TMP rasterises new characters into the dynamic asset.
                font.TryAddCharacters("曦鑫", out string missing);
                Assume.That(font.characterTable.Count, Is.GreaterThan(characters), "the font baked something (missing: " + missing + ")");
                EditorUtility.SetDirty(font);

                AssetDatabase.SaveAssets();
                Assert.That(Sha(MediumDynamic), Is.EqualTo(before), "a save does not write the baked glyphs");
            }
            finally
            {
                GeneratedAssetSaveGuard.DiscardInMemoryChanges();
            }

            var reloaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MediumDynamic);
            Assert.That(reloaded.characterTable.Count, Is.EqualTo(characters), "the in-memory bake is dropped");
            Assert.That(reloaded.HasCharacter('…'), Is.True, "the committed ellipsis is still there");
            Assert.That(Sha(MediumDynamic), Is.EqualTo(before));
        }
    }
}
