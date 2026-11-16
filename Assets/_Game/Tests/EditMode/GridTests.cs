using NUnit.Framework;
using VerdantSiege.Grid;

namespace VerdantSiege.Tests
{
    public sealed class GridTests
    {
        private sealed class Token { }

        [Test]
        public void Coord_CenterX_IsColumnMidpoint()
        {
            Assert.AreEqual(0.5f, new GridCoord(0, 0).CenterX);
            Assert.AreEqual(8.5f, new GridCoord(4, 8).CenterX);
        }

        [Test]
        public void Bounds_AreRespected()
        {
            var g = new BattleGrid<Token>(5, 9);
            Assert.IsTrue(g.InBounds(new GridCoord(0, 0)));
            Assert.IsTrue(g.InBounds(new GridCoord(4, 8)));
            Assert.IsFalse(g.InBounds(new GridCoord(5, 0)));
            Assert.IsFalse(g.InBounds(new GridCoord(0, 9)));
            Assert.IsFalse(g.InBounds(new GridCoord(-1, 3)));
            Assert.AreEqual(PlacementCheck.OutOfBounds, g.CanPlace(new GridCoord(2, 9)));
        }

        [Test]
        public void Occupancy_PlaceAndRemove()
        {
            var g = new BattleGrid<Token>(5, 9);
            var t = new Token();
            var c = new GridCoord(2, 3);
            Assert.AreEqual(PlacementCheck.Ok, g.CanPlace(c));
            g.Place(c, t);
            Assert.AreSame(t, g.Get(c));
            Assert.AreEqual(PlacementCheck.Occupied, g.CanPlace(c));
            Assert.IsFalse(g.Remove(c, new Token()), "stale occupant must not clear the cell");
            Assert.IsTrue(g.Remove(c, t));
            Assert.AreEqual(PlacementCheck.Ok, g.CanPlace(c));
        }

        [Test]
        public void Terrain_BlocksPlacement()
        {
            var g = new BattleGrid<Token>(5, 9);
            g.SetTerrain(new GridCoord(1, 1), CellTerrain.Blocked);
            g.SetTerrain(new GridCoord(1, 2), CellTerrain.Water);
            g.SetTerrain(new GridCoord(1, 3), CellTerrain.Bridge);
            Assert.AreEqual(PlacementCheck.BlockedTerrain, g.CanPlace(new GridCoord(1, 1)));
            Assert.AreEqual(PlacementCheck.UnsupportedTerrain, g.CanPlace(new GridCoord(1, 2)));
            Assert.AreEqual(PlacementCheck.Ok, g.CanPlace(new GridCoord(1, 3)));
        }
    }
}
