using UnityEngine;
using VerdantSiege.Audio;
using VerdantSiege.Data;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Living Evolution wiring: ready chime, the evolution moment (VFX on the DefenderView + sound), Capacitor
    /// releases, thorns, aura Vitality sharing, and the aura link lines (<see cref="EvolutionLinks"/>).
    /// </summary>
    public sealed partial class BattleView
    {
        private EvolutionLinks _links;

        /// <summary>A defender evolved (for HUD feedback such as a short label); world position of the defender.</summary>
        public event System.Action<DefenderRuntime, Vector3> EvolvedAt;

        private void InitEvolution()
        {
            var go = new GameObject("EvolutionLinks");
            go.transform.SetParent(transform, false);
            _links = go.AddComponent<EvolutionLinks>();
            _links.Init(_sim, _layout, _lib, _vfx);
            foreach (DeckSlot s in _sim.Deck)
            {
                if (s.Definition.evolutions == null) continue;
                foreach (EvolutionDefinition e in s.Definition.evolutions)
                    if (e != null && e.projectileOverride != null && e.projectileOverride.prefab != null) _pool.Prewarm(e.projectileOverride.prefab, 4);
            }
            _sim.DefenderEvolutionReady += OnEvolutionReady;
            _sim.DefenderEvolved += OnDefenderEvolved;
            _sim.CapacitorReleased += OnCapacitorReleased;
            _sim.ThornsHit += OnThornsHit;
            _sim.AuraVitalityShared += OnAuraShared;
        }

        private void UnsubscribeEvolution()
        {
            _sim.DefenderEvolutionReady -= OnEvolutionReady;
            _sim.DefenderEvolved -= OnDefenderEvolved;
            _sim.CapacitorReleased -= OnCapacitorReleased;
            _sim.ThornsHit -= OnThornsHit;
            _sim.AuraVitalityShared -= OnAuraShared;
        }

        private void OnEvolutionReady(DefenderRuntime d)
        {
            Play(Sfx.EvolveReady, 0.8f);
            if (_defenders.TryGetValue(d.Id, out DefenderView v))
                _vfx.Pulse(VfxKind.Glow, v.transform.position + Vector3.up * 0.9f, new Color(1f, 0.9f, 0.5f, 0.8f), 1.4f, 0.4f);
        }

        private void OnDefenderEvolved(DefenderRuntime d, EvolutionDefinition evo)
        {
            Play(Sfx.Evolve);
            if (!_defenders.TryGetValue(d.Id, out DefenderView v)) return;
            v.OnEvolved(evo);
            EvolvedAt?.Invoke(d, v.transform.position);
        }

        private void OnCapacitorReleased(DefenderRuntime d, int amount)
        {
            Play(Sfx.CapacitorRelease);
            if (_defenders.TryGetValue(d.Id, out DefenderView v)) v.OnCapacitorReleased();
        }

        private void OnThornsHit(DefenderRuntime d, EnemyRuntime e)
        {
            if (_defenders.TryGetValue(d.Id, out DefenderView v)) v.OnThorns();
        }

        private void OnAuraShared(DefenderRuntime d, int amount)
        {
            Play(Sfx.Produce, 0.5f);
            _links?.Shimmer(d.Id);
        }
    }
}
