using System;
using System.Collections.Generic;
using VerdantSiege.Combat;
using VerdantSiege.Data;
using VerdantSiege.Grid;
using VerdantSiege.Utilities;

namespace VerdantSiege.Battle
{
    /// <summary>Player options that change rules (not difficulty).</summary>
    public struct BattleOptions
    {
        public bool autoCollect;
        public float autoCollectDelay;
        public float undoGraceSeconds;

        public static BattleOptions Default => new BattleOptions { autoCollect = false, autoCollectDelay = 1.2f, undoGraceSeconds = 3f };
    }

    public struct BattleStats
    {
        public float elapsed;
        public int enemiesDefeated;
        public int defendersPlanted;
        public int defendersLost;
        public int vitalityCollected;
        public int breaches;
    }

    /// <summary>
    /// Authoritative battle state owner. Pure C#: no scene objects, no frame timing, no VFX dependency.
    /// The presentation layer calls <see cref="Advance"/> with frame time; the simulation runs fixed steps and
    /// raises events that views and UI subscribe to. Positions along a lane are in column units: 0 is the
    /// Heartroot edge, <see cref="Columns"/> the enemy entry edge.
    /// Split across partial files: this one (core, deployment, economy, flow), .Combat (defender attacks and
    /// projectiles), .Enemies (movement, abilities, auras, clouds) and .Boss (boss phases and battlefield changes).
    /// </summary>
    public sealed partial class BattleSimulation
    {
        public const float FixedStep = 1f / 30f;
        public const int MaxStepsPerAdvance = 8;
        public const float EnemyContactAhead = 0.4f;   // enemy front within this of a defender centre -> bites
        public const float EnemyContactBehind = 0.3f;  // enemy already this far past a centre ignores it
        public const float BreachX = -0.15f;
        public const float SpawnOffset = 0.45f;        // spawn beyond the last column
        public const float FieldEdgeSlack = 0.25f;     // enemies beyond Columns + this are not yet on the field

        // ---------- events (synchronous, raised inside Step) ----------
        public event Action<DefenderRuntime> DefenderPlaced;
        /// <summary>(defender, killed) killed=false for undo/removal.</summary>
        public event Action<DefenderRuntime, bool> DefenderRemoved;
        public event Action<DefenderRuntime, float> DefenderDamaged;
        /// <summary>Raised when a defender acts: shoots, produces, snaps, erupts, heals, casts (animation hook).</summary>
        public event Action<DefenderRuntime> DefenderActed;
        public event Action<EnemyRuntime> EnemySpawned;
        public event Action<EnemyRuntime, DamageResult, DamageType> EnemyDamaged;
        public event Action<EnemyRuntime> EnemyArmorBroken;
        public event Action<EnemyRuntime> EnemyKilled;
        public event Action<EnemyRuntime> EnemyBreached;
        public event Action<EnemyRuntime, DefenderRuntime> EnemyBit;
        public event Action<EnemyRuntime, EnemyActivity> EnemyActivityChanged;
        public event Action<EnemyRuntime, StatusEffectDefinition> StatusApplied;
        public event Action<ProjectileRuntime> ProjectileFired;
        public event Action<ProjectileRuntime, EnemyRuntime> ProjectileHit;
        public event Action<ProjectileRuntime> ProjectileDespawned;
        public event Action<PickupRuntime> PickupSpawned;
        /// <summary>(pickup, collected) collected=false when it expired.</summary>
        public event Action<PickupRuntime, bool> PickupRemoved;
        /// <summary>(waveIndex, major)</summary>
        public event Action<int, bool> WaveStarted;
        /// <summary>(remaining, damage)</summary>
        public event Action<int, int> HeartrootDamaged;
        public event Action<BattleState> StateChanged;
        /// <summary>Sunlight pool cells changed (appeared or faded).</summary>
        public event Action SunlightPoolsChanged;
        /// <summary>Invalid action attempted (for feedback).</summary>
        public event Action<DeployResult, GridCoord> DeployRejected;

