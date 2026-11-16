using UnityEngine;
using VerdantSiege.Audio;
using VerdantSiege.Data;
using VerdantSiege.Visual;

namespace VerdantSiege.App
{
    /// <summary>Top-level configuration loaded from Resources at start-up (the composition root's asset graph).</summary>
    [CreateAssetMenu(menuName = "Verdant Siege/Game Config", fileName = "VS_GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "VS_GameConfig";

        public ContentDatabase content;
        public VisualLibrary visuals;
        public AudioLibrary audio;
        public string firstEncounterId = "vh_01";
        public string versionLabel = "0.1.0 prototype";

        private static GameConfig _cached;

        public static GameConfig Load()
        {
            if (_cached == null) _cached = Resources.Load<GameConfig>(ResourcePath);
            if (_cached == null) Debug.LogError("[VS] Missing Resources/" + ResourcePath + " (run Tools ▸ Verdant Siege ▸ Setup Project).");
            return _cached;
        }
    }
}
