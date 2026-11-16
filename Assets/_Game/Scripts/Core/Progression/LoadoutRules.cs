using System;
using System.Collections.Generic;
using VerdantSiege.Battle;
using VerdantSiege.Combat;
using VerdantSiege.Data;
using VerdantSiege.Save;

namespace VerdantSiege.Progression
{
    public enum LoadoutIssue
    {
        Ok = 0,
        Empty,
        TooMany,
        Duplicate,
        NotUnlocked,
        Missing
    }

    /// <summary>
    /// Pre-battle deck rules (Milestone 4): validation, the default deck for a mission (saved, then recommended, then
    /// last used), threat intel and plain-language advice about gaps in a deck. Pure, no UI.
    /// </summary>
    public static class LoadoutRules
    {
        public static LoadoutIssue Validate(IList<DefenderDefinition> deck, int slots, SaveData save)
        {
            if (deck == null || deck.Count == 0) return LoadoutIssue.Empty;
            if (deck.Count > slots) return LoadoutIssue.TooMany;
            var seen = new HashSet<string>();
            foreach (DefenderDefinition d in deck)
            {
                if (d == null) return LoadoutIssue.Missing;
                if (!seen.Add(d.id)) return LoadoutIssue.Duplicate;
                if (save != null && !save.IsUnlocked(d.id)) return LoadoutIssue.NotUnlocked;
            }
            return LoadoutIssue.Ok;
        }

        public static string Describe(LoadoutIssue issue)
        {
            switch (issue)
            {
                case LoadoutIssue.Empty: return "Choose at least one defender.";
                case LoadoutIssue.TooMany: return "Too many cards for this mission.";
                case LoadoutIssue.Duplicate: return "Each defender can be brought once.";
                case LoadoutIssue.NotUnlocked: return "That defender is not in your collection yet.";
                case LoadoutIssue.Missing: return "Unknown defender.";
                default: return "";
            }
        }

        /// <summary>The mission's authored roster, limited to what the player owns (the "recommended" picks).</summary>
        public static List<DefenderDefinition> Recommended(EncounterDefinition e, SaveData save)
        {
            var list = new List<DefenderDefinition>();
            foreach (DefenderDefinition d in e.roster) if (d != null && save.IsUnlocked(d.id) && !list.Contains(d)) list.Add(d);
            return list;
        }

        /// <summary>
        /// The deck the loadout screen opens with: this mission's saved deck if valid, else the recommended picks topped
        /// up from the last deck used and then the rest of the collection, up to <paramref name="slots"/>.
        /// </summary>
        public static List<DefenderDefinition> DefaultDeck(EncounterDefinition e, SaveData save, ContentDatabase db, int slots)
        {
            var deck = new List<DefenderDefinition>();
            void Add(DefenderDefinition d)
            {
                if (d != null && deck.Count < slots && save.IsUnlocked(d.id) && !deck.Contains(d)) deck.Add(d);
            }
            List<string> saved = save.LoadoutFor(e.id);
            if (saved != null)
            {
                foreach (string id in saved) Add(db.FindDefender(id));
                if (deck.Count > 0) return deck;
            }
            foreach (DefenderDefinition d in Recommended(e, save)) Add(d);
            foreach (string id in save.lastLoadout) Add(db.FindDefender(id));
            foreach (DefenderDefinition d in db.defenders) Add(d);
            return deck;
        }

        /// <summary>Distinct enemies the mission sends (waves and boss summons), in order of first appearance.</summary>
        public static List<EnemyDefinition> Threats(EncounterDefinition e)
        {
            var list = new List<EnemyDefinition>();
            foreach (WaveDefinition w in e.waves)
                if (w != null) foreach (SpawnGroup g in w.groups) if (g?.enemy != null && !list.Contains(g.enemy)) list.Add(g.enemy);
            if (e.boss != null)
                foreach (BossPhase p in e.boss.phases)
                    if (p != null) foreach (BossAttack a in p.pattern) if (a?.summonEnemy != null && !list.Contains(a.summonEnemy)) list.Add(a.summonEnemy);
            return list;
        }

