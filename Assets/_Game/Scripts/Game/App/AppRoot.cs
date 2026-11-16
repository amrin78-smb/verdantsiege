using UnityEngine;
using UnityEngine.SceneManagement;
using VerdantSiege.Audio;
using VerdantSiege.Save;

namespace VerdantSiege.App
{
    /// <summary>
    /// Persistent application services (explicit composition root, not a god object): save data, settings
    /// application, audio and the encounter chosen for the next battle. Gameplay systems live in the battle scene.
    /// </summary>
    public sealed class AppRoot : MonoBehaviour
    {
        public const string MainMenuScene = "MainMenu";
        public const string BattleScene = "Battle";

        public static AppRoot Instance { get; private set; }

        public GameConfig Config { get; private set; }
        public SaveService Save { get; private set; }
        public AudioService Audio { get; private set; }
        public SettingsData Settings => Save.Data.settings;
        /// <summary>Encounter id the battle scene should load.</summary>
        public string PendingEncounterId { get; set; }

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
            Debug.Log($"[VS Save] {outcome} seeds={Save.Data.seeds} path={SaveService.DefaultPath}");
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

        private void OnApplicationPause(bool paused)
        {
            if (paused) Save.Save(); // Android may kill a backgrounded app without further callbacks
        }

        private void OnApplicationQuit() => Save.Save();

        // ---------------------------------------------------------------- scene flow

        public static void LoadMainMenu()
        {
            Time.timeScale = 1f;
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
