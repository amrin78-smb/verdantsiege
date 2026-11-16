using UnityEngine;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle.View
{
    /// <summary>The objective tree: canopy sway, heart-core pulse that weakens with damage, shake on breach.</summary>
    public sealed class HeartrootView : MonoBehaviour
    {
        private UnitRig _rig;
        private VfxSystem _vfx;
        private Spring _shake;
        private float _health01 = 1f;
        private Vector3 _basePos;

        public void Init(VfxSystem vfx)
        {
            _rig = GetComponent<UnitRig>();
            if (_rig == null) _rig = gameObject.AddComponent<UnitRig>();
            _vfx = vfx;
            _basePos = transform.position;
        }

        public void OnDamaged(int remaining, int max)
        {
            _health01 = max > 0 ? remaining / (float)max : 0f;
            _shake.Kick(14f);
            if (_rig != null) _rig.Flash(new Color(1f, 0.3f, 0.35f), 0.2f);
            Transform core = _rig != null ? _rig.Get("HeartCore") : null;
            if (_vfx != null && core != null)
                _vfx.Burst(VfxKind.Breach, core.position, new Color(1f, 0.4f, 0.45f), 24, 2.5f, 0.3f, 0.8f);
        }

        private void LateUpdate()
        {
            if (_rig == null) return;
            float dt = Time.deltaTime;
            float t = Time.time;
            _rig.TickFlash(dt);
            _shake.Tick(dt, 300f, 9f);
            transform.position = _basePos + new Vector3(Mathf.Sin(t * 50f) * _shake.value * 0.004f, 0f, 0f);
            _rig.Pose("Canopy", new Vector3(Mathf.Sin(t * 0.7f) * 1.5f, 0f, Mathf.Sin(t * 0.9f + 1f) * 1.8f));
            float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * (2.2f + (1f - _health01) * 2.5f))), 6f);
            float s = Mathf.Lerp(0.75f, 1f, _health01) * (1f + beat * 0.12f);
            _rig.Pose("HeartCore", Vector3.zero, Vector3.zero, Vector3.one * s);
        }
    }
}