        // ---------- state ----------
        public EncounterDefinition Encounter { get; }
        public BattleGrid<DefenderRuntime> Grid { get; }
        public VitalityBank Vitality { get; }
        public WaveDirector Waves { get; }
        public BattleOptions Options { get; set; }
        /// <summary>While true the first wave waits (guided tutorial). Sky drops and planting continue.</summary>
        public bool HoldFirstWave { get; set; }
        public BattleState State { get; private set; } = BattleState.Running;
        public int HeartrootHealth { get; private set; }
        public int HeartrootMaxHealth { get; }
        public int Seed { get; }
        public int Lanes => Grid.Lanes;
        public int Columns => Grid.Columns;
        public float SpawnX => Columns + SpawnOffset;
        public BattleStats Stats => _stats;
        public IReadOnlyList<DeckSlot> Deck => _deck;
        public IReadOnlyList<EnemyRuntime> Enemies => _enemies;
        public IReadOnlyList<DefenderRuntime> Defenders => _defenders;
        public IReadOnlyList<ProjectileRuntime> Projectiles => _projectiles;
        public IReadOnlyList<PickupRuntime> Pickups => _pickups;
        public IReadOnlyList<GridCoord> SunlightPools => _sunPools;
        public float SunlightPoolRemaining => _sunPoolActive;
        /// <summary>Fraction of the current fixed step already accumulated (for visual interpolation).</summary>
        public float Interpolation => _accumulator / FixedStep;

        private readonly List<DeckSlot> _deck = new List<DeckSlot>();
        private readonly List<DefenderRuntime> _defenders = new List<DefenderRuntime>();
        private readonly List<EnemyRuntime> _enemies = new List<EnemyRuntime>();
        private readonly List<ProjectileRuntime> _projectiles = new List<ProjectileRuntime>();
        private readonly List<PickupRuntime> _pickups = new List<PickupRuntime>();
        private readonly List<GridCoord> _sunPools = new List<GridCoord>();
        private float _sunPoolTimer;
        private float _sunPoolActive;
        private readonly SimplePool<DefenderRuntime> _defenderPool = new SimplePool<DefenderRuntime>(() => new DefenderRuntime(), 16);
        private readonly SimplePool<EnemyRuntime> _enemyPool = new SimplePool<EnemyRuntime>(() => new EnemyRuntime(), 32);
        private readonly SimplePool<ProjectileRuntime> _projectilePool = new SimplePool<ProjectileRuntime>(() => new ProjectileRuntime(), 48);
        private readonly SimplePool<PickupRuntime> _pickupPool = new SimplePool<PickupRuntime>(() => new PickupRuntime(), 12);
        private readonly Action<EnemyDefinition, int, int> _spawnCallback;
        private readonly Action<int> _waveStartedCallback;

        private SeededRandom _rng;
        private BattleStats _stats;
        private float _accumulator;
        private float _skyTimer;
        private int _nextId = 1;

        public BattleSimulation(EncounterDefinition encounter, int seed, BattleOptions options)
        {
            Encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
            if (encounter.battlefield == null) throw new ArgumentException("Encounter has no battlefield", nameof(encounter));
            Seed = seed;
            Options = options;
            _rng = new SeededRandom(seed);
            Grid = new BattleGrid<DefenderRuntime>(encounter.battlefield.lanes, encounter.battlefield.columns);
            foreach (SpecialCell c in encounter.battlefield.specialCells)
                if (Grid.InBounds(c.coord)) Grid.SetTerrain(c.coord, c.terrain);
            Vitality = new VitalityBank(encounter.startingVitality, encounter.vitalityCap);
            Waves = new WaveDirector(encounter, Grid.Lanes, seed);
            HeartrootMaxHealth = HeartrootHealth = Math.Max(1, encounter.heartrootHealth);
            foreach (DefenderDefinition d in encounter.roster) if (d != null) _deck.Add(new DeckSlot(d));
            _skyTimer = encounter.skyDropFirstDelay;
            _sunPoolTimer = encounter.sunlightPoolInterval > 0f ? encounter.prepTime + encounter.sunlightPoolInterval * 0.5f : 0f;
            _spawnCallback = (def, lane, wave) => SpawnEnemy(def, lane, wave);
            _waveStartedCallback = OnWaveStarted;
            if (encounter.boss != null) Boss = new BossRuntime(encounter.boss);
        }

