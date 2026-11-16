using System.Collections.Generic;
using NUnit.Framework;
using VerdantSiege.Battle;
using VerdantSiege.Combat;
using VerdantSiege.Data;
using VerdantSiege.Grid;
using VerdantSiege.Utilities;

namespace VerdantSiege.Tests
{
    public sealed class StatusWaveAndFlowTests
    {
        private TestContent _c;
        [SetUp] public void SetUp() => _c = new TestContent();
        [TearDown] public void TearDown() => _c.Destroy();

        private StatusEffectDefinition Status(StatusKind kind, StatusStacking stacking, float duration, float magnitude, int maxStacks = 1)
        {
            var s = _c.Make<StatusEffectDefinition>();
            s.kind = kind; s.stacking = stacking; s.duration = duration; s.magnitude = magnitude; s.maxStacks = maxStacks;
            s.tickInterval = 0.5f;
            return s;
        }

        [Test]
        public void Status_DurationExpires()
        {
            var set = new StatusEffectSet();
            set.Apply(Status(StatusKind.Slow, StatusStacking.RefreshDuration, 2f, 0.4f));
            set.Tick(1.9f, out _);
            Assert.AreEqual(1, set.Count);
            set.Tick(0.2f, out _);
            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void Status_RefreshResetsTimer_IgnoreKeepsIt()
        {
            var refresh = Status(StatusKind.Slow, StatusStacking.RefreshDuration, 2f, 0.4f);
            var ignore = Status(StatusKind.Stun, StatusStacking.IgnoreWhileActive, 2f, 0f);
            var set = new StatusEffectSet();
            set.Apply(refresh); set.Apply(ignore);
            set.Tick(1.5f, out _);
            Assert.IsTrue(set.Apply(refresh));
            Assert.IsFalse(set.Apply(ignore));
            set.Tick(1f, out _);
            Assert.IsNotNull(set.Find(refresh));
            Assert.IsNull(set.Find(ignore));
        }

        [Test]
        public void Status_StackIntensity_CapsAtMax()
        {
            var s = Status(StatusKind.Slow, StatusStacking.StackIntensity, 3f, 0.2f, 3);
            var set = new StatusEffectSet();
            for (int i = 0; i < 6; i++) set.Apply(s);
            Assert.AreEqual(3, set.Find(s).stacks);
            Assert.AreEqual(0.6f, set.SlowFraction(), 1e-4f);
        }

        [Test]
        public void Status_DamageOverTime_TicksOnEnemy()
        {
            var burn = Status(StatusKind.DamageOverTime, StatusStacking.RefreshDuration, 2.2f, 10f);
            burn.damageType = DamageType.Fire;
            _c.encounter.prepTime = 999f;
            var sim = _c.Sim();
            EnemyRuntime e = sim.SpawnEnemy(_c.walker, 0);
            sim.ApplyStatus(e, burn);
            TestContent.Run(sim, 2.1f);
            Assert.AreEqual(200f - 40f, e.health, 0.01f); // ticks at 0.5, 1.0, 1.5, 2.0
        }

        [Test]
        public void Status_SlowReducesMovement()
        {
            _c.encounter.prepTime = 999f;
            var sim = _c.Sim();
            EnemyRuntime a = sim.SpawnEnemy(_c.walker, 0);
            EnemyRuntime b = sim.SpawnEnemy(_c.walker, 1);
            a.x = b.x = 8f;
            sim.ApplyStatus(b, Status(StatusKind.Slow, StatusStacking.RefreshDuration, 10f, 0.5f));
            TestContent.Run(sim, 4f);
            Assert.AreEqual(8f - 1f, a.x, 0.02f);
            Assert.AreEqual(8f - 0.5f, b.x, 0.02f);
        }

        [Test]
        public void Waves_StartAfterPrep_AdvanceWhenCleared_ThenWin()
        {
            _c.encounter.waves = new List<WaveDefinition> { _c.Wave(_c.runner, 2, 0, 1f), _c.Wave(_c.runner, 1, 1) };
            _c.encounter.startingVitality = 2000;
            var sim = _c.Sim();
            var started = new List<int>();
            sim.WaveStarted += (i, major) => started.Add(i);
            TestContent.Run(sim, 4.9f);
            Assert.AreEqual(0, started.Count);
            TestContent.Run(sim, 0.2f);
            CollectionAssert.AreEqual(new[] { 0 }, started);
            TestContent.Run(sim, 1.1f);
            Assert.AreEqual(2, sim.AliveEnemyCount);
            sim.DebugDefeatAllEnemies();
            TestContent.Run(sim, 2.1f); // inter-wave gap
            CollectionAssert.AreEqual(new[] { 0, 1 }, started);
            TestContent.Run(sim, 0.1f);
            Assert.AreEqual(BattleState.Running, sim.State);
            sim.DebugDefeatAllEnemies();
            Assert.AreEqual(BattleState.Won, sim.State);
            Assert.AreEqual(_c.encounter.seedReward + _c.encounter.flawlessBonus, sim.SeedsEarned);
        }

        [Test]
        public void Waves_TimeoutStartsNextWaveEvenIfEnemiesRemain()
        {
            _c.encounter.waves = new List<WaveDefinition> { _c.Wave(_c.armored, 1, 0, 1f, 10f), _c.Wave(_c.runner, 1, 1) };
            var sim = _c.Sim();
            int last = -1;
            sim.WaveStarted += (i, m) => last = i;
            TestContent.Run(sim, 5f + 10f + 2f + 0.2f);
            Assert.AreEqual(1, last);
        }

        [Test]
        public void Loss_WhenHeartrootFalls_AndStops()
        {
            _c.encounter.heartrootHealth = 1;
            _c.encounter.waves = new List<WaveDefinition> { _c.Wave(_c.runner, 1, 0) };
            var sim = _c.Sim();
            BattleState? final = null;
            sim.StateChanged += s => final = s;
            TestContent.Run(sim, 40f);
            Assert.AreEqual(BattleState.Lost, final);
            Assert.AreEqual(DeployResult.BattleOver, sim.TryDeploy(0, new GridCoord(0, 0)));
            float t = sim.Stats.elapsed;
            TestContent.Run(sim, 5f);
            Assert.AreEqual(t, sim.Stats.elapsed, "a finished battle no longer advances");
        }

        [Test]
        public void Advance_AccumulatesFixedSteps_PauseIsNoAdvance()
        {
            var sim = _c.Sim();
            for (int i = 0; i < 10; i++) sim.Advance(0.05f); // 30-60 fps style frames
            Assert.AreEqual(0.5f, sim.Stats.elapsed, BattleSimulation.FixedStep);
            sim.Advance(0f); // paused frame
            Assert.AreEqual(0.5f, sim.Stats.elapsed, BattleSimulation.FixedStep);
            sim.Advance(10f); // huge hitch: capped
            Assert.LessOrEqual(sim.Stats.elapsed, 0.5f + BattleSimulation.FixedStep * (BattleSimulation.MaxStepsPerAdvance + 1));
        }

        [Test]
        public void Determinism_SameSeedSameOutcome()
        {
            _c.encounter.waves = new List<WaveDefinition> { _c.Wave(_c.walker, 6, -1, 1.5f), _c.Wave(_c.runner, 6, -1, 1f) };
            _c.encounter.skyDropInterval = 5f;
            string Run(int seed)
            {
                var sim = _c.Sim(seed);
                var log = new System.Text.StringBuilder();
                sim.EnemySpawned += e => log.Append(e.lane);
                sim.PickupSpawned += p => log.Append('p').Append((int)(p.x * 100));
                TestContent.Run(sim, 40f);
                return log.ToString();
            }
            Assert.AreEqual(Run(42), Run(42));
            Assert.AreNotEqual(Run(42), Run(43));
        }

        [Test]
        public void LaneSelection_NoMoreThanTwoInARow()
        {
            _c.encounter.waves = new List<WaveDefinition> { _c.Wave(_c.runner, 40, -1, 0.2f) };
            var sim = _c.Sim(7);
            var lanes = new List<int>();
            sim.EnemySpawned += e => lanes.Add(e.lane);
            TestContent.Run(sim, 15f);
            Assert.GreaterOrEqual(lanes.Count, 40);
            for (int i = 2; i < lanes.Count; i++)
                Assert.IsFalse(lanes[i] == lanes[i - 1] && lanes[i] == lanes[i - 2], $"three in a row at {i}");
        }

        [Test]
        public void Pools_ResetStateOnReturn()
        {
            var pool = new SimplePool<EnemyRuntime>(() => new EnemyRuntime());
            EnemyRuntime e = pool.Rent();
            e.health = 50; e.alive = true; e.armor = 5; e.lane = 3;
            e.statuses.Apply(Status(StatusKind.Slow, StatusStacking.RefreshDuration, 5f, 0.3f));
            pool.Return(e);
            EnemyRuntime again = pool.Rent();
            Assert.AreSame(e, again);
            Assert.AreEqual(0f, again.health);
            Assert.IsFalse(again.alive);
            Assert.AreEqual(0, again.statuses.Count);
            Assert.AreEqual(0, again.lane);
        }

        [Test]
        public void SeededRandom_RangesAndRepeatability()
        {
            var a = new SeededRandom(99);
            var b = new SeededRandom(99);
            for (int i = 0; i < 200; i++)
            {
                int x = a.Range(0, 5);
                Assert.AreEqual(x, b.Range(0, 5));
                Assert.That(x, Is.InRange(0, 4));
                float f = a.NextFloat(); b.NextFloat();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }

        [Test]
        public void Validator_FlagsBrokenEncounters()
        {
            Assert.IsEmpty(EncounterValidator.Validate(_c.encounter));
            _c.encounter.waves.Add(_c.Make<WaveDefinition>());
            _c.encounter.waves.Add(_c.Wave(_c.walker, 1, 7));
            _c.shooter.projectile = null;
            List<string> errors = EncounterValidator.Validate(_c.encounter);
            Assert.AreEqual(3, errors.Count, string.Join("\n", errors));
        }
    }
}
