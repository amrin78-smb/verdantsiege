using System;
using System.Collections.Generic;
using VerdantSiege.Battle;
using VerdantSiege.Data;
using VerdantSiege.Save;

namespace VerdantSiege.Progression
{
    /// <summary>
    /// Campaign rules (Milestone 4): which missions are open, stars, deck slots, the defender collection and the
    /// Sanctuary restoration tier. Pure functions over <see cref="SaveData"/> + <see cref="CampaignDefinition"/>.
    /// </summary>
    public static class CampaignRules
    {
        /// <summary>Missions won (any region) for Sanctuary restoration tiers 1..4.</summary>
        public static readonly int[] SanctuaryTierMissions = { 1, 3, 5, 7 };
        public const int MaxSanctuaryTier = 4;
        /// <summary>Mastery XP per completed mission for each of its roster defenders when an older save is migrated.</summary>
        public const int LegacyMasteryPerMission = 35;

        public static bool RegionComplete(SaveData save, RegionDefinition region)
        {
            if (region == null || region.comingSoon || region.missions.Count == 0) return false;
            foreach (EncounterDefinition e in region.missions) if (e != null && !save.IsCompleted(e.id)) return false;
            return true;
        }

        /// <summary>A region opens when every playable region before it is complete; teasers never open.</summary>
        public static bool RegionUnlocked(SaveData save, CampaignDefinition c, RegionDefinition region)
        {
            if (region == null || region.comingSoon) return false;
            int index = c.regions.IndexOf(region);
            for (int i = 0; i < index; i++)
            {
                RegionDefinition r = c.regions[i];
                if (r != null && !r.comingSoon && !RegionComplete(save, r)) return false;
            }
            return true;
        }

        /// <summary>The first mission of an open region is open; every later one opens when the previous one is won.</summary>
        public static bool IsMissionUnlocked(SaveData save, CampaignDefinition c, EncounterDefinition e)
        {
            RegionDefinition region = c.RegionOf(e);
            if (!RegionUnlocked(save, c, region)) return false;
            int i = region.missions.IndexOf(e);
            return i == 0 || (region.missions[i - 1] != null && save.IsCompleted(region.missions[i - 1].id));
        }

        /// <summary>The first open, unwon mission (null when the whole campaign is won).</summary>
        public static EncounterDefinition NextMission(SaveData save, CampaignDefinition c)
        {
            foreach (EncounterDefinition e in c.AllMissions())
                if (!save.IsCompleted(e.id) && IsMissionUnlocked(save, c, e)) return e;
            return null;
        }

        public static EncounterDefinition NextInRegion(CampaignDefinition c, EncounterDefinition e)
        {
            RegionDefinition r = c.RegionOf(e);
            if (r == null) return null;
            int i = r.missions.IndexOf(e);
            return i >= 0 && i + 1 < r.missions.Count ? r.missions[i + 1] : null;
        }

        public static int Stars(SaveData save, EncounterDefinition e)
        {
            EncounterRecord r = save.Find(e.id);
            return r != null ? r.StarCount : 0;
        }

        public static int StarsIn(SaveData save, RegionDefinition region)
        {
            int n = 0;
            foreach (EncounterDefinition e in region.missions) if (e != null) n += Stars(save, e);
            return n;
        }

        public static int CompletedCount(SaveData save, CampaignDefinition c)
        {
            int n = 0;
            foreach (EncounterDefinition e in c.AllMissions()) if (save.IsCompleted(e.id)) n++;
            return n;
        }

        /// <summary>0 (blighted) .. 4 (fully restored) from missions won.</summary>
        public static int SanctuaryTier(SaveData save, CampaignDefinition c)
        {
            int won = CompletedCount(save, c), tier = 0;
            for (int i = 0; i < SanctuaryTierMissions.Length; i++) if (won >= SanctuaryTierMissions[i]) tier = i + 1;
            return tier;
        }

        /// <summary>Cards the player may bring: the mission's authored roster size plus research, capped at 8.</summary>
        public static int DeckSlots(EncounterDefinition e, int researchBonus) =>
            Math.Max(1, Math.Min(ResearchRules.MaxDeckSlots, (e != null ? e.roster.Count : 1) + Math.Max(0, researchBonus)));

        /// <summary>The mission whose first win adds this defender to the collection (null for starters).</summary>
        public static EncounterDefinition UnlockedBy(CampaignDefinition c, DefenderDefinition d)
        {
            foreach (EncounterDefinition e in c.AllMissions()) if (e.unlocksDefenders.Contains(d)) return e;
            return null;
        }

        /// <summary>
        /// Makes the save consistent with the content after load: starting defenders and every completed mission's
        /// unlocks are in the collection, and (once, after a v2 -> v3 migration) earlier victories become mastery so a
        /// returning player keeps the branches they were already using. Returns defenders newly added.
        /// </summary>
        public static List<string> Reconcile(SaveData save, CampaignDefinition c)
        {
            var added = new List<string>();
            if (save == null || c == null) return added;
            foreach (DefenderDefinition d in c.startingDefenders) if (d != null && save.Unlock(d.id)) added.Add(d.id);
            foreach (EncounterDefinition e in c.AllMissions())
                if (save.IsCompleted(e.id))
                    foreach (DefenderDefinition d in e.unlocksDefenders) if (d != null && save.Unlock(d.id)) added.Add(d.id);
            if (save.legacyGrantPending)
            {
                int cap = MasteryRules.Thresholds[MasteryRules.MaxLevel - 1];
                foreach (EncounterDefinition e in c.AllMissions())
                {
                    if (!save.IsCompleted(e.id)) continue;
                    foreach (DefenderDefinition d in e.roster)
                    {
                        if (d == null) continue;
                        int room = cap - save.MasteryXp(d.id);
                        save.AddMasteryXp(d.id, Math.Min(LegacyMasteryPerMission, room));
                    }
                }
                save.legacyGrantPending = false;
                save.sanctuaryTierSeen = SanctuaryTier(save, c); // old progress is not "new" restoration
            }
            if (save.sanctuaryTierSeen < 0) save.sanctuaryTierSeen = SanctuaryTier(save, c);
            return added;
        }
    }
}