        // ================================================================== time

        /// <summary>Feeds frame time; runs as many fixed steps as are due (capped to avoid a spiral of death).</summary>
        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0f || State != BattleState.Running) return;
            _accumulator += deltaTime;
            int steps = 0;
            while (_accumulator >= FixedStep && steps < MaxStepsPerAdvance)
            {
                Step(FixedStep);
                _accumulator -= FixedStep;
                steps++;
            }
            if (steps == MaxStepsPerAdvance && _accumulator > FixedStep) _accumulator = 0f;
        }

        /// <summary>One deterministic simulation step.</summary>
        public void Step(float dt)
        {
            if (State != BattleState.Running) return;
            _stats.elapsed += dt;
            foreach (DeckSlot s in _deck) if (s.cooldownRemaining > 0f) s.cooldownRemaining = Math.Max(0f, s.cooldownRemaining - dt);

            if (!(HoldFirstWave && Waves.CurrentWave < 0))
                Waves.Tick(dt, AliveInWave(Waves.CurrentWave), _spawnCallback, _waveStartedCallback);
            TickSkyDrops(dt);
            TickSunlightPools(dt);
            RefreshAuras();
            TickDefenders(dt);
            TickProjectiles(dt);
            TickEnemies(dt);
            TickClouds(dt);
            TickBoss(dt);
            TickPickups(dt);
            Compact();
            CheckEnd();
        }

        // ================================================================== deployment

        /// <summary>Validates without changing anything.</summary>
        public DeployResult CanDeploy(int slotIndex, GridCoord coord)
        {
            if (State != BattleState.Running) return DeployResult.BattleOver;
            if (slotIndex < 0 || slotIndex >= _deck.Count) return DeployResult.InvalidCard;
            DeckSlot slot = _deck[slotIndex];
            if (!slot.Ready) return DeployResult.OnCooldown;
            switch (Grid.CanPlace(coord))
            {
                case PlacementCheck.OutOfBounds: return DeployResult.OutOfBounds;
                case PlacementCheck.Occupied: return DeployResult.Occupied;
                case PlacementCheck.BlockedTerrain: return DeployResult.BlockedTerrain;
                case PlacementCheck.UnsupportedTerrain: return DeployResult.UnsupportedTerrain;
            }
            if (!Vitality.CanAfford(slot.Definition.cost)) return DeployResult.NotEnoughVitality;
            return DeployResult.Ok;
        }

        /// <summary>
        /// Atomic deploy: validates, spends, places and starts the card cooldown in one call. Rapid duplicate taps
        /// fail on cooldown/occupancy because state changes immediately.
        /// </summary>
        public DeployResult TryDeploy(int slotIndex, GridCoord coord)
        {
            DeployResult r = CanDeploy(slotIndex, coord);
            if (r != DeployResult.Ok)
            {
                DeployRejected?.Invoke(r, coord);
                return r;
            }
            DeckSlot slot = _deck[slotIndex];
            if (!Vitality.TrySpend(slot.Definition.cost)) return DeployResult.NotEnoughVitality;
            DefenderRuntime d = _defenderPool.Rent();
            d.Id = _nextId++;
            d.Definition = slot.Definition;
            d.Coord = coord;
            d.health = slot.Definition.maxHealth;
            d.slotIndex = slotIndex;
            d.alive = true;
            Grid.Place(coord, d);
            _defenders.Add(d);
            DefenderBehaviours.For(d.Definition.behaviour).OnPlaced(d);
            slot.cooldownRemaining = slot.Definition.cooldown;
            _stats.defendersPlanted++;
            DefenderPlaced?.Invoke(d);
            return DeployResult.Ok;
        }

        /// <summary>True while the defender may still be undone for a full refund.</summary>
        public bool CanUndo(DefenderRuntime d) =>
            d != null && d.alive && State == BattleState.Running && d.age <= Options.undoGraceSeconds && d.health >= d.MaxHealth;

        /// <summary>Undo within the grace period: full refund and the card is ready again.</summary>
        public bool TryUndo(DefenderRuntime d)
        {
            if (!CanUndo(d)) return false;
            Vitality.Add(d.Definition.cost);
            if (d.slotIndex >= 0 && d.slotIndex < _deck.Count) _deck[d.slotIndex].cooldownRemaining = 0f;
            _stats.defendersPlanted--;
            RemoveDefender(d, false);
            return true;
        }

        public DefenderRuntime DefenderAt(GridCoord c) => Grid.Get(c);

        // ================================================================== pickups

        public bool CollectPickup(int pickupId)
        {
            for (int i = 0; i < _pickups.Count; i++)
            {
                PickupRuntime p = _pickups[i];
                if (p.Id != pickupId || !p.alive) continue;
                CollectPickup(p);
                return true;
            }
            return false;
        }

        private void CollectPickup(PickupRuntime p)
        {
            p.alive = false;
            int gained = Vitality.Add(p.amount, true);
            _stats.vitalityCollected += gained;
            PickupRemoved?.Invoke(p, true);
        }

        internal void SpawnDefenderPickup(DefenderRuntime d, int amount)
        {
            SpawnPickup(PickupSource.Defender, amount, d.Coord.CenterX + 0.15f, d.Coord.lane - 0.22f);
            DefenderActed?.Invoke(d);
        }

        private void SpawnPickup(PickupSource source, int amount, float x, float laneFloat)
        {
            PickupRuntime p = _pickupPool.Rent();
            p.Id = _nextId++;
            p.amount = amount;
            p.x = x;
            p.laneFloat = laneFloat;
            p.lifetime = p.remaining = Encounter.pickupLifetime;
            p.source = source;
            p.alive = true;
            _pickups.Add(p);
            PickupSpawned?.Invoke(p);
        }

        private void TickSkyDrops(float dt)
        {
            if (Encounter.skyDropInterval <= 0f) return;
            _skyTimer -= dt;
            if (_skyTimer > 0f) return;
            _skyTimer += Encounter.skyDropInterval;
            float x = _rng.Range(1.0f, Columns - 0.6f);
            float lane = _rng.Range(0, Lanes) + _rng.Range(-0.2f, 0.2f);
            SpawnPickup(PickupSource.Sky, Encounter.skyDropAmount, x, lane);
        }

        private void TickPickups(float dt)
        {
            for (int i = 0; i < _pickups.Count; i++)
            {
                PickupRuntime p = _pickups[i];
                if (!p.alive) continue;
                p.age += dt;
                p.remaining -= dt;
                if (Options.autoCollect && p.age >= Options.autoCollectDelay)
                {
                    CollectPickup(p);
                    continue;
                }
                if (p.remaining <= 0f)
                {
                    p.alive = false;
                    PickupRemoved?.Invoke(p, false);
                }
            }
        }

        // ================================================================== sunlight pools

        /// <summary>Action-speed multiplier for a defender at this cell (sunlight pools).</summary>
        public float SpeedMultiplierAt(GridCoord c)
        {
            for (int i = 0; i < _sunPools.Count; i++) if (_sunPools[i] == c) return Encounter.sunlightBoost;
            return 1f;
        }

        private void TickSunlightPools(float dt)
        {
            if (Encounter.sunlightPoolInterval <= 0f) return;
            if (_sunPoolActive > 0f)
            {
                _sunPoolActive -= dt;
                if (_sunPoolActive <= 0f)
                {
                    _sunPools.Clear();
                    SunlightPoolsChanged?.Invoke();
                }
                return;
            }
            _sunPoolTimer -= dt;
            if (_sunPoolTimer > 0f) return;
            _sunPoolTimer = Encounter.sunlightPoolInterval;
            _sunPoolActive = Encounter.sunlightPoolDuration;
            _sunPools.Clear();
            int guard = 0;
            while (_sunPools.Count < Encounter.sunlightPoolCount && guard++ < 50)
            {
                // Pools favour the player's half of the field so they can be used.
                var c = new GridCoord(_rng.Range(0, Lanes), _rng.Range(0, Math.Max(1, Columns - 3)));
                if (Grid.GetTerrain(c) == CellTerrain.Blocked || _sunPools.Contains(c)) continue;
                _sunPools.Add(c);
            }
            SunlightPoolsChanged?.Invoke();
        }

        // ================================================================== defenders (shared)

        private void TickDefenders(float dt)
        {
            for (int i = 0; i < _defenders.Count; i++)
            {
                DefenderRuntime d = _defenders[i];
                if (!d.alive) continue;
                d.age += dt;
                float bdt = dt;
                if (d.statuses.Count > 0)
                {
                    float dot = d.statuses.Tick(dt, out _);
                    if (dot > 0f) DamageDefender(d, dot);
                    if (!d.alive) continue;
                    if (d.statuses.Has(StatusKind.Stun)) continue;
                    bdt = dt * (1f - d.statuses.SlowFraction()); // spores and corrosion slow its actions
                }
                DefenderBehaviours.For(d.Definition.behaviour).Tick(d, this, bdt);
            }
        }

        public void DamageDefender(DefenderRuntime d, float amount)
        {
            if (d == null || !d.alive || amount <= 0f) return;
            d.health -= amount;
            DefenderDamaged?.Invoke(d, amount);
            if (d.health <= 0f)
            {
                d.health = 0f;
                _stats.defendersLost++;
                RemoveDefender(d, true);
            }
        }

        private void RemoveDefender(DefenderRuntime d, bool killed)
        {
            d.alive = false;
            Grid.Remove(d.Coord, d);
            DefenderRemoved?.Invoke(d, killed);
        }

        // ================================================================== enemies (shared)

        /// <summary>Spawns an enemy at the entry edge of a lane (also used by debug tools and boss summons).</summary>
        public EnemyRuntime SpawnEnemy(EnemyDefinition def, int lane, int waveIndex = -1)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            lane = Math.Max(0, Math.Min(Lanes - 1, lane));
            EnemyRuntime e = _enemyPool.Rent();
            e.Id = _nextId++;
            e.Definition = def;
            e.lane = lane;
            e.targetLane = lane;
            e.x = e.prevX = SpawnX + _rng.Range(0f, 0.2f);
            e.health = def.maxHealth;
            e.armor = e.maxArmor = def.armorHealth;
            e.waveIndex = waveIndex;
            e.activity = EnemyActivity.Walking;
            e.alive = true;
            _enemies.Add(e);
            EnemyAbilities.For(def.ability).OnSpawn(e, this);
            EnemySpawned?.Invoke(e);
            return e;
        }

        public DamageResult DamageEnemy(EnemyRuntime e, float amount, DamageType type)
        {
            if (e == null || !e.alive) return default;
            if (e.wardReduction > 0f) amount *= 1f - e.wardReduction;
            bool hadArmor = e.armor > 0f;
            DamageResult r = DamageCalculator.Apply(amount, type, e.Definition.resistances, ref e.armor, ref e.health);
            e.hitFlash = 0.12f;
            EnemyDamaged?.Invoke(e, r, type);
            if (hadArmor && r.armorBroken) EnemyArmorBroken?.Invoke(e);
            if (r.killed) OnEnemyDied(e);
            return r;
        }

        /// <summary>Removes an enemy outright (devoured by a Snapjaw, or withering when its boss falls).</summary>
        internal void KillEnemy(EnemyRuntime e)
        {
            if (e == null || !e.alive) return;
            e.health = 0f;
            OnEnemyDied(e);
        }

        private void OnEnemyDied(EnemyRuntime e)
        {
            e.alive = false;
            _stats.enemiesDefeated++;
            EnemyKilled?.Invoke(e);
            EnemyAbilities.For(e.Definition.ability).OnDeath(e, this);
            if (e.Definition.vitalityDrop > 0)
                SpawnPickup(PickupSource.Enemy, e.Definition.vitalityDrop, Math.Min(e.x + 0.25f, Columns - 0.3f), e.lane - 0.15f);
        }

        public void ApplyStatus(EnemyRuntime e, StatusEffectDefinition status)
        {
            if (e == null || !e.alive || status == null) return;
            if (e.statuses.Apply(status)) StatusApplied?.Invoke(e, status);
        }

        private void Breach(EnemyRuntime e)
        {
            e.alive = false;
            _stats.breaches++;
            int dmg = e.Definition.breachDamage;
            HeartrootHealth = Math.Max(0, HeartrootHealth - dmg);
            EnemyBreached?.Invoke(e);
            HeartrootDamaged?.Invoke(HeartrootHealth, dmg);
        }

        private int AliveInWave(int wave)
        {
            int n = 0;
            for (int i = 0; i < _enemies.Count; i++) if (_enemies[i].alive && _enemies[i].waveIndex == wave) n++;
            return n;
        }

        public int AliveEnemyCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _enemies.Count; i++) if (_enemies[i].alive) n++;
                return n;
            }
        }

        public EnemyRuntime FindEnemy(int id)
        {
            if (id == 0) return null;
            for (int i = 0; i < _enemies.Count; i++) if (_enemies[i].Id == id && _enemies[i].alive) return _enemies[i];
            return null;
        }

        public DefenderRuntime FindDefender(int id)
        {
            if (id == 0) return null;
            for (int i = 0; i < _defenders.Count; i++) if (_defenders[i].Id == id && _defenders[i].alive) return _defenders[i];
            return null;
        }

        private void OnWaveStarted(int index)
        {
            WaveDefinition w = Encounter.waves[index];
            WaveStarted?.Invoke(index, w != null && w.major);
            if (Boss != null && Boss.state == BossState.Dormant && index >= Encounter.bossArrivesAtWave) BossRise();
        }

        // ================================================================== end state

        private void CheckEnd()
        {
            if (HeartrootHealth <= 0) SetState(BattleState.Lost);
            else if (Boss != null) { if (Boss.state == BossState.Defeated) SetState(BattleState.Won); }
            else if (Waves.Finished && AliveEnemyCount == 0) SetState(BattleState.Won);
        }

        private void SetState(BattleState s)
        {
            if (State == s) return;
            State = s;
            StateChanged?.Invoke(s);
        }

        /// <summary>Seeds earned for the result (0 unless won).</summary>
        public int SeedsEarned => State != BattleState.Won ? 0
            : Encounter.seedReward + (HeartrootHealth >= HeartrootMaxHealth ? Encounter.flawlessBonus : 0);

        /// <summary>Removes dead entities from live lists and returns them to their pools.</summary>
        private void Compact()
        {
            for (int i = _defenders.Count - 1; i >= 0; i--)
                if (!_defenders[i].alive) { _defenderPool.Return(_defenders[i]); _defenders.RemoveAt(i); }
            for (int i = _enemies.Count - 1; i >= 0; i--)
                if (!_enemies[i].alive) { _enemyPool.Return(_enemies[i]); _enemies.RemoveAt(i); }
            for (int i = _projectiles.Count - 1; i >= 0; i--)
                if (!_projectiles[i].alive) { _projectilePool.Return(_projectiles[i]); _projectiles.RemoveAt(i); }
            for (int i = _pickups.Count - 1; i >= 0; i--)
                if (!_pickups[i].alive) { _pickupPool.Return(_pickups[i]); _pickups.RemoveAt(i); }
            for (int i = _clouds.Count - 1; i >= 0; i--)
                if (!_clouds[i].alive) { _cloudPool.Return(_clouds[i]); _clouds.RemoveAt(i); }
        }

        // ================================================================== debug

        public void DebugGrantVitality(int amount) => Vitality.Add(amount);
        public void DebugSkipToNextWave() => Waves.ForceNextWave(_waveStartedCallback);
        public void DebugResetCooldowns() { foreach (DeckSlot s in _deck) s.cooldownRemaining = 0f; }

        public void DebugDefeatAllEnemies()
        {
            for (int i = 0; i < _enemies.Count; i++)
                if (_enemies[i].alive) DamageEnemy(_enemies[i], 1e9f, DamageType.Piercing);
            if (Boss != null && Boss.Arrived && Boss.state != BossState.Defeated) DefeatBoss();
            Compact();
            CheckEnd();
        }
    }
}
