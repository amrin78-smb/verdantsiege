using UnityEngine;
using UnityEngine.SceneManagement;
using VerdantSiege.Audio;
using VerdantSiege.Save;

namespace VerdantSiege.App
{
    /// <summary>Which Sanctuary screen the menu scene opens on (results "World Map" / "Next" route here).</summary>
    public enum MenuRoute
    {
        Sanctuary = 0,
        WorldMap,
        Loadout,
        Conservatory
    }

    /// <summary>
    /// Persistent application services (explicit composition root, not a god object): save data, settings
    /// application, audio, progression and the encounter chosen for the next battle. Gameplay systems live in the battle scene.
    /// </summary>
    public sealed class AppRoot : MonoBehaviour
    {
        public const string MainMenuScene = "MainMenu";
        public const string BattleScene = "Battle";

        public static AppRoot Instance { get; private set; }

        public GameConfig Config { get; private set; }
        public SaveService Save { get; private set; }
        public AudioService Audio { get; private set; }
        public ProgressionService Progression { get; private set; }
        public SettingsData Settings => Save.Data.settings;
        /// <summary>Encounter id the battle scene should load.</summary>
        public string PendingEncounterId { get; set; }
        /// <summary>Screen the Sanctuary scene opens on, and the mission it concerns (loadout).</summary>
        public MenuRoute PendingRoute { get; set; }
        public string PendingRouteEncounterId { get; set; }
        /// <summary>Test/debug: battles use the encounter's authored roster and no research/mastery (autoplay direct mode).</summary>
        public bool ForceNeutralLoadout { get; set; }

        public static AppRoot Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[AppRoot]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AppRoot>();
            Instance.Init();
            return Instance;
        }

        private void Init()
        {
            Config = GameConfig.Load();
            Save = new SaveService(SaveService.DefaultPath);
            SaveLoadOutcome outcome = Save.Load();
            Progression = new ProgressionService(Save, Config != null ? Config.content : null);
            Debug.Log($"[VS Save] {outcome} v{Save.Data.version} seeds={Save.Data.seeds} essence={Save.Data.essence} research={Save.Data.research.Count} " +
                      $"defenders={Save.Data.unlockedDefenders.Count} path={SaveService.DefaultPath}");
            Audio = AudioService.Ensure(Config != null ? Config.audio : null);
            ApplySettings();
            QualityManager.Apply(Settings.qualityLevel);
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            PendingEncounterId = Config != null ? Config.firstEncounterId : "vh_01";
        }

        public void ApplySettings()
        {
            if (Audio != null) Audio.SetVolumes(Settings.musicVolume, Settings.sfxVolume, Settings.ambienceVolume);
        }

        public void SaveNow() => Save.Save();

        /// <summary>New game after confirmation: wipes progress (settings kept) and restores the starting collection.</summary>
        public void ResetProgress()
        {
            Save.ResetProgress();
            Progression.Reconcile();
            Save.Save();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Save.Save(); // Android may kill a backgrounded app without further callbacks
        }

        private void OnApplicationQuit() => Save.Save();

        // ---------------------------------------------------------------- scene flow

        public static void LoadMainMenu() => LoadMenu(MenuRoute.Sanctuary);

        /// <summary>Opens the Sanctuary scene on a given screen (e.g. the loadout of the next mission).</summary>
        public static void LoadMenu(MenuRoute route, string encounterId = null)
        {
            Time.timeScale = 1f;
            AppRoot app = Ensure();
            app.PendingRoute = route;
            app.PendingRouteEncounterId = encounterId;
            SceneManager.LoadScene(MainMenuScene);
        }

        public static void LoadBattle(string encounterId)
        {
            Time.timeScale = 1f;
            Ensure().PendingEncounterId = encounterId;
            SceneManager.LoadScene(BattleScene);
        }
    }
}
