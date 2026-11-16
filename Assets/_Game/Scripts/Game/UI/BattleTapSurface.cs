using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VerdantSiege.UI
{
    /// <summary>
    /// Invisible full-screen raycast target behind all HUD widgets. Because it sits behind the UI, taps on
    /// buttons/cards never reach the battlefield. Reports taps and finger swipes (for swipe-collecting pickups).
    /// </summary>
    public sealed class BattleTapSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public event Action<Vector2> Tapped;
        public event Action<Vector2> Swiped;
        public event Action<Vector2> PointerMoved;

        private Vector2 _down;
        private bool _moved;
        private const float TapSlopPx = 28f;

        public static BattleTapSurface Create(Transform parent)
        {
            RectTransform rt = UIKit.Rect("TapSurface", parent);
            UIKit.Stretch(rt);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = true;
            return rt.gameObject.AddComponent<BattleTapSurface>();
        }

        public void OnPointerDown(PointerEventData e)
        {
            _down = e.position;
            _moved = false;
            Swiped?.Invoke(e.position);
        }

        public void OnDrag(PointerEventData e)
        {
            if ((e.position - _down).sqrMagnitude > TapSlopPx * TapSlopPx) _moved = true;
            Swiped?.Invoke(e.position);
            PointerMoved?.Invoke(e.position);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_moved) Tapped?.Invoke(e.position);
        }
    }
}
