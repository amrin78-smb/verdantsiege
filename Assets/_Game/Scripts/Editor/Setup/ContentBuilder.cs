using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VerdantSiege.Combat;
using VerdantSiege.Data;

namespace VerdantSiege.EditorTools
{
    /// <summary>
    /// Authored Milestone 1 content as ScriptableObject assets (values from GDD Appendix A, tuned by the
    /// automated balance tests). Re-running updates assets in place (GUIDs stable), so references survive.
    /// </summary>
    public static class ContentBuilder
    {
        private const string Data = EditorAssets.Root + "Data/";

        public static ContentDatabase Build(Dictionary<string, GameObject> prefabs, Dictionary<string, Sprite> icons)
        {
            // ---- statuses (framework content; first used by Frost Fern / Ember Lily in Milestone 3)
            var chill = EditorAssets.LoadOrCreateSO<StatusEffectDefinition>(Data + "StatusEffects/Status_Chill.asset");
            chill.id = "chill"; chill.displayName = "Chilled"; chill.kind = StatusKind.Slow; chill.duration = 3f; chill.magnitude = 0.35f;
            chill.stacking = StatusStacking.StackIntensity; chill.maxStacks = 2; chill.damageType = DamageType.Frost; chill.tint = new Color(0.55f, 0.85f, 1f);
            var burn = EditorAssets.LoadOrCreateSO<StatusEffectDefinition>(Data + "StatusEffects/Status_Burn.asset");
            burn.id = "burn"; burn.displayName = "Burning"; burn.kind = StatusKind.DamageOverTime; burn.duration = 3f; burn.magnitude = 8f;
            burn.tickInterval = 0.5f; burn.stacking = StatusStacking.RefreshDuration; burn.damageType = DamageType.Fire; burn.tint = new Color(1f, 0.5f, 0.2f);

            // ---- projectiles
            var seedBolt = EditorAssets.LoadOrCreateSO<ProjectileDefinition>(Data + "Projectiles/Projectile_SeedBolt.asset");
            seedBolt.id = "seed_bolt"; seedBolt.speed = 7.5f; seedBolt.damage = 25f; seedBolt.damageType = DamageType.Physical;
            seedBolt.pierceCount = 1; seedBolt.splashRadius = 0f; seedBolt.knockback = 0f; seedBolt.statusEffect = null;
            seedBolt.prefab = Get(prefabs, "SeedBolt"); seedBolt.trailColor = new Color(1f, 0.85f, 0.35f);

            // ---- defenders
            var sunspore = EditorAssets.LoadOrCreateSO<DefenderDefinition>(Data + "Defenders/Defender_Sunspore.asset");
            sunspore.id = "sunspore"; sunspore.displayName = "Sunspore"; sunspore.role = DefenderRole.Artillery;
            sunspore.description = "Fires sun-warmed seed bolts down its lane at the first Blight it sees.";
            sunspore.strengths = "Reliable lane damage"; sunspore.weaknesses = "Fragile; armour plating soaks its bolts";
            sunspore.accentColor = new Color(1f, 0.62f, 0.2f);
            sunspore.cost = 100; sunspore.cooldown = 5f; sunspore.startingCooldown = 0f; sunspore.maxHealth = 300f;
            sunspore.behaviour = DefenderBehaviourKind.LaneShooter; sunspore.projectile = seedBolt;
            sunspore.attackInterval = 1.3f; sunspore.firstAttackDelay = 0.5f; sunspore.range = 0f;
            sunspore.prefab = Get(prefabs, "Sunspore"); sunspore.icon = Get(icons, "Sunspore");

            var bulwark = EditorAssets.LoadOrCreateSO<DefenderDefinition>(Data + "Defenders/Defender_BulwarkBloom.asset");
            bulwark.id = "bulwark_bloom"; bulwark.displayName = "Bulwark Bloom"; bulwark.role = DefenderRole.Guardian;
            bulwark.description = "A dome of bark-plated petals. It cannot attack, but the Blight must chew through it to pass.";
            bulwark.strengths = "Blocks and absorbs"; bulwark.weaknesses = "Deals no damage";
            bulwark.accentColor = new Color(0.55f, 0.72f, 0.35f);
            bulwark.cost = 125; bulwark.cooldown = 12f; bulwark.startingCooldown = 10f; bulwark.maxHealth = 4000f;
            bulwark.behaviour = DefenderBehaviourKind.Blocker; bulwark.projectile = null;
            bulwark.prefab = Get(prefabs, "BulwarkBloom"); bulwark.icon = Get(icons, "BulwarkBloom");

            var glowcap = EditorAssets.LoadOrCreateSO<DefenderDefinition>(Data + "Defenders/Defender_Glowcap.asset");
            glowcap.id = "glowcap"; glowcap.displayName = "Glowcap"; glowcap.role = DefenderRole.Generator;
            glowcap.description = "Gathers light and releases a Sunmote worth 25 Vitality every 10 seconds. Every fourth bloom is a double burst.";
            glowcap.strengths = "Generates Vitality"; glowcap.weaknesses = "No defence of its own";
            glowcap.accentColor = new Color(0.3f, 0.9f, 0.85f);
            glowcap.cost = 75; glowcap.cooldown = 8f; glowcap.startingCooldown = 0f; glowcap.maxHealth = 300f;
            glowcap.behaviour = DefenderBehaviourKind.Generator;
            glowcap.generateFirstDelay = 4f; glowcap.generateInterval = 10f; glowcap.generateAmount = 25; glowcap.burstEvery = 4; glowcap.burstAmount = 50;
            glowcap.prefab = Get(prefabs, "Glowcap"); glowcap.icon = Get(icons, "Glowcap");

            // ---- enemies
            var drifter = Enemy("Enemy_Drifter", "drifter", "Drifter", "A shambling husk of Blight. Slow, steady and hungry for roots.",
                "Any steady lane damage stops it.", 150f, 0f, 0.22f, 1f, 0.5f, 30f, 1f, 1, 1, new Color(0.62f, 0.35f, 0.7f));
            drifter.vitalityDrop = 15; drifter.prefab = Get(prefabs, "Drifter"); drifter.icon = Get(icons, "Drifter");
            var skitter = Enemy("Enemy_Skitter", "skitter", "Skitter", "A fast, fragile rot-beetle that rushes past slow defences.",
                "Shoot it early or block it with a Bulwark Bloom.", 70f, 0f, 0.5f, 1f, 0.45f, 12f, 0.6f, 1, 1, new Color(0.9f, 0.4f, 0.6f));
            skitter.vitalityDrop = 10; skitter.prefab = Get(prefabs, "Skitter"); skitter.icon = Get(icons, "Skitter");
            var brute = Enemy("Enemy_PlatedBrute", "plated_brute", "Plated Brute", "A hulking beast in bone-and-bark plating. The plating absorbs hits until it shatters; then it charges.",
                "Stall it with a Bulwark Bloom while several Sunspores break the plating.", 260f, 300f, 0.15f, 1.35f, 0.6f, 60f, 1.2f, 2, 4, new Color(0.8f, 0.72f, 0.55f));
            brute.vitalityDrop = 50; brute.prefab = Get(prefabs, "PlatedBrute"); brute.icon = Get(icons, "PlatedBrute");

            // ---- battlefield
            var field = EditorAssets.LoadOrCreateSO<BattlefieldDefinition>(Data + "Biomes/Battlefield_Heartlands_5x9.asset");
            field.id = "heartlands_5x9"; field.lanes = 5; field.columns = 9; field.specialCells = new List<SpecialCell>();

            // ---- encounter 1: First Roots (teaches economy, shooters, then armour)
            var roster = new List<DefenderDefinition> { glowcap, sunspore, bulwark };
            var e1 = EditorAssets.LoadOrCreateSO<EncounterDefinition>(Data + "Missions/Encounter_VH01_FirstRoots.asset");
            e1.id = "vh_01"; e1.displayName = "First Roots";
            e1.briefing = "Grow Glowcaps for Vitality, then Sunspores to hold every lane.";
            e1.biomeId = "verdant_heartlands"; e1.battlefield = field; e1.heartrootHealth = 5;
            e1.startingVitality = 225; e1.vitalityCap = 2000; e1.skyDropInterval = 6.5f; e1.skyDropFirstDelay = 3f; e1.skyDropAmount = 25; e1.pickupLifetime = 12f;
            e1.sunlightPoolInterval = 24f; e1.sunlightPoolDuration = 10f; e1.sunlightPoolCount = 2; e1.sunlightBoost = 1.6f;
            e1.prepTime = 22f; e1.interWaveGap = 5f; e1.fixedSeed = 0; e1.roster = roster; e1.seedReward = 50; e1.flawlessBonus = 25;
            e1.waves = new List<WaveDefinition>
            {
                Wave("VH01_W1", "Scouts", false, 40f, G(drifter, 1, 2, 0f, 0f), G(drifter, 1, -1, 14f, 0f)),
                Wave("VH01_W2", "Drift", false, 45f, G(drifter, 4, -1, 0f, 7f)),
                Wave("VH01_W3", "Skittering", false, 45f, G(drifter, 3, -1, 0f, 8f), G(skitter, 3, -1, 9f, 5f)),
                Wave("VH01_W4", "The First Swell", true, 50f, G(drifter, 6, -1, 0f, 3.5f), G(skitter, 3, -1, 8f, 3f)),
                Wave("VH01_W5", "Iron Shell", false, 55f, G(brute, 1, -1, 0f, 0f), G(drifter, 4, -1, 6f, 5f), G(skitter, 2, -1, 14f, 4f)),
                Wave("VH01_W6", "Blight Tide", true, 80f, G(drifter, 8, -1, 0f, 2.6f), G(skitter, 4, -1, 8f, 2.5f), G(brute, 2, -1, 12f, 11f)),
            };

            // ---- encounter 2: Plated March (armour-focused follow-up)
            var e2 = EditorAssets.LoadOrCreateSO<EncounterDefinition>(Data + "Missions/Encounter_VH02_PlatedMarch.asset");
            e2.id = "vh_02"; e2.displayName = "Plated March";
            e2.briefing = "Brutes lead this assault. Wall them with Bulwark Blooms and focus fire.";
            e2.biomeId = "verdant_heartlands"; e2.battlefield = field; e2.heartrootHealth = 5;
            e2.startingVitality = 225; e2.vitalityCap = 2000; e2.skyDropInterval = 7f; e2.skyDropFirstDelay = 3f; e2.skyDropAmount = 25; e2.pickupLifetime = 12f;
            e2.sunlightPoolInterval = 22f; e2.sunlightPoolDuration = 10f; e2.sunlightPoolCount = 2; e2.sunlightBoost = 1.6f;
            e2.prepTime = 22f; e2.interWaveGap = 5f; e2.fixedSeed = 0; e2.roster = roster; e2.seedReward = 75; e2.flawlessBonus = 35;
            e2.waves = new List<WaveDefinition>
            {
                Wave("VH02_W1", "Vanguard", false, 45f, G(drifter, 3, -1, 0f, 8f)),
                Wave("VH02_W2", "First Plate", false, 55f, G(brute, 1, -1, 0f, 0f), G(skitter, 2, -1, 14f, 6f)),
                Wave("VH02_W3", "Pincer", false, 50f, G(drifter, 5, -1, 0f, 5f), G(skitter, 3, -1, 12f, 4f)),
                Wave("VH02_W4", "The Shield Wall", true, 60f, G(brute, 2, -1, 0f, 9f), G(drifter, 5, -1, 4f, 3.5f)),
                Wave("VH02_W5", "Rush", false, 45f, G(skitter, 8, -1, 0f, 1.8f), G(drifter, 3, -1, 6f, 4f)),
                Wave("VH02_W6", "Iron Tide", true, 90f, G(brute, 3, -1, 0f, 8f), G(drifter, 8, -1, 4f, 2.6f), G(skitter, 5, -1, 10f, 2.4f)),
            };

            foreach (Object o in new Object[] { chill, burn, seedBolt, sunspore, bulwark, glowcap, drifter, skitter, brute, field, e1, e2 }) EditorUtility.SetDirty(o);

            // ---- Milestone 3 content (ContentBuilderM3.cs) ------------------------------------------------
            ContentBuilderM3.Result m3 = ContentBuilderM3.Build(new ContentBuilderM3.Base
            {
                glowcap = glowcap, sunspore = sunspore, bulwark = bulwark, drifter = drifter, skitter = skitter, brute = brute,
                burn = burn, field = field
            }, prefabs, icons);
            // ---- end Milestone 3 ----------------------------------------------------------------------------

            var db = EditorAssets.LoadOrCreateSO<ContentDatabase>(Data + "ContentDatabase.asset");
            db.defenders = new List<DefenderDefinition> { sunspore, bulwark, glowcap };
            db.enemies = new List<EnemyDefinition> { drifter, skitter, brute };
            db.encounters = new List<EncounterDefinition> { e1, e2 };
            db.defenders.AddRange(m3.defenders);   // M3
            db.enemies.AddRange(m3.enemies);       // M3
            db.encounters.AddRange(m3.encounters); // M3
            db.bosses = new List<BossDefinition>(m3.bosses); // M3
            ContentBuilderEvolution.Build(db.defenders, burn); // Living Evolution branches (ContentBuilderEvolution.cs)
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            return db;
        }

