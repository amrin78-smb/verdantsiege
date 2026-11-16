using NUnit.Framework;
using VerdantSiege.Battle;
using VerdantSiege.Combat;
using VerdantSiege.Data;
using VerdantSiege.Grid;
using static VerdantSiege.Tests.M3Fixtures;

namespace VerdantSiege.Tests
{
    /// <summary>Milestone 3 enemies: vault, spore cloud + burning, burrow, howl, sapper range, glider anti-air, mimic, warden aura, herald rule.</summary>
    public sealed class EnemyAbilityTests
    {
        private TestContent _c;
        [SetUp] public void SetUp() => _c = new TestContent();
        [TearDown] public void TearDown() => _c.Destroy();

        private static void Steps(BattleSimulation sim, int n) { for (int i = 0; i < n; i++) sim.Step(BattleSimulation.FixedStep); }

        [Test]
        public void Vaultling_LeapsOverExactlyOneDefender_ThenIsBlocked()
        {
            EnemyDefinition vault = Enemy(_c, "vaultling", 170f, 0.5f);
            vault.ability = EnemyAbilityKind.Vault; vault.abilityWindup = 0.5f; vault.biteDamage = 10f;
            DefenderDefinition wall = Defender(_c, "wall", DefenderBehaviourKind.Blocker, 1000f);
            BattleSimulation sim = Sandbox(_c, wall);
            DefenderRuntime first = Plant(sim, 0, 2, 5);
            DefenderRuntime second = Plant(sim, 0, 2, 4);
            EnemyRuntime e = Spawn(sim, vault, 2, 6.2f);
            int telegraphs = 0, leaps = 0;
            sim.EnemyAbilityTelegraphed += en => telegraphs++;
            sim.EnemyAbilityUsed += en => leaps++;
            RunUntil(sim, () => e.activity == EnemyActivity.Windup, 3f);
            Assert.AreEqual(1, telegraphs, "crouch telegraph before the leap");
            Assert.AreEqual(EnemyActivity.Windup, e.activity);
            Run(sim, 4f);
            Assert.AreEqual(1, leaps, "vaults once");
            Assert.IsTrue(e.abilityUsed);
            Assert.AreEqual(EnemyActivity.Attacking, e.activity);
            Assert.AreSame(second, sim.FindBlocker(e), "the second defender stops it");
            Assert.AreEqual(first.MaxHealth, first.health, 1e-3f, "the vaulted defender was never bitten");
            Assert.Less(second.health, second.MaxHealth);
            Assert.That(e.x, Is.InRange(4.2f, 4.95f));
        }

        [Test]
        public void SporeCarrier_LeavesCloud_HarmsDefender_AndFireBurnsIt()
        {
            StatusEffectDefinition sick = Status(_c, StatusKind.Slow, 1f, 0.4f);
            EnemyDefinition spore = Enemy(_c, "spore", 100f);
            spore.ability = EnemyAbilityKind.SporeCloud; spore.abilityDuration = 6f; spore.abilityDamage = 20f; spore.abilityStatus = sick;
            DefenderDefinition wall = Defender(_c, "wall", DefenderBehaviourKind.Blocker, 1000f);
            ProjectileDefinition ember = Projectile(_c, 10f, DamageType.Fire, 6f);
            ember.motion = ProjectileMotion.Lobbed; ember.splashRadius = 0.6f; ember.ignitesClouds = true;
            BattleSimulation sim = Sandbox(_c, wall, Shooter(_c, ember, 1f));
            DefenderRuntime w = Plant(sim, 0, 2, 3);
            EnemyRuntime carrier = Spawn(sim, spore, 2, 3.7f);
            sim.DamageEnemy(carrier, 1e6f, DamageType.Physical);
            Assert.IsNotNull(sim.CloudAt(new GridCoord(2, 3)), "cloud on the cell where it fell");
            Run(sim, 1.05f);
            Assert.AreEqual(980f, w.health, 1e-2f, "20 damage per second");
            Assert.IsTrue(w.statuses.Has(StatusKind.Slow), "spores slow the defender");

            int burned = 0;
            sim.CloudRemoved += (c, b) => { if (b) burned++; };
            Plant(sim, 1, 2, 0);
            Spawn(sim, _c.walker, 2, 3.9f); // gives the Ember shooter a target inside the cloud
            Run(sim, 2f);
            Assert.AreEqual(1, burned, "fire burns the cloud away");
            Assert.IsNull(sim.CloudAt(new GridCoord(2, 3)));
        }

