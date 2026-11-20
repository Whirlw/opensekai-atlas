using System.Collections.Generic;
using System.Linq;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class PairNoteLineView : MonoBehaviour
	{
		private const int POOL_COUNT = 50;

		private MeshFilter _filter;
		private Mesh _mesh;
		private Vector3[] _vertices;
		private Vector2[] _uv;
		private Color32[] _colors;
		private int[] _triangles;
		private List<(INote start, INote end, bool independentTrajectories)> _pairNotes;
		private (double start, NoteBase left, NoteBase right)[] _scheduledLines;
		private int _nextLine;

		public Material Material { get; private set; }

		public void Setup(MusicScore score = null, double displayLead = 1)
		{
			_filter = gameObject.AddComponent<MeshFilter>();
			var renderer = gameObject.AddComponent<MeshRenderer>();

			Material source = ResourceCatalog.PairNoteLineMaterial.Load();
			Material = source != null ? new Material(source) : new Material(Shader.Find("Sprites/Default"));
			renderer.sharedMaterial = Material;

			Texture texture = AssetBundleUtility.LoadAsset<Texture>(LiveConfig.NoteBundleName, "simultaneousLine.png", false);
			if (texture != null)
			{
				Material.SetTexture("_MainTex", texture);
			}

			_pairNotes = new List<(INote, INote, bool)>(POOL_COUNT);
			_scheduledLines = score?.HiddenSimLines?.Select(line =>
				(start: System.Math.Min(line.left.PrepareNativeVisual(displayLead), line.right.PrepareNativeVisual(displayLead)),
				 line.left, line.right)).OrderBy(line => line.start).ToArray();
			_mesh = new Mesh { name = name + "Mesh" };
			SetMeshCapacity(POOL_COUNT);
			_mesh.MarkDynamic();
			_filter.mesh = _mesh;
		}

		private void SetMeshCapacity(int count)
		{
			_vertices = new Vector3[count * 4];
			_uv = new Vector2[count * 4];
			_colors = new Color32[count * 4];
			_triangles = new int[count * 6];
			if (_vertices.Length > ushort.MaxValue)
				_mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			for (int i = 0; i < count; i++)
			{
				int vertexIndex = i * 4;
				int triangleIndex = i * 6;
				_uv[vertexIndex] = Vector2.zero;
				_uv[vertexIndex + 1] = Vector2.right;
				_uv[vertexIndex + 2] = Vector2.up;
				_uv[vertexIndex + 3] = Vector2.one;

				_triangles[triangleIndex] = vertexIndex;
				_triangles[triangleIndex + 1] = vertexIndex + 1;
				_triangles[triangleIndex + 2] = vertexIndex + 2;
				_triangles[triangleIndex + 3] = vertexIndex + 2;
				_triangles[triangleIndex + 4] = vertexIndex + 1;
				_triangles[triangleIndex + 5] = vertexIndex + 3;
			}

			_mesh.vertices = _vertices;
			_mesh.uv = _uv;
			_mesh.triangles = _triangles;
		}

		public void Add(INote note)
		{
			if (note is NoteBase { NativeSimLineTargets: { } targets })
			{
				foreach (var end in targets) AddAuthoredLine((NoteBase)note, end);
			}
			if (note is NoteBase { NativeSimLineSources: { } sources })
			{
				foreach (var start in sources) AddAuthoredLine(start, (NoteBase)note);
			}
			if (note == null || note.PairNote == null || note is NoteBase { IsDecoration: true })
			{
				return;
			}

			INote pair = note.PairNote;
			if (note.DefaultLeftLane >= pair.DefaultLeftLane)
			{
				return;
			}

			if (note.Category == NoteCategory.Hidden || pair.Category == NoteCategory.Hidden
				|| note.Category == NoteCategory.Connection || pair.Category == NoteCategory.Connection)
			{
				return;
			}

			if (note is NoteBase noteBase && pair is NoteBase pairBase
				&& !Mathf.Approximately(noteBase.MusicScoreInfo.time, pairBase.MusicScoreInfo.time))
			{
				return;
			}

			AddLine(note, pair, false);
		}

		private void AddAuthoredLine(NoteBase start, NoteBase end) =>
			AddLine(start, end, start.UsesIndependentSimLineTrajectories(end));

		private void AddLine(INote start, INote end, bool independentTrajectories)
		{
			var line = (start, end, independentTrajectories);
			if (!_pairNotes.Contains(line)) _pairNotes.Add(line);
		}

		public void Clear()
		{
			_pairNotes?.Clear();
			_nextLine = 0;
			ClearVertex();
			ApplyMesh();
		}

		public void Execute(float musicTime)
		{
			if (_mesh == null || _pairNotes == null)
			{
				return;
			}

			double now = (double)musicTime + LiveConfig.ViewLeadSeconds;
			while (_scheduledLines != null && _nextLine < _scheduledLines.Length && _scheduledLines[_nextLine].start <= now)
			{
				var line = _scheduledLines[_nextLine++];
				if (now <= line.left.NativeTarget.Time) AddAuthoredLine(line.left, line.right);
			}
			for (int i = 0; i < _pairNotes.Count; i++)
			{
				var (note, pair, independentTrajectories) = _pairNotes[i];
				if (independentTrajectories
					? note.State == NoteState.Done || pair.State == NoteState.Done ||
					  (double)musicTime + LiveConfig.ViewLeadSeconds > ((NoteBase)note).NativeTarget.Time
					: note.State > NoteState.Scrolling || pair.State >= NoteState.InputBegan)
				{
					_pairNotes.RemoveAt(i--);
				}
			}

			int capacity = _vertices.Length / 4;
			if (_pairNotes.Count > capacity)
			{
				capacity = Mathf.Max(_pairNotes.Count, capacity * 2);
				SetMeshCapacity(capacity);
			}
			for (int i = 0; i < capacity; i++)
			{
				int vertexIndex = i * 4;
				if (i >= _pairNotes.Count)
				{
					ClearVertex(vertexIndex);
					continue;
				}

				var (note, pair, independentTrajectories) = _pairNotes[i];
				SetAlpha(vertexIndex, 1f);
				if (independentTrajectories)
				{
					DrawAuthoredPair((NoteBase)note, (NoteBase)pair, musicTime, vertexIndex);
					continue;
				}
				if (note is NoteBase native && native.NativeScroll != null)
				{
					DrawNativePair(note, pair, musicTime, vertexIndex);
					continue;
				}
				if (note.State == NoteState.Scrolling && note.Progress > 0f)
				{
					float progress = LiveConfig.GetNoteViewProgress(LiveConfig.ViewProgressOf(note));
					if (musicTime >= note.MusicScoreInfo.time && note is LongNote)
					{
						progress = 1f;
					}

					// 根据流速正负选择不同的 spawnPosition
				bool isNegativeSpeed = note is NoteBase noteBase && noteBase.IsNegativeSpeed;
				Vector2 noteLaneCenter = LaneCenter(note);
				Vector2 pairLaneCenter = LaneCenter(pair);
				Vector2 effectiveSpawnPos = isNegativeSpeed
					? 2f * noteLaneCenter - LiveConfig.SpawnPosition // 关于判定线对称的下方起点
					: LiveConfig.SpawnPosition;
				float clampedProgress = isNegativeSpeed ? Mathf.Min(progress, 1f) : progress;
					Vector2 start = LiveUtility.EarlyVec2Lerp(effectiveSpawnPos, noteLaneCenter, clampedProgress);
					Vector2 end = LiveUtility.EarlyVec2Lerp(effectiveSpawnPos, pairLaneCenter, clampedProgress);
					float halfHeight = progress * 0.2f;
					_vertices[vertexIndex] = new Vector3(start.x, start.y + halfHeight, 0f);
					_vertices[vertexIndex + 1] = new Vector3(end.x, end.y + halfHeight, 0f);
					_vertices[vertexIndex + 2] = new Vector3(start.x, start.y - halfHeight, 0f);
					_vertices[vertexIndex + 3] = new Vector3(end.x, end.y - halfHeight, 0f);
				}
			}

			ApplyMesh();
		}

		private void DrawNativePair(INote note, INote pair, float time, int vertex)
		{
			float a = LiveConfig.ViewProgressOf(note, time);
			float b = LiveConfig.ViewProgressOf(pair, time);
			if (a <= 0 || b <= 0 || note.LaneEndF - note.LaneStartF <= -1 || pair.LaneEndF - pair.LaneStartF <= -1)
			{
				ClearVertex(vertex);
				return;
			}
			DrawStrip(LaneCenter(note), LaneCenter(pair), a, b, vertex);
		}

		private void DrawAuthoredPair(NoteBase left, NoteBase right, float time, int vertex)
		{
			double now = (double)time + LiveConfig.ViewLeadSeconds;
			float a = (float)left.NativeViewProgress(now, out bool leftHidden);
			float b = (float)right.NativeViewProgress(now, out bool rightHidden);
			float cutoff = (float)LiveConfig.NATIVE_KEY_PROGRESS_END;
			float alpha = Mathf.Clamp01(2f * (1f - Mathf.Abs(a - b)));
			if (leftHidden || rightHidden || alpha <= 0 || Mathf.Max(a, b) <= 0 || Mathf.Min(a, b) > cutoff
				|| (a - 1f) * (b - 1f) < 0 || right.IsDecoration && now >= right.NativeTarget.Time
				|| left.IsDecoration && now >= left.NativeTarget.Time
				|| left.NativeRight - left.NativeLeft <= -1 || right.NativeRight - right.NativeLeft <= -1)
			{
				ClearVertex(vertex);
				return;
			}
			float clippedA = Mathf.Clamp(a, 0, cutoff), clippedB = Mathf.Clamp(b, 0, cutoff);
			Vector2 start = LaneCenter(left), end = LaneCenter(right);
			Vector2 clippedStart = Vector2.Lerp(start, end, Mathf.InverseLerp(a, b, clippedA));
			Vector2 clippedEnd = a == b ? end : Vector2.Lerp(start, end, Mathf.InverseLerp(a, b, clippedB));
			SetAlpha(vertex, alpha);
			DrawStrip(clippedStart, clippedEnd, clippedA, clippedB, vertex);
		}

		private void SetAlpha(int vertex, float alpha)
		{
			Color32 color = new Color(1, 1, 1, alpha);
			for (int i = 0; i < 4; i++) _colors[vertex + i] = color;
		}

		private void DrawStrip(Vector2 startLane, Vector2 endLane, float a, float b, int vertex)
		{
			a = LiveConfig.GetNoteViewProgress(a);
			b = LiveConfig.GetNoteViewProgress(b);
			Vector2 start = LiveUtility.EarlyVec2Lerp(LiveConfig.SpawnPosition, startLane, a);
			Vector2 end = LiveUtility.EarlyVec2Lerp(LiveConfig.SpawnPosition, endLane, b);
			_vertices[vertex] = new Vector3(start.x, start.y + a * 0.2f, 0f);
			_vertices[vertex + 1] = new Vector3(end.x, end.y + b * 0.2f, 0f);
			_vertices[vertex + 2] = new Vector3(start.x, start.y - a * 0.2f, 0f);
			_vertices[vertex + 3] = new Vector3(end.x, end.y - b * 0.2f, 0f);
		}

		private void ClearVertex()
		{
			if (_vertices == null)
			{
				return;
			}

			for (int i = 0; i < _vertices.Length; i++)
			{
				_vertices[i] = Vector3.zero;
			}
		}

		private void ClearVertex(int vertexIndex)
		{
			if (_vertices == null || vertexIndex < 0 || vertexIndex + 3 >= _vertices.Length)
			{
				return;
			}

			_vertices[vertexIndex] = Vector3.zero;
			_vertices[vertexIndex + 1] = Vector3.zero;
			_vertices[vertexIndex + 2] = Vector3.zero;
			_vertices[vertexIndex + 3] = Vector3.zero;
		}

		private void ApplyMesh()
		{
			_mesh.vertices = _vertices;
			_mesh.colors32 = _colors;
			_filter.mesh = _mesh;
		}

		private void OnDestroy()
		{
			if (Application.isPlaying)
			{
				Destroy(_mesh);
				Destroy(Material);
			}
			else
			{
				DestroyImmediate(_mesh);
				DestroyImmediate(Material);
			}
		}

		private static Vector2 LaneCenter(INote note)
		{
			if (note is NoteBase { NativeScroll: not null })
				return LiveConfig.NativeLanePosition((note.LaneStartF + note.LaneEndF) * 0.5f);
			return (LanePosition(note.LaneStart) + LanePosition(note.LaneEnd)) * 0.5f;
		}

		private static Vector2 LanePosition(float lane)
		{
			Vector2[] positions = LiveConfig.JudgmentPositions;
			if (positions == null || positions.Length == 0)
			{
				return Vector2.zero;
			}

			float clamped = Mathf.Clamp(lane, 0f, positions.Length - 1);
			int left = Mathf.Clamp(Mathf.FloorToInt(clamped), 0, positions.Length - 1);
			int right = Mathf.Clamp(Mathf.CeilToInt(clamped), 0, positions.Length - 1);
			return LiveUtility.EarlyVec2Lerp(positions[left], positions[right], clamped - left);
		}
	}
}
