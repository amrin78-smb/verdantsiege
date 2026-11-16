using System.Collections.Generic;
using NUnit.Framework;
using VerdantSiege.Battle;
using VerdantSiege.Combat;
using VerdantSiege.Data;
using VerdantSiege.Grid;
using static VerdantSiege.Tests.M3Fixtures;

namespace VerdantSiege.Tests
{
    /// <summary>Milestone 3 defenders: pierce, ricochet, chain lightning, chill/freeze, trap, mender, decoy/reflect, ambusher.</summary>
    public sealed class DefenderAbilityTests
    {
        private TestContent _c;
        [SetUp] public void SetUp() => _c = new TestContent();
        [TearDown] public void TearDown() => _c.Destroy();

        [Test]
        public void Thorncaster_PiercesArmour_AndHitsTwoTargets()
        {
            ProjectileDefinition thorn = Projectile(_c, 40f, DamageType.Piercing, 9f);
            thorn.pierceCount = 2;
            BattleSimulation sim = Sandbox(_c, Shooter(_c, thorn, 10f));
            Plant(sim, 0, 2, 0);
            EnemyRuntime armored = Spawn(sim, _c.armored, 2, 5f);
            EnemyRuntime walker = Spawn(sim, _c.walker, 2, 6f);
            EnemyRuntime third = Spawn(sim, _c.walker, 2, 7f);
            int hits = 0;
            sim.ProjectileHit += (p, e) => hits++;
            Run(sim, 1.5f);
            Assert.AreEqual(2, hits, "pierce 2 hits exactly two enemies");
            Assert.AreEqual(400f, armored.armor, 1e-3f, "piercing damage bypasses the plating");
            Assert.AreEqual(260f, armored.health, 1e-3f);
            Assert.AreEqual(160f, walker.health, 1e-3f);
            Assert.AreEqual(200f, third.health, 1e-3f, "third enemy untouched");
        }

        [Test]
        public void Whirlseed_Ricochets_ExactBounceCount_WithFalloff()
        {
            ProjectileDefinition whirl = Projectile(_c, 40f, DamageType.Physical, 7f);
            whirl.bounces = 3; whirl.bounceFalloff = 0.5f; whirl.bounceRange = 2.5f;
            BattleSimulation sim = Sandbox(_c, Shooter(_c, whirl, 20f));
            Plant(sim, 0, 2, 0);
            var enemies = new List<EnemyRuntime>
            {
                Spawn(sim, _c.walker, 2, 5f), Spawn(sim, _c.walker, 1, 5.6f), Spawn(sim, _c.walker, 2, 6.4f),
                Spawn(sim, _c.walker, 3, 6.8f), Spawn(sim, _c.walker, 1, 7.4f), Spawn(sim, _c.walker, 3, 8.0f),
            };
            int hits = 0;
            sim.ProjectileHit += (p, e) => hits++;
            Run(sim, 4f);
            Assert.AreEqual(4, hits, "first hit + 3 bounces");
            float total = 0f;
            int damaged = 0;
            foreach (EnemyRuntime e in enemies) { total += 200f - e.health; if (e.health < 200f) damaged++; }
            Assert.AreEqual(4, damaged, "four different enemies");
            Assert.AreEqual(40f + 20f + 10f + 5f, total, 1e-2f, "damage halves per bounce");
            Assert.AreEqual(160f, enemies[0].health, 1e-3f, "first target takes full damage");
        }

