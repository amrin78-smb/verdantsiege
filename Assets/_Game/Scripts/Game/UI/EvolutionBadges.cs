using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VerdantSiege.Battle;
using VerdantSiege.Battle.View;

namespace VerdantSiege.UI
{
    /// <summary>
    /// HUD-tracked badges above defenders (pooled, screen-space, tappable): a glowing gold "Evolve" badge when an
    /// evolution is ready, and a teal "Release" badge with the stored count on a charged Capacitor. Big touch
    /// targets (they sit above the battlefield tap surface but below HUD panels).
    /// </summary>
    public sealed class EvolutionBadges : MonoBehaviour
    {
        public const float Size = 82f;

        private sealed class Badge
        {
            public RectTransform root;
            public Image disc, glow, icon;
            public TextMeshProUGUI count, label;
            public Image labelBg;
            public int defenderId;
            public DefenderRuntime defender;
        }

        private BattleSimulation _sim;
        private BattleLayout _layout;
        private Camera _cam;
        private RectTransform _root;
        private readonly List<Badge> _pool = new List<Badge>();

        /// <summary>A ready badge was tapped (open the chooser).</summary>
        public event Action<DefenderRuntime> EvolveTapped;
        /// <summary>A Capacitor badge was tapped (release).</summary>
        public event Action<DefenderRuntime> ReleaseTapped;

        /// <summary>Defender id currently highlighted (chooser open for it); its badge is drawn larger.</summary>
        public int HighlightId { get; set; }

        public void Init(BattleSimulation sim, BattleLayout layout, Camera cam, Transform canvas)
        {
            _sim = sim;
            _layout = layout;
            _cam = cam;
            _root = UIKit.Rect("EvolutionBadges", canvas);
            UIKit.Stretch(_root);
            _root.SetSiblingIndex(1); // above the tap surface, below the HUD widgets
        }

        /// <summary>Screen position of a defender's badge (also used by the autoplay harness to tap it).</summary>
        public Vector3 BadgeScreenPoint(DefenderRuntime d) => _cam.WorldToScreenPoint(_layout.CellCenter(d.Coord) + new Vector3(0.28f, 1.2f, 0f)); // z > 0: in front of the camera

        private Badge Get(int i)
        {
            while (_pool.Count <= i)
            {
                var b = new Badge();
                b.root = UIKit.Rect("Badge", _root);
                b.root.sizeDelta = new Vector2(Size, Size);
                Sprite circle = UIKit.Lib != null ? UIKit.Lib.uiCircle : null;
                b.glow = UIKit.Image("Glow", b.root, circle, new Color(1f, 0.85f, 0.4f, 0.35f));
                UIKit.Stretch(b.glow.rectTransform, -16f);
                b.disc = UIKit.Image("Disc", b.root, circle, UIPalette.Gold, true);
                UIKit.Stretch(b.disc.rectTransform);
                var btn = b.disc.gameObject.AddComponent<Button>();
                btn.targetGraphic = b.disc;
                Badge captured = b;
                btn.onClick.AddListener(() => OnTapped(captured));
                Image ring = UIKit.Image("Ring", b.disc.transform, circle, new Color(1f, 1f, 1f, 0.9f));
                UIKit.Stretch(ring.rectTransform, 6f);
                ring.color = new Color(0.25f, 0.45f, 0.2f, 1f);
                b.icon = UIKit.Image("Icon", b.disc.transform, UIKit.Lib != null ? UIKit.Lib.iconLeaf : null, UIPalette.Parchment);
                UIKit.Stretch(b.icon.rectTransform, 20f);
                b.icon.preserveAspect = true;
                b.count = UIKit.Text("Count", b.disc.transform, "", 40f, UIPalette.Parchment, TextAlignmentOptions.Center, true);
                UIKit.Stretch(b.count.rectTransform);
                b.labelBg = UIKit.Image("LabelBg", b.root, UIKit.Lib != null ? UIKit.Lib.uiRound : null, new Color(0.08f, 0.16f, 0.12f, 0.92f));
                UIKit.Place(b.labelBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -38f), new Vector2(134f, 42f));
                b.label = UIKit.Text("Label", b.labelBg.transform, "Evolve", 30f, UIPalette.Gold, TextAlignmentOptions.Center, true);
                UIKit.Stretch(b.label.rectTransform);
                _pool.Add(b);
            }
            return _pool[i];
        }

        /// <summary>Active badges with their screen positions (autoplay diagnostics).</summary>
        public string Describe()
        {
            var sb = new System.Text.StringBuilder();
            foreach (Badge b in _pool)
                if (b.root.gameObject.activeInHierarchy) sb.Append($"[{b.label.text} {b.root.position.x:0},{b.root.position.y:0} s={b.root.lossyScale.x:0.00}] ");
            return sb.Length == 0 ? "none" : sb.ToString();
        }

        private void OnTapped(Badge b)
        {
            DefenderRuntime d = b.defender;
            if (d == null || !d.alive || d.Id != b.defenderId) return;
            if (d.EvolutionReady) EvolveTapped?.Invoke(d);
            else if (_sim.IsCapacitor(d) && d.storedVitality > 0) ReleaseTapped?.Invoke(d);
        }

        private void LateUpdate()
        {
            if (_sim == null || _cam == null) return;
            int used = 0;
            float time = Time.unscaledTime;
            bool running = _sim.State == BattleState.Running;
            foreach (DefenderRuntime d in _sim.Defenders)
            {
                if (!running || !d.alive) continue;
                bool ready = d.EvolutionReady;
                bool charged = !ready && _sim.IsCapacitor(d) && d.storedPulses > 0;
                if (!ready && !charged) continue;
                Vector3 sp = BadgeScreenPoint(d);
                if (sp.z <= 0f) continue;
                Badge b = Get(used++);
                if (!b.root.gameObject.activeSelf) b.root.gameObject.SetActive(true);
                b.defender = d;
                b.defenderId = d.Id;
                b.root.position = new Vector3(sp.x, sp.y, 0f);
                float phase = d.Id * 0.37f;
                float pulse = 1f + Mathf.Sin(time * 4f + phase) * 0.06f;
                float k = (d.Id == HighlightId ? 1.18f : 1f) * pulse;
                if (ready)
                {
                    b.disc.color = UIPalette.Gold;
                    b.glow.color = new Color(1f, 0.85f, 0.4f, 0.28f + 0.18f * Mathf.Sin(time * 4f + phase));
                    b.icon.enabled = true;
                    b.count.text = "";
                    b.label.text = "Evolve";
                    b.label.color = UIPalette.Gold;
                }
                else
                {
                    bool full = _sim.CapacitorFull(d);
                    b.disc.color = new Color(0.35f, 0.9f, 0.85f);
                    b.glow.color = new Color(0.45f, 1f, 0.95f, full ? 0.3f + 0.25f * Mathf.Sin(time * 8f) : 0.18f);
                    b.icon.enabled = false;
                    b.count.text = "x" + d.storedPulses;
                    b.label.text = full ? "Full!" : "Release";
                    b.label.color = new Color(0.6f, 1f, 0.95f);
                    k *= full ? 1f : 0.85f;
                }
                b.root.localScale = new Vector3(k, k, 1f);
            }
            for (int i = used; i < _pool.Count; i++)
                if (_pool[i].root.gameObject.activeSelf) { _pool[i].root.gameObject.SetActive(false); _pool[i].defender = null; }
        }
    }
}
