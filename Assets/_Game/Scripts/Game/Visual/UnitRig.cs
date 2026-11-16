using System.Collections.Generic;
using UnityEngine;

namespace VerdantSiege.Visual
{
    /// <summary>Critically damped-ish spring for punchy procedural motion.</summary>
    public struct Spring
    {
        public float value;
        public float velocity;

        public void Kick(float impulse) => velocity += impulse;

        /// <summary>
        /// Semi-implicit Euler in fixed sub-steps of at most 1/120 s, so it stays stable at any frame time
        /// (slow devices, 2x game speed, hitches). Non-finite state is reset.
        /// </summary>
        public void Tick(float dt, float stiffness = 180f, float damping = 14f)
        {
            if (dt <= 0f) return;
            if (dt > 0.25f) dt = 0.25f;
            int steps = Mathf.CeilToInt(dt * 120f);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                velocity += (-stiffness * value - damping * velocity) * h;
                value += velocity * h;
            }
            if (float.IsNaN(value) || float.IsInfinity(value) || float.IsNaN(velocity)) Reset();
        }

        public void Reset() { value = 0f; velocity = 0f; }
    }

    /// <summary>
    /// Part-based procedural animation rig for Blender-generated units. Parts are found by name once (Awake);
    /// base poses are cached so every pose is computed from rest (no drift). Also owns the hit flash
    /// (MaterialPropertyBlock only while flashing, so SRP batching resumes afterwards).
    /// </summary>
    public sealed class UnitRig : MonoBehaviour
    {
        private struct Part
        {
            public Transform t;
            public Vector3 pos;
            public Quaternion rot;
            public Vector3 scale;
        }

        private static readonly int FlashId = Shader.PropertyToID("_FlashColor");
        private readonly Dictionary<string, Part> _parts = new Dictionary<string, Part>();
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private float _flash;
        private Color _flashColor = Color.white;
        private bool _flashApplied;

        /// <summary>Visual model root (child of this object) used for whole-body squash/turn.</summary>
        public Transform Model { get; private set; }
        public Vector3 ModelBaseScale { get; private set; } = Vector3.one;
        public Quaternion ModelBaseRotation { get; private set; } = Quaternion.identity;

        private void Awake()
        {
            Model = transform.childCount > 0 ? transform.GetChild(0) : transform;
            ModelBaseScale = Model.localScale;
            ModelBaseRotation = Model.localRotation;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t == transform || _parts.ContainsKey(t.name)) continue;
                _parts[t.name] = new Part { t = t, pos = t.localPosition, rot = t.localRotation, scale = t.localScale };
            }
            _renderers = GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
        }

        public bool Has(string part) => _parts.ContainsKey(part);
        public Transform Get(string part) => _parts.TryGetValue(part, out Part p) ? p.t : null;

        public void SetActive(string part, bool active)
        {
            if (_parts.TryGetValue(part, out Part p) && p.t.gameObject.activeSelf != active) p.t.gameObject.SetActive(active);
        }

        /// <summary>Pose a part relative to its rest pose: euler offset (degrees), position offset, uniform-ish scale.</summary>
        public void Pose(string part, Vector3 eulerOffset, Vector3 posOffset, Vector3 scaleMul)
        {
            if (!_parts.TryGetValue(part, out Part p)) return;
            p.t.localRotation = p.rot * Quaternion.Euler(eulerOffset);
            p.t.localPosition = p.pos + posOffset;
            p.t.localScale = Vector3.Scale(p.scale, scaleMul);
        }

        public void Pose(string part, Vector3 eulerOffset) => Pose(part, eulerOffset, Vector3.zero, Vector3.one);

        public void SetModelSquash(Vector3 scaleMul, float yawDegrees = 0f)
        {
            Model.localScale = Vector3.Scale(ModelBaseScale, scaleMul);
            Model.localRotation = Quaternion.Euler(0f, yawDegrees, 0f) * ModelBaseRotation;
        }

        public void ResetPose()
        {
            foreach (KeyValuePair<string, Part> kv in _parts)
            {
                Part p = kv.Value;
                p.t.localPosition = p.pos;
                p.t.localRotation = p.rot;
                p.t.localScale = p.scale;
                if (!p.t.gameObject.activeSelf) p.t.gameObject.SetActive(true);
            }
            Model.localScale = ModelBaseScale;
            Model.localRotation = ModelBaseRotation;
            _flash = 0f;
            ApplyFlash(true);
        }

        public void Flash(Color c, float seconds = 0.09f)
        {
            _flashColor = c;
            _flash = Mathf.Max(_flash, seconds);
        }

        /// <summary>Call every frame from the owning view.</summary>
        public void TickFlash(float dt)
        {
            if (_flash <= 0f && !_flashApplied) return;
            _flash -= dt;
            ApplyFlash(false);
        }

        private void ApplyFlash(bool force)
        {
            if (_renderers == null) return;
            if (_flash > 0f)
            {
                _mpb.SetColor(FlashId, new Color(_flashColor.r, _flashColor.g, _flashColor.b, Mathf.Clamp01(_flash / 0.09f) * 0.75f));
                for (int i = 0; i < _renderers.Length; i++) _renderers[i].SetPropertyBlock(_mpb);
                _flashApplied = true;
            }
            else if (_flashApplied || force)
            {
                for (int i = 0; i < _renderers.Length; i++) _renderers[i].SetPropertyBlock(null);
                _flashApplied = false;
            }
        }
    }
}
