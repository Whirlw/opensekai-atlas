using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenSekai.App.UI
{
    /// <summary>
    /// Sits on an achievement's row inside the list and decides what a drag on it is: sideways turns
    /// the row's levels, anything else scrolls the list.
    /// </summary>
    internal sealed class AchievementSwipe : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler,
        IEndDragHandler
    {
        private AchievementRow _row;
        private ScrollRect _list;
        private bool _sideways;

        internal static void Attach(GameObject host, AchievementRow row, ScrollRect list)
        {
            AchievementSwipe swipe = host.AddComponent<AchievementSwipe>();
            swipe._row = row;
            swipe._list = list;
        }

        public void OnInitializePotentialDrag(PointerEventData eventData) => _list.OnInitializePotentialDrag(eventData);

        public void OnBeginDrag(PointerEventData eventData)
        {
            Vector2 moved = eventData.position - eventData.pressPosition;
            _sideways = Mathf.Abs(moved.x) > Mathf.Abs(moved.y) && _row.BeginSwipe(eventData);
            if (!_sideways) _list.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_sideways) _row.Swipe(eventData);
            else _list.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_sideways) _row.EndSwipe();
            else _list.OnEndDrag(eventData);
            _sideways = false;
        }

        private void OnDisable()
        {
            if (_sideways) _row.EndSwipe();
            _sideways = false;
        }
    }
}
