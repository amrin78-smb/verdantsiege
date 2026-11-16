using System.Collections.Generic;
using UnityEngine;
using VerdantSiege.Combat;

namespace VerdantSiege.Data
{
    /// <summary>Immutable enemy content. Health, armour, position and statuses live on EnemyRuntime.</summary>
    [CreateAssetMenu(menuName = "Verdant Siege/Enemy", fileName = "Enemy_")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "enemy";
        public string displayName = "Enemy";
        [TextArea(2, 4)] public string description = "";
        [Tooltip("Plain-language counter hint shown in intel/bestiary.")]
        public string counterHint = "";

        [Header("Durability")]
        [Min(1f)] public float maxHealth = 200f;
        [Tooltip("Armour plating absorbs non-piercing damage before health. 0 = unarmoured.")]
        [Min(0f)] public float armorHealth = 0f;
        public List<DamageModifier> resistances = new List<DamageModifier>();

        [Header("Movement")]
        [Tooltip("Columns per second.")] [Min(0.01f)] public float moveSpeed = 0.22f;
        [Tooltip("Speed multiplier once the armour plating breaks.")] [Min(0.1f)] public float speedMultiplierAfterArmorBreak = 1f;
        [Tooltip("Physical length behind the front, in columns (used for projectile hits).")]
        [Min(0.1f)] public float bodyLength = 0.5f;

        [Header("Attack")]
        [Min(0f)] public float biteDamage = 30f;
        [Min(0.1f)] public float biteInterval = 1f;

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
    }
}
