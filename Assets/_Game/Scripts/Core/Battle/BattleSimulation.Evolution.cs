using System;
using System.Collections.Generic;
using VerdantSiege.Combat;
using VerdantSiege.Data;
using VerdantSiege.Grid;

namespace VerdantSiege.Battle
{
    /// <summary>Result of an evolution attempt. Anything except Ok leaves the battle unchanged.</summary>
    public enum EvolveResult
    {
        Ok = 0,
        BattleOver,
        NoDefender,
        AlreadyEvolved,
        NotReady,
        InvalidOption,
        NotEnoughVitality
    }

    /// <summary>A visible aura link from an evolved aura source to a neighbour it empowers (presentation reads these).</summary>
    public struct AuraLink
    {
        public int sourceId;
        public int targetId;
        public GridCoord from;
        public GridCoord to;
    }

    /// <summary>
    /// Living Evolution: in-battle XP per defender (role-appropriate), one evolution level with 2-3 data-driven
    /// branches (<see cref="EvolutionDefinition"/>), formation auras and the Capacitor release. All state is on
    /// <see cref="DefenderRuntime"/>; the effective-stat helpers below are what behaviours and combat read, so a
    /// branch changes how a defender plays without touching its immutable definition.
    /// </summary>
    public sealed partial class BattleSimulation
    {
        /// <summary>A defender crossed its XP threshold (raised once per defender).</summary>
        public event Action<DefenderRuntime> DefenderEvolutionReady;
        /// <summary>A branch was chosen and applied (Vitality already spent).</summary>
        public event Action<DefenderRuntime, EvolutionDefinition> DefenderEvolved;
        /// <summary>A Capacitor released its stored charge (defender, Vitality amount).</summary>
        public event Action<DefenderRuntime, int> CapacitorReleased;
        /// <summary>An aura source shared Vitality with its links (source, amount).</summary>
        public event Action<DefenderRuntime, int> AuraVitalityShared;
        /// <summary>A thorny/reflective defender hurt the enemy biting it.</summary>
        public event Action<DefenderRuntime, EnemyRuntime> ThornsHit;

        /// <summary>
        /// Seconds of battle (after the first wave starts) for a defender to fill its XP bar with no action at
        /// all: quiet lanes still evolve eventually. Active defenders get there far sooner (45-90 s tuned).
        /// </summary>
        public const float PassiveEvolutionSeconds = 240f;
        public const int MaxAuraLinks = 64;

        public IReadOnlyList<AuraLink> AuraLinks => _auraLinks;
        private readonly List<AuraLink> _auraLinks = new List<AuraLink>(MaxAuraLinks);
        private int _evolvedAuraSources;

        // ================================================================== XP

        /// <summary>Adds XP to a living, unevolved defender and raises the ready event once when it crosses the threshold.</summary>
        internal void GrantXp(DefenderRuntime d, EvolutionXpSource source, float amount)
        {
            if (d == null || !d.alive || amount <= 0f || !d.CanEvolveEver || d.Definition.xpSource != source) return;
            AddXp(d, amount);
        }

        private void AddXp(DefenderRuntime d, float amount)
        {
            d.xp = Math.Min(d.xp + amount, d.Definition.evolutionXp * 1.5f);
            if (!d.evolutionAnnounced && d.xp >= d.Definition.evolutionXp)
            {
                d.evolutionAnnounced = true;
                DefenderEvolutionReady?.Invoke(d);
            }
        }

        /// <summary>Resolves the projectile's firing defender (pooled runtimes are validated by id).</summary>
        private static DefenderRuntime SourceOf(ProjectileRuntime p) =>
            p.sourceDefender != null && p.sourceDefender.alive && p.sourceDefender.Id == p.sourceDefenderId ? p.sourceDefender : null;

        /// <summary>Damage from a defender's attack: same as <see cref="DamageEnemy(EnemyRuntime,float,DamageType)"/> plus XP for damage actually dealt.</summary>
        internal DamageResult DamageEnemyBy(DefenderRuntime source, EnemyRuntime e, float amount, DamageType type)
        {
            DamageResult r = DamageEnemy(e, amount, type);
            if (source != null) GrantXp(source, EvolutionXpSource.DamageDealt, r.Total);
            return r;
        }

        // ================================================================== choosing

