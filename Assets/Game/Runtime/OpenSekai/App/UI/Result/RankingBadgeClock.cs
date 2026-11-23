using UnityEngine;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Runs a <see cref="RankingBadge"/> from the frame clock while it is on screen: picks up its
    /// crest once drawn and fades it in, and, for a badge that animates itself, plays its idle
    /// motion. A badge whose owner drives it, or one that holds still, switches the clock off once
    /// its crest is in.
    /// </summary>
    internal sealed class RankingBadgeClock : MonoBehaviour
    {
        /// <summary>A long frame or coming back to the app should not jump the motion.</summary>
        private const float MAX_STEP = 0.05f;

        private RankingBadge _badge;
        private bool _animate;
        private double _time;

        internal void Bind(RankingBadge badge, bool animate)
        {
            _badge = badge;
            _animate = animate;
        }

        /// <summary>Turns the clock back on: the badge has a new crest to pick up.</summary>
        internal void Wake() => enabled = true;

        private void Update()
        {
            if (_badge == null)
            {
                enabled = false;
                return;
            }

            float step = Mathf.Min(Time.unscaledDeltaTime, MAX_STEP);
            _badge.Advance(step);
            if (_animate)
            {
                _time += step;
                _badge.Tick(_time);
            }
            else if (_badge.IsSettled)
            {
                enabled = false;
            }
        }
    }
}
