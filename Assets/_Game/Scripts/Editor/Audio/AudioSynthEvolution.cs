using System;
using VerdantSiege.Audio;

namespace VerdantSiege.EditorTools
{
    /// <summary>Living Evolution procedural sound effects (same synthesis kit; deterministic).</summary>
    public static partial class AudioSynth
    {
        private static void BuildEvolutionSfx(AudioLibrary lib, Action<string> step)
        {
            step("Evolution"); Set(lib, Sfx.EvolveReady, "evolve_ready", EvolveReadySfx(), 0.55f, 1, 0.6f);
            step("Evolution"); Set(lib, Sfx.Evolve, "evolve", EvolveSfx(), 0.9f, 1, 0.3f);
            step("Evolution"); Set(lib, Sfx.CapacitorRelease, "capacitor_release", CapacitorReleaseSfx(), 0.75f, 2, 0.2f);
        }

        /// <summary>A gentle rising chime: "something can grow here".</summary>
        private static float[] EvolveReadySfx()
        {
            var d = Buf(0.9f);
            Bell(d, 0f, 659f, 0.4f, 3.5f);
            Bell(d, 0.09f, 880f, 0.4f, 3.5f);
            Bell(d, 0.18f, 1319f, 0.35f, 3f);
            Swell(d, 0f, 0.5f, 330f, 0.12f);
            return Finish(d, -4f);
        }

        /// <summary>The evolution moment: a leafy whoosh swelling into a bright major chord with a sparkle tail.</summary>
        private static float[] EvolveSfx()
        {
            var r = new Rng(901);
            var d = Buf(1.8f);
            Noise(d, r, 0f, 0.5f, 0.35f, 0.25f, 4f, 400f, 3200f, 0.8f, BP); // rising rustle
            Rustle(d, r, 0.05f, 0.45f, 0.35f, 2400f, 900f);
            Swell(d, 0f, 0.55f, 196f, 0.35f);
            Tone(d, 0.42f, 0.25f, 110f, 70f, 0.6f, 0.002f, 9f, 0.4f, 0.2f); // soft bloom thump
            float[] chord = { 523.25f, 659.25f, 783.99f, 1046.5f };
            for (int k = 0; k < chord.Length; k++) Bell(d, 0.45f + k * 0.035f, chord[k], 0.32f, 1.6f);
            Pad(d, 0.45f, 0.6f, 261.63f, 0.18f, 0.03f, 0.6f);
            for (int k = 0; k < 8; k++) Bell(d, 0.6f + k * 0.07f, 1568f + k * 180f, 0.08f, 5f); // sparkle tail
            Soft(d, 1.4f);
            return Finish(d, -1.5f);
        }

        /// <summary>A charged hum released as a bright burst.</summary>
        private static float[] CapacitorReleaseSfx()
        {
            var r = new Rng(902);
            var d = Buf(0.9f);
            Tone(d, 0f, 0.25f, 220f, 880f, 0.3f, 0.05f, 2f, 0.3f);
            Noise(d, r, 0.22f, 0.3f, 0.5f, 0.001f, 10f, 3000f, 800f, 0.7f, LP);
            Bell(d, 0.22f, 1047f, 0.45f, 3f);
            Bell(d, 0.26f, 1568f, 0.35f, 3.5f);
            Tone(d, 0.22f, 0.3f, 130f, 80f, 0.5f, 0.002f, 10f, 0.3f);
            Soft(d, 1.5f);
            return Finish(d, -2f);
        }
    }
}
