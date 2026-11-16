using System;
using UnityEngine;
using VerdantSiege.Visual;

namespace VerdantSiege.EditorTools
{
    /// <summary>
    /// Original UI sprites drawn procedurally (4x4 supersampled shape functions): rounded 9-slice panels and a
    /// coherent icon set. Icons are white (tinted in UI) except the coloured Vitality sun-seed.
    /// </summary>
    public static class UISpriteBuilder
    {
        private const string Dir = EditorAssets.Root + "UI/Sprites/";

        public static void BuildAll(VisualLibrary lib)
        {
            lib.uiRound = Rounded("UI_Round", 96, 30f, false, 34);
            lib.uiRoundOutline = Rounded("UI_RoundOutline", 96, 30f, true, 34);
            lib.uiCircle = Icon("UI_Circle", (x, y) => x * x + y * y <= 0.92f);
            lib.uiShadow = SoftShadow();
            lib.uiGradient = Gradient();
            lib.iconVitality = Vitality();
            lib.iconHeart = Icon("Icon_Heart", Heart);
            lib.iconPause = Icon("Icon_Pause", (x, y) => RoundRect(x + 0.32f, y, 0.16f, 0.62f, 0.08f) || RoundRect(x - 0.32f, y, 0.16f, 0.62f, 0.08f));
            lib.iconPlay = Icon("Icon_Play", (x, y) => Tri(x, y, new Vector2(-0.45f, -0.6f), new Vector2(-0.45f, 0.6f), new Vector2(0.62f, 0f)));
            lib.iconSpeed = Icon("Icon_Speed", (x, y) =>
                Tri(x, y, new Vector2(-0.8f, -0.5f), new Vector2(-0.8f, 0.5f), new Vector2(0.0f, 0f)) ||
                Tri(x, y, new Vector2(-0.05f, -0.5f), new Vector2(-0.05f, 0.5f), new Vector2(0.75f, 0f)));
            lib.iconFlag = Icon("Icon_Flag", (x, y) => RoundRect(x + 0.45f, y, 0.07f, 0.85f, 0.05f) || Pennant(x, y));
            lib.iconSeed = Icon("Icon_Seed", Seed);
            lib.iconGear = Icon("Icon_Gear", Gear);
            lib.iconRestart = Icon("Icon_Restart", Restart);
            lib.iconHome = Icon("Icon_Home", Home);
            lib.iconUndo = Icon("Icon_Undo", Undo);
            lib.iconClose = Icon("Icon_Close", (x, y) => Mathf.Abs(x - y) < 0.2f && Mathf.Abs(x) < 0.62f || Mathf.Abs(x + y) < 0.2f && Mathf.Abs(x) < 0.62f);
            lib.iconSkull = Icon("Icon_Blight", (x, y) => x * x + (y - 0.1f) * (y - 0.1f) < 0.42f && !((x + 0.25f) * (x + 0.25f) + (y - 0.1f) * (y - 0.1f) < 0.03f) && !((x - 0.25f) * (x - 0.25f) + (y - 0.1f) * (y - 0.1f) < 0.03f));
            lib.iconLeaf = Icon("Icon_Leaf", Leaf);
            // Milestone 4 campaign UI.
            lib.iconStar = Icon("Icon_Star", Star);
            lib.iconLock = Icon("Icon_Lock", Lock);
            lib.iconMapPin = Icon("Icon_MapPin", MapPin);
            lib.iconSprout = Icon("Icon_Sprout", Sprout);
        }

        private static bool InPolygon(float x, float y, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        private static Vector2[] _star;

        private static bool Star(float x, float y)
        {
            if (_star == null)
            {
                _star = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                    float r = i % 2 == 0 ? 0.95f : 0.42f;
                    _star[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r - 0.06f);
                }
            }
            return InPolygon(x, y, _star);
        }

        private static bool Lock(float x, float y)
        {
            bool body = RoundRect(x, y + 0.3f, 0.6f, 0.45f, 0.12f);
            float r = Mathf.Sqrt(x * x + (y - 0.18f) * (y - 0.18f));
            bool shackle = r > 0.27f && r < 0.43f && y > 0.1f;
            bool legs = (Mathf.Abs(x) > 0.27f && Mathf.Abs(x) < 0.43f && y > 0.1f && y <= 0.2f);
            bool hole = (x * x + (y + 0.22f) * (y + 0.22f) < 0.012f) || (Mathf.Abs(x) < 0.05f && y < -0.22f && y > -0.5f);
            return ((body && !hole) || shackle || legs);
        }

