using UnityEngine;

namespace OpenSekai.Shared.UI
{
    /// <summary>
    /// The HUD sprites are packed assets: each sprite asset carries its own baked mesh, taken from the
    /// outline of the art that was there when the asset was authored. Painting new art leaves that mesh
    /// behind, so a redrawn glyph is drawn only inside the old outline - the tail of a "1" loses its
    /// corner even though the texture under it is complete. Sliced draw mode builds its own rectangle
    /// instead of using the baked mesh, which is the only reliable way to draw what the texture has.
    /// </summary>
    public static class SpriteQuad
    {
        /// <summary>Draw the sprite as a plain rectangle of the same on-screen size.</summary>
        public static void DrawAsRectangle(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            Sprite sprite = renderer.sprite;
            if (sprite == null)
            {
                return;
            }

            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = sprite.rect.size / sprite.pixelsPerUnit;
        }
    }
}
