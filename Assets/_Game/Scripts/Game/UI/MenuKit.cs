using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VerdantSiege.Data;
using VerdantSiege.Progression;
using VerdantSiege.Save;

namespace VerdantSiege.UI
{
    /// <summary>
    /// Shared building blocks for the Sanctuary screens (Milestone 4): full-screen frames with a top bar, currency
    /// chips, stars, defender tiles, mastery pips. Built on <see cref="UIKit"/> so everything keeps one look.
    /// All sizes are in the 1920x1080 reference space (phones scale by height).
    /// </summary>
    public static class MenuKit
    {
        public const float TopBarHeight = 118f;
        public static readonly Color EssenceColor = new Color(0.78f, 0.55f, 1f);
        public static readonly Color StarOff = new Color(1f, 1f, 1f, 0.16f);
        public static readonly Color LockedGrey = new Color(0.32f, 0.35f, 0.33f, 1f);
        public static readonly Color Backdrop = new Color(0.05f, 0.11f, 0.08f, 0.94f);

        private static Sprite S(Func<Visual.VisualLibrary, Sprite> f) => UIKit.Lib != null ? f(UIKit.Lib) : null;
        public static Sprite Round => S(l => l.uiRound);
        public static Sprite Circle => S(l => l.uiCircle);

        /// <summary>
        /// A full-screen screen (blocks input behind it) with a safe-area content root and a top bar: back button,
        /// title and the currency chips. Returns the screen root; <paramref name="content"/> is the safe area below the bar.
        /// </summary>
        public static GameObject Frame(Transform canvas, string name, string title, Action back, out RectTransform safe, out CurrencyBar currency)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            UIKit.Stretch((RectTransform)go.transform);
            Image bg = go.AddComponent<Image>();
            bg.color = Backdrop;
            bg.raycastTarget = true;
            // Soft vignette band at the top for the bar.
            Image band = UIKit.Image("Band", go.transform, S(l => l.uiGradient), new Color(0.12f, 0.26f, 0.18f, 0.9f));
            band.rectTransform.anchorMin = new Vector2(0f, 1f);
            band.rectTransform.anchorMax = new Vector2(1f, 1f);
            band.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            band.rectTransform.anchoredPosition = new Vector2(0f, -130f);
            band.rectTransform.sizeDelta = new Vector2(0f, 260f);
            band.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f); // opaque at the top, fading downward
            RectTransform safeArea = UIKit.SafeArea(go.transform);
            safe = UIKit.Rect("Content", safeArea);
            safe.gameObject.AddComponent<WidthFit>();

