using UnityEngine;
using UnityEngine.InputSystem;
using VerdantSiege.App;
using VerdantSiege.Audio;
using VerdantSiege.Battle.View;
using VerdantSiege.Data;
using VerdantSiege.Grid;
using VerdantSiege.Save;
using VerdantSiege.UI;
using VerdantSiege.Visual;

namespace VerdantSiege.Battle
{
    /// <summary>
    /// Composition root of the Battle scene: builds the simulation for the chosen encounter, the views, the
    /// HUD and input handling, and owns pause/speed/restart/results. Gameplay rules live in BattleSimulation.
    /// </summary>
    public sealed class BattleController : MonoBehaviour
    {
        [Header("Scene references (assigned by the scene builder)")]
        public Camera battleCamera;
        public HeartrootView heartroot;
        public Transform battlefieldRoot;
        [Tooltip("Encounter used when the scene is opened directly in the editor.")]
        public EncounterDefinition fallbackEncounter;

        public BattleSimulation Sim { get; private set; }
        public BattleLayout Layout { get; private set; }
        public BattleHud Hud { get; private set; }
        public BattleView View { get; private set; }
        public bool Paused { get; private set; }
        public float Speed { get; private set; } = 1f;
        public int SelectedSlot => _selected;

        private AppRoot _app;
        private VisualLibrary _lib;
        private GridOverlay _overlay;
        private VfxSystem _vfx;
        private int _selected = -1;
        private bool _dragging;
        private float _resultDelay = -1f;
        private bool _resultShown;
        private bool _firstPickupHint;
        private EncounterDefinition _encounter;

        private void Awake()
        {
            _app = AppRoot.Ensure();
            Time.timeScale = 1f;
            GameConfig cfg = _app.Config;
            _lib = cfg != null ? cfg.visuals : null;
            UIKit.Lib = _lib;
            _encounter = cfg != null && cfg.content != null ? cfg.content.FindEncounter(_app.PendingEncounterId) : null;
            if (_encounter == null) _encounter = fallbackEncounter;
            if (_encounter == null && cfg != null && cfg.content != null && cfg.content.encounters.Count > 0) _encounter = cfg.content.encounters[0];
            if (battleCamera == null) battleCamera = Camera.main;
        }

        private void Start()
        {
            if (_encounter == null || _lib == null)
            {
                Debug.LogError("[VS Battle] No encounter or visual library configured.");
                enabled = false;
                return;
            }
            SettingsData settings = _app.Settings;
            int seed = _encounter.fixedSeed != 0 ? _encounter.fixedSeed : System.Environment.TickCount;
            var options = BattleOptions.Default;
            options.autoCollect = settings.autoCollect;
            Sim = new BattleSimulation(_encounter, seed, options);
            Layout = new BattleLayout(Sim.Lanes, Sim.Columns, 1f, 1f, Vector3.zero);
            Debug.Log($"[VS Battle] {_encounter.id} seed={seed} lanes={Sim.Lanes} cols={Sim.Columns} waves={_encounter.waves.Count}");

            BuildGround();
            _vfx = VfxSystem.Create(transform, _lib, QualityManager.CurrentLevel, settings.reducedFlashes);
            if (heartroot != null) heartroot.Init(_vfx);

            var viewGo = new GameObject("BattleView");
            viewGo.transform.SetParent(transform, false);
            View = viewGo.AddComponent<BattleView>();
            View.Init(Sim, Layout, _lib, _vfx, heartroot, _app.Audio);
            View.PickupCollectedAt += (world, amount) =>
            {
                Vector3 sp = battleCamera.WorldToScreenPoint(world);
                Hud.FlyVitality(sp);
            };

            var overlayGo = new GameObject("GridOverlay");
            overlayGo.transform.SetParent(transform, false);
            _overlay = overlayGo.AddComponent<GridOverlay>();
            _overlay.Init(Sim, Layout, _lib);

            var hudGo = new GameObject("HUD");
            hudGo.transform.SetParent(transform, false);
            Hud = hudGo.AddComponent<BattleHud>();
            Hud.Build(Sim, settings.autoCollect, settings.musicVolume, settings.sfxVolume, Debug.isDebugBuild);
            WireHud();
            WireSim();
            FitCamera();

            if (_app.Audio != null) _app.Audio.PlayMusic(_app.Config.audio != null ? _app.Config.audio.battleMusic : null);
            Hud.ShowBanner(_encounter.displayName, _encounter.briefing, UIPalette.Parchment, 3.2f);
            if (!settings.tutorialSeen) Invoke(nameof(FirstHint), 3.4f);
        }

        private void FirstHint() => Hud.ShowToast("Tap a card, then tap the lawn to plant it", 4f);

