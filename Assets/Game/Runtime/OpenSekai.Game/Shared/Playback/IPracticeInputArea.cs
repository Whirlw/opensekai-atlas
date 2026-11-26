using UnityEngine;

namespace OpenSekai.Shared.Playback
{
    public interface IPracticeInputArea
    {
        bool ContainsControl(Vector2 screenPosition);
    }
}
