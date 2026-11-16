using System;
using System.Collections.Generic;
using VerdantSiege.Combat;
using VerdantSiege.Data;
using VerdantSiege.Grid;

namespace VerdantSiege.Battle
{
    /// <summary>
    /// Boss rules: arrival, phase pattern of telegraphed attacks, vulnerability windows, phase transitions and
    /// temporary battlefield changes (root-blocked cells). The boss sits at the entry edge across every lane;
    /// projectiles in any lane reach it at <see cref="BossFrontX"/> while it is targetable.
    /// </summary>
    public sealed partial class BattleSimulation
    {
        private struct BlockedCell
        {
            public GridCoord coord;
            public float remaining;
            public CellTerrain original;
        }

        public event Action<BossRuntime> BossArrived;
        /// <summary>(boss, new phase index)</summary>
        public event Action<BossRuntime, int> BossPhaseChanged;
        /// <summary>A warning started; Root Eruption cells are in <see cref="BossRuntime.targetCells"/>.</summary>
        public event Action<BossRuntime, BossAttack> BossTelegraphed;
        /// <summary>The attack landed (targetCells still filled during the call).</summary>
        public event Action<BossRuntime, BossAttack> BossAttacked;
        public event Action<BossRuntime, bool> BossCoreExposedChanged;
        /// <summary>(boss, damage after multipliers)</summary>
        public event Action<BossRuntime, float> BossDamaged;
        public event Action<BossRuntime> BossDefeated;
        public event Action<ProjectileRuntime> ProjectileHitBoss;
        /// <summary>(cell, blocked) blocked=false when the roots withdraw.</summary>
        public event Action<GridCoord, bool> CellBlockedChanged;
        /// <summary>A defender was uprooted by the boss (refund already added).</summary>
        public event Action<DefenderRuntime, int> DefenderUprooted;

        public BossRuntime Boss { get; }
        /// <summary>X (column units) where projectiles reach the boss.</summary>
        public float BossFrontX => Columns + 0.2f;
        public int RootBlockedCount => _blocked.Count;

        private readonly List<BlockedCell> _blocked = new List<BlockedCell>();
        private int[] _columnScore;
        private int[] _laneOrder;

        public bool IsRootBlocked(GridCoord c)
        {
            for (int i = 0; i < _blocked.Count; i++) if (_blocked[i].coord == c) return true;
            return false;
        }

        private void BossRise()
        {
            Boss.state = BossState.Rising;
            Boss.phase = 0;
            Boss.stateTimer = Boss.Definition.entranceSeconds;
            BossArrived?.Invoke(Boss);
        }

        private void TickBoss(float dt)
        {
            TickBlockedCells(dt);
            if (Boss == null || Boss.state == BossState.Dormant || Boss.state == BossState.Defeated) return;
            if (Boss.exposedRemaining > 0f)
            {
                Boss.exposedRemaining -= dt;
                if (Boss.exposedRemaining <= 0f)
                {
                    Boss.exposedRemaining = 0f;
                    BossCoreExposedChanged?.Invoke(Boss, false);
                }
            }
            Boss.stateTimer -= dt;
            if (Boss.stateTimer > 0f) return;
            BossPhase phase = Boss.CurrentPhase;
            switch (Boss.state)
            {
                case BossState.Rising:
                case BossState.Transition:
                    Boss.state = BossState.Idle;
                    Boss.stateTimer = phase != null ? phase.attackInterval * 0.5f : 5f;
                    break;
                case BossState.Idle:
                    BeginBossAttack(phase);
                    break;
                case BossState.Telegraph:
                    ResolveBossAttack(phase);
                    break;
            }
        }

        private void BeginBossAttack(BossPhase phase)
        {
            if (phase == null || phase.pattern.Count == 0)
            {
                Boss.stateTimer = phase != null ? phase.attackInterval : 5f;
                return;
            }
            BossAttack a = phase.pattern[Boss.patternIndex % phase.pattern.Count];
            Boss.patternIndex++;
            Boss.currentAttack = a;
            Boss.state = BossState.Telegraph;
            Boss.stateTimer = a.telegraphSeconds;
            Boss.targetCells.Clear();
            if (a.kind == BossAttackKind.RootEruption) ChooseEruptionCells(a);
            BossTelegraphed?.Invoke(Boss, a);
        }

        private void ResolveBossAttack(BossPhase phase)
        {
            BossAttack a = Boss.currentAttack;
            if (a != null)
            {
                if (a.kind == BossAttackKind.RootEruption)
                {
                    for (int i = 0; i < Boss.targetCells.Count; i++)
                    {
                        GridCoord c = Boss.targetCells[i];
                        DefenderRuntime d = Grid.Get(c);
                        if (d != null && d.alive) Uproot(d);
                        BlockCell(c, a.blockSeconds);
                    }
                }
                else if (a.kind == BossAttackKind.Summon && a.summonEnemy != null)
                {
                    for (int i = 0; i < a.summonCount; i++) SpawnEnemy(a.summonEnemy, _rng.Range(0, Lanes), -1);
                }
                BossAttacked?.Invoke(Boss, a);
                if (a.exposeCoreSeconds > 0f)
                {
                    bool was = Boss.exposedRemaining > 0f;
                    Boss.exposedRemaining = Math.Max(Boss.exposedRemaining, a.exposeCoreSeconds);
                    if (!was) BossCoreExposedChanged?.Invoke(Boss, true);
                }
            }
            Boss.targetCells.Clear();
            Boss.currentAttack = null;
            Boss.state = BossState.Idle;
            Boss.stateTimer = phase != null ? phase.attackInterval : 5f;
        }

