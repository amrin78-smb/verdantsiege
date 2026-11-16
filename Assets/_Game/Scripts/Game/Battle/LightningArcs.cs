using UnityEngine;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Storm Orchid chain lightning: short-lived jagged LineRenderer segments between the chain points (pooled,
    /// one renderer per hop, re-jittered every few frames while visible). Purely visual.
    /// </summary>
    public sealed class LightningArcs : MonoBehaviour
    {
        private const int MaxArcs = 16;
        private const int Points = 7;
        private const float Life = 0.22f;
        private LineRenderer[] _lines;
        private Vector3[] _from, _to;
        private float[] _life;
        private readonly Vector3[] _scratch = new Vector3[Points];
        private float _jitterTimer;

        public void Init(VisualLibrary lib)
        {
            _lines = new LineRenderer[MaxArcs];
            _from = new Vector3[MaxArcs];
            _to = new Vector3[MaxArcs];
            _life = new float[MaxArcs];
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.85f, 0.9f, 1f), 0f), new GradientColorKey(new Color(0.6f, 0.65f, 1f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            for (int i = 0; i < MaxArcs; i++)
            {
                var go = new GameObject("Arc" + i);
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = Points;
                lr.useWorldSpace = true;
                lr.sharedMaterial = lib != null ? lib.glowAdditive : null;
                lr.colorGradient = g;
                lr.widthMultiplier = 0.12f;
                lr.numCapVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;
                _lines[i] = lr;
            }
        }

        /// <summary>Shows one arc from a to b.</summary>
        public void Arc(Vector3 a, Vector3 b)
        {
            if (_lines == null) return;
            int slot = 0;
            float oldest = float.MaxValue;
            for (int i = 0; i < MaxArcs; i++)
            {
                if (_life[i] <= 0f) { slot = i; break; }
                if (_life[i] < oldest) { oldest = _life[i]; slot = i; }
            }
            _from[slot] = a;
            _to[slot] = b;
            _life[slot] = Life;
            _lines[slot].enabled = true;
            Jitter(slot);
        }

        private void Jitter(int i)
        {
            Vector3 a = _from[i], b = _to[i];
            float len = Vector3.Distance(a, b);
            for (int k = 0; k < Points; k++)
            {
                float t = k / (float)(Points - 1);
                Vector3 p = Vector3.Lerp(a, b, t);
                if (k > 0 && k < Points - 1) p += Random.insideUnitSphere * Mathf.Min(0.18f, len * 0.08f);
                _scratch[k] = p;
            }
            _lines[i].SetPositions(_scratch);
        }

        private void LateUpdate()
        {
            if (_lines == null) return;
            float dt = Time.deltaTime;
            _jitterTimer -= dt;
            bool jitter = _jitterTimer <= 0f;
            if (jitter) _jitterTimer = 0.04f;
            for (int i = 0; i < MaxArcs; i++)
            {
                if (_life[i] <= 0f) continue;
                _life[i] -= dt;
                if (_life[i] <= 0f) { _lines[i].enabled = false; continue; }
                _lines[i].widthMultiplier = 0.05f + 0.1f * (_life[i] / Life);
                if (jitter) Jitter(i);
            }
        }
    }
}
