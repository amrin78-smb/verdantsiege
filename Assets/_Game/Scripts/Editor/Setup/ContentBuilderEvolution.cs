using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VerdantSiege.Combat;
using VerdantSiege.Data;

namespace VerdantSiege.EditorTools
{
    /// <summary>
    /// Living Evolution content: 2-3 branches for each of the 12 defenders (EvolutionDefinition assets), their
    /// XP thresholds/sources and the extra projectiles and statuses the branches use. Called from
    /// <see cref="ContentBuilder.Build"/> after the defenders exist. Re-running updates assets in place.
    /// Thresholds are tuned so a defender doing its job evolves ~45-90 s after planting (see TECHNICAL_DECISIONS TD-23).
    /// </summary>
    public static class ContentBuilderEvolution
    {
        private const string Data = EditorAssets.Root + "Data/";
        private static readonly List<Object> Dirty = new List<Object>();

        // Branch accents: warm sun, cool crystal, root green, bloom pink.
        private static readonly Color Sun = new Color(1f, 0.78f, 0.25f), Ice = new Color(0.55f, 0.88f, 1f), Root = new Color(0.45f, 0.9f, 0.45f),
            Bloom = new Color(1f, 0.55f, 0.75f), Fire = new Color(1f, 0.5f, 0.2f), Storm = new Color(0.7f, 0.7f, 1f), Steel = new Color(0.85f, 0.9f, 0.95f);

