using UnityEngine;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle.View
{
    /// <summary>Seed bolt in flight: interpolated position, spin, short trail.</summary>
    public sealed class ProjectileView : MonoBehaviour, IPooledView
    {
        private ProjectileRuntime _p;
        private BattleLayout _layout;
        private TrailRenderer _trail;
        private Transform _model;
        private float _spin;
        private const float Height = 0.62f;

        public ProjectileRuntime Runtime => _p;

        private void Awake()
        {
            _trail = GetComponentInChildren<TrailRenderer>(true);
            _model = transform.childCount > 0 ? transform.GetChild(0) : transform;
        }

        public void Bind(ProjectileRuntime p, BattleLayout layout)
        {
            _p = p;
            _layout = layout;
            _spin = Random.Range(0f, 360f);
            transform.position = _layout.ToWorld(p.x, p.lane) + Vector3.up * Height;
            if (_trail != null) _trail.Clear();
        }

        public void OnRent() { if (_trail != null) { _trail.Clear(); _trail.emitting = true; } }
        public void OnReturn() { _p = null; if (_trail != null) { _trail.emitting = false; _trail.Clear(); } }

        public void Tick(float dt, float interpolation)
        {
            if (_p == null) return;
            float x = Mathf.LerpUnclamped(_p.prevX, _p.x, interpolation);
            float arc = Mathf.Sin(Mathf.Clamp01((x - _p.startX) / 1.2f) * Mathf.PI * 0.5f) * 0.05f;
            transform.position = _layout.ToWorld(x, _p.lane) + Vector3.up * (Height + arc);
            _spin += dt * 720f;
            _model.localRotation = Quaternion.Euler(_spin, 0f, 0f);
        }
    }
}
