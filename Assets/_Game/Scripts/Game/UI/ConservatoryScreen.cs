using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VerdantSiege.App;
using VerdantSiege.Battle;
using VerdantSiege.Data;
using VerdantSiege.Progression;
using VerdantSiege.Save;

namespace VerdantSiege.UI
{
    /// <summary>
    /// Seed Conservatory (Milestone 4). Research tab: three short branches (Sunlight, Cultivation, Evolution) of
    /// tactical upgrades bought with Seeds (basic) or Essence (advanced). Defenders tab: the collection with each
    /// defender's mastery level and its mastery path (which evolution branches and perks each level opens).
    /// </summary>
    public sealed class ConservatoryScreen : MonoBehaviour
    {
        public enum Tab { Research = 0, Defenders = 1 }

        private MainMenuController _hub;
        private AppRoot _app;
        private RectTransform _safe, _body;
        private CurrencyBar _currency;
        private Tab _tab;
        private ResearchDefinition _selectedResearch;
        private DefenderDefinition _selectedDefender;
        private Button _tabResearch, _tabDefenders;

        public Tab Current => _tab;

        public static ConservatoryScreen Open(MainMenuController hub, Transform canvas, Tab tab)
        {
            GameObject go = MenuKit.Frame(canvas, "Conservatory", "Seed Conservatory", hub.ShowSanctuary, out RectTransform safe, out CurrencyBar cur);
            var s = go.AddComponent<ConservatoryScreen>();
            s._hub = hub;
            s._app = AppRoot.Ensure();
            s._safe = safe;
            s._currency = cur;
            s.BuildTabs();
            s.Show(tab);
            PopIn.Play(go);
            return s;
        }

        private SaveData Save => _app.Save.Data;
        private CampaignDefinition Campaign => _app.Progression.Campaign;

        private void BuildTabs()
        {
            _tabResearch = UIKit.Button("TabResearch", _safe, "Research", UIPalette.ButtonSecondary, () => Show(Tab.Research), UIKit.Lib != null ? UIKit.Lib.iconSprout : null, 34f);
            UIKit.Place((RectTransform)_tabResearch.transform, new Vector2(1f, 1f), new Vector2(-874f, -22f), new Vector2(262f, 82f));
            _tabDefenders = UIKit.Button("TabDefenders", _safe, "Defenders", UIPalette.ButtonSecondary, () => Show(Tab.Defenders), UIKit.Lib != null ? UIKit.Lib.iconLeaf : null, 34f);
            UIKit.Place((RectTransform)_tabDefenders.transform, new Vector2(1f, 1f), new Vector2(-600f, -22f), new Vector2(262f, 82f));
        }

        public void Show(Tab tab)
        {
            _tab = tab;
            ((Image)_tabResearch.targetGraphic).color = tab == Tab.Research ? UIPalette.ButtonPrimary : UIPalette.ButtonSecondary;
            ((Image)_tabDefenders.targetGraphic).color = tab == Tab.Defenders ? UIPalette.ButtonPrimary : UIPalette.ButtonSecondary;
            if (_body != null) Destroy(_body.gameObject);
            _body = UIKit.Rect("Body", _safe);
            _body.anchorMin = Vector2.zero;
            _body.anchorMax = Vector2.one;
            _body.offsetMin = new Vector2(28f, 28f);
            _body.offsetMax = new Vector2(-28f, -(MenuKit.TopBarHeight + 6f));
            if (tab == Tab.Research) BuildResearch();
            else BuildDefenders();
            _currency.Refresh();
        }

        // ================================================================ research
        private const float NodeW = 420f, NodeH = 128f, NodeGap = 26f, ColGap = 30f;