        [Test]
        public void Burrower_UntargetableUnderground_TrapHitsIt_ThenSurfaces()
        {
            EnemyDefinition burrower = Enemy(_c, "burrower", 300f, 0.5f);
            burrower.movement = EnemyMovementKind.Underground; burrower.ability = EnemyAbilityKind.Burrow;
            burrower.abilityRange = 3f; burrower.abilityWindup = 0.5f;
            DefenderDefinition trap = Defender(_c, "trap", DefenderBehaviourKind.Trap);
            trap.abilityDamage = 100f; trap.abilityRange = 0.75f; trap.abilityRecharge = 20f; trap.firstAttackDelay = 0.5f;
            BattleSimulation sim = Sandbox(_c, _c.shooter, trap);
            sim.DebugResetCooldowns();
            DefenderRuntime shooter = Plant(sim, 0, 2, 0);
            Plant(sim, 1, 2, 7);
            EnemyRuntime e = sim.SpawnEnemy(burrower, 2);
            int fired = 0, surfaced = 0;
            sim.ProjectileFired += p => fired++;
            sim.EnemyAbilityUsed += en => surfaced++;
            Run(sim, 0.5f);
            Assert.IsTrue(e.underground);
            Assert.IsFalse(sim.HasTargetAhead(shooter), "shots cannot reach a burrowed enemy");
            RunUntil(sim, () => e.health < 300f, 6f);
            Assert.AreEqual(200f, e.health, 1e-3f, "Burrowroot hits it underground");
            Assert.IsTrue(e.underground);
            Assert.AreEqual(0, fired);
            RunUntil(sim, () => !e.underground, 8f);
            Assert.AreEqual(1, surfaced);
            Assert.That(e.x, Is.InRange(e.Definition.moveSpeed * -1f + 6.3f, 6.7f), "surfaces after its tunnelling distance");
            Run(sim, 1f);
            Assert.Greater(fired, 0, "targetable once surfaced");
        }

        [Test]
        public void Howler_HastesNearbyEnemies_ForTheBuffDuration()
        {
            StatusEffectDefinition haste = Status(_c, StatusKind.Haste, 2f, 0.5f);
            EnemyDefinition howler = Enemy(_c, "howler", 280f, 0.2f);
            howler.ability = EnemyAbilityKind.Howl; howler.abilityInterval = 4f; howler.abilityWindup = 0.5f;
            howler.abilityRange = 2f; howler.abilityStatus = haste;
            BattleSimulation sim = Sandbox(_c, _c.shooter);
            EnemyRuntime h = Spawn(sim, howler, 2, 7f);
            EnemyRuntime buddy = Spawn(sim, _c.walker, 3, 7.5f);
            EnemyRuntime far = Spawn(sim, _c.walker, 0, 7f);
            int howls = 0, telegraphs = 0;
            sim.EnemyAbilityTelegraphed += en => telegraphs++;
            sim.EnemyAbilityUsed += en => howls++;
            RunUntil(sim, () => telegraphs > 0, 5f);
            Assert.AreEqual(EnemyActivity.Windup, h.activity, "stops to inflate its sac");
            Assert.AreEqual(0, howls);
            RunUntil(sim, () => howls > 0, 2f);
            Assert.AreEqual(0.375f, buddy.CurrentSpeed, 1e-4f, "+50% speed");
            Assert.AreEqual(0.25f, far.CurrentSpeed, 1e-4f, "two lanes away: unaffected");
            Run(sim, 1.5f);
            Assert.AreEqual(0.375f, buddy.CurrentSpeed, 1e-4f, "still hasted within the duration");
            Run(sim, 0.6f);
            Assert.AreEqual(0.25f, buddy.CurrentSpeed, 1e-4f, "haste wears off");
        }

