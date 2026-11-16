namespace VerdantSiege.Grid
{
    /// <summary>Terrain of a battlefield cell. Later biomes add rules for water, bridges, platforms and hazards.</summary>
    public enum CellTerrain
    {
        Grass = 0,
        Blocked = 1,     // rocks, ruins: no placement
        Water = 2,       // needs aquatic placement rule (Sunken Kingdom)
        Bridge = 3,      // placeable over water
        Elevated = 4,    // raised platform
        Hazard = 5,      // temporarily unsafe (Emberwild)
        SunlightPool = 6 // Verdant Heartlands: periodic boost (Milestone 2)
    }
}
