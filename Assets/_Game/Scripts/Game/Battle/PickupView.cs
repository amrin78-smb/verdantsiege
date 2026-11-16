using UnityEngine;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Sunmote Vitality pickup: falls from the sky (or pops out of a Glowcap), bobs and spins, blinks before
    /// expiring, flies up when collected. Tap detection uses its screen position (generous radius).
    /// </summary>
    public sealed class PickupView : MonoBehaviour, IPooledView
    {
        private PickupRuntime _p;
        private BattleLayout _layout;
        private Transform _model;
        private Vector3 _rest;
        private Vector3 _from;
        private float _collectTimer = -1f;
        private float _expireTimer = -1f;
        private Vector3 _baseScale;
        private const float FallTime = 1.1f;
        private const float PopTime = 0.6f;

        public PickupRuntime Runtime => _p;
        public bool Finished => _collectTimer > 0.4f || _expireTimer > 0.3f;
        public bool Leaving => _collectTimer >= 0f || _expireTimer >= 0f;
        public Vector3 RestPosition => _rest;

        private void Awake()
        {
            _model = transform.childCount > 0 ? transform.GetChild(0) : transform;
            _baseScale = _model.localScale;
        }

        public void Bind(PickupRuntime p, BattleLayout layout)
        {
            _p = p;
            _layout = layout;
            _rest = layout.ToWorld(p.x, p.laneFloat) + Vector3.up * 0.32f;
            _from = p.source == PickupSource.Sky ? _rest + new Vector3(0.6f, 5f, 0.8f) : _rest - Vector3.up * 0.2f;
            transform.position = _from;
        }

        public void OnRent()
        {
            _collectTimer = -1f;
            _expireTimer = -1f;
            if (_model != null) _model.localScale = _baseScale;
        }

        public void OnReturn() => _p = null;

        public void PlayCollect() { if (_collectTimer < 0f) _collectTimer = 0f; }
        public void PlayExpire() { if (_expireTimer < 0f) _expireTimer = 0f; }

        public void Tick(float dt, float time)
        {
            if (_collectTimer >= 0f)
            {
                _collectTimer += dt;
                float k = Mathf.Clamp01(_collectTimer / 0.4f);
                transform.position += Vector3.up * dt * 6f;
                _model.localScale = _baseScale * (1f + k * 0.4f) * (1f - k);
                return;
            }
            if (_expireTimer >= 0f)
            {
                _expireTimer += dt;
                _model.localScale = _baseScale * Mathf.Max(0f, 1f - _expireTimer / 0.3f);
                return;
            }
            if (_p == null) return;
            float age = _p.age;
            float bob = Mathf.Sin(time * 3f + _p.Id) * 0.05f;
            if (_p.source == PickupSource.Sky && age < FallTime)
            {
                float k = age / FallTime;
                float e = 1f - (1f - k) * (1f - k);
                transform.position = Vector3.Lerp(_from, _rest, e);
            }
            else if (_p.source != PickupSource.Sky && age < PopTime)
            {
                float k = age / PopTime;
                transform.position = Vector3.Lerp(_from, _rest, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.6f;
            }
            else transform.position = _rest + Vector3.up * bob;

            _model.localRotation = Quaternion.Euler(0f, time * 90f + _p.Id * 30f, 0f);
            // Blink during the last 3 seconds so expiry is never a surprise.
            bool blinkOff = _p.remaining < 3f && Mathf.Repeat(time * (_p.remaining < 1.5f ? 8f : 4f), 1f) < 0.3f;
            float pulse = 1f + Mathf.Sin(time * 5f) * 0.06f;
            _model.localScale = blinkOff ? _baseScale * 0.75f : _baseScale * pulse;
        }
    }
}
