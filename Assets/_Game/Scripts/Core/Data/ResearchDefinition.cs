using UnityEngine;

namespace VerdantSiege.Data
{
    /// <summary>What a research node changes. Each is a tactical/horizontal rule change, applied through BattleModifiers.</summary>
    public enum ResearchEffect
    {
        StartingVitality = 0,      // + magnitude Vitality at battle start
        PickupLifetime = 1,        // + magnitude seconds on every Sunmote
        SunlightPoolDuration = 2,  // sunlight pools last x(1 + magnitude)
        SkyDropRate = 3,           // sky Sunmotes every interval x(1 - magnitude)
        HeartrootGift = 4,         // a wounded Heartroot sheds a Sunmote worth magnitude
        UndoGrace = 5,             // + magnitude seconds of full-refund undo
        PrepTime = 6,              // + magnitude seconds before the first wave
        CardCooldown = 7,          // card cooldowns x(1 - magnitude)
        DeckSlot = 8,              // + magnitude deck slots (max 8)
        HeartrootHealth = 9,       // + magnitude Heartroot health
        EvolutionGrowth = 10,      // evolution XP needed x(1 - magnitude)
        EvolutionCost = 11,        // evolutions cost magnitude less Vitality
        MasteryXp = 12,            // mastery XP x(1 + magnitude)
        UprootRefund = 13          // + magnitude refund fraction for plants uprooted by a boss
    }

    /// <summary>One node of the Seed Conservatory research tree (immutable). Seeds pay for basic nodes, Essence for advanced ones.</summary>
    [CreateAssetMenu(menuName = "Verdant Siege/Research", fileName = "Research_")]
    public sealed class ResearchDefinition : ScriptableObject
    {
        public string id = "research";
        public string displayName = "Research";
        [Tooltip("One plain-language line: what changes in battle.")]
        public string description = "";
        [Tooltip("Column in the tree (0 Sunlight, 1 Cultivation, 2 Evolution).")]
        [Range(0, 2)] public int branch;
        [Tooltip("Row in its column (0 = top).")]
        [Range(0, 6)] public int tier;
        [Min(0)] public int seedCost;
        [Min(0)] public int essenceCost;
        [Tooltip("Must be researched first (null = none).")]
        public ResearchDefinition prerequisite;
        public ResearchEffect effect;
        public float magnitude;
        [Tooltip("1-2 characters drawn on the node.")]
        public string glyph = "";
    }
}
