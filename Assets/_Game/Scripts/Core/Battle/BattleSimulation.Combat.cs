using System;
using VerdantSiege.Combat;
using VerdantSiege.Data;
using VerdantSiege.Grid;

namespace VerdantSiege.Battle
{
    /// <summary>A point of a chain-lightning arc (x in columns, lane may be fractional; air = flying target).</summary>
    public struct ChainPoint
    {
        public float x;
        public float lane;
        public bool air;
        public bool boss;
    }

    /// <summary>Defender-side combat: targeting rules, projectiles (straight, lobbed, ricochet, hostile, reflected), melee, traps, healing and chains.</summary>
    public sealed partial class BattleSimulation
    {
        public const int MaxChainPoints = 8;

        public event Action<DefenderRuntime> DefenderTelegraphed;
        /// <summary>Ambusher/trap ready/windup/recovering transitions.</summary>
        public event Action<DefenderRuntime> DefenderStateChanged;
        public event Action<DefenderRuntime, float> DefenderHealed;
        public event Action<DefenderRuntime> DefenderCleansed;
        public event Action<DefenderRuntime, StatusEffectDefinition> DefenderStatusApplied;
        /// <summary>(caster, point count): the arc points are in <see cref="ChainPoints"/> (index 0 = the caster).</summary>
        public event Action<DefenderRuntime, int> ChainCast;
        public event Action<DefenderRuntime, EnemyRuntime> EnemyDevoured;
        /// <summary>A trap erupted (defender) at its cell.</summary>
        public event Action<DefenderRuntime> TrapTriggered;
        /// <summary>A lobbed projectile came down (x = landing point).</summary>
        public event Action<ProjectileRuntime> ProjectileLanded;
        public event Action<ProjectileRuntime> ProjectileReflected;
        public event Action<ProjectileRuntime, DefenderRuntime> HostileProjectileHit;

        public ChainPoint[] ChainPoints { get; } = new ChainPoint[MaxChainPoints];
        private readonly int[] _chainHit = new int[MaxChainPoints];

        // ================================================================== targeting rules

        /// <summary>Can this attack reach the enemy at all (burrowed enemies only by traps/splash; flyers only by anti-air)?</summary>
        public static bool CanTarget(EnemyRuntime e, bool hitsAir)
        {
            if (e == null || !e.alive || e.underground) return false;
            return !e.IsFlying || hitsAir;
        }

        private bool OnField(EnemyRuntime e) => e.x <= Columns + FieldEdgeSlack;

        /// <summary>Nearest reachable enemy in the defender's lane ahead of it (within range, if limited).</summary>
        public EnemyRuntime FindTargetAhead(DefenderRuntime d)
        {
            float cx = d.Coord.CenterX;
            float range = d.Definition.range;
            bool air = HitsAir(d);
            EnemyRuntime best = null;
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyRuntime e = _enemies[i];
                if (e.lane != d.Coord.lane || !CanTarget(e, air)) continue;
                if (e.x < cx - 0.3f || !OnField(e)) continue;
                if (range > 0f && e.x - cx > range) continue;
                if (best == null || e.x < best.x) best = e;
            }
            return best;
        }

        /// <summary>Is there something this defender's attack can hit ahead: an enemy, or the boss at the edge?</summary>
        public bool HasTargetAhead(DefenderRuntime d)
        {
            if (FindTargetAhead(d) != null) return true;
            float range = d.Definition.range;
            return Boss != null && Boss.IsTargetable && (range <= 0f || BossFrontX - d.Coord.CenterX <= range);
        }

        // ================================================================== projectiles: firing

