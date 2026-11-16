using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VerdantSiege.Data;

namespace VerdantSiege.EditorTools
{
    /// <summary>
    /// Milestone 4 content: the campaign (regions and world-map layout), mission objectives and defender unlocks, the
    /// Seed Conservatory research tree, and the mastery level each evolution branch needs. Re-running updates the
    /// assets in place (stable GUIDs). Values are tuned against the shared scripted player (see TD-30..TD-34).
    /// </summary>
    public static class ContentBuilderCampaign
    {
        private const string Data = EditorAssets.Root + "Data/";

        public static CampaignDefinition Build(ContentDatabase db)
        {
            DefenderDefinition D(string id)
            {
                DefenderDefinition d = db.FindDefender(id);
                if (d == null) Debug.LogError("[VS Campaign] missing defender " + id);
                return d;
            }
            EncounterDefinition M(string id)
            {
                EncounterDefinition e = db.FindEncounter(id);
                if (e == null) Debug.LogError("[VS Campaign] missing encounter " + id);
                return e;
            }

            // ---------------------------------------------------------------- evolution branches gated by mastery
            // Level 1: the most general branch. Level 2 / 3: the specialist ones (horizontal options, not more power).
            var mastery = new Dictionary<string, int>
            {
                { "sunburst", 1 }, { "solar_cannon", 2 }, { "solar_network", 3 },
                { "abundant", 1 }, { "capacitor", 2 },
                { "thornhide", 1 }, { "heartwood", 2 }, { "bramble_snare", 3 },
                { "bramble_volley", 1 }, { "ironbark_spike", 2 },
                { "deep_freeze", 1 }, { "glacial_shards", 2 },
                { "wildfire", 1 }, { "firestorm", 2 },
                { "cyclone", 1 }, { "gale", 2 },
                { "quick_digest", 1 }, { "grasping_vines", 2 },
                { "wide_bloom", 1 }, { "living_sap", 2 },
                { "bramble_web", 1 }, { "quickroot", 2 },
                { "tempest", 1 }, { "thunderhead", 2 },
                { "hall_of_mirrors", 1 }, { "prism_guard", 2 },
            };
            foreach (DefenderDefinition d in db.defenders)
                foreach (EvolutionDefinition e in d.evolutions)
                {
                    if (e == null) continue;
                    e.masteryRequired = mastery.TryGetValue(e.id, out int l) ? l : 1;
                    EditorUtility.SetDirty(e);
                }

            // ---------------------------------------------------------------- missions: map, challenge, unlocks
            void Mission(string id, float x, float y, MissionObjectiveKind kind, int value, string text, int seeds, params string[] unlocks)
            {
                EncounterDefinition e = M(id);
                if (e == null) return;
                e.mapPosition = new Vector2(x, y);
                e.challenge = new MissionObjective { kind = kind, value = value, text = text };
                e.challengeSeeds = seeds;
                e.challengeEssence = 1;
                e.unlocksDefenders = new List<DefenderDefinition>();
                foreach (string u in unlocks) { DefenderDefinition d = D(u); if (d != null) e.unlocksDefenders.Add(d); }
                EditorUtility.SetDirty(e);
            }
            Mission("vh_01", 0.09f, 0.37f, MissionObjectiveKind.EvolveAtLeast, 2, "Evolve 2 defenders", 40, "thorncaster");
            Mission("vh_02", 0.23f, 0.66f, MissionObjectiveKind.PlantAtMost, 20, "Win planting 20 defenders or fewer", 45, "frost_fern");
            Mission("vh_03", 0.37f, 0.37f, MissionObjectiveKind.DeckSizeAtMost, 4, "Win with 4 or fewer cards", 50, "ember_lily", "burrowroot", "snapjaw_vine");
            Mission("vh_04", 0.51f, 0.68f, MissionObjectiveKind.LoseAtMostDefenders, 1, "Lose no more than 1 defender", 55, "whirlseed", "storm_orchid", "mirrorleaf");
            Mission("vh_05", 0.64f, 0.38f, MissionObjectiveKind.PlantAtMost, 26, "Win planting 26 defenders or fewer", 60, "mender_moss");
            Mission("vh_06", 0.77f, 0.68f, MissionObjectiveKind.DeckSizeAtMost, 6, "Win with 6 or fewer cards", 65);
            Mission("vh_07", 0.90f, 0.46f, MissionObjectiveKind.StaggerBossAtLeast, 3, "Stagger the Root Maw 3 times", 80);

            // ---------------------------------------------------------------- regions
            RegionDefinition Region(string file, string id, string name, string tagline, Color accent, Color ground, bool soon, string hint, params string[] missions)
            {
                var r = EditorAssets.LoadOrCreateSO<RegionDefinition>(Data + "Campaign/" + file + ".asset");
                r.id = id; r.displayName = name; r.tagline = tagline; r.accentColor = accent; r.groundColor = ground;
                r.comingSoon = soon; r.lockedHint = hint;
                r.missions = new List<EncounterDefinition>();
                foreach (string m in missions) { EncounterDefinition e = M(m); if (e != null) r.missions.Add(e); }
                EditorUtility.SetDirty(r);
                return r;
            }
            EditorAssets.EnsureFolder(Data + "Campaign");
            EditorAssets.EnsureFolder(Data + "Research");
            RegionDefinition heartlands = Region("Region_VerdantHeartlands", "verdant_heartlands", "Verdant Heartlands",
                "Meadows, old stone paths and the first roots of the Blight.", new Color(0.55f, 0.85f, 0.4f), new Color(0.3f, 0.52f, 0.24f), false, "",
                "vh_01", "vh_02", "vh_03", "vh_04", "vh_05", "vh_06", "vh_07");
            RegionDefinition ember = Region("Region_Emberwild", "emberwild", "The Emberwild",
                "Volcanic soil, glowing roots and lava channels.", new Color(1f, 0.5f, 0.25f), new Color(0.42f, 0.2f, 0.14f), true,
                "Beyond the Root Maw's lair the soil runs hot. Coming in a future update.");
            RegionDefinition frost = Region("Region_FrostveilTundra", "frostveil_tundra", "Frostveil Tundra",
                "Snow-covered ruins under the aurora.", new Color(0.6f, 0.85f, 1f), new Color(0.32f, 0.42f, 0.52f), true,
                "The far north waits under the aurora. Coming later.");

            // ---------------------------------------------------------------- research (Seed Conservatory)
            var nodes = new List<ResearchDefinition>();
            ResearchDefinition Node(string id, string name, string desc, int branch, int tier, int seeds, int essence, ResearchDefinition pre,
                                    ResearchEffect effect, float magnitude, string glyph)
            {
                var r = EditorAssets.LoadOrCreateSO<ResearchDefinition>(Data + "Research/Research_" + id + ".asset");
                r.id = id; r.displayName = name; r.description = desc; r.branch = branch; r.tier = tier;
                r.seedCost = seeds; r.essenceCost = essence; r.prerequisite = pre; r.effect = effect; r.magnitude = magnitude; r.glyph = glyph;
                EditorUtility.SetDirty(r);
                nodes.Add(r);
                return r;
            }
            // Sunlight: economy and tempo.
            ResearchDefinition deepRoots = Node("deep_roots", "Deep Roots", "Start every battle with +25 Vitality.", 0, 0, 60, 0, null, ResearchEffect.StartingVitality, 25f, "DR");
            ResearchDefinition lingering = Node("lingering_light", "Lingering Light", "Sunmotes stay collectable 4 s longer.", 0, 1, 100, 0, deepRoots, ResearchEffect.PickupLifetime, 4f, "LL");
            ResearchDefinition glades = Node("sunlit_glades", "Sunlit Glades", "Sunlight pools last 50% longer.", 0, 2, 150, 0, lingering, ResearchEffect.SunlightPoolDuration, 0.5f, "SG");
            ResearchDefinition sky = Node("sky_harvest", "Sky Harvest", "Sunlight falls from the sky 15% more often.", 0, 3, 0, 2, glades, ResearchEffect.SkyDropRate, 0.15f, "SH");
            Node("heartroot_gift", "Heartroot's Gift", "When the Heartroot is hurt it sheds a 75 Vitality Sunmote.", 0, 4, 0, 3, sky, ResearchEffect.HeartrootGift, 75f, "HG");
            // Cultivation: deck and planting.
            ResearchDefinition second = Node("second_thoughts", "Second Thoughts", "Undo a fresh planting for a full refund for 6 s (was 3 s).", 1, 0, 60, 0, null, ResearchEffect.UndoGrace, 3f, "ST");
            ResearchDefinition vigil = Node("verdant_vigil", "Verdant Vigil", "8 more seconds to plant before the first wave.", 1, 1, 120, 0, second, ResearchEffect.PrepTime, 8f, "VV");
            ResearchDefinition sprout = Node("quick_sprouting", "Quick Sprouting", "Card cooldowns are 12% shorter.", 1, 2, 180, 0, vigil, ResearchEffect.CardCooldown, 0.12f, "QS");
            ResearchDefinition satchel = Node("seed_satchel", "Seed Satchel", "Bring one more card into every battle (max 8).", 1, 3, 220, 0, sprout, ResearchEffect.DeckSlot, 1f, "+1");
            Node("heartwood_bark", "Heartwood Bark", "The Heartroot can take one more hit.", 1, 4, 0, 2, satchel, ResearchEffect.HeartrootHealth, 1f, "HB");
            // Evolution: growth and mastery.
            ResearchDefinition growth = Node("swift_growth", "Swift Growth", "Defenders need 15% less growth to evolve.", 2, 0, 100, 0, null, ResearchEffect.EvolutionGrowth, 0.15f, "SW");
            ResearchDefinition thrifty = Node("thrifty_grafting", "Thrifty Grafting", "Evolutions cost 10 less Vitality.", 2, 1, 160, 0, growth, ResearchEffect.EvolutionCost, 10f, "TG");
            ResearchDefinition keen = Node("keen_study", "Keen Study", "Defenders earn 50% more mastery.", 2, 2, 0, 1, thrifty, ResearchEffect.MasteryXp, 0.5f, "KS");
            Node("rootbound_recall", "Rootbound Recall", "Plants uprooted by a boss refund their full cost.", 2, 3, 0, 2, keen, ResearchEffect.UprootRefund, 0.5f, "RR");

            var c = EditorAssets.LoadOrCreateSO<CampaignDefinition>(Data + "Campaign/Campaign.asset");
            c.regions = new List<RegionDefinition> { heartlands, ember, frost };
            c.startingDefenders = new List<DefenderDefinition> { D("glowcap"), D("sunspore"), D("bulwark_bloom") };
            c.research = nodes;
            c.researchBranchNames = new[] { "Sunlight", "Cultivation", "Evolution" };
            c.replaySeedFraction = 0.35f;
            EditorUtility.SetDirty(c);
            db.campaign = c;
            EditorUtility.SetDirty(db);
            return c;
        }
    }
}
