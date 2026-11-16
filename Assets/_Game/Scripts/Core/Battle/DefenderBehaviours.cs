using VerdantSiege.Data;

namespace VerdantSiege.Battle
{
    /// <summary>
    /// Stateless defender behaviour strategy; all mutable state lives on <see cref="DefenderRuntime"/>.
    /// Add a new kind by implementing this and registering it in <see cref="DefenderBehaviours.For"/>.
    /// The dt passed in is already scaled by the defender's slow statuses (and is 0-skipped while stunned).
    /// </summary>
    public interface IDefenderBehaviour
    {
        void OnPlaced(DefenderRuntime d);
        void Tick(DefenderRuntime d, BattleSimulation sim, float dt);
    }

    public static class DefenderBehaviours
    {
        private static readonly IDefenderBehaviour Shooter = new LaneShooterBehaviour();
        private static readonly IDefenderBehaviour Blocker = new BlockerBehaviour();
        private static readonly IDefenderBehaviour Generator = new GeneratorBehaviour();
        private static readonly IDefenderBehaviour Ambusher = new AmbusherBehaviour();
        private static readonly IDefenderBehaviour Trap = new TrapBehaviour();
        private static readonly IDefenderBehaviour Mender = new MenderBehaviour();
        private static readonly IDefenderBehaviour Chain = new ChainCasterBehaviour();
        private static readonly IDefenderBehaviour Decoy = new DecoyBehaviour();

        public static IDefenderBehaviour For(DefenderBehaviourKind kind)
        {
            switch (kind)
            {
                case DefenderBehaviourKind.LaneShooter: return Shooter;
                case DefenderBehaviourKind.Generator: return Generator;
                case DefenderBehaviourKind.Ambusher: return Ambusher;
                case DefenderBehaviourKind.Trap: return Trap;
                case DefenderBehaviourKind.Mender: return Mender;
                case DefenderBehaviourKind.ChainCaster: return Chain;
                case DefenderBehaviourKind.Decoy: return Decoy;
                default: return Blocker;
            }
        }
    }

    /// <summary>Fires its projectile down the lane while a reachable target is ahead (and within range, if limited).</summary>
    public sealed class LaneShooterBehaviour : IDefenderBehaviour
    {
        public void OnPlaced(DefenderRuntime d) => d.attackTimer = d.Definition.firstAttackDelay;

        public void Tick(DefenderRuntime d, BattleSimulation sim, float dt)
        {
            if (d.attackTimer > 0f) d.attackTimer -= dt * sim.SpeedMultiplierAt(d.Coord);
            if (d.attackTimer > 0f) return;
            if (!sim.HasTargetAhead(d)) return; // stays ready, fires the moment a target appears
            sim.FireProjectile(d);
            d.attackTimer = d.Definition.attackInterval;
        }
    }

    /// <summary>No attack: its value is its health and the cell it holds.</summary>
    public sealed class BlockerBehaviour : IDefenderBehaviour
    {
        public void OnPlaced(DefenderRuntime d) { }
        public void Tick(DefenderRuntime d, BattleSimulation sim, float dt) { }
    }

    /// <summary>Produces Vitality pickups on a timer; every Nth pulse is a larger burst.</summary>
    public sealed class GeneratorBehaviour : IDefenderBehaviour
    {
        public void OnPlaced(DefenderRuntime d)
        {
            d.generateTimer = d.Definition.generateFirstDelay;
            d.generatePulses = 0;
        }

        public void Tick(DefenderRuntime d, BattleSimulation sim, float dt)
        {
            d.generateTimer -= dt * sim.SpeedMultiplierAt(d.Coord);
            if (d.generateTimer > 0f) return;
            DefenderDefinition def = d.Definition;
            d.generatePulses++;
            bool burst = def.burstEvery > 0 && d.generatePulses % def.burstEvery == 0;
            sim.SpawnDefenderPickup(d, burst ? def.burstAmount : def.generateAmount);
            d.generateTimer += def.generateInterval;
        }
    }

    /// <summary>
    /// Snapjaw Vine: when a ground enemy comes within reach it opens its jaws (abilityWindup, the telegraph), then
    /// swallows it whole (or deals abilityDamage to anything not devourable) and spends abilityRecharge seconds
    /// digesting, during which it cannot attack and is easy prey.
    /// </summary>
    public sealed class AmbusherBehaviour : IDefenderBehaviour
    {
        public void OnPlaced(DefenderRuntime d)
        {
            d.abilityState = DefenderAbilityState.Ready;
            d.abilityTimer = d.Definition.firstAttackDelay;
        }

