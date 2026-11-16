using System;
using System.Collections.Generic;
using VerdantSiege.Data;

namespace VerdantSiege.Battle
{
    /// <summary>
    /// Rule changes from account research (Milestone 4). Every field is neutral by default, so a battle without
    /// research plays exactly as authored. The simulation reads these at the few places they apply; definitions
    /// stay immutable.
    /// </summary>
    public sealed class BattleModifiers
    {
        public int startingVitality;
        public float pickupLifetime;
        public float sunlightPoolDurationMultiplier = 1f;
        public float skyDropIntervalMultiplier = 1f;
        /// <summary>Vitality of the Sunmote a wounded Heartroot sheds (0 = none).</summary>
        public int heartrootGift;
        public float undoGrace;
        public float prepTime;
        public float cardCooldownMultiplier = 1f;
        public int heartrootHealth;
        public float evolutionXpMultiplier = 1f;
        public int evolutionCostReduction;
        public float uprootRefundBonus;

        public bool IsNeutral =>
            startingVitality == 0 && pickupLifetime == 0f && sunlightPoolDurationMultiplier == 1f && skyDropIntervalMultiplier == 1f &&
            heartrootGift == 0 && undoGrace == 0f && prepTime == 0f && cardCooldownMultiplier == 1f && heartrootHealth == 0 &&
            evolutionXpMultiplier == 1f && evolutionCostReduction == 0 && uprootRefundBonus == 0f;
    }

    /// <summary>
    /// What the player brings into a battle: the deck (chosen on the loadout screen), research modifiers and defender
    /// mastery levels. A default loadout is neutral: the encounter's roster, no modifiers, every evolution branch open
    /// (that is what the balance tests and older call sites use).
    /// </summary>
    public sealed class BattleLoadout
    {
        /// <summary>Mastery level at which a defender starts each battle with some growth.</summary>
        public const int HeadStartLevel = 3;
        public const float HeadStartFraction = 0.25f;
        /// <summary>Mastery level at which a defender's evolutions cost less Vitality.</summary>
        public const int ThriftLevel = 4;
        public const float ThriftMultiplier = 0.75f;

        /// <summary>Cards in deck order (null or empty = the encounter's roster).</summary>
        public List<DefenderDefinition> deck;
        public BattleModifiers modifiers = new BattleModifiers();
        /// <summary>Defender id -> mastery level. Null = no mastery system (every branch open, no perks).</summary>
        public Dictionary<string, int> masteryLevels;

        public static BattleLoadout Neutral => new BattleLoadout();

        public int MasteryOf(string defenderId) =>
            masteryLevels == null ? 0 : masteryLevels.TryGetValue(defenderId, out int l) ? Math.Max(1, l) : 1;

        /// <summary>Is this branch available in battle (mastery requirement met)?</summary>
        public bool BranchUnlocked(DefenderDefinition d, EvolutionDefinition evo) =>
            masteryLevels == null || evo == null || MasteryOf(d.id) >= evo.masteryRequired;

        /// <summary>Growth fraction a defender starts with (mastery perk).</summary>
        public float HeadStart(DefenderDefinition d) => masteryLevels != null && MasteryOf(d.id) >= HeadStartLevel ? HeadStartFraction : 0f;

        public float EvolutionCostMultiplier(DefenderDefinition d) => masteryLevels != null && MasteryOf(d.id) >= ThriftLevel ? ThriftMultiplier : 1f;
    }

    /// <summary>Per-defender usage in one battle (feeds defender mastery).</summary>
    public sealed class DefenderUsage
    {
        public string defenderId;
        public int planted;
        public int evolved;
    }
}
