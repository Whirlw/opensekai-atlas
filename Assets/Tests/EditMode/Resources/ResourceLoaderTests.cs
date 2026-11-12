using NUnit.Framework;
using Sekai;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.ResourceLoading
{
    /// <summary>
    /// The dynamic resource path: assets addressed by a key the game computes at runtime (screen layer
    /// names, view prefabs chosen by config, wording tables, atlas sprites) keep going through
    /// <see cref="ResourceLoader"/> and its <c>Resources.Load</c> / <c>LoadAll</c> / <c>LoadAsync</c>
    /// behaviour. P3-2 deliberately left those call sites alone, so these tests pin the contract they
    /// rely on: the asset comes back, a miss is a null, and the loader reports which of the two happened.
    /// </summary>
    public class ResourceLoaderTests
    {
        private const string ExistingAsset = "Fonts/SourceHanSansSC-Medium";
        private const string ExistingFolder = "Fonts";
        private const string MissingAsset = "__no_such_resource__";

        [Test]
        public void ASyncLoadReturnsTheAssetAndReportsDone()
        {
            var loader = new ResourceLoader();

            Font font = loader.LoadResource<Font>(ExistingAsset);

            Assert.That(font, Is.Not.Null);
            Assert.That(loader.Resource, Is.Not.Null);
            Assert.That(loader.AssetName, Is.EqualTo(ExistingAsset));
            Assert.That(loader.ResourceLoadState, Is.EqualTo(AssetLoader.LoadState.Done));
        }

        /// <summary>A miss must stay a null with the error state, not an exception.</summary>
        [Test]
        public void AMissingAssetReturnsNullAndReportsError()
        {
            var loader = new ResourceLoader();

            Font font = loader.LoadResource<Font>(MissingAsset);

            Assert.That(font, Is.Null);
            Assert.That(loader.Resource, Is.Null);
            Assert.That(loader.ResourceLoadState, Is.EqualTo(AssetLoader.LoadState.Error));
        }

        [Test]
        public void LoadingAFolderReturnsEveryAssetInIt()
        {
            var loader = new ResourceLoader();

            Font[] fonts = loader.LoadResourcesAll<Font>(ExistingFolder);

            Assert.That(fonts, Is.Not.Null.And.Length.GreaterThanOrEqualTo(2));
            Assert.That(loader.AllResources, Is.Not.Null.And.Count.EqualTo(fonts.Length));
            Assert.That(loader.ResourceLoadState, Is.EqualTo(AssetLoader.LoadState.Done));
        }

        /// <summary>
        /// With no host MonoBehaviour the loader cannot start a coroutine, so it resolves synchronously
        /// and still calls back once. Callers that pass a null host rely on exactly that.
        /// </summary>
        [Test]
        public void AnAsyncLoadWithoutAHostResolvesImmediately()
        {
            var loader = new ResourceLoader();
            Object received = null;
            int callbacks = 0;

            loader.LoadResourceAsync<Font>(ExistingAsset, null, asset =>
            {
                callbacks++;
                received = asset;
            });

            Assert.That(callbacks, Is.EqualTo(1), "exactly one completion callback");
            Assert.That(received, Is.Not.Null);
            Assert.That(loader.ResourceLoadState, Is.EqualTo(AssetLoader.LoadState.Done));
        }

        [Test]
        public void AnAsyncMissStillCallsBackOnceWithNull()
        {
            var loader = new ResourceLoader();
            Object received = new Object();
            int callbacks = 0;

            loader.LoadResourceAsync<Font>(MissingAsset, null, asset =>
            {
                callbacks++;
                received = asset;
            });

            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(received, Is.Null);
            Assert.That(loader.ResourceLoadState, Is.EqualTo(AssetLoader.LoadState.Error));
        }

        /// <summary>
        /// The loader keeps one asset at a time: a second load replaces the first instead of accumulating.
        /// The asset bundle path and the hot-reload path both depend on that.
        /// </summary>
        [Test]
        public void ASecondLoadReplacesTheFirst()
        {
            var loader = new ResourceLoader();

            loader.LoadResource<Font>(ExistingAsset);
            Font bold = loader.LoadResource<Font>("Fonts/SourceHanSansSC-Bold");

            Assert.That(bold, Is.Not.Null);
            Assert.That(loader.AllResources, Has.Count.EqualTo(1));
            Assert.That(loader.Resource, Is.SameAs(bold));
        }
    }

    /// <summary>
    /// Note prefabs are served from <c>Resources</c> before any bundle, because a stale note bundle is
    /// far more expensive than the megabytes it saves. The lookup has to survive the flat bundle names
    /// ("FlickNote", "longNoteLine.png") against the on-disk layout, so these pin the two pieces that
    /// make that work: the extension strip and the sub-folder candidates.
    /// </summary>
    public class NoteResourcesFallbackTests
    {
        private const string NoteBundle = "live/note/custom01";

        [Test]
        public void ANotePrefabIsServedFromResources()
        {
            GameObject prefab = AssetBundleUtility.LoadAsset<GameObject>(NoteBundle, "FlickNote");

            Assert.That(prefab, Is.Not.Null, "live/note/custom01/FlickNote.prefab");
        }

        /// <summary>Bundle callers pass the old names with an extension; Resources paths must not.</summary>
        [Test]
        public void TheExtensionIsStrippedBeforeTheLookup()
        {
            GameObject prefab = AssetBundleUtility.LoadAsset<GameObject>(NoteBundle, "FlickNote.prefab");

            Assert.That(prefab, Is.Not.Null);
        }

        /// <summary>
        /// Only the note bundle is served from Resources. Any other bundle name must not accidentally
        /// resolve through this path, or a stale asset could shadow the real bundle.
        /// </summary>
        [Test]
        public void OtherBundlesAreNotServedFromResources()
        {
            GameObject prefab = AssetBundleUtility.LoadAsset<GameObject>("live/note/__none__", "FlickNote");

            Assert.That(prefab, Is.Null);
        }
    }
}