        [Test]
        public void StormOrchid_ChainsToFourTargets_AcrossAdjacentLanes_AndStunsMechanical()
        {
            StatusEffectDefinition stun = Status(_c, StatusKind.Stun, 1f, 0f, StatusStacking.IgnoreWhileActive);
            DefenderDefinition orchid = Defender(_c, "orchid", DefenderBehaviourKind.ChainCaster);
            orchid.abilityDamage = 50f; orchid.abilityCount = 4; orchid.abilityRange = 1.8f; orchid.abilityFalloff = 1f;
            orchid.abilityDamageType = DamageType.Electric; orchid.abilityStatus = stun; orchid.attackInterval = 10f; orchid.canHitAir = true;
            EnemyDefinition mech = Enemy(_c, "mech");
            mech.tags = EnemyTags.Mechanical;
            BattleSimulation sim = Sandbox(_c, orchid);
            Plant(sim, 0, 2, 0);
            EnemyRuntime a = Spawn(sim, mech, 2, 4.0f);
            EnemyRuntime b = Spawn(sim, _c.walker, 1, 4.6f);
            EnemyRuntime cc = Spawn(sim, _c.walker, 3, 4.6f);
            EnemyRuntime d = Spawn(sim, _c.walker, 2, 5.4f);
            EnemyRuntime far1 = Spawn(sim, _c.walker, 4, 8.5f);
            EnemyRuntime far2 = Spawn(sim, _c.walker, 0, 8.5f);
            int casts = 0, points = 0;
            sim.ChainCast += (src, n) => { casts++; points = n; };
            Run(sim, 0.2f);
            Assert.AreEqual(1, casts);
            Assert.AreEqual(5, points, "caster + 4 targets");
            foreach (EnemyRuntime e in new[] { a, b, cc, d }) Assert.AreEqual(150f, e.health, 1e-3f, "chained target");
            Assert.AreEqual(200f, far1.health, 1e-3f);
            Assert.AreEqual(200f, far2.health, 1e-3f);
            Assert.IsTrue(a.statuses.Has(StatusKind.Stun), "mechanical target short-circuits");
            Assert.IsFalse(b.statuses.Has(StatusKind.Stun), "organic target is not stunned");
        }

        [Test]
        public void FrostFern_ChillStacks_ThenFreezesAfterFourHits()
        {
            StatusEffectDefinition chill = Status(_c, StatusKind.Slow, 5f, 0.25f, StatusStacking.StackIntensity, 2);
            StatusEffectDefinition freeze = Status(_c, StatusKind.Stun, 1f, 0f, StatusStacking.IgnoreWhileActive);
            ProjectileDefinition shard = Projectile(_c, 1f, DamageType.Frost, 12f);
            shard.statusEffect = chill; shard.freezeAfterHits = 4; shard.freezeStatus = freeze;
            BattleSimulation sim = Sandbox(_c, Shooter(_c, shard, 0.5f));
            Plant(sim, 0, 2, 0);
            EnemyRuntime e = Spawn(sim, _c.walker, 2, 3f);
            int hits = 0;
            sim.ProjectileHit += (p, en) => hits++;
            RunUntil(sim, () => hits >= 1, 5f);
            sim.Step(BattleSimulation.FixedStep);
            Assert.AreEqual(0.25f, e.statuses.SlowFraction(), 1e-4f, "one chill stack");
            Assert.IsFalse(e.statuses.Has(StatusKind.Stun));
            RunUntil(sim, () => hits >= 2, 5f);
            Assert.AreEqual(0.5f, e.statuses.SlowFraction(), 1e-4f, "two chill stacks");
            RunUntil(sim, () => hits >= 3, 5f);
            Assert.IsFalse(e.statuses.Has(StatusKind.Stun), "no freeze before the fourth hit");
            RunUntil(sim, () => hits >= 4, 5f);
            Assert.IsTrue(e.statuses.Has(StatusKind.Stun), "fourth hit freezes");
            Assert.AreEqual(0, e.frostHits, "freeze counter resets");
            Assert.AreEqual(0f, e.CurrentSpeed, "frozen enemies do not move");
        }

