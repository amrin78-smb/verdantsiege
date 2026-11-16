using VerdantSiege.Data;
using VerdantSiege.Grid;

namespace VerdantSiege.Battle
{
    /// <summary>
    /// A simple, human-paced planting policy used by the balance tests and the Linux autoplay harness. It is not
    /// optimal play: it reacts to what is on the field, keeps an economy, gives each lane a damage dealer and then
    /// adds the specialist that answers the enemies it has seen. It never plants into a cell the boss has marked.
    /// Decide() has no side effects; the caller performs the deployment (directly or through real UI taps).
    /// </summary>
    public sealed class ScriptedPlayer
    {
        private const string Glowcap = "glowcap", Sunspore = "sunspore", Bulwark = "bulwark_bloom", Thorn = "thorncaster",
            Snapjaw = "snapjaw_vine", Frost = "frost_fern", Ember = "ember_lily", Whirl = "whirlseed", Mender = "mender_moss",
            Burrow = "burrowroot", Storm = "storm_orchid", Mirror = "mirrorleaf";

        private BattleSimulation _sim;
        private bool _seenFlyer, _seenFast, _seenBurrower, _seenSapper;

        public bool Decide(BattleSimulation sim, out int slot, out GridCoord cell)
        {
            _sim = sim;
            slot = -1;
            cell = GridCoord.Invalid;
            Observe();
            int lanes = sim.Lanes;

            // 1) Emergency: an enemy on the field in a lane nothing can shoot at.
            for (int l = 0; l < lanes; l++)
            {
                EnemyRuntime threat = FirstThreat(l);
                if (threat == null || LaneCanHit(l, threat.IsFlying)) continue;
                if (threat.IsFlying) { if (TryAny(l, out slot, out cell, new[] { Whirl, Thorn, Storm }, 1, 2, 3, 4, 5, 6, 0)) return true; }
                else if (threat.maxArmor > 0f || threat.IsElite) { if (TryAny(l, out slot, out cell, new[] { Thorn, Sunspore, Whirl, Storm, Frost, Ember }, 1, 2, 0)) return true; }
                else if (TryAny(l, out slot, out cell, new[] { Sunspore, Whirl, Thorn, Frost, Ember, Storm }, 1, 2, 0)) return true;
            }

            // 2) Economy.
            int glows = Count(Glowcap);
            if (glows < 2 || (glows < lanes && sim.Waves.CurrentWave < 3))
                for (int k = 0; k < lanes; k++) { int l = Order(k); if (CountLane(l, Glowcap) == 0 && Try(Glowcap, l, out slot, out cell, 0)) return true; }

            // 3) A damage dealer in every lane.
            for (int k = 0; k < lanes; k++)
            {
                int l = Order(k);
                if (Attackers(l) > 0) continue;
                if (TryAny(l, out slot, out cell, _seenFlyer ? new[] { Whirl, Thorn, Sunspore, Frost } : new[] { Sunspore, Whirl, Thorn, Frost }, 1, 2)) return true;
            }

            // 4) Counters for what has been seen.
            for (int k = 0; k < lanes; k++)
            {
                int l = Order(k);
                if (_seenFlyer && !LaneCanHit(l, true) && TryAny(l, out slot, out cell, new[] { Whirl, Thorn, Storm }, 2, 3, 1, 4, 5)) return true;
            }
            // Save Vitality for anti-air before buying extras while some lane cannot hit flyers.
            if (_seenFlyer && HasAntiAirCard())
                for (int l = 0; l < lanes; l++) if (Attackers(l) > 0 && !LaneCanHit(l, true)) return false;
            foreach (EnemyRuntime e in sim.Enemies)
            {
                if (!e.alive || e.x > sim.Columns) continue;
                if ((e.maxArmor > 0f || e.IsElite) && CountLane(e.lane, Thorn) == 0 && Try(Thorn, e.lane, out slot, out cell, 2, 3, 1, 4)) return true;
                if (e.maxArmor > 0f && CountLane(e.lane, Bulwark) == 0 && Try(Bulwark, e.lane, out slot, out cell, 5, 4, 6)) return true;
                if (e.Definition.ability == EnemyAbilityKind.SporeCloud && CountLane(e.lane, Ember) == 0 && Try(Ember, e.lane, out slot, out cell, 2, 1, 3, 4)) return true;
            }
            for (int k = 0; k < lanes; k++)
            {
                int l = Order(k);
                if ((_seenBurrower || _seenFast) && CountLane(l, Burrow) == 0 && Try(Burrow, l, out slot, out cell, 5, 6, 4)) return true;
            }
            if (Count(Storm) < 2)
                for (int k = 0; k < lanes; k++) { int l = Order(k); if (l % 2 == 1 && CountLane(l, Storm) == 0 && Try(Storm, l, out slot, out cell, 2, 3, 4)) return true; }
            if (_seenSapper && Count(Mirror) == 0 && Try(Mirror, lanes / 2, out slot, out cell, 5, 4)) return true;
            if (Count(Mender) < 2)
                for (int k = 0; k < lanes; k++) { int l = Order(k); if (l % 2 == 1 && CountLane(l, Mender) == 0 && Try(Mender, l, out slot, out cell, 3, 4)) return true; }

            // 5) More firepower, then control, ambushers and walls.
            for (int k = 0; k < lanes; k++)
            {
                int l = Order(k);
                if (Attackers(l) < 2 && TryAny(l, out slot, out cell, (l % 2 == 0) ? new[] { Whirl, Thorn, Ember, Sunspore } : new[] { Ember, Thorn, Whirl, Sunspore }, 2, 3, 4)) return true;
            }
            for (int k = 0; k < lanes; k++) { int l = Order(k); if (CountLane(l, Frost) == 0 && Try(Frost, l, out slot, out cell, 3, 2, 4)) return true; }
            for (int k = 0; k < lanes; k++) { int l = Order(k); if (CountLane(l, Snapjaw) == 0 && Try(Snapjaw, l, out slot, out cell, 6, 5)) return true; }
            for (int k = 0; k < lanes; k++)
            {
                int l = Order(k);
                if (Attackers(l) < 3 && TryAny(l, out slot, out cell, new[] { Thorn, Sunspore, Whirl, Ember }, 3, 4, 2)) return true;
            }
            for (int k = 0; k < lanes; k++) { int l = Order(k); if (CountLane(l, Bulwark) == 0 && Try(Bulwark, l, out slot, out cell, 6, 5)) return true; }
            return false;
        }

