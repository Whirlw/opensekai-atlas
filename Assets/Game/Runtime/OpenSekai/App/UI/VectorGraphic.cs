using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>The time the medals' motions are drawn for, so that a screenshot can hold it.</summary>
    internal static class MedalClock
    {
        /// <summary>Holds every medal at this time; null lets them run.</summary>
        internal static float? Held { get; set; }

        /// <summary>Seconds; never slowed by the time scale.</summary>
        internal static float Now => Held ?? Time.unscaledTime;
    }

    /// <summary>
    /// A <see cref="VectorMesh"/> drawing on a canvas. A still one is drawn when it changes; an animated
    /// one every frame while it can be seen, on a canvas of its own, so the page around it is not
    /// batched again. It takes no taps. Its rectangle is for culling; the drawing is about its middle.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class VectorGraphic : MaskableGraphic
    {
        /// <summary>An edge's antialiasing in canvas units: about one pixel on a phone.</summary>
        private const float FEATHER = 0.9f;

        private readonly VectorMesh _mesh = new VectorMesh();
        private Action<VectorMesh, float> _draw;
        private float _unit = 1f;
        private bool _animated;
        private bool _muted;

        /// <summary>True when it is drawn again every frame.</summary>
        internal bool IsAnimated => _animated;

        internal static VectorGraphic Create(string name, Transform parent, bool ownCanvas = false)
        {
            RectTransform rect = LibraryUi.CreateRect(name, parent);
            if (ownCanvas) rect.gameObject.AddComponent<Canvas>();
            VectorGraphic graphic = rect.gameObject.AddComponent<VectorGraphic>();
            graphic.raycastTarget = false;
            graphic.enabled = false;
            return graphic;
        }

        /// <param name="draw">Draws it for a time in seconds.</param>
        /// <param name="unit">Canvas units per drawing unit.</param>
        internal void Show(Action<VectorMesh, float> draw, float unit, bool animated = false, bool muted = false)
        {
            _draw = draw;
            _unit = unit;
            _animated = animated;
            _muted = muted;
            enabled = draw != null;
            SetVerticesDirty();
        }

        internal void Hide() => Show(null, 1f);

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (_draw == null) return;
            _mesh.Begin(_unit, FEATHER);
            _draw(_mesh, MedalClock.Now);
            Color tint = color;
            List<VectorMesh.Vertex> vertices = _mesh.Vertices;
            for (int i = 0; i < vertices.Count; i++)
            {
                VectorMesh.Vertex vertex = vertices[i];
                VectorMesh.Ink ink = vertex.Ink;
                var shade = new Color(ink.R, ink.G, ink.B, ink.A);
                if (_muted)
                {
                    float grey = shade.grayscale * 0.72f;
                    shade = new Color(grey, grey, grey * 1.06f, shade.a);
                }

                helper.AddVert(new Vector3(vertex.X, vertex.Y), shade * tint, Vector4.zero);
            }

            List<int> indices = _mesh.Indices;
            for (int i = 0; i + 2 < indices.Count; i += 3) helper.AddTriangle(indices[i], indices[i + 1], indices[i + 2]);
        }

        private void Update()
        {
            if (_animated && !canvasRenderer.cull) SetVerticesDirty();
        }
    }
}
