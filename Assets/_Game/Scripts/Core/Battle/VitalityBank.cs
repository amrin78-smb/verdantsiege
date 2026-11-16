using System;

namespace VerdantSiege.Battle
{
    /// <summary>The combat resource. Spending is atomic (check and deduct in one call) and capped.</summary>
    public sealed class VitalityBank
    {
        public int Amount { get; private set; }
        public int Cap { get; private set; }
        public int TotalCollected { get; private set; }

        /// <summary>(newAmount, delta)</summary>
        public event Action<int, int> Changed;

        public VitalityBank(int start, int cap)
        {
            Cap = Math.Max(1, cap);
            Amount = Math.Min(Math.Max(0, start), Cap);
        }

        public bool CanAfford(int cost) => cost <= Amount;

        public bool TrySpend(int cost)
        {
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
            if (cost > Amount) return false;
            Amount -= cost;
            if (cost != 0) Changed?.Invoke(Amount, -cost);
            return true;
        }

        /// <summary>Adds (or with a negative value removes) Vitality, clamped to [0, cap]. Returns the applied delta.</summary>
        public int Add(int amount, bool countAsCollected = false)
        {
            int before = Amount;
            long next = (long)Amount + amount;
            Amount = (int)Math.Max(0, Math.Min(Cap, next));
            int delta = Amount - before;
            if (countAsCollected && delta > 0) TotalCollected += delta;
            if (delta != 0) Changed?.Invoke(Amount, delta);
            return delta;
        }
    }
}
