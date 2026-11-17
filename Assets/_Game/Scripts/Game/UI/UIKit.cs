using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VerdantSiege.Visual;

namespace VerdantSiege.UI
{
    /// <summary>Verdant Siege UI palette (warm moss, parchment and sun-gold; Blight magenta for threats).</summary>
    public static class UIPalette
    {
        public static readonly Color Panel = new Color32(0x16, 0x2A, 0x22, 0xE6);
        public static readonly Color PanelLight = new Color32(0x24, 0x40, 0x33, 0xF2);
        public static readonly Color PanelEdge = new Color32(0xD9, 0xB8, 0x6A, 0xFF);
        public static readonly Color Parchment = new Color32(0xF6, 0xEC, 0xD2, 0xFF);
        public static readonly Color TextDim = new Color32(0xC9, 0xD8, 0xC4, 0xFF);
        public static readonly Color Gold = new Color32(0xFF, 0xD3, 0x5C, 0xFF);
        public static readonly Color Vitality = new Color32(0xFF, 0xE0, 0x6B, 0xFF);
        public static readonly Color Leaf = new Color32(0x7C, 0xD6, 0x5A, 0xFF);
        public static readonly Color Danger = new Color32(0xFF, 0x5E, 0x6C, 0xFF);
        public static readonly Color Blight = new Color32(0xC0, 0x5C, 0xE0, 0xFF);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.45f);
        public static readonly Color ButtonPrimary = new Color32(0x4C, 0xA8, 0x4A, 0xFF);
        public static readonly Color ButtonSecondary = new Color32(0x35, 0x52, 0x46, 0xFF);
        public static readonly Color Dim = new Color(0.02f, 0.05f, 0.04f, 0.72f);
    }

    /// <summary>Code-built uGUI helpers so screens stay consistent and need no hand-authored prefabs.</summary>
    public static class UIKit
    {
        public static VisualLibrary Lib;

        public static Canvas CreateCanvas(string name, int sortOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            canvas.pixelPerfect = false;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 16f / 9f;
            scaler.matchWidthOrHeight = aspect >= 1.7f ? 1f : 0.35f; // phones scale with height, 4:3 tablets mostly width
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            ConfigureEventSystem();
            return canvas;
        }

        /// <summary>
        /// Drag threshold scaled to the screen density (~1.8 mm): on a 500 dpi phone the 10 px default turns a
        /// slightly wobbly tap into a drag, which cancels button clicks inside scroll lists.
        /// </summary>
        public static void ConfigureEventSystem()
        {
            EventSystem es = EventSystem.current;
            if (es == null) return;
            float dpi = Screen.dpi > 0f ? Screen.dpi : 96f;
            es.pixelDragThreshold = Mathf.Clamp(Mathf.RoundToInt(dpi * 0.07f), 10, 48);
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>Full-rect child that respects the device safe area (notches, rounded corners).</summary>
        public static RectTransform SafeArea(Transform canvas)
        {
            RectTransform rt = Rect("SafeArea", canvas);
            Stretch(rt);
            rt.gameObject.AddComponent<SafeAreaFitter>();
            return rt;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Anchor + pivot at the same normalised point, then position/size.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color, bool raycast = false)
        {
            RectTransform rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border.sqrMagnitude > 0f) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        /// <summary>Rounded panel with a soft drop shadow and optional gold edge.</summary>
        public static Image Panel(string name, Transform parent, Color color, bool edge = true, bool shadow = true)
        {
            RectTransform root = Rect(name, parent);
            Image bg = root.gameObject.AddComponent<Image>();
            bg.sprite = Lib != null ? Lib.uiRound : null;
            bg.type = UnityEngine.UI.Image.Type.Sliced;
            bg.color = color;
            bg.raycastTarget = true;
            if (shadow)
            {
                // Shadow effect renders beneath the graphic (a child image would draw on top of the panel).
                var sh = root.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.4f);
                sh.effectDistance = new Vector2(0f, -7f);
            }
            if (edge && Lib != null && Lib.uiRoundOutline != null)
            {
                Image e = Image("Edge", root, Lib.uiRoundOutline, new Color(UIPalette.PanelEdge.r, UIPalette.PanelEdge.g, UIPalette.PanelEdge.b, 0.55f));
                Stretch(e.rectTransform);
            }
            return bg;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color,
                                           TextAlignmentOptions align = TextAlignmentOptions.Center, bool display = false)
        {
            RectTransform rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Lib != null)
            {
                TMP_FontAsset f = display ? Lib.displayFont : Lib.bodyFont;
                if (f != null) t.font = f;
            }
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        public static Button Button(string name, Transform parent, string label, Color color, Action onClick, Sprite icon = null,
                                    float fontSize = 40f)
        {
            Image bg = Panel(name, parent, color, true, true);
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            cb.pressedColor = new Color(0.78f, 0.78f, 0.78f);
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            cb.fadeDuration = 0.06f;
            b.colors = cb;
            bg.gameObject.AddComponent<ButtonJuice>();
            if (onClick != null) b.onClick.AddListener(() => onClick());
            if (icon != null)
            {
                Image ic = Image("Icon", bg.transform, icon, UIPalette.Parchment);
                if (string.IsNullOrEmpty(label)) { Stretch(ic.rectTransform, 18f); ic.preserveAspect = true; }
                else
                {
                    Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(64f, 64f));
                    ic.preserveAspect = true;
                }
            }
            if (!string.IsNullOrEmpty(label))
            {
                TextMeshProUGUI t = Text("Label", bg.transform, label, fontSize, UIPalette.Parchment, TextAlignmentOptions.Center, true);
                Stretch(t.rectTransform, 8f);
                if (icon != null) t.rectTransform.offsetMin = new Vector2(96f, 8f);
            }
            return b;
        }

        public static Slider Slider(string name, Transform parent, float value, Action<float> onChange)
        {
            RectTransform root = Rect(name, parent);
            Image track = Image("Track", root, Lib != null ? Lib.uiRound : null, new Color(0f, 0f, 0f, 0.45f));
            track.rectTransform.anchorMin = new Vector2(0f, 0.35f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.65f);
            track.rectTransform.offsetMin = track.rectTransform.offsetMax = Vector2.zero;
            RectTransform fillArea = Rect("FillArea", root);
            fillArea.anchorMin = new Vector2(0f, 0.35f);
            fillArea.anchorMax = new Vector2(1f, 0.65f);
            fillArea.offsetMin = fillArea.offsetMax = Vector2.zero;
            Image fill = Image("Fill", fillArea, Lib != null ? Lib.uiRound : null, UIPalette.Leaf);
            Stretch(fill.rectTransform);
            RectTransform handleArea = Rect("HandleArea", root);
            Stretch(handleArea);
            Image handle = Image("Handle", handleArea, Lib != null ? Lib.uiCircle : null, UIPalette.Parchment, true);
            handle.rectTransform.sizeDelta = new Vector2(56f, 56f);
            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.minValue = 0f;
            s.maxValue = 1f;
            s.value = value;
            if (onChange != null) s.onValueChanged.AddListener(v => onChange(v));
            return s;
        }

        public static Toggle Toggle(string name, Transform parent, string label, bool value, Action<bool> onChange)
        {
            RectTransform root = Rect(name, parent);
            Image box = Image("Box", root, Lib != null ? Lib.uiRound : null, new Color(0f, 0f, 0f, 0.45f), true);
            Place(box.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            Image check = Image("Check", box.transform, Lib != null ? Lib.iconLeaf : null, UIPalette.Leaf);
            Stretch(check.rectTransform, 8f);
            TextMeshProUGUI t = Text("Label", root, label, 36f, UIPalette.Parchment, TextAlignmentOptions.MidlineLeft);
            t.rectTransform.anchorMin = new Vector2(0f, 0f);
            t.rectTransform.anchorMax = new Vector2(1f, 1f);
            t.rectTransform.offsetMin = new Vector2(84f, 0f);
            t.rectTransform.offsetMax = Vector2.zero;
            var tg = root.gameObject.AddComponent<Toggle>();
            tg.targetGraphic = box;
            tg.graphic = check;
            tg.isOn = value;
            if (onChange != null) tg.onValueChanged.AddListener(v => onChange(v));
            return tg;
        }

        public static CanvasGroup Group(GameObject go)
        {
            CanvasGroup g = go.GetComponent<CanvasGroup>();
            return g != null ? g : go.AddComponent<CanvasGroup>();
        }
    }

    /// <summary>Subtle press scale feedback for buttons and cards (unscaled time, works while paused).</summary>
    public sealed class ButtonJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private float _target = 1f;
        private float _scale = 1f;

        public void OnPointerDown(PointerEventData e)
        {
            _target = 0.93f;
            if (VerdantSiege.Audio.AudioService.Instance != null) VerdantSiege.Audio.AudioService.Instance.Play(VerdantSiege.Audio.Sfx.UiTap, 0.8f);
        }

        public void OnPointerUp(PointerEventData e) => _target = 1f;
        public void OnPointerExit(PointerEventData e) => _target = 1f;

        private void Update()
        {
            if (Mathf.Approximately(_scale, _target)) return;
            _scale = Mathf.MoveTowards(_scale, _target, Time.unscaledDeltaTime * 2.5f);
            transform.localScale = new Vector3(_scale, _scale, 1f);
        }
    }

    /// <summary>Screen.safeArea with an optional simulated notch inset (desktop layout tests).</summary>
    public static class ScreenSafe
    {
        public static float SimulatedInsetPx;

        public static Rect Area
        {
            get
            {
                Rect r = Screen.safeArea;
                if (SimulatedInsetPx > 0f) { r.xMin += SimulatedInsetPx; r.xMax -= SimulatedInsetPx; }
                return r;
            }
        }
    }

    /// <summary>Keeps a full-screen rect inside the (possibly simulated) safe area.</summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect _last;
        private Vector2Int _lastSize;

        private void OnEnable() => Apply();

        private void Update()
        {
            if (_last != ScreenSafe.Area || _lastSize.x != Screen.width || _lastSize.y != Screen.height) Apply();
        }

        private void Apply()
        {
            _last = ScreenSafe.Area;
            _lastSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(_last.xMin / Screen.width, _last.yMin / Screen.height);
            rt.anchorMax = new Vector2(_last.xMax / Screen.width, _last.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
