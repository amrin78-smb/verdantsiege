using System;
using System.Collections.Generic;

namespace VerdantSiege.Utilities
{
    /// <summary>Implemented by pooled objects: must restore a clean default state.</summary>
    public interface IPoolable
    {
        void ResetForPool();
    }

    /// <summary>
    /// Allocation-free reuse of plain C# runtime objects (enemies, projectiles, pickups). Every object is reset
    /// explicitly when it is RETURNED, so a rented object never carries state from a previous life.
    /// </summary>
    public sealed class SimplePool<T> where T : class, IPoolable
    {
        private readonly Stack<T> _free = new Stack<T>();
        private readonly Func<T> _factory;

        public int CreatedCount { get; private set; }
        public int FreeCount => _free.Count;

        public SimplePool(Func<T> factory, int prewarm = 0)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            for (int i = 0; i < prewarm; i++) { _free.Push(_factory()); CreatedCount++; }
        }

        public T Rent()
        {
            if (_free.Count > 0) return _free.Pop();
            CreatedCount++;
            return _factory();
        }

        public void Return(T item)
        {
            if (item == null) return;
            item.ResetForPool();
            _free.Push(item);
        }
    }
}
