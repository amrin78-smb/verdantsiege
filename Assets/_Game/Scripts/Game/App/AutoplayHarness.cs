using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using VerdantSiege.Battle;
using VerdantSiege.Data;
using VerdantSiege.Grid;
using VerdantSiege.UI;

namespace VerdantSiege.App
{
    /// <summary>
    /// Test-player autoplay (command line: -vs-autoplay). Plays an encounter through the same input paths as a
    /// player (card select + screen-space taps mapped through the camera), takes screenshots, writes a report
    /// and quits with exit code 0 when no exception/error was logged. Inactive in normal play.
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
                }
            }
            Directory.CreateDirectory(_shots);
            Application.logMessageReceived += OnLog;
            Debug.Log($"[VS Autoplay] tag={_tag} encounter={_encounter} strategy={_strategy} speed={_speed} {Screen.width}x{Screen.height}");
            Invoke(nameof(MenuShot), 2.5f);
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

            if (sim.State != BattleState.Running)
            {
                if (_resultTimer < 0f) _resultTimer = 3f;
                _resultTimer -= dt;
                if (_resultTimer <= 0f && !_resultLogged) Finish(sim);
                return;
            }

            // Pause/resume round-trip once (time scale and audio must recover).
            if (!_pausedTested && t > 20f)
            {
                _pausedTested = true;
                _battle.SetPaused(true);
                Shot("pause_menu");
                float before = sim.Stats.elapsed;
                _battle.Invoke(nameof(BattleController.Deselect), 0f);
                _battle.SetPaused(false);
                if (Mathf.Abs(Time.timeScale - _battle.Speed) > 0.01f) Debug.LogError("[VS Autoplay] time scale not restored after pause");
                if (sim.Stats.elapsed != before) Debug.LogError("[VS Autoplay] simulation advanced while paused");
            }

            _thinkTimer -= dt;
            if (_thinkTimer > 0f) return;
            _thinkTimer = 0.35f;
            if (_strategy == "none") return;
            CollectPickups(sim);
            Plant(sim);
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

        private void Plant(BattleSimulation sim)
        {
            int glow = Slot(sim, "glowcap"), shoot = Slot(sim, "sunspore"), wall = Slot(sim, "bulwark_bloom");
            int lanes = sim.Lanes;
            // Emergency: a lane with an enemy on the field but no shooter.
            for (int l = 0; l < lanes; l++)
            {
                bool threat = false;
                foreach (EnemyRuntime e in sim.Enemies) if (e.alive && e.lane == l && e.x < sim.Columns) threat = true;
                if (threat && !LaneHas(sim, l, "sunspore") && TryPlace(sim, shoot, l, 1, 2, 0)) return;
            }
            // Economy then firepower then walls.
            int glowCount = Count(sim, "glowcap");
            if (glowCount < 2 || (glowCount < lanes && sim.Waves.CurrentWave < 3))
            {
                for (int k = 0; k < lanes; k++) { int l = (2 + k) % lanes; if (!LaneHas(sim, l, "glowcap") && TryPlace(sim, glow, l, 0)) return; }
            }
            for (int k = 0; k < lanes; k++) { int l = (2 + k) % lanes; if (!LaneHas(sim, l, "sunspore") && TryPlace(sim, shoot, l, 1, 2)) return; }
            foreach (EnemyRuntime e in sim.Enemies)
                if (e.alive && e.Definition.armorHealth > 0f && !LaneHas(sim, e.lane, "bulwark_bloom") && TryPlace(sim, wall, e.lane, 5, 4, 6)) return;
            for (int k = 0; k < lanes; k++) { int l = (2 + k) % lanes; if (CountLane(sim, l, "sunspore") < 2 && TryPlace(sim, shoot, l, 2, 3)) return; }
            for (int k = 0; k < lanes; k++) { int l = (2 + k) % lanes; if (CountLane(sim, l, "sunspore") < 3 && TryPlace(sim, shoot, l, 3, 4)) return; }
            for (int k = 0; k < lanes; k++) { int l = (2 + k) % lanes; if (!LaneHas(sim, l, "bulwark_bloom") && TryPlace(sim, wall, l, 6, 5)) return; }
        }

        private static bool LaneHas(BattleSimulation sim, int lane, string id) => CountLane(sim, lane, id) > 0;

        private static int CountLane(BattleSimulation sim, int lane, string id)
        {
            int n = 0;
            foreach (DefenderRuntime d in sim.Defenders) if (d.alive && d.Coord.lane == lane && d.Definition.id == id) n++;
            return n;
        }

        private static int Count(BattleSimulation sim, string id)
        {
            int n = 0;
            foreach (DefenderRuntime d in sim.Defenders) if (d.alive && d.Definition.id == id) n++;
            return n;
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
            _resultLogged = true;
            Shot("result");
            float avg = _frames > 0 ? _frameTimeSum / _frames : 0f;
            var sb = new StringBuilder();
            sb.AppendLine($"Verdant Siege autoplay report  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"tag={_tag} resolution={Screen.width}x{Screen.height} inset={ScreenSafe.SimulatedInsetPx} encounter={_encounter} strategy={_strategy} speed={_speed}");
            sb.AppendLine($"quality={QualityManager.Names[QualityManager.CurrentLevel]} gpu={SystemInfo.graphicsDeviceName} api={SystemInfo.graphicsDeviceType}");
            sb.AppendLine($"frames={_frames} avgFps={(avg > 0 ? 1f / avg : 0):0.0} worstFrame={_worstFrame * 1000f:0}ms (software GL: not representative of phones)");
            BattleStats st = sim.Stats;
            sb.AppendLine($"stats: elapsed={st.elapsed:0.0}s defeated={st.enemiesDefeated} planted={st.defendersPlanted} lost={st.defendersLost} vitality={st.vitalityCollected} breaches={st.breaches}");
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
