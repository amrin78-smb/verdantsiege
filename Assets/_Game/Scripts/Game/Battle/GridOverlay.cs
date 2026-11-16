using UnityEngine;
using VerdantSiege.Grid;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Placement feedback: highlights valid cells while a card is selected, a stronger hover cell under the
    /// finger while dragging, a red flash on invalid taps, and an optional debug grid (coordinates + terrain).
    /// </summary>
    public sealed class GridOverlay : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private MeshRenderer[,] _cells;
        private float[,] _invalidFlash;
        private MaterialPropertyBlock _mpb;
        private BattleSimulation _sim;
        private BattleLayout _layout;
        private VisualLibrary _lib;
        private int _selectedSlot = -1;
        private GridCoord _hover = GridCoord.Invalid;
        private bool _debug;

        public bool DebugGrid
        {
            get => _debug;
            set { _debug = value; }
        }

        public void Init(BattleSimulation sim, BattleLayout layout, VisualLibrary lib)
        {
            _sim = sim;
            _layout = layout;
            _lib = lib;
            _mpb = new MaterialPropertyBlock();
            _cells = new MeshRenderer[layout.Lanes, layout.Columns];
            _invalidFlash = new float[layout.Lanes, layout.Columns];
            for (int l = 0; l < layout.Lanes; l++)
            for (int c = 0; c < layout.Columns; c++)
            {
                var go = new GameObject($"Cell_{l}_{c}");
                go.transform.SetParent(transform, false);
                go.transform.position = layout.CellCenter(new GridCoord(l, c)) + Vector3.up * 0.075f;
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                go.transform.localScale = new Vector3(layout.CellWidth * 0.94f, layout.LaneDepth * 0.94f, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = lib.quadMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = lib.cellHighlight;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false;
                _cells[l, c] = mr;
            }
        }

        public void SetSelection(int slotIndex) => _selectedSlot = slotIndex;
        public void SetHover(GridCoord c) => _hover = c;

        public void FlashInvalid(GridCoord c)
        {
            if (_layout.InBounds(c)) _invalidFlash[c.lane, c.column] = 0.45f;
        }

        private void LateUpdate()
        {
            if (_cells == null) return;
            float dt = Time.unscaledDeltaTime;
            float pulse = 0.75f + Mathf.Sin(Time.unscaledTime * 6f) * 0.25f;
            for (int l = 0; l < _layout.Lanes; l++)
            for (int c = 0; c < _layout.Columns; c++)
            {
                var coord = new GridCoord(l, c);
                MeshRenderer mr = _cells[l, c];
                Color col = default;
                bool show = false;
                if (_invalidFlash[l, c] > 0f)
                {
                    _invalidFlash[l, c] -= dt;
                    col = _lib.invalidCell;
                    col.a *= Mathf.Clamp01(_invalidFlash[l, c] / 0.45f);
                    show = true;
                }
                else if (_selectedSlot >= 0)
                {
                    DeployResult r = _sim.CanDeploy(_selectedSlot, coord);
                    bool cellOk = r == DeployResult.Ok || r == DeployResult.NotEnoughVitality || r == DeployResult.OnCooldown;
                    if (cellOk)
                    {
                        col = _lib.validCell;
                        bool hover = coord == _hover;
                        col.a = hover ? 0.85f : col.a * (0.55f + 0.2f * pulse);
                        if (r != DeployResult.Ok) col = new Color(0.85f, 0.85f, 0.75f, hover ? 0.5f : 0.22f);
                        show = true;
                    }
                }
                else if (_debug)
                {
                    bool occupied = _sim.Grid.IsOccupied(coord);
                    col = occupied ? new Color(0.3f, 0.6f, 1f, 0.45f) : ((l + c) % 2 == 0 ? new Color(1f, 1f, 1f, 0.18f) : new Color(0f, 0f, 0f, 0.18f));
                    if (_sim.Grid.GetTerrain(coord) != CellTerrain.Grass) col = new Color(1f, 0.6f, 0.1f, 0.5f);
                    show = true;
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
