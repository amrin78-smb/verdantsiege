using System;
using System.Collections.Generic;
using VerdantSiege.Battle;
using VerdantSiege.Data;
using VerdantSiege.Save;

namespace VerdantSiege.Progression
{
    /// <summary>What a finished battle produced (copied from the simulation so rewards can be computed and tested without one).</summary>
    public sealed class MissionOutcome
    {
        public bool won;
        public int heart, heartMax;
        public float elapsed;
        public int defendersPlanted, defendersLost, evolutions, deckSize;
        public bool bossDefeated;
        public int staggers;
        public int bossBonusSeeds;
        public int bossEssenceFirst, bossEssenceRepeat;
        public readonly List<DefenderUsage> usage = new List<DefenderUsage>();

        public bool Flawless => won && heart >= heartMax;

        public static MissionOutcome From(BattleSimulation sim)
        {
            var o = new MissionOutcome
            {
                won = sim.State == BattleState.Won,
                heart = sim.HeartrootHealth,
                heartMax = sim.HeartrootMaxHealth,
                elapsed = sim.Stats.elapsed,
                defendersPlanted = sim.Stats.defendersPlanted,
                defendersLost = sim.Stats.defendersLost,
                evolutions = sim.EvolutionsChosen,
                deckSize = sim.Deck.Count,
                bossDefeated = sim.Boss != null && sim.Boss.state == BossState.Defeated && sim.State == BattleState.Won,
                staggers = sim.Boss != null ? sim.Boss.staggerCount : 0,
            };
            if (o.bossDefeated)
            {
                o.bossBonusSeeds = sim.Boss.Definition.bonusSeeds;
                o.bossEssenceFirst = sim.Boss.Definition.essenceFirstDefeat;
                o.bossEssenceRepeat = sim.Boss.Definition.essenceRepeatDefeat;
            }
            foreach (KeyValuePair<string, DefenderUsage> kv in sim.Usage)
                if (kv.Value.planted > 0) o.usage.Add(new DefenderUsage { defenderId = kv.Key, planted = kv.Value.planted, evolved = kv.Value.evolved });
            o.usage.Sort((a, b) => string.CompareOrdinal(a.defenderId, b.defenderId));
            return o;
        }
    }

    /// <summary>One defender's mastery change from a battle (for the results screen).</summary>
    public sealed class MasteryGain
    {
        public string defenderId;
        public int gained, xpBefore, xpAfter, levelBefore, levelAfter;
        /// <summary>Evolution branches unlocked by the level-up(s).</summary>
        public readonly List<EvolutionDefinition> branchesUnlocked = new List<EvolutionDefinition>();
        public bool LeveledUp => levelAfter > levelBefore;
    }

    /// <summary>Everything a battle paid out (already applied to the save by <see cref="RewardRules.Grant"/>).</summary>
    public sealed class MissionRewards
    {
        public bool won, firstClear, replay;
        public int seedsMission, seedsFlawless, seedsChallenge, seedsBoss;
        public int essenceBoss, essenceChallenge;
        public bool firstBossDefeat;
        /// <summary>Star bits after this battle, and the ones earned for the first time now.</summary>
        public int stars, newStars;
        public bool challengeMet;
        public readonly List<MasteryGain> mastery = new List<MasteryGain>();
        public readonly List<DefenderDefinition> unlocked = new List<DefenderDefinition>();
        public bool regionCompleted;
        public int sanctuaryTierBefore, sanctuaryTierAfter;

        public int Seeds => seedsMission + seedsFlawless + seedsChallenge + seedsBoss;
        public int Essence => essenceBoss + essenceChallenge;
    }

    /// <summary>
    /// Mission rewards (Milestone 4). First win pays the mission's Seeds; replays pay a fraction (no farming loop,
    /// but replays are never worthless). Each star (win, flawless, challenge) pays its bonus once. Mastery is earned
    /// on every battle, won or lost, for the defenders actually planted. Defender unlocks come with first wins.
    /// </summary>
    public static class RewardRules
    {
        public static bool ObjectiveMet(MissionObjective o, MissionOutcome r)
        {
            if (o == null || !o.IsSet || !r.won) return false;
            switch (o.kind)
            {
                case MissionObjectiveKind.EvolveAtLeast: return r.evolutions >= o.value;
                case MissionObjectiveKind.LoseAtMostDefenders: return r.defendersLost <= o.value;
                case MissionObjectiveKind.DeckSizeAtMost: return r.deckSize <= o.value;
                case MissionObjectiveKind.PlantAtMost: return r.defendersPlanted <= o.value;
                case MissionObjectiveKind.StaggerBossAtLeast: return r.staggers >= o.value;
                case MissionObjectiveKind.WinWithinSeconds: return r.elapsed <= o.value;
                default: return false;
            }
        }

