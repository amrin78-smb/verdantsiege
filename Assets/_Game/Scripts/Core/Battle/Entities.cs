using VerdantSiege.Combat;
using VerdantSiege.Data;
using VerdantSiege.Grid;
using VerdantSiege.Utilities;

namespace VerdantSiege.Battle
{
    /// <summary>Anything that can receive damage.</summary>
    public interface IDamageable
    {
        int Id { get; }
        bool IsAlive { get; }
        float Health { get; }
        float MaxHealth { get; }
    }

    /// <summary>Anything that sits in a lane at a position (column units).</summary>
    public interface ILaneTargetable : IDamageable
    {
        int Lane { get; }
        float X { get; }
    }

    /// <summary>Mutable state of one planted defender.</summary>
    public sealed class DefenderRuntime : IDamageable, IPoolable
    {
        public int Id { get; set; }
        public DefenderDefinition Definition;
        public GridCoord Coord;
        public float health;
        public float attackTimer;
        public float generateTimer;
        public int generatePulses;
        public float age;
        /// <summary>Card slot the defender came from (for undo refunds).</summary>
        public int slotIndex;
        public bool alive;
        /// <summary>Ambusher/trap state (Ready = armed).</summary>
        public DefenderAbilityState abilityState;
        public float abilityTimer;
        public int abilityTargetId;
        /// <summary>Harmful effects from spore clouds and Sapper spit (Mender Moss cleanses these).</summary>
        public readonly StatusEffectSet statuses = new StatusEffectSet();

        // ---- evolution (see BattleSimulation.Evolution)
        /// <summary>Evolution XP earned this battle (units depend on Definition.xpSource).</summary>
        public float xp;
        /// <summary>Chosen branch (null = not evolved).</summary>
        public EvolutionDefinition evolution;
        /// <summary>The ready notification has been raised.</summary>
        public bool evolutionAnnounced;
        /// <summary>Strongest aura action-speed bonus from evolved neighbours this step.</summary>
        public float auraSpeedBonus;
        /// <summary>Evolved aura source: number of linked neighbours this step, and its Vitality trickle timer.</summary>
        public int auraLinks;
        public float auraTimer;
        /// <summary>Capacitor (evolved generator): Vitality stored and pulses stored.</summary>
        public int storedVitality;
        public int storedPulses;

        /// <summary>XP needed this battle (definition threshold x research growth modifier; set when planted).</summary>
        public float xpNeeded;

        public bool IsEvolved => evolution != null;
        public bool CanEvolveEver => Definition != null && Definition.CanEvolve && evolution == null;
        /// <summary>Effective XP threshold (falls back to the definition's when not set by the simulation).</summary>
        public float XpThreshold => xpNeeded > 0f ? xpNeeded : (Definition != null ? Definition.evolutionXp : 0f);
        /// <summary>Alive, not yet evolved and over the XP threshold.</summary>
        public bool EvolutionReady => alive && CanEvolveEver && xp >= XpThreshold;
        public float XpFraction => Definition == null || XpThreshold <= 0f ? 0f : (evolution != null ? 1f : System.Math.Min(1f, xp / XpThreshold));

        public bool IsAlive => alive;
        public float Health => health;
        public float MaxHealth => Definition != null ? Definition.maxHealth : 1f;
        public float HealthFraction => MaxHealth > 0f ? health / MaxHealth : 0f;
        /// <summary>An armed trap is hidden: enemies walk onto it instead of stopping to eat it.</summary>
        public bool IsArmedTrap => Definition != null && Definition.behaviour == DefenderBehaviourKind.Trap && abilityState != DefenderAbilityState.Recovering;

