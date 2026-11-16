using System.Collections.Generic;
using VerdantSiege.Combat;
using VerdantSiege.Data;

namespace VerdantSiege.Battle
{
    /// <summary>Mutable status instance. Lives only on a runtime entity.</summary>
    public sealed class StatusInstance
    {
        public StatusEffectDefinition definition;
        public float remaining;
        public int stacks;
        public float tickTimer;
    }

    /// <summary>
    /// Active statuses of one entity, with duration, stacking and periodic ticks. Allocation-free after warm-up
    /// (instances are recycled through a small free list).
    /// </summary>
    public sealed class StatusEffectSet
    {
        private readonly List<StatusInstance> _active = new List<StatusInstance>(4);
        private readonly Stack<StatusInstance> _free = new Stack<StatusInstance>();

        public int Count => _active.Count;
        public IReadOnlyList<StatusInstance> Active => _active;

        /// <summary>Returns true if the status was newly applied or changed.</summary>
        public bool Apply(StatusEffectDefinition def)
        {
            if (def == null) return false;
            StatusInstance existing = Find(def);
            if (existing != null)
            {
                switch (def.stacking)
                {
                    case StatusStacking.IgnoreWhileActive:
                        return false;
                    case StatusStacking.StackIntensity:
                        if (existing.stacks < def.maxStacks) existing.stacks++;
                        existing.remaining = def.duration;
                        return true;
                    default:
                        existing.remaining = def.duration;
                        return true;
                }
            }
            StatusInstance s = _free.Count > 0 ? _free.Pop() : new StatusInstance();
            s.definition = def;
            s.remaining = def.duration;
            s.stacks = 1;
            s.tickTimer = def.tickInterval;
            _active.Add(s);
            return true;
        }

        public StatusInstance Find(StatusEffectDefinition def)
        {
            for (int i = 0; i < _active.Count; i++) if (_active[i].definition == def) return _active[i];
            return null;
        }

        public bool Has(StatusKind kind)
        {
            for (int i = 0; i < _active.Count; i++) if (_active[i].definition.kind == kind) return true;
            return false;
        }

        /// <summary>Combined slow fraction (strongest slow x stacks, capped at 0.85).</summary>
        public float SlowFraction()
        {
            float best = 0f;
            for (int i = 0; i < _active.Count; i++)
            {
                StatusInstance s = _active[i];
                if (s.definition.kind != StatusKind.Slow) continue;
                float v = s.definition.magnitude * s.stacks;
                if (v > best) best = v;
            }
            return best > 0.85f ? 0.85f : best;
        }

        /// <summary>Strongest haste bonus (fraction, not stacked across sources, capped at 1).</summary>
        public float HasteFraction()
        {
            float best = 0f;
            for (int i = 0; i < _active.Count; i++)
            {
                StatusInstance s = _active[i];
                if (s.definition.kind != StatusKind.Haste) continue;
                float v = s.definition.magnitude * s.stacks;
                if (v > best) best = v;
            }
            return best > 1f ? 1f : best;
        }

        /// <summary>Removes one harmful status (slow, damage over time or stun; never haste). Returns true if one was removed.</summary>
        public bool RemoveOneNegative()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].definition.kind == StatusKind.Haste) continue;
                RemoveAt(i);
                return true;
            }
            return false;
        }

        public bool HasNegative()
        {
            for (int i = 0; i < _active.Count; i++) if (_active[i].definition.kind != StatusKind.Haste) return true;
            return false;
        }

        /// <summary>Advances timers. Returns total damage-over-time damage that ticked (by the first DoT type).</summary>
        public float Tick(float dt, out DamageType dotType)
        {
            float damage = 0f;
            dotType = DamageType.Physical;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                StatusInstance s = _active[i];
                if (s.definition.kind == StatusKind.DamageOverTime)
                {
                    s.tickTimer -= dt;
                    while (s.tickTimer <= 0f)
                    {
                        s.tickTimer += s.definition.tickInterval;
                        damage += s.definition.magnitude * s.stacks;
                        dotType = s.definition.damageType;
                    }
                }
                s.remaining -= dt;
                if (s.remaining <= 0f) RemoveAt(i);
            }
            return damage;
        }

        private void RemoveAt(int i)
        {
            StatusInstance s = _active[i];
            _active.RemoveAt(i);
            s.definition = null;
            _free.Push(s);
        }

        public void Clear()
        {
            for (int i = _active.Count - 1; i >= 0; i--) RemoveAt(i);
        }
    }
}
