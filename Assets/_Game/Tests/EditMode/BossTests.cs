using System.Collections.Generic;
using NUnit.Framework;
using VerdantSiege.Battle;
using VerdantSiege.Combat;
using VerdantSiege.Data;
using VerdantSiege.Grid;
using static VerdantSiege.Tests.M3Fixtures;

namespace VerdantSiege.Tests
{
    /// <summary>Boss framework: arrival, phase transitions, vulnerability windows, telegraph-then-strike, blocked cells, summons, victory.</summary>
    public sealed class BossTests
    {
        private TestContent _c;
        private BossDefinition _boss;
        private DefenderDefinition _wall;

        [SetUp]
        public void SetUp()
        {
            _c = new TestContent();
            _boss = _c.Make<BossDefinition>();
            _boss.id = "test_boss"; _boss.maxHealth = 1000f; _boss.guardedDamageMultiplier = 0.5f; _boss.exposedDamageMultiplier = 2f;
            _boss.entranceSeconds = 1f; _boss.phaseTransitionSeconds = 1f; _boss.uprootRefund = 0.5f;
            _boss.phases = new List<BossPhase>
            {
                new BossPhase { name = "One", startsAtHealthFraction = 1f, attackInterval = 2f, pattern = new List<BossAttack>
                {
                    new BossAttack { kind = BossAttackKind.RootEruption, cells = 2, telegraphSeconds = 1f, blockSeconds = 3f, exposeCoreSeconds = 2f },
                }},
                new BossPhase { name = "Two", startsAtHealthFraction = 0.5f, attackInterval = 2f, pattern = new List<BossAttack>
                {
                    new BossAttack { kind = BossAttackKind.Summon, summonEnemy = _c.walker, summonCount = 2, telegraphSeconds = 0.5f },
                }},
            };
            _wall = Defender(_c, "wall", DefenderBehaviourKind.Blocker, 1000f);
            _wall.cost = 100;
            _c.encounter.boss = _boss;
            _c.encounter.bossArrivesAtWave = 0;
            _c.encounter.prepTime = 1f;
            _c.encounter.startingVitality = 2000;
            _c.encounter.vitalityCap = 5000;
            _c.encounter.roster = new List<DefenderDefinition> { _wall };
            _c.encounter.waves = new List<WaveDefinition> { _c.Wave(_c.walker, 1, 0, 2f, 300f) };
        }

        [TearDown] public void TearDown() => _c.Destroy();

        private BattleSimulation ArrivedBoss()
        {
            BattleSimulation sim = _c.Sim();
            int arrived = 0;
            sim.BossArrived += b => arrived++;
            RunUntil(sim, () => sim.Boss.state == BossState.Idle, 5f);
            Assert.AreEqual(1, arrived);
            return sim;
        }

        [Test]
        public void Boss_ArrivesWithItsWave_UntargetableWhileRising()
        {
            BattleSimulation sim = _c.Sim();
            Assert.AreEqual(BossState.Dormant, sim.Boss.state);
            Run(sim, 1.1f);
            Assert.AreEqual(BossState.Rising, sim.Boss.state);
            Assert.IsFalse(sim.Boss.IsTargetable);
            sim.DamageBoss(500f, DamageType.Physical);
            Assert.AreEqual(1000f, sim.Boss.health, "no damage while rising");
            Run(sim, 1f);
            Assert.AreEqual(BossState.Idle, sim.Boss.state);
            Assert.IsTrue(sim.Boss.IsTargetable);
        }

        [Test]
        public void Boss_PhaseTransition_AtThreshold_NeverSkipped_InvulnerableDuringRoar()
        {
            BattleSimulation sim = ArrivedBoss();
            var phases = new List<int>();
            sim.BossPhaseChanged += (b, p) => phases.Add(p);
            sim.DamageBoss(5000f, DamageType.Piercing); // would kill outright
            Assert.AreEqual(500f, sim.Boss.health, 1e-3f, "stops at the next phase threshold");
            CollectionAssert.AreEqual(new[] { 1 }, phases);
            Assert.AreEqual(BossState.Transition, sim.Boss.state);
            sim.DamageBoss(100f, DamageType.Piercing);
            Assert.AreEqual(500f, sim.Boss.health, 1e-3f, "invulnerable while roaring");
            Run(sim, 1.1f);
            Assert.AreEqual(BossState.Idle, sim.Boss.state);
            sim.DamageBoss(2000f, DamageType.Piercing);
            Assert.AreEqual(BossState.Defeated, sim.Boss.state);
            sim.Step(BattleSimulation.FixedStep);
            Assert.AreEqual(BattleState.Won, sim.State, "defeating the boss wins the encounter");
        }

