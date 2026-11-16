using System;
using System.Collections.Generic;
using VerdantSiege.Battle;
using VerdantSiege.Data;
using VerdantSiege.Save;

namespace VerdantSiege.Progression
{
    /// <summary>
    /// Defender mastery (Milestone 4): XP earned by using a defender in battle, never by grinding a menu.
    /// Levels unlock evolution branches (data: <see cref="EvolutionDefinition.masteryRequired"/>) and two small
    /// horizontal perks: a head start on in-battle growth (level 3) and cheaper evolutions (level 4).
    /// </summary>
    public static class MasteryRules
    {
        public const int MaxLevel = 4;
        /// <summary>Total XP needed for levels 1..4 (index = level - 1).</summary>
        public static readonly int[] Thresholds = { 0, 40, 110, 220 };

        /// <summary>XP for a defender used this battle: planting and evolving count (both capped, so one battle gives at most 64); a loss pays 60%.</summary>
        public const int UsedXp = 10, PerPlantXp = 3, PlantCap = 6, PerEvolutionXp = 12, EvolutionCap = 3;
        public const float LossFraction = 0.6f;

        public static int LevelFor(int xp)
        {
            int level = 1;
            for (int i = 1; i < Thresholds.Length; i++) if (xp >= Thresholds[i]) level = i + 1;
            return level;
        }

        /// <summary>0..1 progress inside the current level (1 at max level).</summary>
        public static float LevelProgress(int xp)
        {
            int level = LevelFor(xp);
            if (level >= MaxLevel) return 1f;
            int from = Thresholds[level - 1], to = Thresholds[level];
            return Math.Max(0f, Math.Min(1f, (xp - from) / (float)(to - from)));
        }

        /// <summary>XP still needed for the next level (0 at max).</summary>
        public static int XpToNext(int xp)
        {
            int level = LevelFor(xp);
            return level >= MaxLevel ? 0 : Thresholds[level] - xp;
        }

        public static int BattleGain(DefenderUsage u, bool won, float multiplier = 1f)
        {
            if (u == null || u.planted <= 0) return 0;
            float xp = UsedXp + PerPlantXp * Math.Min(u.planted, PlantCap) + PerEvolutionXp * Math.Min(u.evolved, EvolutionCap);
            if (!won) xp *= LossFraction;
            return (int)Math.Round(xp * Math.Max(0f, multiplier));
        }

        public static int LevelOf(SaveData save, string defenderId) => LevelFor(save.MasteryXp(defenderId));

        /// <summary>Mastery levels for every defender in the database (the battle's lock and perk input).</summary>
        public static Dictionary<string, int> Levels(SaveData save, IEnumerable<DefenderDefinition> defenders)
        {
            var map = new Dictionary<string, int>();
            foreach (DefenderDefinition d in defenders) if (d != null) map[d.id] = LevelOf(save, d.id);
            return map;
        }

        /// <summary>Evolution branches this defender gains on reaching exactly <paramref name="level"/>.</summary>
        public static List<EvolutionDefinition> BranchesAt(DefenderDefinition d, int level)
        {
            var list = new List<EvolutionDefinition>();
            if (d?.evolutions == null) return list;
            foreach (EvolutionDefinition e in d.evolutions)
                if (e != null && Math.Max(1, e.masteryRequired) == level) list.Add(e);
            return list;
        }

        /// <summary>Two-word perk name for compact badges.</summary>
        public static string PerkShort(int level) =>
            level == BattleLoadout.HeadStartLevel ? "Head start" : level == BattleLoadout.ThriftLevel ? "Cheaper evolving" : "";

        /// <summary>Plain-language reward for reaching a level (beyond branches): shown in the Conservatory.</summary>
        public static string PerkText(int level)
        {
            switch (level)
            {
                case BattleLoadout.HeadStartLevel: return $"Starts every battle with {(int)(BattleLoadout.HeadStartFraction * 100)}% growth";
                case BattleLoadout.ThriftLevel: return $"Evolutions cost {(int)Math.Round((1f - BattleLoadout.ThriftMultiplier) * 100)}% less Vitality";
                default: return "";
            }
        }
    }
}
