using NUnit.Framework;
using VerdantSiege.Battle;
using VerdantSiege.Combat;
using VerdantSiege.Grid;

namespace VerdantSiege.Tests
{
    public sealed class EconomyFeatureTests
    {
        private TestContent _c;
        [SetUp] public void SetUp() { _c = new TestContent(); _c.encounter.prepTime = 999f; }
        [TearDown] public void TearDown() => _c.Destroy();

        [Test]
        public void KilledEnemy_DropsVitality_BreachDoesNot()
        {
            _c.walker.vitalityDrop = 15;
            var sim = _c.Sim();
            PickupRuntime dropped = null;
            sim.PickupSpawned += p => dropped = p;
            EnemyRuntime e = sim.SpawnEnemy(_c.walker, 2);
            e.x = 5f;
            sim.DamageEnemy(e, 999f, DamageType.Physical);
            Assert.IsNotNull(dropped);
            Assert.AreEqual(PickupSource.Enemy, dropped.source);
            Assert.AreEqual(15, dropped.amount);
            Assert.IsTrue(sim.CollectPickup(dropped.Id));
            Assert.AreEqual(165, sim.Vitality.Amount);

            dropped = null;
            EnemyRuntime b = sim.SpawnEnemy(_c.runner, 0);
            b.x = 0.2f;
            TestContent.Run(sim, 2f); // breaches
            Assert.IsNull(dropped, "a breaching enemy gives nothing");
        }

        [Test]
        public void SunlightPools_AppearBoostAndFade()
        {
            _c.encounter.prepTime = 0f;
            _c.encounter.waves[0].groups[0].count = 1;
            _c.encounter.sunlightPoolInterval = 10f;
            _c.encounter.sunlightPoolDuration = 4f;
            _c.encounter.sunlightPoolCount = 2;
            _c.encounter.sunlightBoost = 2f;
            var sim = _c.Sim(11);
            int changes = 0;
            sim.SunlightPoolsChanged += () => changes++;
            TestContent.Run(sim, 5.1f); // first pool at prep + interval/2 = 5 s
            Assert.AreEqual(2, sim.SunlightPools.Count);
            GridCoord pool = sim.SunlightPools[0];
            Assert.AreEqual(2f, sim.SpeedMultiplierAt(pool));
            Assert.Less(pool.column, sim.Columns - 3, "pools favour the player's half");
            TestContent.Run(sim, 4.1f);
            Assert.AreEqual(0, sim.SunlightPools.Count);
            Assert.AreEqual(1f, sim.SpeedMultiplierAt(pool));
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void SunlightPools_OffWhenIntervalZero()
        {
            _c.encounter.sunlightPoolInterval = 0f;
            _c.encounter.startingVitality = 1000;
            var sim = _c.Sim();
            sim.TryDeploy(2, new GridCoord(0, 0));
            Assert.AreEqual(1f, sim.SpeedMultiplierAt(new GridCoord(0, 0)), "feature off = no boost");
        }
    }
}
namespace VerdantSiege.Tests
{
    public sealed class TutorialHoldTests
    {
        [Test]
        public void HoldFirstWave_FreezesPrepUntilReleased()
        {
            var c = new TestContent();
            c.encounter.prepTime = 2f;
            var sim = c.Sim();
            sim.HoldFirstWave = true;
            TestContent.Run(sim, 10f);
            Assert.AreEqual(-1, sim.Waves.CurrentWave);
            sim.HoldFirstWave = false;
            TestContent.Run(sim, 2.1f);
            Assert.AreEqual(0, sim.Waves.CurrentWave);
            c.Destroy();
        }

        [Test]
        public void HoldFirstWave_CountdownRunsUntilFloor()
        {
            var c = new TestContent();
            c.encounter.prepTime = 20f;
            var sim = c.Sim();
            sim.HoldFirstWave = true;
            TestContent.Run(sim, 8f);
            Assert.Less(sim.Waves.PrepRemaining, 13f, "countdown must keep running while the tutorial is open");
            Assert.IsFalse(sim.FirstWaveWaiting);
            TestContent.Run(sim, 30f);
            Assert.AreEqual(-1, sim.Waves.CurrentWave);
            Assert.IsTrue(sim.FirstWaveWaiting);
            Assert.That(sim.Waves.PrepRemaining, Is.EqualTo(BattleSimulation.HeldPrepFloor).Within(0.1f));
            sim.HoldFirstWave = false;
            TestContent.Run(sim, 5.2f);
            Assert.AreEqual(0, sim.Waves.CurrentWave);
            c.Destroy();
        }
    }
}
