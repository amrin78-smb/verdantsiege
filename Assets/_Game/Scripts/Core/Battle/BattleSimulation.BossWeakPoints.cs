using System;
using System.Collections.Generic;
using VerdantSiege.Combat;
using VerdantSiege.Data;

namespace VerdantSiege.Battle
{
    /// <summary>
    /// Boss weak points: a special attack (<see cref="BossAttack.exposesWeakPoints"/>) exposes
    /// <see cref="BossDefinition.weakPointCount"/> pods, each in front of the boss in one lane, for
    /// <see cref="BossDefinition.weakPointWindowSeconds"/>. Anything that reaches the boss in a pod's lane hits the pod
    /// first (at full damage). Breaking every pod before the wind-up ends interrupts the attack and staggers the boss
    /// (<see cref="BossState.Staggered"/>: Core exposed at <see cref="BossDefinition.staggerDamageMultiplier"/>).
    /// If any pod survives, the attack lands and the pods withdraw. Data-driven so later bosses reuse it.
    /// </summary>
    public sealed partial class BattleSimulation
    {
        /// <summary>The special attack's wind-up began; pods are in <see cref="BossRuntime.weakPoints"/>.</summary>
        public event Action<BossRuntime> BossWeakPointsExposed;
        /// <summary>(boss, pod, damage dealt)</summary>
        public event Action<BossRuntime, BossWeakPoint, float> BossWeakPointDamaged;
        public event Action<BossRuntime, BossWeakPoint> BossWeakPointDestroyed;
        /// <summary>Every pod broke: the special attack was interrupted and the boss is staggered.</summary>
        public event Action<BossRuntime> BossStaggered;
        /// <summary>The special attack landed while pods survived; they withdraw (raised before BossAttacked).</summary>
        public event Action<BossRuntime> BossWeakPointsWithdrawn;

        private readonly List<int> _weakLanes = new List<int>(4);
        private readonly List<BossWeakPoint> _weakPool = new List<BossWeakPoint>(4);

        private void ExposeWeakPoints()
        {
            Boss.weakPoints.Clear();
            Boss.Definition.ResolveWeakPointLanes(Lanes, _weakLanes);
            for (int i = 0; i < _weakLanes.Count; i++)
            {
                while (_weakPool.Count <= i) _weakPool.Add(new BossWeakPoint());
                BossWeakPoint w = _weakPool[i];
                w.index = i;
                w.lane = _weakLanes[i];
                w.maxHealth = w.health = Boss.Definition.weakPointHealth;
                w.alive = true;
                Boss.weakPoints.Add(w);
            }
            if (Boss.weakPoints.Count > 0) BossWeakPointsExposed?.Invoke(Boss);
        }

        /// <summary>Removes the pods (attack landed, phase change, defeat). Raises Withdrawn only if some were alive.</summary>
        private void ClearWeakPoints(bool notify)
        {
            bool any = Boss.WeakPointsExposed;
            for (int i = 0; i < Boss.weakPoints.Count; i++) Boss.weakPoints[i].alive = false;
            Boss.weakPoints.Clear();
            if (any && notify) BossWeakPointsWithdrawn?.Invoke(Boss);
        }

        /// <summary>
        /// Damage that reaches the boss in a lane (projectile, lob, chain). A living weak point in that lane takes it
        /// first, at full strength; otherwise the boss takes it with its multipliers.
        /// </summary>
        public void DamageBossInLane(int lane, float amount, DamageType type)
        {
            if (Boss == null || amount <= 0f) return;
            BossWeakPoint w = Boss.state == BossState.Telegraph ? Boss.WeakPointInLane(lane) : null;
            if (w == null) { DamageBoss(amount, type); return; }
            DamageWeakPoint(w, amount);
        }

        private void DamageWeakPoint(BossWeakPoint w, float amount)
        {
            float dealt = Math.Min(w.health, amount);
            w.health -= amount;
            BossWeakPointDamaged?.Invoke(Boss, w, dealt);
            if (w.health > 0f) return;
            w.health = 0f;
            w.alive = false;
            BossWeakPointDestroyed?.Invoke(Boss, w);
            if (Boss.WeakPointsAlive == 0) StaggerBoss();
        }

        /// <summary>All pods broke during the wind-up: cancel the attack, stagger, expose the Core.</summary>
        private void StaggerBoss()
        {
            Boss.weakPoints.Clear();
            Boss.targetCells.Clear();
            Boss.currentAttack = null;
            Boss.staggerCount++;
            SetBossState(BossState.Staggered, Boss.Definition.staggerSeconds);
            BossStaggered?.Invoke(Boss);
            BossCoreExposedChanged?.Invoke(Boss, true);
        }

        /// <summary>Debug/test: break every exposed pod now.</summary>
        public void DebugBreakWeakPoints()
        {
            if (Boss == null || Boss.state != BossState.Telegraph) return;
            for (int i = Boss.weakPoints.Count - 1; i >= 0; i--)
                if (i < Boss.weakPoints.Count && Boss.weakPoints[i].alive) DamageWeakPoint(Boss.weakPoints[i], Boss.weakPoints[i].health + 1f);
        }
    }
}
