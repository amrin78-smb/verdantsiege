using UnityEngine;

namespace VerdantSiege.Data
{
    /// <summary>How a defender earns in-battle evolution XP (role-appropriate).</summary>
    public enum EvolutionXpSource
    {
        DamageDealt = 0,      // attackers: health/armour actually removed (overkill does not count)
        DamageAbsorbed = 1,   // guardians and decoys: damage taken
        VitalityProduced = 2, // generators: Vitality produced (stored Capacitor charge included)
        HealingDone = 3,      // menders: health restored
        Triggers = 4          // traps: one point per eruption
    }

    /// <summary>Themed accessory model shown on an evolved defender (Art/Models/FX/Evo*).</summary>
    public enum EvolutionAccessory
    {
        None = 0,
        Solar = 1, // EvoSolar: golden crown of sun petals (on the top part)
        Prism = 2, // EvoPrism: crystal prism cluster (on the top part)
        Root = 3,  // EvoRoot: vine-ring network (at the base)
        Bloom = 4  // EvoBloom: open flower halo (on the top part)
    }

    /// <summary>
    /// One evolution branch for a defender (one level: level 1 → evolved). Immutable data: every field is a
    /// mechanic change applied on top of the base <see cref="DefenderDefinition"/> while the runtime carries the
    /// chosen branch. Neutral values (0, 1, null, false) mean "unchanged", so a branch only states what it alters.
    /// </summary>
    [CreateAssetMenu(menuName = "Verdant Siege/Evolution", fileName = "Evolution_")]
    public sealed class EvolutionDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "evolution";
        public string displayName = "Evolution";
        [Tooltip("One plain-language line shown in the chooser.")]
        public string description = "";
        public Color accentColor = Color.white;
        [Tooltip("Vitality cost when chosen (0-75).")]
        [Range(0, 75)] public int cost = 50;
        public EvolutionAccessory accessory = EvolutionAccessory.Bloom;
        [Tooltip("Short glyph drawn on the chooser icon (1-2 characters).")]
        public string glyph = "";

        [Header("Shooter")]
        [Tooltip("Replaces the defender's projectile (charge shots, deep freeze, ricochet storms...).")]
        public ProjectileDefinition projectileOverride;
        [Tooltip("Attack interval multiplier (charge shots are slower).")]
        [Min(0.1f)] public float attackIntervalMultiplier = 1f;
        [Tooltip("Also fires into this many lanes on each side (fan).")]
        [Range(0, 2)] public int multishotSideLanes = 0;
        [Tooltip("Damage of each side-lane shot relative to the main shot.")]
        [Range(0.1f, 1f)] public float sideShotDamageFraction = 0.7f;
        [Tooltip("Lobbed shots split into this many seeds landing in a line down the lane (1 = no split).")]
        [Range(1, 4)] public int lobClusterCount = 1;
        [Min(0f)] public float lobClusterSpacing = 0.9f;
        [Range(0.1f, 1f)] public float lobClusterDamageFraction = 0.6f;

        [Header("Aura (formation: affects defenders within auraRadius cells, Chebyshev)")]
        [Range(0, 2)] public int auraRadius = 0;
        [Tooltip("Linked neighbours act this much faster (0.25 = +25%). Several auras do not stack: the strongest applies.")]
        [Range(0f, 1f)] public float auraActionSpeedBonus = 0f;
        [Tooltip("Vitality added to the bank per linked neighbour every auraVitalityInterval seconds.")]
        [Min(0)] public int auraVitalityPerLink = 0;
        [Min(1f)] public float auraVitalityInterval = 10f;

        [Header("Generator")]
        [Min(0.1f)] public float generateIntervalMultiplier = 1f;
        [Tooltip("Capacitor: pulses are stored (up to this many) instead of dropped; the player releases them as one burst.")]
        [Min(0)] public int capacitorPulses = 0;
        [Tooltip("Release multiplier on the stored Vitality.")]
        [Min(1f)] public float capacitorReleaseMultiplier = 1.5f;

        [Header("Guardian / decoy")]
        [Tooltip("Fraction of each bite returned to the biter as piercing damage.")]
        [Range(0f, 2f)] public float thornsFraction = 0f;
        [Tooltip("Health regenerated per second.")]
        [Min(0f)] public float regenPerSecond = 0f;
        [Tooltip("Status applied to an enemy that bites this defender.")]
        public StatusEffectDefinition onBittenStatus;
        [Tooltip("Decoy: reflected projectile multiplier override (0 = keep).")]
        [Min(0f)] public float reflectMultiplierOverride = 0f;
        [Tooltip("Decoy: lure reach in lanes (0 = keep the adjacent-lane rule).")]
        [Range(0, 2)] public int lureLaneReach = 0;

        [Header("Ability (ambusher, trap, mender, chain caster, decoy)")]
        [Min(0.05f)] public float abilityRechargeMultiplier = 1f;
        [Min(0f)] public float abilityRangeBonus = 0f;
        [Min(0f)] public float abilityDamageMultiplier = 1f;
        [Tooltip("Added to the chain target count (chain caster).")]
        public int abilityCountBonus = 0;
        [Tooltip("Replaces the chain target count (0 = keep).")]
        [Min(0)] public int abilityCountOverride = 0;
        [Tooltip("Chain falloff override (0 = keep).")]
        [Range(0f, 1f)] public float abilityFalloffOverride = 0f;
        [Tooltip("Status applied by the ability (replaces the base one).")]
        public StatusEffectDefinition abilityStatusOverride;
        [Tooltip("The ability status applies to every enemy hit (not only Mechanical ones).")]
        public bool abilityStatusOnAll;
        [Tooltip("Trap: the eruption also bursts in this many lanes on each side.")]
        [Range(0, 2)] public int blastSideLanes = 0;
        [Tooltip("The ability can reach flying enemies.")]
        public bool grantsAntiAir;
    }
}
