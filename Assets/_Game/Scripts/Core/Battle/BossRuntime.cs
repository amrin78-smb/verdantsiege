using System.Collections.Generic;
using VerdantSiege.Data;
using VerdantSiege.Grid;

namespace VerdantSiege.Battle
{
    public enum BossState
    {
        Dormant = 0,    // not yet arrived
        Rising = 1,     // entrance (untargetable)
        Idle = 2,       // between attacks
        Telegraph = 3,  // warning shown; the attack lands when the timer ends
        Transition = 4, // phase change roar (untargetable, pattern restarts)
        Defeated = 5
    }

    /// <summary>Mutable state of the encounter's boss. Rules live in BattleSimulation.Boss; the definition is never written.</summary>
    public sealed class BossRuntime
    {
        public readonly BossDefinition Definition;
        public float health;
        public int phase;
        public BossState state = BossState.Dormant;
        /// <summary>Seconds left in the current state (Rising, Idle, Telegraph, Transition).</summary>
        public float stateTimer;
        /// <summary>Vulnerability window: seconds the Core stays exposed (can run during Idle and Telegraph).</summary>
        public float exposedRemaining;
        public int patternIndex;
        public BossAttack currentAttack;
        /// <summary>Cells the current Root Eruption will strike (shown as warnings during the telegraph).</summary>
        public readonly List<GridCoord> targetCells = new List<GridCoord>(8);

        public BossRuntime(BossDefinition def)
        {
            Definition = def;
            health = def.maxHealth;
        }

        public float MaxHealth => Definition.maxHealth;
        public float HealthFraction => MaxHealth > 0f ? health / MaxHealth : 0f;
        public bool Arrived => state != BossState.Dormant;
        public bool IsTargetable => state == BossState.Idle || state == BossState.Telegraph;
        public bool CoreExposed => exposedRemaining > 0f && IsTargetable;
        public float DamageMultiplier => CoreExposed ? Definition.exposedDamageMultiplier : Definition.guardedDamageMultiplier;
        public BossPhase CurrentPhase => phase >= 0 && phase < Definition.phases.Count ? Definition.phases[phase] : null;
        public int PhaseCount => Definition.phases.Count;
    }
}
