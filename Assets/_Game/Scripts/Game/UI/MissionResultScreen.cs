using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VerdantSiege.Battle;
using VerdantSiege.Data;
using VerdantSiege.Progression;
using VerdantSiege.Save;

namespace VerdantSiege.UI
{
    /// <summary>
    /// Campaign result (Milestone 4): outcome and stars (new ones pop in), battle stats, the Seeds/Essence actually
    /// paid (first clear, replay share, star bonuses), defender unlocks and every planted defender's mastery bar
    /// filling up, with level-ups naming the evolution branches they unlocked. Unscaled time; replaces the plain
    /// BattleHud result for campaign battles.
    /// </summary>
    public sealed class MissionResultScreen : MonoBehaviour
    {
        public struct Data
        {
            public EncounterDefinition encounter;
            public MissionRewards rewards;
            public BattleStats stats;
            public bool won;
            public int heart, heartMax;
            public ContentDatabase content;
        }

        private sealed class MasteryRow
        {
            public Image fill;
            public TextMeshProUGUI level;
            public MasteryGain gain;
            public RectTransform badge;
            public bool wide;
        }

        private Data _d;
        private float _t;
        private readonly List<MasteryRow> _rows = new List<MasteryRow>();
        private readonly List<RectTransform> _newStars = new List<RectTransform>();
        private TextMeshProUGUI _seeds, _essence;
        private bool _counted;

        /// <summary>True once the reward count-up and mastery bars have finished (autoplay screenshots).</summary>
        public bool Settled => _counted;
        public string Summary { get; private set; } = "";

        public static MissionResultScreen Show(BattleHud hud, Data d, Action replay, Action map, Action next)
        {
            var go = new GameObject("MissionResult", typeof(RectTransform));
            go.transform.SetParent(hud.Canvas.transform, false);
            UIKit.Stretch((RectTransform)go.transform);
            Image dim = go.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.05f, 0.04f, 0.82f);
            dim.raycastTarget = true;
            var s = go.AddComponent<MissionResultScreen>();
            s._d = d;
            s.Build(replay, map, next);
            PopIn.Play(go);
            return s;
        }

        private void Build(Action replay, Action map, Action next)
        {
            MissionRewards r = _d.rewards;
            RectTransform safe = UIKit.SafeArea(transform);
            Image panel = UIKit.Panel("Panel", safe, UIPalette.Panel, true, true);
            RectTransform p = panel.rectTransform;
            UIKit.Place(p, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1720f, 980f));

            // ---- title, mission, stars
            string title = _d.won ? (r != null && r.firstClear ? "Garden Defended!" : "Defended Again!") : "The Heartroot Falls";
            TextMeshProUGUI tt = UIKit.Text("Title", p, title, 84f, _d.won ? UIPalette.Gold : UIPalette.Danger, TextAlignmentOptions.Center, true);
            UIKit.Place(tt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(1500f, 104f));
            MenuKit.Shadowed(tt);
            string sub = _d.encounter != null ? _d.encounter.displayName : "";
            if (r != null && r.replay) sub += "  ·  Replay";
            if (!_d.won) sub += "  ·  Every loss teaches: your defenders still gained mastery.";
            TextMeshProUGUI st = UIKit.Text("Mission", p, sub, 32f, UIPalette.TextDim);
            UIKit.Place(st.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(1600f, 44f));

            int bits = r != null ? r.stars : 0;
            int fresh = r != null ? r.newStars : 0;
            string[] labels = { "Victory", "Flawless Heartroot", _d.encounter != null && _d.encounter.challenge.IsSet ? _d.encounter.challenge.text : "Challenge" };
            int[] mask = { EncounterRecord.StarWin, EncounterRecord.StarFlawless, EncounterRecord.StarChallenge };
            for (int i = 0; i < 3; i++)
            {
                bool on = (bits & mask[i]) != 0, isNew = (fresh & mask[i]) != 0;
                RectTransform slot = UIKit.Rect("StarSlot" + i, p);
                UIKit.Place(slot, new Vector2(0.5f, 1f), new Vector2((i - 1) * 380f, -176f), new Vector2(360f, 168f));
                Image star = UIKit.Image("Star", slot, UIKit.Lib != null ? UIKit.Lib.iconStar : null, on ? UIPalette.Gold : MenuKit.StarOff);
                star.preserveAspect = true;
                UIKit.Place(star.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(104f, 104f));
                if (on) MenuKit.Shadowed(star);
                if (isNew) { _newStars.Add(star.rectTransform); star.rectTransform.localScale = Vector3.zero; }
                TextMeshProUGUI l = UIKit.Text("Label", slot, labels[i] + (isNew ? "  <color=#7CD65A>NEW</color>" : ""), MenuKit.TextSmall,
                    on ? UIPalette.Parchment : UIPalette.TextDim, TextAlignmentOptions.Top);
                UIKit.Place(l.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(372f, 76f));
                l.lineSpacing = -8f;
                MenuKit.Fit(l, MenuKit.TextSmall);
            }

