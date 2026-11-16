using System.Collections.Generic;
using UnityEngine;

namespace VerdantSiege.Data
{
    /// <summary>
    /// The campaign (Milestone 4): regions in world-map order, the starting collection, the research tree and the
    /// reward rules shared by every mission. Rules that use it live in VerdantSiege.Progression (pure, testable).
    /// </summary>
    [CreateAssetMenu(menuName = "Verdant Siege/Campaign", fileName = "Campaign")]
    public sealed class CampaignDefinition : ScriptableObject
    {
        public List<RegionDefinition> regions = new List<RegionDefinition>();
        [Tooltip("Defenders every new garden starts with.")]
        public List<DefenderDefinition> startingDefenders = new List<DefenderDefinition>();
        public List<ResearchDefinition> research = new List<ResearchDefinition>();
        public string[] researchBranchNames = { "Sunlight", "Cultivation", "Evolution" };
        [Tooltip("Fraction of a mission's (and boss bonus) Seeds paid on a replay win.")]
        [Range(0f, 1f)] public float replaySeedFraction = 0.35f;

        /// <summary>Every playable mission in campaign order (teaser regions skipped).</summary>
        public IEnumerable<EncounterDefinition> AllMissions()
        {
            foreach (RegionDefinition r in regions)
                if (r != null && !r.comingSoon) foreach (EncounterDefinition e in r.missions) if (e != null) yield return e;
        }

        public RegionDefinition RegionOf(EncounterDefinition e)
        {
            foreach (RegionDefinition r in regions) if (r != null && e != null && r.missions.Contains(e)) return r;
            return null;
        }

        public ResearchDefinition FindResearch(string id)
        {
            foreach (ResearchDefinition r in research) if (r != null && r.id == id) return r;
            return null;
        }
    }
}
