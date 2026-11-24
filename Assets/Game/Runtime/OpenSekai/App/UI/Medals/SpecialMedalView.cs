using TMPro;
using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// The drawing of a special medal on a plate, a chip or a thumbnail: its face under the owner's text
    /// and its frame over it, each with what moves on it. Nothing is built until a special medal shows.
    /// </summary>
    internal sealed class SpecialMedalView
    {
        private readonly RectTransform _back;
        private readonly RectTransform _front;
        private VectorGraphic _ground;
        private VectorGraphic _groundMotion;
        private VectorGraphic _frame;
        private VectorGraphic _frameMotion;
        private string _faceId;

        /// <param name="back">Where the face goes, centered on the plate.</param>
        /// <param name="front">Where the frame goes, centered on the plate.</param>
        internal SpecialMedalView(RectTransform back, RectTransform front)
        {
            _back = back;
            _front = front;
        }

        /// <summary>The medal on show; null when it is not a special one.</summary>
        internal SpecialPlateArt Art { get; private set; }

        /// <summary>Takes the medal <paramref name="faceId"/>; false, with nothing drawn, for one that is not special.</summary>
        internal bool Set(string faceId)
        {
            if (faceId != _faceId)
            {
                _faceId = faceId;
                Art = SpecialPlateArt.Create(faceId);
            }

            if (Art != null) return true;
            _ground?.Hide();
            _groundMotion?.Hide();
            _frame?.Hide();
            _frameMotion?.Hide();
            return false;
        }

        /// <summary>Draws the medal on a plate of <paramref name="size"/>; its measures are in plate heights.</summary>
        internal void Draw(Vector2 size, in PlateSpace space, bool muted = false)
        {
            if (Art == null) return;
            if (_ground == null)
            {
                _ground = VectorGraphic.Create("SpecialGround", _back);
                _groundMotion = VectorGraphic.Create("SpecialGroundMotion", _back, ownCanvas: true);
                _frame = VectorGraphic.Create("SpecialFrame", _front);
                _frameMotion = VectorGraphic.Create("SpecialFrameMotion", _front, ownCanvas: true);
            }

            SpecialPlateArt art = Art;
            art.SetSpace(space);
            Vector2 reach = size + Vector2.one * (size.y * SpecialPlateArt.MARGIN * 2f);
            Place(_ground, size);
            Place(_groundMotion, size);
            Place(_frame, reach);
            Place(_frameMotion, reach);
            _ground.Show((mesh, time) => art.Ground(mesh), size.y, false, muted);
            _frame.Show((mesh, time) => art.Frame(mesh), size.y, false, muted);
            // A medal not earned yet stands still.
            _groundMotion.Show(muted ? null : art.GroundMotion, size.y, true);
            _frameMotion.Show(muted ? null : art.FrameMotion, size.y, true);
        }

        /// <summary>Gives <paramref name="label"/> the medal's letters, or its own again.</summary>
        internal void Paint(TextMeshProUGUI label, bool muted)
        {
            bool painted = Art != null && !muted;
            label.enableVertexGradient = painted;
            if (!painted) return;
            Color top = ToColor(Art.TextTop);
            Color bottom = ToColor(Art.TextBottom);
            label.color = Color.white;
            label.colorGradient = new VertexGradient(top, top, bottom, bottom);
        }

        internal static Color ToColor(VectorMesh.Ink ink) => new Color(ink.R, ink.G, ink.B, ink.A);

        internal static VectorMesh.Ink ToInk(Color color) => new VectorMesh.Ink(color.r, color.g, color.b, color.a);

        private static void Place(VectorGraphic graphic, Vector2 size)
        {
            Vector2 center = new Vector2(0.5f, 0.5f);
            LibraryWidgets.Place(graphic.rectTransform, center, center, Vector2.zero, size);
        }
    }

    /// <summary>
    /// The emblem of an achievement's medal: its series', in one color, or a special medal's own. It
    /// fills a square that the owner sizes.
    /// </summary>
    internal sealed class MedalEmblemView
    {
        private readonly VectorGraphic _graphic;
        private (string Id, Color Ink, float Size, bool Muted) _drawn;

        internal MedalEmblemView(string name, Transform parent)
        {
            _graphic = VectorGraphic.Create(name, parent);
        }

        internal RectTransform Root => _graphic.rectTransform;

        /// <param name="id">A series or a special medal; null hides it.</param>
        /// <param name="ink">Its color, for a series.</param>
        internal void Show(string id, Color ink, float size, bool muted = false)
        {
            var wanted = (id, ink, size, muted);
            if (wanted.Equals(_drawn)) return;
            _drawn = wanted;
            if (id == null)
            {
                _graphic.Hide();
                return;
            }

            _graphic.rectTransform.sizeDelta = new Vector2(size, size);
            SpecialPlateArt special = SpecialPlateArt.Create(id);
            VectorMesh.Ink tone = SpecialMedalView.ToInk(ink);
            if (special != null) _graphic.Show((mesh, time) => special.Emblem(mesh), size, false, muted);
            else _graphic.Show((mesh, time) => MedalEmblemArt.Draw(mesh, id, tone), size);
        }

        internal void Hide() => Show(null, Color.clear, 0f);
    }
}
