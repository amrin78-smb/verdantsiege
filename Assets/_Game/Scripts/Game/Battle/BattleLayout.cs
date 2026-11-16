using UnityEngine;
using VerdantSiege.Grid;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Maps logical lane/column coordinates to world space and back. World X runs along the lane (column 0 at
    /// x=0, next to the Heartroot), world Z across lanes (lane 0 nearest the camera), Y up. The simulation never
    /// sees world positions.
    /// </summary>
    public readonly struct BattleLayout
    {
        public readonly int Lanes;
        public readonly int Columns;
        public readonly float CellWidth;
        public readonly float LaneDepth;
        public readonly Vector3 Origin;

        public BattleLayout(int lanes, int columns, float cellWidth = 1f, float laneDepth = 1f, Vector3 origin = default)
        {
            Lanes = lanes;
            Columns = columns;
            CellWidth = cellWidth;
            LaneDepth = laneDepth;
            Origin = origin;
        }

        public float LaneZ(float lane) => Origin.z + (lane + 0.5f) * LaneDepth;
        public float ColumnX(float x) => Origin.x + x * CellWidth;

        /// <summary>World position for a lane position (x in column units, lane may be fractional).</summary>
        public Vector3 ToWorld(float x, float lane) => new Vector3(ColumnX(x), Origin.y, LaneZ(lane));
        public Vector3 CellCenter(GridCoord c) => ToWorld(c.CenterX, c.lane);

        public GridCoord WorldToCoord(Vector3 world)
        {
            int col = Mathf.FloorToInt((world.x - Origin.x) / CellWidth);
            int lane = Mathf.FloorToInt((world.z - Origin.z) / LaneDepth);
            return new GridCoord(lane, col);
        }

        public bool InBounds(GridCoord c) => c.lane >= 0 && c.lane < Lanes && c.column >= 0 && c.column < Columns;

        public Bounds FieldBounds => new Bounds(
            Origin + new Vector3(Columns * CellWidth * 0.5f, 0f, Lanes * LaneDepth * 0.5f),
            new Vector3(Columns * CellWidth, 0.1f, Lanes * LaneDepth));
    }
}
