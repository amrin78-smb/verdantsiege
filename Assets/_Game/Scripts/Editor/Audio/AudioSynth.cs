using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using VerdantSiege.Audio;

namespace VerdantSiege.EditorTools
{
    /// <summary>
    /// Procedural, fully original audio for Verdant Siege (no samples). Organic-fantasy palette: kalimba, marimba,
    /// glassy bells, ocarina, wooden horns, hand drums, filtered noise for leaves/spores/wind.
    /// Deterministic: every generator uses a fixed seed, so reruns write byte-identical WAVs.
    /// Output: 16-bit mono 22050 Hz WAVs under Assets/_Game/Audio/, plus Assets/_Game/Data/Audio/AudioLibrary.asset.
    /// </summary>
    public static class AudioSynth
    {
        private const int Rate = 22050;
        private const double TwoPi = Math.PI * 2.0;
        private const float Nyquist = Rate * 0.45f;

        private const string SfxDir = "Assets/_Game/Audio/SFX";
        private const string MusicDir = "Assets/_Game/Audio/Music";
        private const string AmbienceDir = "Assets/_Game/Audio/Ambience";
        private const string LibraryPath = "Assets/_Game/Data/Audio/AudioLibrary.asset";

        private const int LP = 0, BP = 1, HP = 2;

        private enum Kind { Sfx, Music, Ambience }

        [MenuItem("Tools/Verdant Siege/Generate Audio")]
        private static void GenerateMenu()
        {
            AudioLibrary lib = BuildAll();
            if (lib != null)
            {
                Selection.activeObject = lib;
                Debug.Log("[AudioSynth] Generated audio and updated " + LibraryPath);
            }
        }

