using System;
using System.Collections;
using OpenSekai.App.Library;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OpenSekai.App.UI
{
    internal sealed class ProfileHistoryHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private CustomMusicScorePlayHistoryRecord _play;
        private Coroutine _hold;
        private int _pointer;
        internal Action<CustomMusicScorePlayHistoryRecord> LongPressed;

        internal void Bind(CustomMusicScorePlayHistoryRecord play)
        {
            Cancel();
            _play = play;
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
