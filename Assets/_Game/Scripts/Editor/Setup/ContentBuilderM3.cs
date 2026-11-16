using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VerdantSiege.Combat;
using VerdantSiege.Data;

namespace VerdantSiege.EditorTools
{
    /// <summary>
    /// Milestone 3 content: the remaining nine defenders (GDD Appendix A costs/cooldowns), nine enemies (two elites),
    /// the Root Maw boss and encounters vh_03..vh_07. Called from <see cref="ContentBuilder.Build"/>. Values are
    /// tuned for humans (generous economy) and verified by ShippedContentTests' scripted player.
    /// Re-running updates assets in place (GUIDs stable).
    /// </summary>
    public static class ContentBuilderM3
    {
        private const string Data = EditorAssets.Root + "Data/";

        /// <summary>Milestone 1 content the new encounters build on.</summary>
        public sealed class Base
        {
            public DefenderDefinition glowcap, sunspore, bulwark;
            public EnemyDefinition drifter, skitter, brute;
            public StatusEffectDefinition burn;
            public BattlefieldDefinition field;
        }

        public sealed class Result
        {
            public readonly List<DefenderDefinition> defenders = new List<DefenderDefinition>();
            public readonly List<EnemyDefinition> enemies = new List<EnemyDefinition>();
            public readonly List<EncounterDefinition> encounters = new List<EncounterDefinition>();
            public readonly List<BossDefinition> bosses = new List<BossDefinition>();
            public readonly List<Object> dirty = new List<Object>();
        }