        public static AudioLibrary BuildAll()
        {
            try
            {
                EnsureFolder(SfxDir);
                EnsureFolder(MusicDir);
                EnsureFolder(AmbienceDir);
                EnsureFolder(Path.GetDirectoryName(LibraryPath).Replace('\\', '/'));

                var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
                if (lib == null)
                {
                    lib = ScriptableObject.CreateInstance<AudioLibrary>();
                    AssetDatabase.CreateAsset(lib, LibraryPath);
                }
                lib.EnsureArraySizes();

                int total = AudioLibrary.SfxCount + 3, done = 0;
                void Step(string what) => EditorUtility.DisplayProgressBar("Verdant Siege Audio", what, done++ / (float)total);

                Step("UI"); Set(lib, Sfx.UiTap, "ui_tap", UiTap(), 0.55f, 2, 0.03f);
                Step("UI"); Set(lib, Sfx.UiBack, "ui_back", UiBack(), 0.55f, 2, 0.03f);
                Step("UI"); Set(lib, Sfx.CardSelect, "card_select", CardSelect(), 0.65f, 2, 0.03f);
                Step("UI"); Set(lib, Sfx.CardDenied, "card_denied", Bonk(233.08f, 196f, 11, false), 0.6f, 1, 0.12f);
                Step("Plant"); Set(lib, Sfx.Plant, "plant", Plant(), 0.8f, 2, 0.05f);
                Step("Plant"); Set(lib, Sfx.PlantInvalid, "plant_invalid", Bonk(207.65f, 174.61f, 12, true), 0.6f, 1, 0.12f);
                Step("Undo"); Set(lib, Sfx.Undo, "undo", Undo(), 0.6f, 1, 0.08f);
                Step("Combat"); Set(lib, Sfx.Shoot, "shoot", Shoot(), 0.42f, 4, 0.05f);
                Step("Combat"); Set(lib, Sfx.Hit, "hit", Hit(), 0.5f, 4, 0.04f);
                Step("Combat"); Set(lib, Sfx.ArmorHit, "armor_hit", ArmorHit(), 0.5f, 3, 0.05f);
                Step("Combat"); Set(lib, Sfx.ArmorBreak, "armor_break", ArmorBreak(), 0.75f, 2, 0.08f);
                Step("Combat"); Set(lib, Sfx.EnemyDie, "enemy_die", EnemyDie(), 0.62f, 3, 0.05f);
                Step("Combat"); Set(lib, Sfx.Bite, "bite", Bite(), 0.45f, 3, 0.12f);
                Step("Combat"); Set(lib, Sfx.DefenderHurt, "defender_hurt", DefenderHurt(), 0.45f, 2, 0.1f);
                Step("Combat"); Set(lib, Sfx.DefenderDie, "defender_die", DefenderDie(), 0.72f, 2, 0.1f);
                Step("Economy"); Set(lib, Sfx.Collect, "collect", Collect(), 0.65f, 3, 0.03f);
                Step("Economy"); Set(lib, Sfx.Produce, "produce", Produce(), 0.5f, 2, 0.1f);
                Step("Economy"); Set(lib, Sfx.SkyDrop, "sky_drop", SkyDrop(), 0.5f, 2, 0.15f);
                Step("Waves"); Set(lib, Sfx.WaveStart, "wave_start", WaveStart(), 0.8f, 1, 0.5f);
                Step("Waves"); Set(lib, Sfx.MajorWave, "major_wave", MajorWave(), 0.9f, 1, 1f);
                Step("Waves"); Set(lib, Sfx.Breach, "breach", Breach(), 0.9f, 1, 0.5f);
                Step("Stings"); Set(lib, Sfx.Victory, "victory", Victory(), 0.9f, 1, 1f);
                Step("Stings"); Set(lib, Sfx.Defeat, "defeat", Defeat(), 0.85f, 1, 1f);
                Step("Enemies"); Set(lib, Sfx.EnemyGroan, "enemy_groan", EnemyGroan(), 0.4f, 2, 1.2f);
                Step("Enemies"); Set(lib, Sfx.BruteStomp, "brute_stomp", BruteStomp(), 0.6f, 2, 0.15f);
                lib.victorySting = lib.GetClip(Sfx.Victory);

                Step("Battle music"); lib.battleMusic = Save(MusicDir + "/battle_music.wav", BattleMusic(), Kind.Music);
                Step("Menu music"); lib.menuMusic = Save(MusicDir + "/menu_music.wav", MenuMusic(), Kind.Music);
                Step("Ambience"); lib.ambienceLoop = Save(AmbienceDir + "/meadow_ambience.wav", Ambience(), Kind.Ambience);
                lib.menuMusicVolume = 0.7f;
                lib.battleMusicVolume = 0.55f;
                lib.victoryStingVolume = 0.9f;
                lib.ambienceVolume = 0.45f;

                EditorUtility.SetDirty(lib);
                AssetDatabase.SaveAssets();
                return lib;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void Set(AudioLibrary lib, Sfx s, string file, float[] data, float volume, int voices, float interval)
        {
            int i = (int)s;
            lib.sfx[i] = Save(SfxDir + "/" + file + ".wav", data, Kind.Sfx);
            lib.sfxVolume[i] = volume;
            lib.maxVoices[i] = voices;
            lib.minInterval[i] = interval;
        }

        // =====================================================================================  SFX

        private static float[] UiTap()
        {
            var r = new Rng(101);
            var d = Buf(0.09f);
            Tone(d, 0f, 0.09f, 1650f, 1380f, 0.8f, 0.0008f, 55f);
            Tone(d, 0f, 0.05f, 1650f * 2.71f, 1380f * 2.71f, 0.22f, 0.0005f, 120f);
            Noise(d, r, 0f, 0.012f, 0.3f, 0.0004f, 300f, 3500f, 3500f, 1.4f, BP);
            return Finish(d, -1.5f);
        }

        private static float[] UiBack()
        {
            var r = new Rng(102);
            var d = Buf(0.11f);
            Tone(d, 0f, 0.11f, 980f, 760f, 0.8f, 0.0008f, 42f);
            Tone(d, 0f, 0.06f, 980f * 2.71f, 760f * 2.71f, 0.2f, 0.0005f, 110f);
            Noise(d, r, 0f, 0.012f, 0.25f, 0.0004f, 300f, 2400f, 2400f, 1.4f, BP);
            return Finish(d, -1.5f);
        }

        private static float[] CardSelect()
        {
            var r = new Rng(103);
            var d = Buf(0.36f);
            Pluck(d, r, 0f, 659.25f, 0.8f, 0.34f, 0.993f);
            Pluck(d, r, 0.008f, 1318.5f, 0.25f, 0.2f, 0.99f);
            Rustle(d, r, 0f, 0.05f, 0.35f, 4200f, 2500f);
            return Finish(d, -1.5f);
        }

        /// <summary>Soft low double-bonk (two muted wooden knocks, second lower).</summary>
        private static float[] Bonk(float f1, float f2, uint seed, bool dirt)
        {
            var r = new Rng(seed);
            var d = Buf(0.32f);
            Tone(d, 0f, 0.14f, f1 * 1.04f, f1, 0.8f, 0.002f, 20f, 0.25f, 0.08f);
            Tone(d, 0.115f, 0.2f, f2 * 1.04f, f2, 0.75f, 0.002f, 17f, 0.25f, 0.08f);
            if (dirt) Noise(d, r, 0f, 0.1f, 0.25f, 0.001f, 40f, 500f, 300f, 0.9f, LP);
            LowpassAll(d, 1800f);
            return Finish(d, -1.5f);
        }

        private static float[] Plant()
        {
            var r = new Rng(104);
            var d = Buf(0.8f);
            Rustle(d, r, 0f, 0.24f, 0.8f, 1300f, 700f);
            Noise(d, r, 0f, 0.12f, 0.3f, 0.002f, 25f, 900f, 400f, 0.9f, LP);
            Tone(d, 0f, 0.2f, 120f, 80f, 0.45f, 0.002f, 18f, 0.35f, 0.15f);
            Kalimba(d, 0.07f, 783.99f, 0.42f, 7f);
            Kalimba(d, 0.15f, 1046.5f, 0.42f, 7f);
            Kalimba(d, 0.23f, 1318.51f, 0.48f, 6f);
            return Finish(d, -1.5f);
        }

        /// <summary>Reversed-feeling swish: noise swells up while its band rises, then cuts off softly.</summary>
        private static float[] Undo()
        {
            var r = new Rng(105);
            float len = 0.34f;
            var d = Buf(len);
            var f = new Svf();
            double ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate, u = t / len;
                float env = u < 0.82f ? (u / 0.82f) * (u / 0.82f) : Mathf.Max(0f, (1f - u) / 0.18f);
                float fc = 700f * Mathf.Pow(2600f / 700f, u);
                float nz = f.Run(r.Bi(), fc, 0.8f, BP);
                ph += TwoPi * (380f + 260f * u) / Rate;
                d[i] = (nz * 0.9f + (float)Math.Sin(ph) * 0.22f) * env;
            }
            return Finish(d, -1.5f);
        }

        private static float[] Shoot()
        {
            var r = new Rng(106);
            var d = Buf(0.16f);
            Noise(d, r, 0f, 0.09f, 0.75f, 0.001f, 45f, 2200f, 1100f, 1.1f, BP);
            Tone(d, 0f, 0.13f, 470f, 250f, 0.55f, 0.0015f, 36f, 0.25f);
            Tone(d, 0.003f, 0.03f, 1400f, 900f, 0.12f, 0.0005f, 150f);
            return Finish(d, -1.5f);
        }

        private static float[] Hit()
        {
            var r = new Rng(107);
            var d = Buf(0.22f);
            Tone(d, 0f, 0.21f, 150f, 62f, 1f, 0.001f, 20f, 0.4f, 0.15f);
            Tone(d, 0f, 0.09f, 700f, 240f, 0.5f, 0.001f, 42f, 0.2f);
            Noise(d, r, 0f, 0.05f, 0.6f, 0.0005f, 70f, 2600f, 900f, 0.8f, LP);
            Soft(d, 1.8f);
            return Finish(d, -1.5f);
        }

        private static float[] ArmorHit()
        {
            var r = new Rng(108);
            var d = Buf(0.16f);
            Tone(d, 0f, 0.15f, 760f, 700f, 0.6f, 0.0005f, 40f);
            Tone(d, 0f, 0.1f, 760f * 2.43f, 700f * 2.43f, 0.32f, 0.0005f, 60f);
            Tone(d, 0f, 0.12f, 190f, 150f, 0.45f, 0.001f, 35f, 0.3f);
            Noise(d, r, 0f, 0.015f, 0.4f, 0.0003f, 250f, 2400f, 2400f, 1.2f, BP);
            LowpassAll(d, 3200f);
            return Finish(d, -1.5f);
        }

        private static float[] ArmorBreak()
        {
            var r = new Rng(109);
            var d = Buf(0.66f);
            Noise(d, r, 0f, 0.13f, 1f, 0.0003f, 30f, 5000f, 1200f, 0.7f, LP);
            Rustle(d, r, 0f, 0.16f, 0.8f, 2500f, 900f);
            Tone(d, 0f, 0.26f, 120f, 55f, 0.7f, 0.001f, 14f, 0.4f, 0.2f);
            for (int k = 0; k < 14; k++)
            {
                float u = r.Next01();
                float t = 0.06f + 0.5f * Mathf.Pow(u, 1.3f);
                float f = r.Range(900f, 2600f);
                float amp = 0.32f * (1f - t / 0.66f) * r.Range(0.6f, 1f);
                Tone(d, t, 0.05f, f, f * 0.95f, amp, 0.0004f, 70f);
                Tone(d, t, 0.03f, f * 2.3f, f * 2.2f, amp * 0.35f, 0.0004f, 110f);
            }
            Soft(d, 1.5f);
            return Finish(d, -1.5f);
        }

        private static float[] EnemyDie()
        {
            var r = new Rng(110);
            var d = Buf(0.6f);
            Tone(d, 0f, 0.05f, 260f, 780f, 0.6f, 0.001f, 30f);
            Tone(d, 0.03f, 0.22f, 620f, 150f, 0.6f, 0.002f, 16f, 0.2f);
            Noise(d, r, 0f, 0.18f, 0.55f, 0.002f, 18f, 1800f, 350f, 0.25f, BP);
            Noise(d, r, 0.08f, 0.52f, 0.32f, 0.04f, 6f, 4500f, 3000f, 1.2f, BP);
            Soft(d, 1.3f);
            return Finish(d, -1.5f);
        }

        private static float[] Bite()
        {
            var r = new Rng(111);
            var d = Buf(0.24f);
            Noise(d, r, 0f, 0.04f, 0.4f, 0.0008f, 70f, 1400f, 900f, 0.9f, LP);
            Noise(d, r, 0.05f, 0.13f, 0.8f, 0.001f, 35f, 1100f, 500f, 0.7f, LP);
            Tone(d, 0.05f, 0.13f, 130f, 80f, 0.7f, 0.001f, 25f, 0.45f, 0.2f);
            Rustle(d, r, 0.05f, 0.1f, 0.45f, 1200f, 2500f);
            LowpassAll(d, 1800f);
            Soft(d, 1.4f);
            return Finish(d, -1.5f);
        }

        private static float[] DefenderHurt()
        {
            var r = new Rng(112);
            var d = Buf(0.3f);
            Rustle(d, r, 0f, 0.24f, 0.9f, 2600f, 1500f);
            Tone(d, 0f, 0.09f, 420f, 260f, 0.4f, 0.001f, 40f, 0.2f);
            Noise(d, r, 0f, 0.05f, 0.3f, 0.0008f, 60f, 1500f, 1500f, 1f, BP);
            return Finish(d, -1.5f);
        }

        /// <summary>Wilting creak (slowing stick-slip pulses through a falling resonator) + sinking tone + falling leaves.</summary>
        private static float[] DefenderDie()
        {
            var r = new Rng(113);
            float len = 1.0f;
            var d = Buf(len);
            var res1 = new Svf();
            var res2 = new Svf();
            float phase = 0f;
            for (int i = 0; i < S(0.75f) && i < d.Length; i++)
            {
                float t = i / (float)Rate, u = t / 0.75f;
                float rate = Mathf.Lerp(45f, 12f, u);
                phase += rate / Rate;
                float x = 0f;
                if (phase >= 1f)
                {
                    phase -= 1f;
                    x = 0.7f + 0.3f * r.Next01();
                }
                x += r.Bi() * 0.02f;
                float fc = 800f * Mathf.Pow(280f / 800f, u);
                float y = res1.Run(x, fc, 0.08f, BP) + 0.5f * res2.Run(x, fc * 2.1f, 0.1f, BP);
                float env = Mathf.Min(1f, t / 0.05f) * (1f - u * 0.6f) * Mathf.Min(1f, (0.75f - t) / 0.05f);
                d[i] += y * 2.2f * env;
            }
            Tone(d, 0.05f, 0.85f, 520f, 190f, 0.3f, 0.08f, 2.5f, 0.2f);
            Rustle(d, r, 0.55f, 0.4f, 0.4f, 2000f, 400f);
            return Finish(d, -1.5f);
        }

        private static float[] Collect()
        {
            var d = Buf(0.75f);
            Kalimba(d, 0f, 1318.51f, 0.5f, 6f);
            Kalimba(d, 0.065f, 1567.98f, 0.5f, 6f);
            Kalimba(d, 0.13f, 2093f, 0.55f, 5f);
            Tone(d, 0.13f, 0.25f, 4186f, 4186f, 0.06f, 0.001f, 22f);
            return Finish(d, -1.5f);
        }

        /// <summary>Bloom shimmer: a soft swelling chord with a cloud of tiny pentatonic bell grains.</summary>
        private static float[] Produce()
        {
            var r = new Rng(114);
            var d = Buf(0.95f);
            float[] notes = { 1046.5f, 1174.66f, 1318.51f, 1567.98f, 1760f, 2093f };
            for (int k = 0; k < 11; k++)
            {
                float t = 0.02f + 0.5f * k / 10f + r.Range(-0.015f, 0.015f);
                float swell = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.6f));
                Bell(d, Mathf.Max(0f, t), notes[(int)(r.Next01() * notes.Length) % notes.Length], 0.12f + 0.2f * swell, 9f);
            }
            Swell(d, 0f, 0.9f, 523.25f, 0.22f);
            Swell(d, 0f, 0.9f, 783.99f, 0.16f);
            Noise(d, r, 0f, 0.8f, 0.08f, 0.3f, 4f, 5000f, 5000f, 1f, BP);
            return Finish(d, -1.5f);
        }

