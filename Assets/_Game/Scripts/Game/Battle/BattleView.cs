using System.Collections.Generic;
using UnityEngine;
using VerdantSiege.Audio;
using VerdantSiege.Combat;
using VerdantSiege.Data;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Binds the simulation's events to pooled views, VFX and audio. Owns no gameplay state: everything shown
    /// is derived from the simulation, and views linger only for exit animations.
    /// </summary>
    public sealed partial class BattleView : MonoBehaviour
    {
        private BattleSimulation _sim;
        private BattleLayout _layout;
        private VisualLibrary _lib;
        private VfxSystem _vfx;
        private PrefabPool _pool;
        private Transform _unitsRoot;
        private HeartrootView _heartroot;
        private AudioService _audio;

        private readonly Dictionary<int, DefenderView> _defenders = new Dictionary<int, DefenderView>();
        private readonly Dictionary<int, EnemyView> _enemies = new Dictionary<int, EnemyView>();
        private readonly Dictionary<int, ProjectileView> _projectiles = new Dictionary<int, ProjectileView>();
        private readonly Dictionary<int, PickupView> _pickups = new Dictionary<int, PickupView>();
        private readonly List<DefenderView> _dyingDefenders = new List<DefenderView>();
        private readonly List<EnemyView> _dyingEnemies = new List<EnemyView>();
        private readonly List<PickupView> _leavingPickups = new List<PickupView>();
        private readonly List<int> _scratchIds = new List<int>();

        public VfxSystem Vfx => _vfx;
        public IReadOnlyDictionary<int, PickupView> PickupViews => _pickups;
        public IReadOnlyDictionary<int, DefenderView> DefenderViews => _defenders;
        public event System.Action<Vector3, int> PickupCollectedAt;

        public void Init(BattleSimulation sim, BattleLayout layout, VisualLibrary lib, VfxSystem vfx, HeartrootView heartroot, AudioService audio)
        {
            _sim = sim;
            _layout = layout;
            _lib = lib;
            _vfx = vfx;
            _heartroot = heartroot;
            _audio = audio;
            _unitsRoot = new GameObject("Units").transform;
            _unitsRoot.SetParent(transform, false);
            var poolRoot = new GameObject("Pool").transform;
            poolRoot.SetParent(transform, false);
            _pool = new PrefabPool(poolRoot);

            foreach (DeckSlot s in sim.Deck) if (s.Definition.prefab != null) _pool.Prewarm(s.Definition.prefab, 4);
            foreach (WaveDefinition w in sim.Encounter.waves)
                if (w != null) foreach (SpawnGroup g in w.groups) if (g?.enemy?.prefab != null) _pool.Prewarm(g.enemy.prefab, 3);
            foreach (DeckSlot s in sim.Deck)
                if (s.Definition.projectile != null && s.Definition.projectile.prefab != null) _pool.Prewarm(s.Definition.projectile.prefab, 12);
            if (lib.sunmote != null) _pool.Prewarm(lib.sunmote, 6);

            sim.DefenderPlaced += OnDefenderPlaced;
            sim.DefenderRemoved += OnDefenderRemoved;
            sim.DefenderDamaged += OnDefenderDamaged;
            sim.DefenderActed += OnDefenderActed;
            sim.EnemySpawned += OnEnemySpawned;
            sim.EnemyDamaged += OnEnemyDamaged;
            sim.EnemyArmorBroken += OnArmorBroken;
            sim.EnemyKilled += OnEnemyKilled;
            sim.EnemyBreached += OnEnemyBreached;
            sim.EnemyBit += OnEnemyBit;
            sim.ProjectileFired += OnProjectileFired;
            sim.ProjectileHit += OnProjectileHit;
            sim.ProjectileDespawned += OnProjectileDespawned;
            sim.PickupSpawned += OnPickupSpawned;
            sim.PickupRemoved += OnPickupRemoved;
            sim.HeartrootDamaged += OnHeartrootDamaged;
            InitM3(); // Milestone 3 wiring (BattleView.M3.cs)
            InitEvolution(); // Living Evolution wiring (BattleView.Evolution.cs)
        }

        private void OnDestroy()
        {
            if (_sim == null) return;
            sim_Unsubscribe();
        }

        private void sim_Unsubscribe()
        {
            _sim.DefenderPlaced -= OnDefenderPlaced;
            _sim.DefenderRemoved -= OnDefenderRemoved;
            _sim.DefenderDamaged -= OnDefenderDamaged;
            _sim.DefenderActed -= OnDefenderActed;
            _sim.EnemySpawned -= OnEnemySpawned;
            _sim.EnemyDamaged -= OnEnemyDamaged;
            _sim.EnemyArmorBroken -= OnArmorBroken;
            _sim.EnemyKilled -= OnEnemyKilled;
            _sim.EnemyBreached -= OnEnemyBreached;
            _sim.EnemyBit -= OnEnemyBit;
            _sim.ProjectileFired -= OnProjectileFired;
            _sim.ProjectileHit -= OnProjectileHit;
            _sim.ProjectileDespawned -= OnProjectileDespawned;
            _sim.PickupSpawned -= OnPickupSpawned;
            _sim.PickupRemoved -= OnPickupRemoved;
            _sim.HeartrootDamaged -= OnHeartrootDamaged;
            UnsubscribeM3();
            UnsubscribeEvolution();
        }

        private void Play(Sfx s, float vol = 1f) { if (_audio != null) _audio.Play(s, vol); }

        // ------------------------------------------------------------ defenders
        private void OnDefenderPlaced(DefenderRuntime d)
        {
            if (d.Definition.prefab == null) return;
            Vector3 pos = _layout.CellCenter(d.Coord);
            var v = _pool.Rent<DefenderView>(d.Definition.prefab, pos, Quaternion.identity, _unitsRoot);
            v.Bind(d, _vfx, _lib);
            _defenders[d.Id] = v;
            _vfx.Burst(VfxKind.PlantDust, pos + Vector3.up * 0.1f, _lib.plantDust, 16, 1.4f, 0.16f, 0.6f);
            Play(Sfx.Plant);
        }

        private void OnDefenderRemoved(DefenderRuntime d, bool killed)
        {
            if (!_defenders.TryGetValue(d.Id, out DefenderView v)) return;
            _defenders.Remove(d.Id);
            if (killed)
            {
                v.PlayDeath();
                _dyingDefenders.Add(v);
                Play(Sfx.DefenderDie);
            }
            else
            {
                _vfx.Burst(VfxKind.PlantDust, v.transform.position + Vector3.up * 0.2f, _lib.plantDust, 10, 1.2f, 0.14f, 0.5f);
                _pool.Return(v.gameObject);
                Play(Sfx.Undo);
            }
        }

        private void OnDefenderDamaged(DefenderRuntime d, float amount)
        {
            if (_defenders.TryGetValue(d.Id, out DefenderView v)) v.OnDamaged();
            Play(Sfx.DefenderHurt, 0.6f);
        }

        private void OnDefenderActed(DefenderRuntime d)
        {
            if (_defenders.TryGetValue(d.Id, out DefenderView v)) v.OnActed();
            if (d.Definition.behaviour == DefenderBehaviourKind.Generator) Play(Sfx.Produce);
            else if (d.Definition.behaviour == DefenderBehaviourKind.LaneShooter) Play(Sfx.Shoot, 0.8f); // M3 kinds: BattleView.M3.cs
        }

        // ------------------------------------------------------------ enemies
        private void OnEnemySpawned(EnemyRuntime e)
        {
            if (e.Definition.prefab == null) return;
            var v = _pool.Rent<EnemyView>(e.Definition.prefab, _layout.ToWorld(e.x, e.lane), Quaternion.identity, _unitsRoot);
            v.Bind(e, _layout, _vfx, _lib);
            _enemies[e.Id] = v;
            if (e.Definition.elite) Play(Sfx.EliteArrive);
            else if (e.Definition.armorHealth > 0f) Play(Sfx.BruteStomp);
            else if (Random.value < 0.35f) Play(Sfx.EnemyGroan, 0.6f);
        }

        private void OnEnemyDamaged(EnemyRuntime e, DamageResult r, DamageType type)
        {
            if (_enemies.TryGetValue(e.Id, out EnemyView v)) v.OnDamaged(r.toArmor > 0f && r.toHealth <= 0f);
        }

        private void OnArmorBroken(EnemyRuntime e)
        {
            if (_enemies.TryGetValue(e.Id, out EnemyView v)) v.OnArmorBroken();
            Play(Sfx.ArmorBreak);
        }

        private void OnEnemyKilled(EnemyRuntime e) => RetireEnemy(e, false);
        private void OnEnemyBreached(EnemyRuntime e) => RetireEnemy(e, true);

        private void RetireEnemy(EnemyRuntime e, bool breached)
        {
            if (!_enemies.TryGetValue(e.Id, out EnemyView v)) return;
            _enemies.Remove(e.Id);
            v.PlayDeath(breached);
            _dyingEnemies.Add(v);
            Play(breached ? Sfx.Breach : Sfx.EnemyDie);
        }

        private void OnEnemyBit(EnemyRuntime e, DefenderRuntime d)
        {
            if (_enemies.TryGetValue(e.Id, out EnemyView v)) v.OnBite();
            Play(Sfx.Bite, 0.7f);
        }

        // ------------------------------------------------------------ projectiles
        private void OnProjectileFired(ProjectileRuntime p)
        {
            if (p.Definition.prefab == null) return;
            var v = _pool.Rent<ProjectileView>(p.Definition.prefab, _layout.ToWorld(p.x, p.lane), Quaternion.identity, _unitsRoot);
            v.Bind(p, _layout);
            _projectiles[p.Id] = v;
        }

        private void OnProjectileHit(ProjectileRuntime p, EnemyRuntime e)
        {
            Vector3 pos = _layout.ToWorld(e.x + 0.05f, e.VisualLane) + Vector3.up * (e.IsFlying ? 1.1f : 0.62f);
            bool armor = e.armor > 0f && p.Definition.damageType != DamageType.Piercing;
            Color spark = p.Definition.damageType == DamageType.Physical ? _lib.hitSpark : p.Definition.trailColor;
            _vfx.Burst(VfxKind.HitSpark, pos, armor ? _lib.armorDebris : spark, armor ? 6 : 8, 2.2f, 0.12f, 0.3f);
            // Impact flash scaled by hit weight, tinted by element (combat feel; budgeted by VfxSystem quality).
            float weight = Mathf.Clamp(p.Definition.damage / 25f, 0.6f, 2.2f);
            Color flash = armor ? new Color(1f, 0.95f, 0.8f, 0.8f) : new Color(spark.r, spark.g, spark.b, 0.85f);
            _vfx.Pulse(VfxKind.MuzzleFlash, pos + Vector3.left * 0.08f, flash, 0.32f + 0.18f * weight, 0.09f);
            if (p.Definition.splashRadius > 0f)
                _vfx.Burst(VfxKind.HitSpark, pos, spark, 14, 3.2f, 0.16f, 0.45f);
            Play(armor ? Sfx.ArmorHit : p.Definition.damageType == DamageType.Frost ? Sfx.FrostHit : Sfx.Hit, 0.8f);
        }

        private void OnProjectileDespawned(ProjectileRuntime p)
        {
            if (!_projectiles.TryGetValue(p.Id, out ProjectileView v)) return;
            _projectiles.Remove(p.Id);
            _pool.Return(v.gameObject);
        }

        // ------------------------------------------------------------ pickups
        private void OnPickupSpawned(PickupRuntime p)
        {
            if (_lib.sunmote == null) return;
            var v = _pool.Rent<PickupView>(_lib.sunmote, _layout.ToWorld(p.x, p.laneFloat), Quaternion.identity, _unitsRoot);
            v.Bind(p, _layout);
            _pickups[p.Id] = v;
            if (p.source == PickupSource.Sky) Play(Sfx.SkyDrop, 0.5f);
        }

        private void OnPickupRemoved(PickupRuntime p, bool collected)
        {
            if (!_pickups.TryGetValue(p.Id, out PickupView v)) return;
            _pickups.Remove(p.Id);
            if (collected)
            {
                v.PlayCollect();
                _vfx.Burst(VfxKind.CollectSparkle, v.transform.position, _lib.collectSparkle, 14, 2f, 0.14f, 0.5f);
                Play(Sfx.Collect);
                PickupCollectedAt?.Invoke(v.transform.position, p.amount);
            }
            else v.PlayExpire();
            _leavingPickups.Add(v);
        }

        private void OnHeartrootDamaged(int remaining, int damage)
        {
            if (_heartroot != null) _heartroot.OnDamaged(remaining, _sim.HeartrootMaxHealth);
        }

        // ------------------------------------------------------------ frame
        private void LateUpdate()
        {
            if (_sim == null) return;
            float dt = Time.deltaTime;
            float time = Time.time;
            float interp = Mathf.Clamp01(_sim.Interpolation);
            foreach (KeyValuePair<int, DefenderView> kv in _defenders) kv.Value.Tick(dt, time);
            foreach (KeyValuePair<int, EnemyView> kv in _enemies) kv.Value.Tick(dt, time, interp);
            foreach (KeyValuePair<int, ProjectileView> kv in _projectiles) kv.Value.Tick(dt, interp);
            foreach (KeyValuePair<int, PickupView> kv in _pickups) kv.Value.Tick(dt, time);
            TickM3(dt, time);

            for (int i = _dyingDefenders.Count - 1; i >= 0; i--)
            {
                DefenderView v = _dyingDefenders[i];
                v.Tick(dt, time);
                if (v.Finished) { _dyingDefenders.RemoveAt(i); _pool.Return(v.gameObject); }
            }
            for (int i = _dyingEnemies.Count - 1; i >= 0; i--)
            {
                EnemyView v = _dyingEnemies[i];
                v.Tick(dt, time, interp);
                if (v.Finished) { _dyingEnemies.RemoveAt(i); _pool.Return(v.gameObject); }
            }
            for (int i = _leavingPickups.Count - 1; i >= 0; i--)
            {
                PickupView v = _leavingPickups[i];
                v.Tick(dt, time);
                if (v.Finished) { _leavingPickups.RemoveAt(i); _pool.Return(v.gameObject); }
            }
        }

        /// <summary>Screen-space pickup hit test (generous radius in pixels). Returns pickup id or -1.</summary>
        public int PickupAtScreen(Camera cam, Vector2 screen, float radiusPx)
        {
            int best = -1;
            float bestD = radiusPx * radiusPx;
            foreach (KeyValuePair<int, PickupView> kv in _pickups)
            {
                if (kv.Value.Leaving) continue;
                Vector3 sp = cam.WorldToScreenPoint(kv.Value.transform.position);
                if (sp.z <= 0f) continue;
                float d = ((Vector2)sp - screen).sqrMagnitude;
                if (d < bestD) { bestD = d; best = kv.Key; }
            }
            return best;
        }

        /// <summary>Collect every pickup whose screen position is within the swipe path (swipe-to-collect).</summary>
        public void CollectAlong(Camera cam, Vector2 screen, float radiusPx)
        {
            _scratchIds.Clear();
            float r2 = radiusPx * radiusPx;
            foreach (KeyValuePair<int, PickupView> kv in _pickups)
            {
                if (kv.Value.Leaving) continue;
                Vector3 sp = cam.WorldToScreenPoint(kv.Value.transform.position);
                if (sp.z > 0f && ((Vector2)sp - screen).sqrMagnitude < r2) _scratchIds.Add(kv.Key);
            }
            foreach (int id in _scratchIds) _sim.CollectPickup(id);
        }
    }
}