        public static Result Build(Base b, Dictionary<string, GameObject> prefabs, Dictionary<string, Sprite> icons)
        {
            var r = new Result();

            // ================================================================ statuses
            var frostChill = Status("Status_FrostChill", "frost_chill", "Chilled", StatusKind.Slow, 3f, 0.25f, 0.5f, DamageType.Frost,
                StatusStacking.StackIntensity, 2, new Color(0.55f, 0.85f, 1f));
            var freeze = Status("Status_Freeze", "freeze", "Frozen", StatusKind.Stun, 1.4f, 0f, 0.5f, DamageType.Frost,
                StatusStacking.IgnoreWhileActive, 1, new Color(0.75f, 0.95f, 1f));
            var staticStun = Status("Status_StaticStun", "static_stun", "Short-circuited", StatusKind.Stun, 1.0f, 0f, 0.5f, DamageType.Electric,
                StatusStacking.IgnoreWhileActive, 1, new Color(0.7f, 0.8f, 1f));
            var haste = Status("Status_Howl", "howl_haste", "Frenzied", StatusKind.Haste, 4f, 0.4f, 0.5f, DamageType.Physical,
                StatusStacking.RefreshDuration, 1, new Color(1f, 0.45f, 0.35f));
            var sporeSick = Status("Status_SporeSick", "spore_sick", "Spore-choked", StatusKind.Slow, 1.2f, 0.45f, 0.5f, DamageType.Toxic,
                StatusStacking.RefreshDuration, 1, new Color(0.65f, 0.85f, 0.35f));
            var corrosion = Status("Status_Corrosion", "corrosion", "Corroded", StatusKind.DamageOverTime, 4f, 6f, 0.5f, DamageType.Toxic,
                StatusStacking.RefreshDuration, 1, new Color(0.55f, 0.9f, 0.3f));
            r.dirty.AddRange(new Object[] { frostChill, freeze, staticStun, haste, sporeSick, corrosion });

            // ================================================================ projectiles
            var thorn = Projectile("Projectile_Thorn", "thorn", ProjectileMotion.Straight, 9f, 40f, DamageType.Piercing, 2, Get(prefabs, "Thorn"), new Color(0.75f, 1f, 0.5f));
            thorn.canHitAir = true;
            var shard = Projectile("Projectile_FrostShard", "frost_shard", ProjectileMotion.Straight, 6.5f, 14f, DamageType.Frost, 1, Get(prefabs, "FrostShard"), new Color(0.6f, 0.9f, 1f));
            shard.statusEffect = frostChill; shard.freezeAfterHits = 4; shard.freezeStatus = freeze;
            var ember = Projectile("Projectile_EmberSeed", "ember_seed", ProjectileMotion.Lobbed, 4.5f, 45f, DamageType.Fire, 1, Get(prefabs, "EmberSeed"), new Color(1f, 0.55f, 0.2f));
            ember.splashRadius = 0.8f; ember.splashDamageFraction = 0.6f; ember.statusEffect = b.burn; ember.ignitesClouds = true;
            var whirl = Projectile("Projectile_WhirlSeed", "whirl_seed", ProjectileMotion.Straight, 7f, 22f, DamageType.Physical, 1, Get(prefabs, "WhirlSeed"), new Color(0.95f, 0.95f, 0.6f));
            whirl.canHitAir = true; whirl.bounces = 3; whirl.bounceFalloff = 0.75f; whirl.bounceRange = 2.2f;
            var spit = Projectile("Projectile_SapperSpit", "sapper_spit", ProjectileMotion.Straight, 4f, 40f, DamageType.Toxic, 1, Get(prefabs, "SapperSpit"), new Color(0.6f, 1f, 0.3f));
            spit.statusEffect = corrosion;
            r.dirty.AddRange(new Object[] { thorn, shard, ember, whirl, spit });

            // ================================================================ defenders
            var thorncaster = Defender("Defender_Thorncaster", "thorncaster", "Thorncaster", DefenderRole.Artillery,
                "Hurls heavy thorns that punch through armour plating and pierce two foes. Also hits Gliders.",
                "Pierces armour; anti-air", "Slow to reload", new Color(0.55f, 0.8f, 0.3f), 175, 10f, 300f, DefenderBehaviourKind.LaneShooter, prefabs, icons, "Thorncaster");
            thorncaster.projectile = thorn; thorncaster.attackInterval = 2.2f; thorncaster.firstAttackDelay = 0.8f; thorncaster.range = 0f;

            var snapjaw = Defender("Defender_SnapjawVine", "snapjaw_vine", "Snapjaw Vine", DefenderRole.Assassin,
                "Lurks low, then swallows the first foe within reach whole. Elites take a crushing bite instead. Needs a long time to digest.",
                "Removes one big threat", "Defenceless while digesting", new Color(0.85f, 0.3f, 0.35f), 150, 18f, 400f, DefenderBehaviourKind.Ambusher, prefabs, icons, "SnapjawVine");
            snapjaw.abilityRange = 1.2f; snapjaw.abilityWindup = 0.35f; snapjaw.abilityRecharge = 14f; snapjaw.abilityDamage = 900f;
            snapjaw.abilityDamageType = DamageType.Physical; snapjaw.firstAttackDelay = 0.5f;

            var frostFern = Defender("Defender_FrostFern", "frost_fern", "Frost Fern", DefenderRole.Control,
                "Fires frost shards that chill and slow. Every fourth shard on the same foe freezes it solid for a moment.",
                "Slows and freezes", "Low damage", new Color(0.55f, 0.85f, 1f), 150, 12f, 300f, DefenderBehaviourKind.LaneShooter, prefabs, icons, "FrostFern");
            frostFern.projectile = shard; frostFern.attackInterval = 1.5f; frostFern.firstAttackDelay = 0.6f;

            var emberLily = Defender("Defender_EmberLily", "ember_lily", "Ember Lily", DefenderRole.Artillery,
                "Lobs blazing seeds over defenders. They burst on impact, burning every foe nearby and burning away spore clouds.",
                "Area burn; clears spores", "Slow; cannot hit Gliders", new Color(1f, 0.5f, 0.2f), 175, 14f, 300f, DefenderBehaviourKind.LaneShooter, prefabs, icons, "EmberLily");
            emberLily.projectile = ember; emberLily.attackInterval = 3.0f; emberLily.firstAttackDelay = 1.0f;

            var whirlseed = Defender("Defender_Whirlseed", "whirlseed", "Whirlseed", DefenderRole.Artillery,
                "Spins out seeds that ricochet to up to three more foes in this or a neighbouring lane. Hits Gliders.",
                "Great against crowds", "Each bounce hits softer", new Color(0.95f, 0.85f, 0.45f), 125, 9f, 300f, DefenderBehaviourKind.LaneShooter, prefabs, icons, "Whirlseed");
            whirlseed.projectile = whirl; whirlseed.attackInterval = 1.8f; whirlseed.firstAttackDelay = 0.6f;

            var mender = Defender("Defender_MenderMoss", "mender_moss", "Mender Moss", DefenderRole.Support,
                "Every few seconds it heals the plants around it (and itself) and washes off one harmful effect such as spores or corrosion.",
                "Heals and cleanses nearby", "No attack", new Color(0.45f, 0.9f, 0.55f), 125, 12f, 350f, DefenderBehaviourKind.Mender, prefabs, icons, "MenderMoss");
            mender.abilityDamage = 120f; mender.abilityRange = 1f; mender.abilityRecharge = 4f; mender.abilityCount = 1; mender.firstAttackDelay = 1f;

            var burrowroot = Defender("Defender_Burrowroot", "burrowroot", "Burrowroot", DefenderRole.Trap,
                "Hides underground. When a foe steps on it, it erupts in a ring of spikes, then needs time to regrow. Catches burrowers too.",
                "Strong against fast and burrowing foes", "Edible while regrowing", new Color(0.7f, 0.5f, 0.3f), 100, 10f, 300f, DefenderBehaviourKind.Trap, prefabs, icons, "Burrowroot");
            burrowroot.abilityDamage = 450f; burrowroot.abilityDamageType = DamageType.Physical; burrowroot.abilityRange = 0.75f;
            burrowroot.abilityRecharge = 9f; burrowroot.firstAttackDelay = 1.5f;

            var storm = Defender("Defender_StormOrchid", "storm_orchid", "Storm Orchid", DefenderRole.Specialist,
                "Calls lightning that leaps between up to four foes across neighbouring lanes. Short-circuits Ironroot machines. Hits Gliders.",
                "Chains across lanes; stuns machines", "Expensive", new Color(0.65f, 0.6f, 1f), 200, 16f, 300f, DefenderBehaviourKind.ChainCaster, prefabs, icons, "StormOrchid");
            storm.abilityDamage = 45f; storm.abilityDamageType = DamageType.Electric; storm.abilityCount = 4; storm.abilityRange = 1.8f;
            storm.abilityFalloff = 0.8f; storm.abilityStatus = staticStun; storm.attackInterval = 2.6f; storm.firstAttackDelay = 0.8f; storm.canHitAir = true;

            var mirror = Defender("Defender_Mirrorleaf", "mirrorleaf", "Mirrorleaf", DefenderRole.Control,
                "A gleaming decoy. Foes in neighbouring lanes are drawn to it, and it reflects Sapper spit back at the thrower, twice as hard.",
                "Draws foes; reflects spit", "Deals no damage of its own", new Color(0.8f, 0.95f, 0.95f), 150, 15f, 2600f, DefenderBehaviourKind.Decoy, prefabs, icons, "Mirrorleaf");
            mirror.abilityRange = 2.2f; mirror.abilityRecharge = 4f; mirror.firstAttackDelay = 1f; mirror.reflectsProjectiles = true;
            mirror.reflectMultiplier = 2f; mirror.taunts = true;

            r.defenders.AddRange(new[] { thorncaster, snapjaw, frostFern, emberLily, whirlseed, mender, burrowroot, storm, mirror });

            // ================================================================ enemies
            var vaultling = Enemy("Enemy_Vaultling", "vaultling", "Vaultling",
                "A springy Blight imp. It crouches when it meets a plant, then leaps clean over it, but only once.",
                "Put a cheap plant in front as a springboard, or slow it with Frost Fern.", 170f, 0.42f, 0.5f, 25f, 0.8f, 1, 2,
                new Color(0.95f, 0.6f, 0.3f), prefabs, icons, "Vaultling");
            vaultling.ability = EnemyAbilityKind.Vault; vaultling.abilityWindup = 0.9f;

            var spore = Enemy("Enemy_SporeCarrier", "spore_carrier", "Spore Carrier",
                "A bloated walker. When it bursts it leaves a choking spore cloud that slows and hurts the plant beneath it.",
                "Burn the cloud away with Ember Lily, or kill it before it reaches your plants.", 260f, 0.2f, 0.55f, 25f, 1f, 1, 2,
                new Color(0.6f, 0.8f, 0.3f), prefabs, icons, "SporeCarrier");
            spore.ability = EnemyAbilityKind.SporeCloud; spore.abilityDuration = 7f; spore.abilityDamage = 25f; spore.abilityStatus = sporeSick;
            spore.tags = EnemyTags.Rotbound;

            var burrower = Enemy("Enemy_Burrower", "burrower", "Burrower",
                "Tunnels beneath your lines, trailing dust, and surfaces deep in the garden. Shots cannot reach it underground.",
                "Burrowroot traps and splash damage hit it underground; keep shooters behind the surfacing point.", 230f, 0.32f, 0.5f, 35f, 1f, 1, 2,
                new Color(0.6f, 0.45f, 0.3f), prefabs, icons, "Burrower");
            burrower.movement = EnemyMovementKind.Underground; burrower.ability = EnemyAbilityKind.Burrow; burrower.abilityRange = 4.6f; burrower.abilityWindup = 1.1f;

            var howler = Enemy("Enemy_Howler", "howler", "Howler",
                "Every few seconds it stops, swells its throat sac and howls, whipping nearby Blight into a frenzy (faster for a while).",
                "Kill it first, or freeze it mid-howl.", 280f, 0.2f, 0.55f, 30f, 1f, 1, 3,
                new Color(0.85f, 0.35f, 0.35f), prefabs, icons, "Howler");
            howler.ability = EnemyAbilityKind.Howl; howler.abilityInterval = 9f; howler.abilityWindup = 1.1f; howler.abilityRange = 1.8f; howler.abilityStatus = haste;

            var sapper = Enemy("Enemy_Sapper", "sapper", "Sapper",
                "An Ironroot saboteur. It halts at range and lobs corrosive spit at your plants (its canister glows before each throw).",
                "Mirrorleaf reflects the spit; Storm Orchid short-circuits it; Mender Moss washes off corrosion.", 220f, 0.24f, 0.5f, 25f, 1f, 1, 3,
                new Color(0.5f, 0.75f, 0.35f), prefabs, icons, "Sapper");
            sapper.ability = EnemyAbilityKind.RangedSpit; sapper.abilityRange = 2.5f; sapper.abilityInterval = 3f; sapper.abilityWindup = 0.8f;
            sapper.abilityDamage = 40f; sapper.abilityProjectile = spit; sapper.tags = EnemyTags.Mechanical;

            var glider = Enemy("Enemy_Glider", "glider", "Glider",
                "A membrane-winged scout that soars over every plant. Only anti-air attacks can reach it.",
                "Thorncaster, Whirlseed and Storm Orchid hit flyers.", 100f, 0.38f, 0.5f, 0f, 1f, 1, 2,
                new Color(0.6f, 0.5f, 0.9f), prefabs, icons, "Glider");
            glider.movement = EnemyMovementKind.Flying;

            var mimic = Enemy("Enemy_Mimic", "mimic", "Mimic",
                "A shapeless blob. When another foe comes near it shimmers, then copies that foe's speed and armour, once.",
                "Kill it before it shimmers, or keep it away from Brutes.", 220f, 0.2f, 0.5f, 30f, 1f, 1, 2,
                new Color(0.75f, 0.55f, 0.85f), prefabs, icons, "Mimic");
            mimic.ability = EnemyAbilityKind.Mimic; mimic.abilityRange = 1.6f; mimic.abilityWindup = 1.0f;

            var warden = Enemy("Enemy_Warden", "warden", "Warden",
                "ELITE. A shield-bearing Ironroot knight. Its aura makes nearby Blight take 35% less damage.",
                "Focus it down with Thorncasters, chain lightning or Ember splash; its shield does not protect itself.", 850f, 0.16f, 0.65f, 50f, 1.2f, 2, 6,
                new Color(0.55f, 0.65f, 0.8f), prefabs, icons, "Warden");
            warden.elite = true; warden.devourable = false; warden.tags = EnemyTags.Mechanical;
            warden.ability = EnemyAbilityKind.WardenAura; warden.abilityRange = 1.6f; warden.abilityMagnitude = 0.35f;

            var herald = Enemy("Enemy_BlightHerald", "blight_herald", "Blight Herald",
                "ELITE. A banner-bearing commander. While it lives, every other foe on the field moves 15% faster.",
                "Kill it fast: Snapjaw bites hard, Thorncasters pierce its guard.", 750f, 0.18f, 0.6f, 40f, 1f, 2, 6,
                new Color(0.75f, 0.3f, 0.55f), prefabs, icons, "BlightHerald");
            herald.elite = true; herald.devourable = false;
            herald.ability = EnemyAbilityKind.HeraldCommand; herald.abilityMagnitude = 0.15f;

            r.enemies.AddRange(new[] { vaultling, spore, burrower, howler, sapper, glider, mimic, warden, herald });

            // ================================================================ boss: The Root Maw
            var maw = EditorAssets.LoadOrCreateSO<BossDefinition>(Data + "Bosses/Boss_RootMaw.asset");
            maw.id = "root_maw"; maw.displayName = "The Root Maw";
            maw.description = "A vast mouth of roots beneath the meadow. It erupts under your plants, walls off the ground and calls the Blight.";
            maw.counterHint = "Spread out: it strikes the most crowded column. Hit its glowing Core while it is exposed.";
            maw.maxHealth = 8000f; maw.guardedDamageMultiplier = 0.4f; maw.exposedDamageMultiplier = 2.2f;
            maw.entranceSeconds = 3.5f; maw.phaseTransitionSeconds = 3f; maw.uprootRefund = 0.5f;
            maw.prefab = Get(prefabs, "RootMaw"); maw.accentColor = new Color(0.6f, 0.3f, 0.55f);
            maw.phases = new List<BossPhase>
            {
                new BossPhase { name = "The Ground Stirs", startsAtHealthFraction = 1f, attackInterval = 7f, pattern = new List<BossAttack>
                {
                    Eruption(2, 2.8f, 9f, 5f),
                    Summon(b.drifter, 2, 2f),
                }},
                new BossPhase { name = "Grasping Roots", startsAtHealthFraction = 0.66f, attackInterval = 6.5f, pattern = new List<BossAttack>
                {
                    Eruption(3, 2.6f, 10f, 5f),
                    Summon(b.skitter, 2, 1.8f),
                    Eruption(2, 2.4f, 9f, 4f),
                    Summon(spore, 1, 1.8f),
                }},
                new BossPhase { name = "Heartrot Frenzy", startsAtHealthFraction = 0.33f, attackInterval = 6f, pattern = new List<BossAttack>
                {
                    Eruption(5, 2.8f, 8f, 6f),
                    Summon(vaultling, 2, 1.8f),
                    Summon(b.drifter, 3, 1.8f),
                }},
            };
            EditorUtility.SetDirty(maw);
            r.bosses.Add(maw);

            // ================================================================ encounters
            BattlefieldDefinition field = b.field;
            var e3 = Encounter("Encounter_VH03_BriarGate", "vh_03", "Briar Gate",
                "Thorncasters pierce armour; Frost Ferns slow and freeze. Vaultlings leap the first plant they meet.", field,
                new List<DefenderDefinition> { b.glowcap, b.sunspore, b.bulwark, thorncaster, frostFern }, 200, 100, 45);
            e3.waves = new List<WaveDefinition>
            {
                Wave("VH03_W1", "Scouts", false, 40f, G(b.drifter, 3, -1, 0f, 7f)),
                Wave("VH03_W2", "Leapers", false, 45f, G(vaultling, 2, -1, 0f, 9f), G(b.drifter, 2, -1, 6f, 8f)),
                Wave("VH03_W3", "Plated Pair", false, 55f, G(b.brute, 2, -1, 0f, 12f), G(b.drifter, 3, -1, 5f, 6f)),
                Wave("VH03_W4", "The Pack Leaps", true, 60f, G(vaultling, 4, -1, 0f, 4.5f), G(b.skitter, 2, -1, 8f, 4f), G(b.drifter, 3, -1, 3f, 5f)),
                Wave("VH03_W5", "A Howl in the Hedges", false, 55f, G(howler, 1, -1, 0f, 0f), G(b.drifter, 5, -1, 3f, 3.5f), G(vaultling, 2, -1, 9f, 5f)),
                Wave("VH03_W6", "Briar Breakers", true, 90f, G(b.brute, 2, -1, 0f, 12f), G(howler, 2, -1, 6f, 14f), G(vaultling, 3, -1, 4f, 5f), G(b.drifter, 6, -1, 2f, 3.2f)),
            };

            var e4 = Encounter("Encounter_VH04_RottingMeadow", "vh_04", "Rotting Meadow",
                "Spore Carriers leave choking clouds: burn them with Ember Lily. Burrowers tunnel past your front line: Burrowroot catches them.", field,
                new List<DefenderDefinition> { b.glowcap, b.sunspore, b.bulwark, thorncaster, frostFern, emberLily, burrowroot, snapjaw }, 225, 110, 50);
            e4.waves = new List<WaveDefinition>
            {
                Wave("VH04_W1", "First Spores", false, 45f, G(b.drifter, 3, -1, 0f, 7f), G(spore, 1, -1, 10f, 0f)),
                Wave("VH04_W2", "Spore Drift", false, 50f, G(spore, 2, -1, 0f, 9f), G(b.drifter, 3, -1, 4f, 6f)),
                Wave("VH04_W3", "Tunnellers", false, 50f, G(burrower, 2, -1, 0f, 10f), G(b.drifter, 2, -1, 5f, 7f)),
                Wave("VH04_W4", "The Rot Swells", true, 65f, G(spore, 3, -1, 0f, 6f), G(burrower, 2, -1, 6f, 8f), G(b.skitter, 2, -1, 3f, 4f), G(b.drifter, 3, -1, 0f, 5f)),
                Wave("VH04_W5", "Undermined", false, 55f, G(b.brute, 1, -1, 0f, 0f), G(burrower, 2, -1, 4f, 6f), G(vaultling, 2, -1, 8f, 6f)),
                Wave("VH04_W6", "Rot Tide", true, 95f, G(spore, 3, -1, 0f, 5f), G(burrower, 3, -1, 6f, 7f), G(b.brute, 2, -1, 10f, 12f), G(b.drifter, 6, -1, 0f, 3.2f), G(b.skitter, 2, -1, 12f, 3f)),
            };

            var e5 = Encounter("Encounter_VH05_Windborne", "vh_05", "Windborne",
                "Gliders soar over plants: answer with Whirlseed, Thorncaster or Storm Orchid. Mirrorleaf throws Sapper spit back.", field,
                new List<DefenderDefinition> { b.glowcap, b.sunspore, b.bulwark, thorncaster, whirlseed, storm, mirror, frostFern }, 225, 120, 55);
            e5.waves = new List<WaveDefinition>
            {
                Wave("VH05_W1", "Shadows Above", false, 45f, G(b.drifter, 3, -1, 0f, 7f), G(glider, 1, -1, 9f, 0f)),
                Wave("VH05_W2", "Wings", false, 50f, G(glider, 2, -1, 0f, 9f), G(b.drifter, 2, -1, 4f, 6f)),
                Wave("VH05_W3", "Saboteurs", false, 55f, G(sapper, 2, -1, 0f, 10f), G(b.drifter, 3, -1, 2f, 6f)),
                Wave("VH05_W4", "Borrowed Shapes", true, 65f, G(glider, 2, -1, 0f, 7f), G(mimic, 2, -1, 5f, 7f), G(b.brute, 1, -1, 3f, 0f), G(b.drifter, 3, -1, 0f, 5f)),
                Wave("VH05_W5", "Acid Rain", false, 60f, G(sapper, 3, -1, 0f, 7f), G(glider, 2, -1, 5f, 6f), G(b.skitter, 3, -1, 2f, 4f)),
                Wave("VH05_W6", "Storm Front", true, 95f, G(glider, 3, -1, 0f, 6f), G(sapper, 2, -1, 6f, 8f), G(mimic, 2, -1, 4f, 6f), G(b.brute, 2, -1, 10f, 12f), G(b.drifter, 5, -1, 0f, 3.4f)),
            };

            var e6 = Encounter("Encounter_VH06_WardensVigil", "vh_06", "The Warden's Vigil",
                "Elites lead the Blight. Wardens shield their allies; a Blight Herald hastens every foe. Kill them fast. Mender Moss keeps your lines alive.", field,
                new List<DefenderDefinition> { b.glowcap, b.sunspore, b.bulwark, thorncaster, mender, storm, emberLily, snapjaw }, 250, 140, 60);
            e6.waves = new List<WaveDefinition>
            {
                Wave("VH06_W1", "Outriders", false, 45f, G(b.drifter, 4, -1, 0f, 6f)),
                Wave("VH06_W2", "The Warden", false, 60f, G(warden, 1, -1, 0f, 0f), G(b.drifter, 4, -1, 3f, 4.5f)),
                Wave("VH06_W3", "Rot and Acid", false, 55f, G(spore, 2, -1, 0f, 8f), G(sapper, 2, -1, 4f, 9f), G(b.skitter, 2, -1, 6f, 4f)),
                Wave("VH06_W4", "Herald's March", true, 65f, G(herald, 1, -1, 0f, 0f), G(b.drifter, 5, -1, 2f, 3.5f), G(vaultling, 2, -1, 5f, 6f)),
                Wave("VH06_W5", "Shield and Wing", false, 60f, G(warden, 1, -1, 0f, 0f), G(b.brute, 1, -1, 4f, 0f), G(glider, 2, -1, 6f, 6f), G(b.drifter, 4, -1, 0f, 4f)),
                Wave("VH06_W6", "The Vigil Breaks", true, 100f, G(herald, 1, -1, 0f, 0f), G(warden, 1, -1, 6f, 0f), G(b.drifter, 6, -1, 0f, 3f), G(b.skitter, 3, -1, 6f, 3f), G(b.brute, 1, -1, 10f, 0f), G(spore, 2, -1, 4f, 8f)),
            };

            var e7 = Encounter("Encounter_VH07_RootMaw", "vh_07", "The Root Maw",
                "Something vast stirs beneath the meadow. Watch the ground: glowing cells erupt. Strike its Core when it opens.", field,
                new List<DefenderDefinition> { b.glowcap, b.sunspore, b.bulwark, thorncaster, frostFern, emberLily, storm, whirlseed }, 250, 200, 100);
            e7.boss = maw; e7.bossArrivesAtWave = 1; e7.prepTime = 25f;
            e7.waves = new List<WaveDefinition>
            {
                Wave("VH07_W1", "Tremors", false, 45f, G(b.drifter, 4, -1, 0f, 6f)),
                Wave("VH07_W2", "The Maw Wakes", true, 50f, G(b.drifter, 3, -1, 4f, 7f), G(b.skitter, 2, -1, 10f, 6f)),
                Wave("VH07_W3", "Root Spawn", false, 50f, G(spore, 2, -1, 0f, 9f), G(vaultling, 2, -1, 6f, 8f)),
                Wave("VH07_W4", "Deep Rot", false, 55f, G(b.brute, 1, -1, 0f, 0f), G(b.drifter, 4, -1, 4f, 5f)),
                Wave("VH07_W5", "Wings of the Maw", false, 55f, G(glider, 2, -1, 0f, 8f), G(burrower, 2, -1, 4f, 9f)),
            };
            r.encounters.AddRange(new[] { e3, e4, e5, e6, e7 });

            r.dirty.AddRange(r.defenders.ToArray());
            r.dirty.AddRange(r.enemies.ToArray());
            r.dirty.AddRange(r.encounters.ToArray());
            foreach (Object o in r.dirty) EditorUtility.SetDirty(o);
            return r;
        }

