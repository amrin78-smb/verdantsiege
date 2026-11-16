using System;
using VerdantSiege.Audio;

namespace VerdantSiege.EditorTools
{
    /// <summary>Milestone 3 procedural sound effects (same synthesis kit as the Milestone 1 sounds; deterministic).</summary>
    public static partial class AudioSynth
    {
        private static void BuildM3Sfx(AudioLibrary lib, Action<string> step)
        {
            step("M3"); Set(lib, Sfx.Snap, "snap", SnapSfx(), 0.75f, 2, 0.1f);
            step("M3"); Set(lib, Sfx.TrapErupt, "trap_erupt", TrapEruptSfx(), 0.8f, 2, 0.1f);
            step("M3"); Set(lib, Sfx.Heal, "heal", HealSfx(), 0.45f, 2, 0.35f);
            step("M3"); Set(lib, Sfx.Zap, "zap", ZapSfx(), 0.55f, 3, 0.08f);
            step("M3"); Set(lib, Sfx.FrostHit, "frost_hit", FrostHitSfx(), 0.45f, 3, 0.06f);
            step("M3"); Set(lib, Sfx.EmberBurst, "ember_burst", EmberBurstSfx(), 0.6f, 3, 0.08f);
            step("M3"); Set(lib, Sfx.Reflect, "reflect", ReflectSfx(), 0.6f, 2, 0.1f);
            step("M3"); Set(lib, Sfx.Howl, "howl", HowlSfx(), 0.6f, 1, 0.6f);
            step("M3"); Set(lib, Sfx.Spit, "spit", SpitSfx(), 0.5f, 3, 0.1f);
            step("M3"); Set(lib, Sfx.Leap, "leap", LeapSfx(), 0.5f, 2, 0.1f);
            step("M3"); Set(lib, Sfx.Burrow, "burrow", BurrowSfx(), 0.6f, 2, 0.2f);
            step("M3"); Set(lib, Sfx.EliteArrive, "elite_arrive", EliteArriveSfx(), 0.85f, 1, 1f);
            step("M3"); Set(lib, Sfx.BossRoar, "boss_roar", BossRoarSfx(), 0.95f, 1, 1f);
            step("M3"); Set(lib, Sfx.BossRumble, "boss_rumble", BossRumbleSfx(), 0.8f, 1, 0.5f);
            step("M3"); Set(lib, Sfx.RootStrike, "root_strike", RootStrikeSfx(), 0.9f, 2, 0.2f);
        }

        private static float[] SnapSfx()
        {
            var r = new Rng(301);
            var d = Buf(0.35f);
            Noise(d, r, 0f, 0.03f, 0.9f, 0.0003f, 120f, 3000f, 1500f, 1f, BP);
            Tone(d, 0f, 0.12f, 220f, 90f, 0.9f, 0.0008f, 26f, 0.4f, 0.2f);
            Noise(d, r, 0.06f, 0.25f, 0.5f, 0.004f, 14f, 900f, 400f, 0.7f, LP);
            Soft(d, 1.8f);
            return Finish(d, -1.5f);
        }

        private static float[] TrapEruptSfx()
        {
            var r = new Rng(302);
            var d = Buf(0.6f);
            Tone(d, 0f, 0.4f, 95f, 45f, 1f, 0.001f, 10f, 0.5f, 0.3f);
            Noise(d, r, 0f, 0.3f, 0.8f, 0.001f, 12f, 2500f, 500f, 0.6f, LP);
            for (int k = 0; k < 6; k++) Tone(d, 0.02f + k * 0.025f, 0.06f, 700f + k * 120f, 500f, 0.25f, 0.0005f, 60f);
            Rustle(d, r, 0.03f, 0.3f, 0.5f, 1800f, 1200f);
            Soft(d, 1.8f);
            return Finish(d, -1.5f);
        }

        private static float[] HealSfx()
        {
            var d = Buf(0.9f);
            Bell(d, 0f, 784f, 0.5f, 3f);
            Bell(d, 0.08f, 988f, 0.45f, 3f);
            Bell(d, 0.16f, 1175f, 0.4f, 3.2f);
            Tone(d, 0f, 0.8f, 392f, 392f, 0.2f, 0.05f, 3f);
            return Finish(d, -3f);
        }

        private static float[] ZapSfx()
        {
            var r = new Rng(304);
            var d = Buf(0.32f);
            for (int k = 0; k < 9; k++)
            {
                float t = k * 0.028f + r.Range(0f, 0.01f);
                Noise(d, r, t, 0.03f, 0.7f, 0.0002f, 150f, 6000f, 3500f, 1.2f, HP);
                Tone(d, t, 0.03f, r.Range(1200f, 2400f), 900f, 0.25f, 0.0002f, 120f);
            }
            Tone(d, 0f, 0.3f, 120f, 110f, 0.3f, 0.002f, 12f, 0.5f, 0.4f);
            Soft(d, 2.2f);
            return Finish(d, -2f);
        }

        private static float[] FrostHitSfx()
        {
            var r = new Rng(305);
            var d = Buf(0.3f);
            Tone(d, 0f, 0.25f, 2100f, 1900f, 0.4f, 0.0005f, 18f);
            Tone(d, 0f, 0.2f, 3150f, 2900f, 0.25f, 0.0005f, 26f);
            Noise(d, r, 0f, 0.06f, 0.5f, 0.0003f, 60f, 5000f, 3000f, 1f, HP);
            return Finish(d, -2f);
        }

