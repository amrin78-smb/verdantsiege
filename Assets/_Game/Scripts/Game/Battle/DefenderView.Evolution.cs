using UnityEngine;
using VerdantSiege.Data;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Living Evolution presentation of a defender: a gold shimmer while an evolution is ready, the evolution
    /// moment (flash, pop, light column, expanding ring), and the evolved look that reads at phone size:
    /// ~1.12x scale, an accent glow ring at the base, a subtle accent tint and the branch's accessory model
    /// (EvoSolar/EvoPrism/EvoBloom on the top part, EvoRoot at the base) when the art exists. Capacitors glow
    /// with their stored charge. Presentation only: everything is derived from <see cref="DefenderRuntime"/>.
    /// </summary>
    public sealed partial class DefenderView
    {
        public const float EvolvedScaleFactor = 1.12f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly string[] TopParts = { "Head", "Cap", "Bloom", "Crystal", "Petals", "Mirror", "Rotor", "Fronds", "Jaw", "Orbs", "PlatesOuter", "Core", "Sprout", "Mound", "Stem" };

        private Transform _ring;
        private MeshRenderer _ringRenderer;
        private MaterialPropertyBlock _ringMpb;
        private GameObject _accessory;
        private EvolutionDefinition _shownEvolution;
        private float _evolveAge = 99f;
        private float _readySparkle;
        private float _scaleBlend;

        /// <summary>Model scale multiplier: grows smoothly to <see cref="EvolvedScaleFactor"/> once evolved.</summary>
        private float EvolvedScale => 1f + (EvolvedScaleFactor - 1f) * _scaleBlend;

        private void ResetEvolution()
        {
            if (_accessory != null) Destroy(_accessory);
            _accessory = null;
            _shownEvolution = null;
            _evolveAge = 99f;
            _scaleBlend = 0f;
            _readySparkle = 0f;
            if (_ring != null) _ring.gameObject.SetActive(false);
        }

        /// <summary>The evolution moment (called by BattleView on DefenderEvolved).</summary>
        public void OnEvolved(EvolutionDefinition evo)
        {
            if (_d == null || evo == null) return;
            _evolveAge = 0f;
            _spawn.value = -0.3f;
            _spawn.velocity = 5f;
            _squash.Kick(-8f);
            _rig.Flash(Color.white, 0.3f);
            Vector3 p = transform.position;
            Color a = evo.accentColor;
            if (_vfx != null)
            {
                _vfx.Pulse(VfxKind.Glow, p + Vector3.up * 0.15f, new Color(a.r, a.g, a.b, 0.95f), 3.4f, 0.6f);
                for (int i = 0; i < 4; i++)
                    _vfx.Pulse(VfxKind.Glow, p + Vector3.up * (0.35f + i * 0.45f), new Color(1f, 0.97f, 0.85f, 0.75f - i * 0.12f), 1.3f - i * 0.15f, 0.45f + i * 0.08f);
                _vfx.Burst(VfxKind.CollectSparkle, p + Vector3.up * 0.6f, a, 36, 3.2f, 0.16f, 0.9f);
                _vfx.Burst(VfxKind.CollectSparkle, p + Vector3.up * 1.2f, new Color(1f, 0.95f, 0.7f), 18, 1.8f, 0.12f, 1.1f);
                _vfx.Burst(VfxKind.PlantDust, p + Vector3.up * 0.05f, Color.Lerp(a, new Color(0.5f, 0.85f, 0.35f), 0.5f), 20, 2.4f, 0.14f, 0.6f);
            }
            ShowEvolved(evo);
        }

        /// <summary>A Capacitor released its charge.</summary>
        public void OnCapacitorReleased()
        {
            _pulse = 1f;
            _squash.Kick(-10f);
            _rig.Flash(new Color(1f, 0.95f, 0.6f), 0.2f);
            if (_vfx == null) return;
            Vector3 p = transform.position + Vector3.up * 0.7f;
            _vfx.Pulse(VfxKind.Glow, p, new Color(0.55f, 1f, 0.95f, 0.9f), 2.6f, 0.45f);
            _vfx.Burst(VfxKind.CollectSparkle, p, new Color(1f, 0.9f, 0.45f), 30, 3f, 0.16f, 0.8f);
        }

        /// <summary>Thorns / prism guard bit back.</summary>
        public void OnThorns()
        {
            _squash.Kick(3f);
            if (_vfx != null && _d != null && _d.evolution != null)
                _vfx.Burst(VfxKind.HitSpark, transform.position + new Vector3(0.45f, 0.5f, 0f), _d.evolution.accentColor, 6, 2f, 0.1f, 0.25f);
        }

        private void ShowEvolved(EvolutionDefinition evo)
        {
            if (_shownEvolution == evo) return;
            _shownEvolution = evo;
            EnsureRing();
            _ring.gameObject.SetActive(true);
            if (_accessory != null) Destroy(_accessory);
            _accessory = null;
            GameObject prefab = AccessoryPrefab(evo.accessory);
            if (prefab != null) _accessory = AttachAccessory(prefab, evo.accessory == EvolutionAccessory.Root);
        }

        private GameObject AccessoryPrefab(EvolutionAccessory a)
        {
            if (_lib == null) return null;
            switch (a)
            {
                case EvolutionAccessory.Solar: return _lib.evoSolar;
                case EvolutionAccessory.Prism: return _lib.evoPrism;
                case EvolutionAccessory.Root: return _lib.evoRoot;
                case EvolutionAccessory.Bloom: return _lib.evoBloom;
                default: return null;
            }
        }

        /// <summary>Instantiates the accessory, normalises its size and parents it to the top part (or the base for roots).</summary>
        private GameObject AttachAccessory(GameObject prefab, bool atBase)
        {
            GameObject go = Instantiate(prefab);
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.transform.position = Vector3.zero;
            Bounds b = RendererBounds(go.transform, out bool any);
            if (!any) { Destroy(go); return null; }
            float target = atBase ? 0.85f : 0.42f; // vine ring spans the cell base; crowns/prisms/halos sit on top
            float extent = Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
            float k = target / extent;
            go.transform.localScale = Vector3.one * k;
            b = RendererBounds(go.transform, out _);
            Transform parent = transform;
            Vector3 anchor = transform.position + Vector3.up * 0.03f;
            if (!atBase)
            {
                Transform top = null;
                foreach (string n in TopParts) { top = _rig.Get(n); if (top != null) break; }
                Bounds tb = top != null ? RendererBounds(top, out bool hasTop) : RendererBounds(_rig.Model, out hasTop);
                if (!hasTop) tb = new Bounds(transform.position + Vector3.up * 0.8f, Vector3.one * 0.2f);
                anchor = new Vector3(tb.center.x, tb.max.y - 0.06f, tb.center.z);
                parent = top != null ? top : _rig.Model;
            }
            go.transform.position += anchor - new Vector3(b.center.x, b.min.y, b.center.z);
            go.transform.SetParent(parent, true);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private static Bounds RendererBounds(Transform t, out bool any)
        {
            any = false;
            var b = new Bounds(t.position, Vector3.zero);
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private void EnsureRing()
        {
            if (_ring != null || _lib == null) return;
            var go = new GameObject("EvolutionRing");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = _lib.quadMesh;
            _ringRenderer = go.AddComponent<MeshRenderer>();
            _ringRenderer.sharedMaterial = _lib.glowAdditive;
            _ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ringRenderer.receiveShadows = false;
            _ringMpb = new MaterialPropertyBlock();
            _ring = go.transform;
        }

        private void TickEvolution(float dt, float time)
        {
            if (_d == null) return;
            EvolutionDefinition evo = _d.evolution;
            if (evo != null && _shownEvolution != evo) ShowEvolved(evo); // e.g. view re-bound to an evolved runtime
            _scaleBlend = Mathf.MoveTowards(_scaleBlend, evo != null ? 1f : 0f, dt * 2.5f);
            if (evo == null)
            {
                if (_d.EvolutionReady && _vfx != null)
                {
                    _readySparkle -= dt;
                    if (_readySparkle <= 0f)
                    {
                        _readySparkle = 0.8f;
                        _vfx.Burst(VfxKind.CollectSparkle, transform.position + new Vector3(Random.Range(-0.25f, 0.25f), 0.4f, Random.Range(-0.2f, 0.2f)),
                            new Color(1f, 0.9f, 0.5f), 3, 0.8f, 0.09f, 0.9f);
                    }
                }
                return;
            }
            _evolveAge += dt;
            if (_ring != null)
            {
                float grow = Mathf.Clamp01(_evolveAge / 0.6f);
                float size = Mathf.Lerp(2.8f, 1.25f, 1f - (1f - grow) * (1f - grow)) + Mathf.Sin(time * 2.2f) * 0.05f;
                _ring.localScale = new Vector3(size, size, 1f);
                _ring.localRotation = Quaternion.Euler(90f, time * 25f % 360f, 0f);
                Color a = evo.accentColor;
                float alpha = Mathf.Lerp(1f, 0.8f + Mathf.Sin(time * 2.6f) * 0.12f, grow);
                Color rc = Color.Lerp(a, Color.white, 0.3f); // brighter than the accent so green branches still read on grass
                _ringMpb.SetColor(BaseColorId, new Color(rc.r, rc.g, rc.b, alpha));
                _ringRenderer.SetPropertyBlock(_ringMpb);
            }
            // Capacitor: the cap swells and glows with its stored charge.
            if (evo.capacitorPulses > 0 && _rig.Has("Cap"))
            {
                float charge = _d.storedPulses / (float)evo.capacitorPulses;
                bool full = _d.storedPulses >= evo.capacitorPulses;
                float c = 1f + charge * 0.14f + (full ? Mathf.Sin(time * 9f) * 0.04f : 0f);
                Transform cap = _rig.Get("Cap");
                cap.localScale *= c;
            }
        }

        private void EvolutionTint(float time, ref Color c, ref float strength)
        {
            if (_d == null) return;
            EvolutionDefinition evo = _d.evolution;
            if (evo == null)
            {
                if (!_d.EvolutionReady) return;
                c = new Color(1f, 0.92f, 0.6f);
                strength = 0.16f + 0.1f * Mathf.Sin(time * 4f);
                return;
            }
            if (evo.capacitorPulses > 0 && _d.storedPulses > 0)
            {
                float charge = _d.storedPulses / (float)evo.capacitorPulses;
                bool full = _d.storedPulses >= evo.capacitorPulses;
                c = new Color(1f, 0.9f, 0.45f);
                strength = 0.1f + 0.25f * charge + (full ? 0.12f * Mathf.Sin(time * 9f) : 0f);
                return;
            }
            c = evo.accentColor;
            strength = 0.12f + 0.04f * Mathf.Sin(time * 2f);
        }
    }
}
