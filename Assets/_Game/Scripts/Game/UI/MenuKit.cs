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
        public const float TopBarHeight = 136f;

        // ---- readability minimums (TD-36), in 1920x1080 reference units. On phones the canvas matches height, so
        // 1080 units = the screen height: on a 6.5" 20:9 phone 34 units is about 2.3 mm of em height (TD-36, raised again for 0.5.2).
        /// <summary>Absolute floor for any menu text; TMP auto-size never goes below it.</summary>
        public const float TextMin = 34f;
        /// <summary>Hints, secondary lines, small captions.</summary>
        public const float TextSmall = 36f;
        /// <summary>Descriptions, briefing, body copy.</summary>
        public const float TextBody = 38f;
        /// <summary>Card titles and names.</summary>
        public const float TextLabel = 38f;
        /// <summary>Costs and counts that must read at a glance.</summary>
        public const float TextNumber = 42f;
        /// <summary>Section headers.</summary>
        public const float TextHeader = 52f;
        /// <summary>Button labels.</summary>
        public const float TextButton = 44f;
        /// <summary>Minimum touch target height (buttons, tabs).</summary>
        public const float TouchMin = 110f;
        public const float ButtonHeight = 116f;
        /// <summary>Outer margin and gap between screen columns.</summary>
        public const float Gutter = 28f;
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
            UIKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(28f, -12f), new Vector2(TouchMin + 8f, TouchMin));
            TextMeshProUGUI t = UIKit.Text("Title", safe, title, 70f, UIPalette.Parchment, TextAlignmentOptions.MidlineLeft, true);
            UIKit.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(166f, -16f), new Vector2(1000f, 104f));
            t.enableAutoSizing = true;
            t.fontSizeMin = 48f;
            t.fontSizeMax = 70f;
            Shadowed(t);
            currency = CurrencyBar.Create(safe);
            return go;
        }

        /// <summary>Auto-size between <paramref name="min"/> (never below <see cref="TextMin"/>) and <paramref name="max"/>.</summary>
        public static TextMeshProUGUI Fit(TextMeshProUGUI t, float max, float min = TextMin)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = Mathf.Max(TextMin, min);
            t.fontSizeMax = Mathf.Max(max, t.fontSizeMin);
            t.fontSize = t.fontSizeMax;
            return t;
        }

        /// <summary>Height the text needs at its current font size when wrapped to <paramref name="width"/>.</summary>
        public static float TextHeight(TextMeshProUGUI t, string text, float width) =>
            Mathf.Ceil(t.GetPreferredValues(string.IsNullOrEmpty(text) ? " " : text, width, 0f).y);

        /// <summary>
        /// A touch scroll area: the returned ScrollRect sits on <paramref name="name"/> (stretch it as needed) with a
        /// clipping viewport (RectMask2D), inertia and elastic edges, plus a <see cref="ScrollHint"/> (edge fades and a
        /// thin position bar that only show when there is more to see). Vertical content is top-anchored and stretches
        /// the viewport width; horizontal content is left-anchored and stretches its height. Size the content (or give it
        /// a ContentSizeFitter). Taps on buttons inside still click; a drag past the EventSystem threshold scrolls and
        /// cancels the press (see <see cref="UIKit.ConfigureEventSystem"/>).
        /// </summary>
        public static ScrollRect Scroll(Transform parent, string name, bool horizontal, out RectTransform content)
        {
            RectTransform root = UIKit.Rect(name, parent);
            RectTransform viewport = UIKit.Rect("Viewport", root);
            UIKit.Stretch(viewport);
            Image catcher = viewport.gameObject.AddComponent<Image>(); // drags on gaps between items scroll too
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            content = UIKit.Rect("Content", viewport);
            if (horizontal)
            {
                content.anchorMin = new Vector2(0f, 0f);
                content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, 0.5f);
            }
            else
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
            }
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var sr = root.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.viewport = viewport;
            sr.horizontal = horizontal;
            sr.vertical = !horizontal;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.elasticity = 0.08f;
            sr.inertia = true;
            sr.decelerationRate = 0.12f;
            sr.scrollSensitivity = 40f;
            root.gameObject.AddComponent<ScrollHint>().Init(sr);
            return sr;
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
            UIKit.Place(bg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1240f, 100f));
            TextMeshProUGUI t = UIKit.Text("Text", bg.transform, text, 40f, color, TextAlignmentOptions.Center, true);
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
            UIKit.Place(root, new Vector2(1f, 1f), new Vector2(-24f, -16f), new Vector2(660f, 100f));
            var bar = root.gameObject.AddComponent<CurrencyBar>();
            bar._seeds = MenuKit.Chip(root, "Seeds", false, "0", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(350f, 100f));
            bar._essence = MenuKit.Chip(root, "Essence", true, "0", new Vector2(1f, 0.5f), Vector2.zero, new Vector2(290f, 100f));
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

    /// <summary>
    /// Scroll affordance for <see cref="MenuKit.Scroll"/>: soft dark fades on the edges that have more content beyond
    /// them, and a thin position bar along the far edge. Hidden when everything fits. Purely visual (no raycasts).
    /// </summary>
    public sealed class ScrollHint : MonoBehaviour
    {
        private const float FadeSize = 64f;
        private static Sprite _rampV, _rampH;
        private ScrollRect _sr;
        private Image _fadeA, _fadeB, _track, _thumb;
        private bool _h;

        /// <summary>Colour of the edge fades (match a dark panel behind the list).</summary>
        public static readonly Color FadeColor = new Color(0.03f, 0.07f, 0.05f, 0.9f);

        private static Sprite Ramp(bool horizontal)
        {
            if (horizontal ? _rampH != null : _rampV != null) return horizontal ? _rampH : _rampV;
            const int n = 32;
            var tex = new Texture2D(horizontal ? n : 1, horizontal ? 1 : n, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "ScrollFade" };
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.SmoothStep(0f, 1f, i / (n - 1f)); // transparent at 0, opaque at the far end
                tex.SetPixel(horizontal ? i : 0, horizontal ? 0 : i, new Color(1f, 1f, 1f, a));
            }
            tex.Apply(false, true);
            Sprite s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            if (horizontal) _rampH = s; else _rampV = s;
            return s;
        }

        public void Init(ScrollRect sr)
        {
            _sr = sr;
            _h = sr.horizontal;
            var root = (RectTransform)transform;
            // A: start edge (top / left), B: end edge (bottom / right). Ramps are opaque at the high-coordinate end,
            // so the start fade is flipped with a negative scale.
            _fadeA = Fade(root, "FadeStart", true);
            _fadeB = Fade(root, "FadeEnd", false);
            _track = UIKit.Image("ScrollTrack", root, MenuKit.Round, new Color(1f, 1f, 1f, 0.1f));
            _thumb = UIKit.Image("ScrollThumb", _track.transform, MenuKit.Round, new Color(UIPalette.Parchment.r, UIPalette.Parchment.g, UIPalette.Parchment.b, 0.6f));
            RectTransform tr = _track.rectTransform;
            if (_h)
            {
                tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 0f); tr.pivot = new Vector2(0.5f, 0f);
                tr.offsetMin = new Vector2(12f, 2f); tr.offsetMax = new Vector2(-12f, 10f);
            }
            else
            {
                tr.anchorMin = new Vector2(1f, 0f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(1f, 0.5f);
                tr.offsetMin = new Vector2(-10f, 12f); tr.offsetMax = new Vector2(-2f, -12f);
            }
            Refresh();
        }

        private Image Fade(RectTransform root, string name, bool start)
        {
            Image f = UIKit.Image(name, root, Ramp(_h), FadeColor);
            RectTransform r = f.rectTransform;
            // Centre pivot so a mirrored (negative-scale) fade stays inside its edge band.
            r.pivot = new Vector2(0.5f, 0.5f);
            if (_h)
            {
                float x = start ? 0f : 1f;
                r.anchorMin = new Vector2(x, 0f); r.anchorMax = new Vector2(x, 1f);
                r.sizeDelta = new Vector2(FadeSize, 0f);
                r.anchoredPosition = new Vector2(start ? FadeSize * 0.5f : -FadeSize * 0.5f, 0f);
                if (start) r.localScale = new Vector3(-1f, 1f, 1f);
            }
            else
            {
                float y = start ? 1f : 0f;
                r.anchorMin = new Vector2(0f, y); r.anchorMax = new Vector2(1f, y);
                r.sizeDelta = new Vector2(0f, FadeSize);
                r.anchoredPosition = new Vector2(0f, start ? -FadeSize * 0.5f : FadeSize * 0.5f);
                if (!start) r.localScale = new Vector3(1f, -1f, 1f);
            }
            return f;
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (_sr == null || _sr.content == null || _sr.viewport == null) return;
            Rect vp = _sr.viewport.rect, ct = _sr.content.rect;
            float view = _h ? vp.width : vp.height, total = _h ? ct.width : ct.height;
            float overflow = total - view;
            bool scrolls = overflow > 2f;
            if (_track.gameObject.activeSelf != scrolls) _track.gameObject.SetActive(scrolls);
            if (!scrolls)
            {
                _fadeA.enabled = _fadeB.enabled = false;
                return;
            }
            // 0 = at the start (top / left), 1 = at the end.
            float p = _h ? _sr.horizontalNormalizedPosition : 1f - _sr.verticalNormalizedPosition;
            p = Mathf.Clamp01(p);
            float aStart = Mathf.Clamp01(p * overflow / 48f), aEnd = Mathf.Clamp01((1f - p) * overflow / 48f);
            _fadeA.enabled = aStart > 0.01f;
            _fadeB.enabled = aEnd > 0.01f;
            _fadeA.color = new Color(FadeColor.r, FadeColor.g, FadeColor.b, FadeColor.a * aStart);
            _fadeB.color = new Color(FadeColor.r, FadeColor.g, FadeColor.b, FadeColor.a * aEnd);
            float frac = Mathf.Clamp(view / total, 0.12f, 1f);
            RectTransform th = _thumb.rectTransform;
            if (_h)
            {
                th.anchorMin = new Vector2(p * (1f - frac), 0f);
                th.anchorMax = new Vector2(p * (1f - frac) + frac, 1f);
            }
            else
            {
                th.anchorMin = new Vector2(0f, 1f - (p * (1f - frac) + frac));
                th.anchorMax = new Vector2(1f, 1f - p * (1f - frac));
            }
            th.offsetMin = th.offsetMax = Vector2.zero;
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
