using UnityEngine;
using VerdantSiege.UI;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Fixed elevated 2.5D camera that fits the whole battlefield (plus the Heartroot and enemy entry margins)
    /// into the part of the screen not covered by the HUD, for any aspect ratio (4:3 tablets to 21:9 phones).
    /// Refits when the resolution or safe area changes.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class BattleCameraRig : MonoBehaviour
    {
        public const float DefaultPitch = 60f;
        public float pitch = DefaultPitch;
        public float fieldOfView = 32f;
        [Tooltip("Viewport rect (0..1) the battlefield must fit inside, excluding HUD areas.")]
        public Rect safeViewport = new Rect(0.16f, 0.02f, 0.82f, 0.84f);

        private Camera _cam;
        private Vector3 _basePos;
        /// <summary>Transient offset (breach shake); applied on top of the fitted pose.</summary>
        public Vector3 ShakeOffset { get; set; }
        private Bounds _focus;
        private Vector2Int _lastSize;
        private Rect _lastSafe;
        private bool _hasFocus;

        private void Awake() => _cam = GetComponent<Camera>();

        public void Fit(Bounds focus, Rect viewport)
        {
            _focus = focus;
            safeViewport = viewport;
            _hasFocus = true;
            Refit();
        }

        private void LateUpdate()
        {
            if (!_hasFocus) return;
            if (_lastSize.x != Screen.width || _lastSize.y != Screen.height || _lastSafe != ScreenSafe.Area) Refit();
            transform.position = _basePos + ShakeOffset;
        }

        public void Refit()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            _lastSize = new Vector2Int(Screen.width, Screen.height);
            _lastSafe = ScreenSafe.Area;
            _cam.fieldOfView = fieldOfView;
            Quaternion rot = Quaternion.Euler(pitch, 0f, 0f);
            transform.rotation = rot;
            Vector3 fwd = rot * Vector3.forward;
            Vector3 right = rot * Vector3.right;
            Vector3 up = rot * Vector3.up;

            Rect vp = SafeAdjusted(safeViewport);
            Vector3 target = _focus.center;
            float lo = 2f, hi = 80f, dist = 20f;
            for (int iter = 0; iter < 3; iter++)
            {
                lo = 2f; hi = 80f;
                for (int i = 0; i < 24; i++)
                {
                    dist = (lo + hi) * 0.5f;
                    transform.position = target - fwd * dist;
                    if (Fits(vp)) hi = dist; else lo = dist;
                }
                dist = hi;
                transform.position = target - fwd * dist;
                // Re-centre: shift so the projected focus centre sits at the viewport centre.
                GetProjectedRect(out float minX, out float maxX, out float minY, out float maxY);
                float dx = (minX + maxX) * 0.5f - vp.center.x;
                float dy = (minY + maxY) * 0.5f - vp.center.y;
                float halfH = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) * dist;
                float halfW = halfH * _cam.aspect;
                target += right * (dx * 2f * halfW) + up * (dy * 2f * halfH);
            }
            transform.position = target - fwd * dist;
            _basePos = transform.position;
        }

        private static Rect SafeAdjusted(Rect vp)
        {
            Rect s = ScreenSafe.Area;
            if (Screen.width <= 0 || Screen.height <= 0) return vp;
            float sx = s.x / Screen.width, sy = s.y / Screen.height, sw = s.width / Screen.width, sh = s.height / Screen.height;
            return new Rect(sx + vp.x * sw, sy + vp.y * sh, vp.width * sw, vp.height * sh);
        }

        private bool Fits(Rect vp)
        {
            GetProjectedRect(out float minX, out float maxX, out float minY, out float maxY);
            return maxX - minX <= vp.width && maxY - minY <= vp.height;
        }

        private void GetProjectedRect(out float minX, out float maxX, out float minY, out float maxY)
        {
            minX = minY = float.MaxValue;
            maxX = maxY = float.MinValue;
            Vector3 c = _focus.center, e = _focus.extents;
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3(c.x + ((i & 1) == 0 ? -e.x : e.x), c.y + ((i & 2) == 0 ? -e.y : e.y), c.z + ((i & 4) == 0 ? -e.z : e.z));
                Vector3 v = _cam.WorldToViewportPoint(p);
                if (v.x < minX) minX = v.x;
                if (v.x > maxX) maxX = v.x;
                if (v.y < minY) minY = v.y;
                if (v.y > maxY) maxY = v.y;
            }
        }
    }
}
