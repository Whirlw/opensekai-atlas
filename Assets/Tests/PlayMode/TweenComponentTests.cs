using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using NUnit.Framework;
using Sekai;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenSekai.Tests.PlayMode
{
    public class TweenComponentTests
    {
        private GameObject _root;
        private IMotionScheduler _previousScheduler;
        private ManualMotionDispatcher _clock;

        [SetUp]
        public void SetUp()
        {
            _previousScheduler = MotionScheduler.DefaultScheduler;
            _clock = new ManualMotionDispatcher();
            MotionScheduler.DefaultScheduler = _clock.Scheduler;
            _root = new GameObject("Animation test");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            _clock.Reset();
            MotionScheduler.DefaultScheduler = _previousScheduler;
        }

        [Test]
        public void AlphaCanPauseRestartWithDelayAndForceFinishBackwards()
        {
            TweenAlpha tween = CreateAlpha(out CanvasGroup group);
            tween.Curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            tween.Delay = 0.25f;
            int completions = 0;
            tween.AddCallback(_ => completions++);

            tween.Play(TweenBase.PlayDirection.Forward);
            _clock.Update(0.125);
            AssertAlpha(group, 0f);
            _clock.Update(0.625);
            AssertAlpha(group, 0.5f);

            tween.Pause();
            _clock.Update(0.25);
            Assert.That(tween.IsActive, Is.True);
            AssertAlpha(group, 0.5f);
            tween.Resume();
            _clock.Update(0.125);
            AssertAlpha(group, 0.625f);

            tween.Restart();
            AssertAlpha(group, 0f);
            _clock.Update(0.125);
            AssertAlpha(group, 0f);
            tween.Play(TweenBase.PlayDirection.Back);
            tween.ForceFinish();
            _clock.Update(2d);
            AssertAlpha(group, 0f);
            Assert.That(tween.IsActive, Is.False);
            Assert.That(completions, Is.EqualTo(1));
        }

        [Test]
        public void PingPongReversesTheEasingAndCompletesEvenLoopAtStart()
        {
            TweenAlpha tween = CreateAlpha(out CanvasGroup group);
            tween.Loop = TweenBase.PlayLoopType.PingPong;
            tween.LoopCount = 2;
            int completions = 0;
            tween.AddCallback(_ => completions++);

            tween.Play(TweenBase.PlayDirection.Forward);
            _clock.Update(0.25);
            AssertAlpha(group, 0.4375f);
            _clock.Update(1.5);
            AssertAlpha(group, 0.4375f);
            _clock.Update(0.25);
            AssertAlpha(group, 0f);
            Assert.That(tween.IsActive, Is.False);
            Assert.That(completions, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ReferenceWaitsForMasterAndDisableHonorsThePrefabFlag()
        {
            TweenAlpha master = CreateAlpha(out CanvasGroup masterGroup);
            TweenAlpha follower = CreateAlpha(out CanvasGroup followerGroup);
            follower.Behaviour = TweenBase.BehaviourType.Reference;
            follower.ReferenceTween = master;
            followerGroup.alpha = 0.25f;
            master.DontKillIfDisable = true;

            follower.Play(TweenBase.PlayDirection.Forward);
            _clock.Update(0.5);
            AssertAlpha(followerGroup, 0.25f);
            master.gameObject.SetActive(false);
            Assert.That(master.IsActive, Is.True);
            _clock.Update(0.5);
            yield return null;
            yield return null;
            AssertAlpha(masterGroup, 1f);
            AssertAlpha(followerGroup, 1f);

            master.DontKillIfDisable = false;
            master.gameObject.SetActive(true);
            master.Play(TweenBase.PlayDirection.Back);
            master.gameObject.SetActive(false);
            _clock.Update(2d);
            Assert.That(master.IsActive, Is.False);
            AssertAlpha(masterGroup, 1f);
        }

        [UnityTest]
        public IEnumerator CancelingAnOldSlideLeavesItsReplacementRunning()
        {
            var window = new GameObject("Slide", typeof(RectTransform), typeof(ScreenSlideInOut));
            window.transform.SetParent(_root.transform);
            var rect = (RectTransform)window.transform;
            var slide = window.GetComponent<ScreenSlideInOut>();
            slide.Setup(window);
            using var cancel = new CancellationTokenSource();

            slide.SetVector(Vector3.zero, new Vector3(100f, 0f, 0f));
            UniTask first = slide.Play(cancel.Token);
            cancel.Cancel();
            slide.SetVector(new Vector3(40f, 0f, 0f), Vector3.zero);
            int completions = 0;
            slide.Play(() => completions++);

            yield return first.ToCoroutine();
            _clock.Update(0.25);
            Assert.That(rect.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(completions, Is.EqualTo(1));
        }

        private TweenAlpha CreateAlpha(out CanvasGroup group)
        {
            var target = new GameObject("Alpha", typeof(CanvasGroup));
            target.transform.SetParent(_root.transform);
            target.SetActive(false);
            group = target.GetComponent<CanvasGroup>();
            var tween = target.AddComponent<TweenAlpha>();
            tween.ComponentType = TweenBase.PlayComponentType.CanvasGroup;
            tween.StartTiming = TweenBase.PlayTiming.Manual;
            tween.To = 1f;
            target.SetActive(true);
            return tween;
        }

        private static void AssertAlpha(CanvasGroup group, float expected)
        {
            Assert.That(group.alpha, Is.EqualTo(expected).Within(0.0001f));
        }
    }
}