        private static float[] SkyDrop()
        {
            var r = new Rng(115);
            var d = Buf(0.95f);
            float[] notes = { 2093f, 1760f, 1567.98f, 1318.51f, 1174.66f, 1046.5f, 880f };
            for (int k = 0; k < notes.Length; k++)
                Bell(d, k * 0.07f, notes[k], 0.3f * (1f - k * 0.06f), 9f);
            var f = new Svf();
            float len = 0.75f;
            for (int i = 0; i < S(len) && i < d.Length; i++)
            {
                float t = i / (float)Rate, u = t / len;
                float fc = 6000f * Mathf.Pow(1800f / 6000f, u);
                float env = Mathf.Sin(Mathf.PI * u);
                d[i] += f.Run(r.Bi(), fc, 1.1f, BP) * 0.25f * env * env;
            }
            return Finish(d, -1.5f);
        }

        private static float[] WaveStart()
        {
            var r = new Rng(116);
            var d = Buf(1.45f);
            Horn(d, r, 0f, 0.62f, 220f, 0.8f, 0.12f, 0.2f, 0.55f, 0.006f);
            Horn(d, r, 0.5f, 0.92f, 293.66f, 0.9f, 0.1f, 0.45f, 0.7f, 0.008f);
            return Finish(d, -1.5f);
        }

        private static float[] MajorWave()
        {
            var r = new Rng(117);
            var d = Buf(2.45f);
            Horn(d, r, 0f, 1.05f, 146.83f, 0.9f, 0.25f, 0.3f, 0.5f, 0.005f);
            Horn(d, r, 0f, 1.05f, 220f, 0.55f, 0.25f, 0.3f, 0.45f, 0.005f);
            Horn(d, r, 0.95f, 1.45f, 155.56f, 0.95f, 0.15f, 0.6f, 0.65f, 0.007f);
            Horn(d, r, 0.95f, 1.45f, 233.08f, 0.6f, 0.15f, 0.6f, 0.6f, 0.007f);
            Drum(d, r, 0f, 95f, 55f, 1f, 9f, 0.4f, 600f);
            Drum(d, r, 0.45f, 95f, 55f, 0.7f, 9f, 0.35f, 600f);
            Drum(d, r, 0.95f, 100f, 52f, 1.1f, 7f, 0.45f, 600f);
            Drum(d, r, 1.6f, 95f, 52f, 0.6f, 8f, 0.3f, 600f);
            Soft(d, 1.3f);
            return Finish(d, -1.5f);
        }

        /// <summary>Deep lub-dub heartbeat (twice) under a dissonant tritone bell.</summary>
        private static float[] Breach()
        {
            var d = Buf(2.2f);
            for (int b = 0; b < 2; b++)
            {
                float t0 = b * 0.8f, g = b == 0 ? 1f : 0.8f;
                Tone(d, t0, 0.32f, 72f, 45f, g, 0.004f, 10f, 0.6f, 0.3f);
                Tone(d, t0 + 0.21f, 0.32f, 66f, 42f, g * 0.7f, 0.004f, 11f, 0.6f, 0.3f);
            }
            Bell(d, 0.02f, 523.25f, 0.33f, 2.2f);
            Bell(d, 0.02f, 739.99f, 0.28f, 2.2f);
            Soft(d, 1.5f);
            return Finish(d, -1.5f);
        }

        /// <summary>Uplifting ~3 s fanfare in C major pentatonic: marimba run, bell crown, warm pad, sparkles.</summary>
        private static float[] Victory()
        {
            var r = new Rng(118);
            var d = Buf(3.1f);
            float[] mel = { 392f, 523.25f, 659.25f, 783.99f, 880f, 783.99f };
            float[] at = { 0f, 0.13f, 0.26f, 0.39f, 0.55f, 0.68f };
            for (int k = 0; k < mel.Length; k++)
            {
                Marimba(d, at[k], mel[k], 0.55f, 6f);
                Marimba(d, at[k], mel[k] * 0.5f, 0.18f, 7f);
            }
            float c6 = 1046.5f, end = 0.84f;
            Marimba(d, end, c6, 0.6f, 4f);
            Bell(d, end, c6, 0.4f, 1.6f);
            Bell(d, end, 1318.51f, 0.18f, 2f);
            Bass(d, end, 130.81f, 0.55f, 2.5f);
            Drum(d, r, end, 160f, 90f, 0.45f, 12f, 0.25f, 1200f);
            Pad(d, end, 1.25f, 261.63f, 0.18f, 0.15f, 1f);
            Pad(d, end, 1.25f, 329.63f, 0.15f, 0.15f, 1f);
            Pad(d, end, 1.25f, 392f, 0.15f, 0.15f, 1f);
            Kalimba(d, 1.3f, 1318.51f, 0.2f, 6f);
            Kalimba(d, 1.38f, 1567.98f, 0.2f, 6f);
            Kalimba(d, 1.46f, 2093f, 0.22f, 5f);
            return Finish(d, -1.5f);
        }

