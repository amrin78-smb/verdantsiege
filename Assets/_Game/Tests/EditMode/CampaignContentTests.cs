using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VerdantSiege.Battle;
using VerdantSiege.Data;
using VerdantSiege.Grid;
using VerdantSiege.Progression;
using VerdantSiege.Save;

namespace VerdantSiege.Tests
{
    /// <summary>
    /// Milestone 4 shipped content: the campaign is consistent (every mission's roster is owned by the time it opens,
    /// research is a proper tree, every defender has exactly one branch open at mastery 1), and progression stays
    /// horizontal: a fresh profile (mastery 1, no research) still wins every mission with the shared bot, research
    /// never makes an undefended garden win, and the restricted-roster challenges are achievable.
    /// </summary>
    public sealed class CampaignContentTests
    {
        private const string DbPath = "Assets/_Game/Data/ContentDatabase.asset";

        private static ContentDatabase Db()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(DbPath);
            Assert.IsNotNull(db, "Run Tools/Verdant Siege/Setup Project first");
            Assert.IsNotNull(db.campaign, "campaign asset");
            return db;
        }

        [Test]
        public void Campaign_CoversEveryEncounter_RegionsAndTeasers()
        {
            ContentDatabase db = Db();
            CampaignDefinition c = db.campaign;
            var missions = new List<EncounterDefinition>(c.AllMissions());
            CollectionAssert.AreEquivalent(db.encounters, missions, "every shipped encounter is a campaign mission");
            Assert.AreEqual("vh_07", missions[missions.Count - 1].id, "the Root Maw is the region finale");
            Assert.IsTrue(c.regions.Exists(r => r.comingSoon && r.id == "emberwild"), "Emberwild teaser");
            foreach (EncounterDefinition e in missions)
            {
                Assert.That(e.mapPosition.x, Is.InRange(0.03f, 0.97f), e.id);
                Assert.That(e.mapPosition.y, Is.InRange(0.1f, 0.9f), e.id);
                Assert.IsTrue(e.challenge.IsSet, e.id + " has a challenge objective");
                Assert.IsFalse(string.IsNullOrEmpty(e.challenge.text), e.id);
            }
        }

        [Test]
        public void Campaign_EveryRosterIsOwnedWhenItsMissionOpens()
        {
            CampaignDefinition c = Db().campaign;
            var owned = new HashSet<string>();
            foreach (DefenderDefinition d in c.startingDefenders) owned.Add(d.id);
            Assert.AreEqual(3, owned.Count, "three starters");
            var unlockedOnce = new HashSet<string>();
            foreach (EncounterDefinition e in c.AllMissions())
            {
                foreach (DefenderDefinition d in e.roster) Assert.IsTrue(owned.Contains(d.id), $"{e.id} recommends {d.id} before it can be owned");
                foreach (DefenderDefinition d in e.unlocksDefenders)
                {
                    Assert.IsTrue(unlockedOnce.Add(d.id), d.id + " unlocked twice");
                    Assert.IsFalse(owned.Contains(d.id), d.id + " is already a starter");
                    owned.Add(d.id);
                }
            }
            Assert.AreEqual(12, owned.Count, "the whole roster is earned in the Heartlands");
        }

        [Test]
        public void Research_IsAValidTree_SeedsBasicEssenceAdvanced()
        {
            CampaignDefinition c = Db().campaign;
            Assert.That(c.research.Count, Is.InRange(12, 18));
            var ids = new HashSet<string>();
            foreach (ResearchDefinition r in c.research)
            {
                Assert.IsTrue(ids.Add(r.id), "duplicate " + r.id);
                Assert.IsTrue(r.seedCost > 0 ^ r.essenceCost > 0, r.id + " costs Seeds or Essence");
                Assert.That(r.description.Length, Is.InRange(10, 72), r.id);
                if (r.prerequisite != null)
                {
                    Assert.IsTrue(c.research.Contains(r.prerequisite), r.id);
                    Assert.AreEqual(r.prerequisite.tier + 1, r.tier, r.id + " sits under its prerequisite");
                    Assert.AreEqual(r.prerequisite.branch, r.branch, r.id);
                }
                else Assert.AreEqual(0, r.tier, r.id);
                if (r.essenceCost > 0) Assert.IsNotNull(r.prerequisite, r.id + ": advanced nodes come after basic ones");
            }
        }

