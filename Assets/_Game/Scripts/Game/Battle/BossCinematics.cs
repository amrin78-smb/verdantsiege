using UnityEngine;
using VerdantSiege.Audio;
using VerdantSiege.Data;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Where the boss stands and how big it is (readability on small phones). The boss stands past the entry edge,
    /// pushed right and slightly scaled down so its body never covers a lane cell at 4:3..21:9; the camera fit keeps
    /// only its front (face, pods) in view, so the rest may extend past the screen edge.
    /// </summary>
    public static class BossStage
    {
        /// <summary>Boss origin, in columns past the last column.</summary>
        public const float OffsetColumns = 1.95f;
        /// <summary>Uniform model scale.</summary>
        public const float Scale = 0.82f;
        /// <summary>Lowered into the soil a little (shorter silhouette over the far lanes).</summary>
        public const float Sink = 0.15f;
        /// <summary>Columns past the field the camera fit must include (face and weak-point pods).</summary>
        public const float FitMargin = 2.1f;
    }

    /// <summary>
    /// Boss encounter cinematics, separate from the rules and the per-part animation: camera shake on entrance,
    /// major impacts (scaled by how big the hit is), weak-point breaks and the death; roar/eruption sounds timed to
    /// the animation; and the 0.35x slow motion for ~1.2 s real time when the boss falls (gameplay is already won).
    /// Respects Settings ▸ Reduced shake through <see cref="BattleCameraShake.ShakeEnabled"/>.
    /// </summary>
    public sealed class BossCinematics : MonoBehaviour
    {
        public const float DeathSlowScale = 0.35f;
        public const float DeathSlowRealSeconds = 1.2f;

        private BattleSimulation _sim;
        private BossView _view;
        private BattleCameraShake _shake;
        private AudioService _audio;
        private System.Action<float, float> _slowMotion;

        public void Init(BattleSimulation sim, BossView view, BattleCameraShake shake, AudioService audio, System.Action<float, float> slowMotion)
        {
            _sim = sim;
            _view = view;
            _shake = shake;
            _audio = audio;
            _slowMotion = slowMotion;
            if (_view != null)
            {
                _view.ImpactRequested += OnImpact;
                _view.RumbleRequested += OnRumble;
                _view.Roared += OnRoar;
                _view.Erupted += OnErupted;
            }
            sim.BossStaggered += OnStaggered;
            sim.BossWeakPointDestroyed += OnPodDestroyed;
            sim.BossWeakPointsExposed += OnPodsExposed;
            sim.DefenderUprooted += OnUprooted;
            sim.BossDefeated += OnDefeated;
        }

        private void OnDestroy()
        {
            if (_view != null)
            {
                _view.ImpactRequested -= OnImpact;
                _view.RumbleRequested -= OnRumble;
                _view.Roared -= OnRoar;
                _view.Erupted -= OnErupted;
            }
            if (_sim == null) return;
            _sim.BossStaggered -= OnStaggered;
            _sim.BossWeakPointDestroyed -= OnPodDestroyed;
            _sim.BossWeakPointsExposed -= OnPodsExposed;
            _sim.DefenderUprooted -= OnUprooted;
            _sim.BossDefeated -= OnDefeated;
        }

        private void OnImpact(float amount) { if (_shake != null) _shake.Impact(amount); }
        private void OnRumble(float amount, float seconds) { if (_shake != null) _shake.Rumble(amount, seconds); }
        private void OnRoar() => Play(Sfx.BossRoar);

        private void OnErupted()
        {
            Play(Sfx.RootStrike);
            Play(Sfx.ArmorBreak, 0.8f);
        }

        private void OnPodsExposed(BossRuntime b) => Play(Sfx.EliteArrive, 0.8f);

        private void OnPodDestroyed(BossRuntime b, BossWeakPoint w)
        {
            Play(Sfx.ArmorBreak);
            OnImpact(0.18f);
        }

        private void OnStaggered(BossRuntime b)
        {
            if (_view != null) _view.OnStaggered();
            Play(Sfx.BossRoar, 0.7f);
            Play(Sfx.MajorWave, 0.6f);
        }

        /// <summary>Each plant torn out adds a little to the landing shake (bigger strikes feel bigger).</summary>
        private void OnUprooted(DefenderRuntime d, int refund) => OnImpact(0.12f);

        private void OnDefeated(BossRuntime b)
        {
            _slowMotion?.Invoke(DeathSlowScale, DeathSlowRealSeconds); // the death roar comes from BossView.Roared
            Play(Sfx.RootStrike, 0.8f);
        }

        private void Play(Sfx s, float vol = 1f) { if (_audio != null) _audio.Play(s, vol); }
    }
}
