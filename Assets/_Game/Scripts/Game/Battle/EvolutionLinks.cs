using System.Collections.Generic;
using UnityEngine;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Aura links of evolved formation defenders (Solar Network, Living Sap): a subtle, gently swaying vine of
    /// light from the source to every neighbour it empowers, with motes drifting along it. Derived from
    /// <see cref="BattleSimulation.AuraLinks"/> every frame (pooled LineRenderers). Purely visual.
    /// </summary>
    public sealed class EvolutionLinks : MonoBehaviour
    {
        private const int MaxLines = 32;
        private const int Points = 6;
        private BattleSimulation _sim;
        private BattleLayout _layout;
        private VfxSystem _vfx;
        private LineRenderer[] _lines;
        private readonly Vector3[] _scratch = new Vector3[Points];
        private readonly Dictionary<int, float> _shimmer = new Dictionary<int, float>();
        private readonly List<int> _shimmerKeys = new List<int>();
        private float _moteTimer;

        public void Init(BattleSimulation sim, BattleLayout layout, VisualLibrary lib, VfxSystem vfx)
        {
            _sim = sim;
            _layout = layout;
            _vfx = vfx;
            _lines = new LineRenderer[MaxLines];
            for (int i = 0; i < MaxLines; i++)
            {
                var go = new GameObject("Link" + i);
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = Points;
                lr.useWorldSpace = true;
                lr.sharedMaterial = lib != null ? lib.glowAdditive : null;
                lr.widthMultiplier = 0.08f;
                lr.numCapVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;
                _lines[i] = lr;
            }
        }

        /// <summary>Brightens a source's links briefly (it just shared Vitality).</summary>
        public void Shimmer(int sourceId) => _shimmer[sourceId] = 0.8f;

        private void LateUpdate()
        {
            if (_sim == null || _lines == null) return;
            float dt = Time.deltaTime;
            float time = Time.time;
            _shimmerKeys.Clear();
            foreach (KeyValuePair<int, float> kv in _shimmer) _shimmerKeys.Add(kv.Key);
            foreach (int k in _shimmerKeys) { float v = _shimmer[k] - dt; if (v <= 0f) _shimmer.Remove(k); else _shimmer[k] = v; }

            var links = _sim.AuraLinks;
            int used = 0;
            _moteTimer -= dt;
            bool motes = _moteTimer <= 0f;
            if (motes) _moteTimer = 0.35f;
            for (int i = 0; i < links.Count && used < MaxLines; i++)
            {
                AuraLink link = links[i];
                DefenderRuntime src = _sim.FindDefender(link.sourceId);
                if (src == null || src.evolution == null) continue;
                LineRenderer lr = _lines[used++];
                Vector3 a = _layout.CellCenter(link.from) + Vector3.up * 0.14f;
                Vector3 b = _layout.CellCenter(link.to) + Vector3.up * 0.14f;
                Vector3 side = Vector3.Cross(b - a, Vector3.up).normalized;
                float phase = link.targetId * 0.73f;
                for (int k = 0; k < Points; k++)
                {
                    float t = k / (float)(Points - 1);
                    float arc = Mathf.Sin(t * Mathf.PI);
                    _scratch[k] = Vector3.Lerp(a, b, t) + Vector3.up * (arc * 0.16f)
                                  + side * (arc * 0.06f * Mathf.Sin(time * 2.3f + phase + t * 4f));
                }
                lr.SetPositions(_scratch);
                _shimmer.TryGetValue(link.sourceId, out float sh);
                Color c = src.evolution.accentColor;
                float alpha = 0.5f + 0.12f * Mathf.Sin(time * 3f + phase) + sh * 0.5f;
                var g = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
                lr.startColor = g;
                lr.endColor = new Color(c.r, c.g, c.b, g.a * 0.55f);
                lr.widthMultiplier = 0.09f + sh * 0.06f;
                if (!lr.enabled) lr.enabled = true;
                if (motes && _vfx != null && (i + Mathf.FloorToInt(time * 3f)) % 3 == 0)
                {
                    float t = Mathf.Repeat(time * 0.6f + phase, 1f);
                    _vfx.Burst(VfxKind.CollectSparkle, Vector3.Lerp(a, b, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.16f), c, 1, 0.3f, 0.07f, 0.6f);
                }
            }
            for (int i = used; i < MaxLines; i++) if (_lines[i].enabled) _lines[i].enabled = false;
        }
    }
}
