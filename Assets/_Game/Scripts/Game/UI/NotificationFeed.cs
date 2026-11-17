using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VerdantSiege.UI
{
    public enum NotifyKind
    {
        Headline = 0, // wave/phase/boss announcements: short title + optional subtitle
        Intel = 1,    // "new foe" / counter hints: title + one or two lines
        Alert = 2     // urgent single line (boss telegraphs)
    }

    /// <summary>
    /// Compact combat notifications: ONE slim strip in the scenery band under the top bars, so messages never
    /// sit on the lanes. Items queue (headlines and alerts jump ahead of intel), duplicates are dropped, and a
    /// long queue skips stale intel. Slides in and fades out in unscaled time.
    /// </summary>
    public sealed class NotificationFeed : MonoBehaviour
    {
        private struct Item
        {
            public NotifyKind kind;
            public string title;
            public string body;
            public Color color;
            public float seconds;
        }

        private readonly List<Item> _queue = new List<Item>();
        private RectTransform _strip;
        private Image _bg, _accent;
        private TextMeshProUGUI _title, _body;
        private CanvasGroup _group;
        private Item _current;
        private float _timer = -1f;
        private float _top = -112f;
        private float _anim;

        public bool Busy => _timer > 0f;
        /// <summary>An intel card is fully shown (autoplay screenshot).</summary>
        public bool ShowingIntel => _timer > 0.5f && _anim >= 1f && _current.kind == NotifyKind.Intel;

        /// <summary>TD-36: title ~38, body 30 (never below 28), up to two body lines; the strip grows only when needed.</summary>
        public const float TitleSize = 38f, BodySize = 30f, Width = 1120f, TitleBand = 50f;

        public static NotificationFeed Create(RectTransform root)
        {
            RectTransform host = UIKit.Rect("NotificationFeed", root);
            UIKit.Stretch(host);
            var f = host.gameObject.AddComponent<NotificationFeed>();
            f.Build(host);
            return f;
        }

        private void Build(RectTransform host)
        {
            _bg = UIKit.Panel("Strip", host, new Color(0.07f, 0.11f, 0.09f, 0.88f), true, true);
            _bg.raycastTarget = false;
            _strip = _bg.rectTransform;
            _accent = UIKit.Image("Accent", _bg.transform, UIKit.Lib != null ? UIKit.Lib.uiRound : null, UIPalette.Gold);
            _accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            _accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            _accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            _accent.rectTransform.offsetMin = new Vector2(10f, 12f);
            _accent.rectTransform.offsetMax = new Vector2(18f, -12f);
            _title = UIKit.Text("Title", _bg.transform, "", TitleSize, UIPalette.Parchment, TextAlignmentOptions.MidlineLeft, true);
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            MenuKit.Fit(_title, TitleSize, 30f);
            _body = UIKit.Text("Body", _bg.transform, "", BodySize, UIPalette.Parchment, TextAlignmentOptions.TopLeft);
            _body.lineSpacing = -6f;
            _group = UIKit.Group(_bg.gameObject);
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        /// <summary>Vertical offset (from the top of the safe area) of the strip: below the wave bar, or below the boss bar.</summary>
        public void SetTop(float y) => _top = y;

        public void Push(NotifyKind kind, string title, string body, Color color, float seconds)
        {
            if (string.IsNullOrEmpty(title)) return;
            if (_timer > 0f && _current.title == title && _current.body == body) { _timer = Mathf.Max(_timer, seconds); return; }
            foreach (Item q in _queue) if (q.title == title && q.body == body) return;
            var item = new Item { kind = kind, title = title, body = body ?? "", color = color, seconds = Mathf.Clamp(seconds, 1.2f, 6f) };
            if (kind == NotifyKind.Intel) _queue.Add(item);
            else
            {
                int i = 0;
                while (i < _queue.Count && _queue[i].kind != NotifyKind.Intel) i++;
                _queue.Insert(i, item);
                // An urgent item shortens a long-running intel card.
                if (_timer > 1f && _current.kind == NotifyKind.Intel) _timer = 0.6f;
            }
            while (_queue.Count > 4) _queue.RemoveAt(_queue.Count - 1);
        }

        private void Show(Item it)
        {
            _current = it;
            _timer = it.seconds;
            _anim = 0f;
            bool twoLine = !string.IsNullOrEmpty(it.body);
            float w = it.kind == NotifyKind.Intel ? Width : Width - 160f;
            _body.fontSize = BodySize;
            float bodyH = twoLine ? Mathf.Min(MenuKit.TextHeight(_body, it.body, w - 52f), 2f * BodySize * 1.2f) : 0f;
            float h = twoLine ? TitleBand + bodyH + 14f : 66f;
            UIKit.Place(_strip, new Vector2(0.5f, 1f), new Vector2(BattleHud.RailWidth * 0.5f, _top), new Vector2(w, h));
            _accent.color = it.color;
            _title.text = it.title;
            _title.color = it.kind == NotifyKind.Intel ? UIPalette.Gold : it.color;
            if (it.kind == NotifyKind.Alert) _title.color = UIPalette.Parchment;
            _title.rectTransform.anchorMin = new Vector2(0f, twoLine ? 1f : 0f);
            _title.rectTransform.anchorMax = new Vector2(1f, 1f);
            _title.rectTransform.pivot = new Vector2(0.5f, 1f);
            _title.rectTransform.offsetMin = new Vector2(32f, twoLine ? -TitleBand : 4f);
            _title.rectTransform.offsetMax = new Vector2(-20f, -4f);
            _body.gameObject.SetActive(twoLine);
            _body.text = it.body;
            _body.rectTransform.anchorMin = Vector2.zero;
            _body.rectTransform.anchorMax = Vector2.one;
            _body.rectTransform.offsetMin = new Vector2(32f, 6f);
            _body.rectTransform.offsetMax = new Vector2(-20f, -TitleBand + 2f);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_timer > 0f)
            {
                _timer -= dt;
                _anim = Mathf.Min(1f, _anim + dt / 0.22f);
            }
            if (_timer <= 0f && _queue.Count > 0)
            {
                Item next = _queue[0];
                _queue.RemoveAt(0);
                Show(next);
            }
            float a = _timer > 0f ? Mathf.Min(_anim, Mathf.Clamp01(_timer / 0.35f)) : 0f;
            _group.alpha = a;
            if (_strip != null)
            {
                float slide = (1f - Mathf.Min(1f, _anim)) * 24f;
                Vector2 p = _strip.anchoredPosition;
                _strip.anchoredPosition = new Vector2(p.x, _top + slide);
            }
        }
    }
}
