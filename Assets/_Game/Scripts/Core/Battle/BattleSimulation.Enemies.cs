using System;
using System.Collections.Generic;
using VerdantSiege.Combat;
using VerdantSiege.Data;
using VerdantSiege.Grid;
using VerdantSiege.Utilities;

namespace VerdantSiege.Battle
{
    /// <summary>Enemy movement and contact, ability hooks, Warden/Herald auras and spore clouds.</summary>
    public sealed partial class BattleSimulation
    {
        /// <summary>An ability started its telegraph (crouch, sac inflate, shimmer, surfacing, canister glow).</summary>
        public event Action<EnemyRuntime> EnemyAbilityTelegraphed;
        /// <summary>An ability fired (leap, howl, spit, copy, surfaced).</summary>
        public event Action<EnemyRuntime> EnemyAbilityUsed;
        public event Action<EnemyRuntime, DefenderRuntime> EnemyLured;
        public event Action<CloudRuntime> CloudSpawned;
        /// <summary>(cloud, burned) burned=true when fire destroyed it.</summary>
        public event Action<CloudRuntime, bool> CloudRemoved;
        /// <summary>Herald's Command started (true) or ended (false).</summary>
        public event Action<bool> HeraldCommandChanged;

        public IReadOnlyList<CloudRuntime> Clouds => _clouds;
        /// <summary>Current Blight Herald speed bonus applied to every other enemy (0 = none).</summary>
        public float HeraldBonus { get; private set; }

        private readonly List<CloudRuntime> _clouds = new List<CloudRuntime>();
        private readonly SimplePool<CloudRuntime> _cloudPool = new SimplePool<CloudRuntime>(() => new CloudRuntime(), 6);
        private const float CloudTick = 0.5f;
        private float _cloudTickTimer = CloudTick;

        // ================================================================== auras (computed once per step)

        private void RefreshAuras()
        {
            float herald = 0f;
            for (int i = 0; i < _enemies.Count; i++) _enemies[i].wardReduction = 0f;
            for (int w = 0; w < _enemies.Count; w++)
            {
                EnemyRuntime src = _enemies[w];
                if (!src.alive || src.underground) continue;
                EnemyDefinition def = src.Definition;
                if (def.ability == EnemyAbilityKind.HeraldCommand)
                {
                    if (def.abilityMagnitude > herald) herald = def.abilityMagnitude;
                    continue;
                }
                if (def.ability != EnemyAbilityKind.WardenAura || !OnField(src)) continue;
                for (int i = 0; i < _enemies.Count; i++)
                {
                    EnemyRuntime e = _enemies[i];
                    if (e == src || !e.alive || Math.Abs(e.lane - src.lane) > 1 || Math.Abs(e.x - src.x) > def.abilityRange) continue;
                    if (def.abilityMagnitude > e.wardReduction) e.wardReduction = def.abilityMagnitude;
                }
            }
            bool was = HeraldBonus > 0f;
            HeraldBonus = herald;
            if (was != herald > 0f) HeraldCommandChanged?.Invoke(herald > 0f);
        }

        /// <summary>Actual movement speed this step: base speed with statuses, plus the Herald's Command (not on the Herald itself).</summary>
        public float SpeedOf(EnemyRuntime e)
        {
            float s = e.CurrentSpeed;
            if (HeraldBonus > 0f && e.Definition.ability != EnemyAbilityKind.HeraldCommand) s *= 1f + HeraldBonus;
            return s;
        }

        // ================================================================== movement and contact

        private void TickEnemies(float dt)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyRuntime e = _enemies[i];
                if (!e.alive) continue;
                if (e.hitFlash > 0f) e.hitFlash -= dt;
                if (e.statuses.Count > 0)
                {
                    float dot = e.statuses.Tick(dt, out DamageType dotType);
                    if (dot > 0f) DamageEnemy(e, dot, dotType);
                    if (!e.alive) continue;
                }
                e.prevX = e.x;
                if (e.laneSwitchTimer > 0f)
                {
                    e.laneSwitchTimer -= dt;
                    if (e.laneSwitchTimer <= 0f) { e.laneSwitchTimer = 0f; e.lane = e.targetLane; }
                }

                if (EnemyAbilities.For(e.Definition.ability).Tick(e, this, dt))
                {
                    if (e.alive && e.x <= BreachX) Breach(e);
                    continue;
                }

