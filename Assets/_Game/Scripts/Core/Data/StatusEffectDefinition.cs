using UnityEngine;
using VerdantSiege.Combat;

namespace VerdantSiege.Data
{
    /// <summary>Immutable status effect rules. Runtime timers/stacks live in <see cref="Battle.StatusEffectSet"/>.</summary>
    [CreateAssetMenu(menuName = "Verdant Siege/Status Effect", fileName = "Status_")]
    public sealed class StatusEffectDefinition : ScriptableObject
    {
        public string id = "status";
        public string displayName = "Status";
        public StatusKind kind = StatusKind.Slow;
        [Min(0.05f)] public float duration = 3f;
        [Tooltip("Slow: fraction (0..0.9). DamageOverTime: damage per tick. Stun: unused.")]
        public float magnitude = 0.4f;
        [Min(0.05f)] public float tickInterval = 0.5f;
        public DamageType damageType = DamageType.Frost;
        public StatusStacking stacking = StatusStacking.RefreshDuration;
        [Min(1)] public int maxStacks = 1;
        public Color tint = new Color(0.55f, 0.85f, 1f);
    }
}
