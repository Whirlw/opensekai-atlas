using System.Collections;
using NUnit.Framework;
using Sekai.Live;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenSekai.Tests.PlayMode
{
    public sealed class AccuracyBarTests
    {
        [UnityTest]
        public IEnumerator SignedInputSamplesReachHudOnceAndResetWithTheTake()
        {
            var root = new GameObject("Live");
            try
            {
                var bar = AccuracyBarView.Create(root.transform);
                TMP_Text Count(int i) => bar.transform.Find("Count " + i).GetComponent<TMP_Text>();
                Assert.That(bar.gameObject.activeSelf, Is.False);
                bar.gameObject.SetActive(true);
                var line = new Vector2(Screen.width * 0.5f, Screen.height * 0.2f);
                var safe = new Rect(0, Screen.height * 0.02f, Screen.width, Screen.height * 0.98f);
                bar.PlaceBelowLine(line, safe, 0f);
                float low = bar.rectTransform.anchoredPosition.y;
                Vector3 size = bar.rectTransform.localScale;
                Assert.That(low - bar.rectTransform.rect.height * size.y / 2f, Is.GreaterThanOrEqualTo(safe.yMin));
                bar.PlaceBelowLine(line, safe, 1f);
                float high = bar.rectTransform.anchoredPosition.y;
                Assert.That(high, Is.GreaterThan(low));
                bar.PlaceBelowLine(line, safe, 0.5f);
                Assert.That(bar.rectTransform.anchoredPosition.y, Is.EqualTo((low + high) / 2f).Within(0.001f));
                Assert.That(bar.rectTransform.localScale, Is.EqualTo(size));
                var note = new NormalNote(new MusicScoreInfo { time = 1f }, 1, 1, 2,
                    NoteCategory.Normal, 1, NoteType.Default, 0f);
                note.OnJudgment = bar.Record;
                void Tap(float time)
                {
                    var touch = new LiveTouch { phase = UnityEngine.InputSystem.TouchPhase.Began, musicTime = time };
                    note.Judgment(ref touch, 1);
                }
                Tap(0.940f);
                Tap(0.945f); // A second finger cannot publish the same note again.
                Assert.That(Count(3).text, Is.EqualTo("1"));
                note.ResetNote();
                Assert.That(note.TimingSample, Is.Null);
                Tap(1.0208f);
                yield return null;
                Assert.That(Count(4).text, Is.EqualTo("1"));
                Assert.That(bar.canvasRenderer.GetMesh().vertexCount, Is.GreaterThan(0));
                var corners = new Vector3[4];
                bar.rectTransform.GetWorldCorners(corners);
                Assert.That(corners[1].y, Is.LessThan(line.y));
                Assert.That(corners[0].y, Is.GreaterThanOrEqualTo(0));
                note.ResetNote();
                note.AutoJudgment(new MusicScoreInfo { time = 1f });
                Assert.That(Count(4).text, Is.EqualTo("1"));
                bar.ClearSamples();
                Assert.That(Count(3).text, Is.EqualTo("0"));
            }
            finally { Object.Destroy(root); }
        }
    }
}
