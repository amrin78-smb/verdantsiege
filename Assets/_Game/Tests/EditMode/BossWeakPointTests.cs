using System.Collections.Generic;
using NUnit.Framework;
using VerdantSiege.Battle;
using VerdantSiege.Combat;
using VerdantSiege.Data;
using VerdantSiege.Grid;
using static VerdantSiege.Tests.M3Fixtures;

namespace VerdantSiege.Tests
{
    /// <summary>
    /// Boss weak points (special attack wind-up), stagger, attack shapes, and the death/victory state.
    /// Uses an in-memory boss whose only attack is the special (or a chosen shape), independent of shipped values.
    /// </summary>
    public sealed class BossWeakPointTests
    {
        private TestContent _c;
        private BossDefinition _boss;
        private DefenderDefinition _wall;
        private BossAttack _special;

        [SetUp]
        public void SetUp()
        {
            _c = new TestContent();
            _boss = _c.Make<BossDefinition>();
            _boss.id = "test_boss"; _boss.maxHealth = 10000f; _boss.guardedDamageMultiplier = 0.5f; _boss.exposedDamageMultiplier = 2f;
            _boss.entranceSeconds = 1f; _boss.phaseTransitionSeconds = 1f; _boss.uprootRefund = 0.5f;
            _boss.weakPointCount = 3; _boss.weakPointLanes = new List<int> { 0, 2, 4 }; _boss.weakPointHealth = 100f;
            _boss.weakPointWindowSeconds = 6f; _boss.staggerSeconds = 4f; _boss.staggerDamageMultiplier = 3f;
            _boss.bonusSeeds = 150; _boss.essenceFirstDefeat = 3; _boss.essenceRepeatDefeat = 1;
            _special = new BossAttack { kind = BossAttackKind.RootEruption, shape = BossStrikeShape.Column, cells = 5,
                telegraphSeconds = 1f, blockSeconds = 3f, exposesWeakPoints = true };
            _boss.phases = new List<BossPhase>
            {
                new BossPhase { name = "One", startsAtHealthFraction = 1f, attackInterval = 2f, pattern = new List<BossAttack> { _special } },
            };
            _wall = Defender(_c, "wall", DefenderBehaviourKind.Blocker, 1000f);
            _wall.cost = 100;
            _c.encounter.boss = _boss;
            _c.encounter.bossArrivesAtWave = 0;
            _c.encounter.prepTime = 1f;
            _c.encounter.startingVitality = 3000;
            _c.encounter.vitalityCap = 9000;
            _c.encounter.roster = new List<DefenderDefinition> { _wall, _c.shooter };
            _c.encounter.waves = new List<WaveDefinition> { _c.Wave(_c.walker, 1, 1, 2f, 600f) };
        }

        [TearDown] public void TearDown() => _c.Destroy();

        private BattleSimulation UntilSpecial()
        {
            BattleSimulation sim = _c.Sim();
            RunUntil(sim, () => sim.Boss.state == BossState.Telegraph, 10f);
            Assert.AreEqual(BossState.Telegraph, sim.Boss.state);
            return sim;
        }

        // ------------------------------------------------------------ data-driven lanes and counts

        [Test]
        public void WeakPointLanes_AreDataDriven_AndClamped()
        {
            var lanes = new List<int>();
            _boss.ResolveWeakPointLanes(5, lanes);
            CollectionAssert.AreEqual(new[] { 0, 2, 4 }, lanes, "authored lanes");

            _boss.weakPointCount = 2; _boss.weakPointLanes = new List<int> { 3, 3, 9 };
            _boss.ResolveWeakPointLanes(5, lanes);
            Assert.AreEqual(2, lanes.Count, "duplicates/out-of-range ignored, missing entries filled");
            CollectionAssert.AllItemsAreUnique(lanes);
            CollectionAssert.Contains(lanes, 3);

            _boss.weakPointCount = 4; _boss.weakPointLanes = new List<int>();
            _boss.ResolveWeakPointLanes(5, lanes);
            Assert.AreEqual(4, lanes.Count, "no authored lanes: spread evenly");
            CollectionAssert.AllItemsAreUnique(lanes);

            _boss.weakPointCount = 8;
            _boss.ResolveWeakPointLanes(5, lanes);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, lanes, "clamped to the lane count");
        }

        [Test]
        public void SpecialWindup_ExposesWeakPoints_FromData()
        {
            _boss.weakPointCount = 2; _boss.weakPointLanes = new List<int> { 1, 3 }; _boss.weakPointHealth = 55f;
            BattleSimulation sim = _c.Sim();
            int exposed = 0;
            sim.BossWeakPointsExposed += b => exposed++;
            RunUntil(sim, () => sim.Boss.state == BossState.Telegraph, 10f);
            Assert.AreEqual(1, exposed);
            Assert.AreEqual(2, sim.Boss.weakPoints.Count);
            Assert.AreEqual(1, sim.Boss.weakPoints[0].lane);
            Assert.AreEqual(3, sim.Boss.weakPoints[1].lane);
            Assert.AreEqual(55f, sim.Boss.weakPoints[0].health, 1e-3f);
            Assert.AreEqual(_boss.weakPointWindowSeconds, sim.Boss.stateDuration, 1e-3f, "the window replaces the attack's telegraph time");
            Assert.IsTrue(sim.Boss.WeakPointsExposed);
        }

