using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Shared.Live
{
    /// <summary>
    /// The swipe test a flick needs to pass, as pure math.
    ///
    /// A touch delta arrives in pixels per <see cref="Time.deltaTime"/>; the judge compares it against a
    /// per-frame threshold, so the delta is first normalised to a 60 fps frame. The direction test accepts
    /// a 180 degree cone around the required vector, which is why the dot product is compared with zero
    /// rather than with a cosine threshold.
    /// </summary>
    public static class FlickGesture
    {
        public const float REFERENCE_FRAME_SECONDS = 1f / 60f;
        private const float EPSILON = 1e-5f;

        /// <summary>Required swipe direction for a note; horizontal flicks ask for a 45 degree diagonal.</summary>
        public static Vector2 RequiredDirection(NoteDirection direction)
        {
            return direction switch
            {
                NoteDirection.Left => new Vector2(-1f, 1f),
                NoteDirection.Right => new Vector2(1f, 1f),
                NoteDirection.DownLeft => new Vector2(-1f, -1f),
                NoteDirection.DownRight => new Vector2(1f, -1f),
                NoteDirection.Down => Vector2.down,
                _ => Vector2.up
            };
        }

        /// <summary>
        /// Scales a per-frame delta to the reference frame, so a 30 fps swipe and a 120 fps swipe of the
        /// same physical speed produce the same number.
        /// </summary>
        public static Vector2 ToReferenceFrame(Vector2 delta, float deltaTime)
        {
            float frameScale = deltaTime / REFERENCE_FRAME_SECONDS;
            if (frameScale <= 0f)
            {
                return delta;
            }

            return delta / frameScale;
        }

        /// <summary>
        /// Returns true when the swipe is long enough, with <paramref name="trueDirection"/> telling whether it
        /// also goes the way the note asks for. A note without a direction accepts any swipe.
        /// </summary>
        public static bool TryUpdate(Vector2 delta, float deltaTime, NoteDirection direction, float flickDistancePixels, out bool trueDirection)
        {
            trueDirection = false;

            if (delta.sqrMagnitude <= EPSILON)
            {
                return false;
            }

            Vector2 perFrame = ToReferenceFrame(delta, deltaTime);
            if (perFrame.sqrMagnitude <= flickDistancePixels * flickDistancePixels)
            {
                return false;
            }

            if (direction is NoteDirection.Default or NoteDirection.Down)
            {
                trueDirection = true;
                return true;
            }

            Vector2 required = RequiredDirection(direction);
            trueDirection = Vector2.Dot(perFrame.normalized, required.normalized) >= 0f;
            return true;
        }
    }
}