        /// <summary>Gentle sad descending motif in A minor with a soft pad.</summary>
        private static float[] Defeat()
        {
            var d = Buf(3.2f);
            float[] mel = { 659.25f, 587.33f, 523.25f };
            for (int k = 0; k < mel.Length; k++) Marimba(d, k * 0.38f, mel[k], 0.5f, 4f);
            float end = 1.14f;
            Marimba(d, end, 440f, 0.55f, 2.5f);
            Bell(d, end, 440f, 0.22f, 1.5f);
            Bass(d, end, 110f, 0.4f, 1.8f);
            Pad(d, end - 0.05f, 1.0f, 220f, 0.2f, 0.3f, 1.1f);
            Pad(d, end - 0.05f, 1.0f, 261.63f, 0.17f, 0.3f, 1.1f);
            Pad(d, end - 0.05f, 1.0f, 329.63f, 0.15f, 0.3f, 1.1f);
            return Finish(d, -1.5f);
        }

        /// <summary>Low wobbly fungal growl: harmonic-rich buzz through two drifting formants ("oh" to "uh").</summary>
        private static float[] EnemyGroan()
        {
            var r = new Rng(119);
            float len = 0.95f;
            var d = Buf(len);
            var f1 = new Svf();
            var f2 = new Svf();
            var body = new Svf();
            var breath = new Svf();
            double ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate, u = t / len;
                float f = 92f * (1f - 0.18f * u) * (1f + 0.07f * Mathf.Sin(2f * Mathf.PI * 6.5f * t) + 0.03f * Mathf.Sin(2f * Mathf.PI * 11f * t));
                ph += TwoPi * f / Rate;
                float src = 0f;
                int hMax = Mathf.Min(40, (int)(Nyquist / f));
                for (int h = 1; h <= hMax; h++) src += (float)Math.Sin(h * ph) / h;
                src += r.Bi() * 0.15f;
                float a = f1.Run(src, Mathf.Lerp(520f, 420f, u), 0.18f, BP);
                float b = f2.Run(src, Mathf.Lerp(1000f, 760f, u), 0.2f, BP);
                float lo = body.Run(src, 300f, 0.9f, LP);
                float br = breath.Run(r.Bi(), 700f, 1f, BP) * 0.1f;
                float env = Mathf.Min(1f, t / 0.12f) * Mathf.Min(1f, (len - t) / 0.3f);
                d[i] = (a + 0.6f * b + 0.35f * lo + br) * env;
            }
            Soft(d, 1.4f);
            return Finish(d, -1.5f);
        }

        private static float[] BruteStomp()
        {
            var r = new Rng(120);
            var d = Buf(0.55f);
            Tone(d, 0f, 0.52f, 78f, 38f, 1f, 0.001f, 8f, 0.5f, 0.3f);
            Tone(d, 0f, 0.13f, 190f, 120f, 0.5f, 0.001f, 30f, 0.2f);
            Noise(d, r, 0f, 0.26f, 0.6f, 0.001f, 14f, 400f, 150f, 0.8f, LP);
            Rustle(d, r, 0.02f, 0.26f, 0.28f, 900f, 300f);
            Soft(d, 2f);
            return Finish(d, -1.5f);
        }

        // =====================================================================================  Music

        /// <summary>
        /// Battle loop: 104 BPM, 16 bars (36.9 s), D dorian. Kalimba ostinato, marimba-ish bass, warm pad,
        /// hand drums + shaker, ocarina melody (A section) doubled by kalimba (B section).
        /// </summary>
        private static float[] BattleMusic()
        {
            const float bpm = 104f;
            const int bars = 16;
            float step = 60f / bpm / 4f;
            float barLen = step * 16f;
            float loopLen = bars * barLen;
            var r = new Rng(201);
            var d = Buf(loopLen + 3f);

            int[] roots = { 62, 60, 58, 60, 62, 60, 58, 57, 65, 60, 67, 62, 65, 60, 67, 57 };
            bool[] minor = { true, false, false, false, true, false, false, true, false, false, false, true, false, false, false, true };
            int[] arp = { 0, 2, 3, 2, 4, 2, 3, 1 };

            for (int bar = 0; bar < bars; bar++)
            {
                float t0 = bar * barLen;
                int pc = roots[bar] % 12;
                int third = minor[bar] ? 3 : 4;

                int padRoot = 55 + Mod(pc - 55, 12);
                Pad(d, t0, barLen, Midi(padRoot), 0.09f, 0.25f, 0.6f);
                Pad(d, t0, barLen, Midi(padRoot + third), 0.075f, 0.25f, 0.6f);
                Pad(d, t0, barLen, Midi(padRoot + 7), 0.075f, 0.25f, 0.6f);

                int kRoot = 62 + Mod(pc - 62, 12);
                int[] tones = { kRoot, kRoot + third, kRoot + 7, kRoot + 12, kRoot + 12 + third };
                for (int e = 0; e < 8; e++)
                    Kalimba(d, t0 + e * 2 * step, Midi(tones[arp[e]]), e % 2 == 0 ? 0.2f : 0.15f, 5f);

                int bRoot = 38 + Mod(pc - 38, 12);
                Bass(d, t0, Midi(bRoot), 0.4f, 4f);
                Bass(d, t0 + 6 * step, Midi(bRoot), 0.28f, 5f);
                Bass(d, t0 + 8 * step, Midi(bRoot + 7), 0.34f, 4.5f);
                Bass(d, t0 + 14 * step, Midi(bRoot + 12), 0.22f, 6f);

                Drum(d, r, t0, 150f, 80f, 0.55f, 11f, 0.15f, 500f);
                Drum(d, r, t0 + 6 * step, 150f, 80f, 0.33f, 12f, 0.12f, 500f);
                Drum(d, r, t0 + 10 * step, 150f, 80f, 0.45f, 11f, 0.14f, 500f);
                Drum(d, r, t0 + 4 * step, 340f, 250f, 0.22f, 28f, 0.35f, 2200f);
                Drum(d, r, t0 + 12 * step, 340f, 250f, 0.24f, 28f, 0.38f, 2200f);
                if (bar % 2 == 1) Drum(d, r, t0 + 15 * step, 340f, 250f, 0.1f, 30f, 0.2f, 2200f);
                if (bar == 7 || bar == 15)
                {
                    Drum(d, r, t0 + 13 * step, 320f, 240f, 0.13f, 30f, 0.25f, 2000f);
                    Drum(d, r, t0 + 14 * step, 300f, 220f, 0.16f, 30f, 0.28f, 1900f);
                }
                for (int s = 0; s < 16; s++)
                {
                    float amp = s % 4 == 2 ? 0.075f : (s % 2 == 1 ? 0.035f : 0.05f);
                    Shaker(d, r, t0 + s * step, amp);
                }
            }

            int[] mel =
            {
                0, 0, 4, 69, 0, 4, 2, 74, 0, 6, 2, 76, 0, 8, 6, 77, 0, 14, 2, 76,
                1, 0, 4, 74, 1, 4, 4, 72, 1, 8, 8, 76,
                2, 0, 4, 77, 2, 4, 2, 79, 2, 6, 2, 77, 2, 8, 6, 74, 2, 14, 2, 72,
                3, 0, 12, 76,
                4, 0, 4, 69, 4, 4, 2, 74, 4, 6, 2, 76, 4, 8, 4, 77, 4, 12, 4, 81,
                5, 0, 4, 79, 5, 4, 4, 76, 5, 8, 4, 72, 5, 12, 4, 76,
                6, 0, 4, 74, 6, 4, 4, 77, 6, 8, 4, 79, 6, 12, 2, 77, 6, 14, 2, 76,
                7, 0, 6, 76, 7, 6, 2, 74, 7, 8, 4, 72, 7, 12, 4, 76,
                8, 0, 6, 81, 8, 6, 2, 79, 8, 8, 4, 77, 8, 12, 4, 81,
                9, 0, 6, 79, 9, 6, 2, 76, 9, 8, 8, 72,
                10, 0, 4, 74, 10, 4, 4, 79, 10, 8, 6, 83, 10, 14, 2, 81,
                11, 0, 8, 81, 11, 8, 4, 77, 11, 12, 4, 74,
                12, 0, 6, 84, 12, 6, 2, 81, 12, 8, 4, 77, 12, 12, 4, 81,
                13, 0, 4, 79, 13, 4, 4, 76, 13, 8, 4, 79, 13, 12, 4, 84,
                14, 0, 4, 83, 14, 4, 4, 81, 14, 8, 4, 79, 14, 12, 2, 83, 14, 14, 2, 81,
                15, 0, 8, 76, 15, 8, 4, 74, 15, 12, 4, 72,
            };
            for (int k = 0; k + 3 < mel.Length; k += 4)
            {
                int bar = mel[k];
                float t = bar * barLen + mel[k + 1] * step;
                float dur = mel[k + 2] * step * 0.92f;
                float f = Midi(mel[k + 3]);
                Ocarina(d, r, t, dur, f, bar >= 8 ? 0.26f : 0.3f);
                if (bar >= 8) Kalimba(d, t, f * 2f, 0.09f, 5f);
            }

            float[] loop = WrapLoop(d, S(loopLen));
            Soft(loop, 1.3f);
            Normalize(loop, -3f);
            return loop;
        }

        /// <summary>Menu loop: 90 BPM, 12 bars (32 s), C major pentatonic. Airy pad, sparse kalimba, celesta-bell melody.</summary>
        private static float[] MenuMusic()
        {
            const float bpm = 90f;
            const int bars = 12;
            float step = 60f / bpm / 4f;
            float barLen = step * 16f;
            float loopLen = bars * barLen;
            var r = new Rng(202);
            var d = Buf(loopLen + 4f);

            int[] roots = { 65, 60, 57, 67 };
            bool[] minor = { false, false, true, false };

            for (int bar = 0; bar < bars; bar++)
            {
                float t0 = bar * barLen;
                int c = bar % 4;
                int pc = roots[c] % 12;
                int third = minor[c] ? 3 : 4;

                int padRoot = 53 + Mod(pc - 53, 12);
                Pad(d, t0, barLen, Midi(padRoot), 0.11f, 0.6f, 1.0f);
                Pad(d, t0, barLen, Midi(padRoot + third), 0.09f, 0.6f, 1.0f);
                Pad(d, t0, barLen, Midi(padRoot + 7), 0.09f, 0.6f, 1.0f);
                Pad(d, t0, barLen, Midi(padRoot + 14), 0.04f, 0.8f, 1.0f); // added 9th, airy

                int kRoot = 60 + Mod(pc - 60, 12);
                int[] tones = { kRoot, kRoot + third, kRoot + 7, kRoot + 12 };
                Kalimba(d, t0, Midi(tones[0]), 0.17f, 3f);
                Kalimba(d, t0 + 4 * step, Midi(tones[2]), 0.13f, 3f);
                Kalimba(d, t0 + 8 * step, Midi(tones[3]), 0.15f, 3f);
                Kalimba(d, t0 + 10 * step, Midi(tones[2]), 0.09f, 3.5f);
                Kalimba(d, t0 + 12 * step, Midi(tones[1] + 12), 0.12f, 3f);

                int bRoot = 41 + Mod(pc - 41, 12);
                Bass(d, t0, Midi(bRoot), 0.32f, 2.2f);
                Bass(d, t0 + 8 * step, Midi(bRoot + 7), 0.2f, 2.8f);

                Shaker(d, r, t0 + 4 * step, 0.025f);
                Shaker(d, r, t0 + 12 * step, 0.03f);
            }

            int[] mel =
            {
                0, 0, 8, 81, 0, 8, 4, 79, 0, 12, 4, 76,
                1, 0, 12, 79,
                2, 0, 4, 76, 2, 4, 4, 79, 2, 8, 8, 81,
                3, 0, 8, 79, 3, 8, 8, 74,
                4, 0, 8, 84, 4, 8, 4, 81, 4, 12, 4, 79,
                5, 0, 12, 76,
                6, 0, 4, 81, 6, 4, 4, 84, 6, 8, 8, 86,
                7, 0, 4, 84, 7, 4, 4, 81, 7, 8, 8, 79,
                9, 0, 8, 76, 9, 8, 8, 74,
                11, 0, 8, 72, 11, 8, 8, 74,
            };
            for (int k = 0; k + 3 < mel.Length; k += 4)
            {
                int bar = mel[k];
                float t = bar * barLen + mel[k + 1] * step;
                float f = Midi(mel[k + 3]);
                Bell(d, t, f, bar >= 8 ? 0.16f : 0.24f, 1.6f);
                if (bar >= 4 && bar < 8) Ocarina(d, r, t, mel[k + 2] * step * 0.9f, f * 0.5f, 0.1f);
            }

            // Airy breath layer: amplitude is zero at both loop ends, so it never needs wrapping.
            var air = new Svf();
            int nLoop = S(loopLen);
            for (int i = 0; i < nLoop; i++)
            {
                float t = i / (float)Rate;
                float a = Mathf.Sin(Mathf.PI * 3f * t / loopLen);
                d[i] += air.Run(r.Bi(), 1400f + 500f * Mathf.Sin(2f * Mathf.PI * 2f * t / loopLen), 1.2f, BP) * 0.03f * a * a;
            }

            float[] loop = WrapLoop(d, nLoop);
            Soft(loop, 1.2f);
            Normalize(loop, -3f);
            return loop;
        }

        // =====================================================================================  Ambience

        /// <summary>24 s meadow: slow-LFO wind, a soft insect hum, distant crickets and seeded bird calls. Seamless.</summary>
        private static float[] Ambience()
        {
            const float len = 24f;
            int n = S(len), fade = Rate;
            var r = new Rng(301);
            var raw = new float[n + fade];

            // Wind (noise) rendered past the end and crossfaded into the start (equal power; uncorrelated noise).
            var wind = new Svf();
            var gust = new Svf();
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)Rate;
                float p = 2f * Mathf.PI * t / len; // LFOs use whole cycles per loop
                float fc = 380f + 260f * (0.5f + 0.5f * Mathf.Sin(3f * p + 1.3f)) + 120f * Mathf.Sin(7f * p);
                float amp = 0.55f + 0.3f * Mathf.Sin(2f * p) + 0.15f * Mathf.Sin(5f * p + 2f);
                float w = wind.Run(r.Bi(), fc, 1.2f, BP) * amp;
                float g = Mathf.Max(0f, Mathf.Sin(3f * p - 0.7f));
                w += gust.Run(r.Bi(), 1500f, 1f, LP) * 0.12f * g * g * g;
                raw[i] = w;
            }
            var d = new float[n];
            Array.Copy(raw, d, n);
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade * Mathf.PI * 0.5f;
                d[i] = raw[i] * Mathf.Sin(k) + raw[n + i] * Mathf.Cos(k);
            }

            // Insect hum and crickets are closed-form periodic (integer Hz, whole LFO cycles) so they loop exactly.
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                double p = TwoPi * t / len;
                double hph = TwoPi * 165.0 * t + 0.6 * Math.Sin(4.0 * p);
                float hum = (float)(Math.Sin(hph) + 0.5 * Math.Sin(2 * hph) + 0.3 * Math.Sin(3 * hph) + 0.15 * Math.Sin(4 * hph) + 0.08 * Math.Sin(5 * hph));
                float hs = 0.5f + 0.5f * (float)Math.Sin(3.0 * p - 0.5);
                d[i] += hum * 0.018f * hs * hs;

                float group = (float)((t * 32.0 / len) % 1.0);
                float gate = group < 0.22f ? Mathf.Sin(Mathf.PI * group / 0.22f) : 0f;
                float pulse = Mathf.Max(0f, (float)Math.Sin(TwoPi * 30.0 * t));
                pulse *= pulse; pulse *= pulse;
                float cs = (float)Math.Sin(2.0 * p * 0.5);
                d[i] += (float)Math.Sin(TwoPi * 4200.0 * t) * gate * pulse * 0.012f * cs * cs;
            }

            // Birds: seeded calls, added with wrap-around so calls near the end continue at the start.
            for (int b = 0; b < 7; b++)
            {
                float start = b * (len / 7f) + r.Range(0.3f, 2.4f);
                Bird(d, r, start, r.Range(0.05f, 0.1f));
            }

            Normalize(d, -6f);
            return d;
        }

        private static void Bird(float[] d, Rng r, float t0, float amp)
        {
            int n = d.Length;
            int syll = 2 + (int)(r.Next01() * 4f);
            float t = t0;
            float baseF = r.Range(2400f, 3600f);
            float trill = r.Range(25f, 40f);
            for (int s = 0; s < syll; s++)
            {
                float dur = r.Range(0.06f, 0.12f);
                float fa = baseF * r.Range(0.9f, 1.15f), fb = baseF * r.Range(1.05f, 1.35f);
                if (r.Next01() < 0.4f) { float tmp = fa; fa = fb; fb = tmp; }
                int s0 = S(t), len = S(dur);
                double ph = 0;
                for (int i = 0; i < len; i++)
                {
                    float u = i / (float)len, tt = i / (float)Rate;
                    float f = Mathf.Lerp(fa, fb, u) + 150f * Mathf.Sin(2f * Mathf.PI * trill * tt);
                    ph += TwoPi * f / Rate;
                    float e = Mathf.Sin(Mathf.PI * u);
                    float v = ((float)Math.Sin(ph) + 0.1f * (float)Math.Sin(2 * ph)) * e * e * amp;
                    d[(s0 + i) % n] += v;
                }
                t += dur + r.Range(0.04f, 0.1f);
            }
        }

        // =====================================================================================  Instruments

        private static void Tone(float[] d, float t0, float dur, float f0, float f1, float amp, float atk, float decay, float h2 = 0f, float h3 = 0f)
        {
            int s0 = S(t0), n = S(dur);
            float ratio = f1 / Mathf.Max(1f, f0);
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= d.Length) break;
                float t = i / (float)Rate;
                float f = f0 * Mathf.Pow(ratio, i / (float)n);
                ph += TwoPi * f / Rate;
                float s = (float)Math.Sin(ph);
                if (h2 != 0f && f * 2f < Nyquist) s += h2 * (float)Math.Sin(2 * ph);
                if (h3 != 0f && f * 3f < Nyquist) s += h3 * (float)Math.Sin(3 * ph);
                d[idx] += s * amp * Env(t, dur, atk, decay);
            }
        }

        /// <summary>Filtered noise burst; filter cutoff glides fc0 → fc1. k = damping (1/Q).</summary>
        private static void Noise(float[] d, Rng r, float t0, float dur, float amp, float atk, float decay, float fc0, float fc1, float k, int mode)
        {
            var f = new Svf();
            int s0 = S(t0), n = S(dur);
            float ratio = fc1 / fc0;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= d.Length) break;
                float t = i / (float)Rate;
                float fc = fc0 * Mathf.Pow(ratio, i / (float)n);
                d[idx] += f.Run(r.Bi(), fc, k, mode) * amp * Env(t, dur, atk, decay);
            }
        }

        /// <summary>Leafy rustle: band-passed noise gated by random micro-crackles.</summary>
        private static void Rustle(float[] d, Rng r, float t0, float dur, float amp, float fc, float density)
        {
            var f = new Svf();
            int s0 = S(t0), n = S(dur);
            float g = 0f;
            float dec = Mathf.Exp(-1f / (Rate * 0.004f));
            float p = density / Rate;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= d.Length) break;
                float u = i / (float)n;
                g *= dec;
                if (r.Next01() < p) g = Mathf.Max(g, r.Range(0.3f, 1f));
                float y = f.Run(r.Bi(), fc, 1f, BP);
                float env = Mathf.Min(1f, u / 0.08f) * (1f - u);
                d[idx] += y * g * amp * env * 2f;
            }
        }

        private static void Kalimba(float[] d, float t0, float f, float amp, float decay = 6f)
        {
            float dur = 6.5f / decay;
            int s0 = S(t0), n = S(dur);
            bool p5 = f * 5.4f < Nyquist, p2 = f * 2f < Nyquist;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= d.Length) break;
                float t = i / (float)Rate;
                double w = TwoPi * f * t;
                float s = Mathf.Exp(-t * decay) * (float)Math.Sin(w);
                if (p2) s += 0.1f * Mathf.Exp(-t * decay * 2.5f) * (float)Math.Sin(2.0 * w);
                if (p5) s += 0.16f * Mathf.Exp(-t * decay * 5f) * (float)Math.Sin(5.4 * w);
                float e = Mathf.Min(1f, t / 0.0015f) * Mathf.Min(1f, (dur - t) / 0.01f);
                d[idx] += s * amp * e;
            }
        }

        private static void Marimba(float[] d, float t0, float f, float amp, float decay = 7f)
        {
            float dur = 6.5f / decay;
            int s0 = S(t0), n = S(dur);
            bool p4 = f * 3.93f < Nyquist, p9 = f * 9.2f < Nyquist;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= d.Length) break;
                float t = i / (float)Rate;
                double w = TwoPi * f * t;
                float s = Mathf.Exp(-t * decay) * (float)Math.Sin(w);
                if (p4) s += 0.25f * Mathf.Exp(-t * decay * 3.5f) * (float)Math.Sin(3.93 * w);
                if (p9) s += 0.05f * Mathf.Exp(-t * decay * 7f) * (float)Math.Sin(9.2 * w);
                float e = Mathf.Min(1f, t / 0.001f) * Mathf.Min(1f, (dur - t) / 0.01f);
                d[idx] += s * amp * e;
            }
        }

        private static readonly float[] s_BellRatio = { 0.5f, 1f, 2f, 2.76f, 5.4f, 8.93f };
        private static readonly float[] s_BellAmp = { 0.15f, 1f, 0.4f, 0.28f, 0.14f, 0.06f };
        private static readonly float[] s_BellDecay = { 0.6f, 1f, 1.6f, 2.2f, 3.5f, 5f };

        /// <summary>Glassy bell (inharmonic partials, higher ones decay faster).</summary>
        private static void Bell(float[] d, float t0, float f, float amp, float decay = 2.5f)
        {
            float dur = Mathf.Min(4f, 6f / decay);
            int s0 = S(t0), n = S(dur);
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= d.Length) break;
                float t = i / (float)Rate;
                double w = TwoPi * f * t;
                float s = 0f;
                for (int p = 0; p < s_BellRatio.Length; p++)
                {
                    if (f * s_BellRatio[p] >= Nyquist) continue;
                    s += s_BellAmp[p] * Mathf.Exp(-t * decay * s_BellDecay[p]) * (float)Math.Sin(s_BellRatio[p] * w);
                }
                float e = Mathf.Min(1f, t / 0.002f) * Mathf.Min(1f, (dur - t) / 0.02f);
                d[idx] += s * amp * e;
            }
        }

        /// <summary>Karplus-Strong plucked string (soft, leafy).</summary>
        private static void Pluck(float[] d, Rng r, float t0, float f, float amp, float dur, float damp)
        {
            int len = Mathf.Max(2, Mathf.RoundToInt(Rate / f));
            var line = new float[len];
            float lp = 0f;
            for (int k = 0; k < len; k++)
            {
                lp += (r.Bi() - lp) * 0.5f;
                line[k] = lp;
            }
            int s0 = S(t0), n = S(dur), p = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= d.Length) break;
                int q = p + 1 == len ? 0 : p + 1;
                float y = line[p];
                line[p] = damp * 0.5f * (line[p] + line[q]);
                p = q;
                float t = i / (float)Rate;
                d[idx] += y * amp * Mathf.Min(1f, (dur - t) / 0.02f);
            }
        }

        /// <summary>Wooden horn: additive harmonics whose brightness opens with the envelope, delayed vibrato, breath.</summary>
        private static void Horn(float[] d, Rng r, float t0, float dur, float f, float amp, float atk, float rel, float bright, float vib)
        {
            int s0 = S(t0), n = S(dur);
            int hMax = Mathf.Clamp((int)(Nyquist / (f * (1f + vib * 2f))), 1, 16);
            var breath = new Svf();
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= d.Length) break;
                float t = i / (float)Rate;
                float e;
                if (t < atk) { float a = Mathf.Sin(0.5f * Mathf.PI * t / atk); e = a * a; }
                else if (t > dur - rel) e = Mathf.Pow(Mathf.Clamp01((dur - t) / rel), 1.5f);
                else e = 1f;
                float v = vib * Mathf.Sin(2f * Mathf.PI * 5.2f * t) * Mathf.Clamp01((t - 0.15f) / 0.3f);
                ph += TwoPi * f * (1f + v) / Rate;
                float b = 0.15f + bright * e;
                float s = 0f;
                for (int h = 1; h <= hMax; h++)
                    s += Mathf.Exp(-(h - 1) / (0.6f + 4f * b)) / Mathf.Sqrt(h) * (float)Math.Sin(h * ph);
                float br = breath.Run(r.Bi(), f * 2f, 0.6f, BP) * 0.12f;
                d[idx] += (s * 0.5f + br) * amp * e;
            }
        }

        /// <summary>Hand drum: pitch-dropping body (with 2nd partial so small speakers hear it) + slap noise.</summary>
        private static void Drum(float[] d, Rng r, float t0, float fHi, float fLo, float amp, float decay, float noiseAmp, float noiseFc)
        {
            float dur = Mathf.Min(1.2f, 6f / decay);
            int s0 = S(t0), n = S(dur);
            var nf = new Svf();
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= d.Length) break;
                float t = i / (float)Rate;
                float f = fLo + (fHi - fLo) * Mathf.Exp(-t * 35f);
                ph += TwoPi * f / Rate;
                float body = ((float)Math.Sin(ph) + 0.35f * (float)Math.Sin(2 * ph)) * Mathf.Exp(-t * decay);
                float nz = nf.Run(r.Bi(), noiseFc, 1f, BP) * Mathf.Exp(-t * 60f) * noiseAmp;
                float e = Mathf.Min(1f, t / 0.001f) * Mathf.Min(1f, (dur - t) / 0.01f);
                d[idx] += (body + nz) * amp * e;
            }
        }

        private static void Shaker(float[] d, Rng r, float t0, float amp)
        {
            Noise(d, r, t0, 0.08f, amp, 0.004f, 55f, 7000f, 6500f, 1.2f, BP);
        }

        /// <summary>Breathy ocarina/flute voice: near-sine with soft chiff and delayed vibrato. Releases after dur.</summary>
        private static void Ocarina(float[] d, Rng r, float t0, float dur, float f, float amp)
        {
            const float atk = 0.035f, rel = 0.12f;
            int s0 = S(t0), n = S(dur + rel);
            var breath = new Svf();
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= d.Length) break;
                float t = i / (float)Rate;
                float e = t < atk ? t / atk : (t < dur ? 1f : Mathf.Max(0f, 1f - (t - dur) / rel));
                float v = 0.006f * Mathf.Sin(2f * Mathf.PI * 5.2f * t) * Mathf.Clamp01((t - 0.25f) / 0.25f);
                ph += TwoPi * f * (1f + v) / Rate;
                float s = (float)Math.Sin(ph) + 0.12f * (float)Math.Sin(2 * ph) + 0.04f * (float)Math.Sin(3 * ph);
                float nz = breath.Run(r.Bi(), f * 2f, 0.8f, BP);
                s += nz * (0.05f + 0.25f * Mathf.Exp(-t * 40f));
                d[idx] += s * amp * e;
            }
        }

        /// <summary>Warm pad: three slightly detuned voices of a soft harmonic stack, smooth attack, hold, release.</summary>
        private static void Pad(float[] d, float t0, float hold, float f, float amp, float atk, float rel)
        {
            atk = Mathf.Min(atk, hold);
            int s0 = S(t0), n = S(hold + rel);
            double w0 = TwoPi * f * (1.0 - 0.0035), w1 = TwoPi * f, w2 = TwoPi * f * (1.0 + 0.004);
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= d.Length) break;
                float t = i / (float)Rate;
                float e;
                if (t < atk) { float a = Mathf.Sin(0.5f * Mathf.PI * t / atk); e = a * a; }
                else if (t < hold) e = 1f;
                else { float a = Mathf.Cos(0.5f * Mathf.PI * Mathf.Clamp01((t - hold) / rel)); e = a * a; }
                d[idx] += (Stack(w0 * t) + Stack(w1 * t) + Stack(w2 * t)) * (amp / 3f) * e;
            }
        }

        private static float Stack(double x)
        {
            return (float)(Math.Sin(x) + 0.3 * Math.Sin(2 * x) + 0.12 * Math.Sin(3 * x) + 0.05 * Math.Sin(4 * x));
        }

        /// <summary>Soft sine swell (rise then fall over dur).</summary>
        private static void Swell(float[] d, float t0, float dur, float f, float amp)
        {
            int s0 = S(t0), n = S(dur);
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= d.Length) break;
                float t = i / (float)Rate, u = i / (float)n;
                float e = Mathf.Sin(Mathf.PI * u);
                double w = TwoPi * f * t;
                d[idx] += (float)(Math.Sin(w) + 0.2 * Math.Sin(2 * w)) * amp * e * e;
            }
        }

        private static void Bass(float[] d, float t0, float f, float amp, float decay)
        {
            float dur = Mathf.Min(1.5f, 5f / decay);
            int s0 = S(t0), n = S(dur);
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= d.Length) break;
                float t = i / (float)Rate;
                double w = TwoPi * f * t;
                float bright = Mathf.Exp(-t * decay * 1.5f);
                float s = (float)Math.Sin(w) + 0.55f * (float)Math.Sin(2 * w) + bright * (0.3f * (float)Math.Sin(3 * w) + 0.12f * (float)Math.Sin(4 * w));
                float e = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * decay) * Mathf.Min(1f, (dur - t) / 0.02f);
                d[idx] += s * amp * e;
            }
        }

        // =====================================================================================  DSP utilities

        /// <summary>Zero-delay-feedback state-variable filter (unconditionally stable with modulated cutoff).</summary>
        private struct Svf
        {
            private float m_Ic1, m_Ic2;

            /// <summary>k = damping (1/Q). mode LP/BP/HP; BP is normalised to unity gain at the centre.</summary>
            public float Run(float x, float fc, float k, int mode)
            {
                float g = Mathf.Tan(Mathf.PI * Mathf.Clamp(fc, 10f, Rate * 0.45f) / Rate);
                float a1 = 1f / (1f + g * (g + k));
                float a2 = g * a1;
                float a3 = g * a2;
                float v3 = x - m_Ic2;
                float v1 = a1 * m_Ic1 + a2 * v3;
                float v2 = m_Ic2 + a2 * m_Ic1 + a3 * v3;
                m_Ic1 = 2f * v1 - m_Ic1;
                m_Ic2 = 2f * v2 - m_Ic2;
                if (mode == LP) return v2;
                if (mode == BP) return k * v1;
                return x - k * v1 - v2;
            }
        }

        /// <summary>Deterministic xorshift32 (independent of System.Random / UnityEngine.Random implementations).</summary>
        private sealed class Rng
        {
            private uint m_S;

            public Rng(uint seed)
            {
                m_S = seed * 2654435761u + 0x6D2B79F5u;
                if (m_S == 0) m_S = 1;
                for (int i = 0; i < 4; i++) Next01();
            }

            public float Next01()
            {
                uint x = m_S;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                m_S = x;
                return (x & 0xFFFFFFu) / 16777216f;
            }

            public float Bi() => Next01() * 2f - 1f;
            public float Range(float a, float b) => a + (b - a) * Next01();
        }

        private static float Env(float t, float dur, float atk, float decay)
        {
            float e = t < atk ? t / atk : Mathf.Exp(-(t - atk) * decay);
            float tail = dur - t;
            if (tail < 0.006f) e *= Mathf.Max(0f, tail / 0.006f);
            return e;
        }

        private static float[] Buf(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];
        private static int S(float seconds) => Mathf.RoundToInt(seconds * Rate);
        private static int Mod(int a, int m) => ((a % m) + m) % m;
        private static float Midi(int m) => 440f * Mathf.Pow(2f, (m - 69) / 12f);

        private static void LowpassAll(float[] d, float fc)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * fc / Rate);
            float y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                y += (d[i] - y) * a;
                d[i] = y;
            }
        }

        /// <summary>Normalises to full scale, then applies tanh saturation (glues transients, rounds peaks).</summary>
        private static void Soft(float[] d, float drive)
        {
            Normalize(d, 0f);
            float norm = 1f / (float)Math.Tanh(drive);
            for (int i = 0; i < d.Length; i++) d[i] = (float)Math.Tanh(drive * d[i]) * norm;
        }

        private static void Normalize(float[] d, float peakDb)
        {
            float peak = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float a = Math.Abs(d[i]);
                if (a > peak) peak = a;
            }
            if (peak < 1e-6f) return;
            float g = Mathf.Pow(10f, peakDb / 20f) / peak;
            for (int i = 0; i < d.Length; i++) d[i] *= g;
        }

        /// <summary>One-shot finishing: DC block, click-free edges, peak normalise.</summary>
        private static float[] Finish(float[] d, float peakDb)
        {
            float prevX = 0f, prevY = 0f;
            const float R = 0.994f;
            for (int i = 0; i < d.Length; i++)
            {
                float x = d[i];
                float y = x - prevX + R * prevY;
                prevX = x;
                prevY = y;
                d[i] = y;
            }
            int fadeIn = Mathf.Min(16, d.Length), fadeOut = Mathf.Min(660, d.Length / 6);
            for (int i = 0; i < fadeIn; i++) d[i] *= i / (float)fadeIn;
            for (int i = 0; i < fadeOut; i++) d[d.Length - 1 - i] *= i / (float)fadeOut;
            Normalize(d, peakDb);
            return d;
        }

        /// <summary>Folds everything rendered past loopLen back onto the start, so ringing tails continue across the seam.</summary>
        private static float[] WrapLoop(float[] full, int loopLen)
        {
            var loop = new float[loopLen];
            Array.Copy(full, loop, Mathf.Min(loopLen, full.Length));
            for (int i = loopLen; i < full.Length; i++) loop[(i - loopLen) % loopLen] += full[i];
            return loop;
        }

        // =====================================================================================  WAV + import

        private static AudioClip Save(string assetPath, float[] data, Kind kind)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string fullPath = Path.Combine(projectRoot, assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, EncodeWav(data, Rate));

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (imp != null)
            {
                imp.forceToMono = true;
                imp.loadInBackground = kind != Kind.Sfx;
                AudioImporterSampleSettings s = imp.defaultSampleSettings;
                s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                float seconds = data.Length / (float)Rate;
                switch (kind)
                {
                    case Kind.Music:
                        s.loadType = AudioClipLoadType.Streaming;
                        s.compressionFormat = AudioCompressionFormat.Vorbis;
                        s.quality = 0.5f;
                        s.preloadAudioData = false;
                        break;
                    case Kind.Ambience:
                        s.loadType = AudioClipLoadType.CompressedInMemory;
                        s.compressionFormat = AudioCompressionFormat.Vorbis;
                        s.quality = 0.4f;
                        s.preloadAudioData = true;
                        break;
                    default:
                        s.loadType = AudioClipLoadType.DecompressOnLoad;
                        s.compressionFormat = seconds < 1.5f ? AudioCompressionFormat.ADPCM : AudioCompressionFormat.Vorbis;
                        s.quality = 0.5f;
                        s.preloadAudioData = true;
                        break;
                }
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        }

        private static byte[] EncodeWav(float[] data, int rate)
        {
            int dataBytes = data.Length * 2;
            var bytes = new byte[44 + dataBytes];
            using (var ms = new MemoryStream(bytes))
            using (var w = new BinaryWriter(ms))
            {
                w.Write((byte)'R'); w.Write((byte)'I'); w.Write((byte)'F'); w.Write((byte)'F');
                w.Write(36 + dataBytes);
                w.Write((byte)'W'); w.Write((byte)'A'); w.Write((byte)'V'); w.Write((byte)'E');
                w.Write((byte)'f'); w.Write((byte)'m'); w.Write((byte)'t'); w.Write((byte)' ');
                w.Write(16);
                w.Write((short)1);      // PCM
                w.Write((short)1);      // mono
                w.Write(rate);
                w.Write(rate * 2);      // byte rate
                w.Write((short)2);      // block align
                w.Write((short)16);     // bits
                w.Write((byte)'d'); w.Write((byte)'a'); w.Write((byte)'t'); w.Write((byte)'a');
                w.Write(dataBytes);
                for (int i = 0; i < data.Length; i++)
                {
                    float v = data[i];
                    if (float.IsNaN(v)) v = 0f;
                    w.Write((short)Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * 32767f));
                }
            }
            return bytes;
        }

        /// <summary>Creates every missing folder of an "Assets/..." path through the AssetDatabase.</summary>
        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string[] parts = assetFolder.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    if (Directory.Exists(Path.Combine(projectRoot, next))) AssetDatabase.ImportAsset(next);
                    else AssetDatabase.CreateFolder(cur, parts[i]);
                }
                cur = next;
            }
        }
    }
}