        private void BuildResearch()
        {
            CampaignDefinition c = Campaign;
            if (c == null) return;
            for (int b = 0; b < 3; b++)
            {
                float x = b * (NodeW + ColGap);
                string name = c.researchBranchNames != null && b < c.researchBranchNames.Length ? c.researchBranchNames[b] : "";
                TextMeshProUGUI h = UIKit.Text("Branch" + b, _body, name, 38f, BranchColor(b), TextAlignmentOptions.TopLeft, true);
                UIKit.Place(h.rectTransform, new Vector2(0f, 1f), new Vector2(x + 6f, 0f), new Vector2(NodeW, 48f));
                MenuKit.Shadowed(h);
            }
            foreach (ResearchDefinition r in c.research)
            {
                if (r == null) continue;
                float x = r.branch * (NodeW + ColGap), y = -58f - r.tier * (NodeH + NodeGap);
                if (r.prerequisite != null && r.prerequisite.branch == r.branch)
                {
                    // Connector from the prerequisite (directly above).
                    float py = -58f - r.prerequisite.tier * (NodeH + NodeGap) - NodeH;
                    bool owned = Save.HasResearch(r.prerequisite.id);
                    Image link = UIKit.Image("Link_" + r.id, _body, MenuKit.Round, owned ? new Color(BranchColor(r.branch).r, BranchColor(r.branch).g, BranchColor(r.branch).b, 0.8f) : new Color(1f, 1f, 1f, 0.15f));
                    UIKit.Place(link.rectTransform, new Vector2(0f, 1f), new Vector2(x + NodeW * 0.5f - 6f, py), new Vector2(12f, py - y));
                }
                BuildNode(r, new Vector2(x, y));
            }
            BuildResearchDetail();
        }

        private static Color BranchColor(int b) => b == 0 ? UIPalette.Gold : b == 1 ? UIPalette.Leaf : new Color(0.75f, 0.6f, 1f);