        [Test]
        public void Burrowroot_HiddenWhileArmed_TriggersOnStep_EdibleWhileRearming_ThenRearms()
        {
            DefenderDefinition trap = Defender(_c, "trap", DefenderBehaviourKind.Trap);
            trap.abilityDamage = 150f; trap.abilityRange = 0.75f; trap.abilityRecharge = 3f; trap.firstAttackDelay = 0.5f;
            BattleSimulation sim = Sandbox(_c, trap);
            DefenderRuntime t = Plant(sim, 0, 2, 4);
            Assert.AreEqual(DefenderAbilityState.Recovering, t.abilityState, "arms after a short delay");
            Run(sim, 0.6f);
            Assert.AreEqual(DefenderAbilityState.Ready, t.abilityState);

            EnemyRuntime probe = Spawn(sim, _c.walker, 2, 4.7f);
            Assert.IsNull(sim.FindBlocker(probe), "an armed trap is walked onto, not eaten");
            int triggers = 0;
            sim.TrapTriggered += d => triggers++;
            sim.Step(BattleSimulation.FixedStep);
            Assert.AreEqual(1, triggers, "stepping on the cell triggers it");
            Assert.AreEqual(50f, probe.health, 1e-3f);
            Assert.AreEqual(DefenderAbilityState.Recovering, t.abilityState);
            Assert.AreSame(t, sim.FindBlocker(probe), "while rearming it is an ordinary plant that blocks and gets eaten");
            Run(sim, 1.5f);
            Assert.Less(t.health, t.MaxHealth, "bitten while rearming");
            Run(sim, 1.6f);
            Assert.AreEqual(2, triggers, "rearmed and erupted again");
            Assert.IsFalse(probe.alive);
        }

        [Test]
        public void MenderMoss_HealsOnly3x3_AndCleansesOneStatus()
        {
            DefenderDefinition mender = Defender(_c, "mender", DefenderBehaviourKind.Mender);
            mender.abilityDamage = 100f; mender.abilityRange = 1f; mender.abilityRecharge = 2f; mender.abilityCount = 1;
            DefenderDefinition wall = Defender(_c, "wall", DefenderBehaviourKind.Blocker, 1000f);
            BattleSimulation sim = Sandbox(_c, mender, wall);
            var inside = new[] { Plant(sim, 1, 1, 3), Plant(sim, 1, 3, 5), Plant(sim, 1, 2, 5) };
            var outside = new[] { Plant(sim, 1, 0, 4), Plant(sim, 1, 2, 6) };
            foreach (DefenderRuntime d in inside) d.health = 500f;
            foreach (DefenderRuntime d in outside) d.health = 500f;
            StatusEffectDefinition corrosion = Status(_c, StatusKind.DamageOverTime, 10f, 1f);
            StatusEffectDefinition spores = Status(_c, StatusKind.Slow, 10f, 0.4f);
            sim.ApplyDefenderStatus(inside[0], corrosion);
            sim.ApplyDefenderStatus(inside[0], spores);
            Plant(sim, 0, 2, 4);
            sim.Step(BattleSimulation.FixedStep);
            foreach (DefenderRuntime d in inside) Assert.AreEqual(600f, d.health, 1.5f, "healed inside the 3x3");
            foreach (DefenderRuntime d in outside) Assert.AreEqual(500f, d.health, 1e-3f, "not healed outside");
            Assert.AreEqual(1, inside[0].statuses.Count, "exactly one harmful status cleansed per pulse");
            Run(sim, 2.1f);
            Assert.AreEqual(0, inside[0].statuses.Count, "next pulse cleanses the other");
        }

        [Test]
        public void Mirrorleaf_LuresFromAdjacentLaneOnly_AndSappersPreferIt()
        {
            DefenderDefinition mirror = Defender(_c, "mirror", DefenderBehaviourKind.Decoy, 2000f);
            mirror.abilityRange = 2.2f; mirror.abilityRecharge = 3f; mirror.taunts = true; mirror.reflectsProjectiles = true;
            DefenderDefinition wall = Defender(_c, "wall", DefenderBehaviourKind.Blocker);
            BattleSimulation sim = Sandbox(_c, mirror, wall);
            Plant(sim, 0, 2, 3);
            EnemyRuntime near = Spawn(sim, _c.walker, 1, 5.0f);
            EnemyRuntime farLane = Spawn(sim, _c.walker, 4, 5.0f);
            sim.Step(BattleSimulation.FixedStep);
            Assert.IsTrue(near.lured);
            Assert.AreEqual(1, near.lane, "lane changes after the switch animation");
            Run(sim, EnemyRuntime.LaneSwitchSeconds + 0.1f);
            Assert.AreEqual(2, near.lane, "lured into the decoy's lane");
            Assert.AreEqual(4, farLane.lane, "two lanes away is out of reach");

            // Ranged targeting prefers the decoy even when another plant is nearer.
            DefenderRuntime closer = Plant(sim, 1, 2, 5);
            EnemyDefinition sapperDef = Enemy(_c, "sapper");
            EnemyRuntime sapper = Spawn(sim, sapperDef, 2, 6.6f);
            Assert.AreEqual(mirror, sim.FindRangedTarget(sapper, 3.5f).Definition, "decoy preferred over the nearer plant");
            Assert.AreSame(closer, sim.FindRangedTarget(sapper, 1.5f), "only the nearer plant is in range");
        }