        public void ResetForPool()
        {
            Id = 0; Definition = null; Coord = GridCoord.Invalid; health = 0f; attackTimer = 0f;
            generateTimer = 0f; generatePulses = 0; age = 0f; slotIndex = -1; alive = false;
            abilityState = DefenderAbilityState.Ready; abilityTimer = 0f; abilityTargetId = 0;
            xp = 0f; xpNeeded = 0f; evolution = null; evolutionAnnounced = false; auraSpeedBonus = 0f; auraLinks = 0; auraTimer = 0f;
            storedVitality = 0; storedPulses = 0;
            statuses.Clear();
        }
    }

    /// <summary>Mutable state of one enemy. X is the front of the enemy in column units (decreasing as it advances).</summary>
    public sealed class EnemyRuntime : ILaneTargetable, IPoolable
    {
        public const float LaneSwitchSeconds = 0.6f;

        public int Id { get; set; }
        public EnemyDefinition Definition;
        public int lane;
        public float x;
        public float prevX;
        public float health;
        public float armor;
        /// <summary>Armour capacity (definition value, or what a Mimic copied).</summary>
        public float maxArmor;
        public float biteTimer;
        public EnemyActivity activity;
        public int waveIndex;
        public bool alive;
        public float hitFlash; // presentation hint, seconds remaining
        public readonly StatusEffectSet statuses = new StatusEffectSet();

        // ---- ability state (see EnemyAbilities)
        public float abilityTimer;
        public bool abilityUsed;
        public int abilityTargetId;
        /// <summary>Vault: start and landing x of the leap.</summary>
        public float leapFromX, leapToX;
        /// <summary>Mimic: copied base speed (0 = none).</summary>
        public float speedOverride;
        public bool underground;
        /// <summary>Frost hits counted toward the next freeze.</summary>
        public int frostHits;
        /// <summary>Damage reduction from Warden auras this step (0..1).</summary>
        public float wardReduction;
        /// <summary>Lure (Mirrorleaf): lane change in progress toward targetLane.</summary>
        public float laneSwitchTimer;
        public int targetLane;
        public bool lured;

        public int Lane => lane;
        public float X => x;
        public bool IsAlive => alive;
        public float Health => health;
        public float MaxHealth => Definition != null ? Definition.maxHealth : 1f;
        public bool HasArmor => armor > 0f;
        public float BackX => x + (Definition != null ? Definition.bodyLength : 0.5f);
        public bool IsFlying => Definition != null && Definition.movement == EnemyMovementKind.Flying;
        public bool IsElite => Definition != null && Definition.elite;
        /// <summary>Lane position for presentation while switching lanes (fractional).</summary>
        public float VisualLane => laneSwitchTimer > 0f ? targetLane + (lane - targetLane) * (laneSwitchTimer / LaneSwitchSeconds) : lane;

        /// <summary>Base speed after statuses, armour break, mimicry and haste (rule-wide modifiers are applied by the simulation).</summary>
        public float CurrentSpeed
        {
            get
            {
                if (Definition == null) return 0f;
                if (statuses.Has(StatusKind.Stun)) return 0f;
                float s = speedOverride > 0f ? speedOverride : Definition.moveSpeed;
                if (maxArmor > 0f && armor <= 0f && speedOverride <= 0f) s *= Definition.speedMultiplierAfterArmorBreak;
                return s * (1f - statuses.SlowFraction()) * (1f + statuses.HasteFraction());
            }
        }

        public void ResetForPool()
        {
            Id = 0; Definition = null; lane = 0; x = 0f; prevX = 0f; health = 0f; armor = 0f; maxArmor = 0f; biteTimer = 0f;
            activity = EnemyActivity.Walking; waveIndex = -1; alive = false; hitFlash = 0f;
            abilityTimer = 0f; abilityUsed = false; abilityTargetId = 0; leapFromX = leapToX = 0f; speedOverride = 0f;
            underground = false; frostHits = 0; wardReduction = 0f; laneSwitchTimer = 0f; targetLane = 0; lured = false;
            statuses.Clear();
        }
    }

