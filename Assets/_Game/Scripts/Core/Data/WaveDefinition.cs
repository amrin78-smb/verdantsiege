using System;
using System.Collections.Generic;
using UnityEngine;

namespace VerdantSiege.Data
{
    [Serializable]
    public sealed class SpawnGroup
    {
        public EnemyDefinition enemy;
        [Min(1)] public int count = 1;
        [Tooltip("-1 = director picks a lane (seeded, avoids long runs in one lane).")]
        public int lane = -1;
        [Tooltip("Seconds after the wave starts before the first spawn of this group.")]
        [Min(0f)] public float startDelay = 0f;
        [Tooltip("Seconds between spawns within the group.")]
        [Min(0f)] public float interval = 3f;
    }

    /// <summary>Authored wave. Waves run in order; procedural generation (Milestone 5) emits the same structure.</summary>
    [CreateAssetMenu(menuName = "Verdant Siege/Wave", fileName = "Wave_")]
    public sealed class WaveDefinition : ScriptableObject
    {
        public string label = "Wave";
        [Tooltip("Major waves get a warning banner and stronger audio cue.")]
        public bool major;
        [Tooltip("The next wave starts after this many seconds even if enemies remain alive.")]
        [Min(5f)] public float maxDuration = 45f;
        public List<SpawnGroup> groups = new List<SpawnGroup>();

        public int TotalEnemies()
        {
            int n = 0;
            foreach (SpawnGroup g in groups) if (g != null && g.enemy != null) n += g.count;
            return n;
        }

        public int ThreatBudget()
        {
            int n = 0;
            foreach (SpawnGroup g in groups) if (g != null && g.enemy != null) n += g.count * g.enemy.threatCost;
            return n;
        }
    }
}
