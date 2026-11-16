using System;
using System.Collections.Generic;
using UnityEngine;

namespace VerdantSiege.Data
{
    public enum BossAttackKind
    {
        /// <summary>Roots erupt in a column after a cell warning: defenders there are uprooted and the cells are blocked for a while.</summary>
        RootEruption = 0,
        /// <summary>The boss raises its roots and new enemies crawl out at the entry edge.</summary>
        Summon = 1
    }

    /// <summary>Which cells a Root Eruption strikes (also selects the attack's animation and warning colour).</summary>
    public enum BossStrikeShape
    {
        /// <summary>Root Slam: up to <see cref="BossAttack.cells"/> cells of the most crowded column.</summary>
        Column = 0,
        /// <summary>Sweeping Grasp: a strip of <see cref="BossAttack.cells"/> adjacent cells along the most crowded lane.</summary>
        LaneSweep = 1,
        /// <summary>Frenzy: <see cref="BossAttack.columns"/> different columns, <see cref="BossAttack.cells"/> cells each.</summary>
        MultiColumn = 2
    }

    /// <summary>One attack in a phase's repeating pattern.</summary>
    [Serializable]
    public sealed class BossAttack
    {
        public BossAttackKind kind = BossAttackKind.RootEruption;
        [Tooltip("Short name shown on the boss bar while it winds up, e.g. \"Root Slam\". Empty = generic.")]
        public string label = "";
        [Tooltip("Warning time before the attack lands (cells glow, the boss winds up).")]
        [Min(0.5f)] public float telegraphSeconds = 2.5f;
        [Header("Root Eruption")]
        [Tooltip("Cells hit in the chosen column (lanes with defenders first). >= lanes = the whole column.")]
        [Min(1)] public int cells = 2;
        public BossStrikeShape shape = BossStrikeShape.Column;
        [Tooltip("MultiColumn only: how many different columns are struck.")]
        [Min(1)] public int columns = 1;
        [Tooltip("Seconds the struck cells stay blocked by roots.")]
        [Min(0f)] public float blockSeconds = 10f;
        [Header("Summon")]
        public EnemyDefinition summonEnemy;
        [Min(0)] public int summonCount = 2;
        [Header("Vulnerability window")]
        [Tooltip("After the attack the Core is exposed for this long (takes extra damage). 0 = no window.")]
        [Min(0f)] public float exposeCoreSeconds = 0f;
        [Header("Weak points (special attack)")]
        [Tooltip("The wind-up exposes the boss's weak points for BossDefinition.weakPointWindowSeconds (replaces telegraphSeconds). " +
                 "Destroying all of them before it ends interrupts the attack and staggers the boss.")]
        public bool exposesWeakPoints = false;
    }

    /// <summary>A boss phase: entered when health falls to or below its threshold.</summary>
    [Serializable]
    public sealed class BossPhase
    {
        public string name = "Phase";
        [Tooltip("Phase starts when boss health fraction is at or below this (first phase: 1).")]
        [Range(0f, 1f)] public float startsAtHealthFraction = 1f;
        [Tooltip("Seconds between the end of one attack and the start of the next telegraph.")]
        [Min(0.5f)] public float attackInterval = 8f;
        public List<BossAttack> pattern = new List<BossAttack>();
    }

    /// <summary>
    /// Immutable boss content. A boss is not an enemy with huge health: it sits at the entry edge, runs a phase
    /// pattern of telegraphed attacks, changes the battlefield and is only properly hurt during vulnerability windows.
    /// Runtime state lives in <see cref="Battle.BossRuntime"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Verdant Siege/Boss", fileName = "Boss_")]
    public sealed class BossDefinition : ScriptableObject
    {
        public string id = "boss";
        public string displayName = "Boss";
        [TextArea(2, 4)] public string description = "";
        [Tooltip("Shown on arrival, e.g. how to beat it.")]
        public string counterHint = "";

        [Min(1f)] public float maxHealth = 5000f;
        [Tooltip("Damage multiplier while the Core is hidden.")]
        [Range(0f, 2f)] public float guardedDamageMultiplier = 0.5f;
        [Tooltip("Damage multiplier while the Core is exposed (vulnerability window).")]
        [Min(0f)] public float exposedDamageMultiplier = 2f;
        [Tooltip("Seconds from arrival until the first attack (the boss is untargetable while it rises).")]
        [Min(0f)] public float entranceSeconds = 3f;
        [Tooltip("Seconds of invulnerable roaring between phases (pending attacks are cancelled).")]
        [Min(0f)] public float phaseTransitionSeconds = 3f;
        [Tooltip("Fraction of a defender's cost refunded when roots uproot it.")]
        [Range(0f, 1f)] public float uprootRefund = 0.5f;
        public List<BossPhase> phases = new List<BossPhase>();

        [Header("Weak points (exposed by attacks with exposesWeakPoints)")]
        [Tooltip("How many weak points appear (clamped to the lane count).")]
        [Min(0)] public int weakPointCount = 3;
        [Tooltip("Lane of each weak point; projectiles in that lane hit the pod before the boss. Missing entries are spread evenly.")]
        public List<int> weakPointLanes = new List<int> { 0, 2, 4 };
        [Min(1f)] public float weakPointHealth = 300f;
        [Tooltip("Seconds the weak points stay exposed before the special attack lands.")]
        [Min(1f)] public float weakPointWindowSeconds = 9f;
        [Tooltip("Seconds the boss is staggered (Core exposed) after all weak points break.")]
        [Min(0f)] public float staggerSeconds = 6f;
        [Tooltip("Damage multiplier on the boss while staggered.")]
        [Min(0f)] public float staggerDamageMultiplier = 3f;

        [Header("Rewards")]
        [Tooltip("Extra Seeds on top of the encounter reward when the boss is defeated.")]
        [Min(0)] public int bonusSeeds = 150;
        [Tooltip("Essence (rare meta currency) for the first defeat.")]
        [Min(0)] public int essenceFirstDefeat = 3;
        [Tooltip("Essence for every later defeat.")]
        [Min(0)] public int essenceRepeatDefeat = 1;

        [Header("Presentation")]
        public GameObject prefab;
        public Color accentColor = new Color(0.55f, 0.25f, 0.5f);

        public int EssenceReward(bool firstDefeat) => firstDefeat ? essenceFirstDefeat : essenceRepeatDefeat;

        /// <summary>
        /// Fills <paramref name="lanes"/> with the weak point lanes for a battlefield of <paramref name="laneCount"/> lanes:
        /// authored lanes first (in range, distinct), then evenly spread lanes for any missing entries.
        /// </summary>
        public void ResolveWeakPointLanes(int laneCount, List<int> lanes)
        {
            lanes.Clear();
            int count = Math.Min(weakPointCount, laneCount);
            if (count <= 0) return;
            if (weakPointLanes != null)
                foreach (int l in weakPointLanes)
                {
                    if (lanes.Count >= count) break;
                    if (l >= 0 && l < laneCount && !lanes.Contains(l)) lanes.Add(l);
                }
            for (int i = 0; lanes.Count < count && i < count; i++)
            {
                int l = count == 1 ? laneCount / 2 : (int)Math.Round(i * (laneCount - 1) / (double)(count - 1));
                if (!lanes.Contains(l)) lanes.Add(l);
            }
            for (int l = 0; lanes.Count < count && l < laneCount; l++) if (!lanes.Contains(l)) lanes.Add(l);
            lanes.Sort();
        }
    }
}