        private void BuildGround()
        {
            var ground = new GameObject("Ground");
            ground.transform.SetParent(battlefieldRoot != null ? battlefieldRoot : transform, false);
            for (int l = 0; l < Layout.Lanes; l++)
            for (int c = 0; c < Layout.Columns; c++)
            {
                GameObject prefab = (l + c) % 2 == 0 ? _lib.groundTileA : _lib.groundTileB;
                if (prefab == null) continue;
                GameObject tile = Instantiate(prefab, Layout.CellCenter(new GridCoord(l, c)), Quaternion.identity, ground.transform);
                tile.name = $"Tile_{l}_{c}";
                foreach (Renderer r in tile.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            StaticBatchingUtility.Combine(ground);
        }

        private void FitCamera()
        {
            if (battleCamera == null) return;
            var rig = battleCamera.GetComponent<BattleCameraRig>();
            if (rig == null) rig = battleCamera.gameObject.AddComponent<BattleCameraRig>();
            Bounds b = Layout.FieldBounds;
            // Include the Heartroot (left), the enemy entry (right) and unit height.
            b.Encapsulate(new Vector3(-0.9f, 0f, b.center.z));
            b.Encapsulate(new Vector3(Layout.Columns + 0.9f, 0f, b.center.z));
            b.Encapsulate(new Vector3(b.center.x, 1.0f, Layout.Lanes * Layout.LaneDepth + 0.15f));
            b.Encapsulate(new Vector3(b.center.x, 0f, -0.1f));
            // HUD-free viewport: left rail and top bar are excluded (reference 1920x1080 scaled by height).
            float scale = Screen.height / 1080f;
            float left = (BattleHud.RailWidth * scale) / Mathf.Max(1, Screen.width);
            float top = (BattleHud.TopBarHeight * scale) / Mathf.Max(1, Screen.height);
            rig.Fit(b, new Rect(left + 0.005f, 0.01f, 1f - left - 0.015f, 1f - top - 0.02f));
        }

        // ================================================================ wiring
        private void WireSim()
        {
            Sim.Vitality.Changed += (amount, delta) => Hud.SetVitality(amount, delta);
            Sim.HeartrootDamaged += (remaining, dmg) =>
            {
                Hud.SetHeartroot(remaining, Sim.HeartrootMaxHealth);
                if (remaining > 0) Hud.ShowToast("The Heartroot is wounded!");
            };
            Sim.WaveStarted += OnWaveStarted;
            Sim.StateChanged += s => { if (s != BattleState.Running) _resultDelay = 1.4f; };
            Sim.PickupSpawned += p =>
            {
                if (_firstPickupHint || _app.Settings.tutorialSeen || _app.Settings.autoCollect) return;
                _firstPickupHint = true;
                Hud.ShowToast("Tap glowing Sunmotes to gather Vitality", 3.5f);
            };
        }

        private void OnWaveStarted(int index, bool major)
        {
            bool last = index == Sim.Waves.TotalWaves - 1;
            WaveDefinition w = Sim.Encounter.waves[index];
            if (last) Hud.ShowBanner("Final Wave", "Hold the line!", UIPalette.Danger);
            else if (major) Hud.ShowBanner("A Great Wave Rises", w != null ? w.label : "", UIPalette.Blight);
            else if (index == 0) Hud.ShowBanner("The Blight Approaches", "Defend the Heartroot", UIPalette.Blight, 2f);
            if (_app.Audio != null) _app.Audio.Play(major || last ? Sfx.MajorWave : Sfx.WaveStart);
        }

        private void WireHud()
        {
            Hud.CardClicked += OnCardClicked;
            Hud.CardDragBegan += (slot, pos) => { if (TrySelect(slot, true)) _dragging = true; };
            Hud.CardDragged += (slot, pos) => { if (_dragging) _overlay.SetHover(ScreenToCoord(pos)); };
            Hud.CardDragEnded += (slot, pos) =>
            {
                if (!_dragging) return;
                _dragging = false;
                GridCoord c = ScreenToCoord(pos);
                if (Layout.InBounds(c)) TryPlant(c);
                else Deselect();
            };
            Hud.TapSurface.Tapped += OnBattlefieldTap;
            Hud.TapSurface.Swiped += pos => { if (_selected < 0 && !Paused) View.CollectAlong(battleCamera, pos, PickupRadiusPx * 0.8f); };
            Hud.TapSurface.PointerMoved += pos => { if (_selected >= 0) _overlay.SetHover(ScreenToCoord(pos)); };
            Hud.PauseClicked += () => SetPaused(true);
            Hud.ResumeClicked += () => SetPaused(false);
            Hud.RestartClicked += Restart;
            Hud.MenuClicked += () => { SaveSettings(); AppRoot.LoadMainMenu(); };
            Hud.SpeedClicked += ToggleSpeed;
            Hud.UndoClicked += () =>
            {
                if (Hud.Inspected != null && Sim.TryUndo(Hud.Inspected)) Hud.ShowInspect(null);
            };
            Hud.InspectClosed += () => Hud.ShowInspect(null);
            Hud.AutoCollectChanged += v =>
            {
                _app.Settings.autoCollect = v;
                BattleOptions o = Sim.Options;
                o.autoCollect = v;
                Sim.Options = o;
            };
            Hud.VolumesChanged += (music, sfx) =>
            {
                _app.Settings.musicVolume = music;
                _app.Settings.sfxVolume = sfx;
                _app.ApplySettings();
            };
            Hud.DebugCommand += OnDebugCommand;
        }

        // ================================================================ input
        private float PickupRadiusPx => Mathf.Max(70f, Screen.height * 0.075f);

        private void OnCardClicked(int slot)
        {
            if (_selected == slot) { Deselect(); return; }
            TrySelect(slot, false);
        }

        /// <summary>Selects a card if it can be used; otherwise explains why.</summary>
        public bool TrySelect(int slot, bool fromDrag)
        {
            if (Sim.State != BattleState.Running || Paused) return false;
            DeckSlot s = Sim.Deck[slot];
            if (!s.Ready)
            {
                Hud.DenyCard(slot);
                Hud.ShowToast(DeployResultText.Describe(DeployResult.OnCooldown));
                Play(Sfx.CardDenied);
                return false;
            }
            if (!Sim.Vitality.CanAfford(s.Definition.cost))
            {
                Hud.DenyCard(slot);
                Hud.DenyVitality();
                Hud.ShowToast(DeployResultText.Describe(DeployResult.NotEnoughVitality));
                Play(Sfx.CardDenied);
                return false;
            }
            _selected = slot;
            Hud.SetSelectedCard(slot);
            Hud.ShowInspect(null);
            _overlay.SetSelection(slot);
            Play(Sfx.CardSelect);
            return true;
        }

        public void Deselect()
        {
            _selected = -1;
            _dragging = false;
            Hud.SetSelectedCard(-1);
            _overlay.SetSelection(-1);
            _overlay.SetHover(GridCoord.Invalid);
        }

        /// <summary>Same path as a finger tap on the battlefield (used by the autoplay harness).</summary>
        public void SimulateTap(Vector2 screen) => OnBattlefieldTap(screen);

        private void OnBattlefieldTap(Vector2 screen)
        {
            if (Paused || Sim.State != BattleState.Running) return;
            // 1) pickups take priority (they float above the lawn)
            int pickup = View.PickupAtScreen(battleCamera, screen, PickupRadiusPx);
            if (pickup >= 0 && _selected < 0) { Sim.CollectPickup(pickup); return; }
            GridCoord c = ScreenToCoord(screen);
            if (_selected >= 0)
            {
                if (!Layout.InBounds(c)) { Deselect(); return; }
                TryPlant(c);
                return;
            }
            if (pickup >= 0) { Sim.CollectPickup(pickup); return; }
            // 2) inspect a defender (nearest on screen, since tall models overlap the lane behind)
            DefenderRuntime d = DefenderAtScreen(screen);
            Hud.ShowInspect(d);
        }

        /// <summary>Attempt to plant the selected card at a cell (also used by the autoplay test harness).</summary>
        public DeployResult TryPlant(GridCoord c)
        {
            if (_selected < 0) return DeployResult.InvalidCard;
            int slot = _selected;
            DeployResult r = Sim.TryDeploy(slot, c);
            if (r == DeployResult.Ok)
            {
                Deselect();
                if (!_app.Settings.tutorialSeen && Sim.Stats.defendersPlanted == 1) Hud.ShowToast("Tap a plant to inspect it or undo for a few seconds", 3f);
                return r;
            }
            _overlay.FlashInvalid(c);
            Hud.ShowToast(DeployResultText.Describe(r));
            Play(Sfx.PlantInvalid);
            if (r == DeployResult.NotEnoughVitality) { Hud.DenyVitality(); Deselect(); }
            else if (r == DeployResult.OnCooldown) { Hud.DenyCard(slot); Deselect(); }
            return r;
        }

        public GridCoord ScreenToCoord(Vector2 screen)
        {
            Ray ray = battleCamera.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float t)) return GridCoord.Invalid;
            return Layout.WorldToCoord(ray.GetPoint(t));
        }

