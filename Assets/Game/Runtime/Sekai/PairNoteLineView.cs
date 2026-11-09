using System.Collections.Generic;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class PairNoteLineView : MonoBehaviour
	{
		private const int POOL_COUNT = 50;

		private Renderer _renderer;
		private MeshFilter _filter;
		private Mesh _mesh;
		private Vector3[] _vertices;
		private Vector2[] _uv;
		private int[] _triangles;
		private List<INote> _pairNotes;

		public Material Material { get; private set; }

		public void Setup()
		{
			_filter = gameObject.AddComponent<MeshFilter>();
			_renderer = gameObject.AddComponent<MeshRenderer>();

			Material source = Resources.Load<Material>("Materials/Ingame/PairNoteLine")
				?? Resources.Load<Material>("materials/ingame/PairNoteLine");
			_renderer.material = source != null ? new Material(source) : new Material(Shader.Find("Sprites/Default"));
			Material = _renderer.material;

			Texture texture = AssetBundleUtility.LoadAsset<Texture>(LiveConfig.NoteBundleName, "simultaneousLine.png", false);
			if (texture != null)
			{
				Material.SetTexture("_MainTex", texture);
			}

			_pairNotes = new List<INote>(POOL_COUNT);
			_mesh = new Mesh { name = name + "Mesh" };
			_vertices = new Vector3[POOL_COUNT * 4];
			_uv = new Vector2[POOL_COUNT * 4];
			_triangles = new int[POOL_COUNT * 6];

			for (int i = 0; i < POOL_COUNT; i++)
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
			_mesh.MarkDynamic();
			_filter.mesh = _mesh;
		}

		public void Add(INote note)
		{
			if (note == null || note.PairNote == null)
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

			_pairNotes.Add(note);
		}

		public void Clear()
		{
			_pairNotes?.Clear();
			ClearVertex();
			ApplyMesh();
		}

		public void Execute()
		{
			if (_mesh == null || _pairNotes == null)
			{
				return;
			}

			for (int i = 0; i < _pairNotes.Count; i++)
			{
				INote note = _pairNotes[i];
				if (note == null || note.State > NoteState.Playing || note.PairNote == null || note.PairNote.State >= NoteState.InputBegan)
				{
					_pairNotes.RemoveAt(i--);
				}
			}

			for (int i = 0; i < POOL_COUNT; i++)
			{
				int vertexIndex = i * 4;
				if (i >= _pairNotes.Count)
				{
					ClearVertex(vertexIndex);
					continue;
				}

				INote note = _pairNotes[i];
				if (note.State == NoteState.Playing && note.Progress > 0f)
				{
					float progress = LiveConfig.GetNoteViewProgress(note.Progress);
					if (MusicScore.CurrentFrameInfo.time >= note.MusicScoreInfo.time && note is LongNote)
					{
						progress = 1f;
					}

					// 根据流速正负选择不同的 spawnPosition
				bool isNegativeSpeed = note is NoteBase noteBase && noteBase.IsNegativeSpeed;
				Vector2 noteLaneCenter = LaneCenter(note);
				Vector2 pairLaneCenter = LaneCenter(note.PairNote);
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
			_filter.mesh = _mesh;
		}

		private static Vector2 LaneCenter(INote note)
		{
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