        // ------------------------------------------------------------------ helpers

        private static T Get<T>(Dictionary<string, T> d, string key) where T : Object => d != null && d.TryGetValue(key, out T v) ? v : null;

        private static StatusEffectDefinition Status(string file, string id, string name, StatusKind kind, float duration, float magnitude,
                                                     float tick, DamageType type, StatusStacking stacking, int maxStacks, Color tint)
        {
            var s = EditorAssets.LoadOrCreateSO<StatusEffectDefinition>(Data + "StatusEffects/" + file + ".asset");
            s.id = id; s.displayName = name; s.kind = kind; s.duration = duration; s.magnitude = magnitude; s.tickInterval = tick;
            s.damageType = type; s.stacking = stacking; s.maxStacks = maxStacks; s.tint = tint;
            return s;
        }

        private static ProjectileDefinition Projectile(string file, string id, ProjectileMotion motion, float speed, float damage, DamageType type,
                                                       int pierce, GameObject prefab, Color trail)
        {
            var p = EditorAssets.LoadOrCreateSO<ProjectileDefinition>(Data + "Projectiles/" + file + ".asset");
            p.id = id; p.motion = motion; p.speed = speed; p.damage = damage; p.damageType = type; p.pierceCount = pierce;
            p.splashRadius = 0f; p.splashDamageFraction = 0.5f; p.knockback = 0f; p.statusEffect = null; p.canHitAir = false;
            p.bounces = 0; p.bounceFalloff = 0.75f; p.bounceRange = 2.2f; p.freezeAfterHits = 0; p.freezeStatus = null; p.ignitesClouds = false;
            p.prefab = prefab; p.trailColor = trail;
            return p;
        }