        public Vector2 CoordToScreen(GridCoord c) => battleCamera.WorldToScreenPoint(Layout.CellCenter(c));

        private DefenderRuntime DefenderAtScreen(Vector2 screen)
        {
            DefenderRuntime best = null;
            float bestD = Mathf.Pow(Screen.height * 0.07f, 2f);
            foreach (DefenderRuntime d in Sim.Defenders)
            {
                if (!d.alive) continue;
                Vector3 sp = battleCamera.WorldToScreenPoint(Layout.CellCenter(d.Coord) + Vector3.up * 0.35f);
                float dist = ((Vector2)sp - screen).sqrMagnitude;
                if (dist < bestD) { bestD = dist; best = d; }
            }
            if (best == null) best = Sim.DefenderAt(ScreenToCoord(screen));
            return best;
        }

        // ================================================================ flow
        private void Update()
        {
            if (Sim == null) return;
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (_resultShown) { }
                else if (_selected >= 0) Deselect();
                else SetPaused(!Paused);
            }
            if (!Paused) Sim.Advance(Time.deltaTime);

            if (_resultDelay > 0f)
            {
                _resultDelay -= Time.unscaledDeltaTime;
                if (_resultDelay <= 0f) ShowResults();
            }
        }

        public void SetPaused(bool paused)
        {
            if (_resultShown && paused) return;
            Paused = paused;
            Time.timeScale = paused ? 0f : Speed;
            if (_app.Audio != null) _app.Audio.SetPaused(paused);
            Hud.ShowPause(paused);
            if (paused) Deselect();
            else SaveSettings();
        }

