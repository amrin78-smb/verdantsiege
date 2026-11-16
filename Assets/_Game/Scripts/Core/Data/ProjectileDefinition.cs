using UnityEngine;
using VerdantSiege.Combat;

namespace VerdantSiege.Data
{
    /// <summary>Immutable projectile rules. Travels along its lane toward +column.</summary>
    [CreateAssetMenu(menuName = "Verdant Siege/Projectile", fileName = "Projectile_")]
    public sealed class ProjectileDefinition : ScriptableObject
    {
        public string id = "projectile";
        [Tooltip("Columns per second.")] [Min(0.5f)] public float speed = 6f;
        [Min(0f)] public float damage = 20f;
        public DamageType damageType = DamageType.Physical;
        [Tooltip("How many enemies a single projectile can hit before it is consumed.")]
        [Min(1)] public int pierceCount = 1;
        [Tooltip("Splash radius in columns along the lane (0 = none). Splash hits the same lane only.")]
        [Min(0f)] public float splashRadius = 0f;
        [Range(0f, 1f)] public float splashDamageFraction = 0.5f;
        [Tooltip("Push back in columns on hit.")] [Min(0f)] public float knockback = 0f;
        public StatusEffectDefinition statusEffect;
        public GameObject prefab;
        public Color trailColor = new Color(1f, 0.85f, 0.35f);
    }
}
