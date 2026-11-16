using UnityEngine;
using VerdantSiege.Combat;
using VerdantSiege.Data;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Presentation of one planted defender. Reads authoritative state from <see cref="DefenderRuntime"/>; all
    /// motion is procedural on the Blender part hierarchy (Head/Stem/Leaf/Cap/Plates, and for Milestone 3
    /// Jaw/Fronds/Crystal/Bloom/Core/Rotor/Pod/Mound/Orbs/Ring/Sprout/Spikes/Petals/Rod/Mirror).
    /// Missing parts are skipped, so placeholder rigs and final art share this code.
    /// </summary>
    [RequireComponent(typeof(UnitRig))]
    public sealed class DefenderView : MonoBehaviour, IPooledView
    {
        private UnitRig _rig;
        private DefenderRuntime _d;
        private VfxSystem _vfx;
        private VisualLibrary _lib;
        private Spring _recoil, _squash, _spawn, _snap;
        private float _phase;
        private float _deathTimer = -1f;
        private float _pulse;
        private float _stateTime;
        private float _rotor;

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
            _stateTime = 0f;
        }

        public void OnRent()
        {
            _deathTimer = -1f;
            _recoil.Reset(); _squash.Reset(); _spawn.Reset(); _snap.Reset();
            _pulse = 0f;
            _stateTime = 0f;
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
            DefenderDefinition def = _d.Definition;
            Vector3 p = transform.position;
            switch (def.behaviour)
            {
                case DefenderBehaviourKind.Generator:
                    _pulse = 1f;
                    _squash.Kick(-3f);
                    if (_vfx != null) _vfx.Burst(VfxKind.CollectSparkle, p + Vector3.up * 0.6f, new Color(0.45f, 1f, 0.95f), 10, 1.2f, 0.12f, 0.6f);
                    break;
                case DefenderBehaviourKind.Ambusher:
                    _snap.Kick(-14f);
                    _squash.Kick(6f);
                    break;
                case DefenderBehaviourKind.Trap:
                    _snap.Kick(16f);
                    if (_vfx != null)
                    {
                        _vfx.Burst(VfxKind.PlantDust, p + Vector3.up * 0.15f, new Color(0.6f, 0.45f, 0.3f), 22, 2.4f, 0.2f, 0.7f);
                        _vfx.Pulse(VfxKind.Glow, p + Vector3.up * 0.2f, new Color(1f, 0.75f, 0.4f, 0.8f), 1.6f, 0.25f);
                    }
                    break;
                case DefenderBehaviourKind.Mender:
                    _pulse = 1f;
                    _squash.Kick(-4f);
                    if (_vfx != null) _vfx.Pulse(VfxKind.Glow, p + Vector3.up * 0.3f, new Color(0.45f, 1f, 0.55f, 0.55f), 3.2f, 0.5f);
                    break;
                case DefenderBehaviourKind.Decoy:
                    _pulse = 1f;
                    if (_vfx != null) _vfx.Pulse(VfxKind.Glow, p + Vector3.up * 0.7f, new Color(0.85f, 1f, 1f, 0.8f), 1.4f, 0.3f);
                    break;
                default:
                    _recoil.Kick(9f);
                    _pulse = 1f;
                    Transform muzzle = _rig.Get("Muzzle");
                    Color flash = Color.Lerp(new Color(1f, 0.85f, 0.4f), def.accentColor, 0.5f);
                    flash.a = 0.9f;
                    if (_vfx != null && muzzle != null) _vfx.Pulse(VfxKind.MuzzleFlash, muzzle.position, flash, 0.45f, 0.12f);
                    break;
            }
        }

        /// <summary>Ability telegraph started (Snapjaw gaping).</summary>
        public void OnTelegraph() => _stateTime = 0f;

        public void OnStateChanged()
        {
            _stateTime = 0f;
            if (_d != null && _d.Definition.behaviour == DefenderBehaviourKind.Trap && _d.abilityState == DefenderAbilityState.Ready && _vfx != null)
                _vfx.Burst(VfxKind.PlantDust, transform.position + Vector3.up * 0.05f, new Color(0.55f, 0.42f, 0.28f), 8, 0.8f, 0.12f, 0.4f);
        }

        public void OnHealed()
        {
            _rig.Flash(new Color(0.5f, 1f, 0.55f), 0.14f);
            if (_vfx != null) _vfx.Burst(VfxKind.CollectSparkle, transform.position + Vector3.up * 0.5f, new Color(0.5f, 1f, 0.6f), 5, 0.9f, 0.1f, 0.5f);
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
            _snap.Tick(dt, 200f, 11f);
            _pulse = Mathf.Max(0f, _pulse - dt * 2.2f);
            _stateTime += dt;

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
            float stemLean = 0f;
            if (_d != null && _d.Definition.behaviour == DefenderBehaviourKind.Ambusher && _d.abilityState == DefenderAbilityState.Recovering) stemLean = -10f; // drooping, digesting
            if (_rig.Has("Stem")) _rig.Pose("Stem", new Vector3(Mathf.Sin(t * 1.3f) * 2f, 0f, Mathf.Sin(t * 1.6f) * 2.5f + stemLean));
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

            if (_d != null) PoseM3(t, dt);
            UpdateTint(time);
        }

        private void PoseM3(float t, float dt)
        {
            DefenderAbilityState st = _d.abilityState;
            // Snapjaw: idle breathing, gaping telegraph, closed while digesting.
            if (_rig.Has("Jaw") || _rig.Has("JawLower"))
            {
                float open;
                if (st == DefenderAbilityState.Windup) open = Mathf.Clamp01(_stateTime / 0.15f) * 55f + Mathf.Sin(t * 40f) * 3f;
                else if (st == DefenderAbilityState.Recovering) open = Mathf.Sin(t * 3f) * 3f; // slow chewing
                else open = 10f + Mathf.Sin(t * 1.8f) * 6f;
                open = Mathf.Max(-5f, open + _snap.value * 4f);
                _rig.Pose("Jaw", new Vector3(0f, 0f, open));
                _rig.Pose("JawLower", new Vector3(0f, 0f, -open * 0.6f));
            }
            if (_rig.Has("Fronds")) _rig.Pose("Fronds", new Vector3(Mathf.Sin(t * 1.5f) * 5f, Mathf.Sin(t * 0.8f) * 8f, 0f));
            if (_rig.Has("Crystal"))
            {
                float c = 1f + _pulse * 0.3f + Mathf.Sin(t * 3f) * 0.04f;
                _rig.Pose("Crystal", new Vector3(0f, Mathf.Sin(t * 0.9f) * 15f, 0f), // sway, not spin: the Muzzle is parented under Crystal
                    new Vector3(0f, Mathf.Sin(t * 2f) * 0.02f, 0f), Vector3.one * c);
            }
            if (_rig.Has("Bloom"))
            {
                float b = 1f + _pulse * 0.25f + Mathf.Sin(t * 2.4f) * 0.03f;
                _rig.Pose("Bloom", new Vector3(0f, Mathf.Sin(t * 0.7f) * 10f, 0f), Vector3.zero, new Vector3(b, 1f + (b - 1f) * 0.5f, b));
            }
            if (_rig.Has("Core")) _rig.Pose("Core", Vector3.zero, Vector3.zero, Vector3.one * (1f + _pulse * 0.4f + Mathf.Sin(t * 5f) * 0.05f));
            if (_rig.Has("Rotor"))
            {
                _rotor += dt * (220f + _pulse * 900f);
                _rig.Pose("Rotor", new Vector3(0f, _rotor % 360f, 0f));
            }
            if (_rig.Has("Pod")) _rig.Pose("Pod", Vector3.zero, Vector3.zero, Vector3.one * (1f + _recoil.value * 0.02f));
            if (_rig.Has("Mound")) _rig.Pose("Mound", Vector3.zero, Vector3.zero, new Vector3(1f + _pulse * 0.08f, 1f + Mathf.Sin(t * 1.7f) * 0.04f, 1f + _pulse * 0.08f));
            if (_rig.Has("Orbs")) _rig.Pose("Orbs", new Vector3(0f, t * 45f % 360f, 0f), new Vector3(0f, 0.04f * Mathf.Sin(t * 2f) + _pulse * 0.1f, 0f), Vector3.one);
            // Burrowroot: hidden while armed (only a sprout peeks out); spikes burst up on eruption and sink while regrowing.
            if (_rig.Has("Spikes") || _rig.Has("Sprout"))
            {
                bool armed = st != DefenderAbilityState.Recovering;
                float spikes;
                if (armed) spikes = 0.02f; // modelled in the erupted pose, pivot on the ground: hidden while armed
                else spikes = Mathf.Max(0.02f, 1.15f - _stateTime * 0.25f) + _snap.value * 0.05f;
                _rig.Pose("Spikes", Vector3.zero, Vector3.zero, new Vector3(1f, Mathf.Max(0.02f, spikes), 1f));
                float sprout = armed ? 1f + Mathf.Sin(t * 2.5f) * 0.08f : 0.4f;
                _rig.Pose("Sprout", new Vector3(Mathf.Sin(t * 2f) * 8f, 0f, 0f), Vector3.zero, Vector3.one * sprout);
                if (_rig.Has("Ring")) _rig.Pose("Ring", Vector3.zero, Vector3.zero, new Vector3(1f, armed ? 0.6f : 1f, 1f));
            }
            if (_rig.Has("Petals"))
            {
                bool charging = _d.attackTimer > 0f && _d.attackTimer < 0.45f;
                float open = 1f + (charging ? 0.15f : 0f) + _pulse * 0.2f;
                _rig.Pose("Petals", new Vector3(0f, t * 20f % 360f, 0f), Vector3.zero, new Vector3(open, 1f, open));
                if (charging && _vfx != null && Random.value < 0.3f)
                {
                    Transform rod = _rig.Get("Rod");
                    if (rod != null) _vfx.Pulse(VfxKind.Glow, rod.position, new Color(0.7f, 0.75f, 1f, 0.7f), 0.35f, 0.08f);
                }
            }
            if (_rig.Has("Rod")) _rig.Pose("Rod", Vector3.zero, Vector3.zero, Vector3.one * (1f + _pulse * 0.3f));
            if (_rig.Has("Mirror"))
            {
                float sway = Mathf.Sin(t * 0.9f) * 18f;
                _rig.Pose("Mirror", new Vector3(0f, sway, 0f), Vector3.zero, Vector3.one * (1f + _pulse * 0.12f));
            }
        }

        private void UpdateTint(float time)
        {
            if (_d == null) return;
            Color c = default;
            float strength = 0f;
            var list = _d.statuses.Active;
            for (int i = 0; i < list.Count; i++)
            {
                c = list[i].definition.tint;
                strength = 0.55f + Mathf.Sin(time * 6f) * 0.1f;
            }
            if (strength <= 0f && _d.Definition.behaviour == DefenderBehaviourKind.Ambusher && _d.abilityState == DefenderAbilityState.Recovering)
            {
                c = new Color(0.25f, 0.2f, 0.3f);
                strength = 0.35f;
            }
            if (strength <= 0f && _d.Definition.behaviour == DefenderBehaviourKind.Trap && _d.abilityState == DefenderAbilityState.Ready)
            {
                c = new Color(1f, 0.8f, 0.45f);
                strength = 0.15f + 0.1f * Mathf.Sin(time * 3f);
            }
            _rig.SetTint(c, strength);
        }

        /// <summary>Called by BattleView when a plate layer disappears (debris feedback).</summary>
        public float LastHealthFraction { get; set; } = 1f;
    }
}
