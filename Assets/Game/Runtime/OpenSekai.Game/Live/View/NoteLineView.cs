using System.Collections.Generic;
using OpenSekai.Shared.Diagnostics;
using Sekai.Live;
using UnityEngine;

namespace Sekai
{
	public class NoteLineView : MonoBehaviour
	{
		private struct TwoPoint
		{
			public Vector2 Left;

			public Vector2 Right;
		}

		private struct Setting
		{
			public float CurrentViewProgress;

			public TwoPoint LanePosition;

			public float LineProgress { get; set; }
		}

		private struct Trapezoid
		{
			public TwoPoint Top;

			public TwoPoint Bottom;
		}

		private const int T_COUNT = 3;
		private const int DEFAULT_SPLIT_MESH_COUNT = 100;
		private const int DEFAULT_POOL_MULTIPLIER = 10;
		private const int TRAPEZOID_VERTEX_COUNT = 8;
		private const int TRAPEZOID_INDEX_COUNT = 18;
		private const int DEFAULT_LIST_CAPACITY = 64;

		private static readonly Color PressColor = Color.white;
		private static readonly Color ReleaseColor = new Color(0.60000002f, 0.70664f, 0.65332f, 1f);
		private static readonly Vector2[] _offsetValue =
		{
			new Vector2(0f, 0.125f),
			new Vector2(0.875f, 1f)
		};

		private int SplitMeshCount;
		private int POOL_COUNT;
		private Renderer _renderer;
		private MeshFilter _filter;
		private Mesh _mesh;
		private Vector3[] _vertices;
		private Vector2[] _uv;
		private int[] _triangles;
		private List<NoteBase> _longNotes = new List<NoteBase>(DEFAULT_LIST_CAPACITY);

		/// <summary>How many bars the draw list holds; a bar must be in it exactly once.</summary>
		internal int BarCount => _longNotes?.Count ?? 0;
		private Vector2[] _leftPoint;
		private Vector2[] _rightPoint;
		private Vector2[] _uvOffsetLeft;
		private Vector2[] _uvOffsetRight;
		private Color[] _colors;
		private Vector2 _statusValue;
		private Setting[] _meshSettingBuffer;
		private int _meshCount;
		private Trapezoid[] _trapezoidVertexInfo;
		private LiveBundleBuildData _bundleBuildData;

		public Material Material { get; private set; }

		private static int GetSplitMeshCount(bool isCustomMusicScore)
		{
			return DEFAULT_SPLIT_MESH_COUNT;
		}

		private static int GetPoolMultiplier(bool isCustomMusicScore)
		{
			return DEFAULT_POOL_MULTIPLIER;
		}

		public void Setup(LiveBundleBuildData _bundleBuildData, string textureName, float alpha, bool isCustomMusicScore = false)
		{
			SplitMeshCount = Mathf.Max(1, GetSplitMeshCount(isCustomMusicScore));
			POOL_COUNT = Mathf.Max(1, SplitMeshCount * GetPoolMultiplier(isCustomMusicScore));
			this._bundleBuildData = _bundleBuildData;

			_filter = gameObject.AddComponent<MeshFilter>();
			_renderer = gameObject.AddComponent<MeshRenderer>();

			Material source = ResourceCatalog.LongNoteLineMaterial.Load();
			Material = source != null ? new Material(source) : new Material(Shader.Find("Sprites/Default"));
			Texture texture = AssetBundleUtility.LoadAsset<Texture>(LiveConfig.NoteBundleName, textureName, false);
			if (texture != null)
			{
				Material.mainTexture = texture;
			}

			Material.color = new Color(1f, 1f, 1f, alpha);
			_renderer.material = Material;
			Material = _renderer.material;

			_longNotes = new List<NoteBase>(DEFAULT_LIST_CAPACITY);
			SetupVertexArray();

			_mesh = new Mesh { name = name + "Mesh" };
			_mesh.vertices = _vertices;
			_mesh.colors = _colors;
			_mesh.uv = _uv;
			_mesh.triangles = _triangles;
			_mesh.MarkDynamic();
			_filter.mesh = _mesh;
			_meshSettingBuffer = new Setting[SplitMeshCount + 10];
			_trapezoidVertexInfo = new Trapezoid[2];
			_meshCount = 0;
		}

