using UnityEngine;
using VerdantSiege.Combat;
using VerdantSiege.Data;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Presentation of one enemy: interpolated lane position (including lure lane changes), distance-driven walk
    /// cycle, bite lunges, hit flash, armour drop and death. Milestone 3 adds ability telegraphs (Vaultling crouch
    /// and leap arc, Howler sac, Sapper canister, Mimic shimmer, Burrower dust trail and surfacing), flyer wing
    /// beats, status tints (chill blue, burn orange, frozen white, frenzy red, warded steel) and the elite health bar.
    /// Parts are found by name (Blender contract); missing parts are simply skipped.
    /// </summary>
    [RequireComponent(typeof(UnitRig))]
    public sealed class EnemyView : MonoBehaviour, IPooledView
    {
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private UnitRig _rig;
        private EnemyRuntime _e;
        private VfxSystem _vfx;
        private VisualLibrary _lib;
        private BattleLayout _layout;
        private Spring _lunge, _hit, _spawn, _ability;
        private float _walkPhase;
        private float _lastX;
        private float _deathTimer = -1f;
        private bool _breached;
        private float _seed;
        private bool _armorVisible;
        private int _legCount;
        private bool _holdsProp;
        private float _dustTimer;
        private float _windupTime;
        private EnemyActivity _lastActivity;
        private Transform _bar;
        private Transform _barFill;
        private MeshRenderer _barFillRenderer;
        private MaterialPropertyBlock _barMpb;
        private static readonly string[] SkitterLegs = { "Leg0", "Leg1", "Leg2", "Leg3", "Leg4", "Leg5" };

        public EnemyRuntime Runtime => _e;
        public bool Dying => _deathTimer >= 0f;
        public bool Finished => _deathTimer > (_breached ? 0.25f : 0.6f);

        private void Awake() => _rig = GetComponent<UnitRig>();

        public void Bind(EnemyRuntime e, BattleLayout layout, VfxSystem vfx, VisualLibrary lib = null)
        {
            _e = e;
            _layout = layout;
            _vfx = vfx;
            _lib = lib;
            _lastX = e.x;
            _seed = (e.Id * 0.618f) % 1f * 6.28f;
            _walkPhase = _seed;
            _armorVisible = e.armor > 0f;
            _rig.SetActive("Armor", _armorVisible);
            _legCount = 0;
            foreach (string l in SkitterLegs) if (_rig.Has(l)) _legCount++;
            // Warden Shield / Herald Banner are children of the root at the hand grip: keep arm motion small so the hand stays on them.
            _holdsProp = _rig.Has("Shield") || _rig.Has("Banner");
            _spawn.value = -0.5f;
            _lastActivity = e.activity;
            if (e.IsElite) EnsureBar();
            if (_bar != null) _bar.gameObject.SetActive(e.IsElite);
            Place(1f);
        }

        public void OnRent()
        {
            _deathTimer = -1f;
            _breached = false;
            _lunge.Reset(); _hit.Reset(); _spawn.Reset(); _ability.Reset();
            _windupTime = 0f;
            if (_rig != null) _rig.ResetPose();
        }

        public void OnReturn()
        {
            _e = null;
            if (_rig != null) _rig.ResetPose();
            if (_bar != null) _bar.gameObject.SetActive(false);
        }

        public void OnDamaged(bool armorHit)
        {
            _rig.Flash(armorHit ? new Color(1f, 0.95f, 0.8f) : Color.white);
            _hit.Kick(armorHit ? 3f : 6f);
        }

        public void OnBite() => _lunge.Kick(10f);

        /// <summary>Ability fired (leap take-off, howl, spit, copy, surfaced).</summary>
        public void OnAbilityUsed()
        {
            _ability.Kick(12f);
            if (_e == null || _vfx == null) return;
            EnemyDefinition d = _e.Definition;
            Vector3 p = transform.position;
            switch (d.ability)
            {
                case EnemyAbilityKind.Vault:
                    _vfx.Burst(VfxKind.PlantDust, p + Vector3.up * 0.1f, new Color(0.7f, 0.6f, 0.45f), 10, 1.4f, 0.14f, 0.5f);
                    break;
                case EnemyAbilityKind.Howl:
                    _vfx.Pulse(VfxKind.Glow, p + Vector3.up * 0.8f, new Color(1f, 0.35f, 0.3f, 0.7f), 3.2f, 0.45f);
                    _vfx.Burst(VfxKind.SporePuff, p + Vector3.up * 0.9f, new Color(1f, 0.45f, 0.35f), 16, 2.4f, 0.16f, 0.6f);
                    break;
                case EnemyAbilityKind.Mimic:
                    _vfx.Burst(VfxKind.CollectSparkle, p + Vector3.up * 0.6f, new Color(0.85f, 0.6f, 1f), 16, 1.6f, 0.13f, 0.6f);
                    break;
                case EnemyAbilityKind.Burrow:
                    _vfx.Burst(VfxKind.PlantDust, p + Vector3.up * 0.1f, new Color(0.55f, 0.42f, 0.3f), 22, 1.8f, 0.2f, 0.8f);
                    break;
            }
        }

        public void OnArmorBroken()
        {
            if (!_armorVisible) return;
            _armorVisible = false;
            _rig.SetActive("Armor", false);
            if (_vfx != null)
                _vfx.Burst(VfxKind.ArmorDebris, transform.position + Vector3.up * 0.9f, new Color(0.78f, 0.7f, 0.56f), 18, 2.4f, 0.16f, 0.9f);
            _hit.Kick(10f);
        }

        public void PlayDeath(bool breached)
        {
            if (_deathTimer >= 0f) return;
            _deathTimer = 0f;
            _breached = breached;
            if (_bar != null) _bar.gameObject.SetActive(false);
            if (_vfx == null) return;
            if (breached)
                _vfx.Burst(VfxKind.Breach, transform.position + Vector3.up * 0.5f, new Color(1f, 0.35f, 0.45f), 16, 2f, 0.25f, 0.6f);
            else
                _vfx.Burst(VfxKind.SporePuff, transform.position + Vector3.up * 0.55f, new Color(0.72f, 0.38f, 0.9f), 20, 1.6f, 0.22f, 0.8f);
        }

        private void Place(float interpolation)
        {
            if (_e == null) return;
            float x = Mathf.LerpUnclamped(_e.prevX, _e.x, interpolation);
            float half = _e.Definition != null ? _e.Definition.bodyLength * 0.5f : 0.25f;
            Vector3 pos = _layout.ToWorld(x + half, _e.VisualLane);
            if (_e.activity == EnemyActivity.Leaping)
            {
                float span = _e.leapToX - _e.leapFromX;
                float k = Mathf.Abs(span) > 0.01f ? Mathf.Clamp01((x - _e.leapFromX) / span) : 0f;
                pos.y += Mathf.Sin(k * Mathf.PI) * 0.95f;
            }
            transform.position = pos;
        }

        public void Tick(float dt, float time, float interpolation)
        {
            if (_rig == null) return;
            _rig.TickFlash(dt);
            if (_deathTimer >= 0f)
            {
                _deathTimer += dt;
                float k = Mathf.Clamp01(_deathTimer / (_breached ? 0.25f : 0.55f));
                float sink = k * k;
                _rig.SetModelSquash(new Vector3(1f + k * 0.25f, Mathf.Max(0.02f, 1f - sink), 1f + k * 0.25f), k * 25f);
                return;
            }
            if (_e == null) return;
            Place(interpolation);
            _lunge.Tick(dt, 240f, 15f);
            _hit.Tick(dt, 220f, 13f);
            _spawn.Tick(dt, 110f, 10f);
            _ability.Tick(dt, 160f, 12f);

            EnemyDefinition def = _e.Definition;
            EnemyActivity act = _e.activity;
            if (act != _lastActivity) { _windupTime = 0f; _lastActivity = act; }
            _windupTime += dt;

            float moved = Mathf.Abs(_lastX - _e.x);
            _lastX = _e.x;
            bool attacking = act == EnemyActivity.Attacking || act == EnemyActivity.Ranged;
            float stride = _legCount > 0 ? 26f : 11f; // radians per column travelled
            _walkPhase += moved * stride;
            float t = time + _seed;

            // ---- burrowed: the body is hidden underground, a dust trail shows where it travels.
            if (_e.underground)
            {
                float rise = act == EnemyActivity.Surfacing ? Mathf.Clamp01(_windupTime / Mathf.Max(0.1f, def.abilityWindup)) : 0f;
                float h = Mathf.Max(0.03f, rise * rise);
                _rig.SetModelSquash(new Vector3(1f + (1f - h) * 0.4f, h, 1f + (1f - h) * 0.4f), Mathf.Sin(t * 20f) * 6f * (act == EnemyActivity.Surfacing ? 1f : 0f));
                _dustTimer -= dt;
                if (_dustTimer <= 0f && _vfx != null)
                {
                    _dustTimer = act == EnemyActivity.Surfacing ? 0.07f : 0.14f;
                    _vfx.Burst(VfxKind.PlantDust, transform.position + Vector3.up * 0.08f, new Color(0.55f, 0.42f, 0.28f),
                        act == EnemyActivity.Surfacing ? 6 : 3, 0.9f, 0.16f, 0.6f);
                }
                UpdateTint(time);
                UpdateBar();
                return;
            }

            float swing = Mathf.Sin(_walkPhase);
            float bob = Mathf.Abs(Mathf.Cos(_walkPhase)) * (attacking ? 0f : 1f);
            float s = 1f + _spawn.value;
            float hitSq = _hit.value * 0.05f;
            Vector3 squash = new Vector3(s * (1f + hitSq), s * (1f - hitSq + Mathf.Sin(t * 2f) * 0.015f), s * (1f + hitSq));
            bool windup = act == EnemyActivity.Windup;
            if (windup && def.ability == EnemyAbilityKind.Vault)
            {
                // Clear crouch telegraph: sinks and widens, trembling, before the leap.
                float k = Mathf.Clamp01(_windupTime / Mathf.Max(0.1f, def.abilityWindup));
                squash = new Vector3(s * (1f + 0.22f * k), s * (1f - 0.3f * k + Mathf.Sin(t * 40f) * 0.02f * k), s * (1f + 0.22f * k));
            }
            if (act == EnemyActivity.Leaping) squash = new Vector3(s * 0.88f, s * 1.18f, s * 0.88f);
            _rig.SetModelSquash(squash);

            // Model faces -X (toward the Heartroot); a positive Z-rotation tilts the top toward -X.
            float lean = attacking ? 6f + _lunge.value * 2.2f : 4f;
            if (act == EnemyActivity.Leaping) lean = -18f;
            float hover = def.IsFlying ? Mathf.Sin(t * 2.4f) * 0.06f : 0f;
            if (_rig.Has("Body"))
                _rig.Pose("Body", new Vector3(0f, 0f, lean - _hit.value * 2.5f), new Vector3(-_lunge.value * 0.02f, bob * 0.04f + hover, 0f), Vector3.one);
            if (_rig.Has("Head"))
            {
                float chomp = attacking ? Mathf.Sin(t * 9f) * 5f + _lunge.value * 2f : Mathf.Sin(t * 1.7f) * 4f;
                if (windup && def.ability == EnemyAbilityKind.Howl) chomp = -25f; // head thrown back to howl
                _rig.Pose("Head", new Vector3(Mathf.Sin(t * 1.3f) * 5f, Mathf.Sin(t * 0.9f) * 6f, chomp), new Vector3(0f, hover, 0f), Vector3.one);
            }
            float legAmp = attacking || windup ? 4f : 28f;
            if (_rig.Has("LegL")) _rig.Pose("LegL", new Vector3(0f, 0f, swing * legAmp));
            if (_rig.Has("LegR")) _rig.Pose("LegR", new Vector3(0f, 0f, -swing * legAmp));
            float armAmp = attacking ? 0f : 22f;
            float armAttack = attacking ? Mathf.Clamp(_lunge.value * 6f, -40f, 40f) + 20f + Mathf.Sin(t * 9f) * 8f : 0f;
            if (act == EnemyActivity.Ranged) armAttack = 35f + _ability.value * 6f; // throwing arm
            if (_holdsProp) { armAmp *= 0.2f; armAttack *= 0.25f; }
            if (_rig.Has("ArmL")) _rig.Pose("ArmL", new Vector3(0f, 0f, -swing * armAmp + armAttack));
            if (_rig.Has("ArmR")) _rig.Pose("ArmR", new Vector3(0f, 0f, swing * armAmp + armAttack * 0.85f));
            if (_legCount > 0)
            {
                for (int i = 0; i < SkitterLegs.Length; i++)
                {
                    // Alternating tripod gait: legs 0,3,4 vs 1,2,5.
                    float ph = _walkPhase + ((i == 0 || i == 3 || i == 4) ? 0f : Mathf.PI);
                    float lift = Mathf.Max(0f, Mathf.Sin(ph)) * 18f;
                    _rig.Pose(SkitterLegs[i], new Vector3(lift * (i % 2 == 0 ? 1f : -1f), Mathf.Cos(ph) * 16f, 0f));
                }
            }
            if (_rig.Has("Armor")) _rig.Pose("Armor", new Vector3(0f, 0f, lean * 0.5f), new Vector3(-_lunge.value * 0.02f, bob * 0.04f, 0f), Vector3.one);

            PoseAbilityParts(def, act, t);
            UpdateTint(time);
            UpdateBar();
        }

        private void PoseAbilityParts(EnemyDefinition def, EnemyActivity act, float t)
        {
            bool windup = act == EnemyActivity.Windup;
            float k = Mathf.Clamp01(_windupTime / Mathf.Max(0.1f, def.abilityWindup));
            if (_rig.Has("Sac"))
            {
                // Howler: inflates before the howl. Spore Carrier: slow breathing.
                float inflate = windup && def.ability == EnemyAbilityKind.Howl ? 1f + 0.9f * k + Mathf.Sin(t * 30f) * 0.05f * k
                    : 1f + Mathf.Sin(t * 2.2f) * 0.06f + Mathf.Max(0f, _ability.value) * 0.05f;
                _rig.Pose("Sac", Vector3.zero, Vector3.zero, Vector3.one * inflate);
            }
            if (_rig.Has("Canister"))
            {
                // Sapper: canister swells and shakes in the last moments before a throw.
                bool charging = act == EnemyActivity.Ranged && _e.abilityTimer <= def.abilityWindup;
                float c = charging ? 1f + 0.35f * (1f - _e.abilityTimer / Mathf.Max(0.1f, def.abilityWindup)) : 1f;
                _rig.Pose("Canister", new Vector3(charging ? Mathf.Sin(t * 35f) * 6f : 0f, 0f, 0f), Vector3.zero, Vector3.one * c);
            }
            if (_rig.Has("WingL") || _rig.Has("WingR"))
            {
                float flap = Mathf.Sin(t * 9f) * 32f;
                _rig.Pose("WingL", new Vector3(flap, 0f, 0f));
                _rig.Pose("WingR", new Vector3(-flap, 0f, 0f));
            }
            if (_rig.Has("Tendrils")) _rig.Pose("Tendrils", new Vector3(Mathf.Sin(t * 3.1f) * 12f, Mathf.Sin(t * 2.3f) * 20f, 0f));
            if (_rig.Has("Eyes"))
            {
                float blink = Mathf.Repeat(t, 3.2f) < 0.12f ? 0.15f : 1f;
                _rig.Pose("Eyes", Vector3.zero, Vector3.zero, new Vector3(1f, blink, 1f));
            }
            if (_rig.Has("Banner")) _rig.Pose("Banner", new Vector3(Mathf.Sin(t * 2.6f) * 8f, 0f, Mathf.Sin(t * 1.9f) * 5f));
            if (_rig.Has("Aura"))
            {
                float a = 1f + Mathf.Sin(t * 3f) * 0.08f;
                _rig.Pose("Aura", new Vector3(0f, t * 40f % 360f, 0f), Vector3.zero, new Vector3(a, 1f, a));
            }
            if (_rig.Has("Shield")) _rig.Pose("Shield", new Vector3(0f, Mathf.Sin(t * 1.4f) * 4f, 0f));
        }

        private void UpdateTint(float time)
        {
            Color c = default;
            float strength = 0f;
            int best = -1;
            var list = _e.statuses.Active;
            for (int i = 0; i < list.Count; i++)
            {
                StatusEffectDefinition sd = list[i].definition;
                int pri = sd.kind == StatusKind.Stun ? 4 : sd.kind == StatusKind.DamageOverTime ? 3 : sd.kind == StatusKind.Slow ? 2 : 1;
                if (pri <= best) continue;
                best = pri;
                c = sd.tint;
                strength = sd.kind == StatusKind.Stun ? 0.95f : sd.kind == StatusKind.Haste ? 0.45f + Mathf.Sin(time * 14f) * 0.2f : 0.7f;
            }
            if (best < 0 && _e.wardReduction > 0f) { c = new Color(0.7f, 0.82f, 1f); strength = 0.35f + Mathf.Sin(time * 4f) * 0.1f; }
            if (_e.activity == EnemyActivity.Windup && _e.Definition.ability == EnemyAbilityKind.Mimic)
            {
                c = new Color(0.9f, 0.7f, 1f);
                strength = 0.4f + 0.4f * Mathf.Abs(Mathf.Sin(time * 18f)); // shimmer telegraph
            }
            _rig.SetTint(c, strength);
        }

        // ------------------------------------------------------------------ elite health bar

        private void EnsureBar()
        {
            if (_bar != null || _lib == null || _lib.quadMesh == null || _lib.cellHighlight == null) return;
            _barMpb = new MaterialPropertyBlock();
            _bar = new GameObject("EliteBar").transform;
            _bar.SetParent(transform, false);
            _bar.localPosition = new Vector3(0f, 2.05f, 0f);
            MeshRenderer bg = Quad("Bg", _bar, new Vector3(1.1f, 0.16f, 1f), Vector3.zero);
            _barMpb.SetColor(ColorId, new Color(0.05f, 0.03f, 0.06f, 0.85f));
            bg.SetPropertyBlock(_barMpb);
            var pivot = new GameObject("FillPivot").transform;
            pivot.SetParent(_bar, false);
            pivot.localPosition = new Vector3(-0.52f, 0f, -0.01f);
            _barFill = pivot;
            _barFillRenderer = Quad("Fill", pivot, new Vector3(1.04f, 0.11f, 1f), new Vector3(0.52f, 0f, 0f));
        }

        private MeshRenderer Quad(string name, Transform parent, Vector3 scale, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = _lib.quadMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _lib.cellHighlight;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mr;
        }

        private void UpdateBar()
        {
            if (_bar == null || !_bar.gameObject.activeSelf || _e == null) return;
            Camera cam = Camera.main;
            if (cam != null) _bar.rotation = cam.transform.rotation;
            float f = Mathf.Clamp01(_e.health / Mathf.Max(1f, _e.MaxHealth));
            _barFill.localScale = new Vector3(Mathf.Max(0.001f, f), 1f, 1f);
            Color c = _e.wardReduction > 0f ? new Color(0.7f, 0.82f, 1f, 0.95f) : new Color(1f, 0.3f + 0.5f * f, 0.35f, 0.95f);
            _barMpb.SetColor(ColorId, c);
            _barFillRenderer.SetPropertyBlock(_barMpb);
        }
    }
}
