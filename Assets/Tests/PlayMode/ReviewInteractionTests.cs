using System.Collections;
using NUnit.Framework;
using Sekai;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using CriWare;

namespace OpenSekai.Tests.PlayMode
{
    public sealed class DialogBackInputTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator EscapeClosesDialogAndKeepsTheUnderlyingPage()
        {
            yield return new LibrarySmokeTests().LibrarySceneBootsAndShowsThePage();
            var manager = ScreenManager.Instance;
            var scene = Sekai.SceneManager.Instance.CurrentScene;
            bool sceneHandler = manager.GetSceneEnableBackKeyProcess(scene);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var dialog = new GameObject("Back key dialog").AddComponent<ReviewDialog>();
            int original = manager.UiScreenStack.Count;
            try
            {
                Assert.That(manager.IsUILayerWorking, Is.False);
                Assert.That(manager.EnableMysekaiBackKeyProcess, Is.False);
                manager.SetSceneEnableBackKeyProcess(scene, false);
                Assert.That(manager.OnExecuteBackKeyProcess, Is.Null);
                Assert.That(manager.OnBackUIScreenOverride, Is.Null);
                manager.PushScreenStack(MenuScreenType.Header);
                Assert.That(manager.UiScreenStack.Count, Is.EqualTo(original + 1));
                dialog.Open();
                yield return null;
                Assert.That(dialog.CurrentState, Is.EqualTo(DialogState.Show));
                Press(keyboard.escapeKey, queueEventOnly: true);
                yield return null;
                yield return null;
                Assert.That(manager.IsUILayerWorking, Is.False);
                Assert.That(dialog.CurrentState, Is.EqualTo(DialogState.PlayCloseAnimation));
                Assert.That(manager.UiScreenStack.Count, Is.EqualTo(original + 1));
            }
            finally
            {
                if (manager.UiScreenStack.Count > original) manager.PopScreenStack();
                manager.SetSceneEnableBackKeyProcess(scene, sceneHandler);
                InputSystem.RemoveDevice(keyboard);
                Object.Destroy(dialog.gameObject);
            }
        }
    }

    public sealed class ReviewInteractionTests
    {
        [UnityTest]
        public IEnumerator SecondPushAndBackDuringPushLeaveOneTransition()
        {
            yield return new LibrarySmokeTests().LibrarySceneBootsAndShowsThePage();
            var manager = ScreenManager.Instance;
            var original = manager.OnChangeUILayer;
            var previousBackOverride = manager.OnBackUIScreenOverride;
            int transitions = 0;
            int backCalls = 0;
            manager.OnChangeUILayer += _ => transitions++;
            try
            {
                manager.PushUIScreen(MenuScreenType.Header);
                Assert.That(manager.IsUILayerWorking, Is.True);
                manager.PushUIScreen(MenuScreenType.TouchEffect);
                manager.OnBackUIScreenOverride = _ => backCalls++;
                manager.BackUIScreen();
                Assert.That(backCalls, Is.Zero);
                float deadline = Time.realtimeSinceStartup + 4f;
                while (manager.IsUILayerWorking && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(manager.IsUILayerWorking, Is.False);
                Assert.That(transitions, Is.EqualTo(1));
                Assert.That(manager.GetCurrentUIScreenType(), Is.EqualTo(MenuScreenType.Header));
            }
            finally
            {
                manager.OnChangeUILayer = original;
                manager.OnBackUIScreenOverride = previousBackOverride;
            }
        }

        [UnityTest]
        public IEnumerator ExternalListenerTogglesReleaseAndReclaimFallback()
        {
            // Keep only the listener under test and the fallback active.
            var obj = new GameObject("Review listener");
            var external = obj.AddComponent<AudioListener>();
            var others = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            var disabled = new System.Collections.Generic.List<AudioListener>();
            foreach (var listener in others)
                if (listener != external && listener.gameObject.name != "CriAtomAudioRuntime" && listener.enabled)
                {
                    disabled.Add(listener);
                    listener.enabled = false;
                }
            try
            {
                CriAtomAudioRuntime.EnsureInitialized();
                CriAtomAudioRuntime.RefreshSceneListeners();
                external.enabled = false;
                yield return null;
                yield return null;
                var fallback = GameObject.Find("CriAtomAudioRuntime").GetComponent<AudioListener>();
                Assert.That(fallback.isActiveAndEnabled, Is.True);
                external.enabled = true;
                yield return null;
                yield return null;
                Assert.That(fallback.enabled, Is.False);
                obj.SetActive(false);
                yield return null;
                yield return null;
                Assert.That(fallback.enabled, Is.True);
                obj.SetActive(true);
                yield return null;
                yield return null;
                Assert.That(fallback.enabled, Is.False);
            }
            finally
            {
                Object.Destroy(obj);
                foreach (var listener in disabled) if (listener != null) listener.enabled = true;
            }
        }
    }

    public sealed class ReviewDialog : DialogBase
    {
        protected override void AwakeProcess() { base.AwakeProcess(); openSE = OpenSE.None; }
        protected override void CloseAnimation() { } // Keep the close state observable for the next frame.
    }
}
