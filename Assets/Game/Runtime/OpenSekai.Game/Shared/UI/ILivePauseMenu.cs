using System;
using UnityEngine;

namespace OpenSekai.Shared.UI
{
    /// <summary>What the pause card shows about the song being played.</summary>
    public sealed class LivePauseInfo
    {
        public string Title { get; set; } = string.Empty;

        /// <summary>Who charted it; empty when the package does not say.</summary>
        public string Charter { get; set; } = string.Empty;

        /// <summary>Manifest difficulty type, lower case ("master", "append" …).</summary>
        public string Difficulty { get; set; } = string.Empty;

        public int Level { get; set; }

        public bool IsAuto { get; set; }

        /// <summary>The jacket the song info page already loaded; null when there is none.</summary>
        public Texture2D Jacket { get; set; }

        /// <summary>The jacket's accent, as the song info page computed it.</summary>
        public Color Accent { get; set; } = new Color32(157, 184, 255, 255);
    }

    /// <summary>
    /// The pause layer of the player-library flow. The live scene owns its lifetime; the menu only
    /// reports which action the player chose.
    /// </summary>
    public interface ILivePauseMenu
    {
        bool IsVisible { get; }

        void Show(LivePauseInfo info, Action resume, Action retry, Action quit);

        void Hide();
    }

    /// <summary>
    /// Builds the pause menu. The library-styled one lives in the App assembly, which this assembly
    /// cannot reference, so App registers its factory at startup; without one the plain
    /// <see cref="LivePauseOverlay"/> is used.
    /// </summary>
    public static class LivePauseMenus
    {
        public static Func<Transform, ILivePauseMenu> Factory { get; set; }

        public static ILivePauseMenu Create(Transform parent)
        {
            ILivePauseMenu menu = null;
            if (Factory != null)
            {
                try
                {
                    menu = Factory(parent);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            return menu ?? LivePauseOverlay.Create(parent);
        }
    }
}
