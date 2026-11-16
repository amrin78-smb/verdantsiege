using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VerdantSiege.Battle;
using VerdantSiege.Data;
using VerdantSiege.Grid;

namespace VerdantSiege.Tests
{
    /// <summary>
    /// Checks the real shipped content: every encounter validates, a simple scripted player (human-like delays,
    /// no perfect play) wins across seeds, and an undefended garden loses. This guards balance regressions.
    /// </summary>
    public sealed class ShippedContentTests
    {
        private const string DbPath = "Assets/_Game/Data/ContentDatabase.asset";

        private static ContentDatabase Db()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(DbPath);
            Assert.IsNotNull(db, "Run Tools/Verdant Siege/Setup Project first");
            return db;
        }

        [Test]
        public void AllEncounters_Validate_AndHaveArt()
        {
            ContentDatabase db = Db();
            Assert.GreaterOrEqual(db.encounters.Count, 1);
            foreach (EncounterDefinition e in db.encounters)
                CollectionAssert.IsEmpty(EncounterValidator.Validate(e), e.id);
            foreach (DefenderDefinition d in db.defenders)
            {
                Assert.IsNotNull(d.prefab, d.id + " prefab");
                Assert.IsNotNull(d.icon, d.id + " icon");
                Assert.IsFalse(string.IsNullOrEmpty(d.description), d.id + " description");
            }
            foreach (EnemyDefinition e in db.enemies) Assert.IsNotNull(e.prefab, e.id + " prefab");
        }

        [Test]
        public void Ids_AreUnique()
        {
            ContentDatabase db = Db();
            var ids = new HashSet<string>();
            foreach (DefenderDefinition d in db.defenders) Assert.IsTrue(ids.Add("d:" + d.id), d.id);
            foreach (EnemyDefinition e in db.enemies) Assert.IsTrue(ids.Add("e:" + e.id), e.id);
            foreach (EncounterDefinition e in db.encounters) Assert.IsTrue(ids.Add("m:" + e.id), e.id);
        }

        [Test]
        public void Encounters_AreWinnableByScriptedPlayer([Values(1, 7, 42, 1234, 99999)] int seed)
        {
            foreach (EncounterDefinition e in Db().encounters)
            {
                BattleSimulation sim = Play(e, seed, true, out float minutes);
                Assert.AreEqual(BattleState.Won, sim.State, $"{e.id} seed {seed}: bot lost (heart {sim.HeartrootHealth}, {minutes:0.0} min)");
                Assert.That(minutes, Is.InRange(2.5f, 9f), $"{e.id} encounter length {minutes:0.0} min outside 2.5..9");
                Debug.Log($"[Balance] {e.id} seed={seed} WON in {minutes:0.0} min, heart {sim.HeartrootHealth}/{sim.HeartrootMaxHealth}, " +
                          $"planted {sim.Stats.defendersPlanted}, lost {sim.Stats.defendersLost}");
            }
        }

        [Test]
        public void Encounters_AreLostWithoutDefence()
        {
            foreach (EncounterDefinition e in Db().encounters)
            {
                BattleSimulation sim = Play(e, 5, false, out _);
                Assert.AreEqual(BattleState.Lost, sim.State, e.id);
            }
        }

        /// <summary>Simple player: reacts every 0.5 s, collects pickups after ~1.5 s, plants with a fixed plan.</summary>
        private static BattleSimulation Play(EncounterDefinition e, int seed, bool defend, out float minutes)
        {
            var sim = new BattleSimulation(e, seed, BattleOptions.Default);
            float think = 0f;
            const float dt = BattleSimulation.FixedStep;
            int guard = 0;
            while (sim.State == BattleState.Running && guard++ < 30 * 60 * 15)
            {
                sim.Step(dt);
                think -= dt;
                if (!defend || think > 0f) continue;
                think = 0.5f;
                foreach (PickupRuntime p in sim.Pickups)
                    if (p.alive && p.age > 1.5f) { sim.CollectPickup(p.Id); break; }
                Plan(sim);
            }
            minutes = sim.Stats.elapsed / 60f;
            return sim;
        }

        private static int Slot(BattleSimulation sim, string id)
        {
            for (int i = 0; i < sim.Deck.Count; i++) if (sim.Deck[i].Definition.id == id) return i;
            return -1;
        }

        private static int CountLane(BattleSimulation sim, int lane, string id)
        {
            int n = 0;
            foreach (DefenderRuntime d in sim.Defenders) if (d.alive && (lane < 0 || d.Coord.lane == lane) && d.Definition.id == id) n++;
            return n;
        }

        private static bool Try(BattleSimulation sim, int slot, int lane, params int[] cols)
        {
            if (slot < 0) return false;
            foreach (int c in cols)
                if (sim.TryDeploy(slot, new GridCoord(lane, c)) == DeployResult.Ok) return true;
            return false;
        }

        private static void Plan(BattleSimulation sim)
        {
            int glow = Slot(sim, "glowcap"), shoot = Slot(sim, "sunspore"), wall = Slot(sim, "bulwark_bloom");
            int lanes = sim.Lanes;
            for (int l = 0; l < lanes; l++)
            {
                bool threat = false;
                foreach (EnemyRuntime en in sim.Enemies) if (en.alive && en.lane == l && en.x < sim.Columns) threat = true;
                if (threat && CountLane(sim, l, "sunspore") == 0 && Try(sim, shoot, l, 1, 2, 0)) return;
            }
            int glows = CountLane(sim, -1, "glowcap");
            if (glows < 2 || (glows < lanes && sim.Waves.CurrentWave < 3))
                for (int k = 0; k < lanes; k++) { int l = (2 + k) % lanes; if (CountLane(sim, l, "glowcap") == 0 && Try(sim, glow, l, 0)) return; }
            for (int k = 0; k < lanes; k++) { int l = (2 + k) % lanes; if (CountLane(sim, l, "sunspore") == 0 && Try(sim, shoot, l, 1, 2)) return; }
            foreach (EnemyRuntime en in sim.Enemies)
                if (en.alive && en.Definition.armorHealth > 0f && CountLane(sim, en.lane, "bulwark_bloom") == 0 && Try(sim, wall, en.lane, 5, 4, 6)) return;
            for (int k = 0; k < lanes; k++) { int l = (2 + k) % lanes; if (CountLane(sim, l, "sunspore") < 2 && Try(sim, shoot, l, 2, 3)) return; }
            for (int k = 0; k < lanes; k++) { int l = (2 + k) % lanes; if (CountLane(sim, l, "sunspore") < 3 && Try(sim, shoot, l, 3, 4)) return; }
            for (int k = 0; k < lanes; k++) { int l = (2 + k) % lanes; if (CountLane(sim, l, "bulwark_bloom") == 0 && Try(sim, wall, l, 6, 5)) return; }
        }
    }
}
