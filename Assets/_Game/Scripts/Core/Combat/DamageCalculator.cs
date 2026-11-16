using System.Collections.Generic;

namespace VerdantSiege.Combat
{
    /// <summary>Outcome of one damage application, for feedback (numbers, armour break VFX).</summary>
    public struct DamageResult
    {
        public float toArmor;
        public float toHealth;
        public bool armorBroken;
        public bool killed;

        public float Total => toArmor + toHealth;
    }

    /// <summary>Pure damage rules shared by every damageable entity.</summary>
    public static class DamageCalculator
    {
        public static float ResistanceMultiplier(List<DamageModifier> resistances, DamageType type)
        {
            if (resistances == null) return 1f;
            for (int i = 0; i < resistances.Count; i++)
                if (resistances[i].type == type) return resistances[i].multiplier < 0f ? 0f : resistances[i].multiplier;
            return 1f;
        }

        /// <summary>
        /// Applies resistances, then armour plating (absorbs non-piercing damage first; overflow carries into
        /// health), then health. Mutates the passed armour/health values.
        /// </summary>
        public static DamageResult Apply(float rawDamage, DamageType type, List<DamageModifier> resistances,
                                         ref float armor, ref float health)
        {
            var r = new DamageResult();
            if (rawDamage <= 0f || health <= 0f) return r;
            float dmg = rawDamage * ResistanceMultiplier(resistances, type);
            if (armor > 0f && type != DamageType.Piercing)
            {
                float absorbed = dmg < armor ? dmg : armor;
                armor -= absorbed;
                dmg -= absorbed;
                r.toArmor = absorbed;
                if (armor <= 0f)
                {
                    armor = 0f;
                    r.armorBroken = true;
                }
            }
            if (dmg > 0f)
            {
                float toHealth = dmg < health ? dmg : health;
                health -= toHealth;
                r.toHealth = toHealth;
                if (health <= 0f)
                {
                    health = 0f;
                    r.killed = true;
                }
            }
            return r;
        }
    }
}