        internal void FireProjectile(DefenderRuntime d)
        {
            ProjectileDefinition def = ProjectileOf(d);
            if (def == null) return;
            EvolutionDefinition evo = d.evolution;
            if (def.motion == ProjectileMotion.Lobbed)
            {
                float targetX;
                EnemyRuntime t = FindTargetAhead(d);
                if (t != null)
                {
                    // Lead the target: land where it will be when the seed comes down.
                    float startX = d.Coord.CenterX + 0.3f;
                    float flight = Math.Max(0f, t.x - startX) / def.speed;
                    targetX = Math.Max(startX + 0.25f, t.x + 0.1f - SpeedOf(t) * flight);
                }
                else targetX = BossFrontX;
                int cluster = evo != null ? Math.Max(1, evo.lobClusterCount) : 1;
                if (cluster <= 1) SpawnShot(d, def, d.Coord.lane, def.damage, targetX);
                else
                    for (int k = 0; k < cluster; k++) // Firestorm: a line of seeds down the lane (the first on the target)
                        SpawnShot(d, def, d.Coord.lane, def.damage * evo.lobClusterDamageFraction,
                            Math.Min(targetX + k * evo.lobClusterSpacing, Math.Max(targetX, BossFrontX)));
            }
            else
            {
                SpawnShot(d, def, d.Coord.lane, def.damage, 0f);
                int side = evo != null ? evo.multishotSideLanes : 0;
                for (int k = 1; k <= side; k++) // Fan: one shot down each neighbouring lane
                {
                    float dmg = def.damage * evo.sideShotDamageFraction;
                    if (d.Coord.lane - k >= 0) SpawnShot(d, def, d.Coord.lane - k, dmg, 0f);
                    if (d.Coord.lane + k < Lanes) SpawnShot(d, def, d.Coord.lane + k, dmg, 0f);
                }
            }
            DefenderActed?.Invoke(d);
        }

        private void SpawnShot(DefenderRuntime d, ProjectileDefinition def, int lane, float damage, float targetX)
        {
            ProjectileRuntime p = RentProjectile(def);
            p.damage = damage;
            p.lane = lane;
            p.fromLane = d.Coord.lane; // fan shots visibly peel off into their lane
            p.startX = p.prevX = p.x = p.hopFromX = d.Coord.CenterX + 0.3f;
            p.hitsRemaining = Math.Max(1, def.pierceCount);
            p.bouncesRemaining = def.bounces;
            p.sourceDefenderId = d.Id;
            p.sourceDefender = d;
            p.targetX = targetX;
            _projectiles.Add(p);
            ProjectileFired?.Invoke(p);
        }

        /// <summary>Hostile projectile from an enemy at a defender cell (Sapper spit).</summary>
        internal void FireHostileProjectile(EnemyRuntime e, DefenderRuntime target, ProjectileDefinition def, float damage)
        {
            if (def == null || target == null) return;
            ProjectileRuntime p = RentProjectile(def);
            p.hostile = true;
            p.damage = damage > 0f ? damage : def.damage;
            p.lane = p.fromLane = e.lane;
            p.startX = p.prevX = p.x = p.hopFromX = e.x - 0.1f;
            p.targetColumn = target.Coord.column;
            p.hitsRemaining = 1;
            p.sourceEnemyId = e.Id;
            _projectiles.Add(p);
            ProjectileFired?.Invoke(p);
        }

        private ProjectileRuntime RentProjectile(ProjectileDefinition def)
        {
            ProjectileRuntime p = _projectilePool.Rent();
            p.Id = _nextId++;
            p.Definition = def;
            p.damage = def.damage;
            p.alive = true;
            return p;
        }

        // ================================================================== projectiles: flight

        private void TickProjectiles(float dt)
        {
            float despawnX = Columns + 1.0f;
            for (int i = 0; i < _projectiles.Count; i++)
            {
                ProjectileRuntime p = _projectiles[i];
                if (!p.alive) continue;
                p.prevX = p.x;
                if (p.hostile) TickHostile(p, dt);
                else if (p.homingEnemyId != 0) TickHoming(p, dt);
                else if (p.Definition.motion == ProjectileMotion.Lobbed) TickLobbed(p, dt);
                else TickStraight(p, dt);
                if (p.alive && (p.x > despawnX || p.x < BreachX - 0.5f)) p.alive = false;
                if (!p.alive) ProjectileDespawned?.Invoke(p);
            }
        }

        private void TickStraight(ProjectileRuntime p, float dt)
        {
            p.x += p.Definition.speed * dt;
            while (p.alive && p.hitsRemaining > 0)
            {
                EnemyRuntime target = FindProjectileHit(p);
                if (target == null) break;
                p.MarkHit(target.Id);
                p.hitsRemaining--;
                ApplyProjectileHit(p, target);
                if (TryBounce(p, target)) return;
                if (p.hitsRemaining <= 0) p.alive = false;
            }
            if (p.alive && p.hitsRemaining > 0 && Boss != null && Boss.IsTargetable && p.x >= BossFrontX)
            {
                HitBoss(p);
                p.alive = false;
            }
        }

