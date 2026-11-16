using System;

namespace VerdantSiege.Combat
{
    public enum DamageType
    {
        Physical = 0,
        Piercing = 1, // bypasses armour plating
        Fire = 2,
        Frost = 3,
        Electric = 4,
        Toxic = 5
    }

    public enum StatusKind
    {
        Slow = 0,           // magnitude = speed reduction fraction (0..1), also slows attack rate
        DamageOverTime = 1, // magnitude = damage per tick
        Stun = 2            // no movement/attacks
    }

    public enum StatusStacking
    {
        RefreshDuration = 0, // one instance; re-application resets the timer
        StackIntensity = 1,  // up to maxStacks; each re-application adds a stack and refreshes the timer
        IgnoreWhileActive = 2
    }

    [Serializable]
    public struct DamageModifier
    {
        public DamageType type;
        [UnityEngine.Tooltip("Damage multiplier for this type (0.5 = resists half, 1.5 = weak).")]
        public float multiplier;

        public DamageModifier(DamageType type, float multiplier)
        {
            this.type = type;
            this.multiplier = multiplier;
        }
    }
}