        /// <summary>Picks the column with the most defenders (ties: seeded), then lanes holding defenders first.</summary>
        private void ChooseEruptionCells(BossAttack a)
        {
            if (_columnScore == null) { _columnScore = new int[Columns]; _laneOrder = new int[Lanes]; }
            Array.Clear(_columnScore, 0, _columnScore.Length);
            for (int i = 0; i < _defenders.Count; i++) if (_defenders[i].alive) _columnScore[_defenders[i].Coord.column]++;
            int best = -1, bestScore = 0, ties = 0;
            for (int c = 0; c < Columns; c++)
            {
                int s = _columnScore[c];
                if (s <= 0) continue;
                if (s > bestScore) { bestScore = s; best = c; ties = 1; }
                else if (s == bestScore && _rng.Range(0, ++ties) == 0) best = c; // reservoir pick among ties
            }
            if (best < 0) best = _rng.Range(Math.Min(2, Columns - 1), Math.Max(3, Columns - 1));
            int n = 0;
            for (int l = 0; l < Lanes; l++) if (Grid.Get(l, best) != null) _laneOrder[n++] = l;
            int withDefenders = n;
            for (int l = 0; l < Lanes; l++) if (Grid.Get(l, best) == null) _laneOrder[n++] = l;
            // Shuffle the empty lanes (seeded) so empty strikes vary.
            for (int i = n - 1; i > withDefenders; i--)
            {
                int j = withDefenders + _rng.Range(0, i - withDefenders + 1);
                int t = _laneOrder[i]; _laneOrder[i] = _laneOrder[j]; _laneOrder[j] = t;
            }
            int cells = Math.Min(a.cells, Lanes);
            for (int i = 0; i < cells; i++) Boss.targetCells.Add(new GridCoord(_laneOrder[i], best));
        }

        private void Uproot(DefenderRuntime d)
        {
            int refund = (int)Math.Round(d.Definition.cost * Boss.Definition.uprootRefund);
            if (refund > 0) Vitality.Add(refund);
            _stats.defendersLost++;
            DefenderUprooted?.Invoke(d, refund);
            d.health = 0f;
            RemoveDefender(d, true);
        }

        private void BlockCell(GridCoord c, float seconds)
        {
            if (seconds <= 0f || !Grid.InBounds(c)) return;
            for (int i = 0; i < _blocked.Count; i++)
            {
                if (_blocked[i].coord != c) continue;
                BlockedCell b = _blocked[i];
                b.remaining = Math.Max(b.remaining, seconds);
                _blocked[i] = b;
                return;
            }
            _blocked.Add(new BlockedCell { coord = c, remaining = seconds, original = Grid.GetTerrain(c) });
            Grid.SetTerrain(c, CellTerrain.Blocked);
            CellBlockedChanged?.Invoke(c, true);
        }

        private void TickBlockedCells(float dt)
        {
            for (int i = _blocked.Count - 1; i >= 0; i--)
            {
                BlockedCell b = _blocked[i];
                b.remaining -= dt;
                if (b.remaining > 0f) { _blocked[i] = b; continue; }
                _blocked.RemoveAt(i);
                Grid.SetTerrain(b.coord, b.original);
                CellBlockedChanged?.Invoke(b.coord, false);
            }
        }

        private void HitBoss(ProjectileRuntime p)
        {
            ProjectileHitBoss?.Invoke(p);
            DamageBoss(p.damage, p.Definition.damageType);
        }

        /// <summary>
        /// Damages the boss (only while targetable). A hit can never skip a phase: health stops at the next
        /// threshold and the phase transition starts.
        /// </summary>
        public void DamageBoss(float amount, DamageType type)
        {
            if (Boss == null || !Boss.IsTargetable || amount <= 0f) return;
            amount *= Boss.DamageMultiplier;
            Boss.health -= amount;
            int next = Boss.phase + 1;
            if (next < Boss.PhaseCount)
            {
                float threshold = Boss.Definition.phases[next].startsAtHealthFraction * Boss.MaxHealth;
                if (Boss.health <= threshold)
                {
                    BossDamaged?.Invoke(Boss, amount);
                    Boss.health = threshold;
                    EnterBossPhase(next);
                    return;
                }
            }
            BossDamaged?.Invoke(Boss, amount);
            if (Boss.health <= 0f) DefeatBoss();
        }

        private void EnterBossPhase(int index)
        {
            Boss.phase = index;
            Boss.state = BossState.Transition;
            Boss.stateTimer = Boss.Definition.phaseTransitionSeconds;
            Boss.patternIndex = 0;
            Boss.currentAttack = null;
            Boss.targetCells.Clear();
            if (Boss.exposedRemaining > 0f)
            {
                Boss.exposedRemaining = 0f;
                BossCoreExposedChanged?.Invoke(Boss, false);
            }
            BossPhaseChanged?.Invoke(Boss, index);
        }

        private void DefeatBoss()
        {
            Boss.health = 0f;
            Boss.state = BossState.Defeated;
            Boss.exposedRemaining = 0f;
            Boss.targetCells.Clear();
            BossDefeated?.Invoke(Boss);
            // Its Blight withers with it.
            for (int i = 0; i < _enemies.Count; i++) if (_enemies[i].alive) KillEnemy(_enemies[i]);
            for (int i = _blocked.Count - 1; i >= 0; i--)
            {
                BlockedCell b = _blocked[i];
                Grid.SetTerrain(b.coord, b.original);
                CellBlockedChanged?.Invoke(b.coord, false);
            }
            _blocked.Clear();
        }

        /// <summary>Debug: summon the boss now / defeat it now.</summary>
        public void DebugBossNow()
        {
            if (Boss == null) return;
            if (Boss.state == BossState.Dormant) BossRise();
            else if (Boss.state != BossState.Defeated) DefeatBoss();
        }
    }
}
