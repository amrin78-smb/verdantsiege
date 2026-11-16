using System.Collections.Generic;
using UnityEngine;

namespace VerdantSiege.Data
{
    /// <summary>
    /// One campaign region on the world map: its missions in order (the last is usually the boss finale), its
    /// palette for the map, and whether it is a locked teaser for a future update.
    /// </summary>
    [CreateAssetMenu(menuName = "Verdant Siege/Region", fileName = "Region_")]
    public sealed class RegionDefinition : ScriptableObject
    {
        public string id = "region";
        public string displayName = "Region";
        [TextArea(1, 2)] public string tagline = "";
        public Color accentColor = new Color(0.5f, 0.8f, 0.4f);
        public Color groundColor = new Color(0.3f, 0.5f, 0.25f);
        [Tooltip("Missions in campaign order; each unlocks when the previous one is won.")]
        public List<EncounterDefinition> missions = new List<EncounterDefinition>();
        [Tooltip("Visible on the map but not playable yet (future region teaser).")]
        public bool comingSoon;
        [Tooltip("Shown on a locked region.")]
        public string lockedHint = "";
    }
}