        public void ToggleSpeed()
        {
            Speed = Speed > 1.5f ? 1f : 2f;
            if (!Paused) Time.timeScale = Speed;
            Hud.SetSpeed(Speed);
        }

        public void Restart()
        {
            SaveSettings();
            Time.timeScale = 1f;
            if (_app.Audio != null) { _app.Audio.SetPaused(false); _app.Audio.StopAllSfx(); }
            AppRoot.LoadBattle(_encounter.id);
        }

        private void ShowResults()
        {
            if (_resultShown) return;
            _resultShown = true;
            Deselect();
            Hud.ShowInspect(null);
            bool won = Sim.State == BattleState.Won;
            Time.timeScale = 1f;
            SaveData data = _app.Save.Data;
            EncounterRecord rec = data.GetOrCreate(_encounter.id);
            rec.attempts++;
            int seeds = Sim.SeedsEarned;
            if (won)
            {
                rec.completed = true;
                if (Sim.HeartrootHealth >= Sim.HeartrootMaxHealth) rec.flawless = true;
                rec.bestHeartroot = Mathf.Max(rec.bestHeartroot, Sim.HeartrootHealth);
                data.seeds += seeds;
                data.settings.tutorialSeen = true;
            }
            _app.SaveNow();
            Debug.Log($"[VS Battle] Result {(won ? "WON" : "LOST")} t={Sim.Stats.elapsed:0.0}s defeated={Sim.Stats.enemiesDefeated} " +
                      $"planted={Sim.Stats.defendersPlanted} lost={Sim.Stats.defendersLost} heart={Sim.HeartrootHealth}/{Sim.HeartrootMaxHealth} seeds+={seeds} total={data.seeds}");
            EncounterDefinition next = NextEncounter();
            Hud.ShowResult(won, Sim.Stats, seeds, Sim.HeartrootHealth, Sim.HeartrootMaxHealth, next != null, () => AppRoot.LoadBattle(next.id));
            Play(won ? Sfx.Victory : Sfx.Defeat);
        }

        private EncounterDefinition NextEncounter()
        {
            ContentDatabase db = _app.Config != null ? _app.Config.content : null;
            if (db == null) return null;
            int i = db.encounters.IndexOf(_encounter);
            return i >= 0 && i + 1 < db.encounters.Count ? db.encounters[i + 1] : null;
        }

        private void SaveSettings() => _app.SaveNow();

        private void OnApplicationPause(bool pause)
        {
            if (pause && Sim != null && Sim.State == BattleState.Running && !Paused) SetPaused(true);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        private void Play(Sfx s) { if (_app.Audio != null) _app.Audio.Play(s); }

        // ================================================================ debug
        private void OnDebugCommand(string cmd)
        {
            ContentDatabase db = _app.Config.content;
            switch (cmd)
            {
                case "+100 Vitality": Sim.DebugGrantVitality(100); break;
                case "-100 Vitality": Sim.DebugGrantVitality(-100); break;
                case "Next wave": Sim.DebugSkipToNextWave(); break;
                case "Reset cooldowns": Sim.DebugResetCooldowns(); break;
                case "Spawn Drifter": SpawnDebug(db.FindEnemy("drifter")); break;
                case "Spawn Skitter": SpawnDebug(db.FindEnemy("skitter")); break;
                case "Spawn Brute": SpawnDebug(db.FindEnemy("plated_brute")); break;
                case "Defeat all": Sim.DebugDefeatAllEnemies(); break;
                case "Grid debug": _overlay.DebugGrid = !_overlay.DebugGrid; break;
            }
        }

        private void SpawnDebug(EnemyDefinition e)
        {
            if (e != null) Sim.SpawnEnemy(e, Random.Range(0, Sim.Lanes));
        }
    }
}
