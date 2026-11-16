using VerdantSiege.Data;

namespace VerdantSiege.Battle
{
    /// <summary>
    /// Stateless defender behaviour strategy; all mutable state lives on <see cref="DefenderRuntime"/>.
    /// Add a new kind by implementing this and registering it in <see cref="DefenderBehaviours.For"/>.
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

        public static IDefenderBehaviour For(DefenderBehaviourKind kind)
        {
            switch (kind)
            {
                case DefenderBehaviourKind.LaneShooter: return Shooter;
                case DefenderBehaviourKind.Generator: return Generator;
                default: return Blocker;
            }
        }
    }

    /// <summary>Fires its projectile down the lane while an enemy is ahead (and within range, if limited).</summary>
    public sealed class LaneShooterBehaviour : IDefenderBehaviour
    {
        public void OnPlaced(DefenderRuntime d) => d.attackTimer = d.Definition.firstAttackDelay;

        public void Tick(DefenderRuntime d, BattleSimulation sim, float dt)
        {
            if (d.attackTimer > 0f) d.attackTimer -= dt;
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
            d.generateTimer -= dt;
            if (d.generateTimer > 0f) return;
            DefenderDefinition def = d.Definition;
            d.generatePulses++;
            bool burst = def.burstEvery > 0 && d.generatePulses % def.burstEvery == 0;
            sim.SpawnDefenderPickup(d, burst ? def.burstAmount : def.generateAmount);
            d.generateTimer += def.generateInterval;
        }
    }
}