        private static DefenderDefinition Defender(string file, string id, string name, DefenderRole role, string desc, string strengths,
                                                   string weaknesses, Color accent, int cost, float cooldown, float hp, DefenderBehaviourKind behaviour,
                                                   Dictionary<string, GameObject> prefabs, Dictionary<string, Sprite> icons, string art)
        {
            var d = EditorAssets.LoadOrCreateSO<DefenderDefinition>(Data + "Defenders/" + file + ".asset");
            d.id = id; d.displayName = name; d.role = role; d.description = desc; d.strengths = strengths; d.weaknesses = weaknesses;
            d.accentColor = accent; d.cost = cost; d.cooldown = cooldown; d.startingCooldown = 0f; d.maxHealth = hp; d.behaviour = behaviour;
            d.projectile = null; d.attackInterval = 1.4f; d.firstAttackDelay = 0.6f; d.range = 0f;
            d.abilityDamage = 0f; d.abilityDamageType = DamageType.Physical; d.abilityRange = 1.2f; d.abilityRecharge = 10f; d.abilityWindup = 0.3f;
            d.abilityCount = 1; d.abilityFalloff = 0.8f; d.abilityStatus = null; d.canHitAir = false;
            d.reflectsProjectiles = false; d.reflectMultiplier = 1.5f; d.taunts = false;
            d.prefab = Get(prefabs, art); d.icon = Get(icons, art);
            return d;
        }

