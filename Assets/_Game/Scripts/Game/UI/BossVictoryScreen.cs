using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VerdantSiege.Battle;

namespace VerdantSiege.UI
{
    /// <summary>
    /// Boss variant of the result screen: a crest/trophy moment ("Boss Defeated", rays, the boss's name), then the
    /// result panel with stats and the boss rewards (encounter Seeds + bonus Seeds, Essence with a first-defeat badge),
    /// counted up. Unscaled time throughout. Separate from BattleHud.ShowResult so the normal result stays untouched.
    /// </summary>
    public sealed class BossVictoryScreen : MonoBehaviour
    {
        public struct Data
        {
            public string bossName;
            public Color accent;
            public BattleStats stats;
            public int encounterSeeds;
            public int bonusSeeds;
            public int essence;
            public int totalEssence;
            public bool firstDefeat;
            public int staggers;
            public int heart, heartMax;
        }

        /// <summary>Seconds of the crest moment before the panel arrives.</summary>
        public const float CrestSeconds = 1.7f;
        public static readonly Color EssenceColor = new Color(0.78f, 0.55f, 1f);

        private Data _d;
        private float _t;
        private RectTransform _crest, _rays, _title, _panel;
        private CanvasGroup _panelGroup, _subGroup;
        private TextMeshProUGUI _seedsText, _essenceText;
        private Image _flash;
        private bool _counted;

        /// <summary>Shows the crest and the boss rewards; Continue leads to the campaign results (stars, mastery, unlocks).</summary>
        public static BossVictoryScreen Show(BattleHud hud, Data d, Action onContinue)
        {
            var go = new GameObject("BossVictoryOverlay", typeof(RectTransform));
            go.transform.SetParent(hud.Canvas.transform, false);
            Image dim = go.AddComponent<Image>();
            dim.color = new Color(0.03f, 0.02f, 0.05f, 0.8f);
            dim.raycastTarget = true;
            UIKit.Stretch((RectTransform)go.transform);
            var s = go.AddComponent<BossVictoryScreen>();
            s._d = d;
            s.Build(onContinue);
            return s;
        }

        /// <summary>True once the result panel is fully visible (autoplay screenshots).</summary>
        public bool PanelShown => _t > CrestSeconds + 1.5f && _counted;
        /// <summary>Reward texts as shown (autoplay report).</summary>
        public string RewardText => _seedsText != null ? _seedsText.text + " / " + _essenceText.text : "";

        /// <summary>Presses Continue (autoplay; same as a tap once the panel is up).</summary>
        public void Continue() { if (_continue != null && _continue.interactable && _panelGroup.interactable) _continue.onClick.Invoke(); }
        private Button _continue;

        private void Build(Action onContinue)
        {
            Transform root = transform;
            Sprite round = UIKit.Lib != null ? UIKit.Lib.uiRound : null;
            Sprite circle = UIKit.Lib != null ? UIKit.Lib.uiCircle : null;

            // ---- crest
            _crest = UIKit.Rect("Crest", root);
            UIKit.Place(_crest, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(420f, 420f));
            _rays = UIKit.Rect("Rays", _crest);
            UIKit.Place(_rays, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            for (int i = 0; i < 12; i++)
            {
                Image ray = UIKit.Image("Ray" + i, _rays, round, new Color(UIPalette.Gold.r, UIPalette.Gold.g, UIPalette.Gold.b, i % 2 == 0 ? 0.22f : 0.12f));
                ray.rectTransform.sizeDelta = new Vector2(i % 2 == 0 ? 46f : 30f, 760f);
                ray.rectTransform.anchoredPosition = Vector2.zero;
                ray.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 15f);
            }
            Image ring = UIKit.Image("Ring", _crest, circle, UIPalette.Gold);
            UIKit.Stretch(ring.rectTransform);
            Image inner = UIKit.Image("Inner", _crest, circle, Color.Lerp(d_accentDark(), Color.black, 0.2f));
            UIKit.Stretch(inner.rectTransform, 22f);
            Image innerRing = UIKit.Image("InnerRing", _crest, circle, new Color(UIPalette.Gold.r, UIPalette.Gold.g, UIPalette.Gold.b, 0.35f));
            UIKit.Stretch(innerRing.rectTransform, 46f);
            Image core = UIKit.Image("Core", _crest, circle, Color.Lerp(d_accentDark(), Color.black, 0.35f));
            UIKit.Stretch(core.rectTransform, 54f);
            Image leaf = UIKit.Image("Leaf", _crest, UIKit.Lib != null ? UIKit.Lib.iconLeaf : null, UIPalette.Gold);
            UIKit.Stretch(leaf.rectTransform, 105f);
            leaf.preserveAspect = true;
            Image skull = UIKit.Image("Fallen", _crest, UIKit.Lib != null ? UIKit.Lib.iconSkull : null, new Color(1f, 1f, 1f, 0.9f));
            UIKit.Place(skull.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(84f, 84f));
            skull.preserveAspect = true;
            Image sb = UIKit.Image("FallenBack", _crest, circle, new Color(0.35f, 0.08f, 0.15f, 1f));
            UIKit.Place(sb.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(104f, 104f));
            sb.transform.SetSiblingIndex(skull.transform.GetSiblingIndex());

            // ---- title ribbon
            Image ribbon = UIKit.Panel("Title", root, new Color(0.42f, 0.1f, 0.18f, 0.97f), true, true);
            ribbon.raycastTarget = false;
            _title = ribbon.rectTransform;
            UIKit.Place(_title, new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(760f, 120f));
            TextMeshProUGUI tt = UIKit.Text("Text", _title, "BOSS DEFEATED!", 76f, UIPalette.Gold, TextAlignmentOptions.Center, true);
            UIKit.Stretch(tt.rectTransform, 6f);
            TextMeshProUGUI sub = UIKit.Text("Sub", root, $"{_d.bossName} has fallen. The Blight withers.", 36f, UIPalette.Parchment);
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -310f), new Vector2(1000f, 50f));
            _subGroup = UIKit.Group(sub.gameObject);