        public static void Build(List<DefenderDefinition> defenders, StatusEffectDefinition burn)
        {
            Dirty.Clear();
            DefenderDefinition D(string id) => defenders.Find(x => x != null && x.id == id);

            // ---- statuses
            var deepFreeze = Status("Status_DeepFreeze", "deep_freeze", "Deep-frozen", StatusKind.Stun, 2.4f, 0f, StatusStacking.IgnoreWhileActive, new Color(0.8f, 0.97f, 1f));
            var snare = Status("Status_BrambleSnare", "bramble_snare", "Snared", StatusKind.Slow, 2f, 0.45f, StatusStacking.RefreshDuration, new Color(0.55f, 0.75f, 0.3f));
            var rootBind = Status("Status_RootBind", "root_bind", "Rooted", StatusKind.Stun, 1.5f, 0f, StatusStacking.IgnoreWhileActive, new Color(0.6f, 0.45f, 0.25f));
            var thunder = Status("Status_Thunderstruck", "thunderstruck", "Thunderstruck", StatusKind.Stun, 1.0f, 0f, StatusStacking.IgnoreWhileActive, new Color(0.75f, 0.8f, 1f));

            // ---- Sunspore (shooter): the owner's example, exactly.
            DefenderDefinition sunspore = D("sunspore");
            var lance = Clone(sunspore.projectile, "Projectile_SolarLance", "solar_lance");
            lance.speed = 11f; lance.damage = 95f; lance.damageType = DamageType.Piercing; lance.pierceCount = 1; lance.trailColor = new Color(1f, 0.9f, 0.45f);
            Setup(sunspore, EvolutionXpSource.DamageDealt, 550f,
                Evo("Evo_Sunspore_SolarCannon", "solar_cannon", "Solar Cannon", "Slow, massive sun-lance that ignores armour.", Sun, 50, EvolutionAccessory.Solar, "SC",
                    e => { e.projectileOverride = lance; e.attackIntervalMultiplier = 2.2f; }),
                Evo("Evo_Sunspore_Sunburst", "sunburst", "Sunburst", "Fires down this lane and both neighbouring lanes.", Bloom, 50, EvolutionAccessory.Bloom, "SB",
                    e => { e.multishotSideLanes = 1; e.sideShotDamageFraction = 0.6f; }),
                Evo("Evo_Sunspore_SolarNetwork", "solar_network", "Solar Network", "Plants touching it act 20% faster; +3 Vitality per link every 10 s.", Root, 50, EvolutionAccessory.Root, "SN",
                    e => { e.auraRadius = 1; e.auraActionSpeedBonus = 0.2f; e.auraVitalityPerLink = 3; e.auraVitalityInterval = 10f; }));

            // ---- Glowcap (generator): GDD 7.4.
            Setup(D("glowcap"), EvolutionXpSource.VitalityProduced, 150f,
                Evo("Evo_Glowcap_Abundant", "abundant", "Abundant", "Releases Sunmotes 40% more often.", Root, 25, EvolutionAccessory.Bloom, "AB",
                    e => { e.generateIntervalMultiplier = 0.6f; }),
                Evo("Evo_Glowcap_Capacitor", "capacitor", "Capacitor", "Stores up to 6 Sunmotes. Tap it to release them all at x1.5.", Ice, 25, EvolutionAccessory.Prism, "CP",
                    e => { e.capacitorPulses = 6; e.capacitorReleaseMultiplier = 1.5f; }));

            // ---- Bulwark Bloom (guardian)
            Setup(D("bulwark_bloom"), EvolutionXpSource.DamageAbsorbed, 700f,
                Evo("Evo_Bulwark_Thornhide", "thornhide", "Thornhide", "Every bite hurts the biter: 75% comes back as piercing damage.", Steel, 50, EvolutionAccessory.Prism, "TH",
                    e => { e.thornsFraction = 0.75f; }),
                Evo("Evo_Bulwark_Heartwood", "heartwood", "Heartwood", "Regrows 20 health every second.", Root, 50, EvolutionAccessory.Bloom, "HW",
                    e => { e.regenPerSecond = 20f; }),
                Evo("Evo_Bulwark_BrambleSnare", "bramble_snare", "Bramble Snare", "Foes that bite it are snared: 45% slower for 2 s.", new Color(0.7f, 0.85f, 0.35f), 50, EvolutionAccessory.Root, "BS",
                    e => { e.onBittenStatus = snare; }));

            // ---- Thorncaster (piercing shooter)
            DefenderDefinition thorncaster = D("thorncaster");
            var spike = Clone(thorncaster.projectile, "Projectile_IronbarkSpike", "ironbark_spike");
            spike.speed = 11f; spike.damage = 115f; spike.pierceCount = 5; spike.canHitAir = true; spike.trailColor = new Color(0.9f, 0.95f, 0.7f);
            Setup(thorncaster, EvolutionXpSource.DamageDealt, 750f,
                Evo("Evo_Thorncaster_BrambleVolley", "bramble_volley", "Bramble Volley", "Also fires thorns down both neighbouring lanes.", Root, 75, EvolutionAccessory.Bloom, "BV",
                    e => { e.multishotSideLanes = 1; e.sideShotDamageFraction = 0.7f; }),
                Evo("Evo_Thorncaster_IronbarkSpike", "ironbark_spike", "Ironbark Spike", "Slow, huge spike that skewers up to 5 foes.", Steel, 75, EvolutionAccessory.Prism, "IS",
                    e => { e.projectileOverride = spike; e.attackIntervalMultiplier = 2.2f; }));

            // ---- Frost Fern (control): GDD 7.4.
            DefenderDefinition frost = D("frost_fern");
            var deepShard = Clone(frost.projectile, "Projectile_DeepFreezeShard", "deep_freeze_shard");
            deepShard.freezeAfterHits = 2; deepShard.freezeStatus = deepFreeze; deepShard.trailColor = new Color(0.8f, 0.97f, 1f);
            var glacial = Clone(frost.projectile, "Projectile_GlacialShard", "glacial_shard");
            glacial.damageMultiplierVsChilled = 3f; glacial.trailColor = new Color(0.45f, 0.75f, 1f);
            Setup(frost, EvolutionXpSource.DamageDealt, 320f,
                Evo("Evo_FrostFern_DeepFreeze", "deep_freeze", "Deep Freeze", "Every 2nd shard freezes, for 2.4 s.", Ice, 50, EvolutionAccessory.Prism, "DF",
                    e => { e.projectileOverride = deepShard; }),
                Evo("Evo_FrostFern_GlacialShards", "glacial_shards", "Glacial Shards", "Shards deal triple damage to chilled or frozen foes.", new Color(0.4f, 0.65f, 1f), 50, EvolutionAccessory.Prism, "GS",
                    e => { e.projectileOverride = glacial; }));

            // ---- Ember Lily (lobbed area)
            DefenderDefinition ember = D("ember_lily");
            var wild = Clone(ember.projectile, "Projectile_WildfireSeed", "wildfire_seed");
            wild.splashSideLanes = 1; wild.statusEffect = burn;
            Setup(ember, EvolutionXpSource.DamageDealt, 800f,
                Evo("Evo_EmberLily_Wildfire", "wildfire", "Wildfire", "Bursts spill into both neighbouring lanes.", Fire, 75, EvolutionAccessory.Solar, "WF",
                    e => { e.projectileOverride = wild; }),
                Evo("Evo_EmberLily_Firestorm", "firestorm", "Firestorm", "Each lob splits into 3 seeds raining down the lane.", new Color(1f, 0.35f, 0.3f), 75, EvolutionAccessory.Bloom, "FS",
                    e => { e.lobClusterCount = 3; e.lobClusterSpacing = 0.9f; e.lobClusterDamageFraction = 0.55f; }));

            // ---- Whirlseed (ricochet)
            DefenderDefinition whirl = D("whirlseed");
            var cyclone = Clone(whirl.projectile, "Projectile_CycloneSeed", "cyclone_seed");
            cyclone.bounces = 6; cyclone.bounceFalloff = 0.85f; cyclone.bounceRange = 2.6f;
            var gale = Clone(whirl.projectile, "Projectile_GaleSeed", "gale_seed");
            gale.bounces = 2; gale.knockback = 0.45f; gale.trailColor = new Color(0.85f, 1f, 0.95f);
            Setup(whirl, EvolutionXpSource.DamageDealt, 650f,
                Evo("Evo_Whirlseed_Cyclone", "cyclone", "Cyclone", "Seeds ricochet up to 6 times and keep more power.", Sun, 50, EvolutionAccessory.Bloom, "CY",
                    e => { e.projectileOverride = cyclone; }),
                Evo("Evo_Whirlseed_Gale", "gale", "Gale", "Seeds knock foes back half a tile (2 ricochets).", new Color(0.7f, 1f, 0.9f), 50, EvolutionAccessory.Solar, "GL",
                    e => { e.projectileOverride = gale; }));

            // ---- Snapjaw Vine (ambusher)
            Setup(D("snapjaw_vine"), EvolutionXpSource.DamageDealt, 900f,
                Evo("Evo_Snapjaw_QuickDigest", "quick_digest", "Quick Digest", "Digests in under half the time.", Bloom, 50, EvolutionAccessory.Bloom, "QD",
                    e => { e.abilityRechargeMultiplier = 0.4f; }),
                Evo("Evo_Snapjaw_GraspingVines", "grasping_vines", "Grasping Vines", "Reaches 2 tiles ahead and snatches Gliders.", Root, 50, EvolutionAccessory.Root, "GV",
                    e => { e.abilityRangeBonus = 1.3f; e.grantsAntiAir = true; }));

            // ---- Mender Moss (support)
            Setup(D("mender_moss"), EvolutionXpSource.HealingDone, 600f,
                Evo("Evo_Mender_WideBloom", "wide_bloom", "Wide Bloom", "Heals a wider 5x5 area (70% per plant).", Bloom, 50, EvolutionAccessory.Bloom, "WB",
                    e => { e.abilityRangeBonus = 1f; e.abilityDamageMultiplier = 0.7f; }),
                Evo("Evo_Mender_LivingSap", "living_sap", "Living Sap", "Plants touching it act 20% faster.", Root, 50, EvolutionAccessory.Root, "LS",
                    e => { e.auraRadius = 1; e.auraActionSpeedBonus = 0.2f; }));

            // ---- Burrowroot (trap)
            Setup(D("burrowroot"), EvolutionXpSource.Triggers, 3f,
                Evo("Evo_Burrowroot_Quickroot", "quickroot", "Quickroot", "Rearms 2.5x faster after each eruption.", Root, 50, EvolutionAccessory.Bloom, "QR",
                    e => { e.abilityRechargeMultiplier = 0.4f; }),
                Evo("Evo_Burrowroot_BrambleWeb", "bramble_web", "Bramble Web", "Erupts across 3 lanes and roots foes for 1.5 s.", new Color(0.8f, 0.6f, 0.35f), 50, EvolutionAccessory.Root, "BW",
                    e => { e.blastSideLanes = 1; e.abilityStatusOverride = rootBind; }));

            // ---- Storm Orchid (chain)
            Setup(D("storm_orchid"), EvolutionXpSource.DamageDealt, 1000f,
                Evo("Evo_StormOrchid_Tempest", "tempest", "Tempest", "Lightning jumps to 7 foes and barely weakens.", Storm, 75, EvolutionAccessory.Prism, "TP",
                    e => { e.abilityCountBonus = 3; e.abilityFalloffOverride = 0.92f; }),
                Evo("Evo_StormOrchid_Thunderhead", "thunderhead", "Thunderhead", "One huge bolt that stuns any foe it strikes.", Sun, 75, EvolutionAccessory.Solar, "TD",
                    e => { e.abilityCountOverride = 1; e.abilityDamageMultiplier = 3.2f; e.abilityStatusOverride = thunder; e.abilityStatusOnAll = true; }));

            // ---- Mirrorleaf (decoy)
            Setup(D("mirrorleaf"), EvolutionXpSource.DamageAbsorbed, 600f,
                Evo("Evo_Mirrorleaf_HallOfMirrors", "hall_of_mirrors", "Hall of Mirrors", "Lures foes from 2 lanes away, more often.", Ice, 50, EvolutionAccessory.Prism, "HM",
                    e => { e.lureLaneReach = 2; e.abilityRechargeMultiplier = 0.6f; }),
                Evo("Evo_Mirrorleaf_PrismGuard", "prism_guard", "Prism Guard", "Biters take 60% back; spit returns x3.", Steel, 50, EvolutionAccessory.Prism, "PG",
                    e => { e.thornsFraction = 0.6f; e.reflectMultiplierOverride = 3f; }));

            foreach (Object o in Dirty) EditorUtility.SetDirty(o);
        }