                bool stunned = e.statuses.Has(StatusKind.Stun);
                DefenderRuntime blocker = FindBlocker(e);
                if (blocker != null)
                {
                    if (e.activity != EnemyActivity.Attacking)
                    {
                        SetActivity(e, EnemyActivity.Attacking);
                        e.biteTimer = e.Definition.biteInterval * 0.5f; // readable wind-up before the first bite
                    }
                    if (!stunned) e.biteTimer -= dt * (1f - e.statuses.SlowFraction());
                    if (e.biteTimer <= 0f)
                    {
                        e.biteTimer += e.Definition.biteInterval;
                        EnemyBit?.Invoke(e, blocker);
                        float bite = e.Definition.biteDamage;
                        DamageDefender(blocker, bite);
                        OnDefenderBitten(blocker, e, bite); // Evolution: thorns / snares
                    }
                    continue;
                }
                if (e.activity != EnemyActivity.Walking) SetActivity(e, EnemyActivity.Walking);
                e.x -= SpeedOf(e) * dt;
                if (e.x <= BreachX) Breach(e);
            }
        }

        internal void SetActivity(EnemyRuntime e, EnemyActivity a)
        {
            if (e.activity == a) return;
            e.activity = a;
            EnemyActivityChanged?.Invoke(e, a);
        }

        internal void RaiseAbilityTelegraph(EnemyRuntime e) => EnemyAbilityTelegraphed?.Invoke(e);
        internal void RaiseAbilityUsed(EnemyRuntime e) => EnemyAbilityUsed?.Invoke(e);

        /// <summary>
        /// The defender this enemy is in contact with, if any. Flyers and burrowed enemies are never blocked, and
        /// an armed (hidden) trap is walked onto rather than eaten.
        /// </summary>
        public DefenderRuntime FindBlocker(EnemyRuntime e)
        {
            if (e.IsFlying || e.underground) return null;
            // Defender centre c+0.5 must lie in [x - ahead, x + behind]  =>  c in [x-ahead-0.5, x+behind-0.5]
            int c = (int)Math.Ceiling(e.x - EnemyContactAhead - 0.5f);
            if (c > e.x + EnemyContactBehind - 0.5f) return null;
            DefenderRuntime d = Grid.Get(e.lane, c);
            return d != null && d.alive && !d.IsArmedTrap ? d : null;
        }

        /// <summary>Ranged target for a Sapper: a defender ahead in its lane within range, preferring decoys, else the nearest.</summary>
        public DefenderRuntime FindRangedTarget(EnemyRuntime e, float range)
        {
            DefenderRuntime best = null;
            bool bestTaunt = false;
            for (int i = 0; i < _defenders.Count; i++)
            {
                DefenderRuntime d = _defenders[i];
                if (!d.alive || d.Coord.lane != e.lane || d.IsArmedTrap) continue;
                float cx = d.Coord.CenterX;
                if (cx > e.x || e.x - cx > range) continue;
                bool taunt = d.Definition.taunts;
                if (best == null || (taunt && !bestTaunt) || (taunt == bestTaunt && cx > best.Coord.CenterX))
                {
                    best = d;
                    bestTaunt = taunt;
                }
            }
            return best;
        }

        /// <summary>Haste (Howler): applies a status to every enemy near the source (itself included).</summary>
        internal void ApplyStatusAround(EnemyRuntime src, float radius, StatusEffectDefinition status)
        {
            if (status == null) return;
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyRuntime e = _enemies[i];
                if (!e.alive || Math.Abs(e.lane - src.lane) > 1 || Math.Abs(e.x - src.x) > radius) continue;
                ApplyStatus(e, status);
            }
        }

        /// <summary>Nearest other enemy within radius that is not a mimic (Mimic copy source).</summary>
        internal EnemyRuntime FindMimicSource(EnemyRuntime m, float radius)
        {
            EnemyRuntime best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyRuntime e = _enemies[i];
                if (e == m || !e.alive || e.underground || e.Definition.ability == EnemyAbilityKind.Mimic || !OnField(e)) continue;
                int dl = Math.Abs(e.lane - m.lane);
                if (dl > 1) continue;
                float dx = e.x - m.x;
                float d = (float)Math.Sqrt(dx * dx + dl * dl);
                if (d > radius || d >= bestD) continue;
                bestD = d;
                best = e;
            }
            return best;
        }

        internal bool IsOnField(EnemyRuntime e) => OnField(e);

        // ================================================================== spore clouds

        /// <summary>Leaves a cloud on the cell under the enemy (Spore Carrier death).</summary>
        internal void SpawnCloud(EnemyRuntime e)
        {
            EnemyDefinition def = e.Definition;
            int col = (int)Math.Floor(e.x);
            if (col < 0 || col >= Columns || def.abilityDuration <= 0f) return;
            var coord = new GridCoord(e.lane, col);
            for (int i = 0; i < _clouds.Count; i++)
                if (_clouds[i].alive && _clouds[i].coord == coord) { _clouds[i].remaining = Math.Max(_clouds[i].remaining, def.abilityDuration); return; }
            CloudRuntime c = _cloudPool.Rent();
            c.Id = _nextId++;
            c.coord = coord;
            c.duration = c.remaining = def.abilityDuration;
            c.damagePerSecond = def.abilityDamage;
            c.status = def.abilityStatus;
            c.alive = true;
            _clouds.Add(c);
            CloudSpawned?.Invoke(c);
        }

        private void TickClouds(float dt)
        {
            if (_clouds.Count == 0) return;
            _cloudTickTimer -= dt;
            bool tick = _cloudTickTimer <= 0f;
            if (tick) _cloudTickTimer += CloudTick;
            for (int i = 0; i < _clouds.Count; i++)
            {
                CloudRuntime c = _clouds[i];
                if (!c.alive) continue;
                c.remaining -= dt;
                if (c.remaining <= 0f)
                {
                    c.alive = false;
                    CloudRemoved?.Invoke(c, false);
                    continue;
                }
                if (!tick) continue;
                DefenderRuntime d = Grid.Get(c.coord);
                if (d == null || !d.alive) continue;
                if (c.status != null) ApplyDefenderStatus(d, c.status);
                DamageDefender(d, c.damagePerSecond * CloudTick);
            }
        }

        /// <summary>Fire burns away every cloud in the lane overlapping [lo, hi] (column units).</summary>
        internal void BurnClouds(int lane, float lo, float hi)
        {
            for (int i = 0; i < _clouds.Count; i++)
            {
                CloudRuntime c = _clouds[i];
                if (!c.alive || c.coord.lane != lane) continue;
                if (c.coord.column + 1f < lo || c.coord.column > hi) continue;
                c.alive = false;
                CloudRemoved?.Invoke(c, true);
            }
        }

        public CloudRuntime CloudAt(GridCoord coord)
        {
            for (int i = 0; i < _clouds.Count; i++) if (_clouds[i].alive && _clouds[i].coord == coord) return _clouds[i];
            return null;
        }
    }
}
