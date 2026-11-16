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

        public bool IsAlive => alive;
        public float Health => health;
        public float MaxHealth => Definition != null ? Definition.maxHealth : 1f;
        public float HealthFraction => MaxHealth > 0f ? health / MaxHealth : 0f;

        public void ResetForPool()
        {
            Id = 0; Definition = null; Coord = GridCoord.Invalid; health = 0f; attackTimer = 0f;
            generateTimer = 0f; generatePulses = 0; age = 0f; slotIndex = -1; alive = false;
        }
    }

    /// <summary>Mutable state of one enemy. X is the front of the enemy in column units (decreasing as it advances).</summary>
    public sealed class EnemyRuntime : ILaneTargetable, IPoolable
    {
        public int Id { get; set; }
        public EnemyDefinition Definition;
        public int lane;
        public float x;
        public float prevX;
        public float health;
        public float armor;
        public float biteTimer;
        public EnemyActivity activity;
        public int waveIndex;
        public bool alive;
        public float hitFlash; // presentation hint, seconds remaining
        public readonly StatusEffectSet statuses = new StatusEffectSet();

        public int Lane => lane;
        public float X => x;
        public bool IsAlive => alive;
        public float Health => health;
        public float MaxHealth => Definition != null ? Definition.maxHealth : 1f;
        public bool HasArmor => armor > 0f;
        public float BackX => x + (Definition != null ? Definition.bodyLength : 0.5f);

        public float CurrentSpeed
        {
            get
            {
                if (Definition == null) return 0f;
                if (statuses.Has(StatusKind.Stun)) return 0f;
                float s = Definition.moveSpeed;
                if (Definition.armorHealth > 0f && armor <= 0f) s *= Definition.speedMultiplierAfterArmorBreak;
                return s * (1f - statuses.SlowFraction());
            }
        }

        public void ResetForPool()
        {
            Id = 0; Definition = null; lane = 0; x = 0f; prevX = 0f; health = 0f; armor = 0f; biteTimer = 0f;
            activity = EnemyActivity.Walking; waveIndex = -1; alive = false; hitFlash = 0f;
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
        public bool alive;
        // Pierce bookkeeping: ids already hit (small fixed buffer; pierce counts are small).
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
            sourceDefenderId = 0; alive = false; hitCount = 0;
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
