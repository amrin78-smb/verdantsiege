using System.Collections.Generic;
using VerdantSiege.Battle;
using VerdantSiege.Combat;
using VerdantSiege.Data;
using VerdantSiege.Grid;

namespace VerdantSiege.Tests
{
    /// <summary>Milestone 3 test definitions built on <see cref="TestContent"/> (in-memory, independent of shipped values).</summary>
    public static class M3Fixtures
    {
        public static StatusEffectDefinition Status(TestContent c, StatusKind kind, float duration, float magnitude,
                                                    StatusStacking stacking = StatusStacking.RefreshDuration, int maxStacks = 1, float tick = 0.5f)
        {
            var s = c.Make<StatusEffectDefinition>();
            s.kind = kind; s.duration = duration; s.magnitude = magnitude; s.stacking = stacking; s.maxStacks = maxStacks; s.tickInterval = tick;
            s.damageType = DamageType.Toxic;
            return s;
        }

        public static ProjectileDefinition Projectile(TestContent c, float damage, DamageType type, float speed = 8f)
        {
            var p = c.Make<ProjectileDefinition>();
            p.damage = damage; p.damageType = type; p.speed = speed;
            return p;
        }

        public static DefenderDefinition Defender(TestContent c, string id, DefenderBehaviourKind kind, float hp = 300f)
        {
            var d = c.Make<DefenderDefinition>();
            d.id = id; d.behaviour = kind; d.cost = 0; d.cooldown = 0f; d.maxHealth = hp; d.firstAttackDelay = 0f;
            return d;
        }

        public static DefenderDefinition Shooter(TestContent c, ProjectileDefinition p, float interval = 5f)
        {
            DefenderDefinition d = Defender(c, "m3_shooter_" + p.GetInstanceID(), DefenderBehaviourKind.LaneShooter);
            d.projectile = p; d.attackInterval = interval;
            return d;
        }

        public static EnemyDefinition Enemy(TestContent c, string id, float hp = 200f, float speed = 0.25f)
        {
            var e = c.Make<EnemyDefinition>();
            e.id = id; e.maxHealth = hp; e.moveSpeed = speed; e.biteDamage = 30f; e.biteInterval = 1f;
            return e;
        }

        /// <summary>A no-wave sandbox with plenty of Vitality and the given cards (all free, no cooldown).</summary>
        public static BattleSimulation Sandbox(TestContent c, params DefenderDefinition[] roster)
        {
            c.encounter.startingVitality = 2000;
            c.encounter.vitalityCap = 5000;
            c.encounter.prepTime = 999f;
            c.encounter.roster = new List<DefenderDefinition>(roster);
            return c.Sim();
        }

        public static DefenderRuntime Plant(BattleSimulation sim, int slot, int lane, int col)
        {
            DeployResult r = sim.TryDeploy(slot, new GridCoord(lane, col));
            NUnit.Framework.Assert.AreEqual(DeployResult.Ok, r, $"plant slot {slot} at ({lane},{col})");
            return sim.DefenderAt(new GridCoord(lane, col));
        }

        public static EnemyRuntime Spawn(BattleSimulation sim, EnemyDefinition def, int lane, float x)
        {
            EnemyRuntime e = sim.SpawnEnemy(def, lane);
            e.x = e.prevX = x;
            if (def.ability == EnemyAbilityKind.Burrow) e.leapToX = x - def.abilityRange;
            return e;
        }

        public static void Run(BattleSimulation sim, float seconds) => TestContent.Run(sim, seconds);

        /// <summary>Steps until the condition holds (or the time limit), returns elapsed seconds.</summary>
        public static float RunUntil(BattleSimulation sim, System.Func<bool> condition, float limit = 30f)
        {
            float t = 0f;
            while (!condition() && t < limit)
            {
                sim.Step(BattleSimulation.FixedStep);
                t += BattleSimulation.FixedStep;
            }
            return t;
        }
    }
}
