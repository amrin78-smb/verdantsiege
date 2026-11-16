using UnityEngine;
using VerdantSiege.Combat;

namespace VerdantSiege.Data
{
    public enum ProjectileMotion
    {
        Straight = 0, // travels along its lane and hits what it sweeps through
        Lobbed = 1    // arcs over everything and lands on the target's position (area impact)
    }

    /// <summary>Immutable projectile rules. Defender projectiles travel toward +column; hostile ones toward the Heartroot.</summary>
    [CreateAssetMenu(menuName = "Verdant Siege/Projectile", fileName = "Projectile_")]
    public sealed class ProjectileDefinition : ScriptableObject
    {
        public string id = "projectile";
        public ProjectileMotion motion = ProjectileMotion.Straight;
        [Tooltip("Columns per second.")] [Min(0.5f)] public float speed = 6f;
        [Min(0f)] public float damage = 20f;
        public DamageType damageType = DamageType.Physical;
        [Tooltip("How many enemies a single projectile can hit before it is consumed.")]
        [Min(1)] public int pierceCount = 1;
        [Tooltip("Splash radius in columns along the lane (0 = none). Splash hits the same lane only, including burrowed enemies.")]
        [Min(0f)] public float splashRadius = 0f;
        [Range(0f, 1f)] public float splashDamageFraction = 0.5f;
        [Tooltip("Push back in columns on hit.")] [Min(0f)] public float knockback = 0f;
        public StatusEffectDefinition statusEffect;
        [Tooltip("Can hit flying enemies (Gliders).")]
        public bool canHitAir;

        [Header("Ricochet")]
        [Tooltip("Extra targets after the first hit; each bounce seeks the nearest enemy in the same or an adjacent lane.")]
        [Min(0)] public int bounces = 0;
        [Tooltip("Damage multiplier applied per bounce.")]
        [Range(0.1f, 1f)] public float bounceFalloff = 0.75f;
        [Tooltip("Max distance (columns) to the next bounce target.")]
        [Min(0.5f)] public float bounceRange = 2.2f;

        [Header("Freeze (Frost Fern)")]
        [Tooltip("After this many hits from this projectile kind the target is frozen (0 = never).")]
        [Min(0)] public int freezeAfterHits = 0;
        public StatusEffectDefinition freezeStatus;

        [Header("Evolution mechanics")]
        [Tooltip("Damage multiplier against enemies that are slowed or frozen (Glacial Shards).")]
        [Min(1f)] public float damageMultiplierVsChilled = 1f;
        [Tooltip("Lobbed impacts also burst in this many lanes on each side (Wildfire).")]
        [Range(0, 2)] public int splashSideLanes = 0;

        [Header("Fire")]
        [Tooltip("Burns away spore clouds where it hits or lands.")]
        public bool ignitesClouds;

        [Header("Presentation")]
        public GameObject prefab;
        public Color trailColor = new Color(1f, 0.85f, 0.35f);
    }
}