        private static EnemyDefinition Enemy(string file, string id, string name, string desc, string counter, float hp, float speed, float body,
                                             float bite, float biteInterval, int breach, int threat, Color accent,
                                             Dictionary<string, GameObject> prefabs, Dictionary<string, Sprite> icons, string art)
        {
            var e = EditorAssets.LoadOrCreateSO<EnemyDefinition>(Data + "Enemies/" + file + ".asset");
            e.id = id; e.displayName = name; e.description = desc; e.counterHint = counter;
            e.maxHealth = hp; e.armorHealth = 0f; e.resistances = new List<DamageModifier>(); e.devourable = true; e.elite = false; e.tags = EnemyTags.None;
            e.movement = EnemyMovementKind.Ground; e.moveSpeed = speed; e.speedMultiplierAfterArmorBreak = 1f; e.bodyLength = body;
            e.biteDamage = bite; e.biteInterval = biteInterval; e.breachDamage = breach; e.threatCost = threat; e.accentColor = accent;
            e.ability = EnemyAbilityKind.None; e.abilityWindup = 0.8f; e.abilityInterval = 8f; e.abilityRange = 2f; e.abilityMagnitude = 0.25f;
            e.abilityDuration = 4f; e.abilityStatus = null; e.abilityDamage = 20f; e.abilityProjectile = null;
            e.prefab = Get(prefabs, art); e.icon = Get(icons, art);
            return e;
        }