            Button b = UIKit.Button("Back", safe, null, UIPalette.ButtonSecondary, back, S(l => l.iconHome));
            UIKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(28f, -18f), new Vector2(96f, 88f));
            TextMeshProUGUI t = UIKit.Text("Title", safe, title, 64f, UIPalette.Parchment, TextAlignmentOptions.MidlineLeft, true);
            UIKit.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(146f, -18f), new Vector2(1100f, 88f));
            t.enableAutoSizing = true;
            t.fontSizeMin = 40f;
            t.fontSizeMax = 64f;
            Shadowed(t);
            currency = CurrencyBar.Create(safe);
            return go;
        }

        public static void Shadowed(Graphic g)
        {
            var sh = g.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0.02f, 0.06f, 0.03f, 0.75f);
            sh.effectDistance = new Vector2(2f, -3f);
        }

        /// <summary>Rotated square gem used for Essence everywhere.</summary>
        public static Image EssenceGem(Transform parent, float size)
        {
            Image gem = UIKit.Image("Gem", parent, Round, EssenceColor);
            gem.rectTransform.sizeDelta = new Vector2(size * 0.74f, size * 0.74f);
            gem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image shine = UIKit.Image("Shine", gem.transform, Circle, new Color(1f, 1f, 1f, 0.55f));
            UIKit.Place(shine.rectTransform, new Vector2(0.3f, 0.7f), Vector2.zero, new Vector2(size * 0.2f, size * 0.2f));
            return gem;
        }

        /// <summary>A pill with an icon and a value ("120" Seeds, "3" Essence).</summary>
        public static TextMeshProUGUI Chip(Transform parent, string name, bool essence, string value, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            Image chip = UIKit.Panel(name, parent, essence ? new Color(0.2f, 0.12f, 0.28f, 0.95f) : UIPalette.Panel, true, false);
            UIKit.Place(chip.rectTransform, anchor, pos, size);
            float icon = size.y * 0.72f;
            if (essence)
            {
                RectTransform holder = UIKit.Rect("IconHolder", chip.transform);
                UIKit.Place(holder, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(icon, icon));
                EssenceGem(holder, icon).rectTransform.anchoredPosition = Vector2.zero;
            }
            else
            {
                Image si = UIKit.Image("Icon", chip.transform, S(l => l.iconSeed), UIPalette.Gold);
                si.preserveAspect = true;
                UIKit.Place(si.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(icon, icon));
            }
            TextMeshProUGUI t = UIKit.Text("Amount", chip.transform, value, size.y * 0.52f, essence ? EssenceColor : UIPalette.Gold, TextAlignmentOptions.MidlineRight, true);
            UIKit.Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(icon + 14f, 0f);
            t.rectTransform.offsetMax = new Vector2(-18f, 0f);
            return t;
        }

        /// <summary>Three stars (win, flawless, challenge) from the record's star bits.</summary>
        public static Image[] Stars(Transform parent, int bits, float size, float spacing, Vector2 anchor, Vector2 pos)
        {
            RectTransform row = UIKit.Rect("Stars", parent);
            UIKit.Place(row, anchor, pos, new Vector2(size * 3f + spacing * 2f, size));
            var stars = new Image[3];
            int[] mask = { EncounterRecord.StarWin, EncounterRecord.StarFlawless, EncounterRecord.StarChallenge };
            for (int i = 0; i < 3; i++)
            {
                bool on = (bits & mask[i]) != 0;
                Image st = UIKit.Image("Star" + i, row, S(l => l.iconStar), on ? UIPalette.Gold : StarOff);
                st.preserveAspect = true;
                UIKit.Place(st.rectTransform, new Vector2(0f, 0.5f), new Vector2(i * (size + spacing), 0f), new Vector2(size, size));
                if (on) Shadowed(st);
                stars[i] = st;
            }
            return stars;
        }

        /// <summary>
        /// A defender portrait: accent disc with the card icon (or a monogram), optional lock. Size is the disc diameter.
        /// </summary>
        public static RectTransform Portrait(Transform parent, DefenderDefinition d, float size, bool locked)
        {
            RectTransform root = UIKit.Rect("Portrait", parent);
            root.sizeDelta = new Vector2(size, size);
            Color accent = d != null ? d.accentColor : Color.grey;
            Image ring = UIKit.Image("Ring", root, Circle, locked ? LockedGrey : Color.Lerp(accent, Color.white, 0.25f));
            UIKit.Stretch(ring.rectTransform);
            Image disc = UIKit.Image("Disc", root, Circle, locked ? new Color(0.12f, 0.14f, 0.13f) : Color.Lerp(accent, new Color(0.08f, 0.16f, 0.1f), 0.55f));
            UIKit.Stretch(disc.rectTransform, size * 0.06f);
            if (d != null && d.icon != null)
            {
                Image ic = UIKit.Image("Icon", root, d.icon, locked ? new Color(0f, 0f, 0f, 0.85f) : Color.white);
                ic.preserveAspect = true;
                UIKit.Stretch(ic.rectTransform, size * 0.1f);
            }
            else if (d != null)
            {
                TextMeshProUGUI m = UIKit.Text("Mono", root, d.displayName.Substring(0, 1), size * 0.5f,
                    locked ? new Color(1f, 1f, 1f, 0.25f) : Color.Lerp(accent, Color.white, 0.5f), TextAlignmentOptions.Center, true);
                UIKit.Stretch(m.rectTransform);
            }
            if (locked)
            {
                Image lk = UIKit.Image("Lock", root, S(l => l.iconLock), UIPalette.Parchment);
                lk.preserveAspect = true;
                UIKit.Place(lk.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.42f, size * 0.42f));
            }
            return root;
        }

        /// <summary>Mastery level as 4 pips (filled up to the level).</summary>
        public static RectTransform MasteryPips(Transform parent, int level, float pip, Vector2 anchor, Vector2 pos)
        {
            RectTransform row = UIKit.Rect("Mastery", parent);
            float gap = pip * 0.35f;
            UIKit.Place(row, anchor, pos, new Vector2(MasteryRules.MaxLevel * pip + (MasteryRules.MaxLevel - 1) * gap, pip));
            for (int i = 0; i < MasteryRules.MaxLevel; i++)
            {
                Image p = UIKit.Image("Pip" + i, row, S(l => l.iconLeaf), i < level ? UIPalette.Gold : new Color(1f, 1f, 1f, 0.18f));
                p.preserveAspect = true;
                UIKit.Place(p.rectTransform, new Vector2(0f, 0.5f), new Vector2(i * (pip + gap), 0f), new Vector2(pip, pip));
            }
            return row;
        }

        /// <summary>A thin horizontal progress bar; returns the fill image (Filled, horizontal).</summary>
        public static Image Bar(Transform parent, string name, Color fillColor, Vector2 anchor, Vector2 pos, Vector2 size, float value)
        {
            Image track = UIKit.Image(name, parent, Round, new Color(0f, 0f, 0f, 0.45f));
            UIKit.Place(track.rectTransform, anchor, pos, size);
            Image fill = UIKit.Image("Fill", track.transform, Round, fillColor);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = Mathf.Clamp01(value);
            UIKit.Stretch(fill.rectTransform, 2f);
            return fill;
        }

        /// <summary>Small transient message near the bottom of a screen (unscaled time).</summary>
        public static void Toast(Transform parent, string text, Color color)
        {
            Image bg = UIKit.Panel("Toast", parent, new Color(0.08f, 0.14f, 0.1f, 0.96f), true, true);
            bg.raycastTarget = false;
            UIKit.Place(bg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1000f, 84f));
            TextMeshProUGUI t = UIKit.Text("Text", bg.transform, text, 34f, color, TextAlignmentOptions.Center, true);
            UIKit.Stretch(t.rectTransform, 10f);
            bg.gameObject.AddComponent<FadeAway>().life = 2.6f;
            PopIn.Play(bg.gameObject);
        }
    }

    /// <summary>Seeds and Essence chips in the top-right corner of a screen; call Refresh after a purchase.</summary>
    public sealed class CurrencyBar : MonoBehaviour
    {
        private TextMeshProUGUI _seeds, _essence;

        public static CurrencyBar Create(Transform safe)
        {
            RectTransform root = UIKit.Rect("Currency", safe);
            UIKit.Place(root, new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(560f, 84f));
            var bar = root.gameObject.AddComponent<CurrencyBar>();
            bar._seeds = MenuKit.Chip(root, "Seeds", false, "0", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(290f, 84f));
            bar._essence = MenuKit.Chip(root, "Essence", true, "0", new Vector2(1f, 0.5f), Vector2.zero, new Vector2(250f, 84f));
            bar.Refresh();
            return bar;
        }

        public void Refresh()
        {
            App.AppRoot app = App.AppRoot.Instance;
            if (app == null) return;
            _seeds.text = app.Save.Data.seeds.ToString();
            _essence.text = app.Save.Data.essence.ToString();
        }
    }

    /// <summary>
    /// Keeps a 1920-wide layout usable on narrower canvases (4:3 / 5:3 tablets scale partly by width): fills its
    /// parent, and when the parent is narrower than <see cref="minWidth"/> it lays out at minWidth and scales down.
    /// </summary>
    public sealed class WidthFit : MonoBehaviour
    {
        public float minWidth = 1880f;
        private Vector2 _last;

        private void OnEnable() => Apply();
        private void LateUpdate() => Apply();

        private void Apply()
        {
            var rt = (RectTransform)transform;
            if (!(rt.parent is RectTransform parent)) return;
            Vector2 size = parent.rect.size;
            if (size == _last || size.x <= 0f) return;
            _last = size;
            float s = Mathf.Min(1f, size.x / minWidth);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size / s;
            rt.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Fades a toast out and destroys it.</summary>
    public sealed class FadeAway : MonoBehaviour
    {
        public float life = 2.5f;
        private float _t;
        private CanvasGroup _g;

        private void Start() => _g = UIKit.Group(gameObject);

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            if (_g != null && _t > life - 0.5f) _g.alpha = Mathf.Clamp01((life - _t) / 0.5f);
            if (_t >= life) Destroy(gameObject);
        }
    }

    /// <summary>Gentle scale pulse (available mission nodes, new rewards). Unscaled time.</summary>
    public sealed class Pulse : MonoBehaviour
    {
        public float amount = 0.06f;
        public float speed = 3f;
        private Vector3 _base;

        private void Start() => _base = transform.localScale;

        private void Update()
        {
            float s = 1f + amount * Mathf.Sin(Time.unscaledTime * speed);
            transform.localScale = _base * s;
        }
    }
}