            // ---- result panel (appears after the crest moment)
            Image panel = UIKit.Panel("Panel", root, UIPalette.Panel, true, true);
            _panel = panel.rectTransform;
            UIKit.Place(_panel, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(1040f, 560f));
            _panelGroup = UIKit.Group(panel.gameObject);
            _panelGroup.alpha = 0f;
            _panelGroup.interactable = false;
            _panelGroup.blocksRaycasts = false;

            string Row(string k, string v) => $"<color=#C9D8C4>{k}</color><pos=66%><color=#F6ECD2>{v}</color>";
            BattleStats st = _d.stats;
            int m = Mathf.FloorToInt(st.elapsed / 60f), sec = Mathf.FloorToInt(st.elapsed % 60f);
            string left = Row("Blight defeated", st.enemiesDefeated.ToString()) + "\n" +
                          Row("Times staggered", _d.staggers.ToString()) + "\n" +
                          Row("Defenders lost", st.defendersLost.ToString());
            string right = Row("Heartroot", $"{_d.heart} / {_d.heartMax}") + "\n" +
                           Row("Vitality gathered", st.vitalityCollected.ToString()) + "\n" +
                           Row("Time", $"{m}:{sec:00}");
            TextMeshProUGUI l = UIKit.Text("StatsL", _panel, left, 32f, UIPalette.Parchment, TextAlignmentOptions.TopLeft);
            UIKit.Place(l.rectTransform, new Vector2(0.5f, 1f), new Vector2(-250f, -36f), new Vector2(460f, 150f));
            l.lineSpacing = 10f;
            TextMeshProUGUI r = UIKit.Text("StatsR", _panel, right, 32f, UIPalette.Parchment, TextAlignmentOptions.TopLeft);
            UIKit.Place(r.rectTransform, new Vector2(0.5f, 1f), new Vector2(250f, -36f), new Vector2(460f, 150f));
            r.lineSpacing = 10f;

