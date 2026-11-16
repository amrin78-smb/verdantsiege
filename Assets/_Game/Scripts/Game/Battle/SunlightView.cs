using System.Collections.Generic;
using UnityEngine;
using VerdantSiege.Grid;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Verdant Heartlands sunlight pools: warm glowing discs with drifting motes on boosted cells. They fade in,
    /// pulse, warn by flickering in their last 2 seconds, then fade out. Presentation only.
    /// </summary>
    public sealed class SunlightView : MonoBehaviour
    {
        private BattleSimulation _sim;
        private BattleLayout _layout;
        private VfxSystem _vfx;
        private Material _mat;
        private readonly List<Transform> _discs = new List<Transform>();
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<GridCoord> _shown = new List<GridCoord>();
        private float _fade;
        private float _moteTimer;
        private MaterialPropertyBlock _mpb;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        public void Init(BattleSimulation sim, BattleLayout layout, VisualLibrary lib, VfxSystem vfx)
        {
            _sim = sim;
            _layout = layout;
            _vfx = vfx;
            _mat = lib.glowAdditive;
            _mpb = new MaterialPropertyBlock();
            sim.SunlightPoolsChanged += OnChanged;
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("SunPool" + i);
                go.transform.SetParent(transform, false);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = lib.quadMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(false);
                _discs.Add(go.transform);
                _renderers.Add(mr);
            }
        }

        private void OnDestroy()
        {
            if (_sim != null) _sim.SunlightPoolsChanged -= OnChanged;
        }

        private void OnChanged()
        {
            if (_sim.SunlightPools.Count > 0)
            {
                _shown.Clear();
                _shown.AddRange(_sim.SunlightPools);
                _fade = 0.01f;
                for (int i = 0; i < _discs.Count; i++)
                {
                    bool on = i < _shown.Count;
                    _discs[i].gameObject.SetActive(on);
                    if (on) _discs[i].position = _layout.CellCenter(_shown[i]) + Vector3.up * 0.09f;
                }
                if (VerdantSiege.Audio.AudioService.Instance != null) VerdantSiege.Audio.AudioService.Instance.Play(VerdantSiege.Audio.Sfx.Produce, 0.7f);
            }
        }

        private void LateUpdate()
        {
            if (_sim == null) return;
            bool active = _sim.SunlightPools.Count > 0;
            float dt = Time.deltaTime;
            _fade = Mathf.MoveTowards(_fade, active ? 1f : 0f, dt * 2.5f);
            if (_fade <= 0f)
            {
                foreach (Transform d in _discs) if (d.gameObject.activeSelf) d.gameObject.SetActive(false);
                return;
            }
            float remaining = _sim.SunlightPoolRemaining;
            float flicker = active && remaining < 2f ? (Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? 0.55f : 1f) : 1f;
            float pulse = 0.85f + Mathf.Sin(Time.time * 3f) * 0.15f;
            var col = new Color(1f, 0.82f, 0.4f, 0.75f * _fade * pulse * flicker);
            _mpb.SetColor(ColorId, col);
            for (int i = 0; i < _discs.Count; i++)
            {
                if (!_discs[i].gameObject.activeSelf) continue;
                float s = 1.15f + Mathf.Sin(Time.time * 2f + i) * 0.05f;
                _discs[i].localScale = new Vector3(s, s, 1f);
                _renderers[i].SetPropertyBlock(_mpb);
            }
            _moteTimer -= dt;
            if (active && _vfx != null && _moteTimer <= 0f)
            {
                _moteTimer = 0.18f;
                for (int i = 0; i < _shown.Count; i++)
                {
                    Vector3 p = _layout.CellCenter(_shown[i]) + new Vector3(Random.Range(-0.4f, 0.4f), 0.1f, Random.Range(-0.4f, 0.4f));
                    _vfx.Burst(VfxKind.CollectSparkle, p, new Color(1f, 0.85f, 0.45f), 1, 0.6f, 0.08f, 1.2f);
                }
            }
        }
    }
}