        // ------------------------------------------------------------------ observation

        private bool _scanned;

        private void Observe()
        {
            if (!_scanned)
            {
                // Like a player reading the briefing: flyers announced by the encounter are planned for from the start.
                _scanned = true;
                foreach (WaveDefinition w in _sim.Encounter.waves)
                    if (w != null) foreach (SpawnGroup g in w.groups)
                        if (g?.enemy != null && g.enemy.IsFlying) _seenFlyer = true;
            }
            foreach (EnemyRuntime e in _sim.Enemies)
            {
                if (!e.alive) continue;
                EnemyDefinition d = e.Definition;
                if (d.IsFlying) _seenFlyer = true;
                if (d.moveSpeed >= 0.4f || d.ability == EnemyAbilityKind.Vault) _seenFast = true;
                if (d.ability == EnemyAbilityKind.Burrow) _seenBurrower = true;
                if (d.ability == EnemyAbilityKind.RangedSpit) _seenSapper = true;
            }
        }

        private EnemyRuntime FirstThreat(int lane)
        {
            EnemyRuntime best = null;
            foreach (EnemyRuntime e in _sim.Enemies)
                if (e.alive && !e.underground && e.lane == lane && e.x < _sim.Columns && (best == null || e.x < best.x)) best = e;
            return best;
        }

        private int Order(int k) => (2 + k) % _sim.Lanes; // centre lane first

        private static bool IsAttacker(DefenderDefinition d) =>
            d.behaviour == DefenderBehaviourKind.LaneShooter || d.behaviour == DefenderBehaviourKind.ChainCaster;

        private int Attackers(int lane)
        {
            int n = 0;
            foreach (DefenderRuntime d in _sim.Defenders) if (d.alive && d.Coord.lane == lane && IsAttacker(d.Definition)) n++;
            return n;
        }

        private bool LaneCanHit(int lane, bool air)
        {
            foreach (DefenderRuntime d in _sim.Defenders)
                if (d.alive && d.Coord.lane == lane && IsAttacker(d.Definition) && (!air || d.Definition.HitsAir)) return true;
            return false;
        }

        private int Count(string id)
        {
            int n = 0;
            foreach (DefenderRuntime d in _sim.Defenders) if (d.alive && d.Definition.id == id) n++;
            return n;
        }

        private int CountLane(int lane, string id)
        {
            int n = 0;
            foreach (DefenderRuntime d in _sim.Defenders) if (d.alive && d.Coord.lane == lane && d.Definition.id == id) n++;
            return n;
        }

        // ------------------------------------------------------------------ placement

        private bool HasAntiAirCard() => SlotOf(Whirl) >= 0 || SlotOf(Thorn) >= 0 || SlotOf(Storm) >= 0;

        private int SlotOf(string id)
        {
            for (int i = 0; i < _sim.Deck.Count; i++) if (_sim.Deck[i].Definition.id == id) return i;
            return -1;
        }

        private bool Marked(GridCoord c)
        {
            BossRuntime b = _sim.Boss;
            if (b == null) return false;
            for (int i = 0; i < b.targetCells.Count; i++) if (b.targetCells[i] == c) return true;
            return false;
        }

        private bool Try(string id, int lane, out int slot, out GridCoord cell, params int[] cols)
        {
            slot = SlotOf(id);
            cell = GridCoord.Invalid;
            if (slot < 0) return false;
            foreach (int c in cols)
            {
                var coord = new GridCoord(lane, c);
                if (Marked(coord) || _sim.CloudAt(coord) != null) continue;
                if (_sim.CanDeploy(slot, coord) != DeployResult.Ok) continue;
                cell = coord;
                return true;
            }
            return false;
        }

        private bool TryAny(int lane, out int slot, out GridCoord cell, string[] ids, params int[] cols)
        {
            foreach (string id in ids) if (Try(id, lane, out slot, out cell, cols)) return true;
            slot = -1;
            cell = GridCoord.Invalid;
            return false;
        }
    }
}