        [Test]
        public void NormalAttack_ExposesNoWeakPoints()
        {
            _special.exposesWeakPoints = false;
            BattleSimulation sim = UntilSpecial();
            Assert.AreEqual(0, sim.Boss.weakPoints.Count);
            Assert.AreEqual(1f, sim.Boss.stateDuration, 1e-3f);
        }

        // ------------------------------------------------------------ lane targeting

        [Test]
        public void LaneDamage_HitsThePodFirst_OtherLanesHitTheBoss()
        {
            BattleSimulation sim = UntilSpecial();
            float boss = sim.Boss.health;
            sim.DamageBossInLane(2, 40f, DamageType.Physical);
            Assert.AreEqual(60f, sim.Boss.WeakPointInLane(2).health, 1e-3f, "pod takes full damage");
            Assert.AreEqual(boss, sim.Boss.health, 1e-3f, "the boss behind it takes nothing");
            sim.DamageBossInLane(1, 40f, DamageType.Physical);
            Assert.AreEqual(boss - 20f, sim.Boss.health, 1e-3f, "no pod in lane 1: guarded boss x0.5");
        }

        [Test]
        public void Projectile_InPodLane_DamagesPod_NotBoss()
        {
            _boss.weakPointWindowSeconds = 30f;
            BattleSimulation sim = UntilSpecial();
            Plant(sim, 1, 0, 6); // shooter in lane 0 (pod lane)
            int podHits = 0;
            sim.BossWeakPointDamaged += (b, w, d) => { if (w.lane == 0) podHits++; };
            float boss = sim.Boss.health;
            Run(sim, 4f);
            Assert.Greater(podHits, 0, "bolts in lane 0 hit the lane-0 pod");
            Assert.Less(sim.Boss.WeakPointInLane(0).health, 100f);
            Assert.AreEqual(boss, sim.Boss.health, 1e-3f, "the boss is shielded in that lane");
        }

        // ------------------------------------------------------------ interrupt / land

        [Test]
        public void AllWeakPointsDestroyed_InterruptsAttack_AndStaggers()
        {
            BattleSimulation sim = _c.Sim();
            DefenderRuntime a = Plant(sim, 0, 1, 4);
            DefenderRuntime b = Plant(sim, 0, 3, 4);
            RunUntil(sim, () => sim.Boss.state == BossState.Telegraph, 10f);
            CollectionAssert.IsNotEmpty(sim.Boss.targetCells);
            int staggered = 0, attacked = 0, destroyed = 0;
            sim.BossStaggered += x => staggered++;
            sim.BossAttacked += (x, atk) => attacked++;
            sim.BossWeakPointDestroyed += (x, w) => destroyed++;
            sim.DamageBossInLane(0, 500f, DamageType.Physical);
            sim.DamageBossInLane(2, 500f, DamageType.Physical);
            Assert.AreEqual(0, staggered, "two of three is not enough");
            Assert.AreEqual(BossState.Telegraph, sim.Boss.state);
            sim.DamageBossInLane(4, 500f, DamageType.Physical);
            Assert.AreEqual(3, destroyed);
            Assert.AreEqual(1, staggered);
            Assert.AreEqual(BossState.Staggered, sim.Boss.state);
            Assert.IsTrue(sim.Boss.CoreExposed, "Core exposed while staggered");
            CollectionAssert.IsEmpty(sim.Boss.targetCells, "the warning is cleared");
            Assert.AreEqual(1, sim.Boss.staggerCount);

            float before = sim.Boss.health;
            sim.DamageBoss(100f, DamageType.Physical);
            Assert.AreEqual(before - 300f, sim.Boss.health, 1e-3f, "stagger bonus x3");

            Run(sim, 3.9f);
            Assert.AreEqual(BossState.Staggered, sim.Boss.state, "stagger lasts its data duration");
            Assert.AreEqual(0, attacked, "the special never landed");
            Assert.IsTrue(a.alive && b.alive, "defenders in the warned column survive");
            Run(sim, 0.2f);
            Assert.AreEqual(BossState.Idle, sim.Boss.state, "recovers after staggerSeconds");
            Assert.IsFalse(sim.Boss.CoreExposed);
        }