        private EnemyRuntime FindProjectileHit(ProjectileRuntime p)
        {
            EnemyRuntime best = null;
            bool air = p.Definition.canHitAir;
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyRuntime e = _enemies[i];
                if (e.lane != p.lane || !CanTarget(e, air) || p.AlreadyHit(e.Id)) continue;
                if (!OnField(e)) continue; // not yet on the field
                // projectile swept segment [prevX, x] overlaps the enemy body [front-0.2, back]
                if (p.x < e.x - 0.2f || p.prevX > e.BackX) continue;
                if (best == null || e.x < best.x) best = e;
            }
            return best;
        }

        /// <summary>Ricochet: after a hit, hop to the nearest unhit enemy in the same or an adjacent lane.</summary>
        private bool TryBounce(ProjectileRuntime p, EnemyRuntime from)
        {
            if (p.bouncesRemaining <= 0) return false;
            ProjectileDefinition def = p.Definition;
            EnemyRuntime best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyRuntime e = _enemies[i];
                if (e == from || !CanTarget(e, def.canHitAir) || p.AlreadyHit(e.Id) || !OnField(e)) continue;
                int dl = Math.Abs(e.lane - from.lane);
                if (dl > 1) continue;
                float dx = Math.Abs(e.x - from.x);
                float dist = (float)Math.Sqrt(dx * dx + dl * dl);
                if (dist > def.bounceRange || dist >= bestD) continue;
                bestD = dist;
                best = e;
            }
            if (best == null) return false;
            p.bouncesRemaining--;
            p.damage *= def.bounceFalloff;
            p.homingEnemyId = best.Id;
            p.fromLane = p.lane;
            p.hopFromX = p.x;
            p.lane = best.lane;
            p.hitsRemaining = 1;
            return true;
        }

        private void TickHoming(ProjectileRuntime p, float dt)
        {
            EnemyRuntime e = FindEnemy(p.homingEnemyId);
            if (e == null || !CanTarget(e, true)) { p.alive = false; return; }
            p.lane = e.lane;
            float tx = e.x + 0.1f;
            float step = p.Definition.speed * dt;
            if (Math.Abs(tx - p.x) > step)
            {
                p.x += tx > p.x ? step : -step;
                return;
            }
            p.x = tx;
            p.MarkHit(e.Id);
            p.homingEnemyId = 0;
            ApplyProjectileHit(p, e);
            if (!p.reflected && TryBounce(p, e)) return;
            p.alive = false;
        }

        private void TickLobbed(ProjectileRuntime p, float dt)
        {
            p.x += p.Definition.speed * dt;
            if (p.x < p.targetX) return;
            p.x = p.targetX;
            p.alive = false;
            ProjectileLanded?.Invoke(p);
            ProjectileDefinition def = p.Definition;
            if (Boss != null && Boss.IsTargetable && p.targetX >= BossFrontX - 0.05f)
            {
                HitBoss(p);
                return;
            }
            float r = Math.Max(def.splashRadius, 0.35f);
            float lo = p.targetX - r, hi = p.targetX + r;
            EnemyRuntime primary = null;
            float bestD = float.MaxValue;
            DefenderRuntime src = SourceOf(p);
            int side = def.splashSideLanes; // Wildfire: the burst also spills into neighbouring lanes
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyRuntime e = _enemies[i];
                if (!e.alive || e.lane != p.lane || (e.IsFlying && !def.canHitAir) || !OnField(e)) continue;
                if (e.BackX < lo || e.x - 0.2f > hi) continue;
                float dd = Math.Abs(e.x - p.targetX);
                if (dd < bestD) { bestD = dd; primary = e; }
            }
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyRuntime e = _enemies[i];
                if (!e.alive || Math.Abs(e.lane - p.lane) > side || (e.IsFlying && !def.canHitAir) || !OnField(e)) continue;
                if (e.BackX < lo || e.x - 0.2f > hi) continue;
                float dmg = e == primary ? p.damage : p.damage * def.splashDamageFraction;
                if (e == primary) ProjectileHit?.Invoke(p, e);
                DamageEnemyBy(src, e, dmg, def.damageType);
                if (def.statusEffect != null && e.alive) ApplyStatus(e, def.statusEffect);
            }
            if (def.ignitesClouds)
                for (int l = Math.Max(0, p.lane - side); l <= Math.Min(Lanes - 1, p.lane + side); l++) BurnClouds(l, lo, hi);
        }

        private void ApplyProjectileHit(ProjectileRuntime p, EnemyRuntime e)
        {
            ProjectileDefinition def = p.Definition;
            DefenderRuntime src = SourceOf(p);
            ProjectileHit?.Invoke(p, e);
            float damage = p.damage;
            // Glacial Shards: shatter damage against slowed or frozen foes.
            if (def.damageMultiplierVsChilled > 1f && (e.statuses.SlowFraction() > 0f || e.statuses.Has(StatusKind.Stun))) damage *= def.damageMultiplierVsChilled;
            DamageEnemyBy(src, e, damage, def.damageType);
            if (def.statusEffect != null && e.alive) ApplyStatus(e, def.statusEffect);
            if (def.freezeAfterHits > 0 && def.freezeStatus != null && e.alive)
            {
                e.frostHits++;
                if (e.frostHits >= def.freezeAfterHits)
                {
                    e.frostHits = 0;
                    ApplyStatus(e, def.freezeStatus);
                }
            }
            if (def.knockback > 0f && e.alive && !e.IsFlying) e.x = Math.Min(e.x + def.knockback, SpawnX);
            if (def.splashRadius > 0f)
            {
                float splash = p.damage * def.splashDamageFraction;
                for (int i = 0; i < _enemies.Count; i++)
                {
                    EnemyRuntime o = _enemies[i];
                    if (o == e || !o.alive || o.lane != e.lane || (o.IsFlying && !def.canHitAir)) continue;
                    if (Math.Abs(o.x - e.x) <= def.splashRadius) DamageEnemyBy(src, o, splash, def.damageType); // reaches burrowed enemies
                }
            }
            if (def.ignitesClouds) BurnClouds(e.lane, e.x - Math.Max(0.3f, def.splashRadius), e.x + Math.Max(0.3f, def.splashRadius));
        }

        private void TickHostile(ProjectileRuntime p, float dt)
        {
            p.x -= p.Definition.speed * dt;
            float hitX = p.targetColumn + 0.5f;
            if (p.x > hitX) return;
            p.x = hitX;
            DefenderRuntime d = Grid.Get(p.lane, p.targetColumn);
            if (d != null && d.alive)
            {
                if (d.Definition.reflectsProjectiles)
                {
                    // Mirrorleaf: the glob bounces back at the shooter, stronger.
                    p.hostile = false;
                    p.reflected = true;
                    p.damage *= d.evolution != null && d.evolution.reflectMultiplierOverride > 0f ? d.evolution.reflectMultiplierOverride : d.Definition.reflectMultiplier;
                    p.homingEnemyId = p.sourceEnemyId;
                    p.fromLane = p.lane;
                    p.hopFromX = p.x;
                    ProjectileReflected?.Invoke(p);
                    if (FindEnemy(p.homingEnemyId) == null) p.alive = false;
                    return;
                }
                HostileProjectileHit?.Invoke(p, d);
                StatusEffectDefinition st = p.Definition.statusEffect;
                DamageDefender(d, p.damage);
                if (st != null && d.alive) ApplyDefenderStatus(d, st);
            }
            p.alive = false;
        }

        public void ApplyDefenderStatus(DefenderRuntime d, StatusEffectDefinition status)
        {
            if (d == null || !d.alive || status == null) return;
            if (d.statuses.Apply(status)) DefenderStatusApplied?.Invoke(d, status);
        }

        // ================================================================== ambusher (Snapjaw Vine)

        /// <summary>Nearest ground enemy within the ambusher's reach ahead (not flying, not burrowed).</summary>
        public EnemyRuntime FindMeleeTarget(DefenderRuntime d)
        {
            float cx = d.Coord.CenterX;
            float reach = AbilityRange(d);
            bool air = HitsAir(d);
            EnemyRuntime best = null;
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyRuntime e = _enemies[i];
                if (e.lane != d.Coord.lane || !CanTarget(e, air)) continue;
                if (e.x < cx - 0.3f || e.x - cx > reach || !OnField(e)) continue;
                if (best == null || e.x < best.x) best = e;
            }
            return best;
        }

        internal void Snap(DefenderRuntime d, EnemyRuntime e)
        {
            DefenderActed?.Invoke(d);
            if (e.Definition.devourable)
            {
                EnemyDevoured?.Invoke(d, e);
                GrantXp(d, EvolutionXpSource.DamageDealt, e.health + e.armor);
                KillEnemy(e);
            }
            else DamageEnemyBy(d, e, AbilityDamage(d), d.Definition.abilityDamageType);
        }

        internal void RaiseDefenderTelegraph(DefenderRuntime d) => DefenderTelegraphed?.Invoke(d);
        internal void RaiseDefenderState(DefenderRuntime d) => DefenderStateChanged?.Invoke(d);

        // ================================================================== trap (Burrowroot)

        /// <summary>Is a non-flying enemy (burrowed included) standing on this cell?</summary>
        public bool EnemyOnCell(GridCoord c)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyRuntime e = _enemies[i];
                if (!e.alive || e.lane != c.lane || e.IsFlying || e.activity == EnemyActivity.Leaping) continue;
                if (e.x < c.column + 1f && e.x >= c.column - 0.2f) return true;
            }
            return false;
        }

        /// <summary>Burst on the trap's cell: damages every non-flying enemy within the blast (burrowed included).</summary>
        internal void TriggerTrap(DefenderRuntime d)
        {
            float cx = d.Coord.CenterX;
            float r = Math.Max(0.5f, AbilityRange(d));
            int side = d.evolution != null ? d.evolution.blastSideLanes : 0; // Bramble Web: bursts in neighbouring lanes too
            StatusEffectDefinition status = d.evolution != null ? d.evolution.abilityStatusOverride : null;
            float damage = AbilityDamage(d);
            DefenderActed?.Invoke(d);
            TrapTriggered?.Invoke(d);
            GrantXp(d, EvolutionXpSource.Triggers, 1f);
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyRuntime e = _enemies[i];
                if (!e.alive || Math.Abs(e.lane - d.Coord.lane) > side || e.IsFlying) continue;
                if (e.BackX < cx - r || e.x > cx + r) continue;
                DamageEnemyBy(d, e, damage, d.Definition.abilityDamageType);
                if (status != null && e.alive) ApplyStatus(e, status);
            }
        }

        // ================================================================== mender (Mender Moss)

        /// <summary>Heals and cleanses defenders within the mender's square radius. Returns false if nobody needed it.</summary>
        internal bool MenderPulse(DefenderRuntime m)
        {
            DefenderDefinition def = m.Definition;
            int r = Math.Max(0, (int)Math.Round(AbilityRange(m)));
            float heal = AbilityDamage(m);
            bool any = false;
            for (int i = 0; i < _defenders.Count; i++)
            {
                DefenderRuntime d = _defenders[i];
                if (!d.alive) continue;
                if (Math.Abs(d.Coord.lane - m.Coord.lane) > r || Math.Abs(d.Coord.column - m.Coord.column) > r) continue;
                bool needsHeal = d.health < d.MaxHealth - 0.5f && heal > 0f;
                bool needsCleanse = def.abilityCount > 0 && d.statuses.HasNegative();
                if (!needsHeal && !needsCleanse) continue;
                if (!any) DefenderActed?.Invoke(m);
                any = true;
                if (needsHeal)
                {
                    float amount = Math.Min(heal, d.MaxHealth - d.health);
                    d.health += amount;
                    GrantXp(m, EvolutionXpSource.HealingDone, amount);
                    DefenderHealed?.Invoke(d, amount);
                }
                if (needsCleanse)
                {
                    bool removed = false;
                    for (int k = 0; k < def.abilityCount; k++) removed |= d.statuses.RemoveOneNegative();
                    if (removed) DefenderCleansed?.Invoke(d);
                }
            }
            return any;
        }

        // ================================================================== chain caster (Storm Orchid)

        /// <summary>Instant lightning: first target ahead in lane (or the boss), then jumps across adjacent lanes.</summary>
        internal bool CastChain(DefenderRuntime d)
        {
            DefenderDefinition def = d.Definition;
            EnemyRuntime first = FindTargetAhead(d);
            int n = 0;
            ChainPoints[n++] = new ChainPoint { x = d.Coord.CenterX + 0.1f, lane = d.Coord.lane };
            if (first == null)
            {
                if (Boss == null || !Boss.IsTargetable || (def.range > 0f && BossFrontX - d.Coord.CenterX > def.range)) return false;
                ChainPoints[n++] = new ChainPoint { x = BossFrontX + 0.4f, lane = d.Coord.lane, boss = true };
                DefenderActed?.Invoke(d);
                ChainCast?.Invoke(d, n);
                DamageBoss(AbilityDamage(d), def.abilityDamageType);
                return true;
            }
            int hitCount = 0;
            float damage = AbilityDamage(d);
            EvolutionDefinition evo = d.evolution;
            int count = def.abilityCount;
            float falloff = def.abilityFalloff;
            StatusEffectDefinition status = AbilityStatus(d);
            bool statusOnAll = false;
            if (evo != null)
            {
                count = evo.abilityCountOverride > 0 ? evo.abilityCountOverride : count + evo.abilityCountBonus;
                if (evo.abilityFalloffOverride > 0f) falloff = evo.abilityFalloffOverride;
                statusOnAll = evo.abilityStatusOnAll;
            }
            bool air = HitsAir(d);
            float jump = AbilityRange(d);
            EnemyRuntime current = first;
            int targets = Math.Min(Math.Max(1, count), MaxChainPoints - 1);
            DefenderActed?.Invoke(d);
            for (int k = 0; k < targets && current != null; k++)
            {
                _chainHit[hitCount++] = current.Id;
                ChainPoints[n++] = new ChainPoint { x = current.x + 0.15f, lane = current.lane, air = current.IsFlying };
                EnemyRuntime next = NextChainTarget(current, air, jump, hitCount);
                bool mechanical = current.Definition.HasTag(EnemyTags.Mechanical);
                DamageEnemyBy(d, current, damage, def.abilityDamageType);
                if ((mechanical || statusOnAll) && status != null && current.alive) ApplyStatus(current, status);
                damage *= falloff;
                current = next;
            }
            ChainCast?.Invoke(d, n);
            return true;
        }

        private EnemyRuntime NextChainTarget(EnemyRuntime from, bool hitsAir, float jumpRange, int hitCount)
        {
            EnemyRuntime best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyRuntime e = _enemies[i];
                if (e == from || !CanTarget(e, hitsAir) || !OnField(e)) continue;
                bool hit = false;
                for (int h = 0; h < hitCount; h++) if (_chainHit[h] == e.Id) { hit = true; break; }
                if (hit) continue;
                int dl = Math.Abs(e.lane - from.lane);
                if (dl > 1) continue;
                float dx = e.x - from.x;
                float dist = (float)Math.Sqrt(dx * dx + dl * dl);
                if (dist > jumpRange || dist >= bestD) continue;
                bestD = dist;
                best = e;
            }
            return best;
        }

        // ================================================================== decoy (Mirrorleaf)

        /// <summary>Lures one walking ground enemy from an adjacent lane into the decoy's lane. Returns true if one was lured.</summary>
        internal bool TryLure(DefenderRuntime d)
        {
            float cx = d.Coord.CenterX;
            float reach = AbilityRange(d);
            int lanes = d.evolution != null && d.evolution.lureLaneReach > 0 ? d.evolution.lureLaneReach : 1; // Hall of Mirrors
            EnemyRuntime best = null;
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyRuntime e = _enemies[i];
                if (!e.alive || e.lured || e.laneSwitchTimer > 0f || e.IsFlying || e.underground) continue;
                int dl = Math.Abs(e.lane - d.Coord.lane);
                if (e.activity != EnemyActivity.Walking || dl < 1 || dl > lanes) continue;
                if (e.x < cx + 0.3f || e.x > cx + reach || !OnField(e)) continue;
                if (best == null || e.x < best.x) best = e;
            }
            if (best == null) return false;
            best.lured = true;
            best.targetLane = d.Coord.lane;
            best.laneSwitchTimer = EnemyRuntime.LaneSwitchSeconds;
            DefenderActed?.Invoke(d);
            EnemyLured?.Invoke(best, d);
            return true;
        }
    }
}
