using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenSekai.App.Library;
using OpenSekai.App.UI;
using OpenSekai.Shared.Playback;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenSekai.Tests.PlayMode
{
    /// <summary>The 成就 page under a finger: unlocking with a tap, turning levels, scrolling the list and opening a reward.</summary>
    public sealed class AchievementInteractionTests
    {
        private string _root;
        private GameObject _events;
        private PlayerProfileStore _store;
        private AchievementScreen _screen;
        private float _volume;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _volume = UiSound.Volume;
            UiSound.Volume = 0f;
            _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "Achievements-" + Guid.NewGuid().ToString("N"))).FullName;
            _store = new PlayerProfileStore(_root);
            for (int i = 0; i < 4; i++) _store.History.Submit(FullCombo(i), null, out _);
            if (EventSystem.current == null) _events = new GameObject("EventSystem", typeof(EventSystem));
            _screen = AchievementScreen.Create();
            _screen.Store = _store;
            _screen.SetPresented(true, false);
            yield return null;
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.Destroy(_screen.gameObject);
            if (_events != null) UnityEngine.Object.Destroy(_events);
            UiSound.Volume = _volume;
            Directory.Delete(_root, true);
        }

        [UnityTest]
        public IEnumerator ATapUnlocksTheLevelThenTheRowTurnsAndTheListSortsItself()
        {
            AchievementRow row = _screen.View.FindRow("fc_lv25");
            AchievementRow other = _screen.View.FindRow("manual_hits_10000");
            Assert.That(row.Top, Is.Zero);
            Assert.That(Text("Count"), Is.EqualTo("0"));
            Press(Center(row.Root));
            Assert.That(row.IsUnlocking, Is.True);
            yield return Until(() => !row.IsUnlocking, 5f);
            Assert.That((row.Step, row.StarCount), Is.EqualTo((1, 2)));
            Assert.That(Text("Count"), Is.EqualTo("1"));
            Assert.That(Text("FilterCount"), Is.EqualTo("已解锁 1 / 9"));
            yield return Until(() => other.Top <= 0f, 2f);
            Assert.That(row.Top, Is.GreaterThan(0f), "under way again, it goes back to its place");
            Assert.That(_store.ReadMedals().Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ASwipeTurnsToAnUnlockedLevelAndAVerticalDragScrolls()
        {
            _store.UnlockAchievement("fc_lv25");
            _screen.Reload();
            yield return null;
            AchievementRow row = _screen.View.FindRow("fc_lv25");
            Assert.That(row.Step, Is.EqualTo(1));
            Vector2 center = Center(row.Root);
            Drag(center, center + new Vector2(Screen.width * 0.3f, 4f));
            // The star of the level on show is the large one.
            yield return Until(() => Star(row, 0).localScale.x > 1.49f && Star(row, 1).localScale.x < 1.01f, 2f);
            Assert.That(row.Step, Is.Zero);
            Drag(center, center - new Vector2(Screen.width * 0.3f, 4f));
            yield return Until(() => Star(row, 1).localScale.x > 1.49f && Star(row, 0).localScale.x < 1.01f, 2f);
            Assert.That(row.Step, Is.EqualTo(1));

            ScrollRect list = row.Root.GetComponentInParent<ScrollRect>();
            PointerEventData drag = Begin(center, out GameObject target);
            Move(drag, target, center + new Vector2(1f, 20f));
            Move(drag, target, center + new Vector2(4f, Screen.height * 0.2f));
            Assert.That(Mathf.Abs(list.content.anchoredPosition.y), Is.GreaterThan(10f));
            End(drag, target);
            Assert.That(row.Step, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ATapOnTheBoxShowsTheRewardAndUnlocksNothing()
        {
            AchievementRow row = _screen.View.FindRow("fc_lv25");
            RectTransform box = row.Root.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "RewardBox");
            Press(Center(box));
            yield return null;
            Assert.That(row.IsUnlocking, Is.False);
            Assert.That(_screen.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "AchievementReward"), Is.True);
            Assert.That(_screen.TryHandleBack(), Is.True);
            Assert.That(_screen.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "AchievementReward"), Is.False);
            Assert.That(_store.ReadMedals(), Is.Empty);
        }

        private static Transform Star(AchievementRow row, int index) => row.Root.Find("Inner/Stars/Strips/Star" + index);

        private string Text(string name) => _screen.GetComponentsInChildren<TextMeshProUGUI>(true).Single(label => label.name == name).text;

        private Vector2 Center(RectTransform rect)
        {
            Camera camera = rect.GetComponentInParent<Canvas>().worldCamera;
            return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        }

        private static void Press(Vector2 position)
        {
            PointerEventData pointer = At(position);
            GameObject target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(pointer.pointerPressRaycast.gameObject);
            Assert.That(target, Is.Not.Null, "something takes the tap");
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void Drag(Vector2 from, Vector2 to)
        {
            PointerEventData pointer = Begin(from, out GameObject target);
            for (int i = 1; i <= 4; i++) Move(pointer, target, Vector2.Lerp(from, to, i / 4f));
            End(pointer, target);
        }

        private static PointerEventData Begin(Vector2 position, out GameObject target)
        {
            PointerEventData pointer = At(position);
            target = ExecuteEvents.GetEventHandler<IDragHandler>(pointer.pointerPressRaycast.gameObject);
            Assert.That(target, Is.Not.Null);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.initializePotentialDrag);
            pointer.dragging = false;
            return pointer;
        }

        private static void Move(PointerEventData pointer, GameObject target, Vector2 position)
        {
            pointer.position = position;
            if (!pointer.dragging)
            {
                pointer.dragging = true;
                ExecuteEvents.Execute(target, pointer, ExecuteEvents.beginDragHandler);
            }

            ExecuteEvents.Execute(target, pointer, ExecuteEvents.dragHandler);
        }

        private static void End(PointerEventData pointer, GameObject target)
        {
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.endDragHandler);
            pointer.dragging = false;
        }

        private static PointerEventData At(Vector2 position)
        {
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = position, pressPosition = position, button = PointerEventData.InputButton.Left, eligibleForClick = true
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, "nothing under " + position);
            pointer.pointerCurrentRaycast = hits[0];
            pointer.pointerPressRaycast = hits[0];
            return pointer;
        }

        private static IEnumerator Until(Func<bool> done, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), "timed out");
                yield return null;
            }
        }

        private static CustomMusicScorePlayHistoryRecord FullCombo(int n) => new CustomMusicScorePlayHistoryRecord
        {
            SchemaVersion = 3, RecordId = "fc-" + n, ScoreKey = "chart-" + n,
            PlayedAtUtc = new DateTimeOffset(2026, 11, 1, n, 0, 0, TimeSpan.Zero).ToString("O"),
            EndReason = PlayResultKind.Completed, Life = 100, PlayLevel = 26 + n, MaximumScore = 1000000, FormulaVersion = 1,
            TotalScore = 990000, TotalComboCount = 500, MaxCombo = 500, PerfectCount = 490, GreatCount = 10, IsFullCombo = true
        };
    }
}
