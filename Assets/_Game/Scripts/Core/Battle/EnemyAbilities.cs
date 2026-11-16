using VerdantSiege.Combat;
using VerdantSiege.Data;

namespace VerdantSiege.Battle
{
    /// <summary>
    /// Stateless enemy ability strategy; all mutable state lives on <see cref="EnemyRuntime"/>.
    /// Tick returns true when the ability controls the enemy this step (the normal walk/bite logic is skipped).
    /// Every ability with a visible effect raises a telegraph event first (see EnemyActivity.Windup).
    /// </summary>
    public interface IEnemyAbility
    {
        void OnSpawn(EnemyRuntime e, BattleSimulation sim);
        bool Tick(EnemyRuntime e, BattleSimulation sim, float dt);
        void OnDeath(EnemyRuntime e, BattleSimulation sim);
    }

    public static class EnemyAbilities
    {
        private static readonly IEnemyAbility None = new NoAbility();
        private static readonly IEnemyAbility Vault = new VaultAbility();
        private static readonly IEnemyAbility Spore = new SporeCloudAbility();
        private static readonly IEnemyAbility Burrow = new BurrowAbility();
        private static readonly IEnemyAbility Howl = new HowlAbility();
        private static readonly IEnemyAbility Spit = new RangedSpitAbility();
        private static readonly IEnemyAbility Mimic = new MimicAbility();

        public static IEnemyAbility For(EnemyAbilityKind kind)
        {
            switch (kind)
            {
                case EnemyAbilityKind.Vault: return Vault;
                case EnemyAbilityKind.SporeCloud: return Spore;
                case EnemyAbilityKind.Burrow: return Burrow;
                case EnemyAbilityKind.Howl: return Howl;
                case EnemyAbilityKind.RangedSpit: return Spit;
                case EnemyAbilityKind.Mimic: return Mimic;
                // Warden aura and Herald command are passive rules evaluated by the simulation each step.
                default: return None;
            }
        }

        internal static bool Stunned(EnemyRuntime e) => e.statuses.Has(StatusKind.Stun);
    }

    public sealed class NoAbility : IEnemyAbility
    {
        public void OnSpawn(EnemyRuntime e, BattleSimulation sim) { }
        public bool Tick(EnemyRuntime e, BattleSimulation sim, float dt) => false;
        public void OnDeath(EnemyRuntime e, BattleSimulation sim) { }
    }

    /// <summary>
    /// Vaultling: on meeting its first defender it crouches (abilityWindup), then leaps over exactly that defender and
    /// lands just past its centre. Only once per life; every later defender blocks it normally.
    /// </summary>
    public sealed class VaultAbility : IEnemyAbility
    {
        public const float LeapSeconds = 0.6f;

        public void OnSpawn(EnemyRuntime e, BattleSimulation sim) { }

        public bool Tick(EnemyRuntime e, BattleSimulation sim, float dt)
        {
            if (e.activity == EnemyActivity.Leaping)
            {
                e.abilityTimer -= dt;
                float k = e.abilityTimer <= 0f ? 1f : 1f - e.abilityTimer / LeapSeconds;
                e.x = e.leapFromX + (e.leapToX - e.leapFromX) * k;
                if (e.abilityTimer <= 0f)
                {
                    e.abilityUsed = true;
                    sim.SetActivity(e, EnemyActivity.Walking);
                }
                return true;
            }
            if (e.abilityUsed) return false;
            DefenderRuntime blocker = sim.FindBlocker(e);
            if (e.activity == EnemyActivity.Windup)
            {
                if (blocker == null || blocker.Id != e.abilityTargetId)
                {
                    sim.SetActivity(e, EnemyActivity.Walking); // the defender vanished: cancel, vault stays available
                    return false;
                }
                if (!EnemyAbilities.Stunned(e)) e.abilityTimer -= dt;
                if (e.abilityTimer > 0f) return true;
                e.leapFromX = e.x;
                e.leapToX = blocker.Coord.column + 0.1f; // past the centre, short of the next cell's contact zone
                e.abilityTimer = LeapSeconds;
                sim.SetActivity(e, EnemyActivity.Leaping);
                sim.RaiseAbilityUsed(e);
                return true;
            }
            if (blocker == null) return false;
            e.abilityTargetId = blocker.Id;
            e.abilityTimer = e.Definition.abilityWindup;
            sim.SetActivity(e, EnemyActivity.Windup);
            sim.RaiseAbilityTelegraph(e);
            return true;
        }

        public void OnDeath(EnemyRuntime e, BattleSimulation sim) { }
    }

    /// <summary>Spore Carrier: when defeated it leaves a spore cloud on its cell that harms the defender there.</summary>
    public sealed class SporeCloudAbility : IEnemyAbility
    {
        public void OnSpawn(EnemyRuntime e, BattleSimulation sim) { }
        public bool Tick(EnemyRuntime e, BattleSimulation sim, float dt) => false;
        public void OnDeath(EnemyRuntime e, BattleSimulation sim) => sim.SpawnCloud(e);
    }

    /// <summary>
    /// Burrower: spawns underground and tunnels abilityRange columns (untargetable except by traps and splash,
    /// never blocked, a dust trail shows where it is), then surfaces over abilityWindup seconds and walks on.
    /// </summary>
    public sealed class BurrowAbility : IEnemyAbility
    {
        public void OnSpawn(EnemyRuntime e, BattleSimulation sim)
        {
            e.underground = true;
            e.activity = EnemyActivity.Burrowed;
            e.leapToX = e.x - e.Definition.abilityRange; // surfacing point
        }

