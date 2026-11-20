#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using LitMotion;
using NUnit.Framework;
using Sekai;
using Sekai.Core.Live;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Tests.PlayMode
{
    public class LiveMotionTests
    {
        private GameObject _root;
        private ManualMotionDispatcher _clock;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Live animation test");
            _clock = new ManualMotionDispatcher();
            LiveMotion.CaptureScheduler = _clock.Scheduler;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            LiveMotion.CaptureScheduler = null;
            _clock.Reset();
        }

        [Test]
        public void DisablingAFaderCancelsThePendingCallbackAndAllowsReplay()
        {
            var sprite = _root.AddComponent<SpriteRenderer>();
            var fader = _root.AddComponent<ColorFader>();
            int completions = 0;
            fader.Play(Color.red, 1f, 0.5f, () => completions++);
            Assert.That(fader.IsPlayFading, Is.True);
            _root.SetActive(false);
            Assert.That(fader.IsPlayFading, Is.False);
            _root.SetActive(true);
            fader.Play(Color.blue, 0f, 0f, () => completions++);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(sprite.color, Is.EqualTo(Color.blue));
        }

        [Test]
        public void RepeatedTabSetupKeepsOneSelectionSubscription()
        {
            var tabs = _root.AddComponent<UIPartsDialogTabGroup>();
            for (int i = 0; i < 2; i++)
            {
                var tab = new GameObject("Tab", typeof(RectTransform));
                tab.transform.SetParent(_root.transform, false);
                tab.AddComponent<UIPartsDialogTab>();
            }
            tabs.Setup();
            tabs.Setup();
            int changes = 0;
            tabs.ToggleGroup.SelectedIndexChanged += _ => changes++;
            tabs.ToggleGroup.SelectedIndex = 1;
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(tabs.ToggleGroup.SelectedIndex, Is.EqualTo(1));
            Assert.That(_root.transform.GetChild(1).GetComponent<UIPartsDialogTab>().IsOn, Is.True);
            var field = typeof(Sekai.UI.CustomIndexToggleGroup).GetField("SelectedIndexChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var handlers = (System.Action<int>)field.GetValue(tabs.ToggleGroup);
            Assert.That(handlers.GetInvocationList(), Has.Length.EqualTo(2));
        }

        [TestCase(false, 1f)]
        [TestCase(true, 0f)]
        public void IntroRunsVisibleThenHiddenCallbacksOnOneLargeStep(bool retry, float initialAlpha)
        {
            MusicInfoView view = CreateIntro(out CanvasGroup page);
            if (retry)
            {
                view.Play();
                _clock.Update(4);
                Assert.That(view.gameObject.activeSelf, Is.False);
            }

            var phases = new List<string>();
            view.Play(onFinish: () =>
            {
                Assert.That(page.alpha, Is.Zero.Within(0.001f));
                Assert.That(view.gameObject.activeSelf, Is.False);
                phases.Add("hidden");
            }, onFinishViewGroupFade: () =>
            {
                Assert.That(page.alpha, Is.EqualTo(1f).Within(0.001f));
                Assert.That(view.gameObject.activeSelf, Is.True);
                phases.Add("visible");
            });

            Assert.That(view.gameObject.activeSelf, Is.True);
            Assert.That(page.alpha, Is.EqualTo(initialAlpha));
            _clock.Update(4);
            Assert.That(phases, Is.EqualTo(new[] { "visible", "hidden" }));
        }

        [Test]
        public void IntroContentOnlyExitStartsFromItsCurrentAlphaAndKeepsPageActive()
        {
            MusicInfoView view = CreateIntro(out CanvasGroup page);
            var jacket = new GameObject("Jacket", typeof(RectTransform), typeof(CanvasGroup));
            jacket.transform.SetParent(_root.transform, false);
            CanvasGroup group = jacket.GetComponent<CanvasGroup>();
            SetField(view, "jacketGroup", group);
            view.JacketFadeOutDelaySec = 0.25f;
            int finished = 0;

            view.Play(disableOnlyJacketOnFinish: true, onFinish: () => finished++);
            _clock.Update(0.26);
            float exitAlpha = group.alpha;
            Assert.That(exitAlpha, Is.EqualTo(0.75f).Within(0.001f));

            _clock.Update(0.225);
            Assert.That(group.alpha, Is.EqualTo(exitAlpha * 0.25f).Within(0.001f));
            _clock.Update(1);
            Assert.That(group.alpha, Is.Zero.Within(0.001f));
            Assert.That(page.alpha, Is.EqualTo(1f).Within(0.001f));
            Assert.That(view.gameObject.activeSelf, Is.True);
            Assert.That(finished, Is.Zero);
        }

        [Test]
        public void DisabledIntroCancelsItsPendingCallbacks()
        {
            MusicInfoView view = CreateIntro(out _);
            int callbacks = 0;
            view.Play(onFinish: () => callbacks++, onFinishViewGroupFade: () => callbacks++);
            _root.SetActive(false);
            _clock.Update(4);
            Assert.That(callbacks, Is.Zero);
        }

        [Test]
        public void AddScoreRestartsImmediatelyAndHidesAfterTheNewHold()
        {
            var view = _root.AddComponent<ScoreView>();
            var popup = new GameObject("Add score").transform;
            popup.SetParent(_root.transform, false);
            popup.localPosition = new Vector3(0f, 2f, 3f);
            popup.localScale = new Vector3(1f, -1f, 1f);
            SetField(view, "addScoreRoot", popup);
            view.Setup(null);
            var score = new LiveScore { totalScore = 100 };

            view.UpdateScore(ref score, 100);
            _clock.Update(0.6);
            Assert.That(popup.localPosition.x, Is.Zero.Within(0.001f));

            view.UpdateScore(ref score, 100);
            Assert.That(popup.localPosition, Is.EqualTo(new Vector3(-0.5f, 2f, 3f)));
            _clock.Update(0.1);
            Assert.That(popup.localPosition.x, Is.EqualTo(-0.125f).Within(0.001f));
            Assert.That(popup.localScale.y, Is.EqualTo(-1f));
            _clock.Update(0.1);
            Assert.That(popup.localPosition.x, Is.Zero.Within(0.001f));
            _clock.Update(0.49);
            Assert.That(popup.localScale.y, Is.EqualTo(-1f));
            _clock.Update(0.02);
            Assert.That(popup.localScale, Is.EqualTo(Vector3.zero));
        }

        private MusicInfoView CreateIntro(out CanvasGroup page)
        {
            page = _root.AddComponent<CanvasGroup>();
            var view = _root.AddComponent<MusicInfoView>();
            SetField(view, "viewGroup", page);
            view.Setup(null);
            return view;
        }

        private static void SetField<T>(T component, string field, Object value)
        {
            typeof(T).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, value);
        }
    }
}
#endif