        [Test]
        public void Mastery_EveryDefenderHasOneBranchAtLevelOne()
        {
            foreach (DefenderDefinition d in Db().defenders)
            {
                int first = 0;
                foreach (EvolutionDefinition e in d.evolutions)
                {
                    Assert.That(e.masteryRequired, Is.InRange(1, 3), e.id);
                    if (e.masteryRequired == 1) first++;
                }
                Assert.AreEqual(1, first, d.id + ": exactly one branch is open before any mastery");
                Assert.AreEqual(d.evolutions.Count, MasteryRules.BranchesAt(d, 1).Count + MasteryRules.BranchesAt(d, 2).Count + MasteryRules.BranchesAt(d, 3).Count, d.id);
            }
        }

        // ------------------------------------------------------------------ balance with progression

        private static BattleSimulation Play(EncounterDefinition e, int seed, bool defend, BattleLoadout loadout, out float minutes, int plantCap = int.MaxValue)
        {
            var sim = new BattleSimulation(e, seed, BattleOptions.Default, loadout);
            var player = new ScriptedPlayer();
            float think = 0f;
            const float dt = BattleSimulation.FixedStep;
            int guard = 0;
            while (sim.State == BattleState.Running && guard++ < 30 * 60 * 15)
            {
                sim.Step(dt);
                think -= dt;
                if (!defend || think > 0f) continue;
                think = 0.5f;
                foreach (PickupRuntime p in sim.Pickups)
                    if (p.alive && p.age > 1.5f) { sim.CollectPickup(p.Id); break; }
                if (player.DecideEvolution(sim, out DefenderRuntime evolving, out int option))
                    Assert.AreEqual(EvolveResult.Ok, sim.TryEvolve(evolving, option), "ScriptedPlayer chose an invalid evolution");
                if (sim.Stats.defendersPlanted < plantCap && player.Decide(sim, out int slot, out GridCoord cell))
                    Assert.AreEqual(DeployResult.Ok, sim.TryDeploy(slot, cell), "ScriptedPlayer chose an invalid deployment");
            }
            minutes = sim.Stats.elapsed / 60f;
            return sim;
        }

        /// <summary>A profile that owns every defender but has no mastery and no research (the hardest legal state).</summary>
        private static BattleLoadout FreshProfile(ContentDatabase db, EncounterDefinition e)
        {
            var save = new SaveData();
            foreach (DefenderDefinition d in db.defenders) save.Unlock(d.id);
            return LoadoutRules.Build(e.roster, save, db);
        }

        private static BattleLoadout FullProgress(ContentDatabase db, IList<DefenderDefinition> deck)
        {
            var save = new SaveData();
            foreach (DefenderDefinition d in db.defenders) { save.Unlock(d.id); save.AddMasteryXp(d.id, 10000); }
            foreach (ResearchDefinition r in db.campaign.research) save.research.Add(r.id);
            return LoadoutRules.Build(deck, save, db);
        }

        [Test]
        public void FreshProfile_MasteryOneNoResearch_StillWinsEveryMission([Values(1, 42)] int seed)
        {
            ContentDatabase db = Db();
            foreach (EncounterDefinition e in db.campaign.AllMissions())
            {
                BattleSimulation sim = Play(e, seed, true, FreshProfile(db, e), out float minutes);
                Assert.AreEqual(BattleState.Won, sim.State, $"{e.id} seed {seed}: a fresh profile must not need grinding (heart {sim.HeartrootHealth}, {minutes:0.0} min)");
                Debug.Log($"[Campaign] fresh {e.id} seed={seed} WON {minutes:0.0} min heart {sim.HeartrootHealth}/{sim.HeartrootMaxHealth} evolutions {sim.EvolutionsChosen}");
            }
        }

        [Test]
        public void FullResearchAndMastery_DoesNotTrivialise_UndefendedStillLoses()
        {
            ContentDatabase db = Db();
            foreach (EncounterDefinition e in db.campaign.AllMissions())
            {
                BattleSimulation sim = Play(e, 5, false, FullProgress(db, e.roster), out _);
                Assert.AreEqual(BattleState.Lost, sim.State, e.id + ": research must not win the battle on its own");
            }
        }

