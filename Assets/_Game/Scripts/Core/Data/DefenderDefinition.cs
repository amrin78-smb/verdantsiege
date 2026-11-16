using UnityEngine;
using VerdantSiege.Combat;

namespace VerdantSiege.Data
{
    public enum DefenderRole
    {
        Artillery, Guardian, Generator, Control, Assassin, Support, Trap, Specialist
    }

    /// <summary>Which runtime behaviour drives the defender. New behaviours are added in Battle/DefenderBehaviours.</summary>
    public enum DefenderBehaviourKind
    {
        LaneShooter = 0, // fires projectiles down its lane at enemies ahead
        Blocker = 1,     // no attack; absorbs damage
        Generator = 2,   // produces Vitality pickups
        Ambusher = 3,    // Snapjaw: snaps the first enemy within reach, then recovers
        Trap = 4,        // Burrowroot: hidden; erupts when an enemy steps on its cell, then rearms
        Mender = 5,      // Mender Moss: heals and cleanses defenders around it
        ChainCaster = 6, // Storm Orchid: instant lightning that chains across lanes
        Decoy = 7        // Mirrorleaf: lures enemies from adjacent lanes, reflects hostile projectiles
    }

    /// <summary>Immutable defender content. Health, cooldowns and timers live on DefenderRuntime/deck slots.</summary>
    [CreateAssetMenu(menuName = "Verdant Siege/Defender", fileName = "Defender_")]
    public sealed class DefenderDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "defender";
        public string displayName = "Defender";
        public DefenderRole role = DefenderRole.Artillery;
        [TextArea(2, 4)] public string description = "";
        [Tooltip("Short plain-language strengths, e.g. 'Reliable lane damage'.")]
        public string strengths = "";
        public string weaknesses = "";
        public Color accentColor = Color.white;

        [Header("Deployment")]
        [Min(0)] public int cost = 100;
        [Tooltip("Seconds before the card can be planted again.")] [Min(0f)] public float cooldown = 5f;
        [Tooltip("Card cooldown already running when the battle starts (seconds).")] [Min(0f)] public float startingCooldown = 0f;

        [Header("Durability")]
        [Min(1f)] public float maxHealth = 300f;

        [Header("Behaviour")]
        public DefenderBehaviourKind behaviour = DefenderBehaviourKind.LaneShooter;

        [Header("Lane Shooter / Chain Caster")]
        public ProjectileDefinition projectile;
        [Min(0.05f)] public float attackInterval = 1.4f;
        [Tooltip("Delay before the first shot after planting.")] [Min(0f)] public float firstAttackDelay = 0.6f;
        [Tooltip("Range in columns ahead of the defender (0 = whole lane).")] [Min(0f)] public float range = 0f;

        [Header("Generator")]
        [Min(0.5f)] public float generateInterval = 12f;
        [Min(0f)] public float generateFirstDelay = 5f;
        [Min(1)] public int generateAmount = 25;
        [Tooltip("Every Nth pulse is a bonus burst (0 = never).")] [Min(0)] public int burstEvery = 4;
        [Min(1)] public int burstAmount = 50;

        [Header("Ability (Ambusher, Trap, Mender, Chain Caster, Decoy)")]
        [Tooltip("Ambusher snap / trap burst / chain bolt damage. Mender: health restored per pulse.")]
        [Min(0f)] public float abilityDamage = 0f;
        public DamageType abilityDamageType = DamageType.Physical;
        [Tooltip("Ambusher reach ahead, trap blast half-width, mender radius (cells), chain jump distance, decoy lure reach (columns).")]
        [Min(0f)] public float abilityRange = 1.2f;
        [Tooltip("Ambusher recovery, trap rearm, mender pulse interval (seconds).")]
        [Min(0.1f)] public float abilityRecharge = 10f;
        [Tooltip("Ambusher/trap telegraph before the strike (seconds).")]
        [Min(0f)] public float abilityWindup = 0.3f;
        [Tooltip("Chain Caster: total targets. Mender: statuses cleansed per defender per pulse.")]
        [Min(0)] public int abilityCount = 1;
        [Tooltip("Chain Caster damage multiplier per jump.")]
        [Range(0.1f, 1f)] public float abilityFalloff = 0.8f;
        [Tooltip("Status applied by the ability (Storm Orchid: stun on Mechanical enemies).")]
        public StatusEffectDefinition abilityStatus;
        [Tooltip("Hits flying enemies (Chain Caster). Shooters use their projectile's canHitAir.")]
        public bool canHitAir;

        [Header("Decoy")]
        [Tooltip("Hostile projectiles that hit this defender bounce back at the shooter.")]
        public bool reflectsProjectiles;
        [Min(0f)] public float reflectMultiplier = 1.5f;
        [Tooltip("Enemies prefer this defender (ranged targeting, lure from adjacent lanes).")]
        public bool taunts;

        [Header("Presentation")]
        public GameObject prefab;
        public Sprite icon;

        /// <summary>True if this defender's attacks reach flying enemies.</summary>
        public bool HitsAir => projectile != null && behaviour == DefenderBehaviourKind.LaneShooter ? projectile.canHitAir : canHitAir;
    }
}
