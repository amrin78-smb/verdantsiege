using UnityEngine;

namespace VerdantSiege.Battle.View
{
    /// <summary>
    /// Trauma-based camera shake for the battle camera (breach knocks, boss impacts). Callers add trauma scaled by
    /// how big the impact is; the offset is trauma^2 x noise, decays in real (unscaled) time so slow motion and the
    /// 2x speed toggle do not change how a shake feels, and is applied through <see cref="BattleCameraRig.ShakeOffset"/>
    /// on top of the fitted pose. Disabled entirely by the Reduced Shake setting.
    /// </summary>
    [RequireComponent(typeof(BattleCameraRig))]
    public sealed class BattleCameraShake : MonoBehaviour
    {
        [Tooltip("Largest positional offset in metres at full trauma.")]
        public float maxOffset = 0.42f;
        [Tooltip("Trauma lost per real second.")]
        public float decay = 1.35f;
        public float frequency = 22f;

        private BattleCameraRig _rig;
        private float _trauma;
        private float _rumble;
        private float _rumbleSeconds;
        private float _seed;

        /// <summary>False with the Reduced Shake setting: every request is ignored.</summary>
        public bool ShakeEnabled { get; set; } = true;
        public float Trauma => _trauma;

        private void Awake()
        {
            _rig = GetComponent<BattleCameraRig>();
            _seed = Random.value * 100f;
        }

        /// <summary>One impact: amount 0..1 (0.25 small knock, 0.5 big slam, 0.9 cataclysm).</summary>
        public void Impact(float amount)
        {
            if (!ShakeEnabled) return;
            _trauma = Mathf.Clamp01(Mathf.Max(_trauma, amount) + amount * 0.25f);
        }

        /// <summary>Sustained low rumble (entrance, death throes): holds at least <paramref name="amount"/> for a while.</summary>
        public void Rumble(float amount, float seconds)
        {
            if (!ShakeEnabled) return;
            _rumble = Mathf.Max(_rumble, Mathf.Clamp01(amount));
            _rumbleSeconds = Mathf.Max(_rumbleSeconds, seconds);
        }

        public void Stop()
        {
            _trauma = _rumble = _rumbleSeconds = 0f;
            if (_rig != null) _rig.ShakeOffset = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (_rig == null) return;
            float dt = Time.unscaledDeltaTime;
            if (Time.timeScale <= 0f) { _rig.ShakeOffset = Vector3.zero; return; } // paused: hold still
            if (_rumbleSeconds > 0f)
            {
                _rumbleSeconds -= dt;
                if (_rumbleSeconds <= 0f) _rumble = 0f;
            }
            _trauma = Mathf.Max(0f, _trauma - decay * dt);
            float t = Mathf.Max(_trauma, _rumble);
            if (t <= 0.001f || !ShakeEnabled)
            {
                _rig.ShakeOffset = Vector3.zero;
                return;
            }
            float k = t * t * maxOffset;
            float time = Time.unscaledTime * frequency;
            _rig.ShakeOffset = new Vector3(
                (Mathf.PerlinNoise(_seed, time) - 0.5f) * 2f * k,
                (Mathf.PerlinNoise(_seed + 11f, time) - 0.5f) * 2f * k * 0.7f,
                (Mathf.PerlinNoise(_seed + 23f, time) - 0.5f) * 2f * k * 0.5f);
        }
    }
}
