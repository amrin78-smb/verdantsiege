using System;
using UnityEngine;
using VerdantSiege.Data;
using VerdantSiege.Visual;
using Random = UnityEngine.Random;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Presentation of the encounter boss (Root Maw parts: Body, Jaw, RootL, RootR, Eye, Core; optional WeakPoint0..n).
    /// Purely visual, driven by <see cref="BossRuntime"/> state and the sim events forwarded by BattleView:
    /// - entrance: tremors underground, eruption through the soil with tearing roots, then a roar;
    /// - one wind-up and landing animation per attack shape (Root Slam lunge, Sweeping Grasp yaw sweep, Frenzy
    ///   multi-jolts) and a rearing special that exposes weak points;
    /// - stagger (slumped, dazed, Core swollen), phase roars, and a death sequence (thrash, burst, collapse, sink).
    /// Camera impacts and roars are raised as events so <see cref="BossCinematics"/> owns shake and sound.
    /// </summary>
    [RequireComponent(typeof(UnitRig))]
    public sealed class BossView : MonoBehaviour, IPooledView
    {
        private enum Anim { None, Slam, Sweep, Frenzy, Special, Summon }

        /// <summary>Camera impact request (0..1).</summary>
        public event Action<float> ImpactRequested;
        /// <summary>Sustained rumble request (amount, seconds).</summary>
        public event Action<float, float> RumbleRequested;
        /// <summary>The boss roars (entrance, phase change, death).</summary>
        public event Action Roared;
        /// <summary>It broke through the ground during the entrance.</summary>
        public event Action Erupted;

        private UnitRig _rig;
        private BossRuntime _boss;
        private VfxSystem _vfx;
        private BossWeakPointsView _pods;
        private Spring _slam, _hit, _sway;
        private BossState _lastState;
        private float _stateTime;
        private float _glowTimer;
        private float _deathTime = -1f;
        private float _entranceTime = -1f;
        private bool _erupted, _entranceRoared;
        private Anim _anim;
        private float _animTime = 99f;
        private float _sweepDir = 1f;
        private int _frenzyJolts;
        private Vector3 _home;
        private float _fieldLanes = 5f;
        private bool _deathBurst, _deathSpores;
        private float _fade = 1f;

        /// <summary>Death sequence complete (collapsed and sunk).</summary>
        public bool Finished => _deathTime > 3.2f;
        public float DeathTime => _deathTime;
        public Vector3 Home => _home;
        public BossWeakPointsView WeakPoints => _pods;

        private void Awake() => _rig = GetComponent<UnitRig>();

        public void Bind(BossRuntime boss, Vector3 home, float scale, VfxSystem vfx, BattleLayout layout, VisualLibrary lib, float frontX)
        {
            _boss = boss;
            _vfx = vfx;
            _home = home;
            _fieldLanes = layout.Lanes;
            transform.position = home;
            transform.localScale = Vector3.one * scale;
            _lastState = boss.state;
            _stateTime = 0f;
            _deathTime = -1f;
            _entranceTime = -1f;
            _rig.SetModelSquash(new Vector3(1f, 0.02f, 1f));
            _pods = gameObject.GetComponent<BossWeakPointsView>();
            if (_pods == null) _pods = gameObject.AddComponent<BossWeakPointsView>();
            _pods.Init(boss, _rig, layout, lib, vfx, frontX);
        }

        public void OnRent() { if (_rig != null) _rig.ResetPose(); _slam.Reset(); _hit.Reset(); _sway.Reset(); }
        public void OnReturn() { _boss = null; if (_rig != null) _rig.ResetPose(); }

        // ------------------------------------------------------------------ event hooks (from BattleView)

        public void OnDamaged(bool exposed)
        {
            _rig.Flash(exposed ? new Color(1f, 0.6f, 0.9f) : new Color(0.9f, 0.85f, 0.8f), 0.08f);
            _hit.Kick(exposed ? 4f : 1.5f);
        }

        public void OnTelegraph(BossAttack a)
        {
            if (a.kind == BossAttackKind.RootEruption && a.shape == BossStrikeShape.LaneSweep && _boss.targetCells.Count > 0)
                _sweepDir = _boss.targetCells[0].lane < (_fieldLanes - 1f) * 0.5f ? -1f : 1f;
        }

        public void OnAttack(BossAttack a)
        {
            _anim = AnimOf(a);
            _animTime = 0f;
            _frenzyJolts = 0;
            switch (_anim)
            {
                case Anim.Slam:
                    _slam.Kick(16f);
                    ImpactRequested?.Invoke(0.32f + 0.05f * _boss.targetCells.Count);
                    break;
                case Anim.Sweep:
                    _sway.Kick(-_sweepDir * 30f);
                    ImpactRequested?.Invoke(0.38f + 0.04f * _boss.targetCells.Count);
                    break;
                case Anim.Frenzy:
                    _slam.Kick(10f);
                    ImpactRequested?.Invoke(0.3f);
                    _frenzyJolts = 1;
                    break;
                case Anim.Special:
                    _slam.Kick(26f);
                    ImpactRequested?.Invoke(0.8f);
                    if (_vfx != null)
                    {
                        _vfx.Pulse(VfxKind.Glow, transform.position + new Vector3(-1.2f, 0.4f, 0f), new Color(1f, 0.15f, 0.2f, 0.9f), 7f, 0.5f);
                        _vfx.Burst(VfxKind.ArmorDebris, transform.position + new Vector3(-1.4f, 0.3f, 0f), new Color(0.4f, 0.26f, 0.2f), 40, 4f, 0.24f, 1.1f);
                    }
                    break;
                case Anim.Summon:
                    _slam.Kick(8f);
                    if (_vfx != null)
                        _vfx.Burst(VfxKind.SporePuff, transform.position + new Vector3(-1f, 0.4f, 0f), new Color(0.6f, 0.3f, 0.65f), 24, 2.4f, 0.24f, 0.8f);
                    break;
            }
        }

        public void OnStaggered()
        {
            _hit.Kick(14f);
            _slam.Reset();
            _anim = Anim.None;
            _rig.Flash(new Color(1f, 0.9f, 1f), 0.25f);
            ImpactRequested?.Invoke(0.55f);
            if (_vfx == null) return;
            Transform core = _rig.Get("Core");
            Vector3 c = core != null ? core.position : transform.position + Vector3.up * 1.2f;
            _vfx.Pulse(VfxKind.Glow, c, new Color(1f, 0.55f, 0.95f, 1f), 5f, 0.6f);
            _vfx.Burst(VfxKind.CollectSparkle, c, new Color(1f, 0.85f, 1f), 40, 3.5f, 0.2f, 0.9f);
        }

        public void OnPhaseChanged()
        {
            _hit.Kick(10f);
            Roared?.Invoke();
            ImpactRequested?.Invoke(0.5f);
            if (_vfx != null)
            {
                _vfx.Pulse(VfxKind.Glow, transform.position + Vector3.up * 1.2f, new Color(1f, 0.35f, 0.6f, 0.8f), 6f, 0.6f);
                _vfx.Burst(VfxKind.ArmorDebris, transform.position + Vector3.up * 1.5f, new Color(0.45f, 0.3f, 0.3f), 30, 3f, 0.2f, 1f);
            }
        }

        public void OnDefeated()
        {
            _deathTime = 0f;
            _deathBurst = _deathSpores = false;
            Roared?.Invoke();
            RumbleRequested?.Invoke(0.45f, 2.2f);
            ImpactRequested?.Invoke(0.6f);
        }

        /// <summary>Readability: 1 = opaque, lower = the boss steps back visually (dimmed) while it overlaps play space.</summary>
        public void SetFade(float fade) => _fade = fade;

        private static Anim AnimOf(BossAttack a)
        {
            if (a == null) return Anim.None;
            if (a.kind == BossAttackKind.Summon) return Anim.Summon;
            if (a.exposesWeakPoints) return Anim.Special;
            switch (a.shape)
            {
                case BossStrikeShape.LaneSweep: return Anim.Sweep;
                case BossStrikeShape.MultiColumn: return Anim.Frenzy;
                default: return Anim.Slam;
            }
        }

        // ------------------------------------------------------------------ frame

        public void Tick(float dt, float time)
        {
            if (_boss == null || _rig == null) return;
            _rig.TickFlash(dt);
            _slam.Tick(dt, 140f, 10f);
            _hit.Tick(dt, 200f, 12f);
            _sway.Tick(dt, 60f, 7f);
            if (_boss.state != _lastState) { _lastState = _boss.state; _stateTime = 0f; }
            _stateTime += dt;
            _animTime += dt;
            TickPose(dt, time);
            if (_pods != null) _pods.Tick(dt, time, _deathTime); // after posing: art pods follow the posed body
        }

        private void TickPose(float dt, float time)
        {
            if (_deathTime >= 0f) { TickDeath(dt, time); return; }
            switch (_boss.state)
            {
                case BossState.Dormant:
                    _rig.SetModelSquash(new Vector3(1f, 0.02f, 1f));
                    transform.position = _home + Vector3.down * 3f;
                    return;
                case BossState.Rising:
                    TickEntrance(dt, time);
                    return;
            }
            _entranceTime = -1f;
            TickAlive(dt, time);
        }

        private void TickEntrance(float dt, float time)
        {
            if (_entranceTime < 0f) { _entranceTime = 0f; _erupted = _entranceRoared = false; RumbleRequested?.Invoke(0.3f, 1.7f); }
            _entranceTime += dt;
            float e = Mathf.Max(1.5f, _boss.Definition.entranceSeconds);
            float t = _entranceTime;
            float eruptAt = e * 0.36f, roarAt = e * 0.58f;
            if (t < eruptAt)
            {
                // Underground: the soil trembles and cracks along the entry edge.
                _rig.SetModelSquash(new Vector3(1f, 0.02f, 1f));
                transform.position = _home + Vector3.down * 3f;
                if (_vfx != null && Random.value < 0.6f)
                    _vfx.Burst(VfxKind.PlantDust, _home + new Vector3(Random.Range(-1.4f, 0.3f), 0.1f, Random.Range(-2.4f, 2.4f)), new Color(0.5f, 0.38f, 0.28f), 4, 1.4f + t, 0.22f, 0.8f);
                return;
            }
            if (!_erupted)
            {
                _erupted = true;
                Erupted?.Invoke();
                ImpactRequested?.Invoke(0.75f);
                if (_vfx != null)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        // Roots tear up out of the soil along the edge.
                        Vector3 p = _home + new Vector3(Random.Range(-1.5f, -0.4f), 0.15f, -2.3f + i * 0.92f);
                        _vfx.Burst(VfxKind.ArmorDebris, p, new Color(0.42f, 0.27f, 0.2f), 14, 4.2f, 0.2f, 1.1f);
                        _vfx.Burst(VfxKind.PlantDust, p, new Color(0.55f, 0.42f, 0.3f), 10, 2.6f, 0.34f, 1.2f);
                    }
                    _vfx.Pulse(VfxKind.Glow, _home + Vector3.up * 0.8f, new Color(1f, 0.4f, 0.8f, 0.7f), 6f, 0.5f);
                }
            }
            // Burst up with an overshoot, then settle.
            float k = Mathf.Clamp01((t - eruptAt) / 0.55f);
            float rise = 1f - Mathf.Pow(1f - k, 3f);
            float over = Mathf.Sin(Mathf.Clamp01((t - eruptAt) / 0.9f) * Mathf.PI) * 0.12f;
            float shake = Mathf.Sin(time * 34f) * 4f * (1f - k);
            _rig.SetModelSquash(new Vector3(1f - over * 0.4f, Mathf.Max(0.02f, rise + over), 1f), shake);
            transform.position = _home + Vector3.down * (3f * (1f - rise));
            float rootsUp = Mathf.Clamp01((t - eruptAt) / 0.4f) * (1f - Mathf.Clamp01((t - roarAt) / 0.6f));
            _rig.Pose("RootL", new Vector3(60f * rootsUp, 0f, 0f));
            _rig.Pose("RootR", new Vector3(-60f * rootsUp, 0f, 0f));
            if (t >= roarAt && !_entranceRoared)
            {
                _entranceRoared = true;
                Roared?.Invoke();
                ImpactRequested?.Invoke(0.45f);
                if (_vfx != null) _vfx.Pulse(VfxKind.Glow, transform.position + Vector3.up * 1.4f, new Color(1f, 0.35f, 0.65f, 0.8f), 5f, 0.5f);
            }
            float roar = Mathf.Clamp01((t - roarAt) / 0.25f) * (1f - Mathf.Clamp01((t - roarAt - 1f) / 0.5f));
            _rig.Pose("Jaw", new Vector3(0f, 0f, -(6f + 52f * roar + Mathf.Sin(time * 30f) * 4f * roar)));
            _rig.Pose("Body", new Vector3(0f, 0f, 10f * roar));
            _rig.Pose("Eye", Vector3.zero, Vector3.zero, Vector3.one * (1f + 0.4f * roar));
            _rig.SetTint(new Color(1f, 0.45f, 0.75f), 0.25f * roar);
        }

        private void TickAlive(float dt, float time)
        {
            float t = time;
            bool tele = _boss.state == BossState.Telegraph;
            bool roar = _boss.state == BossState.Transition;
            bool stagger = _boss.state == BossState.Staggered;
            BossAttack a = _boss.currentAttack;
            Anim teleAnim = tele ? AnimOf(a) : Anim.None;
            float windup = tele ? Mathf.Clamp01(_stateTime / 0.8f) : 0f;
            float urgency = tele && _boss.stateDuration > 0f ? 1f - Mathf.Clamp01(_boss.stateTimer / _boss.stateDuration) : 0f;

            float breathe = 1f + Mathf.Sin(t * 1.3f) * 0.025f;
            float yaw = _sway.value;
            float lean = 0f, rear = 0f, jaw = 6f + Mathf.Sin(t * 1.1f) * 4f;
            float rootL = Mathf.Sin(t * 1.7f) * 6f, rootR = -Mathf.Sin(t * 1.5f) * 6f, rootSlam = _slam.value * 2f;
            Vector3 lunge = new Vector3(_slam.value * 0.02f, 0f, 0f);
            Color tint = new Color(1f, 0.5f, 0.85f);
            float tintK = 0f;

            switch (teleAnim)
            {
                case Anim.Slam: // rear back, both roots raised high
                    lean = 10f * windup; jaw = Mathf.Lerp(6f, 34f, windup) + Mathf.Sin(t * 22f) * 3f;
                    rootL += 55f * windup; rootR -= 55f * windup;
                    tint = new Color(1f, 0.3f, 0.3f); tintK = 0.2f + 0.15f * Mathf.Sin(t * 10f);
                    break;
                case Anim.Sweep: // coil to one side, the near root drawn back
                    yaw += _sweepDir * 22f * windup; lean = 5f * windup; jaw = 20f * windup + 6f;
                    if (_sweepDir < 0f) rootL += 70f * windup; else rootR -= 70f * windup;
                    tint = new Color(1f, 0.6f, 0.2f); tintK = 0.2f + 0.12f * Mathf.Sin(t * 8f);
                    break;
                case Anim.Frenzy: // trembling, alternating roots pumping on a fast beat
                {
                    float beat = Mathf.Sin(t * (14f + urgency * 10f));
                    rootL += 40f * windup * Mathf.Max(0f, beat); rootR -= 40f * windup * Mathf.Max(0f, -beat);
                    yaw += Mathf.Sin(t * 31f) * 3f * windup; jaw = 26f + beat * 10f;
                    tint = Color.Lerp(new Color(1f, 0.25f, 0.8f), new Color(1f, 0.55f, 0.15f), 0.5f + 0.5f * beat); tintK = 0.3f;
                    break;
                }
                case Anim.Special: // rears up, jaw wide, roots spread to present the pods
                    rear = 0.14f * windup; lean = 16f * windup; jaw = 42f * windup + Mathf.Sin(t * (6f + urgency * 18f)) * 5f;
                    rootL += 35f * windup; rootR -= 35f * windup;
                    tint = new Color(0.85f, 0.08f, 0.15f); tintK = 0.15f + 0.25f * urgency * (0.6f + 0.4f * Mathf.Sin(t * (5f + urgency * 15f)));
                    break;
                case Anim.Summon:
                    rootL += 30f * windup; rootR -= 30f * windup; jaw = 18f;
                    break;
            }

            // Landing animations (after BossAttacked).
            if (_animTime < 0.9f)
            {
                float k = _animTime / 0.9f;
                switch (_anim)
                {
                    case Anim.Slam:
                        lean -= 18f * (1f - k); rootSlam += 30f * (1f - k);
                        break;
                    case Anim.Sweep:
                        yaw += -_sweepDir * 28f * Mathf.Sin(k * Mathf.PI);
                        if (_sweepDir < 0f) rootL -= 50f * (1f - k); else rootR += 50f * (1f - k);
                        break;
                    case Anim.Frenzy:
                        // Three quick jolts in rhythm (the hazard flashes match them).
                        if (_frenzyJolts < 3 && _animTime > _frenzyJolts * 0.16f)
                        {
                            _frenzyJolts++;
                            _slam.Kick(9f);
                            ImpactRequested?.Invoke(0.22f);
                        }
                        lean -= 10f * (1f - k);
                        break;
                    case Anim.Special:
                        lean -= 26f * (1f - k); rootSlam += 45f * (1f - k); jaw = Mathf.Lerp(4f, jaw, k);
                        break;
                }
            }

            if (stagger)
            {
                // Dazed: slumped forward, jaw slack, slow wobble, the Core swollen and bright.
                float s = Mathf.Clamp01(_stateTime / 0.3f);
                lean = -14f * s; jaw = 30f + Mathf.Sin(t * 2.2f) * 5f; yaw = Mathf.Sin(t * 2.6f) * 7f * s;
                rootL = -25f * s; rootR = 25f * s;
                tint = new Color(1f, 0.6f, 0.95f); tintK = 0.25f + 0.1f * Mathf.Sin(t * 6f);
                if (_vfx != null && Random.value < 0.25f)
                {
                    float ang = t * 4f + Random.value;
                    _vfx.Burst(VfxKind.CollectSparkle, transform.position + new Vector3(Mathf.Cos(ang) * 0.9f - 0.3f, 2.9f, Mathf.Sin(ang) * 0.9f),
                        new Color(1f, 0.95f, 0.7f), 1, 0.2f, 0.16f, 0.5f);
                }
            }
            if (roar) { jaw = 50f + Mathf.Sin(t * 30f) * 5f; yaw += Mathf.Sin(t * 38f) * 3f; rear = 0.06f; }

            _rig.SetModelSquash(new Vector3(breathe + _hit.value * 0.01f, breathe + rear - _slam.value * 0.01f - (stagger ? 0.1f : 0f), 1f), yaw);
            transform.position = _home + lunge;
            _rig.Pose("Body", new Vector3(0f, 0f, lean));
            _rig.Pose("Jaw", new Vector3(0f, 0f, -jaw));
            _rig.Pose("RootL", new Vector3(rootL, 0f, -rootSlam));
            _rig.Pose("RootR", new Vector3(rootR, 0f, -rootSlam));
            _rig.Pose("Eye", new Vector3(0f, Mathf.Sin(t * 0.7f) * 25f, Mathf.Sin(t * 0.5f) * 10f), Vector3.zero,
                Vector3.one * (stagger ? 0.7f : tele ? 1.25f : 1f));

            bool exposed = _boss.CoreExposed;
            float core = stagger ? 1.8f + Mathf.Sin(t * 7f) * 0.15f : exposed ? 1.45f + Mathf.Sin(t * 9f) * 0.12f : 0.75f;
            _rig.Pose("Core", Vector3.zero, new Vector3(exposed ? -0.15f : 0.1f, 0f, 0f), Vector3.one * core);
            if (exposed && tintK <= 0f) tintK = 0.25f;
            if (_fade < 1f) { tint = Color.Lerp(tint, new Color(0.08f, 0.05f, 0.08f), 1f - _fade); tintK = Mathf.Max(tintK, (1f - _fade) * 0.6f); }
            _rig.SetTint(tint, tintK);
            if (exposed && _vfx != null)
            {
                _glowTimer -= dt;
                if (_glowTimer <= 0f)
                {
                    _glowTimer = stagger ? 0.15f : 0.25f;
                    Transform c = _rig.Get("Core");
                    if (c != null) _vfx.Pulse(VfxKind.Glow, c.position, new Color(1f, 0.45f, 0.85f, 0.75f), stagger ? 2.6f : 1.8f, 0.3f);
                }
            }
        }

        /// <summary>Thrash, burst (weak points + Core), collapse forward and sink with a spore explosion.</summary>
        private void TickDeath(float dt, float time)
        {
            _deathTime += dt;
            float d = _deathTime;
            Transform coreT = _rig.Get("Core");
            Vector3 corePos = coreT != null ? coreT.position : transform.position + Vector3.up * 1.2f;
            if (d < 0.7f)
            {
                // Thrashing: violent yaw, flailing roots, snapping jaw.
                float k = d / 0.7f;
                float yaw = Mathf.Sin(time * 26f) * 16f * (1f - 0.4f * k);
                _rig.SetModelSquash(new Vector3(1f + Mathf.Sin(time * 40f) * 0.04f, 1f + Mathf.Sin(time * 33f) * 0.05f, 1f), yaw);
                _rig.Pose("Jaw", new Vector3(0f, 0f, -(20f + Mathf.Abs(Mathf.Sin(time * 18f)) * 40f)));
                _rig.Pose("RootL", new Vector3(Mathf.Sin(time * 21f) * 60f, 0f, Mathf.Cos(time * 17f) * 25f));
                _rig.Pose("RootR", new Vector3(-Mathf.Sin(time * 19f) * 60f, 0f, Mathf.Cos(time * 23f) * 25f));
                _rig.Pose("Core", Vector3.zero, Vector3.zero, Vector3.one * (1.4f + k * 0.8f));
                _rig.SetTint(new Color(1f, 0.55f, 0.9f), 0.3f + 0.3f * k);
                if (d > 0.3f && !_deathBurst)
                {
                    _deathBurst = true;
                    ImpactRequested?.Invoke(0.85f);
                    if (_pods != null) _pods.BurstAll();
                    if (_vfx != null)
                    {
                        _vfx.Pulse(VfxKind.Glow, corePos, new Color(1f, 0.6f, 0.95f, 1f), 9f, 0.7f);
                        _vfx.Burst(VfxKind.CollectSparkle, corePos, new Color(1f, 0.8f, 1f), 60, 5f, 0.24f, 1.1f);
                        _vfx.Burst(VfxKind.HitSpark, corePos, new Color(1f, 0.45f, 0.85f), 40, 6f, 0.2f, 0.7f);
                    }
                }
                return;
            }
            // Collapse forward and sink into the soil.
            float c = Mathf.Clamp01((d - 0.7f) / 1.6f);
            float sink = Mathf.Clamp01((d - 1.1f) / 2.0f);
            float ease = c * c * (3f - 2f * c);
            _rig.SetModelSquash(new Vector3(1f + ease * 0.2f, Mathf.Max(0.02f, 1f - ease * 0.45f - sink * 0.5f), 1f + ease * 0.1f),
                Mathf.Sin(time * 9f) * 5f * (1f - ease));
            _rig.Pose("Body", new Vector3(0f, 0f, -28f * ease));
            _rig.Pose("Jaw", new Vector3(0f, 0f, -(10f + 30f * (1f - ease))));
            _rig.Pose("RootL", new Vector3(-30f * ease, 0f, 20f * ease));
            _rig.Pose("RootR", new Vector3(30f * ease, 0f, 20f * ease));
            _rig.Pose("Core", Vector3.zero, Vector3.zero, Vector3.one * Mathf.Lerp(2.2f, 0.01f, c));
            _rig.SetTint(new Color(0.25f, 0.2f, 0.22f), 0.2f + 0.4f * ease);
            transform.position = _home + new Vector3(-0.35f * ease, -2.6f * sink * sink, 0f);
            if (d > 1.0f && !_deathSpores)
            {
                _deathSpores = true;
                ImpactRequested?.Invoke(0.6f);
                if (_vfx != null)
                {
                    _vfx.Burst(VfxKind.SporePuff, transform.position + Vector3.up * 1.2f, new Color(0.7f, 0.4f, 0.75f), 70, 4.5f, 0.42f, 1.8f);
                    _vfx.Burst(VfxKind.SporePuff, transform.position + Vector3.up * 0.5f, new Color(0.65f, 0.85f, 0.35f), 40, 3.2f, 0.36f, 1.6f);
                    _vfx.Pulse(VfxKind.Glow, transform.position + Vector3.up * 1f, new Color(0.9f, 0.5f, 1f, 0.7f), 8f, 0.8f);
                }
            }
            if (_vfx != null && sink > 0f && sink < 1f && Random.value < 0.5f)
                _vfx.Burst(VfxKind.PlantDust, _home + new Vector3(Random.Range(-1.4f, 0.6f), 0.1f, Random.Range(-2.3f, 2.3f)), new Color(0.5f, 0.38f, 0.28f), 4, 1.8f, 0.26f, 0.9f);
        }
    }
}
