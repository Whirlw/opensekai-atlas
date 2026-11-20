using System;
using System.IO;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using Sekai;
using UnityEditor;
using UnityEngine;

namespace OpenSekai.Tests.EditMode
{
    public sealed class AssetBundleLifetimeTests
    {
        [Test]
        public async System.Threading.Tasks.Task FailedUriReadCanBeRetriedAndCancelled()
        {
            string path = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString("N"));
            string uri = new Uri(path).AbsoluteUri;
            try
            {
                try
                {
                    await AssetBundleManager.ReadUriAsync(uri, default);
                    Assert.Fail("A missing file returned a payload.");
                }
                catch (IOException error) { Assert.That(error.Message, Does.Contain(uri)); }
                File.WriteAllBytes(path, new byte[] { 1, 3, 5 });
                Assert.That(await AssetBundleManager.ReadUriAsync(uri, default), Is.EqualTo(new byte[] { 1, 3, 5 }));
                var cancelled = await AssetBundleManager.ReadUriAsync(uri, new CancellationToken(true)).SuppressCancellationThrow();
                Assert.That(cancelled.IsCanceled, Is.True);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Test]
        public void LastParentReleaseDropsItsRealMeshBundleReference()
        {
            string id = "review" + Guid.NewGuid().ToString("N");
            string assets = "Assets/" + id;
            string output = Path.Combine(Application.temporaryCachePath, id);
            var host = new GameObject(id);
            var manager = host.AddComponent<AssetManager>();
            var catalog = AssetBundleManager.Instance;
            catalog.Initialize();
            var infoField = typeof(AssetBundleManager).GetField("_clientAssetBundleInfo", BindingFlags.NonPublic | BindingFlags.Instance);
            var rootField = typeof(AssetBundleManager).GetField("_preparedCache", BindingFlags.NonPublic | BindingFlags.Instance);
            var previous = infoField.GetValue(catalog);
            var previousRoot = rootField.GetValue(catalog);
            string parent = id + "parent", child = id + "mesh";
            try
            {
                AssetDatabase.CreateFolder("Assets", id);
                Directory.CreateDirectory(output);
                string meshPath = assets + "/mesh.asset", prefabPath = assets + "/mesh.prefab";
                var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.up, Vector3.right }, triangles = new[] { 0, 1, 2 } };
                AssetDatabase.CreateAsset(mesh, meshPath);
                var go = new GameObject("parent", typeof(MeshFilter));
                try
                {
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }

                var manifest = BuildPipeline.BuildAssetBundles(output, new[]
                {
                    new AssetBundleBuild { assetBundleName = parent, assetNames = new[] { prefabPath } },
                    new AssetBundleBuild { assetBundleName = child, assetNames = new[] { meshPath } }
                }, BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);
                Assert.That(manifest.GetAllDependencies(parent), Is.EqualTo(new[] { child }));
                var info = new AssetBundleInfo();
                foreach (string name in manifest.GetAllAssetBundles())
                {
                    File.Move(Path.Combine(output, name), Path.Combine(output, name + "-fixture"));
                    info.bundles.Add(name, new AssetBundleElement { bundleName = name, cacheFileName = name,
                        hash = "fixture", dependencies = manifest.GetAllDependencies(name) });
                }
                infoField.SetValue(catalog, info);
                rootField.SetValue(catalog, output);
                var loader = manager.LoadAssetBundle(parent);
                Assert.That(loader.LoadResource<GameObject>("mesh").GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
                Assert.That(manager.LoadAssetBundle(parent), Is.SameAs(loader));
                manager.DisposeImmediate(parent);
                Assert.That(manager.GetAssetBundleReferenceCount(child), Is.EqualTo(1));
                manager.DisposeImmediate(parent);
                Assert.That(manager.GetAssetBundleReferenceCount(parent), Is.Zero);
                Assert.That(manager.GetAssetBundleReferenceCount(child), Is.Zero);
            }
            finally
            {
                manager.ForceDispose(parent);
                manager.ForceDispose(child);
                rootField.SetValue(catalog, previousRoot);
                infoField.SetValue(catalog, previous);
                UnityEngine.Object.DestroyImmediate(host);
                AssetDatabase.DeleteAsset(assets);
                if (Directory.Exists(output)) Directory.Delete(output, true);
            }
        }
    }
}
