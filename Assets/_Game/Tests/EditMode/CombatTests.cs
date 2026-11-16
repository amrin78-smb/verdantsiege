using System.Collections.Generic;
using NUnit.Framework;
using VerdantSiege.Battle;
using VerdantSiege.Combat;
using VerdantSiege.Data;
using VerdantSiege.Grid;

namespace VerdantSiege.Tests
{
    public sealed class CombatTests
    {
        private TestContent _c;
        [SetUp] public void SetUp() { _c = new TestContent(); _c.encounter.startingVitality = 2000; _c.encounter.prepTime = 999f; }
        [TearDown] public void TearDown() => _c.Destroy();

        [Test]
        public void Damage_ResistanceMultiplies()
        {
            var res = new List<DamageModifier> { new DamageModifier(DamageType.Fire, 0.5f), new DamageModifier(DamageType.Frost, 1.5f) };
            float armor = 0f, hp = 100f;
            DamageCalculator.Apply(20f, DamageType.Fire, res, ref armor, ref hp);
            Assert.AreEqual(90f, hp, 1e-4f);
            DamageCalculator.Apply(20f, DamageType.Frost, res, ref armor, ref hp);
            Assert.AreEqual(60f, hp, 1e-4f);
            DamageCalculator.Apply(20f, DamageType.Physical, res, ref armor, ref hp);
            Assert.AreEqual(40f, hp, 1e-4f);
        }

        [Test]
        public void Armor_AbsorbsThenOverflows_PiercingBypasses()
        {
            float armor = 30f, hp = 100f;
            DamageResult r = DamageCalculator.Apply(50f, DamageType.Physical, null, ref armor, ref hp);
            Assert.IsTrue(r.armorBroken);
            Assert.AreEqual(0f, armor);
            Assert.AreEqual(80f, hp, 1e-4f);

            armor = 30f; hp = 100f;
            DamageCalculator.Apply(50f, DamageType.Piercing, null, ref armor, ref hp);
            Assert.AreEqual(30f, armor, "piercing ignores plating");
            Assert.AreEqual(50f, hp, 1e-4f);
        }

        [Test]
        public void Damage_KillsAtZero_NoNegativeHealth()
        {
            float armor = 0f, hp = 10f;
            DamageResult r = DamageCalculator.Apply(50f, DamageType.Physical, null, ref armor, ref hp);
            Assert.IsTrue(r.killed);
            Assert.AreEqual(0f, hp);
            Assert.AreEqual(10f, r.toHealth);
        }

        [Test]
        public void Shooter_FiresOnlyWithTargetAhead_AndProjectileHits()
        {
            var sim = _c.Sim();
            sim.TryDeploy(0, new GridCoord(2, 0));
            int fired = 0, hits = 0;
            sim.ProjectileFired += p => fired++;
            sim.ProjectileHit += (p, e) => hits++;
            TestContent.Run(sim, 3f);
            Assert.AreEqual(0, fired, "no target -> no shots");

            EnemyRuntime e = sim.SpawnEnemy(_c.walker, 2);
            TestContent.Run(sim, 4f);
            Assert.Greater(fired, 0);
            Assert.Greater(hits, 0);
            Assert.Less(e.health, _c.walker.maxHealth);
        }

        [Test]
        public void Shooter_IgnoresOtherLanes()
        {
            var sim = _c.Sim();
            sim.TryDeploy(0, new GridCoord(2, 0));
            int fired = 0;
            sim.ProjectileFired += p => fired++;
            sim.SpawnEnemy(_c.walker, 3);
            TestContent.Run(sim, 4f);
            Assert.AreEqual(0, fired);
        }

        [Test]
        public void Projectile_Pierce_HitsMultipleEnemies()
        {
            _c.bolt.pierceCount = 2;
            _c.bolt.damage = 5f;
            var sim = _c.Sim();
            EnemyRuntime a = sim.SpawnEnemy(_c.walker, 1);
            EnemyRuntime b = sim.SpawnEnemy(_c.walker, 1);
            a.x = 5f; b.x = 6f;
            sim.TryDeploy(0, new GridCoord(1, 0));
            TestContent.Run(sim, 1.6f); // first shot at 0.5s, flies ~5 columns/0.9s
            Assert.Less(a.health, 200f);
            Assert.Less(b.health, 200f);
        }

