using System;
using System.Collections.Generic;
using OpenSekai.Shared.Diagnostics;
using Sekai.Live;
using UnityEngine;
using UnityEngine.Rendering;

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
			public float Alpha;

			public TwoPoint LanePosition;

			public float LineProgress { get; set; }
		}

		private struct Trapezoid
		{
			public TwoPoint Top;

			public TwoPoint Bottom;
		}

		private sealed class LayerBatch
		{
			public Mesh Mesh;
			public MeshRenderer Renderer;
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
		private int _meshCapacity;
		private readonly LayerBatch[] _batches = new LayerBatch[4];
		private Camera _viewCamera;
		private bool _guideView;
		private bool _hasViewport;
		private Vector3 _viewportBottomLeft, _viewportTopLeft, _viewportBottomRight, _viewportTopRight;
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

		public Material Material { get; private set; }

		private static int GetSplitMeshCount(bool isCustomMusicScore)
		{
			return DEFAULT_SPLIT_MESH_COUNT;
		}

		private static int GetPoolMultiplier(bool isCustomMusicScore)
		{
			return DEFAULT_POOL_MULTIPLIER;
		}

		public void Setup(LiveBundleBuildData _bundleBuildData, string textureName, float alpha, bool isCustomMusicScore = false,
			Camera viewCamera = null, bool guide = false)
		{
			SplitMeshCount = Mathf.Max(1, GetSplitMeshCount(isCustomMusicScore));
			_viewCamera = viewCamera;
			_guideView = guide;

			Material source = ResourceCatalog.LongNoteLineMaterial.Load();
			Material = source != null ? new Material(source) : new Material(Shader.Find("Sprites/Default"));
			Texture texture = AssetBundleUtility.LoadAsset<Texture>(LiveConfig.NoteBundleName, textureName, false);
			if (texture != null)
			{
				Material.mainTexture = texture;
			}

			Material.color = new Color(1f, 1f, 1f, alpha);

			_longNotes = new List<NoteBase>(DEFAULT_LIST_CAPACITY);
			EnsureMeshCapacity(SplitMeshCount * GetPoolMultiplier(isCustomMusicScore));

			_batches[(int)NextRushConnectorLayer.Top] = CreateBatch(gameObject);
			_meshSettingBuffer = new Setting[SplitMeshCount + 10];
			_trapezoidVertexInfo = new Trapezoid[2];
			_meshCount = 0;
		}

		private LayerBatch CreateBatch(GameObject host)
		{
			var mesh = new Mesh { name = host.name + "Mesh" };
			mesh.MarkDynamic();
			host.AddComponent<MeshFilter>().sharedMesh = mesh;
			var renderer = host.AddComponent<MeshRenderer>();
			renderer.sharedMaterial = Material;
			renderer.enabled = false;
			return new LayerBatch { Mesh = mesh, Renderer = renderer };
		}

		private void EnsureMeshCapacity(int required)
		{
			if (required <= _meshCapacity) return;

			// Dense Guide layers can exceed the initial capacity. Reuse the grown buffers next frame.
			int capacity = Mathf.Max(required, _meshCapacity * 2);
			int vertexCount = capacity * TRAPEZOID_VERTEX_COUNT;
			Array.Resize(ref _vertices, vertexCount);
			Array.Resize(ref _leftPoint, vertexCount);
			Array.Resize(ref _rightPoint, vertexCount);
			Array.Resize(ref _uvOffsetLeft, vertexCount);
			Array.Resize(ref _uvOffsetRight, vertexCount);
			Array.Resize(ref _uv, vertexCount);
			Array.Resize(ref _colors, vertexCount);
			Array.Resize(ref _triangles, capacity * TRAPEZOID_INDEX_COUNT);

			for (int meshIndex = _meshCapacity; meshIndex < capacity; meshIndex++)
			{
				int vertexBase = TRAPEZOID_VERTEX_COUNT * meshIndex;
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
			_meshCapacity = capacity;
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
			if (note != null && note.NativeConnectorDisplayEnd > double.NegativeInfinity) return;
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
			Remove((LongNote)note);
		}

		public void Clear()
		{
			_longNotes?.Clear();
			foreach (var batch in _batches)
			{
				if (batch == null) continue;
				batch.Renderer.enabled = false;
				batch.Mesh.Clear();
			}
		}

		public void Excute(float time)
		{
			if (_batches[(int)NextRushConnectorLayer.Top] == null || _longNotes == null)
			{
				return;
			}

			// A restarted chart replays the same note objects through the pools. Anything that was
			// finished before the restart must not stay in this list: a stale bar at the front makes
			// every mesh of the new run collapse, so the bodies simply do not draw.
			int layers = 0;
			bool native = false;
			for (int i = _longNotes.Count - 1; i >= 0; i--)
			{
				NoteBase note = _longNotes[i];
				if (note == null)
				{
					_longNotes.RemoveAt(i);
					continue;
				}

				if (note.State != NoteState.Done
					|| note is LongNote bar && (double)time + LiveConfig.ViewLeadSeconds < bar.NativeConnectorDisplayEnd)
				{
					native |= note.NativeScroll != null;
					layers |= LayerMaskFor(note);
					continue;
				}

				if (LiveTrace.IsOn(LiveTraceCategory.Pool))
				{
					LiveTrace.Write(LiveTraceCategory.Pool, "drop time=" + time.ToString("F3") + " barT=" + note.MusicScoreInfo.time.ToString("F3") + " state=" + note.State + " result=" + note.Result + " offset=" + note.offsetTime.ToString("F3"));
				}

				_longNotes.RemoveAt(i);
			}

			_hasViewport = false;
			for (int layer = 0; layer < _batches.Length; layer++)
			{
				int layerBit = 1 << layer;
				_meshCount = 0;
				if ((layers & layerBit) != 0)
				{
					foreach (var note in _longNotes)
						if ((LayerMaskFor(note) & layerBit) != 0)
							NextExecute(note, time, (NextRushConnectorLayer)layer);
				}
				ApplyMesh((NextRushConnectorLayer)layer, native);
			}
		}

		private int LayerMaskFor(NoteBase note) => note.NativeScroll == null ? 1
			: (note as LongNote)?.NativeConnectorLayers(_guideView) ?? 0;

		private void NextExecute(INote baseNote, float time, NextRushConnectorLayer layer)
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

				var segment = (startNote as NoteBase)?.NativeConnector;
				if (segment == null || (segment.Layer == layer && segment.IsGuide == _guideView))
				{
					if (segment?.FullScreen == true)
					{
						DrawFullScreen(time, bar, (NoteBase)startNote, (NoteBase)endNote, segment);
					}
					else
					{
						int centerPosition = GetCenterPosition(time, startNote, endNote, isTail);
						if (centerPosition < 2 && LiveTrace.IsOn(LiveTraceCategory.View))
							LiveTrace.Write(LiveTraceCategory.View, "mesh time=" + time.ToString("F3") + " startT=" + startNote.MusicScoreInfo.time.ToString("F3") + " endT=" + endNote.MusicScoreInfo.time.ToString("F3") + " centerPos=" + centerPosition + " meshCount=" + _meshCount);
						if (centerPosition >= 2)
							MeshCreate(time, baseNote, centerPosition, segment);
					}
				}

				startNote = endNote;
				if (isTail)
				{
					break;
				}
			}
		}

		private Color SetSegmentStyle(float time, INote baseNote, NextRushConnectorSegment segment)
		{
			SetStatusValue(time, baseNote);
			Color color = GetNoteColor(baseNote);
			if (segment != null)
			{
				_statusValue.y = segment.IsCritical ? 0.025f : 0.525f;
				if (segment.IsGuide) color = segment.GuideColor();
				else if (segment.HasColor) color *= segment.StyleColor();
				if (segment.TracksDamageContact && baseNote is GuideNote damage
					&& (!damage.DamageContact || damage.State == NoteState.Done))
					color *= ReleaseColor;
			}
			return color;
		}

		private void MeshCreate(float time, INote baseNote, int meshDataCount, NextRushConnectorSegment segment)
		{
			EnsureMeshCapacity(_meshCount + meshDataCount - 1);
			Color color = SetSegmentStyle(time, baseNote, segment);
			bool solidGuide = segment?.IsGuide == true;
			float textureMode = solidGuide ? -1f : 0f;
			for (int i = 0; i < meshDataCount - 1; i++)
			{
				int vertexIndex = _meshCount * TRAPEZOID_VERTEX_COUNT;
				CalculateTrapezoidVertex(_meshSettingBuffer[i], _meshSettingBuffer[i + 1], solidGuide);
				float startAlpha = segment == null ? 1 : _meshSettingBuffer[i].Alpha;
				float endAlpha = segment == null ? 1 : _meshSettingBuffer[i + 1].Alpha;
				UpdateVertex(vertexIndex, color, startAlpha, endAlpha);
				UpdateUV(vertexIndex, i, textureMode);
				_meshCount++;
			}
		}

		private void DrawFullScreen(float time, LongNote bar, NoteBase start, NoteBase end, NextRushConnectorSegment segment)
		{
			double now = (double)time + LiveConfig.ViewLeadSeconds;
			if (_viewCamera == null
				|| bar?.TailNote is { HasJudgment: true, State: NoteState.Done }
				|| !segment.TryFullScreenAlpha(now, start.NativeTarget.Time, end.NativeTarget.Time, out float alpha)
				|| segment.IsHidden(now, start.NativeScroll.SampleFrame(now).Hidden)) return;
			if (!_hasViewport)
			{
				float depth = _viewCamera.WorldToViewportPoint(transform.position).z;
				depth = Mathf.Clamp(depth, _viewCamera.nearClipPlane + 0.01f, _viewCamera.farClipPlane - 0.01f);
				_viewportBottomLeft = ViewportPoint(0, 0, depth);
				_viewportTopLeft = ViewportPoint(0, 1, depth);
				_viewportBottomRight = ViewportPoint(1, 0, depth);
				_viewportTopRight = ViewportPoint(1, 1, depth);
				_hasViewport = true;
			}

			Color color = SetSegmentStyle(time, bar, segment);
			color.a *= alpha;
			float textureMode = segment.IsGuide ? 2f : 1f;
			EnsureMeshCapacity(_meshCount + 1);
			int vertexIndex = _meshCount++ * TRAPEZOID_VERTEX_COUNT;
			for (int i = 0; i < TRAPEZOID_VERTEX_COUNT; i++)
			{
				bool top = (i & 1) != 0;
				int index = vertexIndex + i;
				_vertices[index] = i < 4
					? (top ? _viewportTopLeft : _viewportBottomLeft)
					: (top ? _viewportTopRight : _viewportBottomRight);
				_colors[index] = color;
				_uv[index] = new Vector2(textureMode, 0);
				_leftPoint[index] = _rightPoint[index] = Vector2.zero;
				_uvOffsetLeft[index] = new Vector2(_statusValue.x, 0);
				_uvOffsetRight[index] = new Vector2(_statusValue.y, 0);
			}
		}

		private Vector3 ViewportPoint(float x, float y, float depth) =>
			transform.InverseTransformPoint(_viewCamera.ViewportToWorldPoint(new Vector3(x, y, depth)));

		/// <summary>
		/// 获取note的实际渲染颜色
		/// 优先使用自定义颜色（guideColor），否则使用默认类型颜色
		/// </summary>
		private Color GetNoteColor(INote baseNote)
		{
			if (baseNote is LongNote && baseNote.State == NoteState.Last && LiveTrace.HighlightLast)
			{
				return Color.red;
			}

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

		private void UpdateUV(int vertexIndex, int meshDataIndex, float textureMode)
		{
			float startLineProgress = _meshSettingBuffer[meshDataIndex].LineProgress;
			float endLineProgress = _meshSettingBuffer[meshDataIndex + 1].LineProgress;
			for (int offset = 0; offset <= 4; offset += 4)
			{
				_uv[vertexIndex + offset + 3] = new Vector2(textureMode, endLineProgress);
				_uv[vertexIndex + offset + 1] = new Vector2(textureMode, endLineProgress);
				_uv[vertexIndex + offset + 2] = new Vector2(textureMode, startLineProgress);
				_uv[vertexIndex + offset] = new Vector2(textureMode, startLineProgress);
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

		private void CalculateTrapezoidVertex(Setting startSetting, Setting endSetting, bool solidGuide)
		{
			if (solidGuide)
			{
				// Fill the space between the endpoints with two quads sharing a center edge.
				var top = endSetting.LanePosition;
				var bottom = startSetting.LanePosition;
				Vector2 topCenter = (top.Left + top.Right) * 0.5f;
				Vector2 bottomCenter = (bottom.Left + bottom.Right) * 0.5f;
				_trapezoidVertexInfo[0].Top = new TwoPoint { Left = top.Left, Right = topCenter };
				_trapezoidVertexInfo[0].Bottom = new TwoPoint { Left = bottom.Left, Right = bottomCenter };
				_trapezoidVertexInfo[1].Top = new TwoPoint { Left = topCenter, Right = top.Right };
				_trapezoidVertexInfo[1].Bottom = new TwoPoint { Left = bottomCenter, Right = bottom.Right };
				return;
			}

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

		private void UpdateVertex(int vertexIndex, Color color, float startAlpha, float endAlpha)
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
					_colors[index] = new Color(color.r, color.g, color.b, color.a * ((i & 1) == 0 ? startAlpha : endAlpha));
				}
			}
		}

		private int GetCenterPosition(float time, INote startNote, INote endNote, bool isTail)
		{
			if (startNote is not NoteBase startNoteBase || endNote is not NoteBase endNoteBase)
			{
				return 0;
			}
			if (startNoteBase.NativeScroll != null && endNoteBase.NativeScroll != null)
				return SampleNativeSegment(time, startNoteBase, endNoteBase);

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

		private int SampleNativeSegment(float time, NoteBase start, NoteBase end)
		{
			var segment = start.NativeConnector;
			if (segment != null && segment.StartAlpha == 0 && segment.EndAlpha == 0) return 0;
			var owner = start as LongNote ?? start.ParentNote as LongNote;
			if (segment?.ThroughJudgeLine == true
				&& owner?.TailNote is { HasJudgment: true, State: NoteState.Done }) return 0;
			double now = (double)time + LiveConfig.ViewLeadSeconds;
			double a = start.NativeViewProgress(now, out bool hidden);
			double b = end.NativeViewProgress(now, out _);
			if ((segment?.IsHidden(now, hidden) ?? hidden)
				|| !NextRushConnectorSegment.TryClip(segment, now, start.NativeTarget.Time, end.NativeTarget.Time,
					ref a, ref b, out double first, out double last)) return 0;
			// The mesh's winding expects the first cross section closest to the judge line.
			if (a < b)
			{
				(a, b) = (b, a);
				(first, last) = (last, first);
			}
			int minSplits = 1;
			if (segment != null && a != b)
			{
				// Keep sampling over the curve even when endpoint motion compresses its screen height.
				int samples = segment.Ease >= 30 && segment.Ease <= 37
					? Mathf.Max(4, Mathf.CeilToInt(32 * (float)Math.Abs(last - first))) : segment.Ease != 1 ? 4 : 1;
				minSplits = Mathf.Min(samples, SplitMeshCount);
			}
			int splits = Mathf.Clamp(Mathf.CeilToInt((float)(a - b) * SplitMeshCount), minSplits, SplitMeshCount);
			List<INote> viewNotes = start.ParentNote is LongNote bar ? bar.LineNoteList : start.ViewNoteList;
			int index = viewNotes != null ? Mathf.Max(0, viewNotes.IndexOf(start)) : 0;
			int segmentCount = Mathf.Max(1, (viewNotes?.Count ?? 2) - 1);
			float stepBoundary = segment?.StepBoundary ?? float.NaN;
			bool step = stepBoundary >= 0f && stepBoundary <= 1f;
			int written = 0;
			for (int i = 0; i <= splits; i++)
			{
				float fraction = (float)i / splits;
				float along = Mathf.Lerp((float)first, (float)last, fraction);
				if (step && i > 0)
				{
					float previous = Mathf.Lerp((float)first, (float)last, (float)(i - 1) / splits);
					bool forward = first < last;
					if (forward ? previous < stepBoundary && along >= stepBoundary
						: previous > stepBoundary && along <= stepBoundary)
					{
						float jump = (float)((stepBoundary - first) / (last - first));
						// Equal heights leave no mesh between the two sides of the jump.
						Write(jump, stepBoundary, forward ? 0f : 1f);
						Write(jump, stepBoundary, forward ? 1f : 0f);
						if (along == stepBoundary) continue;
					}
				}
				float eased = step && along == stepBoundary ? (last < stepBoundary ? 0f : 1f)
					: segment?.Interpolate(along) ?? along;
				Write(fraction, along, eased);
			}
			return written;

			void Write(float fraction, float along, float eased)
			{
				float progress = OpenSekai.Shared.Live.NoteLineProgress.View(Mathf.Lerp((float)a, (float)b, fraction));
				float left = Mathf.LerpUnclamped(start.NativeLeft, end.NativeLeft, eased);
				float right = Mathf.LerpUnclamped(start.NativeRight, end.NativeRight, eased);
				Vector2 center = LiveUtility.EarlyVec2Lerp(LiveConfig.SpawnPosition, LiveConfig.NativeLanePosition((left + right) / 2), progress);
				float inset = segment?.IsGuide == true ? 0f : 0.1f;
				float halfWidth = progress * Mathf.Max(0, (right - left + 1) * LiveConfig.widthX - inset) / 2;
				_meshSettingBuffer[written++] = new Setting
				{
					CurrentViewProgress = progress,
					Alpha = segment == null ? 1 : Mathf.Lerp(segment.StartAlpha, segment.EndAlpha, along),
					LanePosition = new TwoPoint
					{
						Left = new Vector2(center.x - halfWidth, center.y),
						Right = new Vector2(center.x + halfWidth, center.y)
					},
					LineProgress = (index + along) / segmentCount
				};
			}
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

		private void ApplyMesh(NextRushConnectorLayer layer, bool native)
		{
			int index = (int)layer;
			var batch = _batches[index];
			if (_meshCount == 0)
			{
				if (batch != null && batch.Renderer.enabled)
				{
					batch.Renderer.enabled = false;
					batch.Mesh.Clear();
				}
				return;
			}

			if (batch == null)
			{
				var host = new GameObject(layer.ToString()) { layer = gameObject.layer };
				host.transform.SetParent(transform, false);
				_batches[index] = batch = CreateBatch(host);
			}
			batch.Renderer.sortingOrder = native ? ConnectorSortingOrder(layer) + (_guideView ? 1 : 0) : 0;
			Mesh mesh = batch.Mesh;
			int vertices = _meshCount * TRAPEZOID_VERTEX_COUNT;
			if (mesh.vertexCount > vertices) mesh.Clear();
			if (vertices > ushort.MaxValue && mesh.indexFormat == IndexFormat.UInt16)
				mesh.indexFormat = IndexFormat.UInt32;
			mesh.SetVertices(_vertices, 0, vertices);
			mesh.SetColors(_colors, 0, vertices);
			mesh.SetUVs(0, _uv, 0, vertices);
			mesh.SetUVs(1, _leftPoint, 0, vertices);
			mesh.SetUVs(2, _rightPoint, 0, vertices);
			mesh.SetUVs(3, _uvOffsetLeft, 0, vertices);
			mesh.SetUVs(4, _uvOffsetRight, 0, vertices);
			mesh.SetIndices(_triangles, 0, _meshCount * TRAPEZOID_INDEX_COUNT, MeshTopology.Triangles, 0);
			batch.Renderer.enabled = true;
		}

		// The live stage lanes are sorted at -101 to -99, simultaneous lines at 0, and keys at 30/31.
		private static int ConnectorSortingOrder(NextRushConnectorLayer layer) => layer switch
		{
			NextRushConnectorLayer.Under => -104,
			NextRushConnectorLayer.Bottom => -10,
			NextRushConnectorLayer.Top => -8,
			NextRushConnectorLayer.Over => 40,
			_ => throw new ArgumentOutOfRangeException(nameof(layer))
		};

		private void OnDestroy()
		{
			foreach (var batch in _batches)
				if (batch != null) DisposeRenderObject(batch.Mesh);
			DisposeRenderObject(Material);
		}

		private static void DisposeRenderObject(UnityEngine.Object value)
		{
			if (value == null) return;
			if (Application.isPlaying) Destroy(value);
			else DestroyImmediate(value);
		}
	}
}