        public void Tick(DefenderRuntime d, BattleSimulation sim, float dt)
        {
            DefenderDefinition def = d.Definition;
            switch (d.abilityState)
            {
                case DefenderAbilityState.Recovering:
                    d.abilityTimer -= dt;
                    if (d.abilityTimer > 0f) return;
                    d.abilityState = DefenderAbilityState.Ready;
                    d.abilityTimer = 0f;
                    sim.RaiseDefenderState(d);
                    return;
                case DefenderAbilityState.Windup:
                    d.abilityTimer -= dt;
                    if (d.abilityTimer > 0f) return;
                    EnemyRuntime target = sim.FindEnemy(d.abilityTargetId);
                    if (target == null || sim.FindMeleeTarget(d) == null || !BattleSimulation.CanTarget(target, false)
                        || target.lane != d.Coord.lane || target.x - d.Coord.CenterX > def.abilityRange + 0.2f)
                    {
                        // Missed (target died, flew or burrowed): snap on whoever is closest, if anyone.
                        target = sim.FindMeleeTarget(d);
                    }
                    if (target == null)
                    {
                        d.abilityState = DefenderAbilityState.Ready;
                        sim.RaiseDefenderState(d);
                        return;
                    }
                    sim.Snap(d, target);
                    d.abilityState = DefenderAbilityState.Recovering;
                    d.abilityTimer = def.abilityRecharge;
                    sim.RaiseDefenderState(d);
                    return;
                default:
                    if (d.abilityTimer > 0f) { d.abilityTimer -= dt; return; }
                    EnemyRuntime t = sim.FindMeleeTarget(d);
                    if (t == null) return;
                    d.abilityTargetId = t.Id;
                    d.abilityState = DefenderAbilityState.Windup;
                    d.abilityTimer = def.abilityWindup;
                    sim.RaiseDefenderTelegraph(d);
                    sim.RaiseDefenderState(d);
                    return;
            }
        }
    }

    /// <summary>
    /// Burrowroot: hidden while armed (enemies walk onto it instead of eating it). When a ground or burrowed enemy
    /// steps on its cell it erupts for abilityDamage to everything in the blast, then rearms for abilityRecharge
    /// seconds, during which it is an ordinary, edible plant. Planting arms it after firstAttackDelay.
    /// </summary>
    public sealed class TrapBehaviour : IDefenderBehaviour
    {
        public void OnPlaced(DefenderRuntime d)
        {
            d.abilityState = DefenderAbilityState.Recovering;
            d.abilityTimer = d.Definition.firstAttackDelay;
        }

        public void Tick(DefenderRuntime d, BattleSimulation sim, float dt)
        {
            if (d.abilityState == DefenderAbilityState.Recovering)
            {
                d.abilityTimer -= dt;
                if (d.abilityTimer > 0f) return;
                d.abilityState = DefenderAbilityState.Ready;
                sim.RaiseDefenderState(d);
                return;
            }
            if (!sim.EnemyOnCell(d.Coord)) return;
            sim.TriggerTrap(d);
            d.abilityState = DefenderAbilityState.Recovering;
            d.abilityTimer = d.Definition.abilityRecharge;
            sim.RaiseDefenderState(d);
        }
    }

    /// <summary>Mender Moss: every abilityRecharge seconds heals and cleanses the defenders around it (waits while nobody needs it).</summary>
    public sealed class MenderBehaviour : IDefenderBehaviour
    {
        public void OnPlaced(DefenderRuntime d) => d.abilityTimer = d.Definition.firstAttackDelay;

        public void Tick(DefenderRuntime d, BattleSimulation sim, float dt)
        {
            if (d.abilityTimer > 0f) d.abilityTimer -= dt;
            if (d.abilityTimer > 0f) return;
            if (sim.MenderPulse(d)) d.abilityTimer = d.Definition.abilityRecharge;
        }
    }

    /// <summary>Storm Orchid: instant chain lightning on attackInterval while something is ahead.</summary>
    public sealed class ChainCasterBehaviour : IDefenderBehaviour
    {
        public void OnPlaced(DefenderRuntime d) => d.attackTimer = d.Definition.firstAttackDelay;

        public void Tick(DefenderRuntime d, BattleSimulation sim, float dt)
        {
            if (d.attackTimer > 0f) d.attackTimer -= dt * sim.SpeedMultiplierAt(d.Coord);
            if (d.attackTimer > 0f) return;
            if (sim.CastChain(d)) d.attackTimer = d.Definition.attackInterval;
        }
    }

    /// <summary>Mirrorleaf: a tough decoy that lures one enemy from an adjacent lane every abilityRecharge seconds (and reflects spit).</summary>
    public sealed class DecoyBehaviour : IDefenderBehaviour
    {
        public void OnPlaced(DefenderRuntime d) => d.abilityTimer = d.Definition.firstAttackDelay;

        public void Tick(DefenderRuntime d, BattleSimulation sim, float dt)
        {
            if (d.abilityTimer > 0f) d.abilityTimer -= dt;
            if (d.abilityTimer > 0f) return;
            if (sim.TryLure(d)) d.abilityTimer = d.Definition.abilityRecharge;
        }
    }
}
