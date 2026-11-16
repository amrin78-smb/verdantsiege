using System;
using System.Collections.Generic;
using UnityEngine;
using VerdantSiege.Grid;

namespace VerdantSiege.Data
{
    [Serializable]
    public struct SpecialCell
    {
        public GridCoord coord;
        public CellTerrain terrain;
    }

    /// <summary>Battlefield dimensions and special cells. Visual layout lives in the scene/biome.</summary>
    [CreateAssetMenu(menuName = "Verdant Siege/Battlefield", fileName = "Battlefield_")]
    public sealed class BattlefieldDefinition : ScriptableObject
    {
        public string id = "battlefield";
        [Range(1, 7)] public int lanes = 5;
        [Range(3, 12)] public int columns = 9;
        public List<SpecialCell> specialCells = new List<SpecialCell>();
    }
}
