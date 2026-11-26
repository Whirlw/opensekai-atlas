using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.Live
{
    /// <summary>Signed input offsets on a common scale, with recent hits above the rail and song totals below.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class AccuracyBarView : MaskableGraphic
    {
        private const float HalfWidth = 180f;
        private const float Height = 64f;
        private const int HistorySize = 24;
        private static readonly Color32[] RailColors =
        {
            new(101, 240, 119, 255), new(234, 248, 96, 255), new(255, 165, 92, 255),
            new(254, 91, 85, 255), new(211, 126, 243, 255)
        };

        private readonly float[] _history = new float[HistorySize];
        private readonly int[] _counts = new int[9];
        private readonly TextMeshProUGUI[] _labels = new TextMeshProUGUI[9];
        private readonly float[] _edges = new float[5];
        private int _samples;
        private GameObject _canvasRoot;

        internal static AccuracyBarView Create(Transform parent)
        {
            var root = new GameObject("Accuracy HUD", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(parent, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var go = new GameObject("Timing distribution", typeof(RectTransform), typeof(AccuracyBarView));
            go.transform.SetParent(root.transform, false);
            var bar = go.GetComponent<AccuracyBarView>();
            bar._canvasRoot = root;
            bar.raycastTarget = false;
            bar.rectTransform.anchorMin = bar.rectTransform.anchorMax = Vector2.zero;
            bar.rectTransform.sizeDelta = new Vector2(2 * HalfWidth + 44, Height);
            var window = LiveConfig.GetJudgeWindow(JudgeFrameType.Normal);
            bar._edges[0] = window.PerfectAfterJudgeTime * 1000f;
            bar._edges[1] = window.GreatAfterJudgeTime * 1000f;
            bar._edges[2] = window.GoodAfterJudgeTime * 1000f;
            bar._edges[3] = window.BadAfterJudgeTime * 1000f;
            bar._edges[4] = bar._edges[3] + 1000f / 60f;

            var font = ResourceCatalog.BoldBaseFont.Load();
            for (int i = 0; i < bar._labels.Length; i++)
                bar._labels[i] = bar.Counter(i, font);
            bar.ClearSamples();
            go.SetActive(false);
            return bar;
        }

        private TextMeshProUGUI Counter(int index, TMP_FontAsset font)
        {
            var go = new GameObject("Count " + index, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Center;
            text.color = RailColors[Math.Abs(index - 4)];
            text.enableAutoSizing = true;
            text.fontSizeMin = 10;
            text.fontSizeMax = 17f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(44f, 24f);
            text.rectTransform.anchoredPosition = new Vector2((index - 4) * HalfWidth / 4, -23f);
            // A dark outline keeps small counters legible over bright jackets and videos.
            var outline = go.AddComponent<Shadow>();
            outline.effectColor = new Color(0, 0, 0, 0.9f);
            outline.effectDistance = new Vector2(1, -1);
            return text;
        }

        internal void PlaceBelowLine(Vector2 screenLine, Rect safe, float height)
        {
            float top = Mathf.Min(screenLine.y, safe.yMax);
            float available = Mathf.Max(0f, top - safe.yMin);
            float scale = Mathf.Min(Screen.height / 900f, safe.width / (2f * HalfWidth + 48f));
            scale = Mathf.Min(scale, available / (Height + 12f));
            float half = rectTransform.sizeDelta.x * scale * 0.5f;
            rectTransform.localScale = Vector3.one * scale;
            float inset = (Height / 2 + 6f) * scale;
            float centerY = Mathf.Lerp(safe.yMin + inset, top - inset, height);
            rectTransform.anchoredPosition = new Vector2(Mathf.Clamp(screenLine.x, safe.xMin + half, safe.xMax - half), centerY);
        }

        internal void Remove() => Destroy(_canvasRoot);

        internal void ClearSamples()
        {
            _samples = 0;
            Array.Clear(_counts, 0, _counts.Length);
            foreach (var label in _labels) label.text = "0";
            SetVerticesDirty();
        }

        internal void Record(NoteBase note)
        {
            if (note?.TimingSample is not float sample || !note.HasJudgment || note.IsSkip
                || note.Result is NoteResult.None or NoteResult.Auto or NoteResult.Pass or NoteResult.Miss) return;

            float ms = (sample - note.MusicScoreInfo.time) * 1000f;
            _history[_samples % HistorySize] = ms;
            _samples++;
            int bin = 4 + Math.Sign(ms) * Band(ms);
            _labels[bin].SetText("{0}", ++_counts[bin]);
            SetVerticesDirty();
        }

        private int Band(float ms)
        {
            float distance = Mathf.Abs(ms);
            for (int i = 0; i < _edges.Length - 1; i++)
                if (distance <= _edges[i]) return i;
            return _edges.Length - 1;
        }

        private float X(float ms) => HalfWidth * Mathf.Clamp(ms / _edges[4], -1f, 1f);

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (_edges[4] == 0f) return;
            Quad(mesh, -HalfWidth - 2, 1, HalfWidth + 2, 12, new Color32(17, 19, 27, 210));
            float previous = 0f;
            for (int i = 0; i < _edges.Length; i++)
            {
                float edge = X(_edges[i]);
                Quad(mesh, -edge, 3, -previous, 10, RailColors[i]);
                Quad(mesh, previous, 3, edge, 10, RailColors[i]);
                previous = edge;
            }
            Quad(mesh, -1.25f, -1, 1.25f, 14, Color.white);
            int count = Math.Min(_samples, HistorySize);
            for (int age = count - 1; age >= 0; age--)
            {
                float ms = _history[(_samples - 1 - age) % HistorySize];
                float x = X(ms);
                Color color = RailColors[Band(ms)];
                color.a = 1f - age * 0.8f / (HistorySize - 1);
                Quad(mesh, x - 1.6f, 16, x + 1.6f, 26, color);
            }
            if (count == 0) return;
            float last = X(_history[(_samples - 1) % HistorySize]);
            int v = mesh.currentVertCount;
            mesh.AddVert(new Vector3(last - 5, -10), Color.white, Vector2.zero);
            mesh.AddVert(new Vector3(last, -2), Color.white, Vector2.zero);
            mesh.AddVert(new Vector3(last + 5, -10), Color.white, Vector2.zero);
            mesh.AddTriangle(v, v + 1, v + 2);
        }

        private static void Quad(VertexHelper mesh, float left, float bottom, float right, float top, Color32 tint)
        {
            int v = mesh.currentVertCount;
            mesh.AddVert(new Vector3(left, bottom), tint, Vector2.zero);
            mesh.AddVert(new Vector3(left, top), tint, Vector2.zero);
            mesh.AddVert(new Vector3(right, top), tint, Vector2.zero);
            mesh.AddVert(new Vector3(right, bottom), tint, Vector2.zero);
            mesh.AddTriangle(v, v + 1, v + 2);
            mesh.AddTriangle(v + 2, v + 3, v);
        }
    }
}
