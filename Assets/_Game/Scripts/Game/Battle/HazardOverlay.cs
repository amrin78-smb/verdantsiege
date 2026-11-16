using UnityEngine;
using VerdantSiege.Grid;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Ground decals for battlefield hazards, derived from simulation state every frame: boss strike warnings
    /// (pulsing red, brightening as the strike nears), root-blocked cells (dark bramble) and spore clouds
    /// (sickly green with drifting spores). Sits just under the placement highlights so those stay readable.
    /// </summary>
    public sealed class HazardOverlay : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color Warning = new Color(1f, 0.22f, 0.2f, 0.75f);
        private static readonly Color Roots = new Color(0.32f, 0.16f, 0.24f, 0.8f);
        private static readonly Color Cloud = new Color(0.62f, 0.85f, 0.3f, 0.5f);

        private MeshRenderer[,] _cells;
        private MaterialPropertyBlock _mpb;
        private BattleSimulation _sim;
        private BattleLayout _layout;
        private VfxSystem _vfx;
        private bool[,] _warn;
        private float _puffTimer;

        public void Init(BattleSimulation sim, BattleLayout layout, VisualLibrary lib, VfxSystem vfx)
        {
            _sim = sim;
            _layout = layout;
            _vfx = vfx;
            _mpb = new MaterialPropertyBlock();
            _cells = new MeshRenderer[layout.Lanes, layout.Columns];
            _warn = new bool[layout.Lanes, layout.Columns];
            for (int l = 0; l < layout.Lanes; l++)
            for (int c = 0; c < layout.Columns; c++)
            {
                var go = new GameObject($"Hazard_{l}_{c}");
                go.transform.SetParent(transform, false);
                go.transform.position = layout.CellCenter(new GridCoord(l, c)) + Vector3.up * 0.07f;
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                go.transform.localScale = new Vector3(layout.CellWidth * 0.98f, layout.LaneDepth * 0.98f, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = lib.quadMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = lib.cellHighlight;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false;
                _cells[l, c] = mr;
            }
            sim.CellBlockedChanged += OnCellBlocked;
            sim.CloudRemoved += OnCloudRemoved;
        }

        private void OnDestroy()
        {
            if (_sim == null) return;
            _sim.CellBlockedChanged -= OnCellBlocked;
            _sim.CloudRemoved -= OnCloudRemoved;
        }

        private void OnCellBlocked(GridCoord c, bool blocked)
        {
            if (_vfx == null) return;
            Vector3 p = _layout.CellCenter(c);
            if (blocked)
            {
                _vfx.Burst(VfxKind.ArmorDebris, p + Vector3.up * 0.2f, new Color(0.45f, 0.28f, 0.22f), 16, 3f, 0.2f, 0.9f);
                _vfx.Burst(VfxKind.SporePuff, p + Vector3.up * 0.4f, new Color(0.55f, 0.25f, 0.5f), 10, 1.6f, 0.25f, 0.7f);
            }
            else _vfx.Burst(VfxKind.PlantDust, p + Vector3.up * 0.1f, new Color(0.5f, 0.4f, 0.3f), 8, 1f, 0.14f, 0.5f);
        }

        private void OnCloudRemoved(CloudRuntime c, bool burned)
        {
            if (!burned || _vfx == null) return;
            Vector3 p = _layout.CellCenter(c.coord);
            _vfx.Burst(VfxKind.HitSpark, p + Vector3.up * 0.4f, new Color(1f, 0.55f, 0.2f), 20, 2.4f, 0.2f, 0.6f);
            _vfx.Pulse(VfxKind.Glow, p + Vector3.up * 0.3f, new Color(1f, 0.5f, 0.15f, 0.8f), 1.6f, 0.35f);
        }

        private void LateUpdate()
        {
            if (_cells == null || _sim == null) return;
            float time = Time.time;
            System.Array.Clear(_warn, 0, _warn.Length);
            BossRuntime boss = _sim.Boss;
            float urgency = 0f;
            if (boss != null && boss.state == BossState.Telegraph && boss.currentAttack != null)
            {
                foreach (GridCoord g in boss.targetCells) if (_layout.InBounds(g)) _warn[g.lane, g.column] = true;
                urgency = 1f - Mathf.Clamp01(boss.stateTimer / Mathf.Max(0.1f, boss.currentAttack.telegraphSeconds));
            }
            _puffTimer -= Time.deltaTime;
            bool puff = _puffTimer <= 0f;
            if (puff) _puffTimer = 0.35f;

            for (int l = 0; l < _layout.Lanes; l++)
            for (int c = 0; c < _layout.Columns; c++)
            {
                var coord = new GridCoord(l, c);
                MeshRenderer mr = _cells[l, c];
                Color col = default;
                bool show = false;
                if (_warn[l, c])
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(time * (8f + urgency * 14f));
                    col = Warning;
                    col.a = Mathf.Lerp(0.35f, 0.9f, urgency) * (0.65f + 0.35f * pulse);
                    show = true;
                }
                else if (_sim.Grid.GetTerrain(coord) == CellTerrain.Blocked && _sim.IsRootBlocked(coord))
                {
                    col = Roots;
                    col.a *= 0.85f + 0.15f * Mathf.Sin(time * 2f + c);
                    show = true;
                }
                else
                {
                    CloudRuntime cloud = _sim.CloudAt(coord);
                    if (cloud != null)
                    {
                        col = Cloud;
                        col.a *= Mathf.Clamp01(cloud.remaining / 1.2f) * (0.8f + 0.2f * Mathf.Sin(time * 3f + l));
                        show = true;
                        if (puff && _vfx != null)
                            _vfx.Burst(VfxKind.SporePuff, _layout.CellCenter(coord) + new Vector3(Random.Range(-0.3f, 0.3f), 0.25f, Random.Range(-0.3f, 0.3f)),
                                new Color(0.6f, 0.85f, 0.3f), 2, 0.35f, 0.3f, 1.1f);
                    }
                }
                if (mr.enabled != show) mr.enabled = show;
                if (show)
                {
                    _mpb.SetColor(ColorId, col);
                    mr.SetPropertyBlock(_mpb);
                }
            }
        }
    }
}
