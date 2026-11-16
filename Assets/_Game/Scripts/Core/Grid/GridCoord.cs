using System;

namespace VerdantSiege.Grid
{
    /// <summary>
    /// Logical battlefield cell. Lane 0 is the lane nearest the camera, column 0 is next to the Heartroot
    /// (player side); enemies enter beyond the last column. Independent of world-space transforms.
    /// </summary>
    [Serializable]
    public struct GridCoord : IEquatable<GridCoord>
    {
        public int lane;
        public int column;

        public GridCoord(int lane, int column)
        {
            this.lane = lane;
            this.column = column;
        }

        public static readonly GridCoord Invalid = new GridCoord(-1, -1);

        /// <summary>Centre of the cell along the lane, in column units (the simulation's lane axis).</summary>
        public float CenterX => column + 0.5f;

        public bool Equals(GridCoord other) => lane == other.lane && column == other.column;
        public override bool Equals(object obj) => obj is GridCoord g && Equals(g);
        public override int GetHashCode() => (lane * 397) ^ column;
        public static bool operator ==(GridCoord a, GridCoord b) => a.Equals(b);
        public static bool operator !=(GridCoord a, GridCoord b) => !a.Equals(b);
        public override string ToString() => $"(lane {lane}, col {column})";
    }
}
