using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VerdantSiege.App;
using VerdantSiege.Data;
using VerdantSiege.Progression;
using VerdantSiege.Save;

namespace VerdantSiege.UI
{
    /// <summary>
    /// Pre-battle loadout (Milestone 4): pick the deck for a mission from the collection (tap to add, tap a deck card
    /// to remove), with mission intel (threats and counters) and live advice about gaps in the deck. The deck is saved
    /// per mission (and as "last used") when the battle begins.
    /// </summary>
    public sealed class LoadoutScreen : MonoBehaviour
    {
        // Readability pass (TD-36): big cards; the deck row scrolls sideways, the collection and the intel scroll down.
        private const float RightWidth = 700f;
        private const float SlotW = 212f, SlotH = 258f;
        private const float TileW = 228f, TileH = 280f;
        private const float DeckHead = 60f, DeckRowH = SlotH + 30f;

        private MainMenuController _hub;
        private AppRoot _app;
        private RectTransform _safe, _deckRow, _grid, _adviceBox, _intelContent;
        private TextMeshProUGUI _deckCount, _advice, _brief;
        private EncounterDefinition _e;
        private int _slots;
        private readonly List<DefenderDefinition> _deck = new List<DefenderDefinition>();

        public EncounterDefinition Mission => _e;
        public IReadOnlyList<DefenderDefinition> Deck => _deck;

        public static LoadoutScreen Open(MainMenuController hub, Transform canvas, EncounterDefinition e)
        {
            GameObject go = MenuKit.Frame(canvas, "Loadout", "Prepare: " + e.displayName, hub.ShowWorldMap, out RectTransform safe, out _);
            var s = go.AddComponent<LoadoutScreen>();
            s._hub = hub;
            s._app = AppRoot.Ensure();
            s._safe = safe;
            s._e = e;
            s._slots = s._app.Progression.DeckSlots(e);
            s._deck.AddRange(s._app.Progression.DeckFor(e));
            s.Build();
            PopIn.Play(go);
            return s;
        }

        private SaveData Save => _app.Save.Data;

