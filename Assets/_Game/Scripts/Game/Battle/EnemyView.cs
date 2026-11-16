using UnityEngine;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Presentation of one enemy: interpolated lane position, distance-driven walk cycle (legs/arms swing with
    /// actual movement, so slowed enemies visibly slow), bite lunges, hit flash, armour drop and death.
    /// </summary>
    [RequireComponent(typeof(UnitRig))]
    public sealed class EnemyView : MonoBehaviour, IPooledView
    {
        private UnitRig _rig;
        private EnemyRuntime _e;
        private VfxSystem _vfx;
        private BattleLayout _layout;
        private Spring _lunge, _hit, _spawn;
        private float _walkPhase;
        private float _lastX;
        private float _deathTimer = -1f;
        private bool _breached;
        private float _seed;
        private bool _armorVisible;
        private int _legCount;
        private static readonly string[] SkitterLegs = { "Leg0", "Leg1", "Leg2", "Leg3", "Leg4", "Leg5" };

        public EnemyRuntime Runtime => _e;
        public bool Dying => _deathTimer >= 0f;
        public bool Finished => _deathTimer > (_breached ? 0.25f : 0.6f);

        private void Awake() => _rig = GetComponent<UnitRig>();

        public void Bind(EnemyRuntime e, BattleLayout layout, VfxSystem vfx)
        {
            _e = e;
            _layout = layout;
            _vfx = vfx;
            _lastX = e.x;
            _seed = (e.Id * 0.618f) % 1f * 6.28f;
            _walkPhase = _seed;
            _armorVisible = e.armor > 0f;
            _rig.SetActive("Armor", _armorVisible);
            _legCount = 0;
            foreach (string l in SkitterLegs) if (_rig.Has(l)) _legCount++;
            _spawn.value = -0.5f;
            Place(1f);
        }

        public void OnRent()
        {
            _deathTimer = -1f;
            _breached = false;
            _lunge.Reset(); _hit.Reset(); _spawn.Reset();
            if (_rig != null) _rig.ResetPose();
        }

        public void OnReturn()
        {
            _e = null;
            if (_rig != null) _rig.ResetPose();
        }

        public void OnDamaged(bool armorHit)
        {
            _rig.Flash(armorHit ? new Color(1f, 0.95f, 0.8f) : Color.white);
            _hit.Kick(armorHit ? 3f : 6f);
        }

        public void OnBite() => _lunge.Kick(10f);

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
            transform.position = _layout.ToWorld(x + half, _e.lane);
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
            Place(interpolation);
            _lunge.Tick(dt, 240f, 15f);
            _hit.Tick(dt, 220f, 13f);
            _spawn.Tick(dt, 110f, 10f);

            float moved = Mathf.Abs(_lastX - _e.x);
            _lastX = _e.x;
            bool attacking = _e.activity == EnemyActivity.Attacking;
            float stride = _legCount > 0 ? 26f : 11f; // radians per column travelled
            _walkPhase += moved * stride;
            float t = time + _seed;

            float swing = Mathf.Sin(_walkPhase);
            float bob = Mathf.Abs(Mathf.Cos(_walkPhase)) * (attacking ? 0f : 1f);
            float s = 1f + _spawn.value;
            float hitSq = _hit.value * 0.05f;
            _rig.SetModelSquash(new Vector3(s * (1f + hitSq), s * (1f - hitSq + Mathf.Sin(t * 2f) * 0.015f), s * (1f + hitSq)));

            // Model faces -X (toward the Heartroot); a positive Z-rotation tilts the top toward -X.
            float lean = attacking ? 6f + _lunge.value * 2.2f : 4f;
            if (_rig.Has("Body"))
                _rig.Pose("Body", new Vector3(0f, 0f, lean - _hit.value * 2.5f), new Vector3(-_lunge.value * 0.02f, bob * 0.04f, 0f), Vector3.one);
            if (_rig.Has("Head"))
            {
                float chomp = attacking ? Mathf.Sin(t * 9f) * 5f + _lunge.value * 2f : Mathf.Sin(t * 1.7f) * 4f;
                _rig.Pose("Head", new Vector3(Mathf.Sin(t * 1.3f) * 5f, Mathf.Sin(t * 0.9f) * 6f, chomp));
            }
            float legAmp = attacking ? 4f : 28f;
            if (_rig.Has("LegL")) _rig.Pose("LegL", new Vector3(0f, 0f, swing * legAmp));
            if (_rig.Has("LegR")) _rig.Pose("LegR", new Vector3(0f, 0f, -swing * legAmp));
            float armAmp = attacking ? 0f : 22f;
            float armAttack = attacking ? Mathf.Clamp(_lunge.value * 6f, -40f, 40f) + 20f + Mathf.Sin(t * 9f) * 8f : 0f;
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
        }
    }
}
