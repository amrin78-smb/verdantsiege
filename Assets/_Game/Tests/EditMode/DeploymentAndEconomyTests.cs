using NUnit.Framework;
using VerdantSiege.Battle;
using VerdantSiege.Grid;

namespace VerdantSiege.Tests
{
    public sealed class DeploymentAndEconomyTests
    {
        private TestContent _c;
        [SetUp] public void SetUp() => _c = new TestContent();
        [TearDown] public void TearDown() => _c.Destroy();

        [Test]
        public void Deploy_SpendsCostAndStartsCooldown()
        {
            var sim = _c.Sim();
            Assert.AreEqual(DeployResult.Ok, sim.TryDeploy(0, new GridCoord(2, 1)));
            Assert.AreEqual(50, sim.Vitality.Amount);
            Assert.AreEqual(5f, sim.Deck[0].cooldownRemaining, 1e-4f);
            Assert.IsNotNull(sim.DefenderAt(new GridCoord(2, 1)));
            Assert.AreEqual(1, sim.Stats.defendersPlanted);
        }

        [Test]
        public void Deploy_RapidDuplicateTapIsRejected()
        {
            _c.encounter.startingVitality = 1000;
            var sim = _c.Sim();
            Assert.AreEqual(DeployResult.Ok, sim.TryDeploy(0, new GridCoord(2, 1)));
            Assert.AreEqual(DeployResult.OnCooldown, sim.TryDeploy(0, new GridCoord(2, 2)));
            sim.DebugResetCooldowns();
            Assert.AreEqual(DeployResult.Occupied, sim.TryDeploy(0, new GridCoord(2, 1)));
            Assert.AreEqual(900, sim.Vitality.Amount, "only the first deploy may spend");
        }

        [Test]
        public void Deploy_InsufficientVitality_ChangesNothing()
        {
            _c.encounter.startingVitality = 99;
            var sim = _c.Sim();
            Assert.AreEqual(DeployResult.NotEnoughVitality, sim.TryDeploy(0, new GridCoord(0, 0)));
            Assert.AreEqual(99, sim.Vitality.Amount);
            Assert.IsTrue(sim.Deck[0].Ready);
            Assert.IsNull(sim.DefenderAt(new GridCoord(0, 0)));
        }

        [Test]
        public void Deploy_InvalidCellsAndCards()
        {
            _c.field.specialCells.Add(new VerdantSiege.Data.SpecialCell { coord = new GridCoord(1, 1), terrain = CellTerrain.Blocked });
            var sim = _c.Sim();
            Assert.AreEqual(DeployResult.OutOfBounds, sim.TryDeploy(0, new GridCoord(5, 0)));
            Assert.AreEqual(DeployResult.BlockedTerrain, sim.TryDeploy(0, new GridCoord(1, 1)));
            Assert.AreEqual(DeployResult.InvalidCard, sim.TryDeploy(7, new GridCoord(0, 0)));
            Assert.AreEqual(150, sim.Vitality.Amount);
        }

        [Test]
        public void Cooldown_RecoversOverTime()
        {
            _c.encounter.startingVitality = 1000;
            var sim = _c.Sim();
            sim.TryDeploy(0, new GridCoord(0, 0));
            TestContent.Run(sim, 4.9f);
            Assert.IsFalse(sim.Deck[0].Ready);
            TestContent.Run(sim, 0.2f);
            Assert.IsTrue(sim.Deck[0].Ready);
        }

        [Test]
        public void StartingCooldown_AppliesAtBattleStart()
        {
            _c.blocker.startingCooldown = 10f;
            var sim = _c.Sim();
            Assert.IsFalse(sim.Deck[1].Ready);
            Assert.AreEqual(DeployResult.OnCooldown, sim.TryDeploy(1, new GridCoord(0, 0)));
        }

        [Test]
        public void Undo_RefundsWithinGrace_NotAfter()
        {
            var sim = _c.Sim();
            sim.TryDeploy(0, new GridCoord(2, 2));
            var d = sim.DefenderAt(new GridCoord(2, 2));
            Assert.IsTrue(sim.TryUndo(d));
            Assert.AreEqual(150, sim.Vitality.Amount);
            Assert.IsTrue(sim.Deck[0].Ready);
            Assert.IsNull(sim.DefenderAt(new GridCoord(2, 2)));

            sim.TryDeploy(0, new GridCoord(2, 2));
            d = sim.DefenderAt(new GridCoord(2, 2));
            TestContent.Run(sim, 3.5f);
            Assert.IsFalse(sim.TryUndo(d));
        }

        [Test]
        public void VitalityBank_ClampsAndSpendsAtomically()
        {
            var bank = new VitalityBank(100, 200);
            Assert.IsFalse(bank.TrySpend(150));
            Assert.AreEqual(100, bank.Amount);
            Assert.IsTrue(bank.TrySpend(100));
            Assert.AreEqual(0, bank.Amount);
            Assert.AreEqual(200, bank.Add(500));
            Assert.AreEqual(200, bank.Amount);
            Assert.AreEqual(-200, bank.Add(-1000));
            Assert.AreEqual(0, bank.Amount);
        }

        [Test]
        public void Generator_ProducesPickups_WithBurst()
        {
            _c.encounter.startingVitality = 1000;
            var sim = _c.Sim();
            sim.TryDeploy(2, new GridCoord(0, 0));
            int spawned = 0, burstSeen = 0;
            sim.PickupSpawned += p => { spawned++; if (p.amount == 50) burstSeen++; };
            TestContent.Run(sim, 5f + 12f * 3 + 0.1f); // pulses at 5, 17, 29, 41
            Assert.AreEqual(4, spawned);
            Assert.AreEqual(1, burstSeen);
        }

        [Test]
        public void Pickups_CollectAndExpire()
        {
            _c.encounter.skyDropInterval = 4f;
            _c.encounter.skyDropFirstDelay = 1f;
            _c.encounter.pickupLifetime = 5f;
            _c.encounter.prepTime = 999f;
            var sim = _c.Sim();
            TestContent.Run(sim, 1.05f);
            Assert.AreEqual(1, sim.Pickups.Count);
            Assert.IsTrue(sim.CollectPickup(sim.Pickups[0].Id));
            Assert.AreEqual(175, sim.Vitality.Amount);
            Assert.IsFalse(sim.CollectPickup(-5));
            TestContent.Run(sim, 0.1f);
            Assert.AreEqual(0, sim.Pickups.Count);

            int expired = 0;
            sim.PickupRemoved += (p, collected) => { if (!collected) expired++; };
            TestContent.Run(sim, 4f + 5.1f); // spawn at t=5, expires at t=10
            Assert.AreEqual(1, expired);
        }

        [Test]
        public void AutoCollect_CollectsAfterDelay()
        {
            _c.encounter.skyDropInterval = 100f;
            _c.encounter.skyDropFirstDelay = 0.5f;
            var sim = new BattleSimulation(_c.encounter, 3, new BattleOptions { autoCollect = true, autoCollectDelay = 1f, undoGraceSeconds = 3f });
            TestContent.Run(sim, 2f);
            Assert.AreEqual(175, sim.Vitality.Amount);
        }
    }
}
