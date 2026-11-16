using UnityEngine;
using VerdantSiege.Audio;
using VerdantSiege.Data;
using VerdantSiege.Grid;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Milestone 3 presentation wiring: defender abilities (snaps, traps, heals, chain lightning, reflections),
    /// enemy ability telegraphs, spore clouds, hazard decals and the boss. Same rule as the rest of BattleView:
    /// everything shown is derived from simulation events/state.
    /// </summary>
    public sealed partial class BattleView
    {
        private HazardOverlay _hazards;
        private LightningArcs _arcs;
        private BossView _bossView;

        public BossView BossView => _bossView;

        private void InitM3()
        {
            var hz = new GameObject("Hazards");
            hz.transform.SetParent(transform, false);
            _hazards = hz.AddComponent<HazardOverlay>();
            _hazards.Init(_sim, _layout, _lib, _vfx);
            var arcs = new GameObject("LightningArcs");
            arcs.transform.SetParent(transform, false);
            _arcs = arcs.AddComponent<LightningArcs>();
            _arcs.Init(_lib);

            foreach (WaveDefinition w in _sim.Encounter.waves)
                if (w != null) foreach (SpawnGroup g in w.groups)
                    if (g?.enemy?.abilityProjectile != null && g.enemy.abilityProjectile.prefab != null) _pool.Prewarm(g.enemy.abilityProjectile.prefab, 4);

            BossRuntime boss = _sim.Boss;
            if (boss != null && boss.Definition.prefab != null)
            {
                // Readability (boss polish): pushed right and scaled so no lane cell is covered; see BossStage.
                Vector3 home = _layout.ToWorld(_sim.Columns + BossStage.OffsetColumns, (_sim.Lanes - 1) * 0.5f) + Vector3.down * BossStage.Sink;
                GameObject go = Instantiate(boss.Definition.prefab, home, Quaternion.identity, _unitsRoot);
                _bossView = go.GetComponent<BossView>();
                if (_bossView == null) _bossView = go.AddComponent<BossView>();
                _bossView.Bind(boss, home, BossStage.Scale, _vfx, _layout, _lib, _sim.BossFrontX);
            }

            _sim.DefenderTelegraphed += OnDefenderTelegraphed;
            _sim.DefenderStateChanged += OnDefenderStateChanged;
            _sim.DefenderHealed += OnDefenderHealed;
            _sim.DefenderCleansed += OnDefenderCleansed;
            _sim.DefenderStatusApplied += OnDefenderStatus;
            _sim.ChainCast += OnChainCast;
            _sim.EnemyDevoured += OnDevoured;
            _sim.TrapTriggered += OnTrapTriggered;
            _sim.ProjectileLanded += OnProjectileLanded;
            _sim.ProjectileReflected += OnProjectileReflected;
            _sim.HostileProjectileHit += OnHostileHit;
            _sim.ProjectileHitBoss += OnProjectileHitBoss;
            _sim.EnemyAbilityTelegraphed += OnAbilityTelegraphed;
            _sim.EnemyAbilityUsed += OnAbilityUsed;
            _sim.EnemyLured += OnLured;
            _sim.CloudSpawned += OnCloudSpawned;
            _sim.BossArrived += OnBossArrived;
            _sim.BossTelegraphed += OnBossTelegraphed;
            _sim.BossAttacked += OnBossAttacked;
            _sim.BossDamaged += OnBossDamaged;
            _sim.BossPhaseChanged += OnBossPhase;
            _sim.BossDefeated += OnBossDefeated;
            _sim.DefenderUprooted += OnUprooted;
        }

        private void UnsubscribeM3()
        {
            _sim.DefenderTelegraphed -= OnDefenderTelegraphed;
            _sim.DefenderStateChanged -= OnDefenderStateChanged;
            _sim.DefenderHealed -= OnDefenderHealed;
            _sim.DefenderCleansed -= OnDefenderCleansed;
            _sim.DefenderStatusApplied -= OnDefenderStatus;
            _sim.ChainCast -= OnChainCast;
            _sim.EnemyDevoured -= OnDevoured;
            _sim.TrapTriggered -= OnTrapTriggered;
            _sim.ProjectileLanded -= OnProjectileLanded;
            _sim.ProjectileReflected -= OnProjectileReflected;
            _sim.HostileProjectileHit -= OnHostileHit;
            _sim.ProjectileHitBoss -= OnProjectileHitBoss;
            _sim.EnemyAbilityTelegraphed -= OnAbilityTelegraphed;
            _sim.EnemyAbilityUsed -= OnAbilityUsed;
            _sim.EnemyLured -= OnLured;
            _sim.CloudSpawned -= OnCloudSpawned;
            _sim.BossArrived -= OnBossArrived;
            _sim.BossTelegraphed -= OnBossTelegraphed;
            _sim.BossAttacked -= OnBossAttacked;
            _sim.BossDamaged -= OnBossDamaged;
            _sim.BossPhaseChanged -= OnBossPhase;
            _sim.BossDefeated -= OnBossDefeated;
            _sim.DefenderUprooted -= OnUprooted;
        }

        private void TickM3(float dt, float time)
        {
            if (_bossView != null) _bossView.Tick(dt, time);
        }

        private Vector3 EnemyPoint(EnemyRuntime e, float up = 0.6f) =>
            _layout.ToWorld(e.x + 0.1f, e.VisualLane) + Vector3.up * (e.IsFlying ? up + 0.5f : up);

        // ------------------------------------------------------------ defenders
        private void OnDefenderTelegraphed(DefenderRuntime d)
        {
            if (_defenders.TryGetValue(d.Id, out DefenderView v)) v.OnTelegraph();
        }

        private void OnDefenderStateChanged(DefenderRuntime d)
        {
            if (_defenders.TryGetValue(d.Id, out DefenderView v)) v.OnStateChanged();
        }

        private void OnDefenderHealed(DefenderRuntime d, float amount)
        {
            if (_defenders.TryGetValue(d.Id, out DefenderView v)) v.OnHealed();
            Play(Sfx.Heal, 0.5f);
        }

        private void OnDefenderCleansed(DefenderRuntime d)
        {
            _vfx.Burst(VfxKind.CollectSparkle, _layout.CellCenter(d.Coord) + Vector3.up * 0.6f, new Color(0.8f, 1f, 0.85f), 10, 1.4f, 0.12f, 0.5f);
        }

        private void OnDefenderStatus(DefenderRuntime d, StatusEffectDefinition s)
        {
            _vfx.Burst(VfxKind.SporePuff, _layout.CellCenter(d.Coord) + Vector3.up * 0.5f, s.tint, 5, 0.8f, 0.14f, 0.5f);
        }

        private void OnChainCast(DefenderRuntime d, int count)
        {
            ChainPoint[] pts = _sim.ChainPoints;
            for (int i = 1; i < count; i++)
            {
                Vector3 a = PointOf(pts[i - 1], i - 1 == 0);
                Vector3 b = PointOf(pts[i], false);
                _arcs.Arc(a, b);
                _vfx.Pulse(VfxKind.Glow, b, new Color(0.7f, 0.75f, 1f, 0.8f), 0.7f, 0.15f);
            }
            Play(Sfx.Zap, 0.8f);
        }

        private Vector3 PointOf(ChainPoint p, bool caster)
        {
            float y = caster ? 0.85f : p.air ? 1.15f : p.boss ? 1.1f : 0.65f;
            return _layout.ToWorld(p.x, p.lane) + Vector3.up * y;
        }

        private void OnDevoured(DefenderRuntime d, EnemyRuntime e)
        {
            _vfx.Burst(VfxKind.SporePuff, EnemyPoint(e), new Color(0.9f, 0.3f, 0.35f), 18, 1.8f, 0.18f, 0.6f);
            Play(Sfx.Snap);
        }

        private void OnTrapTriggered(DefenderRuntime d) => Play(Sfx.TrapErupt);

        // ------------------------------------------------------------ projectiles
        private void OnProjectileLanded(ProjectileRuntime p)
        {
            Vector3 pos = _layout.ToWorld(p.x, p.lane) + Vector3.up * 0.25f;
            float r = Mathf.Max(0.35f, p.Definition.splashRadius);
            _vfx.Burst(VfxKind.HitSpark, pos, p.Definition.trailColor, 22, 2.6f * r, 0.2f, 0.5f);
            _vfx.Pulse(VfxKind.Glow, pos, new Color(p.Definition.trailColor.r, p.Definition.trailColor.g, p.Definition.trailColor.b, 0.8f), 1.6f * r + 0.6f, 0.3f);
            Play(Sfx.EmberBurst, 0.8f);
        }

        private void OnProjectileReflected(ProjectileRuntime p)
        {
            Vector3 pos = _layout.ToWorld(p.x, p.lane) + Vector3.up * 0.7f;
            _vfx.Pulse(VfxKind.Glow, pos, new Color(0.85f, 1f, 1f, 0.9f), 1.1f, 0.25f);
            _vfx.Burst(VfxKind.CollectSparkle, pos, new Color(0.85f, 1f, 1f), 10, 1.8f, 0.1f, 0.4f);
            Play(Sfx.Reflect);
        }

        private void OnHostileHit(ProjectileRuntime p, DefenderRuntime d)
        {
            _vfx.Burst(VfxKind.SporePuff, _layout.CellCenter(d.Coord) + Vector3.up * 0.6f, p.Definition.trailColor, 12, 1.6f, 0.16f, 0.5f);
            Play(Sfx.Spit, 0.7f);
        }

        private void OnProjectileHitBoss(ProjectileRuntime p)
        {
            bool pod = _sim.Boss.WeakPointInLane(p.lane) != null;
            Vector3 pos = _layout.ToWorld(_sim.BossFrontX + 0.2f, p.lane) + Vector3.up * 0.8f;
            if (pod && _bossView != null && _bossView.WeakPoints != null && _bossView.WeakPoints.TryPodCentreForLane(p.lane, out Vector3 c)) pos = c;
            bool exposed = _sim.Boss.CoreExposed || pod;
            _vfx.Burst(VfxKind.HitSpark, pos, exposed ? new Color(1f, 0.5f, 0.9f) : _lib.armorDebris, exposed ? 10 : 6, 2.2f, 0.13f, 0.3f);
            Play(exposed ? Sfx.Hit : Sfx.ArmorHit, 0.7f);
        }

        // ------------------------------------------------------------ enemies
        private void OnAbilityTelegraphed(EnemyRuntime e)
        {
            switch (e.Definition.ability)
            {
                case EnemyAbilityKind.Vault:
                    _vfx.Pulse(VfxKind.Glow, EnemyPoint(e, 1.4f), new Color(1f, 0.75f, 0.3f, 0.8f), 0.6f, e.Definition.abilityWindup);
                    break;
                case EnemyAbilityKind.Burrow:
                    _vfx.Burst(VfxKind.PlantDust, EnemyPoint(e, 0.1f), new Color(0.55f, 0.42f, 0.28f), 18, 1.6f, 0.18f, 0.7f);
                    Play(Sfx.Burrow, 0.8f);
                    break;
                case EnemyAbilityKind.RangedSpit:
                    _vfx.Pulse(VfxKind.Glow, EnemyPoint(e, 0.9f), new Color(0.6f, 1f, 0.3f, 0.7f), 0.7f, e.Definition.abilityWindup);
                    break;
            }
        }

        private void OnAbilityUsed(EnemyRuntime e)
        {
            if (_enemies.TryGetValue(e.Id, out EnemyView v)) v.OnAbilityUsed();
            switch (e.Definition.ability)
            {
                case EnemyAbilityKind.Vault: Play(Sfx.Leap, 0.8f); break;
                case EnemyAbilityKind.Howl: Play(Sfx.Howl); break;
                case EnemyAbilityKind.RangedSpit: Play(Sfx.Spit, 0.6f); break;
                case EnemyAbilityKind.Burrow: Play(Sfx.Burrow); break;
                case EnemyAbilityKind.Mimic: Play(Sfx.Reflect, 0.5f); break;
            }
        }

        private void OnLured(EnemyRuntime e, DefenderRuntime d)
        {
            _vfx.Pulse(VfxKind.Glow, EnemyPoint(e, 1.0f), new Color(0.85f, 1f, 1f, 0.6f), 0.8f, 0.4f);
            if (_defenders.TryGetValue(d.Id, out DefenderView v)) v.OnActed();
        }

        private void OnCloudSpawned(CloudRuntime c)
        {
            _vfx.Burst(VfxKind.SporePuff, _layout.CellCenter(c.coord) + Vector3.up * 0.4f, new Color(0.65f, 0.85f, 0.3f), 24, 1.6f, 0.3f, 1f);
        }

        // ------------------------------------------------------------ boss
        private void OnBossArrived(BossRuntime b) => Play(Sfx.BossRumble); // the roar comes with the eruption (BossCinematics)

        private void OnBossTelegraphed(BossRuntime b, BossAttack a)
        {
            if (_bossView != null) _bossView.OnTelegraph(a);
            Play(a.kind == BossAttackKind.RootEruption ? Sfx.BossRumble : Sfx.EnemyGroan, 0.9f);
            if (a.kind != BossAttackKind.RootEruption) return;
            foreach (GridCoord c in b.targetCells)
                _vfx.Burst(VfxKind.PlantDust, _layout.CellCenter(c) + Vector3.up * 0.1f, new Color(0.5f, 0.3f, 0.3f), 6, 1f, 0.14f, 0.6f);
        }

        private void OnBossAttacked(BossRuntime b, BossAttack a)
        {
            if (_bossView != null) _bossView.OnAttack(a);
            if (a.kind == BossAttackKind.RootEruption) Play(Sfx.RootStrike);
        }

        private void OnBossDamaged(BossRuntime b, float amount)
        {
            if (_bossView != null) _bossView.OnDamaged(b.CoreExposed);
        }

        private void OnBossPhase(BossRuntime b, int phase)
        {
            if (_bossView != null) _bossView.OnPhaseChanged(); // roar sound via BossView.Roared -> BossCinematics
        }

        private void OnBossDefeated(BossRuntime b)
        {
            if (_bossView != null) _bossView.OnDefeated();
        }

        private void OnUprooted(DefenderRuntime d, int refund)
        {
            _vfx.Burst(VfxKind.ArmorDebris, _layout.CellCenter(d.Coord) + Vector3.up * 0.4f, new Color(0.4f, 0.55f, 0.25f), 14, 2.6f, 0.16f, 0.8f);
        }
    }
}
