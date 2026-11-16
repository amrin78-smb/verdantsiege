using UnityEngine;

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
        Generator = 2    // produces Vitality pickups
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

        [Header("Lane Shooter")]
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

        [Header("Presentation")]
        public GameObject prefab;
        public Sprite icon;
    }
}
