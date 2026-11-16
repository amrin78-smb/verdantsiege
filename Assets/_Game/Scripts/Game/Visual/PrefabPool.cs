using System.Collections.Generic;
using UnityEngine;

namespace VerdantSiege.Visual
{
    /// <summary>Implemented by pooled view components; called on every rent/return.</summary>
    public interface IPooledView
    {
        void OnRent();
        void OnReturn();
    }

    /// <summary>
    /// GameObject pool keyed by prefab. Instances are deactivated and parented under the pool root when
    /// returned; <see cref="IPooledView"/> components reset their own state explicitly.
    /// </summary>
    public sealed class PrefabPool
    {
        private readonly Dictionary<GameObject, Stack<GameObject>> _free = new Dictionary<GameObject, Stack<GameObject>>();
        private readonly Dictionary<GameObject, GameObject> _prefabOf = new Dictionary<GameObject, GameObject>();
        private readonly Transform _root;
        private readonly List<IPooledView> _scratch = new List<IPooledView>(4);

        public int InstantiatedCount { get; private set; }

        public PrefabPool(Transform root)
        {
            _root = root;
        }

        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null) return;
            var list = new List<GameObject>(count);
            for (int i = 0; i < count; i++) list.Add(Rent(prefab, Vector3.zero, Quaternion.identity, null));
            foreach (GameObject g in list) Return(g);
        }

        public GameObject Rent(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent)
        {
            if (!_free.TryGetValue(prefab, out Stack<GameObject> stack)) _free[prefab] = stack = new Stack<GameObject>();
            GameObject go;
            if (stack.Count > 0)
            {
                go = stack.Pop();
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(pos, rot);
            }
            else
            {
                go = Object.Instantiate(prefab, pos, rot, parent);
                InstantiatedCount++;
                _prefabOf[go] = prefab;
            }
            go.SetActive(true);
            go.GetComponents(_scratch);
            for (int i = 0; i < _scratch.Count; i++) _scratch[i].OnRent();
            return go;
        }

        public T Rent<T>(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent) where T : Component
        {
            GameObject go = Rent(prefab, pos, rot, parent);
            T c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        public void Return(GameObject go)
        {
            if (go == null) return;
            if (!_prefabOf.TryGetValue(go, out GameObject prefab)) { Object.Destroy(go); return; }
            go.GetComponents(_scratch);
            for (int i = 0; i < _scratch.Count; i++) _scratch[i].OnReturn();
            go.SetActive(false);
            go.transform.SetParent(_root, false);
            _free[prefab].Push(go);
        }
    }
}