        private static bool MapPin(float x, float y)
        {
            float cy = y - 0.25f;
            bool head = x * x + cy * cy < 0.36f;
            bool point = Tri(x, y, new Vector2(-0.48f, 0.05f), new Vector2(0.48f, 0.05f), new Vector2(0f, -0.92f));
            bool hole = x * x + cy * cy < 0.05f;
            return (head || point) && !hole;
        }

        private static bool Sprout(float x, float y)
        {
            bool stem = Mathf.Abs(x + 0.02f * Mathf.Sin(y * 3f)) < 0.07f && y < 0.2f && y > -0.85f;
            float lx = x + 0.32f, ly = y - 0.25f;
            float u1 = (lx * 0.8f + ly * 0.6f), v1 = (-lx * 0.6f + ly * 0.8f);
            bool left = (u1 * u1) / 0.16f + (v1 * v1) / 0.03f <= 1f;
            float rx = x - 0.32f, ry = y - 0.42f;
            float u2 = (rx * 0.8f - ry * 0.6f), v2 = (rx * 0.6f + ry * 0.8f);
            bool right = (u2 * u2) / 0.18f + (v2 * v2) / 0.035f <= 1f;
            bool soil = RoundRect(x, y + 0.82f, 0.62f, 0.1f, 0.08f);
            return stem || left || right || soil;
        }

        // ------------------------------------------------------------------ shapes (x,y in [-1,1], y up)
        private static bool RoundRect(float x, float y, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(x) - (hw - r), qy = Mathf.Abs(y) - (hh - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r <= 0f;
        }

        private static bool Tri(float x, float y, Vector2 a, Vector2 b, Vector2 c)
        {
            float Sign(Vector2 p1, Vector2 p2, Vector2 p3) => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
            var p = new Vector2(x, y);
            float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        private static bool Pennant(float x, float y) => Tri(x, y, new Vector2(-0.4f, 0.82f), new Vector2(-0.4f, 0.05f), new Vector2(0.7f, 0.5f));

        private static bool Heart(float x, float y)
        {
            float px = x * 1.15f, py = (y + 0.1f) * 1.2f;
            float a = px * px + py * py - 0.55f;
            return a * a * a - px * px * py * py * py <= 0f;
        }

        private static bool Leaf(float x, float y)
        {
            // Rotated almond with a vein cut.
            float c = 0.7071f;
            float u = (x + y) * c, v = (y - x) * c;
            bool body = (u * u) / 0.72f + (v * v) / 0.18f <= 1f && Mathf.Abs(v) <= 0.45f * (1f - Mathf.Abs(u) * 0.9f) + 0.02f;
            bool vein = Mathf.Abs(v) < 0.035f && u > -0.55f && u < 0.55f;
            bool stem = Mathf.Abs(v) < 0.06f && u < -0.6f && u > -0.92f;
            return (body && !vein) || stem;
        }

        private static bool Seed(float x, float y)
        {
            float py = y + 0.05f;
            float w = 0.55f * Mathf.Sqrt(Mathf.Max(0f, 1f - py * py / 0.72f)) * (1f - py * 0.25f);
            bool body = Mathf.Abs(x) <= w && Mathf.Abs(py) < 0.85f;
            bool shine = (x + 0.18f) * (x + 0.18f) + (py - 0.25f) * (py - 0.25f) < 0.018f;
            return body && !shine;
        }

        private static bool Gear(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(y, x);
            float teeth = Mathf.Cos(a * 8f) > 0.35f ? 0.86f : 0.66f;
            return r <= teeth && r >= 0.26f;
        }

        private static bool Restart(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            bool ring = r > 0.45f && r < 0.7f && !(a > 50f && a < 100f);
            bool head = Tri(x, y, new Vector2(0.18f, 0.88f), new Vector2(0.18f, 0.28f), new Vector2(0.62f, 0.58f));
            return ring || head;
        }

        private static bool Home(float x, float y)
        {
            bool roof = Tri(x, y, new Vector2(-0.85f, 0.05f), new Vector2(0.85f, 0.05f), new Vector2(0f, 0.8f));
            bool body = x > -0.58f && x < 0.58f && y > -0.75f && y <= 0.1f;
            bool door = x > -0.15f && x < 0.15f && y > -0.75f && y < -0.25f;
            return (roof || body) && !door;
        }

        private static bool Undo(float x, float y)
        {
            float cx = x - 0.05f, cy = y + 0.15f;
            float r = Mathf.Sqrt(cx * cx + cy * cy);
            bool arc = r > 0.42f && r < 0.66f && cy > -0.05f;
            bool tail = cx > 0.42f && cx < 0.66f && cy > -0.65f && cy <= 0f;
            bool head = Tri(x, y, new Vector2(-0.9f, -0.08f), new Vector2(-0.35f, -0.08f), new Vector2(-0.62f, -0.55f));
            return arc || tail || head;
        }

        // ------------------------------------------------------------------ rasterisers
        private static Sprite Icon(string name, Func<float, float, bool> inside, int size = 128)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            const int ss = 4;
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                int hits = 0;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    float x = ((i + (sx + 0.5f) / ss) / size) * 2f - 1f;
                    float y = ((j + (sy + 0.5f) / ss) / size) * 2f - 1f;
                    if (inside(x, y)) hits++;
                }
                byte a = (byte)(255 * hits / (ss * ss));
                px[j * size + i] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return EditorAssets.SaveSprite(tex, Dir + name + ".png");
        }

