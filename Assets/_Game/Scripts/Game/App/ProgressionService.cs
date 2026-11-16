using System.Collections.Generic;
using VerdantSiege.Battle;
using VerdantSiege.Data;
using VerdantSiege.Progression;
using VerdantSiege.Save;

namespace VerdantSiege.App
{
    /// <summary>
    /// Thin app-side facade over the pure progression rules (VerdantSiege.Progression) for the menus and the battle
    /// scene: decks, research purchases, mission rewards. It owns no rules; it pairs the save with the content and saves.
    /// </summary>
    public sealed class ProgressionService
    {
        private readonly SaveService _save;
        private readonly ContentDatabase _db;

        public ProgressionService(SaveService save, ContentDatabase db)
        {
            _save = save;
            _db = db;
            Reconcile();
        }

        public SaveData Data => _save.Data;
        public ContentDatabase Content => _db;
        public CampaignDefinition Campaign => _db != null ? _db.campaign : null;
        public bool HasCampaign => Campaign != null;

        /// <summary>Starting defenders, unlocks from completed missions and the one-off legacy mastery grant.</summary>
        public void Reconcile()
        {
            if (HasCampaign) CampaignRules.Reconcile(Data, Campaign);
        }

        public int DeckSlots(EncounterDefinition e) =>
            CampaignRules.DeckSlots(e, HasCampaign ? ResearchRules.DeckSlotBonus(Data, Campaign.research) : 0);

        public List<DefenderDefinition> DeckFor(EncounterDefinition e) =>
            HasCampaign ? LoadoutRules.DefaultDeck(e, Data, _db, DeckSlots(e)) : new List<DefenderDefinition>(e.roster);

        public LoadoutIssue SaveDeck(EncounterDefinition e, List<DefenderDefinition> deck)
        {
            LoadoutIssue issue = LoadoutRules.Validate(deck, DeckSlots(e), Data);
            if (issue != LoadoutIssue.Ok) return issue;
            var ids = new List<string>();
            foreach (DefenderDefinition d in deck) ids.Add(d.id);
            Data.SetLoadout(e.id, ids);
            _save.Save();
            return issue;
        }

        /// <summary>The battle input for a mission: saved/default deck, research and mastery (null without a campaign: neutral).</summary>
        public BattleLoadout LoadoutFor(EncounterDefinition e) => HasCampaign ? LoadoutRules.Build(DeckFor(e), Data, _db) : null;

        public bool IsMissionUnlocked(EncounterDefinition e) => !HasCampaign || CampaignRules.IsMissionUnlocked(Data, Campaign, e);

        public EncounterDefinition NextMission()
        {
            if (!HasCampaign) return _db != null && _db.encounters.Count > 0 ? _db.encounters[0] : null;
            return CampaignRules.NextMission(Data, Campaign);
        }

        public List<DefenderDefinition> Collection()
        {
            var list = new List<DefenderDefinition>();
            if (_db != null) foreach (DefenderDefinition d in _db.defenders) if (d != null && Data.IsUnlocked(d.id)) list.Add(d);
            return list;
        }

        public ResearchResult Purchase(ResearchDefinition r)
        {
            ResearchResult res = ResearchRules.TryPurchase(Data, r);
            if (res == ResearchResult.Ok) _save.Save();
            return res;
        }

        /// <summary>True when at least one research node can be bought now (menu badge).</summary>
        public bool AnyResearchAffordable()
        {
            if (!HasCampaign) return false;
            foreach (ResearchDefinition r in Campaign.research) if (r != null && ResearchRules.StateOf(Data, r) == ResearchState.Available) return true;
            return false;
        }

        /// <summary>Applies the battle's rewards to the save (the caller saves).</summary>
        public MissionRewards Grant(EncounterDefinition e, BattleSimulation sim) =>
            RewardRules.Grant(Data, Campaign, _db, e, MissionOutcome.From(sim));
    }
}
