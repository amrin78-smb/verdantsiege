using UnityEngine;
using VerdantSiege.Data;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Presentation of one planted defender. Reads authoritative state from <see cref="DefenderRuntime"/>; all
    /// motion is procedural on the Blender part hierarchy (Head/Stem/Leaf/Cap/Plates...).
    /// </summary>
    [RequireComponent(typeof(UnitRig))]
    public sealed class DefenderView : MonoBehaviour, IPooledView
    {
        private UnitRig _rig;
        private DefenderRuntime _d;
        private VfxSystem _vfx;
        private VisualLibrary _lib;
        private Spring _recoil, _squash, _spawn;
        private float _phase;
        private float _deathTimer = -1f;
        private float _pulse;

        public DefenderRuntime Runtime => _d;
        public bool Finished => _deathTimer > 0.55f;
        public bool Dying => _deathTimer >= 0f;

        private void Awake() => _rig = GetComponent<UnitRig>();

        public void Bind(DefenderRuntime d, VfxSystem vfx, VisualLibrary lib)
        {
            _d = d;
            _vfx = vfx;
            _lib = lib;
            _phase = (d.Coord.lane * 1.7f + d.Coord.column * 0.9f) % 6.28f;
            _spawn.value = -0.6f; // grows in with overshoot
            _spawn.velocity = 0f;
        }

        public void OnRent()
        {
            _deathTimer = -1f;
            _recoil.Reset(); _squash.Reset(); _spawn.Reset();
            _pulse = 0f;
            if (_rig != null) _rig.ResetPose();
        }

        public void OnReturn()
        {
            _d = null;
            if (_rig != null) _rig.ResetPose();
        }

        public void OnActed()
        {
            if (_d == null) return;
            if (_d.Definition.behaviour == DefenderBehaviourKind.Generator)
            {
                _pulse = 1f;
                _squash.Kick(-3f);
                if (_vfx != null) _vfx.Burst(VfxKind.CollectSparkle, transform.position + Vector3.up * 0.6f, new Color(0.45f, 1f, 0.95f), 10, 1.2f, 0.12f, 0.6f);
            }
            else
            {
                _recoil.Kick(9f);
                Transform muzzle = _rig.Get("Muzzle");
                if (_vfx != null && muzzle != null) _vfx.Pulse(VfxKind.MuzzleFlash, muzzle.position, new Color(1f, 0.85f, 0.4f, 0.9f), 0.45f, 0.12f);
            }
        }

        public void OnDamaged()
        {
            _rig.Flash(new Color(1f, 0.45f, 0.35f));
            _squash.Kick(4f);
        }

        public void PlayDeath()
        {
            if (_deathTimer >= 0f) return;
            _deathTimer = 0f;
            if (_vfx != null)
            {
                _vfx.Burst(VfxKind.PlantDust, transform.position + Vector3.up * 0.3f, new Color(0.45f, 0.6f, 0.25f), 14, 1.4f, 0.14f, 0.7f);
            }
        }

        public void Tick(float dt, float time)
        {
            if (_rig == null) return;
            _rig.TickFlash(dt);
            if (_deathTimer >= 0f)
            {
                _deathTimer += dt;
                float k = Mathf.Clamp01(_deathTimer / 0.5f);
                _rig.SetModelSquash(new Vector3(1f + k * 0.3f, Mathf.Max(0.01f, 1f - k), 1f + k * 0.3f));
                return;
            }
            _recoil.Tick(dt, 260f, 16f);
            _squash.Tick(dt, 200f, 12f);
            _spawn.Tick(dt, 120f, 9f);
            _pulse = Mathf.Max(0f, _pulse - dt * 2.2f);

            float t = time + _phase;
            float breathe = Mathf.Sin(t * 2.1f) * 0.025f;
            float s = 1f + _spawn.value;
            float sq = _squash.value * 0.06f;
            _rig.SetModelSquash(new Vector3(s * (1f + sq), s * (1f + breathe - sq), s * (1f + sq)));

            // Shooter: head bob + recoil along the firing axis (model space -X after the recoil kick).
            if (_rig.Has("Head"))
                _rig.Pose("Head", new Vector3(0f, 0f, Mathf.Sin(t * 1.6f) * 4f - _recoil.value * 1.5f),
                    new Vector3(-_recoil.value * 0.012f, Mathf.Sin(t * 2.1f) * 0.01f, 0f),
                    new Vector3(1f + _recoil.value * 0.02f, 1f - _recoil.value * 0.015f, 1f + _recoil.value * 0.02f));
            if (_rig.Has("Stem")) _rig.Pose("Stem", new Vector3(Mathf.Sin(t * 1.3f) * 2f, 0f, Mathf.Sin(t * 1.6f) * 2.5f));
            if (_rig.Has("LeafL")) _rig.Pose("LeafL", new Vector3(Mathf.Sin(t * 1.9f) * 6f, 0f, 0f));
            if (_rig.Has("LeafR")) _rig.Pose("LeafR", new Vector3(-Mathf.Sin(t * 1.9f + 0.8f) * 6f, 0f, 0f));
            // Generator cap pulse.
            if (_rig.Has("Cap"))
            {
                float p = 1f + Mathf.Sin(t * 2.6f) * 0.03f + _pulse * 0.18f;
                _rig.Pose("Cap", new Vector3(Mathf.Sin(t * 1.4f) * 3f, 0f, Mathf.Cos(t * 1.2f) * 3f), Vector3.zero, new Vector3(p, 1f + (p - 1f) * 0.6f, p));
            }
            if (_rig.Has("Babies")) _rig.Pose("Babies", Vector3.zero, Vector3.zero, Vector3.one * (1f + Mathf.Sin(t * 3.1f) * 0.05f));

            // Guardian damage stages.
            if (_d != null && (_rig.Has("PlatesOuter") || _rig.Has("PlatesInner")))
            {
                float hf = _d.HealthFraction;
                _rig.SetActive("PlatesOuter", hf > 0.66f);
                _rig.SetActive("PlatesInner", hf > 0.33f);
            }
        }

        /// <summary>Called by BattleView when a plate layer disappears (debris feedback).</summary>
        public float LastHealthFraction { get; set; } = 1f;
    }
}
