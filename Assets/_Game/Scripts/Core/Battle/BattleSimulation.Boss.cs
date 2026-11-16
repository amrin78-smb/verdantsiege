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
    /// projectiles in any lane reach it at <see cref="BossFrontX"/> while it is targetable (weak points: see
    /// BattleSimulation.BossWeakPoints).
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
        /// <summary>X (column units) where projectiles reach the boss (and its weak points).</summary>
        public float BossFrontX => Columns + 0.2f;
        public int RootBlockedCount => _blocked.Count;
        /// <summary>Extra Seeds for defeating the boss (0 unless won against a boss).</summary>
        public int BossBonusSeeds => State == BattleState.Won && Boss != null && Boss.state == BossState.Defeated ? Boss.Definition.bonusSeeds : 0;

        private readonly List<BlockedCell> _blocked = new List<BlockedCell>();
        private int[] _columnScore;
        private int[] _laneOrder;
        private bool[] _columnTaken;

        public bool IsRootBlocked(GridCoord c)
        {
            for (int i = 0; i < _blocked.Count; i++) if (_blocked[i].coord == c) return true;
            return false;
        }

        private void SetBossState(BossState s, float seconds)
        {
            Boss.state = s;
            Boss.stateTimer = seconds;
            Boss.stateDuration = seconds;
        }

        private void BossRise()
        {
            Boss.phase = 0;
            SetBossState(BossState.Rising, Boss.Definition.entranceSeconds);
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
                    if (!Boss.IsStaggered) BossCoreExposedChanged?.Invoke(Boss, false);
                }
            }
            Boss.stateTimer -= dt;
            if (Boss.stateTimer > 0f) return;
            BossPhase phase = Boss.CurrentPhase;
            switch (Boss.state)
            {
                case BossState.Rising:
                case BossState.Transition:
                    SetBossState(BossState.Idle, phase != null ? phase.attackInterval * 0.5f : 5f);
                    break;
                case BossState.Staggered:
                    SetBossState(BossState.Idle, phase != null ? phase.attackInterval * 0.5f : 5f);
                    if (Boss.exposedRemaining <= 0f) BossCoreExposedChanged?.Invoke(Boss, false);
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
                SetBossState(BossState.Idle, phase != null ? phase.attackInterval : 5f);
                return;
            }
            BossAttack a = phase.pattern[Boss.patternIndex % phase.pattern.Count];
            Boss.patternIndex++;
            Boss.currentAttack = a;
            bool special = a.exposesWeakPoints && Boss.Definition.weakPointCount > 0;
            SetBossState(BossState.Telegraph, special ? Boss.Definition.weakPointWindowSeconds : a.telegraphSeconds);
            Boss.targetCells.Clear();
            if (a.kind == BossAttackKind.RootEruption) ChooseEruptionCells(a);
            BossTelegraphed?.Invoke(Boss, a);
            if (special) ExposeWeakPoints();
        }

        private void ResolveBossAttack(BossPhase phase)
        {
            BossAttack a = Boss.currentAttack;
            ClearWeakPoints(true);
            if (a != null)
            {
                if (a.exposesWeakPoints) Boss.specialsLanded++;
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
            SetBossState(BossState.Idle, phase != null ? phase.attackInterval : 5f);
        }

        private void ChooseEruptionCells(BossAttack a)
        {
            if (_columnScore == null) { _columnScore = new int[Columns]; _laneOrder = new int[Lanes]; _columnTaken = new bool[Columns]; }
            Array.Clear(_columnTaken, 0, _columnTaken.Length);
            switch (a.shape)
            {
                case BossStrikeShape.LaneSweep:
                    ChooseLaneSweep(a);
                    break;
                case BossStrikeShape.MultiColumn:
                    for (int k = 0; k < Math.Max(1, a.columns); k++) ChooseColumnCells(a);
                    break;
                default:
                    ChooseColumnCells(a);
                    break;
            }
        }

        /// <summary>Picks the free column with the most defenders (ties: seeded), then lanes holding defenders first.</summary>
        private void ChooseColumnCells(BossAttack a)
        {
            Array.Clear(_columnScore, 0, _columnScore.Length);
            for (int i = 0; i < _defenders.Count; i++) if (_defenders[i].alive) _columnScore[_defenders[i].Coord.column]++;
            int best = -1, bestScore = 0, ties = 0;
            for (int c = 0; c < Columns; c++)
            {
                if (_columnTaken[c]) continue;
                int s = _columnScore[c];
                if (s <= 0) continue;
                if (s > bestScore) { bestScore = s; best = c; ties = 1; }
                else if (s == bestScore && _rng.Range(0, ++ties) == 0) best = c; // reservoir pick among ties
            }
            if (best < 0)
            {
                // Nothing planted (or every crowded column already taken): strike a free mid-field column.
                int lo = Math.Min(2, Columns - 1), hi = Math.Max(lo + 1, Columns - 1);
                best = _rng.Range(lo, hi);
                for (int k = 0; k < Columns && _columnTaken[best]; k++) best = (best + 1) % Columns;
                if (_columnTaken[best]) return;
            }
            _columnTaken[best] = true;
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

        /// <summary>
        /// Sweeping Grasp: the strip of <see cref="BossAttack.cells"/> adjacent cells along one lane that holds the most
        /// defenders (ties: seeded). The strip never reaches the first column (the Heartroot's own soil).
        /// </summary>
        private void ChooseLaneSweep(BossAttack a)
        {
            int len = Math.Max(1, Math.Min(a.cells, Columns - 1));
            int bestLane = -1, bestStart = Columns - len, bestScore = 0, ties = 0;
            for (int l = 0; l < Lanes; l++)
            for (int start = 1; start + len <= Columns; start++)
            {
                int s = 0;
                for (int c = start; c < start + len; c++) if (Grid.Get(l, c) != null) s++;
                if (s <= 0) continue;
                if (s > bestScore) { bestScore = s; bestLane = l; bestStart = start; ties = 1; }
                else if (s == bestScore && _rng.Range(0, ++ties) == 0) { bestLane = l; bestStart = start; }
            }
            if (bestLane < 0) { bestLane = _rng.Range(0, Lanes); bestStart = Columns - len; }
            // Listed from the boss side so the sweep reads right-to-left.
            for (int c = bestStart + len - 1; c >= bestStart; c--) Boss.targetCells.Add(new GridCoord(bestLane, c));
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
            DamageBossInLane(p.lane, p.damage, p.Definition.damageType);
        }

        /// <summary>
        /// Damages the boss body (only while targetable). A hit can never skip a phase: health stops at the next
        /// threshold and the phase transition starts. Lane hits go through <see cref="DamageBossInLane"/> (weak points first).
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
            bool wasExposed = Boss.CoreExposed;
            ClearWeakPoints(true);
            Boss.phase = index;
            SetBossState(BossState.Transition, Boss.Definition.phaseTransitionSeconds);
            Boss.patternIndex = 0;
            Boss.currentAttack = null;
            Boss.targetCells.Clear();
            Boss.exposedRemaining = 0f;
            if (wasExposed) BossCoreExposedChanged?.Invoke(Boss, false);
            BossPhaseChanged?.Invoke(Boss, index);
        }

        private void DefeatBoss()
        {
            ClearWeakPoints(false);
            Boss.health = 0f;
            Boss.state = BossState.Defeated;
            Boss.stateTimer = Boss.stateDuration = 0f;
            Boss.exposedRemaining = 0f;
            Boss.currentAttack = null;
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