            // ---- left: battle stats
            BattleStats s = _d.stats;
            int m = Mathf.FloorToInt(s.elapsed / 60f), sec = Mathf.FloorToInt(s.elapsed % 60f);
            string Row(string k, string v) => $"<color=#C9D8C4>{k}</color><pos=64%><color=#F6ECD2>{v}</color>";
            string body = Row("Blight defeated", s.enemiesDefeated.ToString()) + "\n" +
                          Row("Defenders planted", s.defendersPlanted.ToString()) + "\n" +
                          Row("Defenders lost", s.defendersLost.ToString()) + "\n" +
                          Row("Vitality gathered", s.vitalityCollected.ToString()) + "\n" +
                          Row("Heartroot", $"{_d.heart} / {_d.heartMax}") + "\n" +
                          Row("Time", $"{m}:{sec:00}");
            Image statsBg = UIKit.Panel("StatsPanel", p, new Color(0f, 0f, 0f, 0.18f), false, false);
            UIKit.Place(statsBg.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -366f), new Vector2(560f, 440f));
            TextMeshProUGUI head = UIKit.Text("Head", statsBg.transform, "Battle", 40f, UIPalette.Gold, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(head.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -16f), new Vector2(540f, 46f));
            TextMeshProUGUI stats = UIKit.Text("Stats", statsBg.transform, body, 31f, UIPalette.Parchment, TextAlignmentOptions.TopLeft);
            UIKit.Place(stats.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -70f), new Vector2(510f, 360f));
            stats.lineSpacing = 14f;

            // ---- right: rewards, unlocks, mastery
            const float rightW = 1040f, rightH = 464f;
            RectTransform right = UIKit.Rect("Rewards", p);
            UIKit.Place(right, new Vector2(0f, 1f), new Vector2(640f, -366f), new Vector2(rightW, rightH));
            _seeds = MenuKit.Chip(right, "SeedsChip", false, "+0", new Vector2(0f, 1f), Vector2.zero, new Vector2(330f, 88f));
            _essence = MenuKit.Chip(right, "EssenceChip", true, "+0", new Vector2(0f, 1f), new Vector2(350f, 0f), new Vector2(250f, 88f));
            bool ess = r != null && r.Essence > 0;
            if (!ess) _essence.transform.parent.gameObject.SetActive(false);
            string bdText = SeedBreakdown(r);
            TextMeshProUGUI breakdown = UIKit.Text("Breakdown", right, bdText, MenuKit.TextSmall, UIPalette.TextDim, TextAlignmentOptions.MidlineLeft);
            float bx0 = ess ? 620f : 350f;
            float y;
            if (MenuKit.TextHeight(breakdown, bdText, rightW - bx0) <= 84f)
            {
                UIKit.Place(breakdown.rectTransform, new Vector2(0f, 1f), new Vector2(bx0, 0f), new Vector2(rightW - bx0, 88f));
                y = -100f;
            }
            else
            {
                // Too long beside the chips: its own line(s) underneath.
                float bh = MenuKit.TextHeight(breakdown, bdText, rightW);
                UIKit.Place(breakdown.rectTransform, new Vector2(0f, 1f), new Vector2(4f, -96f), new Vector2(rightW, bh));
                y = -96f - bh - 10f;
            }

            if (r != null && r.unlocked.Count > 0)
            {
                Image band = UIKit.Panel("Unlocked", right, new Color(0.3f, 0.5f, 0.2f, 0.55f), true, false);
                UIKit.Place(band.rectTransform, new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(rightW, 74f));
                float x = 16f;
                foreach (DefenderDefinition d in r.unlocked)
                {
                    RectTransform pt = MenuKit.Portrait(band.transform, d, 60f, false);
                    UIKit.Place(pt, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(60f, 60f));
                    x += 68f;
                }
                var names = new List<string>();
                foreach (DefenderDefinition d in r.unlocked) names.Add(d.displayName);
                TextMeshProUGUI ut = UIKit.Text("Text", band.transform, "New defender" + (names.Count > 1 ? "s" : "") + ": " + string.Join(", ", names), 34f,
                    UIPalette.Gold, TextAlignmentOptions.MidlineLeft, true);
                UIKit.Place(ut.rectTransform, new Vector2(0f, 0.5f), new Vector2(x + 8f, 0f), new Vector2(rightW - x - 24f, 66f));
                MenuKit.Fit(ut, 34f);
                y -= 86f;
            }
            if (r != null && r.regionCompleted)
            {
                TextMeshProUGUI rc = UIKit.Text("Region", right, "Region restored! The Sanctuary grows greener.", MenuKit.TextLabel, UIPalette.Leaf, TextAlignmentOptions.MidlineLeft, true);
                UIKit.Place(rc.rectTransform, new Vector2(0f, 1f), new Vector2(4f, y), new Vector2(rightW, 44f));
                y -= 50f;
            }
            TextMeshProUGUI mh = UIKit.Text("MasteryHead", right, "Mastery", 40f, UIPalette.Gold, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(mh.rectTransform, new Vector2(0f, 1f), new Vector2(4f, y), new Vector2(400f, 50f));
            y -= 54f;
            if (r == null || r.mastery.Count == 0)
            {
                TextMeshProUGUI none = UIKit.Text("None", right, "Plant defenders to earn their mastery.", MenuKit.TextBody, UIPalette.TextDim, TextAlignmentOptions.TopLeft);
                UIKit.Place(none.rectTransform, new Vector2(0f, 1f), new Vector2(4f, y), new Vector2(rightW, 44f));
            }
            else
            {
                // One full-width row per defender (readable on phones); the list scrolls when the deck was big.
                ScrollRect sr = MenuKit.Scroll(right, "MasteryScroll", false, out RectTransform list);
                RectTransform srt = (RectTransform)sr.transform;
                srt.anchorMin = Vector2.zero;
                srt.anchorMax = Vector2.one;
                srt.offsetMin = Vector2.zero;
                srt.offsetMax = new Vector2(0f, y);
                const float rowH = 88f;
                int n = r.mastery.Count;
                for (int i = 0; i < n; i++)
                    _rows.Add(BuildMasteryRow(list, r.mastery[i], new Vector2(0f, -i * (rowH + 8f)), rowH, rightW - 18f));
                list.sizeDelta = new Vector2(0f, n * (rowH + 8f));
            }

            // ---- buttons
            float bx = next != null ? -300f : -160f;
            Button again = UIKit.Button("Again", p, _d.won ? "Replay" : "Try Again", _d.won ? UIPalette.ButtonSecondary : UIPalette.ButtonPrimary,
                () => replay?.Invoke(), UIKit.Lib != null ? UIKit.Lib.iconRestart : null, 40f);
            UIKit.Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(bx, 28f), new Vector2(290f, 100f));
            Button mapB = UIKit.Button("WorldMap", p, "World Map", next != null || !_d.won ? UIPalette.ButtonSecondary : UIPalette.ButtonPrimary,
                () => map?.Invoke(), UIKit.Lib != null ? UIKit.Lib.iconMapPin : null, 38f);
            UIKit.Place((RectTransform)mapB.transform, new Vector2(0.5f, 0f), new Vector2(bx + 310f, 28f), new Vector2(300f, 100f));
            if (next != null)
            {
                Button nb = UIKit.Button("Next", p, "Next", UIPalette.ButtonPrimary, () => next(), UIKit.Lib != null ? UIKit.Lib.iconPlay : null, 40f);
                UIKit.Place((RectTransform)nb.transform, new Vector2(0.5f, 0f), new Vector2(bx + 620f, 28f), new Vector2(260f, 100f));
            }
            Summary = r == null ? "" : $"seeds+{r.Seeds} essence+{r.Essence} stars={r.stars} new={r.newStars} unlocked={r.unlocked.Count} mastery={r.mastery.Count}" +
                                       $" levelUps={r.mastery.FindAll(g => g.LeveledUp).Count}";
        }

        private static string SeedBreakdown(MissionRewards r)
        {
            if (r == null || !r.won) return "No Seeds without a victory.";
            var parts = new List<string>();
            parts.Add(r.replay ? $"Replay {r.seedsMission}" : $"First clear {r.seedsMission}");
            if (r.seedsFlawless > 0) parts.Add($"Flawless {r.seedsFlawless}");
            if (r.seedsChallenge > 0) parts.Add($"Challenge {r.seedsChallenge}");
            if (r.seedsBoss > 0) parts.Add($"Boss {r.seedsBoss}");
            return string.Join("  ·  ", parts);
        }

        private MasteryRow BuildMasteryRow(RectTransform parent, MasteryGain g, Vector2 pos, float h, float w)
        {
            DefenderDefinition d = _d.content != null ? _d.content.FindDefender(g.defenderId) : null;
            Image bg = UIKit.Panel("Mastery_" + g.defenderId, parent, new Color(0f, 0f, 0f, 0.22f), false, false);
            UIKit.Place(bg.rectTransform, new Vector2(0f, 1f), pos, new Vector2(w, h));
            RectTransform portrait = MenuKit.Portrait(bg.transform, d, h - 12f, false);
            UIKit.Place(portrait, new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(h - 12f, h - 12f));
            const float barW = 290f;
            float x0 = h + 6f;
            TextMeshProUGUI name = UIKit.Text("Name", bg.transform, d != null ? d.displayName : g.defenderId, MenuKit.TextLabel, UIPalette.Parchment, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(x0, -6f), new Vector2(barW - 96f, 42f));
            name.textWrappingMode = TextWrappingModes.NoWrap;
            MenuKit.Fit(name, MenuKit.TextLabel);
            var row = new MasteryRow { gain = g, wide = true };
            TextMeshProUGUI plus = UIKit.Text("Gain", bg.transform, "+" + g.gained, MenuKit.TextSmall, UIPalette.Leaf, TextAlignmentOptions.TopRight, true);
            UIKit.Place(plus.rectTransform, new Vector2(0f, 1f), new Vector2(x0 + barW - 96f, -8f), new Vector2(96f, 40f));
            row.level = UIKit.Text("Level", bg.transform, "Lv " + g.levelBefore, MenuKit.TextNumber, UIPalette.Gold, TextAlignmentOptions.MidlineLeft, true);
            UIKit.Place(row.level.rectTransform, new Vector2(0f, 0.5f), new Vector2(x0 + barW + 16f, 0f), new Vector2(150f, 46f));
            row.fill = MenuKit.Bar(bg.transform, "Bar", UIPalette.Gold, new Vector2(0f, 0f), new Vector2(x0, 14f), new Vector2(barW, 18f),
                MasteryRules.LevelProgress(g.xpBefore));
            if (g.LeveledUp)
            {
                string what = g.branchesUnlocked.Count > 0 ? "New branch: " + g.branchesUnlocked[0].displayName : MasteryRules.PerkText(g.levelAfter);
                Image badge = UIKit.Panel("LevelUp", bg.transform, new Color(0.55f, 0.4f, 0.1f, 0.96f), true, false);
                badge.raycastTarget = false;
                float bw = w - (x0 + barW + 172f) - 8f;
                UIKit.Place(badge.rectTransform, new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(bw, h - 12f));
                TextMeshProUGUI bt = UIKit.Text("Text", badge.transform, "<color=#FFD35C>LEVEL UP!</color>  " + what, MenuKit.TextSmall, UIPalette.Parchment, TextAlignmentOptions.Center, true);
                UIKit.Stretch(bt.rectTransform, 6f);
                bt.lineSpacing = -10f;
                MenuKit.Fit(bt, MenuKit.TextSmall);
                row.badge = badge.rectTransform;
                badge.gameObject.SetActive(false);
            }
            return row;
        }

        private static float Back(float t) { t = Mathf.Clamp01(t); const float c = 1.9f; float u = t - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }

        private void Update()
        {
            _t += Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            for (int i = 0; i < _newStars.Count; i++) _newStars[i].localScale = Vector3.one * Back((_t - 0.45f - i * 0.25f) / 0.45f);
            float c = Mathf.Clamp01((_t - 0.6f) / 1.0f);
            MissionRewards r = _d.rewards;
            if (r != null)
            {
                _seeds.text = "+" + Mathf.RoundToInt(r.Seeds * c);
                _essence.text = "+" + Mathf.RoundToInt(r.Essence * c);
            }
            float b = Mathf.Clamp01((_t - 0.9f) / 1.3f);
            foreach (MasteryRow row in _rows)
            {
                MasteryGain g = row.gain;
                float xp = Mathf.Lerp(g.xpBefore, g.xpAfter, b);
                int level = MasteryRules.LevelFor(Mathf.RoundToInt(xp));
                row.fill.fillAmount = MasteryRules.LevelProgress(Mathf.RoundToInt(xp));
                row.level.text = level >= MasteryRules.MaxLevel && row.wide ? "Lv 4 MAX" : "Lv " + level;
                if (row.badge != null && level > g.levelBefore && !row.badge.gameObject.activeSelf)
                {
                    row.badge.gameObject.SetActive(true);
                    PopIn.Play(row.badge.gameObject);
                    if (Audio.AudioService.Instance != null) Audio.AudioService.Instance.Play(Audio.Sfx.UiTap, 0.9f);
                }
            }
            if (!_counted && c >= 1f && b >= 1f && _t > 0.45f + 3 * 0.25f + 0.45f) _counted = true;
        }
    }
}