        /// <summary>Validates an evolution without changing anything.</summary>
        public EvolveResult CanEvolve(DefenderRuntime d, int option)
        {
            if (State != BattleState.Running) return EvolveResult.BattleOver;
            if (d == null || !d.alive) return EvolveResult.NoDefender;
            if (d.evolution != null) return EvolveResult.AlreadyEvolved;
            if (!d.EvolutionReady) return EvolveResult.NotReady;
            List<EvolutionDefinition> list = d.Definition.evolutions;
            if (option < 0 || option >= list.Count || list[option] == null) return EvolveResult.InvalidOption;
            if (!Vitality.CanAfford(list[option].cost)) return EvolveResult.NotEnoughVitality;
            return EvolveResult.Ok;
        }

        /// <summary>Atomic: validates, spends and applies the branch in one call (a double tap fails on AlreadyEvolved).</summary>
        public EvolveResult TryEvolve(DefenderRuntime d, int option)
        {
            EvolveResult r = CanEvolve(d, option);
            if (r != EvolveResult.Ok) return r;
            EvolutionDefinition evo = d.Definition.evolutions[option];
            if (!Vitality.TrySpend(evo.cost)) return EvolveResult.NotEnoughVitality;
            d.evolution = evo;
            d.auraTimer = evo.auraVitalityInterval;
            // A shooter mid-reload keeps its timing; a slower charge shot starts charging from now.
            if (d.attackTimer > AttackInterval(d)) d.attackTimer = AttackInterval(d);
            if (evo.auraRadius > 0) _evolvedAuraSources++;
            DefenderEvolved?.Invoke(d, evo);
            return EvolveResult.Ok;
        }

        // ================================================================== effective stats (read by behaviours and combat)

        public ProjectileDefinition ProjectileOf(DefenderRuntime d) =>
            d.evolution != null && d.evolution.projectileOverride != null ? d.evolution.projectileOverride : d.Definition.projectile;

        public float AttackInterval(DefenderRuntime d) =>
            d.Definition.attackInterval * (d.evolution != null ? d.evolution.attackIntervalMultiplier : 1f);

        public float AbilityRecharge(DefenderRuntime d) =>
            d.Definition.abilityRecharge * (d.evolution != null ? d.evolution.abilityRechargeMultiplier : 1f);

        public float AbilityRange(DefenderRuntime d) =>
            d.Definition.abilityRange + (d.evolution != null ? d.evolution.abilityRangeBonus : 0f);

        public float AbilityDamage(DefenderRuntime d) =>
            d.Definition.abilityDamage * (d.evolution != null ? d.evolution.abilityDamageMultiplier : 1f);

        public StatusEffectDefinition AbilityStatus(DefenderRuntime d) =>
            d.evolution != null && d.evolution.abilityStatusOverride != null ? d.evolution.abilityStatusOverride : d.Definition.abilityStatus;

        public float GenerateInterval(DefenderRuntime d) =>
            d.Definition.generateInterval * (d.evolution != null ? d.evolution.generateIntervalMultiplier : 1f);

        /// <summary>Does this defender's attack reach flyers (evolution may change the projectile or grant anti-air)?</summary>
        public bool HitsAir(DefenderRuntime d)
        {
            if (d.evolution != null && d.evolution.grantsAntiAir) return true;
            if (d.Definition.behaviour == DefenderBehaviourKind.LaneShooter)
            {
                ProjectileDefinition p = ProjectileOf(d);
                return p != null && p.canHitAir;
            }
            return d.Definition.canHitAir;
        }

        /// <summary>Action-speed multiplier: sunlight pools and the strongest evolved aura it is linked to.</summary>
        public float ActionRate(DefenderRuntime d) => SpeedMultiplierAt(d.Coord) * (1f + d.auraSpeedBonus);

        // ================================================================== auras (computed once per step, before defenders act)

