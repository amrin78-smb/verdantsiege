using System.Collections.Generic;
using UnityEngine;
using VerdantSiege.Battle;
using VerdantSiege.Combat;
using VerdantSiege.Data;

namespace VerdantSiege.Tests
{
    /// <summary>In-memory content for tests (independent of the shipped asset values).</summary>
    public sealed class TestContent
    {
        public readonly List<Object> created = new List<Object>();
        public ProjectileDefinition bolt;
        public DefenderDefinition shooter, blocker, generator;
        public EnemyDefinition walker, runner, armored;
        public BattlefieldDefinition field;
        public EncounterDefinition encounter;

        public T Make<T>() where T : ScriptableObject
        {
            T so = ScriptableObject.CreateInstance<T>();
            created.Add(so);
            return so;
        }

        public TestContent()
        {
            bolt = Make<ProjectileDefinition>();
            bolt.id = "bolt"; bolt.speed = 6f; bolt.damage = 20f;

            shooter = Make<DefenderDefinition>();
            shooter.id = "shooter"; shooter.cost = 100; shooter.cooldown = 5f; shooter.maxHealth = 300f;
            shooter.behaviour = DefenderBehaviourKind.LaneShooter; shooter.projectile = bolt;
            shooter.attackInterval = 1.4f; shooter.firstAttackDelay = 0.5f;

            blocker = Make<DefenderDefinition>();
            blocker.id = "blocker"; blocker.cost = 125; blocker.cooldown = 12f; blocker.maxHealth = 3000f;
            blocker.behaviour = DefenderBehaviourKind.Blocker;

            generator = Make<DefenderDefinition>();
            generator.id = "generator"; generator.cost = 75; generator.cooldown = 8f; generator.maxHealth = 300f;
            generator.behaviour = DefenderBehaviourKind.Generator; generator.generateFirstDelay = 5f;
            generator.generateInterval = 12f; generator.generateAmount = 25; generator.burstEvery = 4; generator.burstAmount = 50;

            walker = Make<EnemyDefinition>();
            walker.id = "walker"; walker.maxHealth = 200f; walker.moveSpeed = 0.25f; walker.biteDamage = 30f; walker.biteInterval = 1f;

            runner = Make<EnemyDefinition>();
            runner.id = "runner"; runner.maxHealth = 90f; runner.moveSpeed = 0.6f; runner.biteDamage = 15f; runner.biteInterval = 0.6f;

            armored = Make<EnemyDefinition>();
            armored.id = "armored"; armored.maxHealth = 300f; armored.armorHealth = 400f; armored.moveSpeed = 0.16f;
            armored.speedMultiplierAfterArmorBreak = 1.25f; armored.breachDamage = 2; armored.threatCost = 4;

            field = Make<BattlefieldDefinition>();
            field.lanes = 5; field.columns = 9;

            encounter = Make<EncounterDefinition>();
            encounter.id = "test"; encounter.battlefield = field; encounter.startingVitality = 150; encounter.vitalityCap = 2000;
            encounter.skyDropInterval = 0f; encounter.prepTime = 5f; encounter.interWaveGap = 2f; encounter.heartrootHealth = 3;
            encounter.roster = new List<DefenderDefinition> { shooter, blocker, generator };
            encounter.waves = new List<WaveDefinition> { Wave(walker, 1, 2) };
        }

        public WaveDefinition Wave(EnemyDefinition e, int count, int lane, float interval = 2f, float maxDuration = 60f)
        {
            WaveDefinition w = Make<WaveDefinition>();
            w.maxDuration = maxDuration;
            w.groups = new List<SpawnGroup> { new SpawnGroup { enemy = e, count = count, lane = lane, interval = interval } };
            return w;
        }

        public BattleSimulation Sim(int seed = 1) => new BattleSimulation(encounter, seed, BattleOptions.Default);

        public void Destroy()
        {
            foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        public static void Run(BattleSimulation sim, float seconds)
        {
            int steps = Mathf.CeilToInt(seconds / BattleSimulation.FixedStep);
            for (int i = 0; i < steps; i++) sim.Step(BattleSimulation.FixedStep);
        }
    }
}
