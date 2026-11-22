using System;
using System.Collections;
using OpenSekai.App.Library;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OpenSekai.App.UI
{
    internal sealed class ProfileHistoryHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        /// <summary>The dip list rows use, as on the package rows.</summary>
        private const float PRESS_SCALE = 0.985f;

        private CustomMusicScorePlayHistoryRecord _play;
        private LibraryPressFeedback _press;
        private Coroutine _hold;
        private int _pointer;
        internal Action<CustomMusicScorePlayHistoryRecord> LongPressed;

        /// <param name="host">Object that receives the pointer events.</param>
        /// <param name="body">What shrinks while a run is pressed.</param>
        internal static ProfileHistoryHold Attach(GameObject host, Transform body)
        {
            ProfileHistoryHold hold = host.AddComponent<ProfileHistoryHold>();
            hold._press = LibraryPressFeedback.Attach(host, body, PRESS_SCALE);
            hold._press.enabled = false;
            return hold;
        }

        /// <summary>A run to delete on a long press, or null for a row that does not respond (Best 30, pooled).</summary>
        internal void Bind(CustomMusicScorePlayHistoryRecord play)
        {
            Cancel();
            _play = play;
            if (_press != null) _press.enabled = play != null;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (_play == null || _hold != null || data.button != PointerEventData.InputButton.Left) return;
            _pointer = data.pointerId;
            _hold = StartCoroutine(Hold(data));
        }

        private IEnumerator Hold(PointerEventData data)
        {
            yield return new WaitForSecondsRealtime(0.7f);
            _hold = null;
            if (!data.dragging && data.eligibleForClick) LongPressed?.Invoke(_play);
        }

        public void OnPointerUp(PointerEventData data) { if (data.pointerId == _pointer) Cancel(); }
        public void OnPointerExit(PointerEventData data) { if (data.pointerId == _pointer) Cancel(); }
        private void OnDisable() => Cancel();
        private void Cancel()
        {
            if (_hold != null) StopCoroutine(_hold);
            _hold = null;
        }
    }
}