        [Test]
        public void Projectile_Splash_DamagesNeighboursInLane()
        {
            _c.bolt.splashRadius = 1f; _c.bolt.splashDamageFraction = 0.5f;
            var sim = _c.Sim();
            EnemyRuntime a = sim.SpawnEnemy(_c.walker, 1);
            EnemyRuntime b = sim.SpawnEnemy(_c.walker, 1);
            EnemyRuntime far = sim.SpawnEnemy(_c.walker, 1);
            a.x = 5f; b.x = 5.6f; far.x = 8.5f;
            sim.DamageEnemy(a, 1f, DamageType.Physical); // sanity
            ProjectileHitBetween(sim, a);
            Assert.AreEqual(200f - 1f - 20f, a.health, 0.01f);
            Assert.AreEqual(190f, b.health, 0.01f);
            Assert.AreEqual(200f, far.health, 0.01f);
        }

        private void ProjectileHitBetween(BattleSimulation sim, EnemyRuntime first)
        {
            sim.TryDeploy(0, new GridCoord(first.lane, 0));
            int hits = 0;
            sim.ProjectileHit += (p, e) => hits++;
            // freeze enemies so geometry stays put
            _c.walker.moveSpeed = 0.0001f;
            while (hits == 0) sim.Step(BattleSimulation.FixedStep);
        }

        [Test]
        public void Knockback_PushesEnemyBack()
        {
            _c.bolt.knockback = 0.5f;
            _c.walker.moveSpeed = 0.0001f;
            var sim = _c.Sim();
            EnemyRuntime e = sim.SpawnEnemy(_c.walker, 0);
            e.x = 4f;
            sim.TryDeploy(0, new GridCoord(0, 0));
            int hits = 0;
            sim.ProjectileHit += (p, en) => hits++;
            while (hits == 0) sim.Step(BattleSimulation.FixedStep);
            Assert.AreEqual(4.5f, e.x, 0.01f);
        }

        [Test]
        public void Enemy_WalksAndStopsAtDefender_ThenEatsIt()
        {
            var sim = _c.Sim();
            sim.TryDeploy(1, new GridCoord(2, 4)); // blocker, 3000 hp
            EnemyRuntime e = sim.SpawnEnemy(_c.walker, 2);
            float startX = e.x;
            TestContent.Run(sim, 2f);
            Assert.Less(e.x, startX, "walks toward the Heartroot");
            TestContent.Run(sim, 30f);
            Assert.AreEqual(EnemyActivity.Attacking, e.activity);
            Assert.That(e.x, Is.InRange(4.5f, 4.5f + BattleSimulation.EnemyContactAhead + 0.02f));
            var d = sim.DefenderAt(new GridCoord(2, 4));
            Assert.Less(d.health, 3000f);

            _c.blocker.maxHealth = 1; // irrelevant to the live runtime: runtime owns its own health
            Assert.Greater(d.health, 1f, "runtime state must not read back from the definition");
        }

        [Test]
        public void Enemy_KillsDefender_AndCellFrees()
        {
            _c.bolt.damage = 1f; // the shooter must not win this duel
            var sim = _c.Sim();
            sim.TryDeploy(0, new GridCoord(2, 1)); // shooter 300 hp
            bool removed = false;
            sim.DefenderRemoved += (d, killed) => removed = killed;
            EnemyRuntime e = sim.SpawnEnemy(_c.armored, 2); // armour 400 + 300 hp, shooter alone cannot stop it
            _c.armored.biteDamage = 100f;
            TestContent.Run(sim, 80f);
            Assert.IsTrue(removed);
            Assert.IsNull(sim.DefenderAt(new GridCoord(2, 1)));
            Assert.AreEqual(1, sim.Stats.defendersLost);
        }

        [Test]
        public void Enemy_Breach_DamagesHeartroot()
        {
            var sim = _c.Sim();
            sim.SpawnEnemy(_c.armored, 0);
            int remaining = -1;
            sim.HeartrootDamaged += (r, d) => remaining = r;
            TestContent.Run(sim, 70f); // (9.45+0.15)/0.16 = 60s
            Assert.AreEqual(1, remaining, "armoured enemy breach costs 2 of 3");
            Assert.AreEqual(0, sim.AliveEnemyCount);
        }

        [Test]
        public void ArmorBreak_RaisesEventAndSpeedsUp()
        {
            var sim = _c.Sim();
            EnemyRuntime e = sim.SpawnEnemy(_c.armored, 0);
            bool broke = false;
            sim.EnemyArmorBroken += en => broke = true;
            float before = e.CurrentSpeed;
            sim.DamageEnemy(e, 450f, DamageType.Physical);
            Assert.IsTrue(broke);
            Assert.AreEqual(250f, e.health, 0.01f);
            Assert.AreEqual(before * 1.25f, e.CurrentSpeed, 1e-4f);
        }
    }
}