    /// <summary>Mutable state of one projectile in flight.</summary>
    public sealed class ProjectileRuntime : IPoolable
    {
        public int Id;
        public ProjectileDefinition Definition;
        public int lane;
        public float x;
        public float prevX;
        public float startX;
        public int hitsRemaining;
        public int sourceDefenderId;
        /// <summary>Firing defender (validated against sourceDefenderId before use: runtimes are pooled).</summary>
        public DefenderRuntime sourceDefender;
        public bool alive;
        /// <summary>Current damage (bounce falloff and reflection change it).</summary>
        public float damage;
        /// <summary>Fired by an enemy toward the Heartroot side.</summary>
        public bool hostile;
        public int sourceEnemyId;
        /// <summary>Hostile: target cell column. Lobbed: landing x.</summary>
        public int targetColumn;
        public float targetX;
        /// <summary>Homing (ricochet hop or reflected spit): enemy being chased (0 = none).</summary>
        public int homingEnemyId;
        public int bouncesRemaining;
        public bool reflected;
        /// <summary>Lane the projectile came from (presentation blends lane changes over a hop).</summary>
        public int fromLane;
        public float hopFromX;
        // Pierce bookkeeping: ids already hit (small fixed buffer; pierce/bounce counts are small).
        public readonly int[] hitIds = new int[8];
        public int hitCount;

        public bool AlreadyHit(int enemyId)
        {
            for (int i = 0; i < hitCount; i++) if (hitIds[i] == enemyId) return true;
            return false;
        }

        public void MarkHit(int enemyId)
        {
            if (hitCount < hitIds.Length) hitIds[hitCount++] = enemyId;
        }

        public void ResetForPool()
        {
            Id = 0; Definition = null; lane = 0; x = 0f; prevX = 0f; startX = 0f; hitsRemaining = 0;
            sourceDefenderId = 0; sourceDefender = null; alive = false; hitCount = 0; damage = 0f; hostile = false; sourceEnemyId = 0;
            targetColumn = 0; targetX = 0f; homingEnemyId = 0; bouncesRemaining = 0; reflected = false; fromLane = 0; hopFromX = 0f;
        }
    }

    /// <summary>A collectable Vitality pickup resting at (x, laneFloat).</summary>
    public sealed class PickupRuntime : IPoolable
    {
        public int Id;
        public int amount;
        public float x;           // column units
        public float laneFloat;   // lane units (may sit between lanes)
        public float remaining;   // lifetime
        public float lifetime;
        public float age;
        public PickupSource source;
        public bool alive;

        public void ResetForPool()
        {
            Id = 0; amount = 0; x = 0f; laneFloat = 0f; remaining = 0f; lifetime = 0f; age = 0f;
            source = PickupSource.Sky; alive = false;
        }
    }

    /// <summary>A lingering spore cloud on one cell (left by a Spore Carrier). Harms the defender in it; fire burns it away.</summary>
    public sealed class CloudRuntime : IPoolable
    {
        public int Id;
        public GridCoord coord;
        public float remaining;
        public float duration;
        public float damagePerSecond;
        public StatusEffectDefinition status;
        public bool alive;

        public void ResetForPool()
        {
            Id = 0; coord = GridCoord.Invalid; remaining = 0f; duration = 0f; damagePerSecond = 0f; status = null; alive = false;
        }
    }

    /// <summary>One card in the battle deck: definition plus its cooldown.</summary>
    public sealed class DeckSlot
    {
        public readonly DefenderDefinition Definition;
        public float cooldownRemaining;

        public DeckSlot(DefenderDefinition def)
        {
            Definition = def;
            cooldownRemaining = def != null ? def.startingCooldown : 0f;
        }

        public bool Ready => cooldownRemaining <= 0f;
        public float CooldownFraction => Definition == null || Definition.cooldown <= 0f ? 0f
            : (cooldownRemaining <= 0f ? 0f : cooldownRemaining / System.Math.Max(Definition.cooldown, Definition.startingCooldown));
    }
}
