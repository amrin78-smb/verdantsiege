using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VerdantSiege.Battle;
using VerdantSiege.Data;
using VerdantSiege.Grid;
using VerdantSiege.Progression;
using VerdantSiege.Save;
using VerdantSiege.UI;

namespace VerdantSiege.App
{
    /// <summary>
    /// Test-player autoplay (command line: -vs-autoplay). Plays an encounter through the same input paths as a
    /// player (card select + screen-space taps mapped through the camera), takes screenshots, writes a report
    /// and quits with exit code 0 when no exception/error was logged. Inactive in normal play.
    /// Modes: direct (default; -vs-encounter, authored roster, no research/mastery) or -vs-flow campaign (Milestone 4:
    /// Sanctuary -> Conservatory (buys research) -> World Map -> briefing -> Loadout -> battle -> rewards -> World Map ->
    /// Sanctuary, all through the menu buttons, with the player's real save). -vs-expect-progress fails the run unless
    /// the save loaded at start already holds progress (restart persistence check).
    /// </summary>
    public sealed class AutoplayHarness : MonoBehaviour
    {
        private string _shots = "Logs/AutoPilot";
        private string _tag = "run";
        private string _encounter = "vh_01";
        private string _strategy = "good";
        private float _speed = 2f;
        private int _errors;
        private readonly List<string> _errorLines = new List<string>();
        private BattleController _battle;
        private float _thinkTimer;
        private float _frameTimeSum;
        private int _frames;
        private float _worstFrame;
        private readonly HashSet<string> _shotsTaken = new HashSet<string>();
        private int _planStep;
        private bool _resultLogged;
        private float _resultTimer = -1f;
        private bool _pausedTested;
        private float _bossDeadTimer, _crestTimer, _victoryHold;
        private readonly ScriptedPlayer _player = new ScriptedPlayer();
        private string _flow = "direct";
        private bool _expectProgress;
        private int _grantSeeds, _grantEssence;
        private bool _unlockAll;
        private bool _battleFinished;
        private BattleSimulation _lastSim;
        private readonly StringBuilder _flowLog = new StringBuilder();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-vs-autoplay") < 0) return;
            var go = new GameObject("[Autoplay]");
            DontDestroyOnLoad(go);
            go.AddComponent<AutoplayHarness>().Parse(args);
        }

