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
                TextMeshProUGUI l = UIKit.Text("Label", slot, labels[i] + (isNew ? "  <color=#7CD65A>NEW</color>" : ""), 27f,
                    on ? UIPalette.Parchment : UIPalette.TextDim, TextAlignmentOptions.Top);
                UIKit.Place(l.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(370f, 66f));
                l.enableAutoSizing = true; l.fontSizeMin = 20f; l.fontSizeMax = 27f;
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
            UIKit.Place(statsBg.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -360f), new Vector2(600f, 440f));
            TextMeshProUGUI head = UIKit.Text("Head", statsBg.transform, "Battle", 36f, UIPalette.Gold, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(head.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -16f), new Vector2(540f, 46f));
            TextMeshProUGUI stats = UIKit.Text("Stats", statsBg.transform, body, 31f, UIPalette.Parchment, TextAlignmentOptions.TopLeft);
            UIKit.Place(stats.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -70f), new Vector2(550f, 360f));
            stats.lineSpacing = 14f;

            // ---- right: rewards, unlocks, mastery
            RectTransform right = UIKit.Rect("Rewards", p);
            UIKit.Place(right, new Vector2(0f, 1f), new Vector2(680f, -360f), new Vector2(1000f, 480f));
            _seeds = MenuKit.Chip(right, "SeedsChip", false, "+0", new Vector2(0f, 1f), Vector2.zero, new Vector2(330f, 88f));
            _essence = MenuKit.Chip(right, "EssenceChip", true, "+0", new Vector2(0f, 1f), new Vector2(350f, 0f), new Vector2(250f, 88f));
            if (r != null && r.Essence == 0) _essence.transform.parent.gameObject.SetActive(false);
            TextMeshProUGUI breakdown = UIKit.Text("Breakdown", right, SeedBreakdown(r), 24f, UIPalette.TextDim, TextAlignmentOptions.TopLeft);
            UIKit.Place(breakdown.rectTransform, new Vector2(0f, 1f), new Vector2(r != null && r.Essence > 0 ? 620f : 350f, -6f),
                new Vector2(r != null && r.Essence > 0 ? 380f : 650f, 84f));
            breakdown.enableAutoSizing = true;
            breakdown.fontSizeMin = 18f;
            breakdown.fontSizeMax = 24f;

            float y = -104f;
            if (r != null && r.unlocked.Count > 0)
            {
                Image band = UIKit.Panel("Unlocked", right, new Color(0.3f, 0.5f, 0.2f, 0.55f), true, false);
                UIKit.Place(band.rectTransform, new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(1000f, 70f));
                float x = 16f;
                foreach (DefenderDefinition d in r.unlocked)
                {
                    RectTransform pt = MenuKit.Portrait(band.transform, d, 54f, false);
                    UIKit.Place(pt, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(54f, 54f));
                    x += 62f;
                }
                var names = new List<string>();
                foreach (DefenderDefinition d in r.unlocked) names.Add(d.displayName);
                TextMeshProUGUI ut = UIKit.Text("Text", band.transform, "New defender" + (names.Count > 1 ? "s" : "") + ": " + string.Join(", ", names), 30f,
                    UIPalette.Gold, TextAlignmentOptions.MidlineLeft, true);
                UIKit.Place(ut.rectTransform, new Vector2(0f, 0.5f), new Vector2(x + 8f, 0f), new Vector2(1000f - x - 24f, 60f));
                ut.enableAutoSizing = true; ut.fontSizeMin = 20f; ut.fontSizeMax = 30f;
                y -= 82f;
            }
            if (r != null && r.regionCompleted)
            {
                TextMeshProUGUI rc = UIKit.Text("Region", right, "Region restored! The Sanctuary grows greener.", 28f, UIPalette.Leaf, TextAlignmentOptions.MidlineLeft, true);
                UIKit.Place(rc.rectTransform, new Vector2(0f, 1f), new Vector2(4f, y), new Vector2(1000f, 40f));
                y -= 46f;
            }
            TextMeshProUGUI mh = UIKit.Text("MasteryHead", right, "Mastery", 34f, UIPalette.Gold, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(mh.rectTransform, new Vector2(0f, 1f), new Vector2(4f, y), new Vector2(400f, 44f));
            y -= 50f;
            if (r == null || r.mastery.Count == 0)
            {
                TextMeshProUGUI none = UIKit.Text("None", right, "Plant defenders to earn their mastery.", 26f, UIPalette.TextDim, TextAlignmentOptions.TopLeft);
                UIKit.Place(none.rectTransform, new Vector2(0f, 1f), new Vector2(4f, y), new Vector2(900f, 40f));
            }
            else
            {
                // Up to 4 defenders: one wide row each (readable on phones); more: two columns.
                int n = r.mastery.Count;
                bool wide = n <= 4;
                float rowH = wide ? 66f : n > 6 ? 54f : 62f, rowW = wide ? 1000f : 495f;
                for (int i = 0; i < n; i++)
                {
                    int col = wide ? 0 : i % 2, row = wide ? i : i / 2;
                    _rows.Add(BuildMasteryRow(right, r.mastery[i], new Vector2(col * 505f, y - row * (rowH + 6f)), rowH, rowW));
                }
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
            bool wide = w > 600f;
            RectTransform portrait = MenuKit.Portrait(bg.transform, d, h - 10f, false);
            UIKit.Place(portrait, new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(h - 10f, h - 10f));
            TextMeshProUGUI name = UIKit.Text("Name", bg.transform, d != null ? d.displayName : g.defenderId, 25f, UIPalette.Parchment, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(h + 4f, -4f), new Vector2(250f, 30f));
            var row = new MasteryRow { gain = g, wide = w > 600f };
            float barW = wide ? 380f : w - h - 18f;
            TextMeshProUGUI plus = UIKit.Text("Gain", bg.transform, "+" + g.gained, wide ? 25f : 22f, UIPalette.Leaf, TextAlignmentOptions.TopRight, true);
            if (wide)
            {
                // name ... +gain over the bar, level to its right, level-up badge in the remaining space.
                row.level = UIKit.Text("Level", bg.transform, "Lv " + g.levelBefore, 28f, UIPalette.Gold, TextAlignmentOptions.MidlineLeft, true);
                UIKit.Place(row.level.rectTransform, new Vector2(0f, 0.5f), new Vector2(h + 4f + barW + 14f, 0f), new Vector2(130f, 40f));
                UIKit.Place(plus.rectTransform, new Vector2(0f, 1f), new Vector2(h + 4f + barW - 90f, -6f), new Vector2(90f, 30f));
            }
            else
            {
                row.level = UIKit.Text("Level", bg.transform, "Lv " + g.levelBefore, 25f, UIPalette.Gold, TextAlignmentOptions.TopRight, true);
                UIKit.Place(row.level.rectTransform, new Vector2(1f, 1f), new Vector2(-12f, -4f), new Vector2(170f, 30f));
                UIKit.Place(plus.rectTransform, new Vector2(1f, 1f), new Vector2(-88f, -6f), new Vector2(90f, 28f));
            }
            row.fill = MenuKit.Bar(bg.transform, "Bar", UIPalette.Gold, new Vector2(0f, 0f), new Vector2(h + 4f, 9f), new Vector2(barW, 14f),
                MasteryRules.LevelProgress(g.xpBefore));
            if (g.LeveledUp)
            {
                string what = g.branchesUnlocked.Count > 0 ? (wide ? "New branch: " : "") + g.branchesUnlocked[0].displayName
                    : wide ? MasteryRules.PerkText(g.levelAfter) : MasteryRules.PerkShort(g.levelAfter);
                Image badge = UIKit.Panel("LevelUp", bg.transform, new Color(0.55f, 0.4f, 0.1f, 0.96f), true, false);
                badge.raycastTarget = false;
                if (wide) UIKit.Place(badge.rectTransform, new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(w - h - barW - 170f, h - 12f));
                else UIKit.Place(badge.rectTransform, new Vector2(1f, 0f), new Vector2(-8f, 2f), new Vector2(300f, 26f));
                TextMeshProUGUI bt = UIKit.Text("Text", badge.transform, "LEVEL UP!  " + what, wide ? 24f : 20f, UIPalette.Parchment, TextAlignmentOptions.Center, true);
                UIKit.Stretch(bt.rectTransform, wide ? 6f : 1f);
                bt.enableAutoSizing = true; bt.fontSizeMin = wide ? 18f : 12f; bt.fontSizeMax = wide ? 24f : 20f;
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
