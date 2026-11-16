using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using VerdantSiege.Battle;
using VerdantSiege.Data;
using VerdantSiege.Grid;
using VerdantSiege.Progression;
using VerdantSiege.Save;

namespace VerdantSiege.Tests
{
    /// <summary>
    /// Milestone 4: save v3 + migration, campaign unlock rules, rewards (first clear, replay, stars, boss), defender
    /// mastery (accrual, branch locks, perks), research purchase and its battle modifiers, and loadout rules.
    /// </summary>
    public sealed class CampaignProgressionTests
    {
        private TestContent _c;
        private string _dir, _path;
        private CampaignDefinition _campaign;
        private ContentDatabase _db;
        private RegionDefinition _region, _teaser;
        private EncounterDefinition _m1, _m2, _m3;
        private DefenderDefinition _extra, _flyerKiller;

        [SetUp]
        public void SetUp()
        {
            _c = new TestContent();
            _dir = Path.Combine(Path.GetTempPath(), "vs_m4_tests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "save.json");

            _extra = _c.Make<DefenderDefinition>();
            _extra.id = "extra"; _extra.displayName = "Extra"; _extra.cost = 50; _extra.maxHealth = 200f;
            _extra.behaviour = DefenderBehaviourKind.LaneShooter; _extra.projectile = _c.bolt;
            _flyerKiller = _c.Make<DefenderDefinition>();
            _flyerKiller.id = "aa"; _flyerKiller.displayName = "Skyshot"; _flyerKiller.cost = 50;
            ProjectileDefinition aa = _c.Make<ProjectileDefinition>();
            aa.id = "aa_bolt"; aa.damage = 10f; aa.speed = 6f; aa.canHitAir = true;
            _flyerKiller.behaviour = DefenderBehaviourKind.LaneShooter; _flyerKiller.projectile = aa;

            _m1 = _c.encounter;
            _m1.id = "m1"; _m1.seedReward = 100; _m1.flawlessBonus = 30; _m1.challengeSeeds = 40; _m1.challengeEssence = 1;
            _m1.challenge = new MissionObjective { kind = MissionObjectiveKind.LoseAtMostDefenders, value = 0, text = "Lose nothing" };
            _m1.unlocksDefenders = new List<DefenderDefinition> { _extra };
            _m2 = Mission("m2", new List<DefenderDefinition> { _c.shooter, _extra });
            _m3 = Mission("m3", new List<DefenderDefinition> { _c.shooter });
            _region = _c.Make<RegionDefinition>();
            _region.id = "r1"; _region.missions = new List<EncounterDefinition> { _m1, _m2, _m3 };
            _teaser = _c.Make<RegionDefinition>();
            _teaser.id = "soon"; _teaser.comingSoon = true;
            _campaign = _c.Make<CampaignDefinition>();
            _campaign.regions = new List<RegionDefinition> { _region, _teaser };
            _campaign.startingDefenders = new List<DefenderDefinition> { _c.shooter, _c.blocker, _c.generator };
            _campaign.replaySeedFraction = 0.35f;
            _db = _c.Make<ContentDatabase>();
            _db.defenders = new List<DefenderDefinition> { _c.shooter, _c.blocker, _c.generator, _extra, _flyerKiller };
            _db.encounters = new List<EncounterDefinition> { _m1, _m2, _m3 };
            _db.campaign = _campaign;
        }

        [TearDown]
        public void TearDown()
        {
            _c.Destroy();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private EncounterDefinition Mission(string id, List<DefenderDefinition> roster)
        {
            EncounterDefinition e = _c.Make<EncounterDefinition>();
            e.id = id; e.displayName = id; e.battlefield = _c.field; e.startingVitality = 150; e.vitalityCap = 2000;
            e.skyDropInterval = 0f; e.prepTime = 5f; e.heartrootHealth = 3; e.roster = roster; e.seedReward = 60; e.flawlessBonus = 20;
            e.waves = new List<WaveDefinition> { _c.Wave(_c.walker, 1, 2) };
            return e;
        }

        private SaveData NewSave()
        {
            var s = new SaveData();
            CampaignRules.Reconcile(s, _campaign);
            return s;
        }

        private static MissionOutcome Win(int heart = 3, int heartMax = 3, int lost = 0, params DefenderUsage[] usage)
        {
            var o = new MissionOutcome { won = true, heart = heart, heartMax = heartMax, defendersLost = lost, elapsed = 200f, deckSize = 3 };
            o.usage.AddRange(usage);
            return o;
        }

        private ResearchDefinition Node(string id, ResearchEffect effect, float magnitude, int seeds = 10, int essence = 0, ResearchDefinition pre = null)
        {
            var r = _c.Make<ResearchDefinition>();
            r.id = id; r.displayName = id; r.effect = effect; r.magnitude = magnitude; r.seedCost = seeds; r.essenceCost = essence; r.prerequisite = pre;
            _campaign.research.Add(r);
            return r;
        }

        // ================================================================== save v3

        [Test]
        public void Save_CurrentVersionIsThree_NewSaveHasEmptyProgression()
        {
            Assert.AreEqual(3, SaveData.CurrentVersion);
            var d = new SaveData();
            Assert.AreEqual(0, d.mastery.Count);
            Assert.AreEqual(0, d.research.Count);
            Assert.AreEqual(-1, d.sanctuaryTierSeen);
        }

        [Test]
        public void Migration_V2ToV3_StarsFromFlags_NewFieldsReset_LegacyGrantPending()
        {
            File.WriteAllText(_path, "{\"version\":2,\"seeds\":300,\"essence\":4," +
                                     "\"encounters\":[{\"encounterId\":\"m1\",\"completed\":true,\"flawless\":true,\"attempts\":2,\"bossDefeats\":0}," +
                                     "{\"encounterId\":\"m2\",\"completed\":true,\"flawless\":false},{\"encounterId\":\"m3\",\"completed\":false}]," +
                                     "\"research\":[\"stray\"],\"mastery\":[{\"defenderId\":\"x\",\"xp\":999}],\"settings\":{\"autoCollect\":true}}");
            var s = new SaveService(_path);
            Assert.AreEqual(SaveLoadOutcome.Loaded, s.Load());
            SaveData d = s.Data;
            Assert.AreEqual(3, d.version);
            Assert.AreEqual(300, d.seeds);
            Assert.AreEqual(4, d.essence, "v2 Essence is kept");
            Assert.AreEqual(EncounterRecord.StarWin | EncounterRecord.StarFlawless, d.GetOrCreate("m1").stars);
            Assert.AreEqual(EncounterRecord.StarWin, d.GetOrCreate("m2").stars);
            Assert.AreEqual(0, d.GetOrCreate("m3").stars);
            Assert.AreEqual(1, d.GetOrCreate("m1").wins);
            Assert.AreEqual(0, d.research.Count, "fields a v2 save cannot have are dropped");
            Assert.AreEqual(0, d.mastery.Count);
            Assert.IsTrue(d.legacyGrantPending);
            Assert.IsTrue(d.settings.autoCollect);
        }

        [Test]
        public void Migration_V1ToV3_ChainsBothSteps()
        {
            File.WriteAllText(_path, "{\"version\":1,\"seeds\":50,\"essence\":9,\"encounters\":[{\"encounterId\":\"m1\",\"completed\":true,\"bossDefeats\":3}]}");
            var s = new SaveService(_path);
            Assert.AreEqual(SaveLoadOutcome.Loaded, s.Load());
            Assert.AreEqual(3, s.Data.version);
            Assert.AreEqual(0, s.Data.essence, "v1 -> v2 still zeroes Essence");
            Assert.AreEqual(0, s.Data.GetOrCreate("m1").bossDefeats);
            Assert.AreEqual(EncounterRecord.StarWin, s.Data.GetOrCreate("m1").stars);
            Assert.IsTrue(s.Data.legacyGrantPending);
        }

        [Test]
        public void Migration_NewerVersion_IsNotOverwritten()
        {
            File.WriteAllText(_path, "{\"version\":4,\"seeds\":77}");
            var s = new SaveService(_path);
            Assert.AreEqual(SaveLoadOutcome.NewerVersion, s.Load());
            Assert.IsFalse(s.Save());
            StringAssert.Contains("\"version\":4", File.ReadAllText(_path));
        }

        [Test]
        public void Reconcile_LegacyGrant_GivesMasteryOnce_AndUnlocksEarnedDefenders()
        {
            File.WriteAllText(_path, "{\"version\":2,\"encounters\":[{\"encounterId\":\"m1\",\"completed\":true},{\"encounterId\":\"m2\",\"completed\":true}]}");
            var s = new SaveService(_path);
            s.Load();
            List<string> added = CampaignRules.Reconcile(s.Data, _campaign);
            CollectionAssert.Contains(added, "extra", "m1's unlock reward is applied to an older save");
            Assert.IsTrue(s.Data.IsUnlocked("shooter"), "starting defenders");
            // shooter is in m1 and m2 rosters: 2 x 35.
            Assert.AreEqual(2 * CampaignRules.LegacyMasteryPerMission, s.Data.MasteryXp("shooter"));
            Assert.AreEqual(CampaignRules.LegacyMasteryPerMission, s.Data.MasteryXp("extra"));
            Assert.IsFalse(s.Data.legacyGrantPending);
            Assert.AreEqual(CampaignRules.SanctuaryTier(s.Data, _campaign), s.Data.sanctuaryTierSeen, "old progress is not celebrated again");
            CampaignRules.Reconcile(s.Data, _campaign);
            Assert.AreEqual(2 * CampaignRules.LegacyMasteryPerMission, s.Data.MasteryXp("shooter"), "granted once");
        }

        [Test]
        public void V3Progress_SurvivesRestart()
        {
            var s = new SaveService(_path);
            s.Load();
            CampaignRules.Reconcile(s.Data, _campaign);
            RewardRules.Grant(s.Data, _campaign, _db, _m1, Win(3, 3, 0, new DefenderUsage { defenderId = "shooter", planted = 3, evolved = 1 }));
            s.Data.research.Add("deep_roots");
            s.Data.SetLoadout("m2", new List<string> { "shooter", "extra" });
            s.Data.sanctuaryTierSeen = 1;
            Assert.IsTrue(s.Save());
            var s2 = new SaveService(_path); // relaunch
            Assert.AreEqual(SaveLoadOutcome.Loaded, s2.Load());
            SaveData d = s2.Data;
            Assert.IsTrue(d.IsCompleted("m1"));
            Assert.AreEqual(7, d.GetOrCreate("m1").stars);
            Assert.IsTrue(d.IsUnlocked("extra"));
            Assert.Greater(d.MasteryXp("shooter"), 0);
            CollectionAssert.AreEqual(new[] { "deep_roots" }, d.research);
            CollectionAssert.AreEqual(new[] { "shooter", "extra" }, d.LoadoutFor("m2"));
            CollectionAssert.AreEqual(new[] { "shooter", "extra" }, d.lastLoadout);
            Assert.AreEqual(1, d.sanctuaryTierSeen);
            Assert.IsFalse(d.legacyGrantPending);
        }

        // ================================================================== campaign rules

        [Test]
        public void Campaign_MissionsUnlockInOrder_TeaserNeverOpens()
        {
            SaveData s = NewSave();
            Assert.IsTrue(CampaignRules.IsMissionUnlocked(s, _campaign, _m1));
            Assert.IsFalse(CampaignRules.IsMissionUnlocked(s, _campaign, _m2));
            Assert.AreEqual(_m1, CampaignRules.NextMission(s, _campaign));
            s.GetOrCreate("m1").completed = true;
            Assert.IsTrue(CampaignRules.IsMissionUnlocked(s, _campaign, _m2));
            Assert.IsFalse(CampaignRules.IsMissionUnlocked(s, _campaign, _m3));
            Assert.AreEqual(_m2, CampaignRules.NextMission(s, _campaign));
            s.GetOrCreate("m2").completed = true;
            s.GetOrCreate("m3").completed = true;
            Assert.IsTrue(CampaignRules.RegionComplete(s, _region));
            Assert.IsNull(CampaignRules.NextMission(s, _campaign), "campaign complete");
            Assert.IsFalse(CampaignRules.RegionUnlocked(s, _campaign, _teaser), "coming-soon regions are teasers");
        }

        [Test]
        public void Campaign_SanctuaryTier_FollowsMissionsWon_DeckSlotsClamp()
        {
            SaveData s = NewSave();
            Assert.AreEqual(0, CampaignRules.SanctuaryTier(s, _campaign));
            s.GetOrCreate("m1").completed = true;
            Assert.AreEqual(1, CampaignRules.SanctuaryTier(s, _campaign));
            s.GetOrCreate("m2").completed = true;
            s.GetOrCreate("m3").completed = true;
            Assert.AreEqual(2, CampaignRules.SanctuaryTier(s, _campaign), "3 wins = tier 2");
            Assert.AreEqual(3, CampaignRules.DeckSlots(_m1, 0));
            Assert.AreEqual(4, CampaignRules.DeckSlots(_m1, 1));
            Assert.AreEqual(8, CampaignRules.DeckSlots(_m1, 20), "never more than 8 cards");
        }

        // ================================================================== rewards

        [Test]
        public void Rewards_FirstClear_PaysMissionStarsAndUnlocks()
        {
            SaveData s = NewSave();
            Assert.IsFalse(s.IsUnlocked("extra"));
            MissionRewards r = RewardRules.Grant(s, _campaign, _db, _m1, Win());
            Assert.IsTrue(r.firstClear);
            Assert.AreEqual(100, r.seedsMission);
            Assert.AreEqual(30, r.seedsFlawless);
            Assert.AreEqual(40, r.seedsChallenge, "lost 0 defenders: challenge met");
            Assert.AreEqual(1, r.essenceChallenge);
            Assert.AreEqual(170, s.seeds);
            Assert.AreEqual(1, s.essence);
            Assert.AreEqual(7, r.newStars);
            Assert.IsTrue(s.IsUnlocked("extra"));
            CollectionAssert.AreEqual(new[] { _extra }, r.unlocked);
            Assert.AreEqual(1, s.GetOrCreate("m1").attempts);
        }

        [Test]
        public void Rewards_Replay_PaysAShare_StarBonusesOnlyOnce()
        {
            SaveData s = NewSave();
            RewardRules.Grant(s, _campaign, _db, _m1, Win());
            int before = s.seeds, essence = s.essence;
            MissionRewards r = RewardRules.Grant(s, _campaign, _db, _m1, Win());
            Assert.IsTrue(r.replay);
            Assert.AreEqual(35, r.seedsMission, "35% of 100");
            Assert.AreEqual(0, r.seedsFlawless + r.seedsChallenge, "stars pay once");
            Assert.AreEqual(0, r.newStars);
            Assert.AreEqual(before + 35, s.seeds);
            Assert.AreEqual(essence, s.essence);
            Assert.AreEqual(0, r.unlocked.Count);
            Assert.AreEqual(2, s.GetOrCreate("m1").wins);
        }

        [Test]
        public void Rewards_LaterStarOnReplay_IsPaid()
        {
            SaveData s = NewSave();
            RewardRules.Grant(s, _campaign, _db, _m1, Win(2, 3, 2)); // hurt, lost 2: only the win star
            Assert.AreEqual(EncounterRecord.StarWin, s.GetOrCreate("m1").stars);
            MissionRewards r = RewardRules.Grant(s, _campaign, _db, _m1, Win(3, 3, 0));
            Assert.AreEqual(EncounterRecord.StarFlawless | EncounterRecord.StarChallenge, r.newStars);
            Assert.AreEqual(35 + 30 + 40, r.Seeds);
        }

        [Test]
        public void Rewards_Loss_NoSeeds_ButMasteryAndAttempt()
        {
            SaveData s = NewSave();
            var o = new MissionOutcome { won = false, heart = 0, heartMax = 3 };
            o.usage.Add(new DefenderUsage { defenderId = "shooter", planted = 2 });
            MissionRewards r = RewardRules.Grant(s, _campaign, _db, _m1, o);
            Assert.AreEqual(0, r.Seeds);
            Assert.AreEqual(0, s.seeds);
            Assert.IsFalse(s.IsCompleted("m1"));
            Assert.AreEqual(1, s.GetOrCreate("m1").attempts);
            Assert.AreEqual(MasteryRules.BattleGain(new DefenderUsage { planted = 2 }, false), s.MasteryXp("shooter"));
            Assert.Greater(s.MasteryXp("shooter"), 0, "a loss still teaches");
        }

        [Test]
        public void Rewards_Boss_FirstThenRepeatEssence_AndReplayBonusShare()
        {
            SaveData s = NewSave();
            MissionOutcome o = Win();
            o.bossDefeated = true; o.bossBonusSeeds = 150; o.bossEssenceFirst = 3; o.bossEssenceRepeat = 1;
            MissionRewards first = RewardRules.Grant(s, _campaign, _db, _m1, o);
            Assert.IsTrue(first.firstBossDefeat);
            Assert.AreEqual(150, first.seedsBoss);
            Assert.AreEqual(3, first.essenceBoss);
            MissionRewards again = RewardRules.Grant(s, _campaign, _db, _m1, o);
            Assert.IsFalse(again.firstBossDefeat);
            Assert.AreEqual(RewardRules.Replay(150, 0.35f), again.seedsBoss, "35% of 150");
            Assert.That(again.seedsBoss, Is.InRange(52, 53));
            Assert.AreEqual(1, again.essenceBoss);
            Assert.AreEqual(3 + 1 + 1, s.essence, "boss 3 + challenge 1 + repeat 1");
        }

        [Test]
        public void Objectives_EachKindEvaluates()
        {
            var o = new MissionOutcome { won = true, evolutions = 3, defendersLost = 2, deckSize = 4, defendersPlanted = 20, staggers = 2, elapsed = 250f };
            bool Met(MissionObjectiveKind k, int v) => RewardRules.ObjectiveMet(new MissionObjective { kind = k, value = v }, o);
            Assert.IsTrue(Met(MissionObjectiveKind.EvolveAtLeast, 3));
            Assert.IsFalse(Met(MissionObjectiveKind.EvolveAtLeast, 4));
            Assert.IsTrue(Met(MissionObjectiveKind.LoseAtMostDefenders, 2));
            Assert.IsFalse(Met(MissionObjectiveKind.LoseAtMostDefenders, 1));
            Assert.IsTrue(Met(MissionObjectiveKind.DeckSizeAtMost, 4));
            Assert.IsFalse(Met(MissionObjectiveKind.DeckSizeAtMost, 3));
            Assert.IsTrue(Met(MissionObjectiveKind.PlantAtMost, 20));
            Assert.IsFalse(Met(MissionObjectiveKind.PlantAtMost, 19));
            Assert.IsTrue(Met(MissionObjectiveKind.StaggerBossAtLeast, 2));
            Assert.IsFalse(Met(MissionObjectiveKind.StaggerBossAtLeast, 3));
            Assert.IsTrue(Met(MissionObjectiveKind.WinWithinSeconds, 250));
            Assert.IsFalse(Met(MissionObjectiveKind.WinWithinSeconds, 249));
            Assert.IsFalse(Met(MissionObjectiveKind.None, 0));
            o.won = false;
            Assert.IsFalse(Met(MissionObjectiveKind.EvolveAtLeast, 0), "a loss never meets a challenge");
        }

        // ================================================================== mastery

        [Test]
        public void Mastery_Levels_GainFormula_AndLevelUpListsBranches()
        {
            Assert.AreEqual(1, MasteryRules.LevelFor(0));
            Assert.AreEqual(1, MasteryRules.LevelFor(39));
            Assert.AreEqual(2, MasteryRules.LevelFor(40));
            Assert.AreEqual(3, MasteryRules.LevelFor(110));
            Assert.AreEqual(4, MasteryRules.LevelFor(10000));
            Assert.AreEqual(0, MasteryRules.BattleGain(new DefenderUsage { planted = 0, evolved = 0 }, true), "unused defenders gain nothing");
            Assert.AreEqual(10 + 3 * 2 + 12, MasteryRules.BattleGain(new DefenderUsage { planted = 2, evolved = 1 }, true));
            Assert.AreEqual(10 + 3 * 6, MasteryRules.BattleGain(new DefenderUsage { planted = 30 }, true), "plants are capped: no grinding by spam");
            Assert.AreEqual(10 + 3 * 6 + 12 * 3, MasteryRules.BattleGain(new DefenderUsage { planted = 9, evolved = 9 }, true), "evolutions are capped too");
            Assert.AreEqual(Mathf.RoundToInt((10 + 3 * 2) * 0.6f), MasteryRules.BattleGain(new DefenderUsage { planted = 2 }, false));
            Assert.AreEqual(Mathf.RoundToInt((10 + 3 * 2) * 1.5f), MasteryRules.BattleGain(new DefenderUsage { planted = 2 }, true, 1.5f));

            EvolutionDefinition a = _c.Make<EvolutionDefinition>(); a.id = "a"; a.displayName = "A"; a.masteryRequired = 1;
            EvolutionDefinition b = _c.Make<EvolutionDefinition>(); b.id = "b"; b.displayName = "B"; b.masteryRequired = 2;
            _c.shooter.evolutions = new List<EvolutionDefinition> { a, b };
            SaveData s = NewSave();
            s.AddMasteryXp("shooter", 30);
            MissionRewards r = RewardRules.Grant(s, _campaign, _db, _m1, Win(3, 3, 0, new DefenderUsage { defenderId = "shooter", planted = 1 }));
            MasteryGain g = r.mastery.Find(x => x.defenderId == "shooter");
            Assert.AreEqual(1, g.levelBefore);
            Assert.AreEqual(2, g.levelAfter);
            Assert.IsTrue(g.LeveledUp);
            CollectionAssert.AreEqual(new[] { b }, g.branchesUnlocked);
        }

        private BattleSimulation EvolvingSim(Dictionary<string, int> mastery, BattleModifiers mods = null)
        {
            EvolutionDefinition a = _c.Make<EvolutionDefinition>(); a.id = "a"; a.cost = 40; a.masteryRequired = 1;
            EvolutionDefinition b = _c.Make<EvolutionDefinition>(); b.id = "b"; b.cost = 40; b.masteryRequired = 2;
            _c.shooter.evolutions = new List<EvolutionDefinition> { a, b };
            _c.shooter.evolutionXp = 100f;
            var loadout = new BattleLoadout { masteryLevels = mastery, modifiers = mods ?? new BattleModifiers() };
            return new BattleSimulation(_c.encounter, 1, BattleOptions.Default, loadout);
        }

        [Test]
        public void Mastery_LocksBranchesInBattle_NeutralLoadoutOpensAll()
        {
            BattleSimulation sim = EvolvingSim(new Dictionary<string, int> { { "shooter", 1 } });
            Assert.AreEqual(DeployResult.Ok, sim.TryDeploy(0, new GridCoord(2, 1)));
            DefenderRuntime d = sim.DefenderAt(new GridCoord(2, 1));
            d.xp = 100f;
            int vit = sim.Vitality.Amount;
            Assert.IsTrue(sim.BranchUnlocked(d, 0));
            Assert.IsFalse(sim.BranchUnlocked(d, 1));
            Assert.AreEqual(EvolveResult.Locked, sim.TryEvolve(d, 1));
            Assert.AreEqual(vit, sim.Vitality.Amount, "a locked branch changes nothing");
            Assert.IsNull(d.evolution);
            Assert.AreEqual(EvolveResult.Ok, sim.TryEvolve(d, 0));

            BattleSimulation neutral = EvolvingSim(null);
            neutral.TryDeploy(0, new GridCoord(2, 1));
            DefenderRuntime n = neutral.DefenderAt(new GridCoord(2, 1));
            n.xp = 100f;
            Assert.AreEqual(EvolveResult.Ok, neutral.TryEvolve(n, 1), "no mastery system: every branch open (balance tests)");
        }

        [Test]
        public void Mastery_ChooserKeepsOpenOnLockedBranch()
        {
            BattleSimulation sim = EvolvingSim(new Dictionary<string, int> { { "shooter", 1 } });
            sim.TryDeploy(0, new GridCoord(2, 1));
            DefenderRuntime d = sim.DefenderAt(new GridCoord(2, 1));
            d.xp = 100f;
            var chooser = new EvolutionChooserState(sim);
            EvolveResult denied = EvolveResult.Ok;
            chooser.Denied += r => denied = r;
            Assert.IsTrue(chooser.Open(d));
            Assert.AreEqual(EvolveResult.Locked, chooser.Choose(1));
            Assert.AreEqual(EvolveResult.Locked, denied);
            Assert.IsTrue(chooser.IsOpen);
        }

        [Test]
        public void Mastery_Level3HeadStart_Level4CheaperEvolutions()
        {
            BattleSimulation sim = EvolvingSim(new Dictionary<string, int> { { "shooter", 4 } });
            sim.TryDeploy(0, new GridCoord(2, 1));
            DefenderRuntime d = sim.DefenderAt(new GridCoord(2, 1));
            Assert.AreEqual(25f, d.xp, 1e-3f, "starts with 25% growth");
            Assert.IsFalse(d.EvolutionReady);
            Assert.AreEqual(30, sim.EvolutionCost(d, 0), "40 x 0.75");
            BattleSimulation l2 = EvolvingSim(new Dictionary<string, int> { { "shooter", 2 } });
            l2.TryDeploy(0, new GridCoord(2, 1));
            Assert.AreEqual(0f, l2.DefenderAt(new GridCoord(2, 1)).xp);
            Assert.AreEqual(40, l2.EvolutionCost(l2.DefenderAt(new GridCoord(2, 1)), 0));
        }

        [Test]
        public void Usage_TracksPlantsUndoAndEvolutions_ForMastery()
        {
            BattleSimulation sim = EvolvingSim(null);
            sim.DebugGrantVitality(500);
            sim.TryDeploy(0, new GridCoord(1, 1));
            sim.TryDeploy(1, new GridCoord(2, 1));
            DefenderRuntime blocker = sim.DefenderAt(new GridCoord(2, 1));
            Assert.IsTrue(sim.TryUndo(blocker));
            DefenderRuntime d = sim.DefenderAt(new GridCoord(1, 1));
            d.xp = 100f;
            sim.TryEvolve(d, 0);
            MissionOutcome o = MissionOutcome.From(sim);
            Assert.AreEqual(1, o.usage.Count, "an undone planting does not count");
            Assert.AreEqual("shooter", o.usage[0].defenderId);
            Assert.AreEqual(1, o.usage[0].planted);
            Assert.AreEqual(1, o.usage[0].evolved);
            Assert.AreEqual(1, o.evolutions);
            Assert.AreEqual(3, o.deckSize);
        }

        // ================================================================== research

        [Test]
        public void Research_PurchaseIsAtomic_WithPrerequisitesAndCurrencies()
        {
            ResearchDefinition a = Node("a", ResearchEffect.StartingVitality, 25f, 60);
            ResearchDefinition b = Node("b", ResearchEffect.SkyDropRate, 0.15f, 0, 2, a);
            SaveData s = NewSave();
            s.seeds = 50;
            Assert.AreEqual(ResearchState.Unaffordable, ResearchRules.StateOf(s, a));
            Assert.AreEqual(ResearchResult.NotEnoughSeeds, ResearchRules.TryPurchase(s, a));
            Assert.AreEqual(50, s.seeds);
            Assert.AreEqual(ResearchResult.PrerequisiteMissing, ResearchRules.TryPurchase(s, b));
            Assert.AreEqual(ResearchState.Locked, ResearchRules.StateOf(s, b));
            s.seeds = 100;
            Assert.AreEqual(ResearchResult.Ok, ResearchRules.TryPurchase(s, a));
            Assert.AreEqual(40, s.seeds);
            Assert.AreEqual(ResearchResult.AlreadyOwned, ResearchRules.TryPurchase(s, a));
            Assert.AreEqual(40, s.seeds, "no double spend");
            Assert.AreEqual(ResearchResult.NotEnoughEssence, ResearchRules.TryPurchase(s, b));
            s.essence = 2;
            Assert.AreEqual(ResearchResult.Ok, ResearchRules.TryPurchase(s, b));
            Assert.AreEqual(0, s.essence);
            Assert.AreEqual(40, s.seeds, "Essence nodes cost no Seeds");
        }

        [Test]
        public void Research_NoneOwned_IsNeutral()
        {
            Node("a", ResearchEffect.StartingVitality, 25f);
            Node("b", ResearchEffect.CardCooldown, 0.12f);
            Assert.IsTrue(ResearchRules.Modifiers(NewSave(), _campaign.research).IsNeutral);
            Assert.AreEqual(0, ResearchRules.DeckSlotBonus(NewSave(), _campaign.research));
            Assert.AreEqual(1f, ResearchRules.MasteryXpMultiplier(NewSave(), _campaign.research));
        }

        private BattleSimulation WithResearch(params ResearchDefinition[] owned)
        {
            SaveData s = NewSave();
            foreach (ResearchDefinition r in owned) s.research.Add(r.id);
            return new BattleSimulation(_c.encounter, 1, BattleOptions.Default, LoadoutRules.Build(null, s, _db));
        }

        [Test]
        public void Research_EconomyEffects_ApplyToTheSimulation()
        {
            BattleSimulation sim = WithResearch(Node("v", ResearchEffect.StartingVitality, 25f), Node("h", ResearchEffect.HeartrootHealth, 1f),
                Node("p", ResearchEffect.PrepTime, 8f), Node("u", ResearchEffect.UndoGrace, 3f), Node("l", ResearchEffect.PickupLifetime, 4f));
            Assert.AreEqual(150 + 25, sim.Vitality.Amount);
            Assert.AreEqual(4, sim.HeartrootMaxHealth);
            Assert.AreEqual(4, sim.HeartrootHealth);
            Assert.AreEqual(5f + 8f, sim.Waves.PrepRemaining, 1e-4f);
            Assert.AreEqual(6f, sim.Options.undoGraceSeconds, 1e-4f);
            _c.encounter.pickupLifetime = 12f;
            sim.TryDeploy(2, new GridCoord(0, 0)); // generator drops a Sunmote
            TestContent.Run(sim, 6f);
            Assert.Greater(sim.Pickups.Count, 0);
            Assert.AreEqual(16f, sim.Pickups[0].lifetime, 1e-4f);
        }

        [Test]
        public void Research_SkyDropsComeSooner()
        {
            _c.encounter.skyDropInterval = 10f;
            _c.encounter.skyDropFirstDelay = 1f;
            int Drops(BattleSimulation s)
            {
                int n = 0;
                s.PickupSpawned += p => n++;
                TestContent.Run(s, 31.5f);
                return n;
            }
            int plain = Drops(WithResearch());
            int faster = Drops(WithResearch(Node("sky", ResearchEffect.SkyDropRate, 0.5f)));
            Assert.AreEqual(4, plain, "1, 11, 21, 31 s");
            Assert.AreEqual(7, faster, "every 5 s instead");
        }

        [Test]
        public void Research_CardCooldown_AndEvolutionGrowthAndCost()
        {
            EvolutionDefinition a = _c.Make<EvolutionDefinition>(); a.id = "a"; a.cost = 50;
            _c.shooter.evolutions = new List<EvolutionDefinition> { a };
            _c.shooter.evolutionXp = 100f;
            BattleSimulation sim = WithResearch(Node("cd", ResearchEffect.CardCooldown, 0.2f), Node("g", ResearchEffect.EvolutionGrowth, 0.15f),
                Node("c", ResearchEffect.EvolutionCost, 10f));
            sim.TryDeploy(0, new GridCoord(2, 1));
            Assert.AreEqual(5f * 0.8f, sim.Deck[0].cooldownRemaining, 1e-4f);
            DefenderRuntime d = sim.DefenderAt(new GridCoord(2, 1));
            Assert.AreEqual(85f, d.XpThreshold, 1e-3f);
            d.xp = 85f;
            Assert.IsTrue(d.EvolutionReady);
            Assert.AreEqual(40, sim.EvolutionCost(d, 0));
            int vit = sim.Vitality.Amount;
            Assert.AreEqual(EvolveResult.Ok, sim.TryEvolve(d, 0));
            Assert.AreEqual(vit - 40, sim.Vitality.Amount);
        }

        [Test]
        public void Research_HeartrootGift_DropsASunmoteOnABreach()
        {
            BattleSimulation sim = WithResearch(Node("gift", ResearchEffect.HeartrootGift, 75f));
            var spawned = new List<PickupRuntime>();
            sim.PickupSpawned += p => spawned.Add(p);
            sim.HoldFirstWave = false;
            TestContent.Run(sim, 60f); // the lone walker breaches the empty lane
            Assert.Less(sim.HeartrootHealth, sim.HeartrootMaxHealth);
            Assert.IsTrue(spawned.Exists(p => p.amount == 75), "a 75 Vitality Sunmote");
            BattleSimulation plain = WithResearch();
            int n = 0;
            plain.PickupSpawned += p => n++;
            TestContent.Run(plain, 60f);
            Assert.AreEqual(0, n);
        }

        [Test]
        public void Research_DeckSlotAndMasteryXp_AreMetaEffects()
        {
            ResearchDefinition slot = Node("slot", ResearchEffect.DeckSlot, 1f);
            ResearchDefinition keen = Node("keen", ResearchEffect.MasteryXp, 0.5f);
            SaveData s = NewSave();
            s.research.Add(slot.id);
            s.research.Add(keen.id);
            Assert.AreEqual(1, ResearchRules.DeckSlotBonus(s, _campaign.research));
            Assert.AreEqual(1.5f, ResearchRules.MasteryXpMultiplier(s, _campaign.research), 1e-4f);
            Assert.IsTrue(ResearchRules.Modifiers(s, _campaign.research).IsNeutral, "not battle rules");
            MissionRewards r = RewardRules.Grant(s, _campaign, _db, _m1, Win(3, 3, 0, new DefenderUsage { defenderId = "shooter", planted = 2 }));
            Assert.AreEqual(Mathf.RoundToInt(16 * 1.5f), r.mastery[0].gained);
        }

        // ================================================================== loadout

        [Test]
        public void Loadout_Validate()
        {
            SaveData s = NewSave();
            Assert.AreEqual(LoadoutIssue.Empty, LoadoutRules.Validate(new List<DefenderDefinition>(), 3, s));
            Assert.AreEqual(LoadoutIssue.TooMany, LoadoutRules.Validate(new List<DefenderDefinition> { _c.shooter, _c.blocker, _c.generator }, 2, s));
            Assert.AreEqual(LoadoutIssue.Duplicate, LoadoutRules.Validate(new List<DefenderDefinition> { _c.shooter, _c.shooter }, 3, s));
            Assert.AreEqual(LoadoutIssue.NotUnlocked, LoadoutRules.Validate(new List<DefenderDefinition> { _c.shooter, _extra }, 3, s));
            Assert.AreEqual(LoadoutIssue.Missing, LoadoutRules.Validate(new List<DefenderDefinition> { null }, 3, s));
            Assert.AreEqual(LoadoutIssue.Ok, LoadoutRules.Validate(new List<DefenderDefinition> { _c.generator, _c.shooter }, 3, s));
        }

        [Test]
        public void Loadout_DefaultDeck_SavedThenRecommendedThenLastThenCollection()
        {
            SaveData s = NewSave();
            // m2 recommends shooter + extra, extra is locked: recommended (owned) then filled from the collection.
            List<DefenderDefinition> deck = LoadoutRules.DefaultDeck(_m2, s, _db, 3);
            Assert.AreEqual(_c.shooter, deck[0]);
            Assert.AreEqual(3, deck.Count);
            CollectionAssert.DoesNotContain(deck, _extra);
            s.Unlock("extra");
            s.lastLoadout = new List<string> { "generator" };
            deck = LoadoutRules.DefaultDeck(_m2, s, _db, 3);
            CollectionAssert.AreEqual(new[] { _c.shooter, _extra, _c.generator }, deck, "recommended, then last used");
            s.SetLoadout("m2", new List<string> { "blocker", "aa" }); // aa is not owned: dropped
            deck = LoadoutRules.DefaultDeck(_m2, s, _db, 3);
            CollectionAssert.AreEqual(new[] { _c.blocker }, deck, "the saved deck wins, minus anything not owned");
            Assert.AreEqual(2, LoadoutRules.DefaultDeck(_m1, NewSave(), _db, 2).Count, "never more than the slots");
        }

        [Test]
        public void Loadout_DeckIsWhatTheBattleUses()
        {
            SaveData s = NewSave();
            BattleLoadout l = LoadoutRules.Build(new List<DefenderDefinition> { _c.generator, _c.shooter }, s, _db);
            var sim = new BattleSimulation(_m1, 1, BattleOptions.Default, l);
            Assert.AreEqual(2, sim.Deck.Count);
            Assert.AreEqual(_c.generator, sim.Deck[0].Definition);
            Assert.AreEqual(3, new BattleSimulation(_m1, 1, BattleOptions.Default, null).Deck.Count, "neutral: the authored roster");
        }

        [Test]
        public void Loadout_Advice_FlagsGapsAndNamesCounters()
        {
            EnemyDefinition flyer = _c.Make<EnemyDefinition>();
            flyer.id = "glider"; flyer.displayName = "Glider"; flyer.movement = EnemyMovementKind.Flying; flyer.maxHealth = 50f;
            _m2.waves = new List<WaveDefinition> { _c.Wave(flyer, 1, 2) };
            var collection = new List<DefenderDefinition> { _c.shooter, _c.generator, _flyerKiller };
            List<string> tips = LoadoutRules.Advice(_m2, new List<DefenderDefinition> { _c.blocker }, collection);
            Assert.IsTrue(tips.Exists(t => t.Contains("shoots")), "no attacker");
            Assert.IsTrue(tips.Exists(t => t.Contains("generator")), "no generator");
            Assert.IsTrue(tips.Exists(t => t.Contains("Glider") && t.Contains("Skyshot")), "anti-air gap, counter named");
            tips = LoadoutRules.Advice(_m2, new List<DefenderDefinition> { _c.generator, _flyerKiller }, collection);
            Assert.AreEqual(0, tips.Count, string.Join(" | ", tips));
            _m2.waves = new List<WaveDefinition> { _c.Wave(_c.armored, 1, 2) };
            tips = LoadoutRules.Advice(_m2, new List<DefenderDefinition> { _c.generator, _c.shooter }, collection);
            Assert.AreEqual(0, tips.Count, "no piercing defender owned yet: nothing actionable to say");
        }
    }
}