        private static T Get<T>(Dictionary<string, T> d, string key) where T : Object => d != null && d.TryGetValue(key, out T v) ? v : null;

        private static EnemyDefinition Enemy(string file, string id, string name, string desc, string counter, float hp, float armor,
                                             float speed, float afterBreak, float body, float bite, float biteInterval, int breach, int threat, Color accent)
        {
            var e = EditorAssets.LoadOrCreateSO<EnemyDefinition>(Data + "Enemies/" + file + ".asset");
            e.id = id; e.displayName = name; e.description = desc; e.counterHint = counter;
            e.maxHealth = hp; e.armorHealth = armor; e.resistances = new List<DamageModifier>();
            e.moveSpeed = speed; e.speedMultiplierAfterArmorBreak = afterBreak; e.bodyLength = body;
            e.biteDamage = bite; e.biteInterval = biteInterval; e.breachDamage = breach; e.threatCost = threat; e.accentColor = accent;
            return e;
        }

        private static SpawnGroup G(EnemyDefinition e, int count, int lane, float delay, float interval) =>
            new SpawnGroup { enemy = e, count = count, lane = lane, startDelay = delay, interval = interval };

        private static WaveDefinition Wave(string file, string label, bool major, float maxDuration, params SpawnGroup[] groups)
        {
            var w = EditorAssets.LoadOrCreateSO<WaveDefinition>(Data + "Waves/Wave_" + file + ".asset");
            w.label = label; w.major = major; w.maxDuration = maxDuration; w.groups = new List<SpawnGroup>(groups);
            EditorUtility.SetDirty(w);
            return w;
        }
    }
}