        [Test]
        public void Sapper_StopsAtRange_AndSpits_WithoutBiting()
        {
            StatusEffectDefinition corrosion = Status(_c, StatusKind.DamageOverTime, 4f, 5f);
            ProjectileDefinition spit = Projectile(_c, 40f, DamageType.Toxic, 6f);
            spit.statusEffect = corrosion;
            EnemyDefinition sapper = Enemy(_c, "sapper", 220f, 1f);
            sapper.ability = EnemyAbilityKind.RangedSpit; sapper.abilityRange = 2.5f; sapper.abilityInterval = 3f;
            sapper.abilityWindup = 0.5f; sapper.abilityDamage = 40f; sapper.abilityProjectile = spit;
            DefenderDefinition wall = Defender(_c, "wall", DefenderBehaviourKind.Blocker, 1000f);
            BattleSimulation sim = Sandbox(_c, wall);
            DefenderRuntime w = Plant(sim, 0, 2, 3);
            EnemyRuntime e = sim.SpawnEnemy(sapper, 2);
            int bites = 0, spits = 0;
            sim.EnemyBit += (en, d) => bites++;
            sim.EnemyAbilityUsed += en => spits++;
            Run(sim, 6f);
            Assert.AreEqual(EnemyActivity.Ranged, e.activity);
            Assert.That(e.x, Is.InRange(5.9f, 6.01f), "halts 2.5 columns from the defender");
            Assert.GreaterOrEqual(spits, 1);
            Assert.Less(w.health, 1000f - 39f, "spit damages the defender");
            Assert.IsTrue(w.statuses.Has(StatusKind.DamageOverTime), "corrosion applied");
            Assert.AreEqual(0, bites);
        }

        [Test]
        public void Glider_IgnoresBlockers_AndOnlyAntiAirHitsIt()
        {
            EnemyDefinition glider = Enemy(_c, "glider", 100f, 1f);
            glider.movement = EnemyMovementKind.Flying;
            ProjectileDefinition thorn = Projectile(_c, 40f, DamageType.Piercing, 9f);
            thorn.canHitAir = true;
            DefenderDefinition wall = Defender(_c, "wall", DefenderBehaviourKind.Blocker, 1000f);
            BattleSimulation sim = Sandbox(_c, _c.shooter, wall, Shooter(_c, thorn, 1f));
            sim.DebugResetCooldowns();
            DefenderRuntime ground = Plant(sim, 0, 2, 0);
            Plant(sim, 1, 2, 4);
            EnemyRuntime g = sim.SpawnEnemy(glider, 2);
            int fired = 0;
            sim.ProjectileFired += p => fired++;
            bool everAttacked = false;
            sim.EnemyActivityChanged += (en, a) => { if (en == g && a == EnemyActivity.Attacking) everAttacked = true; };
            Run(sim, 4f);
            Assert.IsFalse(sim.HasTargetAhead(ground), "ground shooters ignore flyers");
            Run(sim, 7f);
            Assert.AreEqual(0, fired);
            Assert.IsFalse(everAttacked, "flies over the wall");
            Assert.AreEqual(2, sim.HeartrootHealth, "breach costs 1");

            Plant(sim, 2, 3, 0);
            EnemyRuntime g2 = Spawn(sim, glider, 3, 6f);
            Run(sim, 1.5f);
            Assert.Less(g2.health, 100f, "anti-air (canHitAir) projectiles hit it");
        }

