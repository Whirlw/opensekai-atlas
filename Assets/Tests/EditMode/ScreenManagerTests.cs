using System.Linq;
using NUnit.Framework;
using Sekai;
using UnityEngine;

namespace OpenSekai.Tests.EditMode
{
    public sealed class ScreenManagerTests
    {
        private ScreenManager _previous;
        private ScreenManager _manager;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _previous = ScreenManager.Instance;
            _root = new GameObject("ScreenManager test", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var dialogLayer = new GameObject(nameof(DisplayLayerType.Layer_Dialog), typeof(RectTransform));
            dialogLayer.transform.SetParent(_root.transform, false);
            _manager = _root.AddComponent<ScreenManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            ScreenManager.SetupInstance(_previous);
        }

        [Test]
        public void RegisteredLayersResolveTheirPrefabs()
        {
            foreach (var type in new[] { MenuScreenType.Header, MenuScreenType.TouchEffect, MenuScreenType.LiveLoading })
            {
                Assert.That(_manager.TryGetScreenLayerData(type, out var data), Is.True, type.ToString());
                Assert.That(data.LoadScreenLayerPrefab(), Is.Not.Null, type.ToString());
            }

            Assert.That(_manager.TryGetScreenLayerData(MenuScreenType.None, out var missing), Is.False);
            Assert.That(missing, Is.Null);
        }

        [Test]
        public void StackKeepsTheLatestSixteenEntriesInOrder()
        {
            for (int index = 0; index < 18; index++)
            {
                _manager.PushScreenStack(MenuScreenType.Header, index);
            }
            _manager.PushScreenStack(MenuScreenType.None);

            Assert.That(_manager.GetScreenStackList(), Has.Count.EqualTo(16));
            Assert.That(Enumerable.Range(0, _manager.UiScreenStack.Count).Select(index => (int)_manager.UiScreenStack[index].Item2), Is.EqualTo(Enumerable.Range(2, 16)));
            Assert.That(_manager.GetPeekScreenType(), Is.EqualTo(MenuScreenType.Header));
            _manager.PopScreenStack();
            Assert.That(_manager.UiScreenStack.Peek().Item2, Is.EqualTo(16));
            _manager.ClearScreenStack();
            Assert.That(_manager.GetPeekScreenType(), Is.EqualTo(MenuScreenType.None));
        }

        [Test]
        public void BackOverrideIsClearedBeforeInvokingTheCallback()
        {
            bool? wait = null;
            _manager.OnBackUIScreenOverride = value =>
            {
                Assert.That(_manager.OnBackUIScreenOverride, Is.Null);
                wait = value;
            };

            _manager.BackUIScreen(isWaitExitAnimation: true);
            Assert.That(wait, Is.EqualTo(true));
        }

        [Test]
        public void RootInputRemainsLockedUntilBothOwnersReleaseIt()
        {
            _manager.DisableTapScreen(_root);
            _manager.DisableTapScreen(_manager);
            Assert.That(_manager.CanTapByRootCanvas(), Is.False);

            _manager.EnableTapScreen(_root);
            Assert.That(_manager.CanTapByRootCanvas(), Is.False);
            _manager.EnableTapScreen(_manager);
            Assert.That(_manager.CanTapByRootCanvas(), Is.True);
        }

        [Test]
        public void ActiveDialogLookupUsesTheDialogLayerAndActiveSelf()
        {
            var parent = _manager.GetLayerObject(DisplayLayerType.Layer_Dialog).transform;
            var dialogObject = new GameObject("Test dialog");
            dialogObject.transform.SetParent(parent, false);
            var dialog = dialogObject.AddComponent<DialogBase>();
            var outside = new GameObject("Outside dialog");
            outside.transform.SetParent(_root.transform, false);
            outside.AddComponent<DialogBase>();

            Assert.That(_manager.GetActiveDialogs(), Is.EqualTo(new[] { dialog }));
            Assert.That(_manager.GetActiveDialog("Test dialog"), Is.SameAs(dialog));
            dialogObject.SetActive(false);
            Assert.That(_manager.ExistsDialog(), Is.False);
        }
    }
}
