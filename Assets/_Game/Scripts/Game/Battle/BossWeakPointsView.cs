using System.Collections.Generic;
using UnityEngine;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Glowing weak-point pods on the boss's roots, one per <see cref="BossWeakPoint"/>. Each pod sits on its lane's hit
    /// zone (just past the last column, in front of the boss) so what you see is what the simulation hits.
    /// Art contract: parts named <c>WeakPoint0..n</c> on the boss model are used (moved onto the lane hit zones, ordered
    /// by lane); when a part is missing a code-built pod (emissive vertex-colour bulb on a root stalk) is created.
    /// Polls the runtime each frame: grow on exposure, flash/shake on hits, pop when destroyed, sink when withdrawn.
    /// </summary>
    public sealed class BossWeakPointsView : MonoBehaviour
    {
        private sealed class Pod
        {
            public Transform root;      // positioned on the hit zone
            public Transform bulb;      // scaled/pulsed
            public Vector3 bulbScale;
            public Vector3 restLocal;   // art part rest position (parent space)
            public bool fromArt;
            public BossWeakPoint source;
            public bool shown;
            public float lastHealth;
            public float grow;          // 0..1 appear
            public float hitKick;
            public float popTime = -1f; // >=0 while popping
            public bool withdrawing;
        }

        private static Mesh _bulbMesh, _stalkMesh;
        private readonly List<Pod> _pods = new List<Pod>();
        private BossRuntime _boss;
        private UnitRig _rig;
        private BattleLayout _layout;
        private VfxSystem _vfx;
        private VisualLibrary _lib;
        private float _frontX;
        private float _glowTimer;
        private readonly List<int> _lanes = new List<int>(4);

        public int Count => _pods.Count;

        public void Init(BossRuntime boss, UnitRig rig, BattleLayout layout, VisualLibrary lib, VfxSystem vfx, float frontX)
        {
            _boss = boss;
            _rig = rig;
            _layout = layout;
            _lib = lib;
            _vfx = vfx;
            _frontX = frontX;
            boss.Definition.ResolveWeakPointLanes(layout.Lanes, _lanes);
            foreach (Pod p in _pods) if (p.root != null && !p.fromArt) Destroy(p.root.gameObject);
            _pods.Clear();
            // Art parts are matched to lanes by their order along the lanes (world Z), so the model need not know lane ids.
            var art = new List<Transform>();
            for (int i = 0; i < 8; i++) { Transform t = rig.Get("WeakPoint" + i); if (t != null) art.Add(t); }
            art.Sort((a, b) => a.position.z.CompareTo(b.position.z));
            for (int i = 0; i < _lanes.Count; i++)
            {
                var pod = new Pod();
                if (i < art.Count)
                {
                    pod.fromArt = true;
                    pod.root = art[i];
                    pod.bulb = art[i];
                    pod.restLocal = art[i].localPosition;
                }
                else
                {
                    pod.root = BuildPod(i).transform;
                    pod.bulb = pod.root.Find("Bulb");
                }
                pod.bulbScale = pod.bulb.localScale;
                pod.root.gameObject.SetActive(false);
                _pods.Add(pod);
            }
        }

        /// <summary>World position above the pod for lane-ordered index i (top of the bulb), for HUD anchoring.</summary>
        public Vector3 PodTop(int index) => PodCentre(index) + Vector3.up * 0.42f;

        public bool PodVisible(int index) => index >= 0 && index < _pods.Count && _pods[index].shown && _pods[index].popTime < 0f;

        /// <summary>Centre of the pod guarding a lane (for hit effects), or false when no pod is in that lane.</summary>
        public bool TryPodCentreForLane(int lane, out Vector3 centre)
        {
            int i = _lanes.IndexOf(lane);
            centre = i >= 0 && i < _pods.Count ? PodCentre(i) : Vector3.zero;
            return i >= 0 && i < _pods.Count;
        }

        /// <summary>
        /// Pod centre: art parts keep their modelled height and depth in front of the body (following its pose) but are
        /// slid along the lanes onto their lane's centre; code-built pods stand on the sim's hit zone past the last column.
        /// </summary>
        private Vector3 PodCentre(int index)
        {
            int lane = index < _lanes.Count ? _lanes[index] : 0;
            Vector3 zone = _layout.ToWorld(_frontX + 0.3f, lane);
            if (index < _pods.Count && _pods[index].fromArt && _pods[index].root.parent != null)
            {
                Vector3 rest = _pods[index].root.parent.TransformPoint(_pods[index].restLocal);
                return new Vector3(rest.x, rest.y, zone.z);
            }
            return zone + Vector3.up * 0.55f;
        }

        private Vector3 HitZone(int index) => PodCentre(index) - Vector3.up * 0.55f;

        /// <summary>Death: every pod bursts (shown briefly if it was hidden).</summary>
        public void BurstAll()
        {
            for (int i = 0; i < _pods.Count; i++)
            {
                Pod p = _pods[i];
                if (_vfx != null)
                {
                    Vector3 c = HitZone(i) + Vector3.up * 0.55f;
                    _vfx.Burst(VfxKind.HitSpark, c, new Color(1f, 0.85f, 0.4f), 26, 4f, 0.2f, 0.6f);
                    _vfx.Pulse(VfxKind.Glow, c, new Color(1f, 0.6f, 0.9f, 0.9f), 2.6f, 0.4f);
                }
                if (p.shown) p.popTime = 0f;
            }
        }

        public void Tick(float dt, float time, float deathTime)
        {
            if (_boss == null) return;
            for (int i = 0; i < _pods.Count; i++)
            {
                Pod p = _pods[i];
                BossWeakPoint w = i < _boss.weakPoints.Count ? _boss.weakPoints[i] : null;
                bool exposed = w != null && w.alive && deathTime < 0f;
                if (exposed && (!p.shown || p.withdrawing || p.popTime >= 0f))
                {
                    p.shown = true;
                    p.withdrawing = false;
                    p.popTime = -1f;
                    p.grow = 0f;
                    p.source = w;
                    p.lastHealth = w.health;
                    p.root.gameObject.SetActive(true);
                    if (_vfx != null) _vfx.Burst(VfxKind.PlantDust, HitZone(i) + Vector3.up * 0.1f, new Color(0.5f, 0.38f, 0.28f), 12, 2f, 0.24f, 0.8f);
                }
                if (!p.shown) continue;

                if (exposed)
                {
                    if (w.health < p.lastHealth - 0.01f)
                    {
                        p.hitKick = 1f;
                        if (_vfx != null) _vfx.Burst(VfxKind.HitSpark, HitZone(i) + Vector3.up * 0.55f, new Color(1f, 0.8f, 0.5f), 5, 2f, 0.12f, 0.25f);
                    }
                    p.lastHealth = w.health;
                }
                else if (p.popTime < 0f && !p.withdrawing)
                {
                    // Gone from the runtime: destroyed (health 0) pops, otherwise it withdraws into the soil.
                    if (p.source != null && p.source.health <= 0f) Pop(p, i);
                    else p.withdrawing = true;
                }

                p.grow = Mathf.MoveTowards(p.grow, p.withdrawing ? 0f : 1f, dt * (p.withdrawing ? 2.5f : 3.5f));
                p.hitKick = Mathf.MoveTowards(p.hitKick, 0f, dt * 6f);
                float pulse = 1f + 0.12f * Mathf.Sin(time * 7f + i * 1.7f);
                float frac = exposed ? w.HealthFraction : 0f;
                float popScale = 1f;
                if (p.popTime >= 0f)
                {
                    p.popTime += dt;
                    popScale = p.popTime < 0.08f ? 1f + p.popTime * 6f : Mathf.Max(0f, 1.5f - (p.popTime - 0.08f) * 8f);
                    if (p.popTime > 0.3f) { Hide(p); continue; }
                }
                if (p.withdrawing && p.grow <= 0f) { Hide(p); continue; }

                float ease = 1f - (1f - p.grow) * (1f - p.grow);
                Vector3 zone = HitZone(i);
                Vector3 shake = p.hitKick > 0f ? new Vector3(Mathf.Sin(time * 60f), 0f, Mathf.Cos(time * 53f)) * 0.05f * p.hitKick : Vector3.zero;
                if (p.fromArt)
                {
                    // Swells out of the bark (scale) rather than rising from the soil.
                    p.root.position = PodCentre(i) + shake;
                    p.root.localScale = p.bulbScale * (Mathf.Max(0.05f, ease) * pulse * popScale * (0.8f + 0.2f * frac) * (1f + p.hitKick * 0.15f));
                }
                else
                {
                    p.root.position = zone + Vector3.down * (0.6f * (1f - ease)) + shake;
                    p.root.rotation = Quaternion.identity;
                    Vector3 world = Vector3.one * (pulse * popScale * (0.8f + 0.2f * frac) * (1f + p.hitKick * 0.15f));
                    p.bulb.localScale = Vector3.Scale(p.bulbScale, world) / Mathf.Max(0.01f, transform.lossyScale.x);
                }
            }

            // Shared halo glow on exposed pods (readable at phone size), faster as the wind-up runs out.
            _glowTimer -= dt;
            if (_glowTimer <= 0f && _vfx != null && _boss.WeakPointsExposed)
            {
                float urgency = _boss.stateDuration > 0f ? 1f - Mathf.Clamp01(_boss.stateTimer / _boss.stateDuration) : 0f;
                _glowTimer = Mathf.Lerp(0.35f, 0.14f, urgency);
                for (int i = 0; i < _pods.Count; i++)
                    if (PodVisible(i) && !_pods[i].withdrawing)
                        _vfx.Pulse(VfxKind.Glow, HitZone(i) + Vector3.up * 0.55f, new Color(1f, 0.75f, 0.3f, 0.55f + 0.3f * urgency), 1.3f, 0.3f);
            }
        }

        private void Pop(Pod p, int index)
        {
            p.popTime = 0f;
            if (_vfx == null) return;
            Vector3 c = HitZone(index) + Vector3.up * 0.55f;
            _vfx.Burst(VfxKind.HitSpark, c, new Color(1f, 0.85f, 0.4f), 30, 4.2f, 0.2f, 0.6f);
            _vfx.Burst(VfxKind.SporePuff, c, new Color(1f, 0.5f, 0.85f), 18, 2.4f, 0.28f, 0.8f);
            _vfx.Pulse(VfxKind.Glow, c, new Color(1f, 0.85f, 0.5f, 1f), 2.8f, 0.35f);
        }

        private static void Hide(Pod p)
        {
            p.shown = false;
            p.withdrawing = false;
            p.popTime = -1f;
            p.grow = 0f;
            p.source = null;
            p.root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ code-built pod (placeholder until art lands)

        private GameObject BuildPod(int index)
        {
            var root = new GameObject("WeakPointPod" + index);
            root.transform.SetParent(transform, false);
            var stalk = new GameObject("Stalk");
            stalk.transform.SetParent(root.transform, false);
            stalk.AddComponent<MeshFilter>().sharedMesh = StalkMesh();
            Renderer sr = stalk.AddComponent<MeshRenderer>();
            sr.sharedMaterial = _lib != null ? _lib.vertexLit : null;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var bulb = new GameObject("Bulb");
            bulb.transform.SetParent(root.transform, false);
            bulb.transform.localPosition = new Vector3(0f, 0.55f, 0f) / Mathf.Max(0.01f, transform.lossyScale.x);
            bulb.transform.localScale = Vector3.one;
            bulb.AddComponent<MeshFilter>().sharedMesh = BulbMesh();
            Renderer br = bulb.AddComponent<MeshRenderer>();
            br.sharedMaterial = _lib != null ? _lib.vertexLit : null;
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Root object is in boss space (scaled); compensate so the pod has the same world size on any boss scale.
            stalk.transform.localScale = Vector3.one / Mathf.Max(0.01f, transform.lossyScale.x);
            return root;
        }

        /// <summary>Lumpy glowing bulb (radius ~0.24 m): vertex alpha = emission (VS_VertexLit contract).</summary>
        private static Mesh BulbMesh()
        {
            if (_bulbMesh != null) return _bulbMesh;
            const int rings = 10, sides = 14;
            var v = new List<Vector3>();
            var c = new List<Color>();
            var tris = new List<int>();
            Color hot = new Color(1f, 0.92f, 0.45f, 1f), rim = new Color(1f, 0.35f, 0.75f, 0.9f), husk = new Color(0.35f, 0.18f, 0.3f, 0.15f);
            for (int r = 0; r <= rings; r++)
            {
                float a = Mathf.PI * r / rings;
                for (int s = 0; s <= sides; s++)
                {
                    float b = 2f * Mathf.PI * s / sides;
                    float lump = 1f + 0.08f * Mathf.Sin(b * 3f + a * 2f) + 0.05f * Mathf.Sin(b * 5f - a * 3f);
                    var n = new Vector3(Mathf.Sin(a) * Mathf.Cos(b), Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(b));
                    v.Add(new Vector3(n.x * 0.22f, n.y * 0.27f, n.z * 0.22f) * lump);
                    float up = n.y * 0.5f + 0.5f;
                    // Glowing veins between darker husk plates.
                    float vein = Mathf.Abs(Mathf.Sin(b * 3f)) < 0.35f ? 1f : 0f;
                    Color col = Color.Lerp(rim, hot, up);
                    col = Color.Lerp(husk, col, Mathf.Clamp01(0.55f + vein * 0.6f + up * 0.3f));
                    c.Add(col);
                }
            }
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < sides; s++)
            {
                int i0 = r * (sides + 1) + s, i1 = i0 + sides + 1;
                tris.Add(i0); tris.Add(i0 + 1); tris.Add(i1);
                tris.Add(i0 + 1); tris.Add(i1 + 1); tris.Add(i1);
            }
            _bulbMesh = new Mesh { name = "WeakPointBulb" };
            _bulbMesh.SetVertices(v);
            _bulbMesh.SetColors(c);
            _bulbMesh.SetTriangles(tris, 0);
            _bulbMesh.RecalculateNormals();
            _bulbMesh.RecalculateBounds();
            return _bulbMesh;
        }

        /// <summary>Curved tapered root stalk from the soil (y 0) to the bulb (y ~0.55).</summary>
        private static Mesh StalkMesh()
        {
            if (_stalkMesh != null) return _stalkMesh;
            const int segs = 6, sides = 8;
            var v = new List<Vector3>();
            var c = new List<Color>();
            var tris = new List<int>();
            for (int k = 0; k <= segs; k++)
            {
                float t = (float)k / segs;
                var centre = new Vector3(0.12f * Mathf.Sin(t * Mathf.PI) + 0.1f * (1f - t), t * 0.5f, 0f);
                float radius = Mathf.Lerp(0.11f, 0.05f, t);
                for (int s = 0; s <= sides; s++)
                {
                    float b = 2f * Mathf.PI * s / sides;
                    v.Add(centre + new Vector3(Mathf.Cos(b) * radius, 0f, Mathf.Sin(b) * radius));
                    c.Add(Color.Lerp(new Color(0.3f, 0.19f, 0.17f, 0f), new Color(0.75f, 0.3f, 0.6f, 0.5f), t * t));
                }
            }
            for (int k = 0; k < segs; k++)
            for (int s = 0; s < sides; s++)
            {
                int i0 = k * (sides + 1) + s, i1 = i0 + sides + 1;
                tris.Add(i0); tris.Add(i1); tris.Add(i0 + 1);
                tris.Add(i0 + 1); tris.Add(i1); tris.Add(i1 + 1);
            }
            _stalkMesh = new Mesh { name = "WeakPointStalk" };
            _stalkMesh.SetVertices(v);
            _stalkMesh.SetColors(c);
            _stalkMesh.SetTriangles(tris, 0);
            _stalkMesh.RecalculateNormals();
            _stalkMesh.RecalculateBounds();
            return _stalkMesh;
        }
    }
}