        public static int Replay(int amount, float fraction) => (int)Math.Round(amount * Math.Max(0f, Math.Min(1f, fraction)), MidpointRounding.AwayFromZero);

        /// <summary>Computes and applies the rewards of a finished battle to the save (attempts, stars, currencies, mastery, unlocks).</summary>
        public static MissionRewards Grant(SaveData save, CampaignDefinition campaign, ContentDatabase db, EncounterDefinition e, MissionOutcome o)
        {
            var r = new MissionRewards { won = o.won };
            r.sanctuaryTierBefore = campaign != null ? CampaignRules.SanctuaryTier(save, campaign) : 0;
            EncounterRecord rec = save.GetOrCreate(e.id);
            rec.attempts++;

            // Mastery: every planted defender, win or lose.
            float masteryMul = campaign != null ? ResearchRules.MasteryXpMultiplier(save, campaign.research) : 1f;
            foreach (DefenderUsage u in o.usage)
            {
                int gain = MasteryRules.BattleGain(u, o.won, masteryMul);
                if (gain <= 0) continue;
                var g = new MasteryGain { defenderId = u.defenderId, gained = gain, xpBefore = save.MasteryXp(u.defenderId) };
                g.levelBefore = MasteryRules.LevelFor(g.xpBefore);
                save.AddMasteryXp(u.defenderId, gain);
                g.xpAfter = save.MasteryXp(u.defenderId);
                g.levelAfter = MasteryRules.LevelFor(g.xpAfter);
                DefenderDefinition def = db != null ? db.FindDefender(u.defenderId) : null;
                if (def != null) for (int l = g.levelBefore + 1; l <= g.levelAfter; l++) g.branchesUnlocked.AddRange(MasteryRules.BranchesAt(def, l));
                r.mastery.Add(g);
            }

            if (o.won)
            {
                float replayFraction = campaign != null ? campaign.replaySeedFraction : 0.35f;
                r.firstClear = !rec.completed;
                r.replay = !r.firstClear;
                r.seedsMission = r.firstClear ? e.seedReward : Replay(e.seedReward, replayFraction);
                int bits = EncounterRecord.StarWin | (o.Flawless ? EncounterRecord.StarFlawless : 0);
                r.challengeMet = ObjectiveMet(e.challenge, o);
                if (r.challengeMet) bits |= EncounterRecord.StarChallenge;
                r.newStars = bits & ~rec.stars;
                if ((r.newStars & EncounterRecord.StarFlawless) != 0) r.seedsFlawless = e.flawlessBonus;
                if ((r.newStars & EncounterRecord.StarChallenge) != 0) { r.seedsChallenge = e.challengeSeeds; r.essenceChallenge = e.challengeEssence; }
                if (o.bossDefeated)
                {
                    r.firstBossDefeat = rec.bossDefeats == 0;
                    r.seedsBoss = r.firstBossDefeat ? o.bossBonusSeeds : Replay(o.bossBonusSeeds, replayFraction);
                    r.essenceBoss = save.AwardBossVictory(e.id, o.bossEssenceFirst, o.bossEssenceRepeat); // adds to save.essence
                }
                rec.stars |= bits;
                r.stars = rec.stars;
                rec.completed = true;
                if (o.Flawless) rec.flawless = true;
                rec.bestHeartroot = Math.Max(rec.bestHeartroot, o.heart);
                rec.wins++;
                save.seeds += r.Seeds;
                save.essence += Math.Max(0, r.essenceChallenge);
                if (r.firstClear)
                {
                    foreach (DefenderDefinition d in e.unlocksDefenders) if (d != null && save.Unlock(d.id)) r.unlocked.Add(d);
                    RegionDefinition region = campaign != null ? campaign.RegionOf(e) : null;
                    r.regionCompleted = region != null && CampaignRules.RegionComplete(save, region);
                }
            }
            else r.stars = rec.stars;
            r.sanctuaryTierAfter = campaign != null ? CampaignRules.SanctuaryTier(save, campaign) : 0;
            return r;
        }
    }
}
