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

    /// <summary>One attack in a phase's repeating pattern.</summary>
    [Serializable]
    public sealed class BossAttack
    {
        public BossAttackKind kind = BossAttackKind.RootEruption;
        [Tooltip("Warning time before the attack lands (cells glow, the boss winds up).")]
        [Min(0.5f)] public float telegraphSeconds = 2.5f;
        [Header("Root Eruption")]
        [Tooltip("Cells hit in the chosen column (lanes with defenders first). >= lanes = the whole column.")]
        [Min(1)] public int cells = 2;
        [Tooltip("Seconds the struck cells stay blocked by roots.")]
        [Min(0f)] public float blockSeconds = 10f;
        [Header("Summon")]
        public EnemyDefinition summonEnemy;
        [Min(0)] public int summonCount = 2;
        [Header("Vulnerability window")]
        [Tooltip("After the attack the Core is exposed for this long (takes extra damage). 0 = no window.")]
        [Min(0f)] public float exposeCoreSeconds = 0f;
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

        [Header("Presentation")]
        public GameObject prefab;
        public Color accentColor = new Color(0.55f, 0.25f, 0.5f);
    }
}