        public bool Tick(EnemyRuntime e, BattleSimulation sim, float dt)
        {
            if (!e.underground) return false;
            if (e.activity == EnemyActivity.Surfacing)
            {
                e.abilityTimer -= dt;
                if (e.abilityTimer <= 0f)
                {
                    e.underground = false;
                    e.abilityUsed = true;
                    sim.SetActivity(e, EnemyActivity.Walking);
                    sim.RaiseAbilityUsed(e);
                }
                return true;
            }
            e.x -= sim.SpeedOf(e) * dt;
            if (e.x <= e.leapToX)
            {
                e.abilityTimer = e.Definition.abilityWindup;
                sim.SetActivity(e, EnemyActivity.Surfacing);
                sim.RaiseAbilityTelegraph(e);
            }
            return true;
        }

        public void OnDeath(EnemyRuntime e, BattleSimulation sim) { }
    }

    /// <summary>Howler: every abilityInterval it stops, inflates its sac (telegraph), then hastes nearby enemies.</summary>
    public sealed class HowlAbility : IEnemyAbility
    {
        public void OnSpawn(EnemyRuntime e, BattleSimulation sim) => e.abilityTimer = e.Definition.abilityInterval * 0.5f;

        public bool Tick(EnemyRuntime e, BattleSimulation sim, float dt)
        {
            EnemyDefinition def = e.Definition;
            if (e.activity == EnemyActivity.Windup)
            {
                if (!EnemyAbilities.Stunned(e)) e.abilityTimer -= dt;
                if (e.abilityTimer > 0f) return true;
                sim.ApplyStatusAround(e, def.abilityRange, def.abilityStatus);
                e.abilityTimer = def.abilityInterval;
                sim.SetActivity(e, EnemyActivity.Walking);
                sim.RaiseAbilityUsed(e);
                return true;
            }
            if (!sim.IsOnField(e) || EnemyAbilities.Stunned(e)) return false;
            e.abilityTimer -= dt;
            if (e.abilityTimer > 0f) return false;
            e.abilityTimer = def.abilityWindup;
            sim.SetActivity(e, EnemyActivity.Windup);
            sim.RaiseAbilityTelegraph(e);
            return true;
        }

        public void OnDeath(EnemyRuntime e, BattleSimulation sim) { }
    }

    /// <summary>
    /// Sapper: stops once a defender is within abilityRange ahead and lobs corrosive spit at it every abilityInterval
    /// (decoys preferred). The canister glows abilityWindup seconds before each throw. Bites if something touches it.
    /// </summary>
    public sealed class RangedSpitAbility : IEnemyAbility
    {
        public void OnSpawn(EnemyRuntime e, BattleSimulation sim) => e.abilityTimer = e.Definition.abilityWindup + 0.4f;

        public bool Tick(EnemyRuntime e, BattleSimulation sim, float dt)
        {
            EnemyDefinition def = e.Definition;
            if (!sim.IsOnField(e) || sim.FindBlocker(e) != null) return false;
            DefenderRuntime target = sim.FindRangedTarget(e, def.abilityRange);
            if (target == null)
            {
                if (e.activity == EnemyActivity.Ranged) sim.SetActivity(e, EnemyActivity.Walking);
                return false;
            }
            sim.SetActivity(e, EnemyActivity.Ranged);
            if (EnemyAbilities.Stunned(e)) return true;
            float before = e.abilityTimer;
            e.abilityTimer -= dt * (1f - e.statuses.SlowFraction());
            if (before > def.abilityWindup && e.abilityTimer <= def.abilityWindup) sim.RaiseAbilityTelegraph(e);
            if (e.abilityTimer > 0f) return true;
            e.abilityTimer = def.abilityInterval;
            sim.FireHostileProjectile(e, target, def.abilityProjectile, def.abilityDamage);
            sim.RaiseAbilityUsed(e);
            return true;
        }

        public void OnDeath(EnemyRuntime e, BattleSimulation sim) { }
    }

    /// <summary>
    /// Mimic: the first time another (non-mimic) enemy is within abilityRange it shimmers for abilityWindup, then
    /// copies that enemy's base speed and current armour, once.
    /// </summary>
    public sealed class MimicAbility : IEnemyAbility
    {
        public void OnSpawn(EnemyRuntime e, BattleSimulation sim) { }

        public bool Tick(EnemyRuntime e, BattleSimulation sim, float dt)
        {
            if (e.abilityUsed) return false;
            EnemyDefinition def = e.Definition;
            if (e.activity == EnemyActivity.Windup)
            {
                if (!EnemyAbilities.Stunned(e)) e.abilityTimer -= dt;
                if (e.abilityTimer > 0f) return true;
                EnemyRuntime src = sim.FindMimicSource(e, def.abilityRange * 1.5f);
                sim.SetActivity(e, EnemyActivity.Walking);
                if (src == null) return false; // lost its model: will try again
                e.abilityUsed = true;
                e.abilityTargetId = src.Id;
                e.speedOverride = src.Definition.moveSpeed;
                if (src.armor > e.armor) e.armor = e.maxArmor = src.armor;
                sim.RaiseAbilityUsed(e);
                return true;
            }
            if (!sim.IsOnField(e) || sim.FindMimicSource(e, def.abilityRange) == null) return false;
            e.abilityTimer = def.abilityWindup;
            sim.SetActivity(e, EnemyActivity.Windup);
            sim.RaiseAbilityTelegraph(e);
            return true;
        }

        public void OnDeath(EnemyRuntime e, BattleSimulation sim) { }
    }
}
