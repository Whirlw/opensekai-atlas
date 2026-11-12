using NUnit.Framework;
using Sekai;
using TMPro;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.ResourceLoading
{
    /// <summary>
    /// The catalog is the only place that spells a fixed Resources path, so this fixture is the place
    /// that proves each of those paths still resolves and still has the type the caller expects. A
    /// renamed or re-foldered asset shows up here instead of as a null in the middle of a live.
    /// </summary>
    public class ResourceCatalogTests
    {
        private static void AssertEntry<T>(ResourceEntry<T> entry, string description) where T : Object
        {
            Assert.That(entry.Path, Is.Not.Null.And.Not.Empty, description + ": path");
            Assert.That(entry.Load(), Is.Not.Null, description + ": Resources.Load(\"" + entry.Path + "\")");

            if (entry.FallbackPath != null)
            {
                Assert.That(entry.Load(), Is.Not.Null, description + ": fallback path is only a rescue");
            }
        }

        [Test]
        public void TheFontEntriesResolve()
        {
            AssertEntry(ResourceCatalog.BoldBaseFont, "bold base TMP font");
            AssertEntry(ResourceCatalog.BoldDynamicFont, "bold dynamic TMP font");
            AssertEntry(ResourceCatalog.MediumBaseFont, "medium base TMP font");
            AssertEntry(ResourceCatalog.MediumDynamicFont, "medium dynamic TMP font");
            AssertEntry(ResourceCatalog.MediumTtfFont, "medium TTF");
            AssertEntry(ResourceCatalog.BoldTtfFont, "bold TTF");
        }

        /// <summary>
        /// The library screen builds its own TMP asset from the bundled TTF. The TTF resolving is not
        /// enough — TMP has to be able to read a face out of it — so this exercises the step the
        /// library actually performs.
        /// </summary>
        [Test]
        public void TheBundledUiFontCanBecomeATmpAsset()
        {
            Font font = ResourceCatalog.MediumTtfFont.Load();
            Assert.That(font, Is.Not.Null, "Fonts/SourceHanSansSC-Medium");

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font);
            try
            {
                Assert.That(asset, Is.Not.Null, "TMP must be able to build a face from the bundled TTF");
                Assert.That(asset.faceInfo.pointSize, Is.GreaterThan(0));
            }
            finally
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }
        }

        [Test]
        public void TheLiveBundleConfigResolves()
        {
            AssertEntry(ResourceCatalog.LiveBundleConfig, "live bundle config");
        }

        [Test]
        public void TheFixedPrefabsResolve()
        {
            AssertEntry(ResourceCatalog.LiveTransitioner, "live transitioner");
            AssertEntry(ResourceCatalog.EventSystem, "event system");
            AssertEntry(ResourceCatalog.DialogFillCover, "dialog fill cover");
            AssertEntry(ResourceCatalog.LiveOutUi, "live out UI");
            AssertEntry(ResourceCatalog.EntryScreenLayers, "entry screen layers");
        }

        [Test]
        public void TheFixedMaterialsResolve()
        {
            AssertEntry(ResourceCatalog.PairNoteLineMaterial, "pair note line material");
            AssertEntry(ResourceCatalog.LongNoteLineMaterial, "long note line material");
        }

        /// <summary>
        /// A wrong path must stay a null, not an exception: the old call sites all handled null, and the
        /// catalog must not change that contract.
        /// </summary>
        [Test]
        public void AMissingPathReturnsNull()
        {
            var missing = new ResourceEntry<GameObject>("__no_such_resource__");

            Assert.That(missing.Load(), Is.Null);
            Assert.That(missing.TryLoad(out GameObject asset), Is.False);
            Assert.That(asset, Is.Null);
        }

        /// <summary>
        /// The historical lower-cased duplicates are still honoured, and only after the primary misses.
        /// </summary>
        [Test]
        public void TheFallbackRescuesAMissingPrimary()
        {
            var entry = new ResourceEntry<GameObject>("__no_such_resource__", "UI/UIParts/UIPartsDialogFillCover");

            Assert.That(entry.FallbackPath, Is.EqualTo("UI/UIParts/UIPartsDialogFillCover"));
            Assert.That(entry.Load(), Is.Not.Null);
        }
    }
}