        private static EncounterDefinition Encounter(string file, string id, string name, string briefing, BattlefieldDefinition field,
                                                     List<DefenderDefinition> roster, int startVitality, int seeds, int flawless)
        {
            var e = EditorAssets.LoadOrCreateSO<EncounterDefinition>(Data + "Missions/" + file + ".asset");
            e.id = id; e.displayName = name; e.briefing = briefing; e.biomeId = "verdant_heartlands"; e.battlefield = field; e.heartrootHealth = 5;
            e.startingVitality = startVitality; e.vitalityCap = 2000; e.skyDropInterval = 8f; e.skyDropFirstDelay = 3f; e.skyDropAmount = 25;
            e.pickupLifetime = 12f; e.prepTime = 25f; e.interWaveGap = 5f; e.fixedSeed = 0; e.roster = roster;
            e.seedReward = seeds; e.flawlessBonus = flawless; e.boss = null; e.bossArrivesAtWave = 0;
            return e;
        }

        private static BossAttack Eruption(int cells, float telegraph, float block, float expose) =>
            new BossAttack { kind = BossAttackKind.RootEruption, cells = cells, telegraphSeconds = telegraph, blockSeconds = block, exposeCoreSeconds = expose };

        private static BossAttack Summon(EnemyDefinition e, int count, float telegraph) =>
            new BossAttack { kind = BossAttackKind.Summon, summonEnemy = e, summonCount = count, telegraphSeconds = telegraph, exposeCoreSeconds = 0f };

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