		private void SetupVertexArray()
		{
			int vertexCount = POOL_COUNT * TRAPEZOID_VERTEX_COUNT;
			_vertices = new Vector3[vertexCount];
			_leftPoint = new Vector2[vertexCount];
			_rightPoint = new Vector2[vertexCount];
			_uvOffsetLeft = new Vector2[vertexCount];
			_uvOffsetRight = new Vector2[vertexCount];
			_uv = new Vector2[vertexCount];
			_colors = new Color[vertexCount];
			_triangles = new int[POOL_COUNT * TRAPEZOID_INDEX_COUNT];

			ClearVertex(0, POOL_COUNT);
			for (int meshIndex = 0; meshIndex < POOL_COUNT; meshIndex++)
			{
				int vertexBase = TRAPEZOID_VERTEX_COUNT * meshIndex;
				for (int i = 0; i < TRAPEZOID_VERTEX_COUNT; i++)
				{
					_uv[vertexBase + i] = new Vector2(0f, i & 1);
					_colors[vertexBase + i] = Color.white;
				}

				int triangleBase = TRAPEZOID_INDEX_COUNT * meshIndex;
				for (int t = 0; t < T_COUNT; t++)
				{
					int quadVertex = vertexBase + t * 2;
					int quadIndex = triangleBase + t * 6;
					_triangles[quadIndex] = quadVertex;
					_triangles[quadIndex + 1] = quadVertex + 1;
					_triangles[quadIndex + 2] = quadVertex + 2;
					_triangles[quadIndex + 3] = quadVertex + 1;
					_triangles[quadIndex + 4] = quadVertex + 3;
					_triangles[quadIndex + 5] = quadVertex + 2;
				}
			}
		}

		/// <summary>
		/// Puts a bar in the draw list, once. The scan asks for the same note every frame while it is
		/// inside its preload margin (a spawn request has to be repeatable), and a bar whose category has
		/// no note pool - a friction-hide bar, a guide bar - never reaches the dictionary the manager uses
		/// to remember it. Adding it again then stacks an identical mesh on top of it: the bar looks
		/// brighter, and the mesh budget it eats is what made the live stutter.
		/// </summary>
		public void Add(LongNote note)
		{
			if (note != null && !_longNotes.Contains(note))
			{
				_longNotes.Add(note);
			}
		}

		public void Remove(LongNote note)
		{
			if (note != null && _longNotes != null && _longNotes.Remove(note))
			{
				if (LiveTrace.IsOn(LiveTraceCategory.Pool))
				{
					LiveTrace.Write(LiveTraceCategory.Pool, "remove barT=" + note.MusicScoreInfo.time.ToString("F3") + " state=" + note.State + " result=" + note.Result);
				}
			}
		}

		public void Add(GuideNote note)
		{
			if (note != null && !_longNotes.Contains(note))
			{
				_longNotes.Add(note);
			}
		}

		public void Remove(GuideNote note)
		{
			_longNotes?.Remove(note);
		}

		public void Clear()
		{
			_longNotes?.Clear();
			ClearVertex(0, POOL_COUNT);
			ApplyMesh();
		}

		public void Excute(float time)
		{
			if (_mesh == null || _longNotes == null)
			{
				return;
			}

			// A restarted chart replays the same note objects through the pools. Anything that was
			// finished before the restart must not stay in this list: a stale bar at the front makes
			// every mesh of the new run collapse, so the bodies simply do not draw.
			for (int i = _longNotes.Count - 1; i >= 0; i--)
			{
				NoteBase note = _longNotes[i];
				if (note == null)
				{
					_longNotes.RemoveAt(i);
					continue;
				}

				if (note.State != NoteState.Done)
				{
					continue;
				}

				if (LiveTrace.IsOn(LiveTraceCategory.Pool))
				{
					LiveTrace.Write(LiveTraceCategory.Pool, "drop time=" + time.ToString("F3") + " barT=" + note.MusicScoreInfo.time.ToString("F3") + " state=" + note.State + " result=" + note.Result + " offset=" + note.offsetTime.ToString("F3"));
				}

				_longNotes.RemoveAt(i);
			}

			int previousMeshCount = _meshCount;
			_meshCount = 0;
			for (int i = 0; i < _longNotes.Count; i++)
			{
				NextExecute(_longNotes[i], time, ref _meshCount);
			}

			if (previousMeshCount > _meshCount)
			{
				ClearVertex(_meshCount, previousMeshCount - _meshCount);
			}

			ApplyMesh();
		}