        private void Build()
        {
            float g = MenuKit.Gutter, top = MenuKit.TopBarHeight;
            // ---- left: deck (horizontal scroll) and collection (vertical scroll grid); takes all the width the intel leaves
            RectTransform left = UIKit.Rect("Left", _safe);
            left.anchorMin = Vector2.zero;
            left.anchorMax = Vector2.one;
            left.offsetMin = new Vector2(g, g);
            left.offsetMax = new Vector2(-(RightWidth + 2f * g), -top);
            TextMeshProUGUI dh = UIKit.Text("DeckHead", left, "Your deck", MenuKit.TextHeader, UIPalette.Gold, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(dh.rectTransform, new Vector2(0f, 1f), new Vector2(4f, 0f), new Vector2(300f, 54f));
            _deckCount = UIKit.Text("DeckCount", left, "", MenuKit.TextLabel, UIPalette.TextDim, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(_deckCount.rectTransform, new Vector2(0f, 1f), new Vector2(270f, -8f), new Vector2(760f, 44f));
            Image deckBg = UIKit.Panel("DeckRow", left, new Color(0f, 0f, 0f, 0.25f), true, false);
            RectTransform deckPanel = deckBg.rectTransform;
            deckPanel.anchorMin = new Vector2(0f, 1f);
            deckPanel.anchorMax = new Vector2(1f, 1f);
            deckPanel.pivot = new Vector2(0.5f, 1f);
            deckPanel.offsetMin = new Vector2(0f, -(DeckHead + DeckRowH));
            deckPanel.offsetMax = new Vector2(0f, -DeckHead);
            ScrollRect deckScroll = MenuKit.Scroll(deckPanel, "DeckScroll", true, out _deckRow);
            UIKit.Stretch((RectTransform)deckScroll.transform, 4f);
            var row = _deckRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(10, 10, 0, 0);
            row.spacing = 12f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            _deckRow.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            float collTop = DeckHead + DeckRowH + 22f;
            TextMeshProUGUI ch = UIKit.Text("CollectionHead", left, "Collection", MenuKit.TextHeader, UIPalette.Gold, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(ch.rectTransform, new Vector2(0f, 1f), new Vector2(4f, -collTop), new Vector2(300f, 54f));
            TextMeshProUGUI chHint = UIKit.Text("CollectionHint", left, "Tap to add  ·  tap a deck card to remove", MenuKit.TextSmall, UIPalette.TextDim, TextAlignmentOptions.TopLeft);
            chHint.rectTransform.anchorMin = new Vector2(0f, 1f);
            chHint.rectTransform.anchorMax = new Vector2(1f, 1f);
            chHint.rectTransform.pivot = new Vector2(0f, 1f);
            chHint.rectTransform.offsetMin = new Vector2(280f, -(collTop + 50f));
            chHint.rectTransform.offsetMax = new Vector2(0f, -(collTop + 10f));
            MenuKit.Fit(chHint, MenuKit.TextSmall);
            ScrollRect gridScroll = MenuKit.Scroll(left, "CollectionScroll", false, out _grid);
            RectTransform gs = (RectTransform)gridScroll.transform;
            gs.anchorMin = Vector2.zero;
            gs.anchorMax = Vector2.one;
            gs.offsetMin = Vector2.zero;
            gs.offsetMax = new Vector2(0f, -(collTop + 62f));
            var grid = _grid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(TileW, TileH);
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(6, 18, 8, 12);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.Flexible;
            _grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ---- right: intel (scrolls) above the two actions
            Image right = UIKit.Panel("Intel", _safe, new Color(0f, 0f, 0f, 0.25f), true, false);
            RectTransform r = right.rectTransform;
            r.anchorMin = new Vector2(1f, 0f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(1f, 0.5f);
            r.offsetMin = new Vector2(-(RightWidth + g), g);
            r.offsetMax = new Vector2(-g, -top);
            ScrollRect intel = MenuKit.Scroll(r, "IntelScroll", false, out _intelContent);
            RectTransform isr = (RectTransform)intel.transform;
            isr.anchorMin = Vector2.zero;
            isr.anchorMax = Vector2.one;
            isr.offsetMin = new Vector2(6f, MenuKit.ButtonHeight + 44f);
            isr.offsetMax = new Vector2(-6f, -8f);
            _brief = UIKit.Text("Briefing", _intelContent, e_Briefing(), MenuKit.TextBody, UIPalette.Parchment, TextAlignmentOptions.TopLeft);
            Image adv = UIKit.Panel("Advice", _intelContent, new Color(0.35f, 0.25f, 0.05f, 0.55f), true, false);
            _adviceBox = adv.rectTransform;
            _advice = UIKit.Text("Text", _adviceBox, "", MenuKit.TextBody, UIPalette.Parchment, TextAlignmentOptions.TopLeft);
            UIKit.Stretch(_advice.rectTransform, 18f);

            Button rec = UIKit.Button("Recommended", r, "Recommended", UIPalette.ButtonSecondary, UseRecommended, null, MenuKit.TextSmall + 2f);
            UIKit.Place((RectTransform)rec.transform, new Vector2(0f, 0f), new Vector2(18f, 22f), new Vector2(280f, MenuKit.ButtonHeight + 8f));
            Button begin = UIKit.Button("Begin", r, "Begin Battle", UIPalette.ButtonPrimary, Begin, UIKit.Lib != null ? UIKit.Lib.iconPlay : null, 40f);
            UIKit.Place((RectTransform)begin.transform, new Vector2(1f, 0f), new Vector2(-18f, 22f), new Vector2(360f, MenuKit.ButtonHeight + 8f));
            begin.gameObject.AddComponent<Pulse>().amount = 0.03f;

            Refresh();
        }

        /// <summary>Stacks the briefing and the advice box in the intel scroll (heights from the wrapped text).</summary>
        private void LayoutIntel()
        {
            const float pad = 22f;
            float w = RightWidth - 12f - 2f * pad - 14f; // scroll viewport minus padding and the scroll bar
            float bh = MenuKit.TextHeight(_brief, _brief.text, w);
            UIKit.Place(_brief.rectTransform, new Vector2(0f, 1f), new Vector2(pad, -16f), new Vector2(w, bh));
            float ah = MenuKit.TextHeight(_advice, _advice.text, w - 36f) + 36f;
            UIKit.Place(_adviceBox, new Vector2(0f, 1f), new Vector2(pad - 4f, -16f - bh - 18f), new Vector2(w + 8f, ah));
            _intelContent.sizeDelta = new Vector2(0f, 16f + bh + 18f + ah + 20f);
        }

        private string e_Briefing()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"<b><size=120%><color=#FFD35C>{_e.displayName}</color></size></b>\n{_e.briefing}\n\n<color=#FFD35C><b>Threats</b></color>\n");
            foreach (EnemyDefinition en in LoadoutRules.Threats(_e)) // the intel scrolls: list every threat
            {
                sb.Append($"• <b>{en.displayName}</b>");
                if (!string.IsNullOrEmpty(en.counterHint)) sb.Append($"\n   <color=#C9D8C4><size=90%>{en.counterHint}</size></color>");
                sb.Append('\n');
            }
            if (_e.boss != null) sb.Append($"• <b><color=#FF8FA0>{_e.boss.displayName}</color></b> <size=90%><color=#C9D8C4>(boss)</color></size>\n");
            return sb.ToString();
        }

        private void Refresh()
        {
            // Deactivate before destroying so the layout groups ignore the old cards this frame.
            foreach (Transform c in _deckRow) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
            foreach (Transform c in _grid) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
            _deckCount.text = $"{_deck.Count} / {_slots} cards" + (_slots > _e.roster.Count ? "  <color=#7CD65A>(+Seed Satchel)</color>" : "");

            for (int i = 0; i < _slots; i++)
            {
                DefenderDefinition d = i < _deck.Count ? _deck[i] : null;
                RectTransform slot = d != null ? Card(_deckRow, "Slot" + i, d, SlotW, SlotH, CardState.InDeck, () => Remove(d))
                                               : EmptySlot(_deckRow, "Slot" + i);
                slot.sizeDelta = new Vector2(SlotW, SlotH);
            }

            List<DefenderDefinition> all = _app.Config.content.defenders;
            for (int i = 0; i < all.Count; i++)
            {
                DefenderDefinition d = all[i];
                bool owned = Save.IsUnlocked(d.id);
                bool inDeck = _deck.Contains(d);
                CardState state = !owned ? CardState.Locked : inDeck ? CardState.Chosen : CardState.Available;
                Card(_grid, "Tile_" + d.id, d, TileW, TileH, state, () => Toggle(d));
            }

            List<string> tips = LoadoutRules.Advice(_e, _deck, _app.Progression.Collection());
            LoadoutIssue issue = LoadoutRules.Validate(_deck, _slots, Save);
            if (issue != LoadoutIssue.Ok) tips.Insert(0, LoadoutRules.Describe(issue));
            if (tips.Count == 0)
            {
                _advice.text = "<color=#7CD65A><b>Ready.</b></color> This deck has an answer for every threat in the briefing.";
                ((Image)_adviceBox.GetComponent<Image>()).color = new Color(0.15f, 0.35f, 0.15f, 0.55f);
            }
            else
            {
                _advice.text = "<color=#FFD35C><b>Advice</b></color>\n• " + string.Join("\n• ", tips);
                ((Image)_adviceBox.GetComponent<Image>()).color = new Color(0.38f, 0.26f, 0.05f, 0.6f);
            }
            LayoutIntel();
        }

        private enum CardState { InDeck, Chosen, Available, Locked }

        private RectTransform Card(RectTransform parent, string name, DefenderDefinition d, float w, float h, CardState state, System.Action onTap)
        {
            bool locked = state == CardState.Locked;
            Color bgColor = state == CardState.InDeck ? Color.Lerp(d.accentColor, UIPalette.Panel, 0.7f)
                : state == CardState.Chosen ? new Color(0.12f, 0.2f, 0.15f, 0.9f)
                : locked ? new Color(0.1f, 0.11f, 0.1f, 0.95f) : UIPalette.PanelLight;
            Button b = UIKit.Button(name, parent, null, bgColor, onTap);
            var rt = (RectTransform)b.transform;
            rt.sizeDelta = new Vector2(w, h);
            float pd = Mathf.Min(w * 0.66f, h - 140f);
            RectTransform portrait = MenuKit.Portrait(rt, d, pd, locked);
            UIKit.Place(portrait, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(pd, pd));
            TextMeshProUGUI n = UIKit.Text("Name", rt, d.displayName, MenuKit.TextLabel, locked ? UIPalette.TextDim : UIPalette.Parchment, TextAlignmentOptions.Center, true);
            UIKit.Place(n.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -pd - 16f), new Vector2(w - 12f, 42f));
            n.textWrappingMode = TextWrappingModes.NoWrap;
            MenuKit.Fit(n, MenuKit.TextLabel);
            if (locked)
            {
                EncounterDefinition by = _app.Progression.Campaign != null ? CampaignRules.UnlockedBy(_app.Progression.Campaign, d) : null;
                TextMeshProUGUI l = UIKit.Text("Unlock", rt, by != null ? "Win " + by.displayName : "Locked", MenuKit.TextMin, UIPalette.TextDim, TextAlignmentOptions.Center);
                UIKit.Place(l.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(w - 12f, h - pd - 70f));
                l.lineSpacing = -12f;
                return rt;
            }
            // Cost + mastery.
            float ic = MenuKit.TextNumber + 4f;
            Image vi = UIKit.Image("Vit", rt, UIKit.Lib != null ? UIKit.Lib.iconVitality : null, Color.white);
            vi.preserveAspect = true;
            UIKit.Place(vi.rectTransform, new Vector2(0.5f, 0f), new Vector2(-36f, 34f), new Vector2(ic, ic));
            TextMeshProUGUI cost = UIKit.Text("Cost", rt, d.cost.ToString(), MenuKit.TextNumber, UIPalette.Vitality, TextAlignmentOptions.MidlineLeft, true);
            UIKit.Place(cost.rectTransform, new Vector2(0.5f, 0f), new Vector2(34f, 30f), new Vector2(90f, ic + 8f));
            cost.overflowMode = TextOverflowModes.Overflow; // a short number: never truncate it to nothing
            MenuKit.MasteryPips(rt, MasteryRules.LevelOf(Save, d.id), 24f, new Vector2(0.5f, 0f), new Vector2(0f, 5f));
            if (state == CardState.Chosen)
            {
                Image check = UIKit.Image("InDeck", rt, MenuKit.Circle, UIPalette.Leaf);
                UIKit.Place(check.rectTransform, new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(48f, 48f));
                Image leaf = UIKit.Image("Leaf", check.transform, UIKit.Lib != null ? UIKit.Lib.iconLeaf : null, Color.white);
                UIKit.Stretch(leaf.rectTransform, 7f);
                UIKit.Group(rt.gameObject).alpha = 0.6f;
            }
            return rt;
        }

        private RectTransform EmptySlot(RectTransform parent, string name)
        {
            Image e = UIKit.Image(name, parent, MenuKit.Round, new Color(1f, 1f, 1f, 0.06f));
            if (UIKit.Lib != null && UIKit.Lib.uiRoundOutline != null)
            {
                Image o = UIKit.Image("Edge", e.transform, UIKit.Lib.uiRoundOutline, new Color(1f, 1f, 1f, 0.25f));
                UIKit.Stretch(o.rectTransform);
            }
            TextMeshProUGUI t = UIKit.Text("Plus", e.transform, "+", 70f, new Color(1f, 1f, 1f, 0.3f), TextAlignmentOptions.Center, true);
            UIKit.Stretch(t.rectTransform);
            return e.rectTransform;
        }

        private void Toggle(DefenderDefinition d)
        {
            if (!Save.IsUnlocked(d.id))
            {
                EncounterDefinition by = _app.Progression.Campaign != null ? CampaignRules.UnlockedBy(_app.Progression.Campaign, d) : null;
                MenuKit.Toast(_safe, d.displayName + (by != null ? " joins your garden when you win " + by.displayName + "." : " is not in your garden yet."), UIPalette.Parchment);
                return;
            }
            if (_deck.Contains(d)) { Remove(d); return; }
            if (_deck.Count >= _slots)
            {
                MenuKit.Toast(_safe, "Deck full: tap a card in your deck to swap it out.", UIPalette.Gold);
                return;
            }
            _deck.Add(d);
            Refresh();
        }

        private void Remove(DefenderDefinition d)
        {
            _deck.Remove(d);
            Refresh();
        }

        /// <summary>The mission's authored roster (owned cards), topped up to the slot count.</summary>
        public void UseRecommended()
        {
            _deck.Clear();
            foreach (DefenderDefinition d in LoadoutRules.Recommended(_e, Save)) if (_deck.Count < _slots) _deck.Add(d);
            Refresh();
        }

        public void Begin()
        {
            LoadoutIssue issue = _app.Progression.SaveDeck(_e, new List<DefenderDefinition>(_deck));
            if (issue != LoadoutIssue.Ok)
            {
                MenuKit.Toast(_safe, LoadoutRules.Describe(issue), UIPalette.Danger);
                return;
            }
            AppRoot.LoadBattle(_e.id);
        }
    }
}
