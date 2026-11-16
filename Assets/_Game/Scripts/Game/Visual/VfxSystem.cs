using UnityEngine;

namespace VerdantSiege.Visual
{
    public enum VfxKind
    {
        HitSpark = 0,
        SporePuff,
        PlantDust,
        CollectSparkle,
        ArmorDebris,
        MuzzleFlash,
        Breach,
        Glow
    }

    /// <summary>
    /// One pooled ParticleSystem per effect kind; bursts are emitted with EmitParams at any position, so a
    /// hundred hits cost no instantiation. Pure presentation: gameplay never waits on these. Particle budgets
    /// scale with the quality level.
    /// </summary>
    public sealed class VfxSystem : MonoBehaviour
    {
        private ParticleSystem[] _systems;
        private float _budget = 1f;
        private bool _reducedFlashes;

        public static VfxSystem Create(Transform parent, VisualLibrary lib, int qualityLevel, bool reducedFlashes)
        {
            var go = new GameObject("VFX");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<VfxSystem>();
            v._budget = qualityLevel == 0 ? 0.55f : qualityLevel == 1 ? 0.8f : 1f;
            v._reducedFlashes = reducedFlashes;
            v.Build(lib);
            return v;
        }

        private void Build(VisualLibrary lib)
        {
            int n = System.Enum.GetValues(typeof(VfxKind)).Length;
            _systems = new ParticleSystem[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject(((VfxKind)i).ToString());
                go.transform.SetParent(transform, false);
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = ps.main;
                main.loop = false;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = (int)(220 * _budget);
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                ParticleSystem.EmissionModule em = ps.emission;
                em.enabled = false;
                ParticleSystem.ShapeModule sh = ps.shape;
                sh.enabled = false;
                ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = lib != null ? lib.glowAdditive : null;
                r.renderMode = ParticleSystemRenderMode.Billboard;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                ParticleSystem.MainModule m2 = ps.main;
                m2.gravityModifier = (VfxKind)i == VfxKind.ArmorDebris ? 1.6f : (VfxKind)i == VfxKind.PlantDust ? 0.4f : 0f;
                ps.Play();
                _systems[i] = ps;
            }
        }

        /// <summary>Emits a radial burst.</summary>
        public void Burst(VfxKind kind, Vector3 pos, Color color, int count, float speed = 1.5f, float size = 0.18f, float life = 0.45f)
        {
            if (_systems == null) return;
            if (_reducedFlashes && (kind == VfxKind.MuzzleFlash || kind == VfxKind.Breach)) { count = Mathf.Max(1, count / 2); size *= 0.7f; }
            ParticleSystem ps = _systems[(int)kind];
            int n = Mathf.Max(1, Mathf.RoundToInt(count * _budget));
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                if (kind == VfxKind.PlantDust || kind == VfxKind.ArmorDebris) dir.y = Mathf.Abs(dir.y) + 0.4f;
                if (kind == VfxKind.CollectSparkle) dir.y = Mathf.Abs(dir.y) * 1.5f;
                p.position = pos + dir * 0.05f;
                p.velocity = dir * speed * Random.Range(0.5f, 1f);
                p.startColor = color;
                p.startSize = size * Random.Range(0.7f, 1.3f);
                p.startLifetime = life * Random.Range(0.75f, 1.2f);
                ps.Emit(p, 1);
            }
        }

        /// <summary>Single static glow sprite (flash, telegraph).</summary>
        public void Pulse(VfxKind kind, Vector3 pos, Color color, float size, float life)
        {
            if (_systems == null) return;
            var p = new ParticleSystem.EmitParams
            {
                position = pos, velocity = Vector3.zero, startColor = color, startSize = size, startLifetime = life,
                applyShapeToPosition = false
            };
            _systems[(int)kind].Emit(p, 1);
        }
    }
}