		private void NextExecute(INote baseNote, float time, ref int _meshCount)
		{
			LongNote bar = baseNote as LongNote;
			List<INote> viewNotes = bar != null ? bar.LineNoteList : baseNote?.ViewNoteList;
			if (viewNotes == null || viewNotes.Count < 2)
			{
				return;
			}

			INote startNote = bar != null ? bar : viewNotes[0];
			INote tail = bar?.ChildNote;
			for (int i = 1; i < viewNotes.Count; i++)
			{
				INote endNote = viewNotes[i];
				if (endNote == null)
				{
					continue;
				}

				bool isTail = ReferenceEquals(endNote, tail);
				if (!isTail && endNote.IsSkip)
				{
					continue;
				}

				int centerPosition = GetCenterPosition(time, startNote, endNote, isTail);
				if (centerPosition < 2 && LiveTrace.IsOn(LiveTraceCategory.View))
				{
					LiveTrace.Write(LiveTraceCategory.View, "mesh time=" + time.ToString("F3") + " startT=" + startNote.MusicScoreInfo.time.ToString("F3") + " endT=" + endNote.MusicScoreInfo.time.ToString("F3") + " centerPos=" + centerPosition + " meshCount=" + _meshCount);
				}
				int nextMeshCount = _meshCount + centerPosition;
				if (POOL_COUNT < nextMeshCount)
				{
					_meshCount = nextMeshCount;
					return;
				}

				if (centerPosition >= 2)
				{
					MeshCreate(ref _meshCount, time, baseNote, centerPosition);
				}

				startNote = endNote;
				if (isTail)
				{
					break;
				}
			}
		}

		private void MeshCreate(ref int _meshCount, float time, INote baseNote, int meshDataCount)
		{
			if (meshDataCount < 2)
			{
				return;
			}

			SetStatusValue(time, baseNote);
			Color color = GetNoteColor(baseNote);
			for (int i = 0; i < meshDataCount - 1 && _meshCount < POOL_COUNT; i++)
			{
				int vertexIndex = _meshCount * TRAPEZOID_VERTEX_COUNT;
				CalculateTrapezoidVertex(_meshSettingBuffer[i], _meshSettingBuffer[i + 1]);
				UpdateVertex(vertexIndex, color);
				UpdateUV(vertexIndex, i);
				_meshCount++;
			}
		}

		/// <summary>
		/// 获取note的实际渲染颜色
		/// 优先使用自定义颜色（guideColor），否则使用默认类型颜色
		/// </summary>
		private Color GetNoteColor(INote baseNote)
		{
			// 默认颜色基于状态
			Color defaultColor = baseNote != null && baseNote.ShowsAsReleased ? ReleaseColor : PressColor;

			// 检查是否为Guide相关类型
			if (baseNote == null)
			{
				return defaultColor;
			}

			// 检查是否为Guide类型（Guide, GuideEnd, GuideHidden）
			if (baseNote.Category != NoteCategory.Guide &&
				baseNote.Category != NoteCategory.GuideEnd &&
				baseNote.Category != NoteCategory.GuideHidden)
			{
				return defaultColor;
			}

			// 尝试获取自定义颜色
			if (baseNote is LongNote longNote)
			{
				string guideColor = longNote.guideColor;
				if (!string.IsNullOrEmpty(guideColor))
				{
					if (UnityEngine.ColorUtility.TryParseHtmlString(guideColor, out Color customColor))
					{
						return customColor;
					}
				}
			}

			return defaultColor;
		}

		private void UpdateUV(int vertexIndex, int meshDataIndex)
		{
			float startLineProgress = _meshSettingBuffer[meshDataIndex].LineProgress;
			float endLineProgress = _meshSettingBuffer[meshDataIndex + 1].LineProgress;
			for (int offset = 0; offset <= 4; offset += 4)
			{
				_uv[vertexIndex + offset + 3].y = endLineProgress;
				_uv[vertexIndex + offset + 1].y = endLineProgress;
				_uv[vertexIndex + offset + 2].y = startLineProgress;
				_uv[vertexIndex + offset].y = startLineProgress;
			}
		}

		private void ClearVertex(int startIndex, int length)
		{
			if (_vertices == null)
			{
				return;
			}

			int start = Mathf.Max(0, startIndex * TRAPEZOID_VERTEX_COUNT);
			int end = Mathf.Min(_vertices.Length, start + Mathf.Max(0, length) * TRAPEZOID_VERTEX_COUNT);
			for (int i = start; i < end; i++)
			{
				_vertices[i] = Vector3.zero;
				_colors[i] = Color.clear;
			}
		}

		private void SetStatusValue(float time, INote baseNote)
		{
			float animationRate = 0f;
			if (baseNote != null && baseNote.State > NoteState.InputBegan)
			{
				float phase = (time - baseNote.MusicScoreInfo.time) * Mathf.PI * 2f;
				animationRate = 0.5f - Mathf.Cos(phase * 2f) * 0.5f;
			}

			_statusValue.x = animationRate;
			_statusValue.y = baseNote != null && baseNote.Type == NoteType.Critical ? 0.025f : 0.525f;
		}

