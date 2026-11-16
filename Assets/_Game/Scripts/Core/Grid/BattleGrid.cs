using System;

namespace VerdantSiege.Grid
{
    /// <summary>Why a cell cannot take a defender.</summary>
    public enum PlacementCheck
    {
        Ok = 0,
        OutOfBounds,
        Occupied,
        BlockedTerrain,
        UnsupportedTerrain
    }

    /// <summary>
    /// Lanes x columns occupancy and terrain. Stores occupant handles (generic) so the grid has no
    /// dependency on combat types. All checks are O(1).
    /// </summary>
    public sealed class BattleGrid<TOccupant> where TOccupant : class
    {
        private readonly TOccupant[] _occupants;
        private readonly CellTerrain[] _terrain;

        public int Lanes { get; }
        public int Columns { get; }

        public BattleGrid(int lanes, int columns)
        {
            if (lanes < 1 || columns < 1) throw new ArgumentException("Grid needs at least one lane and one column");
            Lanes = lanes;
            Columns = columns;
            _occupants = new TOccupant[lanes * columns];
            _terrain = new CellTerrain[lanes * columns];
        }

        private int Index(GridCoord c) => c.lane * Columns + c.column;

        public bool InBounds(GridCoord c) => c.lane >= 0 && c.lane < Lanes && c.column >= 0 && c.column < Columns;

        public CellTerrain GetTerrain(GridCoord c) => InBounds(c) ? _terrain[Index(c)] : CellTerrain.Blocked;

        public void SetTerrain(GridCoord c, CellTerrain t)
        {
            if (!InBounds(c)) throw new ArgumentOutOfRangeException(nameof(c), c.ToString());
            _terrain[Index(c)] = t;
        }

        public TOccupant Get(GridCoord c) => InBounds(c) ? _occupants[Index(c)] : null;
        public TOccupant Get(int lane, int column) => Get(new GridCoord(lane, column));
        public bool IsOccupied(GridCoord c) => Get(c) != null;

        /// <summary>Validation for a standard ground defender.</summary>
        public PlacementCheck CanPlace(GridCoord c)
        {
            if (!InBounds(c)) return PlacementCheck.OutOfBounds;
            CellTerrain t = _terrain[Index(c)];
            if (t == CellTerrain.Blocked) return PlacementCheck.BlockedTerrain;
            if (t == CellTerrain.Water) return PlacementCheck.UnsupportedTerrain;
            if (_occupants[Index(c)] != null) return PlacementCheck.Occupied;
            return PlacementCheck.Ok;
        }

        public void Place(GridCoord c, TOccupant occupant)
        {
            PlacementCheck check = CanPlace(c);
            if (check != PlacementCheck.Ok) throw new InvalidOperationException($"Cannot place at {c}: {check}");
            _occupants[Index(c)] = occupant ?? throw new ArgumentNullException(nameof(occupant));
        }

        /// <summary>Clears the cell only if it holds this occupant (guards against stale removals).</summary>
        public bool Remove(GridCoord c, TOccupant occupant)
        {
            if (!InBounds(c) || _occupants[Index(c)] != occupant) return false;
            _occupants[Index(c)] = null;
            return true;
        }
    }
}