        private void BuildNode(ResearchDefinition r, Vector2 pos)
        {
            ResearchState state = ResearchRules.StateOf(Save, r);
            Color bg = state == ResearchState.Owned ? Color.Lerp(BranchColor(r.branch), UIPalette.Panel, 0.6f)
                : state == ResearchState.Locked ? new Color(0.1f, 0.12f, 0.11f, 0.95f) : UIPalette.PanelLight;
            Button b = UIKit.Button("Research_" + r.id, _body, null, bg, () => { _selectedResearch = r; Show(Tab.Research); });
            var rt = (RectTransform)b.transform;
            UIKit.Place(rt, new Vector2(0f, 1f), pos, new Vector2(NodeW, NodeH));
            if (_selectedResearch == r)
            {
                Image sel = UIKit.Image("Selected", rt, UIKit.Lib != null ? UIKit.Lib.uiRoundOutline : null, UIPalette.Gold);
                UIKit.Stretch(sel.rectTransform, -6f);
            }
            Image disc = UIKit.Image("Disc", rt, MenuKit.Circle, state == ResearchState.Locked ? MenuKit.LockedGrey : BranchColor(r.branch));
            UIKit.Place(disc.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(84f, 84f));
            TextMeshProUGUI g = UIKit.Text("Glyph", disc.transform, state == ResearchState.Locked ? "" : r.glyph, 32f, new Color(0.1f, 0.14f, 0.1f), TextAlignmentOptions.Center, true);
            UIKit.Stretch(g.rectTransform);
            TextMeshProUGUI n = UIKit.Text("Name", rt, r.displayName, 30f, state == ResearchState.Locked ? UIPalette.TextDim : UIPalette.Parchment, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(n.rectTransform, new Vector2(0f, 1f), new Vector2(108f, -10f), new Vector2(NodeW - 118f, 38f));
            TextMeshProUGUI d = UIKit.Text("Desc", rt, r.description, 21f, UIPalette.TextDim, TextAlignmentOptions.TopLeft);
            UIKit.Place(d.rectTransform, new Vector2(0f, 1f), new Vector2(109f, -48f), new Vector2(NodeW - 120f, 50f));
            d.enableAutoSizing = true; d.fontSizeMin = 15f; d.fontSizeMax = 21f;
            if (state == ResearchState.Owned)
            {
                TextMeshProUGUI o = UIKit.Text("Owned", rt, "RESEARCHED", 20f, UIPalette.Leaf, TextAlignmentOptions.BottomRight, true);
                UIKit.Place(o.rectTransform, new Vector2(1f, 0f), new Vector2(-14f, 6f), new Vector2(220f, 28f));
            }
            else
            {
                bool essence = r.essenceCost > 0;
                bool afford = essence ? Save.essence >= r.essenceCost : Save.seeds >= r.seedCost;
                TextMeshProUGUI cost = UIKit.Text("Cost", rt, essence ? $"{r.essenceCost} Essence" : $"{r.seedCost} Seeds", 22f,
                    !afford ? UIPalette.Danger : essence ? MenuKit.EssenceColor : UIPalette.Gold, TextAlignmentOptions.BottomRight, true);
                UIKit.Place(cost.rectTransform, new Vector2(1f, 0f), new Vector2(-14f, 6f), new Vector2(220f, 28f));
                if (state == ResearchState.Locked)
                {
                    Image lk = UIKit.Image("Lock", rt, UIKit.Lib != null ? UIKit.Lib.iconLock : null, new Color(1f, 1f, 1f, 0.4f));
                    lk.preserveAspect = true;
                    UIKit.Place(lk.rectTransform, new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(40f, 40f));
                }
            }
        }

        private void BuildResearchDetail()
        {
            float x = 3 * (NodeW + ColGap);
            Image p = UIKit.Panel("Detail", _body, new Color(0f, 0f, 0f, 0.28f), true, false);
            RectTransform rt = p.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(x, 0f);
            rt.offsetMax = Vector2.zero;
            ResearchDefinition r = _selectedResearch;
            if (r == null)
            {
                TextMeshProUGUI intro = UIKit.Text("Intro", rt,
                    "<color=#FFD35C><b>Research</b></color>\nEach discovery changes the rules of every battle a little: more time, more light, new ways to grow.\n\n" +
                    "<color=#FFD35C>Seeds</color> come from missions (full on the first win, a share on replays) and stars.\n" +
                    "<color=#C78CFF>Essence</color> comes from bosses and mission challenges.\n\nTap a discovery to see it.",
                    27f, UIPalette.Parchment, TextAlignmentOptions.TopLeft);
                UIKit.Stretch(intro.rectTransform, 26f);
                intro.enableAutoSizing = true; intro.fontSizeMin = 18f; intro.fontSizeMax = 27f;
                return;
            }
            ResearchState state = ResearchRules.StateOf(Save, r);
            TextMeshProUGUI n = UIKit.Text("Name", rt, r.displayName, 46f, BranchColor(r.branch), TextAlignmentOptions.TopLeft, true);
            UIKit.Place(n.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -22f), new Vector2(440f, 56f));
            n.enableAutoSizing = true; n.fontSizeMin = 30f; n.fontSizeMax = 46f;
            TextMeshProUGUI d = UIKit.Text("Desc", rt, r.description, 30f, UIPalette.Parchment, TextAlignmentOptions.TopLeft);
            UIKit.Place(d.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -90f), new Vector2(440f, 170f));
            string status = state == ResearchState.Owned ? "<color=#7CD65A>Researched: active in every battle.</color>"
                : state == ResearchState.Locked ? $"Needs <b>{r.prerequisite.displayName}</b> first."
                : r.essenceCost > 0 ? $"Costs <color=#C78CFF>{r.essenceCost} Essence</color> (you have {Save.essence})."
                : $"Costs <color=#FFD35C>{r.seedCost} Seeds</color> (you have {Save.seeds}).";
            TextMeshProUGUI s = UIKit.Text("Status", rt, status, 27f, UIPalette.TextDim, TextAlignmentOptions.TopLeft);
            UIKit.Place(s.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -280f), new Vector2(440f, 120f));
            if (state != ResearchState.Owned)
            {
                Button buy = UIKit.Button("Buy", rt, "Research", state == ResearchState.Available ? UIPalette.ButtonPrimary : MenuKit.LockedGrey, () => Buy(r),
                    UIKit.Lib != null ? UIKit.Lib.iconSprout : null, 40f);
                buy.interactable = state == ResearchState.Available;
                UIKit.Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(380f, 108f));
            }
        }

        /// <summary>Buys a research node (also used by the autoplay harness). Returns the result.</summary>
        public ResearchResult Buy(ResearchDefinition r)
        {
            ResearchResult res = _app.Progression.Purchase(r);
            if (res == ResearchResult.Ok)
            {
                if (Audio.AudioService.Instance != null) Audio.AudioService.Instance.Play(Audio.Sfx.Evolve);
                _selectedResearch = r;
                Show(Tab.Research);
                MenuKit.Toast(_safe, r.displayName + " researched!", UIPalette.Leaf);
            }
            else MenuKit.Toast(_safe, res == ResearchResult.NotEnoughSeeds ? "Not enough Seeds yet." : res == ResearchResult.NotEnoughEssence ? "Not enough Essence yet."
                : res == ResearchResult.PrerequisiteMissing ? "Research the discovery above it first." : "Already researched.", UIPalette.Danger);
            return res;
        }

        // ================================================================ defenders (mastery)
        private void BuildDefenders()
        {
            List<DefenderDefinition> all = _app.Config.content.defenders;
            if (_selectedDefender == null && all.Count > 0) _selectedDefender = all[0];
            const float w = 196f, h = 214f;
            for (int i = 0; i < all.Count; i++)
            {
                DefenderDefinition d = all[i];
                bool owned = Save.IsUnlocked(d.id);
                Button b = UIKit.Button("Defender_" + d.id, _body, null, d == _selectedDefender ? Color.Lerp(d.accentColor, UIPalette.Panel, 0.55f) : owned ? UIPalette.PanelLight : new Color(0.1f, 0.11f, 0.1f, 0.95f),
                    () => { _selectedDefender = d; Show(Tab.Defenders); });
                var rt = (RectTransform)b.transform;
                UIKit.Place(rt, new Vector2(0f, 1f), new Vector2((i % 4) * (w + 12f), -(i / 4) * (h + 14f)), new Vector2(w, h));
                RectTransform pt = MenuKit.Portrait(rt, d, 118f, !owned);
                UIKit.Place(pt, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(118f, 118f));
                TextMeshProUGUI n = UIKit.Text("Name", rt, d.displayName, 24f, owned ? UIPalette.Parchment : UIPalette.TextDim, TextAlignmentOptions.Center, true);
                UIKit.Place(n.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -134f), new Vector2(w - 10f, 32f));
                n.enableAutoSizing = true; n.fontSizeMin = 16f; n.fontSizeMax = 24f;
                if (owned) MenuKit.MasteryPips(rt, MasteryRules.LevelOf(Save, d.id), 24f, new Vector2(0.5f, 0f), new Vector2(0f, 14f));
            }
            BuildDefenderDetail(_selectedDefender, 4 * (w + 12f) + 18f);
        }

        private void BuildDefenderDetail(DefenderDefinition d, float x)
        {
            Image p = UIKit.Panel("Detail", _body, new Color(0f, 0f, 0f, 0.28f), true, false);
            RectTransform rt = p.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(x, 0f);
            rt.offsetMax = Vector2.zero;
            if (d == null) return;
            bool owned = Save.IsUnlocked(d.id);
            int xp = Save.MasteryXp(d.id), level = MasteryRules.LevelFor(xp);
            RectTransform pt = MenuKit.Portrait(rt, d, 132f, !owned);
            UIKit.Place(pt, new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(132f, 132f));
            TextMeshProUGUI n = UIKit.Text("Name", rt, d.displayName, 50f, Color.Lerp(d.accentColor, Color.white, 0.4f), TextAlignmentOptions.TopLeft, true);
            UIKit.Place(n.rectTransform, new Vector2(0f, 1f), new Vector2(176f, -20f), new Vector2(560f, 60f));
            TextMeshProUGUI role = UIKit.Text("Role", rt, $"{d.role}  ·  {d.cost} Vitality  ·  {d.strengths}", 24f, UIPalette.TextDim, TextAlignmentOptions.TopLeft);
            UIKit.Place(role.rectTransform, new Vector2(0f, 1f), new Vector2(178f, -82f), new Vector2(720f, 32f));
            role.enableAutoSizing = true; role.fontSizeMin = 16f; role.fontSizeMax = 24f;
            if (!owned)
            {
                EncounterDefinition by = Campaign != null ? CampaignRules.UnlockedBy(Campaign, d) : null;
                TextMeshProUGUI l = UIKit.Text("Locked", rt, (by != null ? $"Joins your garden when you win <b>{by.displayName}</b>.\n\n" : "Not in your garden yet.\n\n") + d.description,
                    28f, UIPalette.Parchment, TextAlignmentOptions.TopLeft);
                UIKit.Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -180f), new Vector2(880f, 300f));
                return;
            }
            TextMeshProUGUI lv = UIKit.Text("Level", rt, level >= MasteryRules.MaxLevel ? "Mastery 4 · MAX" : $"Mastery {level}", 30f, UIPalette.Gold, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(lv.rectTransform, new Vector2(0f, 1f), new Vector2(178f, -118f), new Vector2(300f, 38f));
            MenuKit.Bar(rt, "XpBar", UIPalette.Gold, new Vector2(0f, 1f), new Vector2(430f, -126f), new Vector2(300f, 18f), MasteryRules.LevelProgress(xp));
            TextMeshProUGUI next = UIKit.Text("Next", rt, level >= MasteryRules.MaxLevel ? "" : $"{MasteryRules.XpToNext(xp)} to go", 22f, UIPalette.TextDim, TextAlignmentOptions.TopLeft);
            UIKit.Place(next.rectTransform, new Vector2(0f, 1f), new Vector2(746f, -120f), new Vector2(200f, 30f));
            TextMeshProUGUI how = UIKit.Text("How", rt, "Mastery grows when you plant and evolve this defender in battle (a loss still counts).", 22f, UIPalette.TextDim, TextAlignmentOptions.TopLeft);
            UIKit.Place(how.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -166f), new Vector2(900f, 30f));

            // Mastery path: what each level opens.
            float y = -212f;
            for (int l = 1; l <= MasteryRules.MaxLevel; l++)
            {
                bool reached = level >= l;
                var lines = new List<string>();
                foreach (EvolutionDefinition e in MasteryRules.BranchesAt(d, l))
                    lines.Add($"<b><color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(e.accentColor, Color.white, 0.3f))}>{e.displayName}</color></b>  <size=85%>{e.description}</size>");
                string perk = MasteryRules.PerkText(l);
                if (!string.IsNullOrEmpty(perk)) lines.Add($"<b>Perk:</b> <size=90%>{perk}</size>");
                if (lines.Count == 0) continue;
                float rowH = 30f + lines.Count * 40f;
                Image row = UIKit.Panel("Lv" + l, rt, reached ? new Color(0.2f, 0.32f, 0.2f, 0.7f) : new Color(0f, 0f, 0f, 0.25f), reached, false);
                UIKit.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(22f, y), new Vector2(920f, rowH));
                Image badge = UIKit.Image("Badge", row.transform, MenuKit.Circle, reached ? UIPalette.Gold : MenuKit.LockedGrey);
                UIKit.Place(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(60f, 60f));
                TextMeshProUGUI bn = UIKit.Text("N", badge.transform, l.ToString(), 32f, new Color(0.1f, 0.12f, 0.1f), TextAlignmentOptions.Center, true);
                UIKit.Stretch(bn.rectTransform);
                TextMeshProUGUI body = UIKit.Text("Body", row.transform, string.Join("\n", lines), 25f, reached ? UIPalette.Parchment : UIPalette.TextDim, TextAlignmentOptions.MidlineLeft);
                body.rectTransform.anchorMin = Vector2.zero;
                body.rectTransform.anchorMax = Vector2.one;
                body.rectTransform.offsetMin = new Vector2(90f, 6f);
                body.rectTransform.offsetMax = new Vector2(-14f, -6f);
                body.enableAutoSizing = true; body.fontSizeMin = 15f; body.fontSizeMax = 25f;
                if (!reached)
                {
                    Image lk = UIKit.Image("Lock", row.transform, UIKit.Lib != null ? UIKit.Lib.iconLock : null, new Color(1f, 1f, 1f, 0.35f));
                    lk.preserveAspect = true;
                    UIKit.Place(lk.rectTransform, new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(34f, 34f));
                }
                y -= rowH + 12f;
            }
        }
    }
}
