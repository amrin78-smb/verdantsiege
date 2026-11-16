using System.Collections.Generic;
using VerdantSiege.Data;

namespace VerdantSiege.Battle
{
    /// <summary>
    /// Static content checks for an encounter. Run by EditMode tests over all shipped content and by the
    /// procedural director (Milestone 5) on every generated encounter before it is used.
    /// </summary>
    public static class EncounterValidator
    {
        /// <summary>Most cards the battle rail shows (the HUD shrinks cards beyond six).</summary>
        public const int MaxRosterSize = 8;

        public static List<string> Validate(EncounterDefinition e)
        {
            var errors = new List<string>();
            if (e == null) { errors.Add("encounter is null"); return errors; }
            string n = string.IsNullOrEmpty(e.id) ? e.name : e.id;
            if (string.IsNullOrEmpty(e.id)) errors.Add($"{n}: missing id");
            if (e.battlefield == null) errors.Add($"{n}: missing battlefield");
            else if (e.battlefield.lanes < 1 || e.battlefield.columns < 3) errors.Add($"{n}: battlefield too small");
            if (e.roster == null || e.roster.Count == 0) errors.Add($"{n}: empty roster");
            else
            {
                bool affordable = false;
                foreach (DefenderDefinition d in e.roster)
                {
                    if (d == null) { errors.Add($"{n}: null roster entry"); continue; }
                    if (d.cost <= e.startingVitality) affordable = true;
                    if (d.behaviour == DefenderBehaviourKind.LaneShooter && d.projectile == null)
                        errors.Add($"{n}: shooter {d.id} has no projectile");
                }
                if (!affordable) errors.Add($"{n}: no defender is affordable with the starting Vitality");
                if (e.roster.Count > MaxRosterSize) errors.Add($"{n}: roster has {e.roster.Count} cards (max {MaxRosterSize})");
            }
            if (e.waves == null || e.waves.Count == 0) errors.Add($"{n}: no waves");
            else
            {
                int lanes = e.battlefield != null ? e.battlefield.lanes : 0;
                for (int i = 0; i < e.waves.Count; i++)
                {
                    WaveDefinition w = e.waves[i];
                    if (w == null) { errors.Add($"{n}: wave {i} is null"); continue; }
                    if (w.TotalEnemies() == 0) errors.Add($"{n}: wave {i} ({w.label}) spawns nothing");
                    foreach (SpawnGroup g in w.groups)
                    {
                        if (g == null || g.enemy == null) { errors.Add($"{n}: wave {i} has an empty group"); continue; }
                        if (g.lane >= lanes) errors.Add($"{n}: wave {i} group lane {g.lane} outside {lanes} lanes");
                        if (g.count < 1) errors.Add($"{n}: wave {i} group count < 1");
                    }
                }
            }
            if (e.startingVitality > e.vitalityCap) errors.Add($"{n}: starting Vitality above cap");
            if (e.boss != null)
            {
                BossDefinition b = e.boss;
                if (b.phases == null || b.phases.Count == 0) errors.Add($"{n}: boss {b.id} has no phases");
                else
                {
                    if (b.phases[0].startsAtHealthFraction < 0.999f) errors.Add($"{n}: boss {b.id} first phase must start at full health");
                    for (int i = 0; i < b.phases.Count; i++)
                    {
                        BossPhase p = b.phases[i];
                        if (p == null || p.pattern == null || p.pattern.Count == 0) { errors.Add($"{n}: boss {b.id} phase {i} has no attacks"); continue; }
                        if (i > 0 && p.startsAtHealthFraction >= b.phases[i - 1].startsAtHealthFraction)
                            errors.Add($"{n}: boss {b.id} phase thresholds must decrease");
                        foreach (BossAttack a in p.pattern)
                            if (a.kind == BossAttackKind.Summon && (a.summonEnemy == null || a.summonCount < 1))
                                errors.Add($"{n}: boss {b.id} phase {i} summon has no enemy");
                    }
                }
                if (e.waves != null && e.bossArrivesAtWave >= e.waves.Count) errors.Add($"{n}: boss arrives after the last wave");
            }
            return errors;
        }
    }
}