        private static float[] EmberBurstSfx()
        {
            var r = new Rng(306);
            var d = Buf(0.55f);
            Noise(d, r, 0f, 0.5f, 1f, 0.002f, 7f, 1800f, 400f, 0.6f, LP);
            Tone(d, 0f, 0.2f, 140f, 70f, 0.7f, 0.001f, 18f, 0.4f);
            Rustle(d, r, 0.05f, 0.45f, 0.45f, 3000f, 2200f);
            Soft(d, 1.6f);
            return Finish(d, -1.5f);
        }

        private static float[] ReflectSfx()
        {
            var d = Buf(0.45f);
            Bell(d, 0f, 1568f, 0.5f, 6f);
            Tone(d, 0f, 0.3f, 600f, 1400f, 0.35f, 0.001f, 10f);
            return Finish(d, -2f);
        }

        private static float[] HowlSfx()
        {
            var r = new Rng(308);
            var d = Buf(1.1f);
            Tone(d, 0f, 1.05f, 260f, 420f, 0.6f, 0.15f, 2.2f, 0.35f, 0.2f);
            Tone(d, 0.1f, 0.95f, 390f, 630f, 0.3f, 0.15f, 2.5f, 0.2f);
            Noise(d, r, 0f, 1.0f, 0.25f, 0.2f, 2.5f, 900f, 1300f, 0.4f, BP);
            Soft(d, 1.5f);
            return Finish(d, -1.5f);
        }

        private static float[] SpitSfx()
        {
            var r = new Rng(309);
            var d = Buf(0.3f);
            Noise(d, r, 0f, 0.12f, 0.8f, 0.002f, 25f, 1600f, 700f, 0.5f, BP);
            Tone(d, 0f, 0.15f, 420f, 180f, 0.5f, 0.002f, 25f, 0.3f);
            Noise(d, r, 0.08f, 0.2f, 0.3f, 0.01f, 15f, 2400f, 1800f, 0.8f, BP);
            return Finish(d, -2f);
        }

        private static float[] LeapSfx()
        {
            var r = new Rng(310);
            var d = Buf(0.4f);
            Tone(d, 0f, 0.35f, 240f, 720f, 0.5f, 0.002f, 8f, 0.2f);
            Noise(d, r, 0f, 0.3f, 0.35f, 0.01f, 9f, 900f, 2600f, 0.6f, BP);
            return Finish(d, -2f);
        }

        private static float[] BurrowSfx()
        {
            var r = new Rng(311);
            var d = Buf(0.8f);
            Noise(d, r, 0f, 0.75f, 0.9f, 0.05f, 4f, 600f, 250f, 0.6f, LP);
            Rustle(d, r, 0f, 0.7f, 0.5f, 900f, 600f);
            Tone(d, 0f, 0.6f, 60f, 45f, 0.5f, 0.05f, 5f, 0.5f);
            Soft(d, 1.6f);
            return Finish(d, -1.5f);
        }

        private static float[] EliteArriveSfx()
        {
            var r = new Rng(312);
            var d = Buf(1.4f);
            Tone(d, 0f, 1.3f, 110f, 104f, 0.8f, 0.08f, 2f, 0.6f, 0.4f);
            Tone(d, 0f, 1.3f, 165f, 156f, 0.5f, 0.08f, 2f, 0.4f, 0.2f);
            Tone(d, 0.35f, 0.9f, 220f, 208f, 0.45f, 0.06f, 2.5f, 0.5f);
            Noise(d, r, 0f, 0.4f, 0.4f, 0.002f, 8f, 500f, 200f, 0.6f, LP);
            Soft(d, 1.8f);
            return Finish(d, -1f);
        }

        private static float[] BossRoarSfx()
        {
            var r = new Rng(313);
            var d = Buf(2.0f);
            Tone(d, 0f, 1.9f, 70f, 52f, 1f, 0.2f, 1.4f, 0.7f, 0.5f);
            Tone(d, 0.05f, 1.8f, 105f, 80f, 0.6f, 0.2f, 1.6f, 0.5f, 0.3f);
            Noise(d, r, 0f, 1.9f, 0.6f, 0.25f, 1.5f, 700f, 300f, 0.5f, BP);
            Noise(d, r, 0f, 0.5f, 0.6f, 0.005f, 6f, 300f, 120f, 0.7f, LP);
            Soft(d, 2.4f);
            return Finish(d, -0.8f);
        }

        private static float[] BossRumbleSfx()
        {
            var r = new Rng(314);
            var d = Buf(1.6f);
            Noise(d, r, 0f, 1.55f, 1f, 0.3f, 1.5f, 220f, 160f, 0.7f, LP);
            Tone(d, 0f, 1.5f, 48f, 42f, 0.7f, 0.3f, 1.6f, 0.6f, 0.3f);
            Rustle(d, r, 0.2f, 1.2f, 0.25f, 500f, 300f);
            Soft(d, 1.6f);
            return Finish(d, -1.5f);
        }

        private static float[] RootStrikeSfx()
        {
            var r = new Rng(315);
            var d = Buf(0.9f);
            Tone(d, 0f, 0.8f, 85f, 38f, 1f, 0.001f, 5f, 0.6f, 0.4f);
            Noise(d, r, 0f, 0.6f, 0.9f, 0.001f, 7f, 3000f, 400f, 0.5f, LP);
            Rustle(d, r, 0f, 0.5f, 0.6f, 2200f, 1600f);
            for (int k = 0; k < 8; k++) Tone(d, 0.03f + k * 0.03f, 0.05f, 900f - k * 60f, 600f, 0.2f, 0.0005f, 70f);
            Soft(d, 2f);
            return Finish(d, -1f);
        }
    }
}