        // ------------------------------------------------------------------ helpers
        private static void Setup(DefenderDefinition d, EvolutionXpSource source, float xp, params EvolutionDefinition[] evolutions)
        {
            if (d == null) { Debug.LogError("[VS Setup] Evolution: defender missing"); return; }
            d.xpSource = source;
            d.evolutionXp = xp;
            d.evolutions = new List<EvolutionDefinition>(evolutions);
            Dirty.Add(d);
        }

        private static EvolutionDefinition Evo(string file, string id, string name, string desc, Color accent, int cost, EvolutionAccessory accessory,
                                               string glyph, System.Action<EvolutionDefinition> mechanics)
        {
            var e = EditorAssets.LoadOrCreateSO<EvolutionDefinition>(Data + "Evolutions/" + file + ".asset");
            // Reset every mechanic to neutral, so re-running never keeps a stale value.
            var fresh = ScriptableObject.CreateInstance<EvolutionDefinition>();
            EditorUtility.CopySerialized(fresh, e);
            Object.DestroyImmediate(fresh);
            e.name = file;
            e.id = id; e.displayName = name; e.description = desc; e.accentColor = accent; e.cost = cost; e.accessory = accessory; e.glyph = glyph;
            mechanics(e);
            Dirty.Add(e);
            return e;
        }

        private static ProjectileDefinition Clone(ProjectileDefinition src, string file, string id)
        {
            var p = EditorAssets.LoadOrCreateSO<ProjectileDefinition>(Data + "Projectiles/" + file + ".asset");
            EditorUtility.CopySerialized(src, p);
            p.name = file;
            p.id = id;
            Dirty.Add(p);
            return p;
        }

        private static StatusEffectDefinition Status(string file, string id, string name, StatusKind kind, float duration, float magnitude,
                                                     StatusStacking stacking, Color tint)
        {
            var s = EditorAssets.LoadOrCreateSO<StatusEffectDefinition>(Data + "StatusEffects/" + file + ".asset");
            s.id = id; s.displayName = name; s.kind = kind; s.duration = duration; s.magnitude = magnitude; s.tickInterval = 0.5f;
            s.damageType = DamageType.Physical; s.stacking = stacking; s.maxStacks = 1; s.tint = tint;
            Dirty.Add(s);
            return s;
        }
    }
}
