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
                // Card portraits are optional (cards fall back to a monogram until art lands).
                Assert.IsFalse(string.IsNullOrEmpty(d.description), d.id + " description");
            }
            foreach (EnemyDefinition e in db.enemies) Assert.IsNotNull(e.prefab, e.id + " prefab");
            foreach (BossDefinition b in db.bosses) Assert.IsNotNull(b.prefab, b.id + " prefab");
        }

        [Test]
        public void Milestone3_RosterAndEncounters_Present()
        {
            ContentDatabase db = Db();
            Assert.AreEqual(12, db.defenders.Count, "launch roster: 12 defenders");
            Assert.AreEqual(12, db.enemies.Count, "launch roster: 12 enemies");
            foreach (string id in new[] { "vh_03", "vh_04", "vh_05", "vh_06", "vh_07" }) Assert.IsNotNull(db.FindEncounter(id), id);
            Assert.IsNotNull(db.FindEncounter("vh_07").boss, "vh_07 is the Root Maw boss fight");
            foreach (EncounterDefinition e in db.encounters) Assert.LessOrEqual(e.roster.Count, EncounterValidator.MaxRosterSize, e.id);
            int elites = 0;
            foreach (EnemyDefinition e in db.enemies) if (e.elite) elites++;
            Assert.AreEqual(2, elites, "Warden and Blight Herald are elites");
        }

        [Test]
        public void Evolution_EveryDefenderHasTwoOrThreeDistinctBranches()
        {
            var ids = new HashSet<string>();
            foreach (DefenderDefinition d in Db().defenders)
            {
                Assert.IsTrue(d.CanEvolve, d.id + " can evolve");
                Assert.That(d.evolutions.Count, Is.InRange(2, 3), d.id + " branch count");
                Assert.Greater(d.evolutionXp, 0f, d.id);
                foreach (EvolutionDefinition e in d.evolutions)
                {
                    Assert.IsNotNull(e, d.id);
                    Assert.IsTrue(ids.Add(e.id), "duplicate evolution id " + e.id);
                    Assert.That(e.cost, Is.InRange(0, 75), e.id + " cost");
                    Assert.IsFalse(string.IsNullOrEmpty(e.displayName), e.id);
                    Assert.That(e.description.Length, Is.InRange(10, 72), e.id + " description must be one short line");
                    Assert.AreEqual(1f, e.accentColor.a, 1e-3f, e.id + " accent opaque");
                }
            }
        }

        [Test]
        public void Ids_AreUnique()
        {
            ContentDatabase db = Db();
            var ids = new HashSet<string>();
            foreach (DefenderDefinition d in db.defenders) Assert.IsTrue(ids.Add("d:" + d.id), d.id);
            foreach (EnemyDefinition e in db.enemies) Assert.IsTrue(ids.Add("e:" + e.id), e.id);
            foreach (EncounterDefinition e in db.encounters) Assert.IsTrue(ids.Add("m:" + e.id), e.id);
            foreach (BossDefinition b in db.bosses) Assert.IsTrue(ids.Add("b:" + b.id), b.id);
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

        /// <summary>Boss length target (owner): about 4-6 minutes for the scripted bot; the weak-point mechanic must come up.</summary>
        [Test]
        public void BossEncounter_LastsFourToSixMinutes_WeakPointsComeUp([Values(1, 42, 99999)] int seed)
        {
            EncounterDefinition e = Db().FindEncounter("vh_07");
            int specials = 0, staggers = 0;
            BattleSimulation sim = Play(e, seed, true, out float minutes, s =>
            {
                s.BossWeakPointsExposed += b => specials++;
                s.BossStaggered += b => staggers++;
            });
            Assert.AreEqual(BattleState.Won, sim.State, $"seed {seed}: bot lost the boss (heart {sim.HeartrootHealth}, {minutes:0.0} min)");
            Assert.That(minutes, Is.InRange(4f, 6.5f), $"seed {seed}: boss encounter {minutes:0.0} min outside 4..6.5");
            Assert.Greater(specials, 0, "the special attack (weak points) happens in every fight");
            Debug.Log($"[Balance] vh_07 seed={seed} {minutes:0.00} min, specials {specials}, staggers {staggers}, landed {sim.Boss.specialsLanded}, heart {sim.HeartrootHealth}");
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

        /// <summary>
        /// Tuning aid (explicit: run with -testFilter BalanceReport): plays every encounter on 8 seeds and logs the
        /// outcome, length, breaches by enemy and the cards the bot used.
        /// </summary>
        [Test, Explicit]
        public void BalanceReport()
        {
            var sb = new System.Text.StringBuilder("[BalanceReport]\n");
            foreach (EncounterDefinition e in Db().encounters)
            {
                int wins = 0;
                var breaches = new Dictionary<string, int>();
                var planted = new Dictionary<string, int>();
                var evolved = new Dictionary<string, int>();
                var readyTimes = new List<float>();
                float minutesSum = 0f, heartSum = 0f, minMin = float.MaxValue, maxMin = 0f;
                int specials = 0, staggers = 0, uprooted = 0;
                foreach (int seed in new[] { 1, 7, 42, 1234, 99999, 5, 77, 314 })
                {
                    BattleSimulation sim = Play(e, seed, true, out float minutes, s =>
                    {
                        s.EnemyBreached += en => { breaches.TryGetValue(en.Definition.id, out int n); breaches[en.Definition.id] = n + 1; };
                        s.DefenderPlaced += d => { planted.TryGetValue(d.Definition.id, out int n); planted[d.Definition.id] = n + 1; };
                        s.DefenderEvolved += (d, ev) => { evolved.TryGetValue(ev.id, out int n); evolved[ev.id] = n + 1; };
                        s.DefenderEvolutionReady += d => readyTimes.Add(d.age);
                        s.BossWeakPointsExposed += b => specials++;
                        s.BossStaggered += b => staggers++;
                        s.DefenderUprooted += (d, r) => uprooted++;
                    });
                    if (sim.State == BattleState.Won) wins++;
                    minutesSum += minutes;
                    minMin = Mathf.Min(minMin, minutes);
                    maxMin = Mathf.Max(maxMin, minutes);
                    heartSum += sim.HeartrootHealth;
                }
                sb.Append($"{e.id}: won {wins}/8 avgMin {minutesSum / 8f:0.0} ({minMin:0.0}-{maxMin:0.0}) avgHeart {heartSum / 8f:0.0} | ");
                if (e.boss != null) sb.Append($"specials {specials} staggers {staggers} uprooted {uprooted} | ");
                sb.Append("breaches ");
                foreach (var kv in breaches) sb.Append($"{kv.Key}={kv.Value} ");
                sb.Append("| planted ");
                foreach (var kv in planted) sb.Append($"{kv.Key}={kv.Value} ");
                readyTimes.Sort();
                sb.Append($"| evolved ");
                foreach (var kv in evolved) sb.Append($"{kv.Key}={kv.Value} ");
                if (readyTimes.Count > 0)
                    sb.Append($"| ready after planting: n={readyTimes.Count} p25={readyTimes[readyTimes.Count / 4]:0}s median={readyTimes[readyTimes.Count / 2]:0}s p75={readyTimes[readyTimes.Count * 3 / 4]:0}s");
                sb.Append('\n');
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>Simple player: reacts every 0.5 s, collects pickups after ~1.5 s, plants with the shared ScriptedPlayer policy.</summary>
        private static BattleSimulation Play(EncounterDefinition e, int seed, bool defend, out float minutes, System.Action<BattleSimulation> hook = null)
        {
            var sim = new BattleSimulation(e, seed, BattleOptions.Default);
            hook?.Invoke(sim);
            var player = new ScriptedPlayer();
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
                if (player.DecideEvolution(sim, out DefenderRuntime evolving, out int option))
                    Assert.AreEqual(EvolveResult.Ok, sim.TryEvolve(evolving, option), "ScriptedPlayer chose an invalid evolution");
                if (player.Decide(sim, out int slot, out GridCoord cell))
                    Assert.AreEqual(DeployResult.Ok, sim.TryDeploy(slot, cell), "ScriptedPlayer chose an invalid deployment");
            }
            minutes = sim.Stats.elapsed / 60f;
            return sim;
        }
    }
}
