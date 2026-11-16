using System;

namespace VerdantSiege.Data
{
    /// <summary>How an enemy travels along its lane. Decides which defenders can hit it and what blocks it.</summary>
    public enum EnemyMovementKind
    {
        Ground = 0,      // walks, stops to eat the first defender it touches
        Flying = 1,      // glides over defenders; only anti-air attacks (canHitAir) reach it
        Underground = 2  // tunnels (untargetable except by traps and splash) and surfaces later
    }

    /// <summary>Special ability strategy. Implementations live in Battle/EnemyAbilities.cs; state lives on EnemyRuntime.</summary>
    public enum EnemyAbilityKind
    {
        None = 0,
        Vault = 1,        // crouches, then leaps over the first defender it meets (once)
        SporeCloud = 2,   // leaves a spore cloud on its cell when defeated
        Burrow = 3,       // tunnels for N columns, then surfaces with a telegraph
        Howl = 4,         // periodically hastes nearby enemies
        RangedSpit = 5,   // stops at range and lobs corrosive spit at a defender
        Mimic = 6,        // copies speed and armour of a nearby enemy once
        WardenAura = 7,   // nearby enemies take reduced damage
        HeraldCommand = 8 // while alive, every enemy moves faster
    }

    [Flags]
    public enum EnemyTags
    {
        None = 0,
        Mechanical = 1 << 0, // Ironroot plating/machinery: Storm Orchid stuns it
        Rotbound = 1 << 1    // fungal/organic flavour tag (no rule yet)
    }
}
