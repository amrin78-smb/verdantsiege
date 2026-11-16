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
        Defeated = 5,
        Staggered = 6   // all weak points broken: the attack was interrupted, Core exposed for bonus damage
    }

    /// <summary>One weak point (glowing pod) exposed during a special attack's wind-up. Sits in front of the boss in its lane.</summary>
    public sealed class BossWeakPoint
    {
        public int index;
        public int lane;
        public float health;
        public float maxHealth;
        public bool alive;
        public float HealthFraction => maxHealth > 0f ? health / maxHealth : 0f;
    }

    /// <summary>Mutable state of the encounter's boss. Rules live in BattleSimulation.Boss; the definition is never written.</summary>
    public sealed class BossRuntime
    {
        public readonly BossDefinition Definition;
        public float health;
        public int phase;
        public BossState state = BossState.Dormant;
        /// <summary>Seconds left in the current state (Rising, Idle, Telegraph, Transition, Staggered).</summary>
        public float stateTimer;
        /// <summary>Length of the current state when it began (for countdown displays).</summary>
        public float stateDuration;
        /// <summary>Vulnerability window: seconds the Core stays exposed (can run during Idle and Telegraph).</summary>
        public float exposedRemaining;
        public int patternIndex;
        public BossAttack currentAttack;
        /// <summary>Cells the current Root Eruption will strike (shown as warnings during the telegraph).</summary>
        public readonly List<GridCoord> targetCells = new List<GridCoord>(16);
        /// <summary>Weak points of the current special attack (empty unless one is winding up). Alive ones block their lane.</summary>
        public readonly List<BossWeakPoint> weakPoints = new List<BossWeakPoint>(4);
        /// <summary>Times the boss was staggered (weak points broken) in this battle.</summary>
        public int staggerCount;
        /// <summary>Special attacks that landed because weak points survived.</summary>
        public int specialsLanded;

        public BossRuntime(BossDefinition def)
        {
            Definition = def;
            health = def.maxHealth;
        }

        public float MaxHealth => Definition.maxHealth;
        public float HealthFraction => MaxHealth > 0f ? health / MaxHealth : 0f;
        public bool Arrived => state != BossState.Dormant;
        public bool IsTargetable => state == BossState.Idle || state == BossState.Telegraph || state == BossState.Staggered;
        public bool IsStaggered => state == BossState.Staggered;
        public bool CoreExposed => IsStaggered || (exposedRemaining > 0f && IsTargetable);
        public float DamageMultiplier => IsStaggered ? Definition.staggerDamageMultiplier
            : CoreExposed ? Definition.exposedDamageMultiplier : Definition.guardedDamageMultiplier;
        public BossPhase CurrentPhase => phase >= 0 && phase < Definition.phases.Count ? Definition.phases[phase] : null;
        public int PhaseCount => Definition.phases.Count;

        /// <summary>True while a special attack's weak points are exposed (at least one still alive).</summary>
        public bool WeakPointsExposed => WeakPointsAlive > 0;

        public int WeakPointsAlive
        {
            get
            {
                int n = 0;
                for (int i = 0; i < weakPoints.Count; i++) if (weakPoints[i].alive) n++;
                return n;
            }
        }

        /// <summary>The living weak point in a lane, or null.</summary>
        public BossWeakPoint WeakPointInLane(int lane)
        {
            for (int i = 0; i < weakPoints.Count; i++) if (weakPoints[i].alive && weakPoints[i].lane == lane) return weakPoints[i];
            return null;
        }
    }
}
