using UnityEngine;
using VerdantSiege.Data;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Presentation of the encounter boss (Root Maw parts: Body, Jaw, RootL, RootR, Eye, Core). It rises at the entry
    /// edge, opens its jaw and lifts its roots during telegraphs, slams when an attack lands, swells its glowing Core
    /// during vulnerability windows, shudders through phase changes and sinks when defeated. Purely visual.
    /// </summary>
    [RequireComponent(typeof(UnitRig))]
    public sealed class BossView : MonoBehaviour, IPooledView
    {
        private UnitRig _rig;
        private BossRuntime _boss;
        private VfxSystem _vfx;
        private Spring _slam, _hit;
        private BossState _lastState;
        private float _stateTime;
        private float _glowTimer;
        private float _deathTime = -1f;
        private Vector3 _home;

        public bool Finished => _deathTime > 2.2f;

        private void Awake() => _rig = GetComponent<UnitRig>();

        public void Bind(BossRuntime boss, Vector3 home, VfxSystem vfx)
        {
            _boss = boss;
            _vfx = vfx;
            _home = home;
            transform.position = home;
            _lastState = boss.state;
            _stateTime = 0f;
            _deathTime = -1f;
            _rig.SetModelSquash(new Vector3(1f, 0.02f, 1f));
        }

        public void OnRent() { if (_rig != null) _rig.ResetPose(); _slam.Reset(); _hit.Reset(); }
        public void OnReturn() { _boss = null; if (_rig != null) _rig.ResetPose(); }

        public void OnDamaged(bool exposed)
        {
            _rig.Flash(exposed ? new Color(1f, 0.6f, 0.9f) : new Color(0.9f, 0.85f, 0.8f), 0.08f);
            _hit.Kick(exposed ? 4f : 1.5f);
        }

        public void OnAttack(BossAttack a)
        {
            _slam.Kick(a.kind == BossAttackKind.RootEruption ? 14f : 8f);
            if (_vfx != null && a.kind == BossAttackKind.Summon)
                _vfx.Burst(VfxKind.SporePuff, transform.position + new Vector3(-1f, 0.4f, 0f), new Color(0.6f, 0.3f, 0.65f), 24, 2.4f, 0.24f, 0.8f);
        }

        public void OnPhaseChanged()
        {
            _hit.Kick(10f);
            if (_vfx != null)
            {
                _vfx.Pulse(VfxKind.Glow, transform.position + Vector3.up * 1.2f, new Color(1f, 0.35f, 0.6f, 0.8f), 6f, 0.6f);
                _vfx.Burst(VfxKind.ArmorDebris, transform.position + Vector3.up * 1.5f, new Color(0.45f, 0.3f, 0.3f), 30, 3f, 0.2f, 1f);
            }
        }

        public void OnDefeated() => _deathTime = 0f;

        public void Tick(float dt, float time)
        {
            if (_boss == null || _rig == null) return;
            _rig.TickFlash(dt);
            _slam.Tick(dt, 140f, 10f);
            _hit.Tick(dt, 200f, 12f);
            if (_boss.state != _lastState) { _lastState = _boss.state; _stateTime = 0f; }
            _stateTime += dt;
            float t = time;

            if (_deathTime >= 0f)
            {
                _deathTime += dt;
                float k = Mathf.Clamp01(_deathTime / 2f);
                _rig.SetModelSquash(new Vector3(1f + k * 0.2f, Mathf.Max(0.02f, 1f - k * k), 1f + k * 0.1f), Mathf.Sin(t * 30f) * 4f * (1f - k));
                if (_vfx != null && Random.value < 0.4f)
                    _vfx.Burst(VfxKind.SporePuff, transform.position + new Vector3(Random.Range(-1f, 1f), 0.5f, Random.Range(-2.2f, 2.2f)), new Color(0.6f, 0.35f, 0.6f), 4, 1.2f, 0.25f, 0.8f);
                return;
            }

            switch (_boss.state)
            {
                case BossState.Dormant:
                    _rig.SetModelSquash(new Vector3(1f, 0.02f, 1f));
                    return;
                case BossState.Rising:
                {
                    float k = Mathf.Clamp01(_stateTime / Mathf.Max(0.5f, _boss.Definition.entranceSeconds));
                    _rig.SetModelSquash(new Vector3(1f, Mathf.Max(0.02f, k), 1f), Mathf.Sin(t * 25f) * 3f * (1f - k));
                    if (_vfx != null && Random.value < 0.5f)
                        _vfx.Burst(VfxKind.PlantDust, transform.position + new Vector3(Random.Range(-1.2f, 0.4f), 0.1f, Random.Range(-2.4f, 2.4f)), new Color(0.5f, 0.38f, 0.28f), 5, 1.6f, 0.22f, 0.8f);
                    return;
                }
            }

            bool tele = _boss.state == BossState.Telegraph;
            bool roar = _boss.state == BossState.Transition;
            float breathe = 1f + Mathf.Sin(t * 1.3f) * 0.025f;
            float shake = roar ? Mathf.Sin(t * 38f) * 3f : 0f;
            _rig.SetModelSquash(new Vector3(breathe + _hit.value * 0.01f, breathe - _slam.value * 0.01f, 1f), shake);
            transform.position = _home + new Vector3(_slam.value * 0.02f, 0f, 0f);

            float jaw = 6f + Mathf.Sin(t * 1.1f) * 4f;
            if (tele) jaw = Mathf.Lerp(6f, 40f, Mathf.Clamp01(_stateTime / 0.6f)) + Mathf.Sin(t * 22f) * 3f;
            if (roar) jaw = 50f + Mathf.Sin(t * 30f) * 5f;
            _rig.Pose("Jaw", new Vector3(0f, 0f, -jaw));
            float rootLift = tele ? Mathf.Clamp01(_stateTime / 0.8f) * 35f : 0f;
            _rig.Pose("RootL", new Vector3(rootLift + Mathf.Sin(t * 1.7f) * 6f, 0f, -_slam.value * 2f));
            _rig.Pose("RootR", new Vector3(-rootLift - Mathf.Sin(t * 1.5f) * 6f, 0f, -_slam.value * 2f));
            _rig.Pose("Eye", new Vector3(0f, Mathf.Sin(t * 0.7f) * 25f, Mathf.Sin(t * 0.5f) * 10f), Vector3.zero, Vector3.one * (tele ? 1.25f : 1f));

            bool exposed = _boss.CoreExposed;
            float core = exposed ? 1.45f + Mathf.Sin(t * 9f) * 0.12f : 0.75f;
            _rig.Pose("Core", Vector3.zero, new Vector3(exposed ? -0.15f : 0.1f, 0f, 0f), Vector3.one * core);
            _rig.SetTint(tele ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.5f, 0.85f), tele ? 0.25f + 0.15f * Mathf.Sin(t * 10f) : exposed ? 0.25f : 0f);
            if (exposed && _vfx != null)
            {
                _glowTimer -= dt;
                if (_glowTimer <= 0f)
                {
                    _glowTimer = 0.25f;
                    Transform c = _rig.Get("Core");
                    if (c != null) _vfx.Pulse(VfxKind.Glow, c.position, new Color(1f, 0.45f, 0.85f, 0.75f), 1.8f, 0.3f);
                }
            }
        }
    }
}
