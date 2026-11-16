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
    /// Living Evolution: XP per role, readiness, atomic purchase, every branch mechanic type, aura adjacency,
    /// determinism, no evolution after death, and the chooser state machine (simulation side).
    /// </summary>
    public sealed class EvolutionTests
    {
        private TestContent _c;
        [SetUp] public void SetUp() => _c = new TestContent();
        [TearDown] public void TearDown() => _c.Destroy();

        // ------------------------------------------------------------------ fixtures
        private EvolutionDefinition Evo(string id, int cost = 0, System.Action<EvolutionDefinition> mechanics = null)
        {
            var e = _c.Make<EvolutionDefinition>();
            e.id = id; e.displayName = id; e.cost = cost;
            mechanics?.Invoke(e);
            return e;
        }

        private static void Branches(DefenderDefinition d, EvolutionXpSource source, float xp, params EvolutionDefinition[] evos)
        {
            d.xpSource = source;
            d.evolutionXp = xp;
            d.evolutions = new List<EvolutionDefinition>(evos);
        }

        /// <summary>Fills the XP bar and evolves (the mechanic tests do not depend on how XP was earned).</summary>
        private static void ForceEvolve(BattleSimulation sim, DefenderRuntime d, int option = 0)
        {
            d.xp = d.Definition.evolutionXp;
            Assert.AreEqual(EvolveResult.Ok, sim.TryEvolve(d, option));
        }

        private EnemyDefinition Tank() => Enemy(_c, "tank", 100000f, 0f);

        // ================================================================== XP per role

        [Test]
        public void Xp_Attacker_EarnsDamageActuallyDealt_NotOverkill()
        {
            ProjectileDefinition bolt = Projectile(_c, 20f, DamageType.Physical);
            DefenderDefinition shooter = Shooter(_c, bolt, 5f);
            Branches(shooter, EvolutionXpSource.DamageDealt, 1000f, Evo("a"), Evo("b"));
            BattleSimulation sim = Sandbox(_c, shooter);
            DefenderRuntime d = Plant(sim, 0, 2, 0);
            EnemyRuntime e = Spawn(sim, Enemy(_c, "w", 200f, 0f), 2, 4f);
            Run(sim, 1.5f);
            Assert.AreEqual(20f, d.xp, 1e-3f);
            e.health = 5f; // the next bolt overkills: only 5 counts
            Run(sim, 5f);
            Assert.IsFalse(e.alive);
            Assert.AreEqual(25f, d.xp, 1e-3f);
        }

        [Test]
        public void Xp_GuardianAndDecoy_EarnDamageAbsorbed_OtherRolesDoNot()
        {
            DefenderDefinition wall = Defender(_c, "wall", DefenderBehaviourKind.Blocker, 4000f);
            Branches(wall, EvolutionXpSource.DamageAbsorbed, 1000f, Evo("a"), Evo("b"));
            DefenderDefinition plainWall = Defender(_c, "plain", DefenderBehaviourKind.Blocker, 4000f);
            Branches(plainWall, EvolutionXpSource.DamageDealt, 1000f, Evo("c"), Evo("d"));
            BattleSimulation sim = Sandbox(_c, wall, plainWall);
            DefenderRuntime d = Plant(sim, 0, 2, 3);
            DefenderRuntime other = Plant(sim, 1, 0, 3);
            Spawn(sim, _c.walker, 2, 3.8f);
            Spawn(sim, _c.walker, 0, 3.8f);
            Run(sim, 3.2f);
            Assert.Greater(d.xp, 0f);
            Assert.AreEqual(d.MaxHealth - d.health, d.xp, 1e-3f, "XP = damage absorbed");
            Assert.AreEqual(0f, other.xp, "a defender whose XP source is damage dealt earns nothing from bites");
        }

        [Test]
        public void Xp_Generator_EarnsVitalityProduced()
        {
            Branches(_c.generator, EvolutionXpSource.VitalityProduced, 500f, Evo("a"), Evo("b"));
            BattleSimulation sim = Sandbox(_c, _c.generator);
            DefenderRuntime d = Plant(sim, 0, 1, 1);
            Run(sim, 5.1f);
            Assert.AreEqual(25f, d.xp, 1e-3f);
        }

        [Test]
        public void Xp_Mender_EarnsHealingDone()
        {
            DefenderDefinition mender = Defender(_c, "mender", DefenderBehaviourKind.Mender);
            mender.abilityDamage = 50f; mender.abilityRange = 1f; mender.abilityRecharge = 1f; mender.abilityCount = 0;
            Branches(mender, EvolutionXpSource.HealingDone, 1000f, Evo("a"), Evo("b"));
            DefenderDefinition wall = Defender(_c, "wall", DefenderBehaviourKind.Blocker, 1000f);
            BattleSimulation sim = Sandbox(_c, mender, wall);
            DefenderRuntime m = Plant(sim, 0, 2, 2);
            DefenderRuntime w = Plant(sim, 1, 2, 3);
            sim.DamageDefender(w, 120f);
            Run(sim, 3.5f);
            Assert.AreEqual(1000f, w.health, 1e-3f);
            Assert.AreEqual(120f, m.xp, 1e-3f, "50 + 50 + 20 (no overheal)");
        }

        [Test]
        public void Xp_Trap_EarnsOnePerTrigger()
        {
            DefenderDefinition trap = Defender(_c, "trap", DefenderBehaviourKind.Trap);
            trap.abilityDamage = 10f; trap.abilityRange = 0.75f; trap.abilityRecharge = 3f; trap.firstAttackDelay = 0.1f;
            Branches(trap, EvolutionXpSource.Triggers, 3f, Evo("a"), Evo("b"));
            BattleSimulation sim = Sandbox(_c, trap);
            DefenderRuntime d = Plant(sim, 0, 2, 3);
            Spawn(sim, Enemy(_c, "w", 1000f, 0.25f), 2, 4.1f);
            int triggers = 0;
            sim.TrapTriggered += x => triggers++;
            RunUntil(sim, () => triggers > 0, 10f);
            Assert.AreEqual(1f, d.xp, 1e-3f);
        }

        // ================================================================== readiness

        [Test]
        public void Readiness_EventOnceAtThreshold_ThenPassiveXpOnlyAfterWavesStart()
        {
            ProjectileDefinition bolt = Projectile(_c, 20f, DamageType.Physical);
            DefenderDefinition shooter = Shooter(_c, bolt, 1f);
            Branches(shooter, EvolutionXpSource.DamageDealt, 60f, Evo("a"), Evo("b"));
            BattleSimulation sim = Sandbox(_c, shooter);
            DefenderRuntime d = Plant(sim, 0, 2, 0);
            int ready = 0;
            sim.DefenderEvolutionReady += x => { Assert.AreSame(d, x); ready++; };
            Spawn(sim, Tank(), 2, 5f);
            Run(sim, 1.5f);
            Assert.IsFalse(d.EvolutionReady, "40 / 60");
            Run(sim, 1.2f);
            Assert.IsTrue(d.EvolutionReady);
            Run(sim, 5f);
            Assert.AreEqual(1, ready, "raised once");
            Assert.LessOrEqual(d.xp, 90f, "XP is capped");

            // Passive growth: nothing before the first wave, threshold/240 per second after it.
            _c.encounter.prepTime = 2f;
            _c.encounter.roster = new List<DefenderDefinition> { shooter };
            BattleSimulation sim2 = _c.Sim();
            DefenderRuntime q = Plant(sim2, 0, 0, 0); // quiet lane (the wave walks lane 2)
            Run(sim2, 1.9f);
            Assert.AreEqual(0f, q.xp, 1e-4f);
            Run(sim2, 24.1f);
            Assert.AreEqual(60f * 24f / BattleSimulation.PassiveEvolutionSeconds, q.xp, 0.05f);
        }

        // ================================================================== purchase

        [Test]
        public void Purchase_IsAtomic_ChecksReadinessCostAndOption()
        {
            DefenderDefinition wall = Defender(_c, "wall", DefenderBehaviourKind.Blocker);
            Branches(wall, EvolutionXpSource.DamageAbsorbed, 100f, Evo("a", 60), Evo("b", 20));
            BattleSimulation sim = Sandbox(_c, wall);
            DefenderRuntime d = Plant(sim, 0, 2, 2);
            int vit = sim.Vitality.Amount;
            Assert.AreEqual(EvolveResult.NotReady, sim.TryEvolve(d, 0));
            d.xp = 100f;
            sim.Vitality.Add(-(vit - 50));
            Assert.AreEqual(EvolveResult.NotEnoughVitality, sim.TryEvolve(d, 0));
            Assert.AreEqual(50, sim.Vitality.Amount);
            Assert.IsNull(d.evolution);
            Assert.AreEqual(EvolveResult.InvalidOption, sim.TryEvolve(d, 2));
            Assert.AreEqual(EvolveResult.InvalidOption, sim.TryEvolve(d, -1));
            int evolved = 0;
            sim.DefenderEvolved += (x, e) => evolved++;
            Assert.AreEqual(EvolveResult.Ok, sim.TryEvolve(d, 1));
            Assert.AreEqual(30, sim.Vitality.Amount, "spent exactly the branch cost");
            Assert.AreEqual("b", d.evolution.id);
            Assert.AreEqual(EvolveResult.AlreadyEvolved, sim.TryEvolve(d, 1), "double tap");
            Assert.AreEqual(30, sim.Vitality.Amount);
            Assert.AreEqual(1, evolved);
            Assert.IsFalse(d.EvolutionReady);
        }

        [Test]
        public void NoEvolution_AfterDeath_OrWithoutBranches()
        {
            DefenderDefinition wall = Defender(_c, "wall", DefenderBehaviourKind.Blocker, 100f);
            Branches(wall, EvolutionXpSource.DamageAbsorbed, 50f, Evo("a"), Evo("b"));
            DefenderDefinition bare = Defender(_c, "bare", DefenderBehaviourKind.Blocker, 100f);
            BattleSimulation sim = Sandbox(_c, wall, bare);
            DefenderRuntime d = Plant(sim, 0, 2, 2);
            DefenderRuntime b = Plant(sim, 1, 1, 2);
            b.xp = 1000f;
            Assert.IsFalse(b.EvolutionReady, "no branches, never ready");
            Assert.AreEqual(EvolveResult.NotReady, sim.TryEvolve(b, 0));
            int vit = sim.Vitality.Amount;
            sim.DamageDefender(d, 1000f); // absorbs 100 (= threshold x2) and dies in the same hit
            Assert.IsFalse(d.alive);
            Assert.IsFalse(d.EvolutionReady);
            Assert.AreEqual(EvolveResult.NoDefender, sim.TryEvolve(d, 0));
            Assert.AreEqual(vit, sim.Vitality.Amount);
        }

        // ================================================================== chooser state machine

        [Test]
        public void Chooser_OpensOnReadyTap_ClosesElsewhere_DeniesThenEvolves_ClosesOnDeath()
        {
            DefenderDefinition wall = Defender(_c, "wall", DefenderBehaviourKind.Blocker, 500f);
            Branches(wall, EvolutionXpSource.DamageAbsorbed, 50f, Evo("a", 40), Evo("b", 10));
            BattleSimulation sim = Sandbox(_c, wall);
            DefenderRuntime a = Plant(sim, 0, 1, 1);
            DefenderRuntime b = Plant(sim, 0, 3, 1);
            var chooser = new EvolutionChooserState(sim);
            int changes = 0, denies = 0;
            chooser.Changed += () => changes++;
            chooser.Denied += r => { Assert.AreEqual(EvolveResult.NotEnoughVitality, r); denies++; };

            Assert.IsFalse(chooser.OnBattlefieldTap(a), "not ready: nothing opens");
            Assert.IsFalse(chooser.IsOpen);
            a.xp = b.xp = 50f;
            Assert.IsTrue(chooser.OnBattlefieldTap(a));
            Assert.AreSame(a, chooser.Target);
            Assert.IsTrue(chooser.OnBattlefieldTap(b), "tapping another ready defender switches");
            Assert.AreSame(b, chooser.Target);
            Assert.IsFalse(chooser.OnBattlefieldTap(null), "tapping empty ground closes");
            Assert.IsFalse(chooser.IsOpen);

            chooser.Open(a);
            sim.Vitality.Add(-(sim.Vitality.Amount - 20));
            Assert.AreEqual(EvolveResult.NotEnoughVitality, chooser.Choose(0));
            Assert.IsTrue(chooser.IsOpen, "a refusal keeps the card open");
            Assert.AreEqual(1, denies);
            Assert.AreEqual(EvolveResult.Ok, chooser.Choose(1));
            Assert.IsFalse(chooser.IsOpen, "success closes");
            Assert.AreEqual("b", a.evolution.id);
            Assert.IsFalse(chooser.Open(a), "evolved: cannot reopen");

            chooser.Open(b);
            sim.DamageDefender(b, 10000f);
            Assert.IsFalse(chooser.Validate(), "closes when its defender dies");
            Assert.IsFalse(chooser.IsOpen);
            Assert.AreEqual(EvolveResult.NoDefender, chooser.Choose(0));
            Assert.Greater(changes, 4);
        }

        // ================================================================== shooter branches

        [Test]
        public void Multishot_FansIntoNeighbouringLanes_AtReducedDamage()
        {
            ProjectileDefinition bolt = Projectile(_c, 20f, DamageType.Physical);
            DefenderDefinition shooter = Shooter(_c, bolt, 10f);
            Branches(shooter, EvolutionXpSource.DamageDealt, 1f, Evo("fan", 0, e => { e.multishotSideLanes = 1; e.sideShotDamageFraction = 0.5f; }));
            BattleSimulation sim = Sandbox(_c, shooter, shooter);
            DefenderRuntime d = Plant(sim, 0, 2, 0);
            DefenderRuntime edge = Plant(sim, 1, 0, 1);
            ForceEvolve(sim, d);
            ForceEvolve(sim, edge);
            var e0 = Spawn(sim, Tank(), 0, 5f);
            var e1 = Spawn(sim, Tank(), 1, 5f);
            var e2 = Spawn(sim, Tank(), 2, 5f);
            var e3 = Spawn(sim, Tank(), 3, 5f);
            var e4 = Spawn(sim, Tank(), 4, 5f);
            int fired = 0;
            sim.ProjectileFired += p => fired++;
            Run(sim, 1.5f);
            Assert.AreEqual(3 + 2, fired, "centre lane: 3 shots; edge lane 0: 2 (no lane -1)");
            Assert.AreEqual(20f, e2.MaxHealth - e2.health, 1e-3f);
            Assert.AreEqual(10f, e3.MaxHealth - e3.health, 1e-3f);
            Assert.AreEqual(10f + 10f, e1.MaxHealth - e1.health, 1e-3f, "lane 1 takes a side shot from each shooter");
            Assert.AreEqual(20f, e0.MaxHealth - e0.health, 1e-3f, "edge shooter's own lane");
            Assert.AreEqual(0f, e4.MaxHealth - e4.health, 1e-3f);
        }

        [Test]
        public void ChargeShot_OverridesProjectile_AndSlowsTheRhythm()
        {
            ProjectileDefinition bolt = Projectile(_c, 20f, DamageType.Physical);
            ProjectileDefinition lance = Projectile(_c, 100f, DamageType.Piercing, 12f);
            lance.pierceCount = 2;
            DefenderDefinition shooter = Shooter(_c, bolt, 1f);
            Branches(shooter, EvolutionXpSource.DamageDealt, 1f, Evo("cannon", 0, e => { e.projectileOverride = lance; e.attackIntervalMultiplier = 3f; }));
            BattleSimulation sim = Sandbox(_c, shooter);
            DefenderRuntime d = Plant(sim, 0, 2, 0);
            ForceEvolve(sim, d);
            EnemyDefinition plated = Enemy(_c, "plated", 10000f, 0f);
            plated.armorHealth = 400f;
            EnemyRuntime armored = Spawn(sim, plated, 2, 4f);
            EnemyRuntime tank = Spawn(sim, Tank(), 2, 6f);
            armored.x = armored.prevX = 4f; tank.x = tank.prevX = 6f;
            int fired = 0;
            sim.ProjectileFired += p => { fired++; Assert.AreSame(lance, p.Definition); };
            Run(sim, 6.3f);
            Assert.AreEqual(3, fired, "every 3 s: t=0, 3, 6");
            Assert.AreEqual(400f, armored.armor, 1e-3f, "piercing lance ignores the plating");
            Assert.Less(tank.health, tank.MaxHealth, "pierces into the second foe");
        }

        [Test]
        public void GlacialShards_DealBonusDamage_OnlyToChilledFoes()
        {
            ProjectileDefinition shard = Projectile(_c, 20f, DamageType.Frost);
            ProjectileDefinition glacial = Projectile(_c, 20f, DamageType.Frost);
            glacial.damageMultiplierVsChilled = 3f;
            DefenderDefinition fern = Shooter(_c, shard, 10f);
            Branches(fern, EvolutionXpSource.DamageDealt, 1f, Evo("glacial", 0, e => e.projectileOverride = glacial));
            StatusEffectDefinition slow = Status(_c, StatusKind.Slow, 10f, 0.3f);
            BattleSimulation sim = Sandbox(_c, fern, fern);
            ForceEvolve(sim, Plant(sim, 0, 1, 0));
            ForceEvolve(sim, Plant(sim, 1, 3, 0));
            EnemyRuntime chilled = Spawn(sim, Tank(), 1, 4f);
            EnemyRuntime warm = Spawn(sim, Tank(), 3, 4f);
            sim.ApplyStatus(chilled, slow);
            Run(sim, 1.5f);
            Assert.AreEqual(60f, chilled.MaxHealth - chilled.health, 1e-3f);
            Assert.AreEqual(20f, warm.MaxHealth - warm.health, 1e-3f);
        }

        [Test]
        public void Lobbed_ClusterSplits_AndWildfireSpillsIntoNeighbouringLanes()
        {
            ProjectileDefinition lob = Projectile(_c, 40f, DamageType.Fire, 6f);
            lob.motion = ProjectileMotion.Lobbed; lob.splashRadius = 0.4f; lob.splashDamageFraction = 1f;
            ProjectileDefinition wild = Projectile(_c, 40f, DamageType.Fire, 6f);
            wild.motion = ProjectileMotion.Lobbed; wild.splashRadius = 0.4f; wild.splashDamageFraction = 1f; wild.splashSideLanes = 1;
            DefenderDefinition clusterLily = Shooter(_c, lob, 10f);
            Branches(clusterLily, EvolutionXpSource.DamageDealt, 1f, Evo("storm", 0, e => { e.lobClusterCount = 3; e.lobClusterSpacing = 1f; e.lobClusterDamageFraction = 0.5f; }));
            DefenderDefinition wildLily = Shooter(_c, lob, 10f);
            Branches(wildLily, EvolutionXpSource.DamageDealt, 1f, Evo("wild", 0, e => e.projectileOverride = wild));

            BattleSimulation sim = Sandbox(_c, clusterLily);
            ForceEvolve(sim, Plant(sim, 0, 2, 0));
            var a = Spawn(sim, Tank(), 2, 4f);
            int fired = 0;
            sim.ProjectileFired += p => fired++;
            Run(sim, 2.5f);
            Assert.AreEqual(3, fired, "one attack, three seeds");
            Assert.AreEqual(20f, a.MaxHealth - a.health, 1e-3f, "the first seed lands on the target at half damage");

            BattleSimulation sim2 = Sandbox(_c, wildLily);
            ForceEvolve(sim2, Plant(sim2, 0, 2, 0));
            var m = Spawn(sim2, Tank(), 2, 4f);
            var up = Spawn(sim2, Tank(), 3, 4f);
            var down = Spawn(sim2, Tank(), 1, 4f);
            var far = Spawn(sim2, Tank(), 0, 4f);
            Run(sim2, 2.5f);
            Assert.AreEqual(40f, m.MaxHealth - m.health, 1e-3f);
            Assert.AreEqual(40f, up.MaxHealth - up.health, 1e-3f);
            Assert.AreEqual(40f, down.MaxHealth - down.health, 1e-3f);
            Assert.AreEqual(0f, far.MaxHealth - far.health, 1e-3f);
        }

        // ================================================================== aura (formation)

        [Test]
        public void Aura_SpeedsUpOnlyAdjacentDefenders_AndSharesVitalityPerLink()
        {
            ProjectileDefinition bolt = Projectile(_c, 1f, DamageType.Physical);
            DefenderDefinition shooter = Shooter(_c, bolt, 1f);
            DefenderDefinition hub = Defender(_c, "hub", DefenderBehaviourKind.Blocker);
            Branches(hub, EvolutionXpSource.DamageAbsorbed, 1f, Evo("network", 0, e =>
            {
                e.auraRadius = 1; e.auraActionSpeedBonus = 0.5f; e.auraVitalityPerLink = 5; e.auraVitalityInterval = 4f;
            }));
            BattleSimulation sim = Sandbox(_c, hub, shooter);
            DefenderRuntime h = Plant(sim, 0, 2, 2);
            DefenderRuntime diag = Plant(sim, 1, 1, 1);  // diagonal neighbour: linked
            DefenderRuntime far = Plant(sim, 1, 4, 0);   // two lanes away: not linked
            ForceEvolve(sim, h);
            Spawn(sim, Tank(), 1, 8f);
            Spawn(sim, Tank(), 4, 8f);
            int diagShots = 0, farShots = 0;
            sim.ProjectileFired += p => { if (p.sourceDefenderId == diag.Id) diagShots++; else if (p.sourceDefenderId == far.Id) farShots++; };
            int vit = sim.Vitality.Amount;
            Run(sim, 4.05f);
            Assert.AreEqual(vit + 5, sim.Vitality.Amount, "one link x 5 Vitality after 4 s");
            Run(sim, 5.95f);
            Assert.AreEqual(1, sim.AuraLinks.Count);
            Assert.AreEqual(h.Id, sim.AuraLinks[0].sourceId);
            Assert.AreEqual(diag.Id, sim.AuraLinks[0].targetId);
            Assert.AreEqual(0.5f, diag.auraSpeedBonus, 1e-4f);
            Assert.AreEqual(0f, far.auraSpeedBonus, 1e-4f);
            Assert.AreEqual(10, farShots, 1);
            Assert.AreEqual(15, diagShots, 1, "+50% action speed");

            sim.DamageDefender(h, 1e6f);
            Run(sim, 0.1f);
            Assert.AreEqual(0, sim.AuraLinks.Count, "links vanish with the source");
            Assert.AreEqual(0f, diag.auraSpeedBonus, 1e-4f);
        }

        // ================================================================== generator branches

        [Test]
        public void Generator_Abundant_ProducesMoreOften()
        {
            Branches(_c.generator, EvolutionXpSource.VitalityProduced, 1f, Evo("abundant", 0, e => e.generateIntervalMultiplier = 0.5f));
            BattleSimulation sim = Sandbox(_c, _c.generator, _c.generator);
            DefenderRuntime fast = Plant(sim, 0, 1, 1);
            Plant(sim, 1, 3, 1);
            ForceEvolve(sim, fast);
            int fastPulses = 0, basePulses = 0;
            sim.PickupSpawned += p => { if (p.laneFloat < 2f) fastPulses++; else basePulses++; };
            Run(sim, 16f);
            Assert.AreEqual(2, fastPulses, "5 s, then every 6 s");
            Assert.AreEqual(1, basePulses, "5 s, then every 12 s");
        }

        [Test]
        public void Generator_Capacitor_StoresUpToCapacity_ReleasesBurst()
        {
            Branches(_c.generator, EvolutionXpSource.VitalityProduced, 1f, Evo("capacitor", 0, e => { e.capacitorPulses = 2; e.capacitorReleaseMultiplier = 1.5f; }));
            BattleSimulation sim = Sandbox(_c, _c.generator);
            DefenderRuntime d = Plant(sim, 0, 1, 1);
            ForceEvolve(sim, d);
            int pickups = 0, released = 0;
            sim.PickupSpawned += p => { pickups++; released = p.amount; };
            Run(sim, 29.1f); // pulses at 5, 17, 29: the third is wasted (full)
            Assert.AreEqual(0, pickups, "stored, not dropped");
            Assert.AreEqual(2, d.storedPulses);
            Assert.AreEqual(50, d.storedVitality);
            Assert.IsTrue(sim.CapacitorFull(d));
            Assert.AreEqual(75, sim.TryReleaseCapacitor(d));
            Assert.AreEqual(1, pickups);
            Assert.AreEqual(75, released);
            Assert.AreEqual(0, d.storedVitality);
            Assert.AreEqual(0, sim.TryReleaseCapacitor(d), "nothing left");
        }

        // ================================================================== guardian branches

        [Test]
        public void Guardian_Thorns_Regen_AndSnare()
        {
            DefenderDefinition wall = Defender(_c, "wall", DefenderBehaviourKind.Blocker, 4000f);
            Branches(wall, EvolutionXpSource.DamageAbsorbed, 1f,
                Evo("thorns", 0, e => e.thornsFraction = 0.5f),
                Evo("regen", 0, e => e.regenPerSecond = 10f),
                Evo("snare", 0, e => e.onBittenStatus = Status(_c, StatusKind.Slow, 2f, 0.45f)));
            BattleSimulation sim = Sandbox(_c, wall, wall, wall);
            DefenderRuntime thorns = Plant(sim, 0, 0, 3);
            DefenderRuntime regen = Plant(sim, 1, 2, 3);
            DefenderRuntime snare = Plant(sim, 2, 4, 3);
            ForceEvolve(sim, thorns, 0);
            ForceEvolve(sim, regen, 1);
            ForceEvolve(sim, snare, 2);
            EnemyRuntime biter = Spawn(sim, _c.walker, 0, 3.8f);
            EnemyRuntime snared = Spawn(sim, _c.walker, 4, 3.8f);
            sim.DamageDefender(regen, 100f);
            Run(sim, 0.6f); // first bite at 0.5 s
            Assert.AreEqual(200f - 15f, biter.health, 1e-3f, "half of the 30 bite comes back");
            Assert.Greater(snared.statuses.SlowFraction(), 0.4f, "biter snared");
            Run(sim, 1.4f);
            Assert.AreEqual(3900f + 20f, regen.health, 0.5f, "10 health per second");
        }

        // ================================================================== ability branches

        [Test]
        public void Trap_FasterRearm_AndBrambleWebAcrossLanesWithStatus()
        {
            StatusEffectDefinition root = Status(_c, StatusKind.Stun, 1.5f, 0f, StatusStacking.IgnoreWhileActive);
            DefenderDefinition trap = Defender(_c, "trap", DefenderBehaviourKind.Trap);
            trap.abilityDamage = 50f; trap.abilityRange = 0.75f; trap.abilityRecharge = 4f; trap.firstAttackDelay = 0f;
            Branches(trap, EvolutionXpSource.Triggers, 1f,
                Evo("quick", 0, e => e.abilityRechargeMultiplier = 0.5f),
                Evo("web", 0, e => { e.blastSideLanes = 1; e.abilityStatusOverride = root; }));
            BattleSimulation sim = Sandbox(_c, trap, trap);
            DefenderRuntime quick = Plant(sim, 0, 0, 3);
            DefenderRuntime web = Plant(sim, 1, 3, 3);
            Run(sim, 0.1f);
            ForceEvolve(sim, quick, 0);
            ForceEvolve(sim, web, 1);
            EnemyRuntime stepper = Spawn(sim, Enemy(_c, "w", 1000f, 0f), 0, 3.5f);
            EnemyRuntime main = Spawn(sim, Enemy(_c, "m", 1000f, 0f), 3, 3.5f);
            EnemyRuntime side = Spawn(sim, Enemy(_c, "s", 1000f, 0f), 2, 3.5f);
            EnemyRuntime side2 = Spawn(sim, Enemy(_c, "s2", 1000f, 0f), 4, 3.5f);
            EnemyRuntime far = Spawn(sim, Enemy(_c, "f", 1000f, 0f), 1, 3.5f);
            Run(sim, 0.1f);
            Assert.AreEqual(950f, main.health, 1e-3f);
            Assert.AreEqual(950f, side.health, 1e-3f);
            Assert.AreEqual(950f, side2.health, 1e-3f);
            Assert.IsTrue(side.statuses.Has(StatusKind.Stun), "rooted");
            Assert.AreEqual(1000f, far.health, 1e-3f, "two lanes away is out of the web");
            stepper.alive = false; // only measure the rearm
            Run(sim, 1.8f);
            Assert.AreEqual(DefenderAbilityState.Recovering, quick.abilityState);
            Run(sim, 0.2f);
            Assert.AreEqual(DefenderAbilityState.Ready, quick.abilityState, "rearmed in 2 s instead of 4");
        }

        [Test]
        public void Mender_WideBloom_ReachesFurther_HealsLess()
        {
            DefenderDefinition mender = Defender(_c, "mender", DefenderBehaviourKind.Mender);
            mender.abilityDamage = 50f; mender.abilityRange = 1f; mender.abilityRecharge = 1f; mender.abilityCount = 0;
            Branches(mender, EvolutionXpSource.HealingDone, 1f, Evo("wide", 0, e => { e.abilityRangeBonus = 1f; e.abilityDamageMultiplier = 0.5f; }));
            DefenderDefinition wall = Defender(_c, "wall", DefenderBehaviourKind.Blocker, 1000f);
            BattleSimulation sim = Sandbox(_c, mender, wall);
            DefenderRuntime m = Plant(sim, 0, 2, 2);
            DefenderRuntime farWall = Plant(sim, 1, 4, 4); // two cells away diagonally
            ForceEvolve(sim, m);
            sim.DamageDefender(farWall, 200f);
            Run(sim, 0.05f);
            Assert.AreEqual(825f, farWall.health, 1e-3f, "healed 25 at distance 2");
        }

        [Test]
        public void ChainCaster_TempestJumpsFurther_ThunderheadOneBigStunningBolt()
        {
            StatusEffectDefinition stun = Status(_c, StatusKind.Stun, 1f, 0f, StatusStacking.IgnoreWhileActive);
            DefenderDefinition orchid = Defender(_c, "orchid", DefenderBehaviourKind.ChainCaster);
            orchid.abilityDamage = 50f; orchid.abilityCount = 1; orchid.abilityRange = 1.8f; orchid.abilityFalloff = 0.5f;
            orchid.abilityDamageType = DamageType.Electric; orchid.attackInterval = 10f;
            Branches(orchid, EvolutionXpSource.DamageDealt, 1f,
                Evo("tempest", 0, e => { e.abilityCountBonus = 2; e.abilityFalloffOverride = 1f; }),
                Evo("thunder", 0, e => { e.abilityCountOverride = 1; e.abilityDamageMultiplier = 2f; e.abilityStatusOverride = stun; e.abilityStatusOnAll = true; }));
            BattleSimulation sim = Sandbox(_c, orchid, orchid);
            DefenderRuntime t = Plant(sim, 0, 1, 0);
            DefenderRuntime h = Plant(sim, 1, 4, 0);
            ForceEvolve(sim, t, 0);
            ForceEvolve(sim, h, 1);
            var a = Spawn(sim, Tank(), 1, 4f);
            var b = Spawn(sim, Tank(), 0, 4.5f);
            var c = Spawn(sim, Tank(), 1, 5.2f);
            var d = Spawn(sim, Tank(), 4, 4f);
            var e2 = Spawn(sim, Tank(), 3, 4.5f);
            Run(sim, 0.1f);
            Assert.AreEqual(50f, a.MaxHealth - a.health, 1e-3f);
            Assert.AreEqual(50f, b.MaxHealth - b.health, 1e-3f, "falloff overridden to 1");
            Assert.AreEqual(50f, c.MaxHealth - c.health, 1e-3f, "three targets (1 + 2)");
            Assert.AreEqual(100f, d.MaxHealth - d.health, 1e-3f, "one bolt, double damage");
            Assert.IsTrue(d.statuses.Has(StatusKind.Stun), "stuns any foe, not only machines");
            Assert.AreEqual(0f, e2.MaxHealth - e2.health, 1e-3f, "no chain");
        }

        [Test]
        public void Ambusher_GraspingVines_ReachFurther_AndSnatchFlyers()
        {
            DefenderDefinition snap = Defender(_c, "snap", DefenderBehaviourKind.Ambusher, 400f);
            snap.abilityRange = 1.2f; snap.abilityWindup = 0.2f; snap.abilityRecharge = 10f; snap.abilityDamage = 500f;
            Branches(snap, EvolutionXpSource.DamageDealt, 1f, Evo("vines", 0, e => { e.abilityRangeBonus = 1.3f; e.grantsAntiAir = true; }),
                Evo("digest", 0, e => e.abilityRechargeMultiplier = 0.4f));
            EnemyDefinition glider = Enemy(_c, "glider", 100f, 0f);
            glider.movement = EnemyMovementKind.Flying;
            BattleSimulation sim = Sandbox(_c, snap, snap);
            DefenderRuntime vines = Plant(sim, 0, 1, 0);
            DefenderRuntime plain = Plant(sim, 1, 3, 0);
            ForceEvolve(sim, vines, 0);
            EnemyRuntime g1 = Spawn(sim, glider, 1, 2.6f);
            EnemyRuntime g2 = Spawn(sim, glider, 3, 1.0f);
            Run(sim, 0.5f);
            Assert.IsFalse(g1.alive, "snatched a flyer 2 tiles ahead");
            Assert.IsTrue(g2.alive, "an unevolved Snapjaw ignores flyers");
            Assert.Greater(vines.xp, 0f, "devour counts the swallowed health as XP");

            ForceEvolve(sim, plain, 1);
            Spawn(sim, _c.walker, 3, 1.4f);
            RunUntil(sim, () => plain.abilityState == DefenderAbilityState.Recovering, 3f);
            Assert.AreEqual(4f, plain.abilityTimer, 0.05f, "Quick Digest: 40% of 10 s");
        }

        [Test]
        public void Decoy_HallOfMirrors_LuresFromTwoLanesAway()
        {
            DefenderDefinition mirror = Defender(_c, "mirror", DefenderBehaviourKind.Decoy, 2000f);
            mirror.abilityRange = 3f; mirror.abilityRecharge = 2f; mirror.taunts = true;
            Branches(mirror, EvolutionXpSource.DamageAbsorbed, 1f, Evo("hall", 0, e => e.lureLaneReach = 2));
            BattleSimulation sim = Sandbox(_c, mirror);
            DefenderRuntime d = Plant(sim, 0, 2, 2);
            ForceEvolve(sim, d);
            EnemyRuntime e = Spawn(sim, Enemy(_c, "w", 200f, 0.1f), 0, 4.5f);
            Run(sim, 0.2f);
            Assert.IsTrue(e.lured);
            Assert.AreEqual(2, e.targetLane);
        }

        // ================================================================== determinism

        [Test]
        public void Determinism_WithEvolutions_SameSeedSameBattle()
        {
            _c.shooter.id = "sunspore"; _c.generator.id = "glowcap"; _c.blocker.id = "bulwark_bloom";
            Branches(_c.shooter, EvolutionXpSource.DamageDealt, 60f,
                Evo("solar_cannon", 25, e => e.attackIntervalMultiplier = 2f),
                Evo("sunburst", 25, e => e.multishotSideLanes = 1),
                Evo("solar_network", 25, e => { e.auraRadius = 1; e.auraActionSpeedBonus = 0.25f; e.auraVitalityPerLink = 3; }));
            Branches(_c.generator, EvolutionXpSource.VitalityProduced, 50f, Evo("abundant", 10, e => e.generateIntervalMultiplier = 0.6f));
            _c.encounter.waves = new List<WaveDefinition> { _c.Wave(_c.walker, 8, -1, 2f), _c.Wave(_c.runner, 8, -1, 1.5f), _c.Wave(_c.walker, 10, -1, 1f) };
            _c.encounter.skyDropInterval = 4f; _c.encounter.startingVitality = 400;
            string Play(int seed)
            {
                BattleSimulation sim = _c.Sim(seed);
                var log = new System.Text.StringBuilder();
                sim.DefenderEvolved += (d, e) => log.Append('E').Append(d.Id).Append(e.id).Append('@').Append((int)(sim.Stats.elapsed * 30));
                sim.EnemyKilled += e => log.Append('k').Append(e.Id);
                var player = new ScriptedPlayer();
                float think = 0f;
                for (int i = 0; i < 30 * 120 && sim.State == BattleState.Running; i++)
                {
                    sim.Step(BattleSimulation.FixedStep);
                    if ((think -= BattleSimulation.FixedStep) > 0f) continue;
                    think = 0.5f;
                    foreach (PickupRuntime p in sim.Pickups) if (p.alive && p.age > 1f) { sim.CollectPickup(p.Id); break; }
                    if (player.DecideEvolution(sim, out DefenderRuntime d, out int o)) Assert.AreEqual(EvolveResult.Ok, sim.TryEvolve(d, o));
                    if (player.Decide(sim, out int slot, out GridCoord cell)) sim.TryDeploy(slot, cell);
                }
                log.Append('|').Append(sim.State).Append(sim.HeartrootHealth).Append(sim.Vitality.Amount);
                return log.ToString();
            }
            string a = Play(11);
            StringAssert.Contains("E", a, "the bot evolved something");
            Assert.AreEqual(a, Play(11));
        }
    }
}