        [Test]
        public void Boss_VulnerabilityWindow_MultipliesDamage()
        {
            BattleSimulation sim = ArrivedBoss();
            sim.DamageBoss(100f, DamageType.Physical);
            Assert.AreEqual(950f, sim.Boss.health, 1e-3f, "guarded: x0.5");
            int opened = 0, closed = 0;
            sim.BossCoreExposedChanged += (b, e) => { if (e) opened++; else closed++; };
            RunUntil(sim, () => sim.Boss.CoreExposed, 5f);
            Assert.AreEqual(1, opened, "the eruption leaves the Core exposed");
            sim.DamageBoss(100f, DamageType.Physical);
            Assert.AreEqual(750f, sim.Boss.health, 1e-3f, "exposed: x2");
            Run(sim, 2.1f);
            Assert.AreEqual(1, closed);
            Assert.IsFalse(sim.Boss.CoreExposed);
            sim.DamageBoss(100f, DamageType.Physical);
            Assert.AreEqual(700f, sim.Boss.health, 1e-3f, "guarded again after the window");
        }

        [Test]
        public void Boss_RootEruption_TelegraphsThenStrikes_MostCrowdedColumn()
        {
            BattleSimulation sim = _c.Sim();
            DefenderRuntime a = Plant(sim, 0, 1, 4);
            DefenderRuntime b = Plant(sim, 0, 3, 4);
            DefenderRuntime safe = Plant(sim, 0, 2, 6);
            float telegraphAt = -1f, strikeAt = -1f;
            sim.BossTelegraphed += (boss, atk) => telegraphAt = sim.Stats.elapsed;
            sim.BossAttacked += (boss, atk) => strikeAt = sim.Stats.elapsed;
            RunUntil(sim, () => telegraphAt >= 0f, 10f);
            CollectionAssert.AreEquivalent(new[] { new GridCoord(1, 4), new GridCoord(3, 4) }, sim.Boss.targetCells, "warning shown on the crowded column");
            int vitalityBefore = sim.Vitality.Amount;
            RunUntil(sim, () => sim.Stats.elapsed >= telegraphAt + 0.95f, 2f);
            Assert.IsTrue(a.alive && b.alive, "nothing happens until the telegraph ends");
            RunUntil(sim, () => strikeAt >= 0f, 1f);
            Assert.AreEqual(1f, strikeAt - telegraphAt, 0.05f, "strike lands exactly after the telegraph");
            Assert.IsFalse(a.alive);
            Assert.IsFalse(b.alive);
            Assert.IsTrue(safe.alive);
            Assert.AreEqual(vitalityBefore + 100, sim.Vitality.Amount, "half of each cost refunded");
            Assert.AreEqual(CellTerrain.Blocked, sim.Grid.GetTerrain(new GridCoord(1, 4)));
            Assert.AreEqual(DeployResult.BlockedTerrain, sim.CanDeploy(0, new GridCoord(3, 4)));
        }

        [Test]
        public void Boss_BlockedCells_AreReleasedAfterTheirDuration()
        {
            _boss.phases[0].attackInterval = 6f; // no second eruption inside the measured window
            BattleSimulation sim = _c.Sim();
            Plant(sim, 0, 1, 4);
            Plant(sim, 0, 3, 4);
            var released = new List<GridCoord>();
            sim.CellBlockedChanged += (c, blocked) => { if (!blocked) released.Add(c); };
            float struckAt = -1f;
            sim.BossAttacked += (boss, atk) => struckAt = sim.Stats.elapsed;
            RunUntil(sim, () => struckAt >= 0f, 10f);
            Assert.AreEqual(2, sim.RootBlockedCount);
            RunUntil(sim, () => sim.Stats.elapsed >= struckAt + 2.9f, 4f);
            Assert.AreEqual(0, released.Count, "still blocked just before the duration ends");
            RunUntil(sim, () => sim.Stats.elapsed >= struckAt + 3.1f, 1f);
            CollectionAssert.AreEquivalent(new[] { new GridCoord(1, 4), new GridCoord(3, 4) }, released);
            Assert.AreEqual(CellTerrain.Grass, sim.Grid.GetTerrain(new GridCoord(1, 4)), "original terrain restored");
            sim.DebugResetCooldowns();
            Assert.AreEqual(DeployResult.Ok, sim.CanDeploy(0, new GridCoord(1, 4)));
        }

        [Test]
        public void Boss_Summon_SpawnsEnemies_AndDefeatWithersThem()
        {
            BattleSimulation sim = ArrivedBoss();
            sim.DamageBoss(5000f, DamageType.Piercing); // into phase two (summons)
            int before = sim.AliveEnemyCount;
            int summons = 0;
            sim.BossAttacked += (b, a) => { if (a.kind == BossAttackKind.Summon) summons++; };
            RunUntil(sim, () => summons > 0, 6f);
            Assert.AreEqual(before + 2, sim.AliveEnemyCount, "two walkers crawl out");
            sim.DamageBoss(5000f, DamageType.Piercing);
            Assert.AreEqual(0, sim.AliveEnemyCount, "the Blight withers with its boss");
            sim.Step(BattleSimulation.FixedStep);
            Assert.AreEqual(BattleState.Won, sim.State);
        }

        [Test]
        public void Boss_ProjectilesInAnyLane_HitItAtTheEdge()
        {
            _c.encounter.roster = new List<DefenderDefinition> { _c.shooter };
            BattleSimulation sim = ArrivedBoss();
            Plant(sim, 0, 4, 0);
            int hits = 0;
            sim.ProjectileHitBoss += p => hits++;
            Run(sim, 3f);
            Assert.Greater(hits, 0);
            Assert.Less(sim.Boss.health, 1000f);
        }
    }
}
