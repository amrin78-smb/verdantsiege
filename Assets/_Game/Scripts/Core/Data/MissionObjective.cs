using System;

namespace VerdantSiege.Data
{
    /// <summary>What a mission's optional challenge (third star) asks for. Evaluated from the battle outcome.</summary>
    public enum MissionObjectiveKind
    {
        None = 0,
        EvolveAtLeast = 1,        // evolve at least N defenders
        LoseAtMostDefenders = 2,  // no more than N defenders destroyed
        DeckSizeAtMost = 3,       // win with N or fewer cards in the deck (restricted roster)
        PlantAtMost = 4,          // plant no more than N defenders
        StaggerBossAtLeast = 5,   // stagger the boss at least N times
        WinWithinSeconds = 6      // win within N seconds of battle time
    }

    /// <summary>One optional mission objective (immutable data on an <see cref="EncounterDefinition"/>).</summary>
    [Serializable]
    public sealed class MissionObjective
    {
        public MissionObjectiveKind kind = MissionObjectiveKind.None;
        public int value;
        public string text = "";

        public bool IsSet => kind != MissionObjectiveKind.None;
    }
}