        [Test]
        public void SomeWeakPointsSurvive_AttackLands_PodsWithdraw()
        {
            BattleSimulation sim = _c.Sim();
            DefenderRuntime a = Plant(sim, 0, 1, 4);
            DefenderRuntime b = Plant(sim, 0, 3, 4);
            RunUntil(sim, () => sim.Boss.state == BossState.Telegraph, 10f);
            int staggered = 0, attacked = 0, withdrawn = 0;
            sim.BossStaggered += x => staggered++;
            sim.BossAttacked += (x, atk) => attacked++;
            sim.BossWeakPointsWithdrawn += x => withdrawn++;
            sim.DamageBossInLane(0, 500f, DamageType.Physical);
            sim.DamageBossInLane(4, 500f, DamageType.Physical);
            Assert.AreEqual(1, sim.Boss.WeakPointsAlive);
            RunUntil(sim, () => attacked > 0, 7f);
            Assert.AreEqual(1, attacked, "the special lands when the window closes");
            Assert.AreEqual(0, staggered);
            Assert.AreEqual(1, withdrawn);
            Assert.AreEqual(0, sim.Boss.weakPoints.Count, "pods withdraw");
            Assert.AreEqual(1, sim.Boss.specialsLanded);
            Assert.IsFalse(a.alive || b.alive, "the crowded column was crushed");
            float before = sim.Boss.health;
            sim.DamageBossInLane(2, 100f, DamageType.Physical);
            Assert.AreEqual(before - 50f, sim.Boss.health, 1e-3f, "lane 2 hits the boss again once the pods are gone");
        }

        [Test]
        public void PhaseChange_DuringWindup_ClearsWeakPoints()
        {
            _boss.phases.Add(new BossPhase { name = "Two", startsAtHealthFraction = 0.5f, attackInterval = 2f, pattern = new List<BossAttack> { _special } });
            BattleSimulation sim = UntilSpecial();
            sim.DamageBossInLane(1, 50000f, DamageType.Piercing);
            Assert.AreEqual(BossState.Transition, sim.Boss.state);
            Assert.AreEqual(0, sim.Boss.weakPoints.Count);
            Assert.IsNull(sim.Boss.WeakPointInLane(0));
        }

        // ------------------------------------------------------------ attack shapes

        [Test]
        public void LaneSweep_StrikesAStripAlongTheCrowdedLane()
        {
            _special.exposesWeakPoints = false;
            _special.shape = BossStrikeShape.LaneSweep;
            _special.cells = 3;
            BattleSimulation sim = _c.Sim();
            Plant(sim, 0, 3, 5);
            Plant(sim, 0, 3, 6);
            Plant(sim, 0, 1, 2);
            RunUntil(sim, () => sim.Boss.state == BossState.Telegraph, 10f);
            Assert.AreEqual(3, sim.Boss.targetCells.Count);
            foreach (GridCoord c in sim.Boss.targetCells) Assert.AreEqual(3, c.lane, "all in the crowded lane");
            var cols = new List<int>();
            foreach (GridCoord c in sim.Boss.targetCells) cols.Add(c.column);
            CollectionAssert.Contains(cols, 5);
            CollectionAssert.Contains(cols, 6);
            cols.Sort();
            Assert.AreEqual(cols[0] + 2, cols[2], "contiguous strip");
            Assert.Greater(cols[0], 0, "never the Heartroot's own column");
        }

        [Test]
        public void MultiColumn_StrikesDistinctColumns()
        {
            _special.exposesWeakPoints = false;
            _special.shape = BossStrikeShape.MultiColumn;
            _special.columns = 3;
            _special.cells = 2;
            BattleSimulation sim = _c.Sim();
            Plant(sim, 0, 0, 2);
            Plant(sim, 0, 1, 4);
            RunUntil(sim, () => sim.Boss.state == BossState.Telegraph, 10f);
            Assert.AreEqual(6, sim.Boss.targetCells.Count);
            var cols = new HashSet<int>();
            foreach (GridCoord c in sim.Boss.targetCells) cols.Add(c.column);
            Assert.AreEqual(3, cols.Count, "three different columns");
            Assert.IsTrue(cols.Contains(2) && cols.Contains(4), "crowded columns first");
        }

        // ------------------------------------------------------------ death / victory

        [Test]
        public void Defeat_MidWindup_WinsWithersBlight_ClearsPods_GrantsBonusSeeds()
        {
            BattleSimulation sim = UntilSpecial();
            sim.SpawnEnemy(_c.walker, 1);
            sim.SpawnEnemy(_c.walker, 3);
            Assert.Greater(sim.AliveEnemyCount, 0);
            Assert.AreEqual(0, sim.BossBonusSeeds, "no bonus before the win");
            int defeated = 0;
            sim.BossDefeated += b => defeated++;
            sim.DamageBossInLane(1, 1e6f, DamageType.Piercing);
            Assert.AreEqual(1, defeated);
            Assert.AreEqual(BossState.Defeated, sim.Boss.state);
            Assert.AreEqual(0, sim.Boss.weakPoints.Count);
            Assert.AreEqual(0, sim.AliveEnemyCount, "all remaining Blight wither");
            Assert.IsFalse(sim.Boss.IsTargetable);
            sim.Step(BattleSimulation.FixedStep);
            Assert.AreEqual(BattleState.Won, sim.State);
            Assert.AreEqual(150, sim.BossBonusSeeds);
            Assert.AreEqual(_c.encounter.seedReward + _c.encounter.flawlessBonus, sim.SeedsEarned, "encounter reward unchanged; bonus is separate");
        }
    }
}