        private void RefreshDefenderAuras(float dt)
        {
            if (_evolvedAuraSources <= 0 && _auraLinks.Count == 0) return;
            _auraLinks.Clear();
            for (int i = 0; i < _defenders.Count; i++) { _defenders[i].auraSpeedBonus = 0f; _defenders[i].auraLinks = 0; }
            int sources = 0;
            for (int s = 0; s < _defenders.Count; s++)
            {
                DefenderRuntime src = _defenders[s];
                EvolutionDefinition evo = src.evolution;
                if (!src.alive || evo == null || evo.auraRadius <= 0) continue;
                sources++;
                for (int i = 0; i < _defenders.Count; i++)
                {
                    DefenderRuntime n = _defenders[i];
                    if (n == src || !n.alive) continue;
                    if (Math.Abs(n.Coord.lane - src.Coord.lane) > evo.auraRadius || Math.Abs(n.Coord.column - src.Coord.column) > evo.auraRadius) continue;
                    src.auraLinks++;
                    if (evo.auraActionSpeedBonus > n.auraSpeedBonus) n.auraSpeedBonus = evo.auraActionSpeedBonus;
                    if (_auraLinks.Count < MaxAuraLinks)
                        _auraLinks.Add(new AuraLink { sourceId = src.Id, targetId = n.Id, from = src.Coord, to = n.Coord });
                }
                if (evo.auraVitalityPerLink > 0 && !src.statuses.Has(StatusKind.Stun))
                {
                    src.auraTimer -= dt;
                    if (src.auraTimer <= 0f)
                    {
                        src.auraTimer += evo.auraVitalityInterval;
                        int amount = evo.auraVitalityPerLink * src.auraLinks;
                        if (amount > 0)
                        {
                            Vitality.Add(amount);
                            AuraVitalityShared?.Invoke(src, amount);
                        }
                    }
                }
            }
            _evolvedAuraSources = sources;
        }

        /// <summary>Passive XP and evolved regeneration (once per step per defender, before its behaviour acts).</summary>
        private void TickEvolution(DefenderRuntime d, float dt)
        {
            if (d.CanEvolveEver && Waves.CurrentWave >= 0) AddXp(d, d.Definition.evolutionXp * dt / PassiveEvolutionSeconds);
            EvolutionDefinition evo = d.evolution;
            if (evo != null && evo.regenPerSecond > 0f && d.health < d.MaxHealth)
                d.health = Math.Min(d.MaxHealth, d.health + evo.regenPerSecond * dt);
        }

        // ================================================================== guardians: thorns and snares

        /// <summary>An enemy bit a defender: thorns hurt the biter, snares apply their status.</summary>
        private void OnDefenderBitten(DefenderRuntime d, EnemyRuntime e, float bite)
        {
            EvolutionDefinition evo = d.evolution;
            if (evo == null || !e.alive) return;
            if (evo.thornsFraction > 0f)
            {
                ThornsHit?.Invoke(d, e);
                DamageEnemyBy(d, e, bite * evo.thornsFraction, DamageType.Piercing);
            }
            if (evo.onBittenStatus != null && e.alive) ApplyStatus(e, evo.onBittenStatus);
        }

        // ================================================================== generators: Capacitor

        /// <summary>Generator pulse: drops a Sunmote, or charges the Capacitor.</summary>
        internal void GeneratorPulse(DefenderRuntime d, int amount)
        {
            GrantXp(d, EvolutionXpSource.VitalityProduced, amount);
            EvolutionDefinition evo = d.evolution;
            if (evo != null && evo.capacitorPulses > 0)
            {
                if (d.storedPulses < evo.capacitorPulses)
                {
                    d.storedPulses++;
                    d.storedVitality += amount;
                }
                DefenderActed?.Invoke(d); // the cap glows brighter; a full capacitor wastes further pulses
                return;
            }
            SpawnDefenderPickup(d, amount);
        }

        public bool IsCapacitor(DefenderRuntime d) => d != null && d.evolution != null && d.evolution.capacitorPulses > 0;
        public bool CapacitorFull(DefenderRuntime d) => IsCapacitor(d) && d.storedPulses >= d.evolution.capacitorPulses;

        /// <summary>Releases the stored charge as one big Sunmote burst (x capacitorReleaseMultiplier). Returns the amount.</summary>
        public int TryReleaseCapacitor(DefenderRuntime d)
        {
            if (State != BattleState.Running || d == null || !d.alive || !IsCapacitor(d) || d.storedVitality <= 0) return 0;
            int amount = (int)Math.Round(d.storedVitality * d.evolution.capacitorReleaseMultiplier);
            d.storedVitality = 0;
            d.storedPulses = 0;
            SpawnDefenderPickup(d, amount);
            CapacitorReleased?.Invoke(d, amount);
            return amount;
        }
    }
}