        public static bool IsAttacker(DefenderDefinition d) =>
            d.behaviour == DefenderBehaviourKind.LaneShooter || d.behaviour == DefenderBehaviourKind.ChainCaster;

        public static bool Pierces(DefenderDefinition d) =>
            d.projectile != null && d.behaviour == DefenderBehaviourKind.LaneShooter && (d.projectile.damageType == DamageType.Piercing || d.projectile.pierceCount > 1);

        public static bool BurnsClouds(DefenderDefinition d) => d.projectile != null && d.projectile.ignitesClouds;

        /// <summary>
        /// Plain-language warnings about a deck for this mission ("Gliders fly over plants: bring anti-air"), naming
        /// counters from the player's collection. Threat tips are only given when the collection holds a counter the deck
        /// lacks (actionable advice; a new player is not told to bring what they cannot own yet). Empty when covered.
        /// </summary>
        public static List<string> Advice(EncounterDefinition e, IList<DefenderDefinition> deck, IEnumerable<DefenderDefinition> collection)
        {
            var tips = new List<string>();
            if (deck == null || deck.Count == 0) return tips;
            bool Any(Func<DefenderDefinition, bool> f) { foreach (DefenderDefinition d in deck) if (d != null && f(d)) return true; return false; }
            string Counters(Func<DefenderDefinition, bool> f)
            {
                var names = new List<string>();
                if (collection != null) foreach (DefenderDefinition d in collection) if (d != null && f(d) && names.Count < 3) names.Add(d.displayName);
                return names.Count == 0 ? "" : " Try " + string.Join(" or ", names) + ".";
            }

            if (!Any(IsAttacker)) tips.Add("Nothing in this deck shoots down a lane." + Counters(IsAttacker));
            if (!Any(d => d.behaviour == DefenderBehaviourKind.Generator)) tips.Add("No generator: Vitality will only fall from the sky." + Counters(d => d.behaviour == DefenderBehaviourKind.Generator));
            List<EnemyDefinition> threats = Threats(e);
            void Threat(EnemyDefinition enemy, string text, Func<DefenderDefinition, bool> counter)
            {
                if (enemy == null || Any(counter)) return;
                string names = Counters(counter);
                if (names.Length > 0) tips.Add(text + names);
            }
            Threat(threats.Find(x => x.IsFlying), $"{threats.Find(x => x.IsFlying)?.displayName}s fly over plants: only anti-air hits them.", d => d.HitsAir);
            Threat(threats.Find(x => x.armorHealth > 0f || x.elite), $"{threats.Find(x => x.armorHealth > 0f || x.elite)?.displayName}s are armoured: piercing shots ignore plating.", Pierces);
            Threat(threats.Find(x => x.ability == EnemyAbilityKind.SporeCloud), $"{threats.Find(x => x.ability == EnemyAbilityKind.SporeCloud)?.displayName}s leave choking clouds: fire burns them away.", BurnsClouds);
            Threat(threats.Find(x => x.ability == EnemyAbilityKind.Burrow), $"{threats.Find(x => x.ability == EnemyAbilityKind.Burrow)?.displayName}s tunnel past the front line: traps catch them.", d => d.behaviour == DefenderBehaviourKind.Trap);
            return tips;
        }

        /// <summary>The battle input for a deck: research modifiers and every defender's mastery level.</summary>
        public static BattleLoadout Build(IList<DefenderDefinition> deck, SaveData save, ContentDatabase db)
        {
            CampaignDefinition c = db != null ? db.campaign : null;
            return new BattleLoadout
            {
                deck = deck != null ? new List<DefenderDefinition>(deck) : null,
                modifiers = ResearchRules.Modifiers(save, c != null ? c.research : null),
                masteryLevels = MasteryRules.Levels(save, db != null ? db.defenders : new List<DefenderDefinition>()),
            };
        }
    }
}
