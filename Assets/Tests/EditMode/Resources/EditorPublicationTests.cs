using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenSekai.EditorTools.Screenshots;
using Sekai.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;
using SceneManager = UnityEngine.SceneManagement.SceneManager;

namespace OpenSekai.Tests.EditMode
{
    public sealed class EditorPublicationTests
    {
        private string _root;

        [SetUp]
        public void SetUp() => _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),
            "opensekai-editor-" + Guid.NewGuid().ToString("N"))).FullName;

        [TearDown]
        public void TearDown() => Directory.Delete(_root, true);

        [Test]
        public void FailedBundlePublicationRestoresThePreviousCatalogAndFiles()
        {
            string staged = Directory.CreateDirectory(Path.Combine(_root, "stage")).FullName;
            string output = Directory.CreateDirectory(Path.Combine(_root, "data")).FullName;
            string backup = Path.Combine(_root, "backup");
            File.WriteAllText(Path.Combine(output, "AssetBundleInfo.bytes"), "old catalog");
            File.WriteAllText(Path.Combine(output, "old.bundle"), "playable");
            File.WriteAllText(Path.Combine(staged, "AssetBundleInfo.bytes"), "new catalog");
            var publish = typeof(OpenSekaiAssetBundleBuildPipeline).GetMethod("PublishPackagedData",
                BindingFlags.Static | BindingFlags.NonPublic);
            Action failRefresh = () => throw new IOException("Simulated import failure.");
            var error = Assert.Throws<TargetInvocationException>(() =>
                publish.Invoke(null, new object[] { staged, output, backup, failRefresh }));
            Assert.That(error.InnerException, Is.TypeOf<IOException>());
            Assert.That(File.ReadAllText(Path.Combine(output, "AssetBundleInfo.bytes")), Is.EqualTo("old catalog"));
            Assert.That(File.ReadAllText(Path.Combine(output, "old.bundle")), Is.EqualTo("playable"));
            Assert.That(Directory.Exists(backup), Is.False);
        }

        [Test]
        public void DirtySceneRejectsRepeatedScreenshotRequestsWithoutLeavingTheSessionBusy()
        {
            var previousScene = SceneManager.GetActiveScene();
            // Unity refuses to open another scene alongside an unsaved, untitled scene.
            var dirty = string.IsNullOrEmpty(previousScene.path) ? previousScene
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            bool previouslyDirty = dirty.isDirty;
            bool originalBackground = Application.runInBackground;
            try
            {
                SceneManager.SetActiveScene(dirty);
                EditorSceneManager.MarkSceneDirty(dirty);
                string request = Path.Combine(_root, "request.json");
                File.WriteAllText(request, "{}");
                for (int i = 0; i < 2; i++)
                {
                    var error = Assert.Throws<InvalidOperationException>(() => ScreenshotSession.Start(request));
                    Assert.That(error.Message, Does.Contain("unsaved"));
                    Assert.That(ScreenshotSession.IsBusy, Is.False);
                    Assert.That(Application.runInBackground, Is.EqualTo(originalBackground));
                }
            }
            finally
            {
                if (dirty != previousScene)
                {
                    SceneManager.SetActiveScene(previousScene);
                    EditorSceneManager.CloseScene(dirty, true);
                }
                else if (!previouslyDirty)
                {
                    typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, new object[] { dirty });
                }
            }
        }
    }
}