		private void CalculateTrapezoidVertex(Setting startSetting, Setting endSetting)
		{
			_trapezoidVertexInfo[0].Top.Right = endSetting.LanePosition.Left;
			_trapezoidVertexInfo[0].Top.Left = endSetting.LanePosition.Left;
			_trapezoidVertexInfo[0].Bottom.Right = startSetting.LanePosition.Left;
			_trapezoidVertexInfo[0].Bottom.Left = startSetting.LanePosition.Left;
			_trapezoidVertexInfo[0].Top.Left.x -= endSetting.CurrentViewProgress * 0.4f;
			_trapezoidVertexInfo[0].Bottom.Left.x -= startSetting.CurrentViewProgress * 0.4f;

			_trapezoidVertexInfo[1].Top.Right = endSetting.LanePosition.Right;
			_trapezoidVertexInfo[1].Top.Left = endSetting.LanePosition.Right;
			_trapezoidVertexInfo[1].Bottom.Right = startSetting.LanePosition.Right;
			_trapezoidVertexInfo[1].Bottom.Left = startSetting.LanePosition.Right;
			_trapezoidVertexInfo[1].Top.Right.x += endSetting.CurrentViewProgress * 0.4f;
			_trapezoidVertexInfo[1].Bottom.Right.x += startSetting.CurrentViewProgress * 0.4f;
		}

		private void UpdateVertex(int vertexIndex, Color color)
		{
			for (int t = 0; t < 2; t++)
			{
				int offset = vertexIndex + t * 4;
				Trapezoid trapezoid = _trapezoidVertexInfo[t];
				_vertices[offset + 3] = ToVector3(trapezoid.Top.Right);
				_vertices[offset + 1] = ToVector3(trapezoid.Top.Left);
				_vertices[offset + 2] = ToVector3(trapezoid.Bottom.Right);
				_vertices[offset] = ToVector3(trapezoid.Bottom.Left);

				_leftPoint[offset + 3].y = trapezoid.Top.Left.x;
				_leftPoint[offset + 1].y = trapezoid.Top.Left.x;
				_leftPoint[offset + 2].y = trapezoid.Bottom.Left.x;
				_leftPoint[offset].y = trapezoid.Bottom.Left.x;

				_rightPoint[offset + 3].y = trapezoid.Top.Right.x;
				_rightPoint[offset + 1].y = trapezoid.Top.Right.x;
				_rightPoint[offset + 2].y = trapezoid.Bottom.Right.x;
				_rightPoint[offset].y = trapezoid.Bottom.Right.x;

				Vector2 offsetRange = _offsetValue[t];
				for (int i = 0; i < 4; i++)
				{
					int index = offset + i;
					_uvOffsetLeft[index] = new Vector2(_statusValue.x, offsetRange.x);
					_uvOffsetRight[index] = new Vector2(_statusValue.y, offsetRange.y);
					_colors[index] = color;
				}
			}
		}

