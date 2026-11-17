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
        // Scroll positions survive the rebuild that every selection / purchase triggers.
        private RectTransform _scrollA, _scrollB;
        private Tab _builtTab;
        private bool _built;

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
            _tabResearch = UIKit.Button("TabResearch", _safe, "Research", UIPalette.ButtonSecondary, () => Show(Tab.Research), UIKit.Lib != null ? UIKit.Lib.iconSprout : null, 36f);
            UIKit.Place((RectTransform)_tabResearch.transform, new Vector2(1f, 1f), new Vector2(-898f, -14f), new Vector2(280f, MenuKit.TouchMin));
            _tabDefenders = UIKit.Button("TabDefenders", _safe, "Defenders", UIPalette.ButtonSecondary, () => Show(Tab.Defenders), UIKit.Lib != null ? UIKit.Lib.iconLeaf : null, 36f);
            UIKit.Place((RectTransform)_tabDefenders.transform, new Vector2(1f, 1f), new Vector2(-604f, -14f), new Vector2(280f, MenuKit.TouchMin));
        }

        public void Show(Tab tab)
        {
            _tab = tab;
            ((Image)_tabResearch.targetGraphic).color = tab == Tab.Research ? UIPalette.ButtonPrimary : UIPalette.ButtonSecondary;
            ((Image)_tabDefenders.targetGraphic).color = tab == Tab.Defenders ? UIPalette.ButtonPrimary : UIPalette.ButtonSecondary;
            bool keep = _built && _builtTab == tab;
            float a = keep && _scrollA != null ? _scrollA.anchoredPosition.y : 0f;
            float b = keep && _scrollB != null ? _scrollB.anchoredPosition.y : 0f;
            if (_body != null) Destroy(_body.gameObject);
            _scrollA = _scrollB = null;
            _body = UIKit.Rect("Body", _safe);
            _body.anchorMin = Vector2.zero;
            _body.anchorMax = Vector2.one;
            _body.offsetMin = new Vector2(MenuKit.Gutter, MenuKit.Gutter);
            _body.offsetMax = new Vector2(-MenuKit.Gutter, -MenuKit.TopBarHeight);
            if (tab == Tab.Research) BuildResearch();
            else BuildDefenders();
            if (_scrollA != null) _scrollA.anchoredPosition = new Vector2(_scrollA.anchoredPosition.x, a);
            if (_scrollB != null) _scrollB.anchoredPosition = new Vector2(_scrollB.anchoredPosition.x, b);
            _built = true;
            _builtTab = tab;
            _currency.Refresh();
        }

        // ================================================================ research
        // Readability pass (TD-36): three flexible-width columns in a vertical scroll; the detail panel on the right
        // always shows one discovery large (the next available one until the player taps another).
        private const float NodeH = 250f, NodeGap = 36f, ColGap = 28f, BranchHead = 58f, DetailW = 540f;

        private void BuildResearch()
        {
            CampaignDefinition c = Campaign;
            if (c == null) return;
            if (_selectedResearch == null) _selectedResearch = DefaultResearch(c);
            RectTransform tree = UIKit.Rect("Tree", _body);
            tree.anchorMin = Vector2.zero;
            tree.anchorMax = Vector2.one;
            tree.offsetMin = Vector2.zero;
            tree.offsetMax = new Vector2(-(DetailW + MenuKit.Gutter), 0f);
            for (int b = 0; b < 3; b++)
            {
                string name = c.researchBranchNames != null && b < c.researchBranchNames.Length ? c.researchBranchNames[b] : "";
                TextMeshProUGUI h = UIKit.Text("Branch" + b, tree, name, MenuKit.TextHeader, BranchColor(b), TextAlignmentOptions.TopLeft, true);
                Column(h.rectTransform, b, 0f, BranchHead);
                h.rectTransform.offsetMin += new Vector2(8f, 0f);
                MenuKit.Shadowed(h);
            }
            ScrollRect sr = MenuKit.Scroll(tree, "TreeScroll", false, out RectTransform content);
            RectTransform srt = (RectTransform)sr.transform;
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = new Vector2(0f, -BranchHead);
            int tiers = 0;
            foreach (ResearchDefinition r in c.research) if (r != null) tiers = Mathf.Max(tiers, r.tier + 1);
            const float top = 12f; // room for the selection outline
            content.sizeDelta = new Vector2(-18f, top + tiers * (NodeH + NodeGap));
            content.anchoredPosition = new Vector2(-9f, 0f); // keep clear of the scroll bar
            _scrollA = content;
            foreach (ResearchDefinition r in c.research)
            {
                if (r == null) continue;
                float y = -top - r.tier * (NodeH + NodeGap);
                if (r.prerequisite != null && r.prerequisite.branch == r.branch)
                {
                    // Connector from the prerequisite (directly above).
                    bool owned = Save.HasResearch(r.prerequisite.id);
                    Color bc = BranchColor(r.branch);
                    Image link = UIKit.Image("Link_" + r.id, content, MenuKit.Round, owned ? new Color(bc.r, bc.g, bc.b, 0.8f) : new Color(1f, 1f, 1f, 0.15f));
                    RectTransform lr = link.rectTransform;
                    lr.anchorMin = lr.anchorMax = new Vector2((r.branch + 0.5f) / 3f, 1f);
                    lr.pivot = new Vector2(0.5f, 0f);
                    lr.anchoredPosition = new Vector2(0f, y - 2f);
                    lr.sizeDelta = new Vector2(12f, NodeGap + 4f);
                }
                BuildNode(content, r, y);
            }
            BuildResearchDetail();
        }

        /// <summary>Places <paramref name="rt"/> in branch column <paramref name="b"/> (one third of the width each).</summary>
        private static void Column(RectTransform rt, int b, float y, float h)
        {
            rt.anchorMin = new Vector2(b / 3f, 1f);
            rt.anchorMax = new Vector2((b + 1) / 3f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(ColGap * 0.5f, y - h);
            rt.offsetMax = new Vector2(-ColGap * 0.5f, y);
        }

        private ResearchDefinition DefaultResearch(CampaignDefinition c)
        {
            ResearchDefinition first = null, open = null;
            foreach (ResearchDefinition r in c.research)
            {
                if (r == null) continue;
                if (first == null) first = r;
                ResearchState st = ResearchRules.StateOf(Save, r);
                if (st == ResearchState.Available && (open == null || r.tier < open.tier)) open = r;
            }
            return open ?? first;
        }

        private static Color BranchColor(int b) => b == 0 ? UIPalette.Gold : b == 1 ? UIPalette.Leaf : new Color(0.75f, 0.6f, 1f);

        private void BuildNode(RectTransform parent, ResearchDefinition r, float y)
        {
            ResearchState state = ResearchRules.StateOf(Save, r);
            Color bg = state == ResearchState.Owned ? Color.Lerp(BranchColor(r.branch), UIPalette.Panel, 0.6f)
                : state == ResearchState.Locked ? new Color(0.1f, 0.12f, 0.11f, 0.95f) : UIPalette.PanelLight;
            Button b = UIKit.Button("Research_" + r.id, parent, null, bg, () => { _selectedResearch = r; Show(Tab.Research); });
            var rt = (RectTransform)b.transform;
            Column(rt, r.branch, y, NodeH);
            if (_selectedResearch == r)
            {
                Image sel = UIKit.Image("Selected", rt, UIKit.Lib != null ? UIKit.Lib.uiRoundOutline : null, UIPalette.Gold);
                UIKit.Stretch(sel.rectTransform, -6f);
            }
            Image disc = UIKit.Image("Disc", rt, MenuKit.Circle, state == ResearchState.Locked ? MenuKit.LockedGrey : BranchColor(r.branch));
            UIKit.Place(disc.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(68f, 68f));
            if (state == ResearchState.Locked)
            {
                Image lk = UIKit.Image("Lock", disc.transform, UIKit.Lib != null ? UIKit.Lib.iconLock : null, new Color(1f, 1f, 1f, 0.6f));
                lk.preserveAspect = true;
                UIKit.Stretch(lk.rectTransform, 16f);
            }
            else
            {
                TextMeshProUGUI g = UIKit.Text("Glyph", disc.transform, r.glyph, 30f, new Color(0.1f, 0.14f, 0.1f), TextAlignmentOptions.Center, true);
                UIKit.Stretch(g.rectTransform);
            }
            TextMeshProUGUI n = UIKit.Text("Name", rt, r.displayName, 36f, state == ResearchState.Locked ? UIPalette.TextDim : UIPalette.Parchment, TextAlignmentOptions.MidlineLeft, true);
            n.rectTransform.anchorMin = new Vector2(0f, 1f);
            n.rectTransform.anchorMax = new Vector2(1f, 1f);
            n.rectTransform.pivot = new Vector2(0.5f, 1f);
            n.rectTransform.offsetMin = new Vector2(96f, -82f);
            n.rectTransform.offsetMax = new Vector2(-14f, -14f);
            n.textWrappingMode = TextWrappingModes.NoWrap;
            MenuKit.Fit(n, 36f, 30f);
            TextMeshProUGUI d = UIKit.Text("Desc", rt, r.description, MenuKit.TextSmall, state == ResearchState.Locked ? new Color(0.75f, 0.8f, 0.74f) : UIPalette.TextDim, TextAlignmentOptions.TopLeft);
            UIKit.Stretch(d.rectTransform);
            d.rectTransform.offsetMin = new Vector2(18f, 54f);
            d.rectTransform.offsetMax = new Vector2(-14f, -92f);
            MenuKit.Fit(d, MenuKit.TextSmall);
            string costText = state == ResearchState.Owned ? "RESEARCHED"
                : r.essenceCost > 0 ? $"{r.essenceCost} Essence" : $"{r.seedCost} Seeds";
            bool essence = r.essenceCost > 0;
            bool afford = essence ? Save.essence >= r.essenceCost : Save.seeds >= r.seedCost;
            Color cc = state == ResearchState.Owned ? UIPalette.Leaf : !afford ? UIPalette.Danger : essence ? MenuKit.EssenceColor : UIPalette.Gold;
            TextMeshProUGUI cost = UIKit.Text("Cost", rt, costText, MenuKit.TextSmall, cc, TextAlignmentOptions.BottomRight, true);
            UIKit.Place(cost.rectTransform, new Vector2(1f, 0f), new Vector2(-16f, 10f), new Vector2(320f, 40f));
        }

        private void BuildResearchDetail()
        {
            Image p = UIKit.Panel("Detail", _body, new Color(0f, 0f, 0f, 0.28f), true, false);
            RectTransform rt = p.rectTransform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.offsetMin = new Vector2(-DetailW, 0f);
            rt.offsetMax = Vector2.zero;
            ResearchDefinition r = _selectedResearch;
            if (r == null) return;
            ResearchState state = ResearchRules.StateOf(Save, r);
            bool buyable = state != ResearchState.Owned;
            ScrollRect sr = MenuKit.Scroll(rt, "DetailScroll", false, out RectTransform content);
            RectTransform srt = (RectTransform)sr.transform;
            UIKit.Stretch(srt, 6f);
            if (buyable) srt.offsetMin = new Vector2(6f, MenuKit.ButtonHeight + 40f);
            _scrollB = content;
            const float pad = 24f;
            float w = DetailW - 12f - 2f * pad - 12f;
            float y = -20f;
            TextMeshProUGUI Add(string name, string text, float size, Color color, bool display, float gapAfter)
            {
                TextMeshProUGUI t = UIKit.Text(name, content, text, size, color, TextAlignmentOptions.TopLeft, display);
                float h = MenuKit.TextHeight(t, text, w);
                UIKit.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(pad, y), new Vector2(w, h));
                y -= h + gapAfter;
                return t;
            }
            string branch = Campaign.researchBranchNames != null && r.branch < Campaign.researchBranchNames.Length ? Campaign.researchBranchNames[r.branch] : "";
            Add("Branch", branch.ToUpperInvariant() + "  ·  TIER " + (r.tier + 1), MenuKit.TextSmall, UIPalette.TextDim, true, 4f);
            Add("Name", r.displayName, 50f, BranchColor(r.branch), true, 14f);
            Add("Desc", r.description, 36f, UIPalette.Parchment, false, 24f);
            string status = state == ResearchState.Owned ? "<color=#7CD65A>Researched: active in every battle.</color>"
                : state == ResearchState.Locked ? $"Needs <b>{r.prerequisite.displayName}</b> first."
                : r.essenceCost > 0 ? $"Costs <color=#C78CFF><b>{r.essenceCost} Essence</b></color>\nYou have {Save.essence}."
                : $"Costs <color=#FFD35C><b>{r.seedCost} Seeds</b></color>\nYou have {Save.seeds}.";
            Add("Status", status, MenuKit.TextNumber, UIPalette.TextDim, false, 28f);
            Add("Earn", "<color=#FFD35C>Seeds</color> come from missions (full on the first win, a share on replays) and stars. " +
                        "<color=#C78CFF>Essence</color> comes from bosses and mission challenges.", MenuKit.TextSmall, new Color(0.75f, 0.82f, 0.74f), false, 16f);
            content.sizeDelta = new Vector2(0f, -y + 10f);
            if (buyable)
            {
                Button buy = UIKit.Button("Buy", rt, "Research", state == ResearchState.Available ? UIPalette.ButtonPrimary : MenuKit.LockedGrey, () => Buy(r),
                    UIKit.Lib != null ? UIKit.Lib.iconSprout : null, 42f);
                buy.interactable = state == ResearchState.Available;
                UIKit.Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(420f, MenuKit.ButtonHeight + 8f));
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
        private const float MasteryDetailW = 1000f, TileW = 224f, TileH = 268f;

        private void BuildDefenders()
        {
            List<DefenderDefinition> all = _app.Config.content.defenders;
            if (_selectedDefender == null && all.Count > 0) _selectedDefender = all[0];
            ScrollRect sr = MenuKit.Scroll(_body, "DefenderScroll", false, out RectTransform content);
            RectTransform srt = (RectTransform)sr.transform;
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = new Vector2(-(MasteryDetailW + MenuKit.Gutter), 0f);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(TileW, TileH);
            grid.spacing = new Vector2(14f, 16f);
            grid.padding = new RectOffset(4, 18, 4, 12);
            grid.constraint = GridLayoutGroup.Constraint.Flexible;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scrollA = content;
            for (int i = 0; i < all.Count; i++)
            {
                DefenderDefinition d = all[i];
                bool owned = Save.IsUnlocked(d.id);
                Button b = UIKit.Button("Defender_" + d.id, content, null, d == _selectedDefender ? Color.Lerp(d.accentColor, UIPalette.Panel, 0.55f) : owned ? UIPalette.PanelLight : new Color(0.1f, 0.11f, 0.1f, 0.95f),
                    () => { _selectedDefender = d; Show(Tab.Defenders); });
                var rt = (RectTransform)b.transform;
                RectTransform pt = MenuKit.Portrait(rt, d, 140f, !owned);
                UIKit.Place(pt, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(140f, 140f));
                TextMeshProUGUI n = UIKit.Text("Name", rt, d.displayName, MenuKit.TextLabel, owned ? UIPalette.Parchment : UIPalette.TextDim, TextAlignmentOptions.Center, true);
                UIKit.Place(n.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(TileW - 12f, 44f));
                n.textWrappingMode = TextWrappingModes.NoWrap;
                MenuKit.Fit(n, MenuKit.TextLabel);
                if (owned) MenuKit.MasteryPips(rt, MasteryRules.LevelOf(Save, d.id), 30f, new Vector2(0.5f, 0f), new Vector2(0f, 16f));
                else
                {
                    TextMeshProUGUI l = UIKit.Text("Locked", rt, "LOCKED", MenuKit.TextMin, UIPalette.TextDim, TextAlignmentOptions.Center, true);
                    UIKit.Place(l.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(TileW - 12f, 36f));
                }
            }
            BuildDefenderDetail(_selectedDefender);
        }

        private void BuildDefenderDetail(DefenderDefinition d)
        {
            Image p = UIKit.Panel("Detail", _body, new Color(0f, 0f, 0f, 0.28f), true, false);
            RectTransform rt = p.rectTransform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.offsetMin = new Vector2(-MasteryDetailW, 0f);
            rt.offsetMax = Vector2.zero;
            if (d == null) return;
            ScrollRect sr = MenuKit.Scroll(rt, "DetailScroll", false, out RectTransform c);
            UIKit.Stretch((RectTransform)sr.transform, 6f);
            _scrollB = c;
            const float pad = 22f;
            float w = MasteryDetailW - 12f - 2f * pad - 12f;
            bool owned = Save.IsUnlocked(d.id);
            int xp = Save.MasteryXp(d.id), level = MasteryRules.LevelFor(xp);
            RectTransform pt = MenuKit.Portrait(c, d, 140f, !owned);
            UIKit.Place(pt, new Vector2(0f, 1f), new Vector2(pad, -18f), new Vector2(140f, 140f));
            float tx = pad + 160f, tw = w - 160f;
            TextMeshProUGUI n = UIKit.Text("Name", c, d.displayName, 52f, Color.Lerp(d.accentColor, Color.white, 0.4f), TextAlignmentOptions.TopLeft, true);
            UIKit.Place(n.rectTransform, new Vector2(0f, 1f), new Vector2(tx, -16f), new Vector2(tw, 62f));
            TextMeshProUGUI role = UIKit.Text("Role", c, $"{d.role}  ·  {d.cost} Vitality  ·  {d.strengths}", MenuKit.TextSmall, UIPalette.TextDim, TextAlignmentOptions.TopLeft);
            UIKit.Place(role.rectTransform, new Vector2(0f, 1f), new Vector2(tx, -80f), new Vector2(tw, 40f));
            role.textWrappingMode = TextWrappingModes.NoWrap;
            MenuKit.Fit(role, MenuKit.TextSmall);
            float y;
            if (!owned)
            {
                EncounterDefinition by = Campaign != null ? CampaignRules.UnlockedBy(Campaign, d) : null;
                string txt = (by != null ? $"Joins your garden when you win <b>{by.displayName}</b>.\n\n" : "Not in your garden yet.\n\n") + d.description;
                TextMeshProUGUI l = UIKit.Text("Locked", c, txt, MenuKit.TextBody, UIPalette.Parchment, TextAlignmentOptions.TopLeft);
                float lh = MenuKit.TextHeight(l, txt, w);
                UIKit.Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(pad, -180f), new Vector2(w, lh));
                c.sizeDelta = new Vector2(0f, 180f + lh + 24f);
                return;
            }
            TextMeshProUGUI lv = UIKit.Text("Level", c, level >= MasteryRules.MaxLevel ? "Mastery 4 · MAX" : $"Mastery {level}", MenuKit.TextNumber, UIPalette.Gold, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(lv.rectTransform, new Vector2(0f, 1f), new Vector2(tx, -122f), new Vector2(260f, 44f));
            MenuKit.Bar(c, "XpBar", UIPalette.Gold, new Vector2(0f, 1f), new Vector2(tx + 270f, -134f), new Vector2(280f, 22f), MasteryRules.LevelProgress(xp));
            TextMeshProUGUI next = UIKit.Text("Next", c, level >= MasteryRules.MaxLevel ? "" : $"{MasteryRules.XpToNext(xp)} to go", MenuKit.TextSmall, UIPalette.TextDim, TextAlignmentOptions.TopLeft);
            UIKit.Place(next.rectTransform, new Vector2(0f, 1f), new Vector2(tx + 570f, -124f), new Vector2(tw - 570f, 40f));
            y = -180f;
            string howText = "Mastery grows when you plant and evolve this defender in battle (a loss still counts).";
            TextMeshProUGUI how = UIKit.Text("How", c, howText, MenuKit.TextSmall, UIPalette.TextDim, TextAlignmentOptions.TopLeft);
            float hh = MenuKit.TextHeight(how, howText, w);
            UIKit.Place(how.rectTransform, new Vector2(0f, 1f), new Vector2(pad, y), new Vector2(w, hh));
            y -= hh + 16f;

            // Mastery path: what each level opens (rows grow with their text).
            const float badgeW = 92f, lockW = 56f;
            for (int l = 1; l <= MasteryRules.MaxLevel; l++)
            {
                bool reached = level >= l;
                var lines = new List<string>();
                foreach (EvolutionDefinition e in MasteryRules.BranchesAt(d, l))
                    lines.Add($"<b><color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(e.accentColor, Color.white, 0.3f))}>{e.displayName}</color></b>  {e.description}");
                string perk = MasteryRules.PerkText(l);
                if (!string.IsNullOrEmpty(perk)) lines.Add($"<b>Perk:</b> {perk}");
                if (lines.Count == 0) continue;
                string text = string.Join("\n", lines);
                Image row = UIKit.Panel("Lv" + l, c, reached ? new Color(0.2f, 0.32f, 0.2f, 0.7f) : new Color(0f, 0f, 0f, 0.25f), reached, false);
                TextMeshProUGUI body = UIKit.Text("Body", row.transform, text, MenuKit.TextSmall, reached ? UIPalette.Parchment : UIPalette.TextDim, TextAlignmentOptions.MidlineLeft);
                body.paragraphSpacing = 8f;
                float bw = w - badgeW - lockW;
                float rowH = Mathf.Max(84f, MenuKit.TextHeight(body, text, bw) + 28f);
                UIKit.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(pad, y), new Vector2(w, rowH));
                body.rectTransform.anchorMin = Vector2.zero;
                body.rectTransform.anchorMax = Vector2.one;
                body.rectTransform.offsetMin = new Vector2(badgeW, 8f);
                body.rectTransform.offsetMax = new Vector2(-lockW, -8f);
                Image badge = UIKit.Image("Badge", row.transform, MenuKit.Circle, reached ? UIPalette.Gold : MenuKit.LockedGrey);
                UIKit.Place(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(64f, 64f));
                TextMeshProUGUI bn = UIKit.Text("N", badge.transform, l.ToString(), 36f, new Color(0.1f, 0.12f, 0.1f), TextAlignmentOptions.Center, true);
                UIKit.Stretch(bn.rectTransform);
                if (!reached)
                {
                    Image lk = UIKit.Image("Lock", row.transform, UIKit.Lib != null ? UIKit.Lib.iconLock : null, new Color(1f, 1f, 1f, 0.4f));
                    lk.preserveAspect = true;
                    UIKit.Place(lk.rectTransform, new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(38f, 38f));
                }
                y -= rowH + 12f;
            }
            c.sizeDelta = new Vector2(0f, -y + 12f);
        }
    }
}
