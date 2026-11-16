using System.Collections.Generic;
using VerdantSiege.Data;
using VerdantSiege.Utilities;

namespace VerdantSiege.Battle
{
    /// <summary>
    /// Runs authored waves in order. A wave ends when everything has spawned and either all of its enemies are
    /// gone or its maxDuration elapsed; the next wave follows after the encounter's inter-wave gap.
    /// Lane choice for unpinned groups is seeded and avoids more than two consecutive spawns in one lane.
    /// </summary>
    public sealed class WaveDirector
    {
        private sealed class ActiveGroup
        {
            public SpawnGroup group;
            public int remaining;
            public float timer;
        }

        private readonly EncounterDefinition _encounter;
        private readonly int _lanes;
        private readonly List<ActiveGroup> _groups = new List<ActiveGroup>();
        private SeededRandom _rng;
        private int _lastLane = -1;
        private int _sameLaneRun;

        public int CurrentWave { get; private set; } = -1;
        public int TotalWaves => _encounter.waves.Count;
        public float PrepRemaining { get; private set; }
        public float WaveElapsed { get; private set; }
        public float GapRemaining { get; private set; } = -1f;
        public bool AllWavesStarted => CurrentWave >= TotalWaves - 1 && CurrentWave >= 0;
        public bool CurrentWaveFullySpawned => _groups.Count == 0;
        public bool Finished => AllWavesStarted && CurrentWaveFullySpawned;
        public WaveDefinition CurrentWaveDefinition => CurrentWave >= 0 && CurrentWave < TotalWaves ? _encounter.waves[CurrentWave] : null;

        /// <summary>Overall progress 0..1 for the HUD (waves started, weighted by spawn progress).</summary>
        public float Progress
        {
            get
            {
                if (TotalWaves == 0) return 1f;
                if (CurrentWave < 0) return 0f;
                WaveDefinition w = CurrentWaveDefinition;
                int total = w != null ? w.TotalEnemies() : 0;
                int left = 0;
                foreach (ActiveGroup g in _groups) left += g.remaining;
                float inWave = total > 0 ? 1f - left / (float)total : 1f;
                return (CurrentWave + inWave) / TotalWaves;
            }
        }

        public WaveDirector(EncounterDefinition encounter, int lanes, int seed, float extraPrepTime = 0f)
        {
            _encounter = encounter;
            _lanes = lanes;
            _rng = new SeededRandom(seed ^ 0x5A17);
            PrepRemaining = encounter.prepTime + extraPrepTime;
        }

        /// <param name="aliveFromCurrentWave">enemies from the current wave still alive</param>
        /// <param name="spawn">callback (enemy, lane, waveIndex)</param>
        /// <param name="waveStarted">callback (waveIndex)</param>
        public void Tick(float dt, int aliveFromCurrentWave, System.Action<EnemyDefinition, int, int> spawn,
                         System.Action<int> waveStarted)
        {
            if (TotalWaves == 0) return;
            if (CurrentWave < 0)
            {
                PrepRemaining -= dt;
                if (PrepRemaining <= 0f) StartWave(0, waveStarted);
                else return;
            }

            WaveElapsed += dt;
            for (int i = _groups.Count - 1; i >= 0; i--)
            {
                ActiveGroup g = _groups[i];
                g.timer -= dt;
                while (g.remaining > 0 && g.timer <= 0f)
                {
                    spawn(g.group.enemy, PickLane(g.group.lane), CurrentWave);
                    g.remaining--;
                    g.timer += g.group.interval > 0f ? g.group.interval : 0f; // interval 0 = all at once
                }
                if (g.remaining <= 0) _groups.RemoveAt(i);
            }

            if (AllWavesStarted || !CurrentWaveFullySpawned) return;

            WaveDefinition w = CurrentWaveDefinition;
            bool cleared = aliveFromCurrentWave <= 0;
            bool timedOut = w != null && WaveElapsed >= w.maxDuration;
            if (GapRemaining < 0f && (cleared || timedOut)) GapRemaining = _encounter.interWaveGap;
            if (GapRemaining >= 0f)
            {
                GapRemaining -= dt;
                if (GapRemaining <= 0f) StartWave(CurrentWave + 1, waveStarted);
            }
        }

        /// <summary>Debug/testing: begin the next wave now.</summary>
        public void ForceNextWave(System.Action<int> waveStarted)
        {
            if (CurrentWave >= TotalWaves - 1) return;
            PrepRemaining = 0f;
            StartWave(CurrentWave + 1, waveStarted);
        }

        private void StartWave(int index, System.Action<int> waveStarted)
        {
            CurrentWave = index;
            WaveElapsed = 0f;
            GapRemaining = -1f;
            PrepRemaining = 0f;
            WaveDefinition w = _encounter.waves[index];
            if (w != null)
                foreach (SpawnGroup g in w.groups)
                    if (g != null && g.enemy != null && g.count > 0)
                        _groups.Add(new ActiveGroup { group = g, remaining = g.count, timer = g.startDelay });
            waveStarted?.Invoke(index);
        }

        private int PickLane(int requested)
        {
            if (requested >= 0 && requested < _lanes) return Track(requested);
            int lane = _rng.Range(0, _lanes);
            if (_lanes > 1 && lane == _lastLane && _sameLaneRun >= 2)
                lane = (lane + 1 + _rng.Range(0, _lanes - 1)) % _lanes;
            return Track(lane);
        }

        private int Track(int lane)
        {
            _sameLaneRun = lane == _lastLane ? _sameLaneRun + 1 : 1;
            _lastLane = lane;
            return lane;
        }
    }
}