        private void Parse(string[] a)
        {
            for (int i = 0; i < a.Length - 1; i++)
            {
                switch (a[i])
                {
                    case "-vs-shots": _shots = a[i + 1]; break;
                    case "-vs-tag": _tag = a[i + 1]; break;
                    case "-vs-encounter": _encounter = a[i + 1]; break;
                    case "-vs-strategy": _strategy = a[i + 1]; break;
                    case "-vs-speed": float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _speed); break;
                    case "-vs-safe-inset": float.TryParse(a[i + 1], out ScreenSafe.SimulatedInsetPx); break;
                    case "-vs-flow": _flow = a[i + 1]; break;
                    case "-vs-grant-seeds": int.TryParse(a[i + 1], out _grantSeeds); break;
                    case "-vs-grant-essence": int.TryParse(a[i + 1], out _grantEssence); break;
                }
            }
            _expectProgress = Array.IndexOf(a, "-vs-expect-progress") >= 0;
            _unlockAll = Array.IndexOf(a, "-vs-unlock-all") >= 0;
            Directory.CreateDirectory(_shots);
            Application.logMessageReceived += OnLog;
            Debug.Log($"[VS Autoplay] tag={_tag} flow={_flow} encounter={_encounter} strategy={_strategy} speed={_speed} {Screen.width}x{Screen.height}");
            if (_flow == "campaign") StartCoroutine(CampaignFlow());
            else
            {
                AppRoot.Ensure().ForceNeutralLoadout = true; // direct mode: the encounter's authored roster, no research or mastery
                Invoke(nameof(MenuShot), 2.5f);
            }
        }

        // ================================================================ campaign flow (Milestone 4)
        private static string SaveSummary(SaveData d)
        {
            int done = 0;
            foreach (EncounterRecord r in d.encounters) if (r.completed) done++;
            int masteryXp = 0;
            foreach (MasteryRecord m in d.mastery) masteryXp += m.xp;
            return $"v{d.version} seeds={d.seeds} essence={d.essence} completed={done} research=[{string.Join(",", d.research)}] " +
                   $"defenders={d.unlockedDefenders.Count} masteryXp={masteryXp} lastLoadout=[{string.Join(",", d.lastLoadout)}] sanctuaryTierSeen={d.sanctuaryTierSeen}";
        }

        private void Log(string line)
        {
            _flowLog.AppendLine(line);
            Debug.Log("[VS Autoplay] " + line);
        }

        private static IEnumerator Wait(float seconds) { yield return new WaitForSecondsRealtime(seconds); }

        /// <summary>A screenshot is taken at the end of the frame: wait for it before changing the UI.</summary>
        private static IEnumerator Settle() { yield return new WaitForEndOfFrame(); yield return null; yield return null; }

        private static void Click(string name)
        {
            foreach (Button b in FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.name == name && b.isActiveAndEnabled && b.interactable) { b.onClick.Invoke(); return; }
            Debug.LogError("[VS Autoplay] no clickable button named " + name);
        }

        private IEnumerator CampaignFlow()
        {
            AppRoot app = AppRoot.Ensure();
            while (MainMenuController.Instance == null) yield return null;
            yield return Wait(2f);
            SaveData save = app.Save.Data;
            Log("save at start: " + SaveSummary(save));
            if (_expectProgress)
            {
                bool ok = save.IsCompleted(app.Config.firstEncounterId) && save.mastery.Count > 0 && save.lastLoadout.Count > 0;
                if (!ok) Debug.LogError("[VS Autoplay] restart persistence: expected progress from the previous run, found " + SaveSummary(save));
                else Log("restart persistence: OK (progress survived the relaunch)");
            }
            if (_grantSeeds > 0 || _grantEssence > 0)
            {
                // Screenshot aid only (logged): lets the research screen show a purchase on a fresh save.
                save.seeds += _grantSeeds;
                save.essence += _grantEssence;
                Log($"test grant: +{_grantSeeds} Seeds +{_grantEssence} Essence");
            }
            if (_unlockAll)
            {
                // Screenshot aid only (logged): a full collection with varied mastery so the readability pass can
                // judge populated grids and the scrolled states.
                int k = 0;
                foreach (DefenderDefinition d in app.Config.content.defenders)
                {
                    save.Unlock(d.id);
                    save.AddMasteryXp(d.id, (k++ % 4) * 60);
                }
                Log($"test unlock: all {app.Config.content.defenders.Count} defenders, varied mastery");
            }
            MainMenuController hub = MainMenuController.Instance;
            hub.ShowSanctuary();
            yield return Wait(2.5f); // restoration celebration (if any) plays
            Shot(_expectProgress ? "sanctuary_restart" : "sanctuary");
            yield return Settle(); // capture before the next UI change

            // Conservatory: research and mastery.
            Click("Conservatory");
            yield return Wait(1.2f);
            Shot("research");
            yield return Settle(); // capture before the next UI change
            var cons = hub.CurrentScreen as ConservatoryScreen;
            if (cons != null && app.Progression.Campaign != null)
            {
                foreach (ResearchDefinition r in app.Progression.Campaign.research)
                    if (ResearchRules.StateOf(save, r) == ResearchState.Available)
                    {
                        Log($"research {r.id}: {cons.Buy(r)}");
                        break;
                    }
                yield return Wait(1.2f);
                Shot("research_bought");
                yield return Settle(); // capture before the next UI change
                yield return ScrollToEnd(cons.gameObject, "research_scrolled");
                cons.Show(ConservatoryScreen.Tab.Defenders);
                yield return Wait(1f);
                Shot("mastery");
                yield return Settle(); // capture before the next UI change
                yield return ScrollToEnd(cons.gameObject, "mastery_scrolled");
            }
            else Debug.LogError("[VS Autoplay] Conservatory did not open");
            Click("Back");
            yield return Wait(0.8f);

            // World map -> briefing -> loadout.
            Click("WorldMap");
            yield return Wait(1.5f);
            Shot("worldmap");
            yield return Settle(); // capture before the next UI change
            var map = hub.CurrentScreen as WorldMapScreen;
            EncounterDefinition next = app.Progression.NextMission() ?? app.Config.content.FindEncounter(_encounter);
            if (map == null || next == null) { Debug.LogError("[VS Autoplay] world map / next mission missing"); yield break; }
            _encounter = next.id;
            map.ShowBrief(next);
            yield return Wait(1f);
            Shot("mission_brief");
            yield return Settle(); // capture before the next UI change
            yield return ScrollToEnd(map.gameObject, "mission_brief_scrolled");
            Click("Prepare");
            yield return Wait(1.2f);
            var loadout = hub.CurrentScreen as LoadoutScreen;
            if (loadout == null) { Debug.LogError("[VS Autoplay] loadout did not open"); yield break; }
            var deck = new List<string>();
            foreach (DefenderDefinition d in loadout.Deck) deck.Add(d.id);
            Log($"loadout {next.id}: slots={app.Progression.DeckSlots(next)} deck=[{string.Join(",", deck)}]");
            Shot("loadout");
            yield return Settle(); // capture before the next UI change
            yield return ScrollToEnd(loadout.gameObject, "loadout_scrolled");
            Click("Begin");

            // Battle (played by Update), then the rewards.
            while (!_battleFinished) yield return null;
            float guard = 60f;
            MissionResultScreen res = null;
            while (guard > 0f && (res == null || !res.Settled))
            {
                BossVictoryScreen v = FindAnyObjectByType<BossVictoryScreen>();
                if (v != null && v.PanelShown) v.Continue();
                res = FindAnyObjectByType<MissionResultScreen>();
                guard -= Time.unscaledDeltaTime;
                yield return null;
            }
            yield return Wait(0.6f);
            Shot("result_rewards");
            yield return Settle(); // capture before the next UI change
            if (res != null) yield return ScrollToEnd(res.gameObject, "result_rewards_scrolled");
            if (res != null) Log("rewards: " + res.Summary);
            Log("save after battle: " + SaveSummary(app.Save.Data));
            Click("WorldMap");
            while (MainMenuController.Instance == null || !(MainMenuController.Instance.CurrentScreen is WorldMapScreen)) yield return null;
            yield return Wait(1.5f);
            Shot("worldmap_after");
            yield return Settle(); // capture before the next UI change
            MainMenuController.Instance.ShowSanctuary();
            yield return Wait(3.5f); // restoration grows in
            Shot("sanctuary_after");
            yield return Settle(); // capture before the next UI change
            Finish(_lastSim);
        }

        /// <summary>
        /// Readability pass: scrolls every scroll list under <paramref name="root"/> to its far end (as a drag would) and
        /// takes a screenshot when at least one of them actually scrolls; then scrolls them back.
        /// </summary>
        private IEnumerator ScrollToEnd(GameObject root, string shot)
        {
            if (root == null) yield break;
            ScrollRect[] lists = root.GetComponentsInChildren<ScrollRect>();
            var moved = new List<string>();
            foreach (ScrollRect sr in lists)
            {
                Rect vp = sr.viewport.rect, ct = sr.content.rect;
                bool scrolls = sr.horizontal ? ct.width > vp.width + 2f : ct.height > vp.height + 2f;
                if (!scrolls) continue;
                if (sr.horizontal) sr.horizontalNormalizedPosition = 1f; else sr.verticalNormalizedPosition = 0f;
                moved.Add(sr.name);
            }
            Log($"{shot}: {lists.Length} scroll lists, scrolled [{string.Join(",", moved)}]");
            if (moved.Count == 0) yield break;
            yield return Wait(0.4f);
            Shot(shot);
            yield return Settle();
            foreach (ScrollRect sr in lists)
                if (sr != null) { if (sr.horizontal) sr.horizontalNormalizedPosition = 0f; else sr.verticalNormalizedPosition = 1f; }
            yield return null;
        }

        private void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            _errors++;
            if (_errorLines.Count < 20) _errorLines.Add(type + ": " + msg);
        }

        private void MenuShot()
        {
            Shot("menu");
            AppRoot.LoadBattle(_encounter);
        }

        private void Shot(string name)
        {
            if (!_shotsTaken.Add(name)) return;
            string path = Path.Combine(_shots, $"{_tag}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[VS Autoplay] screenshot " + path);
        }

        private void Update()
        {
            if (_battleFinished) return;
            if (_battle == null)
            {
                if (SceneManager.GetActiveScene().name != AppRoot.BattleScene) return;
                _battle = FindAnyObjectByType<BattleController>();
                if (_battle == null || _battle.Sim == null) { _battle = null; return; }
                if (_speed > 1.5f) _battle.ToggleSpeed();
            }
            BattleSimulation sim = _battle.Sim;
            float dt = Time.unscaledDeltaTime;
            _frameTimeSum += dt;
            _frames++;
            if (_frames > 30) _worstFrame = Mathf.Max(_worstFrame, dt);

            float t = sim.Stats.elapsed;
            if (t > 4f) Shot("battle_start");
            if (t > 45f) Shot("battle_mid");
            if (sim.Waves.CurrentWave >= 2 && sim.AliveEnemyCount >= 3) Shot("battle_combat");
            if (sim.Waves.CurrentWave == sim.Waves.TotalWaves - 1 && sim.AliveEnemyCount >= 4) Shot("battle_final_wave");
            // Milestone 3 moments.
            BossRuntime boss = sim.Boss;
            if (boss != null)
            {
                if (boss.state == BossState.Idle && boss.phase == 0) Shot("boss_arrived");
                if (boss.state == BossState.Telegraph && boss.targetCells.Count > 0 && boss.stateTimer < 1.2f) Shot("boss_telegraph");
                if (boss.CoreExposed) Shot("boss_core_exposed");
                if (boss.phase >= 1 && boss.state == BossState.Idle) Shot("boss_phase2");
                if (boss.phase >= 2 && boss.state == BossState.Idle) Shot("boss_phase3");
                if (sim.RootBlockedCount > 0) Shot("boss_roots");
                // Boss polish moments.
                if (boss.state == BossState.Rising && boss.stateTimer < boss.stateDuration * 0.4f) Shot("boss_entrance");
                if (boss.state == BossState.Telegraph && boss.WeakPointsExposed && boss.stateDuration - boss.stateTimer > 2f) Shot("boss_weakpoints");
                if (boss.state == BossState.Staggered && boss.stateDuration - boss.stateTimer > 0.7f) Shot("boss_stagger");
                if (boss.state == BossState.Telegraph && boss.currentAttack != null && boss.currentAttack.shape == BossStrikeShape.LaneSweep
                    && !boss.WeakPointsExposed && boss.stateTimer < 1.2f) Shot("boss_sweep");
                if (boss.state == BossState.Telegraph && boss.currentAttack != null && boss.currentAttack.shape == BossStrikeShape.MultiColumn
                    && boss.stateTimer < 1.2f) Shot("boss_frenzy");
                if (boss.state == BossState.Defeated)
                {
                    _bossDeadTimer += dt;
                    if (_bossDeadTimer > 0.6f) Shot("boss_death");
                    if (_bossDeadTimer > 2.2f) Shot("boss_death_collapse");
                }
            }
            foreach (EnemyRuntime e in sim.Enemies)
            {
                if (!e.alive) continue;
                if (e.IsElite && e.x < sim.Columns - 1f) Shot("elite");
                if (e.activity == EnemyActivity.Windup && e.Definition.ability == EnemyAbilityKind.Vault) Shot("vault_crouch");
                if (e.underground && e.x < sim.Columns - 1f) Shot("burrow_trail");
                if (e.IsFlying && e.x < sim.Columns - 2f) Shot("glider");
            }
            if (sim.Clouds.Count > 0) Shot("spore_cloud");
            if (_battle.Hud != null && _battle.Hud.FeedShowingIntel) Shot("feed_intel");
            // Living Evolution moments.
            foreach (DefenderRuntime d in sim.Defenders)
                if (d.EvolutionReady && _battle.Evolution != null && !_shotsTaken.Contains("evolve_ready_badge"))
                {
                    Shot("evolve_ready_badge");
                    Debug.Log("[VS Autoplay] badges: " + _battle.Evolution.Badges.Describe());
                    break;
                }
            if (sim.AuraLinks.Count >= 3) Shot("evolve_aura_links");
            if (_evolvedAt > 0f && Time.unscaledTime - _evolvedAt > 0.25f) Shot("evolve_moment");
            if (_evolvedAt > 0f && Time.unscaledTime - _evolvedAt > 2.5f) Shot("evolve_evolved_look");
            if (_evolutions >= 4) Shot("evolve_many");

            if (sim.State != BattleState.Running)
            {
                _lastSim = sim;
                if (_resultTimer < 0f) _resultTimer = 45f; // software GL can run at 1-3 FPS
                _resultTimer -= dt;
                if (boss != null && boss.state == BossState.Defeated)
                {
                    // Boss victory: crest moment and boss panel, then Continue to the campaign results.
                    BossVictoryScreen v = FindAnyObjectByType<BossVictoryScreen>();
                    if (v != null && v.isActiveAndEnabled && !v.PanelShown) { _crestTimer += dt; if (_crestTimer > 0.9f) Shot("boss_victory_crest"); }
                    if (v != null && v.isActiveAndEnabled && v.PanelShown && (_victoryHold += dt) > 0.8f) Shot("boss_victory_panel");
                    if (v != null && v.isActiveAndEnabled && _victoryHold > 1.6f) v.Continue(); // later frame: the shot is captured first
                }
                MissionResultScreen res = FindAnyObjectByType<MissionResultScreen>();
                if ((res != null && res.Settled) || _resultTimer <= 0f)
                {
                    if (_flow == "campaign") { _battleFinished = true; return; }
                    if (!_resultLogged) Finish(sim);
                }
                return;
            }

            // Pause/resume round-trip once (time scale and audio must recover).
            if (!_pausedTested && t > 20f)
            {
                _pausedTested = true;
                StartCoroutine(PauseRoundTrip(sim));
            }
            if (_pausing) return;

            _thinkTimer -= dt;
            if (_thinkTimer > 0f) return;
            _thinkTimer = 0.35f;
            if (_strategy == "none") return;
            if (Evolve(sim)) return;
            CollectPickups(sim);
            Plant(sim);
        }

        private bool _pausing;

        /// <summary>Pause, let the overlay render (unscaled), screenshot it, check the sim stood still, resume.</summary>
        private IEnumerator PauseRoundTrip(BattleSimulation sim)
        {
            _pausing = true;
            _battle.SetPaused(true);
            float before = sim.Stats.elapsed;
            yield return Wait(0.6f); // the overlay pops in
            Shot("pause_menu");
            yield return Settle();
            if (sim.Stats.elapsed != before) Debug.LogError("[VS Autoplay] simulation advanced while paused");
            _battle.Invoke(nameof(BattleController.Deselect), 0f);
            _battle.SetPaused(false);
            if (Mathf.Abs(Time.timeScale - _battle.Speed) > 0.01f) Debug.LogError("[VS Autoplay] time scale not restored after pause");
            _pausing = false;
        }

        private DefenderRuntime _evolveTarget;
        private bool _chooserShown;
        private int _evolveOption = -1;
        private float _evolvedAt = -1f;
        private int _evolutions;

        /// <summary>
        /// Evolves like a player: taps the ready defender on the battlefield (the chooser opens), then on the next
        /// think taps the option the shared ScriptedPlayer picked. Returns true when it used this think.
        /// </summary>
        private bool Evolve(BattleSimulation sim)
        {
            EvolutionChooser chooser = _battle.Evolution;
            if (chooser == null) return false;
            if (_evolveTarget != null)
            {
                DefenderRuntime d = _evolveTarget;
                if (!chooser.IsOpen || chooser.State.Target != d) { _evolveTarget = null; return false; }
                if (!_chooserShown)
                {
                    // Leave the card on screen for one think (a player reads it), then pick.
                    _chooserShown = true;
                    Shot("evolve_chooser");
                    Debug.Log("[VS Autoplay] badges: " + chooser.Badges.Describe());
                    return true;
                }
                _evolveTarget = null;
                EvolveResult r = chooser.Choose(_evolveOption);
                if (r == EvolveResult.Ok)
                {
                    _evolutions++;
                    if (_evolvedAt < 0f) _evolvedAt = Time.unscaledTime;
                    Debug.Log($"[VS Autoplay] evolved {d.Definition.id} -> {d.evolution.id} at t={sim.Stats.elapsed:0.0}s (xp threshold {d.Definition.evolutionXp})");
                }
                else chooser.State.Close();
                return true;
            }
            if (_battle.SelectedSlot >= 0 || !_player.DecideEvolution(sim, out DefenderRuntime target, out int option)) return false;
            _battle.SimulateTap(_battle.CoordToScreen(target.Coord)); // the same tap a player makes on the plant
            if (chooser.IsOpen && chooser.State.Target == target)
            {
                _evolveTarget = target;
                _evolveOption = option;
                return true;
            }
            chooser.State.Close();
            return false; // the tap landed on a pickup or a neighbour: try again next think
        }

        private void CollectPickups(BattleSimulation sim)
        {
            foreach (PickupRuntime p in sim.Pickups)
            {
                if (!p.alive || p.age < 1.2f) continue;
                if (_battle.View.PickupViews.TryGetValue(p.Id, out var v))
                {
                    Vector3 sp = Camera.main.WorldToScreenPoint(v.transform.position);
                    _battle.SimulateTap(sp);
                    return; // one tap per think, like a human
                }
            }
        }

        private int Slot(BattleSimulation sim, string id)
        {
            for (int i = 0; i < sim.Deck.Count; i++) if (sim.Deck[i].Definition.id == id) return i;
            return -1;
        }

        /// <summary>Same planting policy as the balance tests (ScriptedPlayer), executed through real card + cell taps.</summary>
        private void Plant(BattleSimulation sim)
        {
            if (_player.Decide(sim, out int slot, out GridCoord cell)) TryPlace(sim, slot, cell.lane, cell.column);
        }

        private bool TryPlace(BattleSimulation sim, int slot, int lane, params int[] columns)
        {
            if (slot < 0) return false;
            DeckSlot s = sim.Deck[slot];
            if (!s.Ready || !sim.Vitality.CanAfford(s.Definition.cost)) return false;
            foreach (int c in columns)
            {
                var coord = new GridCoord(lane, c);
                if (sim.CanDeploy(slot, coord) != DeployResult.Ok) continue;
                if (!_battle.TrySelect(slot, false)) return false;
                // Tap the cell's screen position (exercises camera mapping and the tap path).
                _battle.SimulateTap(_battle.CoordToScreen(coord));
                bool ok = sim.DefenderAt(coord) != null;
                if (!ok) { Debug.LogError($"[VS Autoplay] tap at {coord} did not plant (screen mapping?)"); _battle.Deselect(); }
                _planStep++;
                return ok;
            }
            return false;
        }

        private void Finish(BattleSimulation sim)
        {
            if (_resultLogged) return;
            _resultLogged = true;
            if (_flow != "campaign") Shot("result"); // campaign flow: result_rewards was taken already
            if (sim == null) { Debug.LogError("[VS Autoplay] no battle was played"); Invoke(nameof(Quit), 1.5f); return; }
            float avg = _frames > 0 ? _frameTimeSum / _frames : 0f;
            var sb = new StringBuilder();
            sb.AppendLine($"Verdant Siege autoplay report  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"tag={_tag} flow={_flow} resolution={Screen.width}x{Screen.height} inset={ScreenSafe.SimulatedInsetPx} encounter={_encounter} strategy={_strategy} speed={_speed}");
            sb.AppendLine($"quality={QualityManager.Names[QualityManager.CurrentLevel]} gpu={SystemInfo.graphicsDeviceName} api={SystemInfo.graphicsDeviceType}");
            sb.AppendLine($"frames={_frames} avgFps={(avg > 0 ? 1f / avg : 0):0.0} worstFrame={_worstFrame * 1000f:0}ms (software GL: not representative of phones)");
            BattleStats st = sim.Stats;
            sb.AppendLine($"stats: elapsed={st.elapsed:0.0}s defeated={st.enemiesDefeated} planted={st.defendersPlanted} lost={st.defendersLost} vitality={st.vitalityCollected} breaches={st.breaches}");
            sb.AppendLine($"evolutions={_evolutions}");
            MissionResultScreen mr = FindAnyObjectByType<MissionResultScreen>();
            if (mr != null) sb.AppendLine("rewards: " + mr.Summary);
            sb.AppendLine("save: " + SaveSummary(AppRoot.Ensure().Save.Data));
            if (_flowLog.Length > 0) sb.Append("flow:\n").Append(_flowLog);
            if (sim.Boss != null)
                sb.AppendLine($"boss: state={sim.Boss.state} phase={sim.Boss.phase + 1}/{sim.Boss.PhaseCount} staggers={sim.Boss.staggerCount} specialsLanded={sim.Boss.specialsLanded} " +
                              $"victoryScreen={(FindAnyObjectByType<BossVictoryScreen>() != null)} rewards=[{(FindAnyObjectByType<BossVictoryScreen>() != null ? FindAnyObjectByType<BossVictoryScreen>().RewardText : "")}] essence={AppRoot.Ensure().Save.Data.essence}");
            sb.AppendLine($"errors={_errors}");
            foreach (string e in _errorLines) sb.AppendLine("  " + e);
            sb.AppendLine($"RESULT: {sim.State} heartroot={sim.HeartrootHealth}/{sim.HeartrootMaxHealth}");
            sb.AppendLine(_errors == 0 ? "AUTOPLAY: PASS" : "AUTOPLAY: FAIL");
            File.WriteAllText(Path.Combine(_shots, $"{_tag}_autoplay_report.txt"), sb.ToString());
            Debug.Log("[VS Autoplay] " + sb);
            Invoke(nameof(Quit), 1.5f);
        }

        private void Quit() => Application.Quit(_errors == 0 ? 0 : 3);
    }
}