        private static Sprite Rounded(string name, int size, float radius, bool outlineOnly, int border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float x = i + 0.5f - half, y = j + 0.5f - half;
                float qx = Mathf.Abs(x) - (half - radius), qy = Mathf.Abs(y) - (half - radius);
                float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius + 1f;
                float a = Mathf.Clamp01(0.5f - d);
                if (outlineOnly) a *= Mathf.Clamp01(d + 5.5f); // ~5 px ring
                // Subtle top-light bevel on filled panels.
                float shade = outlineOnly ? 1f : Mathf.Lerp(0.88f, 1f, (y + half) / size);
                byte c = (byte)(255 * shade);
                px[j * size + i] = new Color32(c, c, c, (byte)(255 * a));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return EditorAssets.SaveSprite(tex, Dir + name + ".png", new Vector4(border, border, border, border));
        }

        private static Sprite SoftShadow()
        {
            const int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size * 0.5f;
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float x = i + 0.5f - half, y = j + 0.5f - half;
                float qx = Mathf.Abs(x) - 16f, qy = Mathf.Abs(y) - 16f;
                float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                float a = Mathf.Clamp01(1f - d / 30f);
                tex.SetPixel(i, j, new Color(1f, 1f, 1f, a * a));
            }
            tex.Apply();
            return EditorAssets.SaveSprite(tex, Dir + "UI_SoftShadow.png", new Vector4(44, 44, 44, 44));
        }

        private static Sprite Gradient()
        {
            const int w = 8, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(1f - y / (h - 1f), 1.6f)));
            tex.Apply();
            return EditorAssets.SaveSprite(tex, Dir + "UI_Gradient.png");
        }

        private static Sprite Vitality()
        {
            const int size = 128, ss = 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var core = new Color(1f, 0.86f, 0.32f);
            var rim = new Color(1f, 0.58f, 0.16f);
            var ray = new Color(1f, 0.93f, 0.55f);
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                Color acc = Color.clear;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    float x = ((i + (sx + 0.5f) / ss) / size) * 2f - 1f;
                    float y = ((j + (sy + 0.5f) / ss) / size) * 2f - 1f;
                    float r = Mathf.Sqrt(x * x + y * y);
                    float a = Mathf.Atan2(y, x);
                    float rayLen = 0.62f + 0.3f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 4f)), 6f);
                    Color c = Color.clear;
                    if (r < 0.46f)
                    {
                        float k = r / 0.46f;
                        c = Color.Lerp(core, rim, k * k);
                        if ((x + 0.14f) * (x + 0.14f) + (y - 0.16f) * (y - 0.16f) < 0.012f) c = Color.white;
                    }
                    else if (r < rayLen) c = new Color(ray.r, ray.g, ray.b, 0.95f);
                    else if (r < 0.98f) c = new Color(1f, 0.8f, 0.3f, Mathf.Clamp01(0.35f * (1f - (r - rayLen) / 0.2f)));
                    acc += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a); // premultiplied
                }
                acc /= ss * ss;
                if (acc.a > 0.001f) { acc.r /= acc.a; acc.g /= acc.a; acc.b /= acc.a; }
                tex.SetPixel(i, j, acc);
            }
            tex.Apply();
            return EditorAssets.SaveSprite(tex, Dir + "Icon_Vitality.png");
        }
    }
}
