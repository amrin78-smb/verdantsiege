using UnityEngine;

namespace VerdantSiege.Audio
{
    /// <summary>
    /// Persistent 2D audio: a pool of SFX voices (per-sfx voice limits and minimum intervals, oldest-voice stealing),
    /// a crossfading music channel (two sources internally) and one ambience loop.
    /// Create with <see cref="Ensure"/>. Update only runs while a fade or duck is in progress; Play() does not allocate.
    /// Timing uses unscaled time so it works with Time.timeScale = 0.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioService : MonoBehaviour
    {
        public const int VoiceCount = 12;
        public const float PausedMusicDuck = 0.3f;
        private const float DuckSpeed = 3f;   // duck level units per second
        private const float InstantSpeed = 1000f;

        public static AudioService Instance { get; private set; }

        [SerializeField] private AudioLibrary m_Library;

        private AudioSource[] m_Voices;
        private int[] m_VoiceSfx;
        private float[] m_VoiceStart;
        private float[] m_VoiceBaseVolume;
        private bool[] m_VoicePaused;
        private float[] m_LastPlay;

        private readonly AudioSource[] m_Music = new AudioSource[2];
        private readonly float[] m_MusicLevel = new float[2];
        private readonly float[] m_MusicTarget = new float[2];
        private readonly float[] m_MusicSpeed = new float[2];
        private readonly float[] m_MusicGain = { 1f, 1f };
        private int m_ActiveMusic;

        private AudioSource m_Ambience;
        private float m_AmbLevel, m_AmbTarget, m_AmbSpeed = 1f, m_AmbGain = 1f;
        private bool m_AmbPaused;

        private float m_Duck = 1f, m_DuckTarget = 1f;
        private float m_MusicVolume = 1f, m_SfxVolume = 1f, m_AmbienceVolume = 1f;
        private bool m_Paused;
        private bool m_Built;
        private uint m_Rand = 0x9E3779B9u;
        private static bool s_WarnedNoLibrary;

        public AudioLibrary Library => m_Library;
        public bool IsPaused => m_Paused;
        public float MusicVolume => m_MusicVolume;
        public float SfxVolume => m_SfxVolume;
        public float AmbienceVolume => m_AmbienceVolume;
        public AudioClip CurrentMusic => m_Built && m_MusicTarget[m_ActiveMusic] > 0f ? m_Music[m_ActiveMusic].clip : null;

        /// <summary>Returns the singleton, creating a persistent GameObject on first call. A non-null lib replaces the current library.</summary>
        public static AudioService Ensure(AudioLibrary lib)
        {
            if (Instance == null)
            {
                var go = new GameObject("AudioService");
                go.AddComponent<AudioService>(); // Awake assigns Instance
            }
            AudioService s = Instance;
            if (s == null) return null;
            if (lib != null) s.m_Library = lib;
            else if (s.m_Library == null) WarnNoLibrary();
            return s;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent != null) transform.SetParent(null, false);
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            Build();
            enabled = false; // no Update until a fade starts
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Build()
        {
            if (m_Built) return;
            m_Built = true;
            m_Voices = new AudioSource[VoiceCount];
            m_VoiceSfx = new int[VoiceCount];
            m_VoiceStart = new float[VoiceCount];
            m_VoiceBaseVolume = new float[VoiceCount];
            m_VoicePaused = new bool[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                m_Voices[i] = CreateSource("Sfx" + i, false, 128);
                m_VoiceSfx[i] = -1;
            }
            m_LastPlay = new float[AudioLibrary.SfxCount];
            for (int i = 0; i < m_LastPlay.Length; i++) m_LastPlay[i] = -1000f;
            m_Music[0] = CreateSource("MusicA", true, 16);
            m_Music[1] = CreateSource("MusicB", true, 16);
            m_Music[0].ignoreListenerPause = true;
            m_Music[1].ignoreListenerPause = true;
            m_Ambience = CreateSource("Ambience", true, 64);
        }

        private AudioSource CreateSource(string sourceName, bool loop, int priority)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f;
            src.dopplerLevel = 0f;
            src.priority = priority;
            src.volume = 0f;
            return src;
        }

        private static void WarnNoLibrary()
        {
            if (s_WarnedNoLibrary) return;
            s_WarnedNoLibrary = true;
            Debug.LogWarning("[AudioService] No AudioLibrary assigned; audio is silent. Run Tools/Verdant Siege/Generate Audio and pass the library to AudioService.Ensure().");
        }

        // ------------------------------------------------------------------ SFX