        [Test]
        public void Mimic_CopiesSpeedAndArmour_Once()
        {
            EnemyDefinition mimic = Enemy(_c, "mimic", 220f, 0.2f);
            mimic.ability = EnemyAbilityKind.Mimic; mimic.abilityRange = 1.5f; mimic.abilityWindup = 0.5f;
            BattleSimulation sim = Sandbox(_c, _c.shooter);
            EnemyRuntime brute = Spawn(sim, _c.armored, 2, 7f);
            EnemyRuntime m = Spawn(sim, mimic, 2, 7.8f);
            int uses = 0, telegraphs = 0;
            sim.EnemyAbilityUsed += en => uses++;
            sim.EnemyAbilityTelegraphed += en => telegraphs++;
            sim.Step(BattleSimulation.FixedStep);
            Assert.AreEqual(1, telegraphs, "shimmer telegraph");
            Run(sim, 0.6f);
            Assert.AreEqual(1, uses);
            Assert.AreEqual(_c.armored.moveSpeed, m.speedOverride, 1e-5f);
            Assert.AreEqual(400f, m.armor, 1e-3f, "copies the plating");
            Assert.AreEqual(brute.Id, m.abilityTargetId);
            Spawn(sim, _c.runner, 1, m.x - 0.2f);
            Run(sim, 2f);
            Assert.AreEqual(1, uses, "only once");
            Assert.AreEqual(_c.armored.moveSpeed, m.speedOverride, 1e-5f);
        }

        [Test]
        public void Warden_AuraReducesDamageToNearbyAllies_NotItself()
        {
            EnemyDefinition warden = Enemy(_c, "warden", 1000f, 0.16f);
            warden.elite = true; warden.ability = EnemyAbilityKind.WardenAura; warden.abilityRange = 1.5f; warden.abilityMagnitude = 0.4f;
            BattleSimulation sim = Sandbox(_c, _c.shooter);
            EnemyRuntime w = Spawn(sim, warden, 2, 7f);
            EnemyRuntime near = Spawn(sim, _c.walker, 3, 7.5f);
            EnemyRuntime far = Spawn(sim, _c.walker, 0, 7f);
            sim.Step(BattleSimulation.FixedStep);
            sim.DamageEnemy(near, 100f, DamageType.Physical);
            sim.DamageEnemy(far, 100f, DamageType.Physical);
            sim.DamageEnemy(w, 100f, DamageType.Physical);
            Assert.AreEqual(140f, near.health, 1e-3f, "40% reduction inside the aura");
            Assert.AreEqual(100f, far.health, 1e-3f);
            Assert.AreEqual(900f, w.health, 1e-3f, "the Warden does not shield itself");
            sim.DamageEnemy(w, 1e6f, DamageType.Physical);
            sim.Step(BattleSimulation.FixedStep);
            sim.DamageEnemy(near, 40f, DamageType.Physical);
            Assert.AreEqual(100f, near.health, 1e-3f, "aura ends with the Warden");
        }

        [Test]
        public void BlightHerald_SpeedsUpEveryEnemy_WhileAlive()
        {
            EnemyDefinition herald = Enemy(_c, "herald", 500f, 0.18f);
            herald.elite = true; herald.ability = EnemyAbilityKind.HeraldCommand; herald.abilityMagnitude = 0.15f;
            BattleSimulation sim = Sandbox(_c, _c.shooter);
            EnemyRuntime walker = Spawn(sim, _c.walker, 0, 7f);
            EnemyRuntime h = Spawn(sim, herald, 4, 8f);
            int on = 0, off = 0;
            sim.HeraldCommandChanged += active => { if (active) on++; else off++; };
            sim.Step(BattleSimulation.FixedStep);
            Assert.AreEqual(1, on);
            float x0 = walker.x, hx0 = h.x;
            Steps(sim, 30);
            Assert.AreEqual(0.25f * 1.15f, x0 - walker.x, 1e-3f, "+15% while the Herald lives");
            Assert.AreEqual(0.18f, hx0 - h.x, 1e-3f, "the Herald itself is not hastened");
            sim.DamageEnemy(h, 1e6f, DamageType.Physical);
            sim.Step(BattleSimulation.FixedStep);
            Assert.AreEqual(1, off);
            x0 = walker.x;
            Steps(sim, 30);
            Assert.AreEqual(0.25f, x0 - walker.x, 1e-3f, "normal speed after it falls");
        }
    }
}