        [Test]
        public void Mirrorleaf_ReflectsSpit_BackAtTheSapper()
        {
            DefenderDefinition mirror = Defender(_c, "mirror", DefenderBehaviourKind.Decoy, 2000f);
            mirror.abilityRange = 2.2f; mirror.reflectsProjectiles = true; mirror.reflectMultiplier = 2f; mirror.taunts = true;
            ProjectileDefinition spit = Projectile(_c, 40f, DamageType.Toxic, 5f);
            EnemyDefinition sapperDef = Enemy(_c, "sapper", 300f);
            sapperDef.ability = EnemyAbilityKind.RangedSpit; sapperDef.abilityRange = 2.5f; sapperDef.abilityInterval = 3f;
            sapperDef.abilityWindup = 0.5f; sapperDef.abilityDamage = 40f; sapperDef.abilityProjectile = spit;
            BattleSimulation sim = Sandbox(_c, mirror);
            DefenderRuntime m = Plant(sim, 0, 2, 3);
            EnemyRuntime sapper = Spawn(sim, sapperDef, 2, 5.5f);
            int reflected = 0;
            sim.ProjectileReflected += p => reflected++;
            Run(sim, 2.5f);
            Assert.AreEqual(1, reflected);
            Assert.AreEqual(m.MaxHealth, m.health, 1e-3f, "the mirror takes no damage");
            Assert.AreEqual(300f - 80f, sapper.health, 1e-3f, "spit returns at double strength");
        }

        [Test]
        public void SnapjawVine_DevoursFirstInReach_RecoversVulnerable_BigDamageOnNonDevourable()
        {
            DefenderDefinition snap = Defender(_c, "snap", DefenderBehaviourKind.Ambusher, 400f);
            snap.abilityRange = 1.2f; snap.abilityWindup = 0.3f; snap.abilityRecharge = 5f; snap.abilityDamage = 500f;
            BattleSimulation sim = Sandbox(_c, snap);
            DefenderRuntime s = Plant(sim, 0, 2, 3);
            EnemyRuntime outOfReach = Spawn(sim, _c.walker, 2, 6.0f);
            EnemyRuntime prey = Spawn(sim, _c.walker, 2, 4.5f);
            int devoured = 0;
            sim.EnemyDevoured += (d, e) => devoured++;
            sim.Step(BattleSimulation.FixedStep);
            Assert.AreEqual(DefenderAbilityState.Windup, s.abilityState, "jaws open first (telegraph)");
            Assert.IsTrue(prey.alive);
            Run(sim, 0.35f);
            Assert.AreEqual(1, devoured);
            Assert.IsFalse(prey.alive);
            Assert.AreEqual(DefenderAbilityState.Recovering, s.abilityState);

            EnemyDefinition eliteDef = Enemy(_c, "elite", 2000f);
            eliteDef.devourable = false; eliteDef.elite = true;
            EnemyRuntime elite = Spawn(sim, eliteDef, 2, 4.6f);
            Run(sim, 3f);
            Assert.AreEqual(2000f, elite.health, 1e-3f, "no attack while digesting");
            Run(sim, 2.5f);
            Assert.AreEqual(1500f, elite.health, 1e-3f, "non-devourable takes the big bite instead");
            Assert.IsTrue(elite.alive);
            Assert.IsTrue(outOfReach.alive);
        }
    }
}