        /// <summary>Plays a one-shot. Respects the library's per-sfx volume, voice limit and minimum interval.</summary>
        public void Play(Sfx s, float volumeScale = 1f, float pitchJitter = 0.06f)
        {
            if (!m_Built) return;
            AudioLibrary lib = m_Library;
            if (lib == null)
            {
                WarnNoLibrary();
                return;
            }
            int id = (int)s;
            if (id < 0 || id >= m_LastPlay.Length) return;
            AudioClip clip = lib.GetClip(s);
            if (clip == null) return;
            float vol = lib.GetVolume(s) * (volumeScale > 0f ? volumeScale : 0f);
            if (vol <= 0f || m_SfxVolume <= 0f) return;

            float now = Time.unscaledTime;
            if (now - m_LastPlay[id] < lib.GetMinInterval(s)) return;

            int maxV = lib.GetMaxVoices(s);
            int sameCount = 0, oldestSame = -1, free = -1, oldestAny = -1;
            float oldestSameT = float.MaxValue, oldestAnyT = float.MaxValue;
            for (int i = 0; i < VoiceCount; i++)
            {
                if (!m_VoicePaused[i] && !m_Voices[i].isPlaying)
                {
                    if (free < 0) free = i;
                    continue;
                }
                float start = m_VoiceStart[i];
                if (m_VoiceSfx[i] == id)
                {
                    sameCount++;
                    if (start < oldestSameT) { oldestSameT = start; oldestSame = i; }
                }
                if (start < oldestAnyT) { oldestAnyT = start; oldestAny = i; }
            }

            int v = sameCount >= maxV ? oldestSame : (free >= 0 ? free : oldestAny);
            if (v < 0) return;

            AudioSource src = m_Voices[v];
            if (src.isPlaying || m_VoicePaused[v]) src.Stop();
            m_VoicePaused[v] = false;
            src.clip = clip;
            src.pitch = pitchJitter > 0f ? 1f + pitchJitter * (NextRandom01() * 2f - 1f) : 1f;
            m_VoiceBaseVolume[v] = vol;
            src.volume = Mathf.Clamp01(vol * m_SfxVolume);
            src.Play();
            m_VoiceSfx[v] = id;
            m_VoiceStart[v] = now;
            m_LastPlay[id] = now;
        }

        public void StopAllSfx()
        {
            if (!m_Built) return;
            for (int i = 0; i < VoiceCount; i++)
            {
                m_Voices[i].Stop();
                m_VoicePaused[i] = false;
                m_VoiceSfx[i] = -1;
            }
        }

        private float NextRandom01()
        {
            uint x = m_Rand;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            m_Rand = x;
            return (x & 0xFFFFFFu) / 16777216f;
        }

        // ------------------------------------------------------------------ Music

        /// <summary>Crossfades to <paramref name="clip"/> (looping). Same clip already playing: no restart. Null fades music out.</summary>
        public void PlayMusic(AudioClip clip, float fade = 1f)
        {
            if (!m_Built) return;
            if (clip == null)
            {
                StopMusic(fade);
                return;
            }
            float speed = Speed(fade);
            int cur = m_ActiveMusic;
            AudioSource curSrc = m_Music[cur];
            if (curSrc.clip == clip && curSrc.isPlaying)
            {
                m_MusicGain[cur] = m_Library != null ? m_Library.GetMusicGain(clip) : 1f;
                SetMusicTarget(cur, 1f, speed);
                SetMusicTarget(1 - cur, 0f, speed);
                BeginFade();
                return;
            }

            SetMusicTarget(cur, 0f, speed);
            int next = 1 - cur;
            AudioSource nextSrc = m_Music[next];
            m_ActiveMusic = next;
            m_MusicGain[next] = m_Library != null ? m_Library.GetMusicGain(clip) : 1f;
            if (!(nextSrc.clip == clip && nextSrc.isPlaying))
            {
                // Reusing a source that is still fading a different clip out would click; that source is the old-old track, so cut it.
                nextSrc.Stop();
                nextSrc.clip = clip;
                m_MusicLevel[next] = 0f;
                nextSrc.volume = 0f;
                nextSrc.Play();
            }
            SetMusicTarget(next, 1f, speed);
            BeginFade();
        }

        public void PlayMenuMusic(float fade = 1f)
        {
            if (m_Library == null) { WarnNoLibrary(); return; }
            PlayMusic(m_Library.menuMusic, fade);
        }

        public void PlayBattleMusic(float fade = 1f)
        {
            if (m_Library == null) { WarnNoLibrary(); return; }
            PlayMusic(m_Library.battleMusic, fade);
        }

        public void StopMusic(float fade = 1f)
        {
            if (!m_Built) return;
            float speed = Speed(fade);
            SetMusicTarget(0, 0f, speed);
            SetMusicTarget(1, 0f, speed);
            BeginFade();
        }

        private void SetMusicTarget(int k, float target, float speed)
        {
            m_MusicTarget[k] = target;
            m_MusicSpeed[k] = speed;
            if (speed >= InstantSpeed) m_MusicLevel[k] = target;
        }

        // ------------------------------------------------------------------ Ambience