		private int GetCenterPosition(float time, INote startNote, INote endNote, bool isTail)
		{
			if (startNote is not NoteBase startNoteBase || endNote is not NoteBase endNoteBase)
			{
				return 0;
			}

			float startOffset = startNoteBase.CalcTimeOffset(startNoteBase);
			// The final segment follows the chart's scroll timing even after its key stops ticking.
			float endOffset = isTail
				? endNoteBase.CalcTimeOffset(endNoteBase)
				: OpenSekai.Shared.Live.NoteLineProgress.SampleOffset(endNoteBase);
			float duration = endNote.MusicScoreInfo.time - startNote.MusicScoreInfo.time;
			if (Mathf.Approximately(startOffset, 0f) || Mathf.Approximately(endOffset, 0f) || Mathf.Approximately(duration, 0f))
			{
				if (LiveTrace.IsOn(LiveTraceCategory.View))
				{
					LiveTrace.Write(LiveTraceCategory.View, "noLine time=" + time.ToString("F3") + " startT=" + startNote.MusicScoreInfo.time.ToString("F3") + " endT=" + endNote.MusicScoreInfo.time.ToString("F3") + " startOffset=" + startOffset.ToString("F4") + " endOffset=" + endOffset.ToString("F4") + " duration=" + duration.ToString("F3"));
				}

				return 0;
			}

			// The body always ends on the judgement line. Only the head key may travel past it, and
			// that decision belongs to the bar's own view, not to the mesh: an unclamped start here
			// both drags the body below the line and inflates the split count past the buffer, which
			// leaves the piece just before the tail key undrawn.
			float startProgress = OpenSekai.Shared.Live.NoteLineProgress.Body(startOffset, time, startNote.MusicScoreInfo.time);
			float endProgress = OpenSekai.Shared.Live.NoteLineProgress.Body(endOffset, time, endNote.MusicScoreInfo.time);
			float progressRange = startProgress - endProgress;
			int splitCount = Mathf.CeilToInt(progressRange * SplitMeshCount);
			if (splitCount < 0)
			{
				return splitCount + 1;
			}

			List<INote> viewNotes = startNote.ParentNote is LongNote bar ? bar.LineNoteList : startNote.ViewNoteList;
			int viewIndex = viewNotes != null ? Mathf.Max(0, viewNotes.IndexOf(startNote)) : 0;
			int viewLastIndex = Mathf.Max(1, (viewNotes?.Count ?? 2) - 1);
			float startCenterLane = (startNote.DefaultLeftLane + startNote.DefaultRightLane) * 0.5f;
			float endCenterLane = (endNote.DefaultLeftLane + endNote.DefaultRightLane) * 0.5f;
			float startWidth = startNote.DefaultRightLane - startNote.DefaultLeftLane;
			float endWidth = endNote.DefaultRightLane - endNote.DefaultLeftLane;
			float startLineProgress = 1f - Mathf.Clamp01((endNote.MusicScoreInfo.time - time) / duration);
			float endLineProgress = 1f - Mathf.Clamp01((endNote.MusicScoreInfo.time - time - endOffset) / duration);

			int sampleCount = splitCount + 1;
			for (int i = 0; i < sampleCount && i < _meshSettingBuffer.Length; i++)
			{
				float rate = splitCount <= 0 ? 0f : (float)i / splitCount;
				float lineProgress = Mathf.Min(Mathf.Lerp(startLineProgress, endLineProgress, rate), endLineProgress);
				float centerProgress = LiveConfig.GetNoteLineCenterProgress(lineProgress, startNote);
				float rawProgress = Mathf.Lerp(startProgress, endProgress, rate);
				float viewProgress = OpenSekai.Shared.Live.NoteLineProgress.View(rawProgress);
				float centerLane = Mathf.Lerp(startCenterLane, endCenterLane, centerProgress);
				float width = Mathf.Lerp(startWidth, endWidth, centerProgress);
				Vector2 lanePosition = GetLanePosition(centerLane);
				// 根据流速正负选择不同的 spawnPosition（startNote 可能是 LongNote 本身，也可能是子音符）
				bool isNegativeSpeed = startNoteBase.IsNegativeSpeed
					|| (startNote.ParentNote is NoteBase parentNote && parentNote.IsNegativeSpeed);
				Vector2 effectiveSpawnPos = isNegativeSpeed
					? 2f * lanePosition - LiveConfig.SpawnPosition // 关于判定线对称的下方起点
					: LiveConfig.SpawnPosition;
				float clampedViewProgress = isNegativeSpeed ? Mathf.Min(viewProgress, 1f) : viewProgress;
				Vector2 viewPosition = LiveUtility.EarlyVec2Lerp(effectiveSpawnPos, lanePosition, clampedViewProgress);
				float halfWidth = viewProgress * (((width + 1f) * LiveConfig.widthX) - 0.1f) * 0.5f;

				_meshSettingBuffer[i] = new Setting
				{
					CurrentViewProgress = viewProgress,
					LanePosition = new TwoPoint
					{
						Left = new Vector2(viewPosition.x - halfWidth, viewPosition.y),
						Right = new Vector2(viewPosition.x + halfWidth, viewPosition.y)
					},
					LineProgress = (lineProgress + viewIndex) / viewLastIndex
				};
			}

			return Mathf.Min(sampleCount, _meshSettingBuffer.Length);
		}

		private static Vector2 GetLanePosition(float lane)
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

		private static Vector3 ToVector3(Vector2 value)
		{
			return new Vector3(value.x, value.y, 0f);
		}

		private void ApplyMesh()
		{
			if (_mesh == null)
			{
				return;
			}

			_mesh.vertices = _vertices;
			_mesh.colors = _colors;
			_mesh.uv = _uv;
			_mesh.uv2 = _leftPoint;
			_mesh.uv3 = _rightPoint;
			_mesh.uv4 = _uvOffsetLeft;
			_mesh.uv5 = _uvOffsetRight;
			_filter.mesh = _mesh;
		}
	}
}
