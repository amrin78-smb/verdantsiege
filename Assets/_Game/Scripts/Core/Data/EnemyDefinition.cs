using System.Collections.Generic;
using UnityEngine;
using VerdantSiege.Combat;

namespace VerdantSiege.Data
{
    /// <summary>Immutable enemy content. Health, armour, position, statuses and ability timers live on EnemyRuntime.</summary>
    [CreateAssetMenu(menuName = "Verdant Siege/Enemy", fileName = "Enemy_")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "enemy";
        public string displayName = "Enemy";
        [TextArea(2, 4)] public string description = "";
        [Tooltip("Plain-language counter hint shown in intel/bestiary.")]
        public string counterHint = "";
        public EnemyTags tags = EnemyTags.None;
        [Tooltip("Elite: large health bar, arrival banner and sound.")]
        public bool elite;

        [Header("Durability")]
        [Min(1f)] public float maxHealth = 200f;
        [Tooltip("Armour plating absorbs non-piercing damage before health. 0 = unarmoured.")]
        [Min(0f)] public float armorHealth = 0f;
        public List<DamageModifier> resistances = new List<DamageModifier>();
        [Tooltip("Snapjaw Vine can swallow it whole. If false, the snap deals its big damage instead.")]
        public bool devourable = true;

        [Header("Movement")]
        public EnemyMovementKind movement = EnemyMovementKind.Ground;
        [Tooltip("Columns per second.")] [Min(0.01f)] public float moveSpeed = 0.22f;
        [Tooltip("Speed multiplier once the armour plating breaks.")] [Min(0.1f)] public float speedMultiplierAfterArmorBreak = 1f;
        [Tooltip("Physical length behind the front, in columns (used for projectile hits).")]
        [Min(0.1f)] public float bodyLength = 0.5f;

        [Header("Attack")]
        [Min(0f)] public float biteDamage = 30f;
        [Min(0.1f)] public float biteInterval = 1f;

        [Header("Ability (see EnemyAbilityKind)")]
        public EnemyAbilityKind ability = EnemyAbilityKind.None;
        [Tooltip("Telegraph before the ability fires (crouch, sac inflate, shimmer, surfacing).")]
        [Min(0f)] public float abilityWindup = 0.8f;
        [Tooltip("Seconds between repeated uses (Howl, Spit).")]
        [Min(0.1f)] public float abilityInterval = 8f;
        [Tooltip("Columns: spit range, aura/howl/mimic radius, burrow distance.")]
        [Min(0f)] public float abilityRange = 2f;
        [Tooltip("Warden damage reduction or Herald speed bonus (fraction).")]
        [Range(0f, 1f)] public float abilityMagnitude = 0.25f;
        [Tooltip("Seconds a spore cloud lasts.")]
        [Min(0f)] public float abilityDuration = 4f;
        [Tooltip("Status the ability applies: Howl haste on enemies, spit corrosion or spore slow on defenders.")]
        public StatusEffectDefinition abilityStatus;
        [Tooltip("Spore cloud damage per second to the defender in the cell; spit damage per glob.")]
        [Min(0f)] public float abilityDamage = 20f;
        [Tooltip("Hostile projectile (Sapper spit).")]
        public ProjectileDefinition abilityProjectile;

        [Header("Objective")]
        [Tooltip("Heartroot damage when this enemy reaches the end of its lane.")]
        [Min(1)] public int breachDamage = 1;

        [Header("Rewards")]
        [Tooltip("Vitality pickup dropped when defeated (0 = none). Not dropped on breach.")]
        [Min(0)] public int vitalityDrop = 10;

        [Header("Encounter Director")]
        [Min(1)] public int threatCost = 1;

        [Header("Presentation")]
        public GameObject prefab;
        public Sprite icon;
        public Color accentColor = Color.white;

        public bool IsFlying => movement == EnemyMovementKind.Flying;
        public bool HasTag(EnemyTags t) => (tags & t) != 0;
    }
}