        /// <summary>Fades in a looping ambience clip (null = library ambienceLoop).</summary>
        public void PlayAmbience(AudioClip clip = null, float fade = 2f)
        {
            if (!m_Built) return;
            if (clip == null && m_Library != null) clip = m_Library.ambienceLoop;
            if (clip == null)
            {
                if (m_Library == null) WarnNoLibrary();
                return;
            }
            bool running = m_Ambience.isPlaying || m_AmbPaused;
            if (m_Ambience.clip != clip || !running)
            {
                m_Ambience.Stop();
                m_AmbPaused = false;
                m_Ambience.clip = clip;
                m_AmbLevel = 0f;
                m_Ambience.volume = 0f;
                m_Ambience.Play();
                if (m_Paused)
                {
                    m_Ambience.Pause();
                    m_AmbPaused = true;
                }
            }
            m_AmbGain = m_Library != null ? m_Library.GetMusicGain(clip) : 1f;
            m_AmbTarget = 1f;
            m_AmbSpeed = Speed(fade);
            if (m_AmbSpeed >= InstantSpeed) m_AmbLevel = 1f;
            BeginFade();
        }

        public void StopAmbience(float fade = 1f)
        {
            if (!m_Built) return;
            m_AmbTarget = 0f;
            m_AmbSpeed = Speed(fade);
            if (m_AmbSpeed >= InstantSpeed) m_AmbLevel = 0f;
            BeginFade();
        }

        // ------------------------------------------------------------------ Global

        /// <summary>Master levels, each 0..1 (from settings). Applies immediately, including to playing voices.</summary>
        public void SetVolumes(float music, float sfx, float ambience)
        {
            m_MusicVolume = Mathf.Clamp01(music);
            m_SfxVolume = Mathf.Clamp01(sfx);
            m_AmbienceVolume = Mathf.Clamp01(ambience);
            if (!m_Built) return;
            for (int i = 0; i < VoiceCount; i++) m_Voices[i].volume = Mathf.Clamp01(m_VoiceBaseVolume[i] * m_SfxVolume);
            ApplyMusicVolumes();
            ApplyAmbienceVolume();
            if (m_SfxVolume <= 0f) StopAllSfx();
        }

        /// <summary>Pauses SFX and ambience; music keeps playing, ducked to 30%. New Play() calls (e.g. UI taps) still sound.</summary>
        public void SetPaused(bool paused)
        {
            if (!m_Built || m_Paused == paused) return;
            m_Paused = paused;
            for (int i = 0; i < VoiceCount; i++)
            {
                if (paused)
                {
                    if (m_Voices[i].isPlaying)
                    {
                        m_Voices[i].Pause();
                        m_VoicePaused[i] = true;
                    }
                }
                else if (m_VoicePaused[i])
                {
                    m_Voices[i].UnPause();
                    m_VoicePaused[i] = false;
                }
            }
            if (paused)
            {
                if (m_Ambience.isPlaying)
                {
                    m_Ambience.Pause();
                    m_AmbPaused = true;
                }
            }
            else if (m_AmbPaused)
            {
                m_Ambience.UnPause();
                m_AmbPaused = false;
            }
            m_DuckTarget = paused ? PausedMusicDuck : 1f;
            BeginFade();
        }

        private static float Speed(float fade) => fade > 0.001f ? 1f / fade : InstantSpeed;

        private void BeginFade()
        {
            ApplyMusicVolumes();
            ApplyAmbienceVolume();
            enabled = true;
        }

        private void Update()
        {
            if (!m_Built)
            {
                enabled = false;
                return;
            }
            float dt = Time.unscaledDeltaTime;
            bool active = false;
            for (int k = 0; k < 2; k++)
            {
                if (m_MusicLevel[k] != m_MusicTarget[k])
                {
                    m_MusicLevel[k] = Mathf.MoveTowards(m_MusicLevel[k], m_MusicTarget[k], m_MusicSpeed[k] * dt);
                    active = true;
                }
                if (m_MusicLevel[k] <= 0f && m_MusicTarget[k] <= 0f && m_Music[k].isPlaying) m_Music[k].Stop();
            }
            if (m_AmbLevel != m_AmbTarget)
            {
                m_AmbLevel = Mathf.MoveTowards(m_AmbLevel, m_AmbTarget, m_AmbSpeed * dt);
                active = true;
            }
            if (m_AmbLevel <= 0f && m_AmbTarget <= 0f && (m_Ambience.isPlaying || m_AmbPaused))
            {
                m_Ambience.Stop();
                m_AmbPaused = false;
            }
            if (m_Duck != m_DuckTarget)
            {
                m_Duck = Mathf.MoveTowards(m_Duck, m_DuckTarget, DuckSpeed * dt);
                active = true;
            }
            ApplyMusicVolumes();
            ApplyAmbienceVolume();
            if (!active) enabled = false;
        }

        private void ApplyMusicVolumes()
        {
            if (!m_Built) return;
            for (int k = 0; k < 2; k++)
            {
                // Equal-power curve so a crossfade does not dip in the middle.
                float curve = Mathf.Sin(m_MusicLevel[k] * (Mathf.PI * 0.5f));
                m_Music[k].volume = Mathf.Clamp01(curve * m_MusicGain[k] * m_MusicVolume * m_Duck);
            }
        }

        private void ApplyAmbienceVolume()
        {
            if (!m_Built) return;
            float curve = Mathf.Sin(m_AmbLevel * (Mathf.PI * 0.5f));
            m_Ambience.volume = Mathf.Clamp01(curve * m_AmbGain * m_AmbienceVolume);
        }
    }
}
