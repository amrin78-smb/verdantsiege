using System;
using System.Collections.Generic;
using VerdantSiege.Battle;
using VerdantSiege.Data;
using VerdantSiege.Save;

namespace VerdantSiege.Progression
{
    public enum ResearchState
    {
        Owned = 0,
        Available,      // prerequisite met and affordable
        Unaffordable,   // prerequisite met, not enough Seeds/Essence yet
        Locked          // prerequisite not researched
    }

    public enum ResearchResult
    {
        Ok = 0,
        AlreadyOwned,
        PrerequisiteMissing,
        NotEnoughSeeds,
        NotEnoughEssence,
        Invalid
    }

    /// <summary>
    /// Seed Conservatory research (Milestone 4): purchase rules (atomic) and how owned nodes become battle
    /// modifiers. Effects are tactical/horizontal; none adds raw damage.
    /// </summary>
    public static class ResearchRules
    {
        public const int MaxDeckSlots = EncounterValidator.MaxRosterSize;

        public static ResearchState StateOf(SaveData save, ResearchDefinition r)
        {
            if (save.HasResearch(r.id)) return ResearchState.Owned;
            if (r.prerequisite != null && !save.HasResearch(r.prerequisite.id)) return ResearchState.Locked;
            return save.seeds >= r.seedCost && save.essence >= r.essenceCost ? ResearchState.Available : ResearchState.Unaffordable;
        }

        /// <summary>Validates, spends and records in one call; anything but Ok leaves the save unchanged.</summary>
        public static ResearchResult TryPurchase(SaveData save, ResearchDefinition r)
        {
            if (save == null || r == null || string.IsNullOrEmpty(r.id)) return ResearchResult.Invalid;
            if (save.HasResearch(r.id)) return ResearchResult.AlreadyOwned;
            if (r.prerequisite != null && !save.HasResearch(r.prerequisite.id)) return ResearchResult.PrerequisiteMissing;
            if (save.seeds < r.seedCost) return ResearchResult.NotEnoughSeeds;
            if (save.essence < r.essenceCost) return ResearchResult.NotEnoughEssence;
            save.seeds -= r.seedCost;
            save.essence -= r.essenceCost;
            save.research.Add(r.id);
            return ResearchResult.Ok;
        }

        private static IEnumerable<ResearchDefinition> Owned(SaveData save, IEnumerable<ResearchDefinition> all)
        {
            if (all == null) yield break;
            foreach (ResearchDefinition r in all) if (r != null && save.HasResearch(r.id)) yield return r;
        }

        /// <summary>Battle modifiers from every owned node (neutral when nothing is owned).</summary>
        public static BattleModifiers Modifiers(SaveData save, IEnumerable<ResearchDefinition> all)
        {
            var m = new BattleModifiers();
            foreach (ResearchDefinition r in Owned(save, all))
            {
                float v = r.magnitude;
                switch (r.effect)
                {
                    case ResearchEffect.StartingVitality: m.startingVitality += (int)Math.Round(v); break;
                    case ResearchEffect.PickupLifetime: m.pickupLifetime += v; break;
                    case ResearchEffect.SunlightPoolDuration: m.sunlightPoolDurationMultiplier *= 1f + v; break;
                    case ResearchEffect.SkyDropRate: m.skyDropIntervalMultiplier *= Math.Max(0.1f, 1f - v); break;
                    case ResearchEffect.HeartrootGift: m.heartrootGift += (int)Math.Round(v); break;
                    case ResearchEffect.UndoGrace: m.undoGrace += v; break;
                    case ResearchEffect.PrepTime: m.prepTime += v; break;
                    case ResearchEffect.CardCooldown: m.cardCooldownMultiplier *= Math.Max(0.1f, 1f - v); break;
                    case ResearchEffect.HeartrootHealth: m.heartrootHealth += (int)Math.Round(v); break;
                    case ResearchEffect.EvolutionGrowth: m.evolutionXpMultiplier *= Math.Max(0.1f, 1f - v); break;
                    case ResearchEffect.EvolutionCost: m.evolutionCostReduction += (int)Math.Round(v); break;
                    case ResearchEffect.UprootRefund: m.uprootRefundBonus += v; break;
                    // DeckSlot and MasteryXp are meta effects (see below), not battle rules.
                }
            }
            return m;
        }

        public static int DeckSlotBonus(SaveData save, IEnumerable<ResearchDefinition> all)
        {
            int n = 0;
            foreach (ResearchDefinition r in Owned(save, all)) if (r.effect == ResearchEffect.DeckSlot) n += (int)Math.Round(r.magnitude);
            return n;
        }

        public static float MasteryXpMultiplier(SaveData save, IEnumerable<ResearchDefinition> all)
        {
            float k = 1f;
            foreach (ResearchDefinition r in Owned(save, all)) if (r.effect == ResearchEffect.MasteryXp) k *= 1f + r.magnitude;
            return k;
        }
    }
}
