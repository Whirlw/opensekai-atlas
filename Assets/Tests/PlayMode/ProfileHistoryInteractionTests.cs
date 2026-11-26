using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenSekai.App.Library;
using OpenSekai.App.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenSekai.Tests.PlayMode
{
    public sealed class ProfileHistoryInteractionTests
    {
        [UnityTest]
        public IEnumerator DeletionsConfirmTwiceAndClearingAsksForABackup()
        {
            string root = Path.Combine(Path.GetTempPath(), "OpenSekai-HistoryUI-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var screen = ProfileScreen.Create(null);
            try
            {
                File.WriteAllLines(Path.Combine(root, "PlayHistory.jsonl"), new[] {
                    "{\"schemaVersion\":2,\"recordId\":\"a\",\"playedAtUtc\":\"2026-11-21\"}",
                    "{\"schemaVersion\":2,\"recordId\":\"b\",\"playedAtUtc\":\"2026-11-22\"}"
                });
                var store = new PlayerProfileStore(root);
                screen.Store = store;
                screen.SetPresented(true, false);
                screen.ShowTab(ProfileTab.History);
                Canvas.ForceUpdateCanvases();
                yield return null;
                Button ButtonNamed(string name) => screen.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
                var clear = ButtonNamed("ClearHistory");
                Transform card = screen.ModalLayer.Find(ProfileDeletePanel.ROOT_NAME);
                Button CardButton(string name) => card.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
                var delete = CardButton("Delete");
                var export = CardButton("Export");
                Assert.That(clear.interactable, Is.True);
                clear.onClick.Invoke();
                Assert.That(card.gameObject.activeSelf, Is.True, "Without a backup, clearing explains what it needs.");
                Assert.That(export.gameObject.activeSelf, Is.True);
                Assert.That(delete.gameObject.activeSelf, Is.False);
                Assert.That(screen.TryHandleBack(), Is.True);
                Assert.That(card.gameObject.activeSelf, Is.False);
                ProfileHistoryHold FirstRow() => screen.GetComponentsInChildren<ProfileHistoryHold>()
                    .OrderByDescending(row => ((RectTransform)row.transform).anchoredPosition.y).First();
                var pointer = new PointerEventData(EventSystem.current) { eligibleForClick = true };
                var row = FirstRow();
                row.OnPointerDown(pointer);
                row.OnPointerUp(pointer);
                row.OnPointerDown(pointer);
                pointer.dragging = true;
                yield return new WaitForSecondsRealtime(0.85f);
                Assert.That(screen.HistoryCount, Is.EqualTo(2));
                pointer.dragging = false;
                row.OnPointerDown(pointer);
                screen.ShowTab(ProfileTab.Best30);
                screen.ShowTab(ProfileTab.History);
                yield return new WaitForSecondsRealtime(0.85f);
                Assert.That(screen.HistoryCount, Is.EqualTo(2), "A recycled row must cancel the old press.");
                FirstRow().OnPointerDown(pointer);
                yield return new WaitForSecondsRealtime(0.85f);
                Assert.That(card.gameObject.activeSelf, Is.True, "A long press asks before deleting.");
                Assert.That(delete.gameObject.activeSelf, Is.True);
                Assert.That(export.gameObject.activeSelf, Is.False);
                delete.onClick.Invoke();
                Assert.That(screen.HistoryCount, Is.EqualTo(2), "The first tap only arms the row.");
                delete.onClick.Invoke();
                Assert.That(card.gameObject.activeSelf, Is.False);
                Assert.That(screen.HistoryCount, Is.EqualTo(1));
                Assert.That(screen.HistoryAt(0).RecordId, Is.EqualTo("a"));
                store.ExportBackup(Path.Combine(root, "player.zip"));
                screen.Reload();
                clear.onClick.Invoke();
                Assert.That(delete.gameObject.activeSelf, Is.True);
                Assert.That(export.gameObject.activeSelf, Is.False);
                delete.onClick.Invoke();
                Assert.That(screen.TryHandleBack(), Is.True);
                Assert.That(screen.HistoryCount, Is.EqualTo(1));
                clear.onClick.Invoke();
                delete.onClick.Invoke();
                Assert.That(screen.HistoryCount, Is.EqualTo(1), "Reopening the card disarms the row.");
                store.History.Submit(new CustomMusicScorePlayHistoryRecord { SchemaVersion = 2, RecordId = "new" }, null, out _);
                Assert.That(screen.HistoryCount, Is.EqualTo(2));
                Assert.That(screen.TryHandleBack(), Is.False, "Writing a new score dismisses the old clear confirmation.");
                clear.onClick.Invoke();
                Assert.That(export.gameObject.activeSelf, Is.True, "The new score is not in the backup yet.");
                screen.TryHandleBack();
                store.ExportBackup(Path.Combine(root, "player.zip"));
                screen.Reload();
                clear.onClick.Invoke();
                delete.onClick.Invoke();
                delete.onClick.Invoke();
                Assert.That(screen.HistoryCount, Is.Zero);
                Assert.That(clear.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.Destroy(screen.gameObject);
                Directory.Delete(root, true);
            }
        }
    }
}