        [Test]
        public void FullResearchAndMastery_BotStillNeedsTheWholeFight()
        {
            ContentDatabase db = Db();
            foreach (EncounterDefinition e in db.campaign.AllMissions())
            {
                BattleSimulation sim = Play(e, 7, true, FullProgress(db, e.roster), out float minutes);
                Assert.AreEqual(BattleState.Won, sim.State, e.id);
                Assert.Greater(minutes, 2.5f, e.id + ": encounters keep their length (research changes tempo, not the waves)");
                Debug.Log($"[Campaign] full {e.id} WON {minutes:0.0} min heart {sim.HeartrootHealth}/{sim.HeartrootMaxHealth} lost {sim.Stats.defendersLost} evolutions {sim.EvolutionsChosen}");
            }
        }

        /// <summary>
        /// The restricted challenges are real constraints the shared bot does not meet by accident (it brings every card
        /// and plants freely), yet a bot that respects them (smaller deck / planting cap) still meets them on most seeds.
        /// </summary>
        [TestCase("vh_02", new[] { "glowcap", "sunspore", "bulwark_bloom" })]
        [TestCase("vh_03", new[] { "glowcap", "sunspore", "thorncaster", "frost_fern" })]
        [TestCase("vh_05", new[] { "glowcap", "sunspore", "bulwark_bloom", "thorncaster", "whirlseed", "storm_orchid", "mirrorleaf", "frost_fern" })]
        [TestCase("vh_06", new[] { "glowcap", "sunspore", "thorncaster", "mender_moss", "storm_orchid", "ember_lily" })]
        public void RestrictedChallenges_AreAchievable(string missionId, string[] deckIds)
        {
            ContentDatabase db = Db();
            EncounterDefinition e = db.FindEncounter(missionId);
            var deck = new List<DefenderDefinition>();
            foreach (string id in deckIds) deck.Add(db.FindDefender(id));
            int cap = e.challenge.kind == MissionObjectiveKind.PlantAtMost ? e.challenge.value : int.MaxValue;
            if (e.challenge.kind == MissionObjectiveKind.DeckSizeAtMost) Assert.LessOrEqual(deck.Count, e.challenge.value);
            int met = 0;
            foreach (int seed in new[] { 1, 42, 99999 })
            {
                BattleSimulation sim = Play(e, seed, true, FreshProfile(db, e).WithDeck(deck), out float minutes, cap);
                MissionOutcome o = MissionOutcome.From(sim);
                if (RewardRules.ObjectiveMet(e.challenge, o)) met++;
                Debug.Log($"[Campaign] {missionId} challenge '{e.challenge.text}' seed={seed} {sim.State} {minutes:0.0} min heart {sim.HeartrootHealth} planted {o.defendersPlanted} deck {o.deckSize}");
            }
            Assert.GreaterOrEqual(met, 2, $"{missionId}: '{e.challenge.text}' is achievable on most seeds");
            BattleSimulation free = Play(e, 1, true, FreshProfile(db, e), out _);
            Assert.IsFalse(RewardRules.ObjectiveMet(e.challenge, MissionOutcome.From(free)), $"{missionId}: the challenge is not met by default play");
        }

        [Test, Explicit]
        public void ChallengeReport()
        {
            // Tuning aid: how often the shared bot meets each mission's challenge with the recommended deck and a fresh profile.
            ContentDatabase db = Db();
            var sb = new System.Text.StringBuilder("[ChallengeReport]\n");
            foreach (EncounterDefinition e in db.campaign.AllMissions())
            {
                int met = 0, won = 0;
                string detail = "";
                foreach (int seed in new[] { 1, 7, 42, 1234, 99999, 5 })
                {
                    BattleSimulation sim = Play(e, seed, true, FreshProfile(db, e), out _);
                    MissionOutcome o = MissionOutcome.From(sim);
                    if (o.won) won++;
                    if (RewardRules.ObjectiveMet(e.challenge, o)) met++;
                    detail += $" [ev{o.evolutions} lost{o.defendersLost} plant{o.defendersPlanted} stag{o.staggers} {o.elapsed / 60f:0.0}m]";
                }
                sb.Append($"{e.id} '{e.challenge.text}': won {won}/6, challenge {met}/6 {detail}\n");
            }
            Debug.Log(sb.ToString());
        }
    }

    internal static class LoadoutTestExtensions
    {
        public static BattleLoadout WithDeck(this BattleLoadout l, List<DefenderDefinition> deck)
        {
            l.deck = deck;
            return l;
        }
    }
}
