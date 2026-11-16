using UnityEngine;
using VerdantSiege.Data;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Projectile in flight: interpolated position, spin, short trail. Lobbed seeds follow a parabola to their
    /// landing point, ricochets glide across lanes, hostile spit flies toward the Heartroot and reflected spit
    /// flies back.
    /// </summary>
    public sealed class ProjectileView : MonoBehaviour, IPooledView
    {
        private ProjectileRuntime _p;
        private BattleLayout _layout;
        private TrailRenderer _trail;
        private Transform _model;
        private float _spin;
        private float _visLane;
        private const float Height = 0.62f;
        private const float LobHeight = 1.25f;

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
            _visLane = p.lane;
            transform.position = _layout.ToWorld(p.x, p.lane) + Vector3.up * Height;
            if (_trail != null) _trail.Clear();
        }

        public void OnRent() { if (_trail != null) { _trail.Clear(); _trail.emitting = true; } }
        public void OnReturn() { _p = null; if (_trail != null) { _trail.emitting = false; _trail.Clear(); } }

        public void Tick(float dt, float interpolation)
        {
            if (_p == null) return;
            float x = Mathf.LerpUnclamped(_p.prevX, _p.x, interpolation);
            _visLane = Mathf.MoveTowards(_visLane, _p.lane, dt * 7f);
            float y;
            bool lob = _p.Definition.motion == ProjectileMotion.Lobbed && !_p.hostile && _p.homingEnemyId == 0;
            if (lob)
            {
                float span = Mathf.Max(0.05f, _p.targetX - _p.startX);
                float k = Mathf.Clamp01((x - _p.startX) / span);
                y = Height * (1f - k) + 0.25f * k + 4f * LobHeight * k * (1f - k);
            }
            else y = Height + Mathf.Sin(Mathf.Clamp01(Mathf.Abs(x - _p.startX) / 1.2f) * Mathf.PI * 0.5f) * 0.05f;
            transform.position = _layout.ToWorld(x, _visLane) + Vector3.up * y;
            bool backwards = _p.hostile || (_p.homingEnemyId != 0 && _p.x < _p.prevX);
            _spin += dt * (_p.Definition.bounces > 0 ? 900f : 720f);
            Quaternion facing = Quaternion.Euler(0f, backwards ? 180f : 0f, 0f);
            _model.localRotation = _p.Definition.bounces > 0 ? facing * Quaternion.Euler(0f, _spin, 0f) : facing * Quaternion.Euler(_spin, 0f, 0f);
        }
    }
}