            // Rewards: Seeds (encounter + boss bonus) and Essence.
            Image seedChip = UIKit.Panel("Seeds", _panel, UIPalette.PanelLight, true, false);
            UIKit.Place(seedChip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-235f, -34f), new Vector2(440f, 122f));
            Image si = UIKit.Image("Icon", seedChip.transform, UIKit.Lib != null ? UIKit.Lib.iconSeed : null, UIPalette.Gold);
            UIKit.Place(si.rectTransform, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(76f, 76f));
            _seedsText = UIKit.Text("Amount", seedChip.transform, "+0 Seeds", 44f, UIPalette.Gold, TextAlignmentOptions.MidlineRight, true);
            UIKit.Stretch(_seedsText.rectTransform);
            _seedsText.rectTransform.offsetMin = new Vector2(100f, 40f);
            _seedsText.rectTransform.offsetMax = new Vector2(-20f, 0f);
            TextMeshProUGUI seedSub = UIKit.Text("Bonus", seedChip.transform, $"incl. +{_d.bonusSeeds} boss bonus", MenuKit.TextMin, UIPalette.TextDim, TextAlignmentOptions.BottomRight);
            UIKit.Stretch(seedSub.rectTransform);
            seedSub.rectTransform.offsetMin = new Vector2(100f, 8f);
            seedSub.rectTransform.offsetMax = new Vector2(-20f, -6f);

            Image essChip = UIKit.Panel("Essence", _panel, new Color(0.24f, 0.14f, 0.32f, 0.97f), true, false);
            UIKit.Place(essChip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(235f, -34f), new Vector2(440f, 122f));
            Image eb = UIKit.Image("Gem", essChip.transform, circle, EssenceColor);
            UIKit.Place(eb.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(70f, 70f));
            eb.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image ei = UIKit.Image("Icon", eb.transform, UIKit.Lib != null ? UIKit.Lib.iconLeaf : null, Color.white);
            UIKit.Stretch(ei.rectTransform, 14f);
            ei.preserveAspect = true;
            _essenceText = UIKit.Text("Amount", essChip.transform, "+0 Essence", 44f, EssenceColor, TextAlignmentOptions.MidlineRight, true);
            UIKit.Stretch(_essenceText.rectTransform);
            _essenceText.rectTransform.offsetMin = new Vector2(100f, 40f);
            _essenceText.rectTransform.offsetMax = new Vector2(-20f, 0f);
            TextMeshProUGUI essSub = UIKit.Text("Total", essChip.transform, (_d.firstDefeat ? "First defeat!  " : "") + $"Total {_d.totalEssence}",
                MenuKit.TextMin, _d.firstDefeat ? UIPalette.Gold : UIPalette.TextDim, TextAlignmentOptions.BottomRight);
            UIKit.Stretch(essSub.rectTransform);
            essSub.rectTransform.offsetMin = new Vector2(100f, 8f);
            essSub.rectTransform.offsetMax = new Vector2(-20f, -6f);

            _continue = UIKit.Button("Continue", _panel, "Continue", UIPalette.ButtonPrimary, () =>
            {
                _continue.interactable = false;
                gameObject.SetActive(false);
                onContinue?.Invoke();
            }, UIKit.Lib != null ? UIKit.Lib.iconPlay : null, 42f);
            UIKit.Place((RectTransform)_continue.transform, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(340f, 104f));

            // Bright flash on arrival.
            _flash = UIKit.Image("Flash", root, null, new Color(1f, 0.95f, 0.85f, 0.85f));
            UIKit.Stretch(_flash.rectTransform);
            _crest.localScale = Vector3.zero;
            _title.localScale = Vector3.zero;
            _subGroup.alpha = 0f;
        }

        private Color d_accentDark() => new Color(_d.accent.r * 0.45f, _d.accent.g * 0.4f, _d.accent.b * 0.5f, 1f);

        private static float Back(float t) { t = Mathf.Clamp01(t); const float c = 1.9f; float u = t - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.25f); // low FPS still progresses in real time
            _t += dt;
            float t = _t;
            _flash.color = new Color(1f, 0.95f, 0.85f, Mathf.Clamp01(0.85f - t * 2.2f));
            if (_flash.color.a <= 0f && _flash.gameObject.activeSelf) _flash.gameObject.SetActive(false);
            _rays.localRotation = Quaternion.Euler(0f, 0f, -t * 18f);

            // Crest moment: pop in (centre), then rise to the top while the panel arrives.
            float pop = Back(t / 0.55f);
            float move = Mathf.SmoothStep(0f, 1f, (t - CrestSeconds) / 0.6f);
            float pulse = 1f + 0.03f * Mathf.Sin(t * 5f) * (1f - move);
            _crest.localScale = Vector3.one * (pop * Mathf.Lerp(1f, 0.46f, move) * pulse);
            _crest.anchoredPosition = new Vector2(0f, Mathf.Lerp(60f, 395f, move));
            _title.localScale = Vector3.one * (Back((t - 0.25f) / 0.45f) * Mathf.Lerp(1f, 0.82f, move));
            _title.anchoredPosition = new Vector2(0f, Mathf.Lerp(-220f, 222f, move));
            _subGroup.alpha = Mathf.Clamp01((t - 0.6f) / 0.4f) * (1f - move);

            float pa = Mathf.Clamp01((t - CrestSeconds - 0.2f) / 0.45f);
            _panelGroup.alpha = pa;
            _panel.anchoredPosition = new Vector2(0f, Mathf.Lerp(-200f, -150f, pa));
            if (pa >= 1f && !_panelGroup.interactable) { _panelGroup.interactable = true; _panelGroup.blocksRaycasts = true; }

            // Count the rewards up.
            float c = Mathf.Clamp01((t - CrestSeconds - 0.6f) / 0.8f);
            if (!_counted || c < 1f)
            {
                int total = _d.encounterSeeds + _d.bonusSeeds;
                _seedsText.text = $"+{Mathf.RoundToInt(total * c)} Seeds";
                _essenceText.text = $"+{Mathf.RoundToInt(_d.essence * c)} Essence";
                _counted = c >= 1f;
            }
        }
    }
}
