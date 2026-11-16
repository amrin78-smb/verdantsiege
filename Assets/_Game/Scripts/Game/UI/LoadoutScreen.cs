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
        private const float LeftWidth = 1160f;
        private const float SlotW = 136f, SlotH = 184f;
        private const float TileW = 182f, TileH = 196f;

        private MainMenuController _hub;
        private AppRoot _app;
        private RectTransform _safe, _deckRow, _grid, _adviceBox;
        private TextMeshProUGUI _deckCount, _advice;
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
            // ---- left: deck and collection
            RectTransform left = UIKit.Rect("Left", _safe);
            left.anchorMin = new Vector2(0f, 0f);
            left.anchorMax = new Vector2(0f, 1f);
            left.pivot = new Vector2(0f, 1f);
            left.offsetMin = new Vector2(28f, 28f);
            left.offsetMax = new Vector2(28f + LeftWidth, -(MenuKit.TopBarHeight + 8f));
            TextMeshProUGUI dh = UIKit.Text("DeckHead", left, "Your deck", 40f, UIPalette.Gold, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(dh.rectTransform, new Vector2(0f, 1f), new Vector2(4f, 0f), new Vector2(400f, 50f));
            _deckCount = UIKit.Text("DeckCount", left, "", 30f, UIPalette.TextDim, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(_deckCount.rectTransform, new Vector2(0f, 1f), new Vector2(236f, -8f), new Vector2(600f, 40f));
            Image deckBg = UIKit.Panel("DeckRow", left, new Color(0f, 0f, 0f, 0.25f), true, false);
            _deckRow = deckBg.rectTransform;
            UIKit.Place(_deckRow, new Vector2(0f, 1f), new Vector2(0f, -56f), new Vector2(LeftWidth, SlotH + 28f));

            TextMeshProUGUI ch = UIKit.Text("CollectionHead", left, "Collection", 40f, UIPalette.Gold, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(ch.rectTransform, new Vector2(0f, 1f), new Vector2(4f, -(56f + SlotH + 48f)), new Vector2(600f, 50f));
            TextMeshProUGUI chHint = UIKit.Text("CollectionHint", left, "Tap to add  ·  tap a deck card to remove", 24f, UIPalette.TextDim, TextAlignmentOptions.TopLeft);
            UIKit.Place(chHint.rectTransform, new Vector2(0f, 1f), new Vector2(240f, -(56f + SlotH + 58f)), new Vector2(800f, 34f));
            _grid = UIKit.Rect("Grid", left);
            UIKit.Place(_grid, new Vector2(0f, 1f), new Vector2(0f, -(56f + SlotH + 108f)), new Vector2(LeftWidth, 2 * TileH + 18f));

            // ---- right: intel, advice, begin
            Image right = UIKit.Panel("Intel", _safe, new Color(0f, 0f, 0f, 0.25f), true, false);
            RectTransform r = right.rectTransform;
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 1f);
            r.offsetMin = new Vector2(28f + LeftWidth + 28f, 28f);
            r.offsetMax = new Vector2(-28f, -(MenuKit.TopBarHeight + 8f));
            TextMeshProUGUI brief = UIKit.Text("Briefing", r, e_Briefing(), 27f, UIPalette.Parchment, TextAlignmentOptions.TopLeft);
            brief.rectTransform.anchorMin = new Vector2(0f, 1f);
            brief.rectTransform.anchorMax = new Vector2(1f, 1f);
            brief.rectTransform.pivot = new Vector2(0.5f, 1f);
            brief.rectTransform.offsetMin = new Vector2(26f, -380f);
            brief.rectTransform.offsetMax = new Vector2(-26f, -20f);
            brief.enableAutoSizing = true; brief.fontSizeMin = 18f; brief.fontSizeMax = 27f;

            Image adv = UIKit.Panel("Advice", r, new Color(0.35f, 0.25f, 0.05f, 0.55f), true, false);
            _adviceBox = adv.rectTransform;
            _adviceBox.anchorMin = new Vector2(0f, 0f);
            _adviceBox.anchorMax = new Vector2(1f, 0f);
            _adviceBox.pivot = new Vector2(0.5f, 0f);
            _adviceBox.offsetMin = new Vector2(20f, 160f);
            _adviceBox.offsetMax = new Vector2(-20f, 160f + 300f);
            _advice = UIKit.Text("Text", _adviceBox, "", 25f, UIPalette.Parchment, TextAlignmentOptions.TopLeft);
            UIKit.Stretch(_advice.rectTransform, 18f);
            _advice.enableAutoSizing = true; _advice.fontSizeMin = 17f; _advice.fontSizeMax = 25f;

            Button rec = UIKit.Button("Recommended", r, "Recommended", UIPalette.ButtonSecondary, UseRecommended, null, 30f);
            UIKit.Place((RectTransform)rec.transform, new Vector2(0f, 0f), new Vector2(18f, 24f), new Vector2(244f, 112f));
            Button begin = UIKit.Button("Begin", r, "Begin Battle", UIPalette.ButtonPrimary, Begin, UIKit.Lib != null ? UIKit.Lib.iconPlay : null, 38f);
            UIKit.Place((RectTransform)begin.transform, new Vector2(1f, 0f), new Vector2(-18f, 24f), new Vector2(330f, 112f));
            begin.gameObject.AddComponent<Pulse>().amount = 0.03f;

            Refresh();
        }

        private string e_Briefing()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"<b><size=120%><color=#FFD35C>{_e.displayName}</color></size></b>\n{_e.briefing}\n\n<color=#FFD35C><b>Threats</b></color>\n");
            int n = 0;
            foreach (EnemyDefinition en in LoadoutRules.Threats(_e))
            {
                if (n++ >= 7) { sb.Append("…\n"); break; }
                sb.Append($"• <b>{en.displayName}</b>");
                if (!string.IsNullOrEmpty(en.counterHint)) sb.Append($" <color=#C9D8C4><size=85%>{en.counterHint}</size></color>");
                sb.Append('\n');
            }
            if (_e.boss != null) sb.Append($"• <b><color=#FF8FA0>{_e.boss.displayName}</color></b> <size=85%><color=#C9D8C4>(boss)</color></size>\n");
            return sb.ToString();
        }

        private void Refresh()
        {
            foreach (Transform c in _deckRow) if (c.name.StartsWith("Slot")) Destroy(c.gameObject);
            foreach (Transform c in _grid) Destroy(c.gameObject);
            _deckCount.text = $"{_deck.Count} / {_slots} cards" + (_slots > _e.roster.Count ? "  <color=#7CD65A>(+Seed Satchel)</color>" : "");

            float x0 = 14f;
            for (int i = 0; i < _slots; i++)
            {
                DefenderDefinition d = i < _deck.Count ? _deck[i] : null;
                RectTransform slot = d != null ? Card(_deckRow, "Slot" + i, d, SlotW, SlotH, CardState.InDeck, () => Remove(d))
                                               : EmptySlot(_deckRow, "Slot" + i);
                UIKit.Place(slot, new Vector2(0f, 0.5f), new Vector2(x0 + i * (SlotW + 6f), 0f), new Vector2(SlotW, SlotH));
            }

            List<DefenderDefinition> all = _app.Config.content.defenders;
            for (int i = 0; i < all.Count; i++)
            {
                DefenderDefinition d = all[i];
                bool owned = Save.IsUnlocked(d.id);
                bool inDeck = _deck.Contains(d);
                CardState state = !owned ? CardState.Locked : inDeck ? CardState.Chosen : CardState.Available;
                RectTransform tile = Card(_grid, "Tile_" + d.id, d, TileW, TileH, state, () => Toggle(d));
                int col = i % 6, row = i / 6;
                UIKit.Place(tile, new Vector2(0f, 1f), new Vector2(col * (TileW + 10f), -row * (TileH + 14f)), new Vector2(TileW, TileH));
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
            float pd = Mathf.Min(w * 0.62f, h - 104f);
            RectTransform portrait = MenuKit.Portrait(rt, d, pd, locked);
            UIKit.Place(portrait, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(pd, pd));
            TextMeshProUGUI n = UIKit.Text("Name", rt, d.displayName, 23f, locked ? UIPalette.TextDim : UIPalette.Parchment, TextAlignmentOptions.Center, true);
            UIKit.Place(n.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -pd - 14f), new Vector2(w - 10f, 30f));
            n.enableAutoSizing = true; n.fontSizeMin = 15f; n.fontSizeMax = 23f;
            if (locked)
            {
                EncounterDefinition by = _app.Progression.Campaign != null ? CampaignRules.UnlockedBy(_app.Progression.Campaign, d) : null;
                TextMeshProUGUI l = UIKit.Text("Unlock", rt, by != null ? "Win " + by.displayName : "Locked", 18f, UIPalette.TextDim, TextAlignmentOptions.Center);
                UIKit.Place(l.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(w - 12f, 40f));
                l.enableAutoSizing = true; l.fontSizeMin = 13f; l.fontSizeMax = 18f;
                return rt;
            }
            // Cost + mastery.
            Image vi = UIKit.Image("Vit", rt, UIKit.Lib != null ? UIKit.Lib.iconVitality : null, Color.white);
            vi.preserveAspect = true;
            UIKit.Place(vi.rectTransform, new Vector2(0f, 0f), new Vector2(10f, 30f), new Vector2(28f, 28f));
            TextMeshProUGUI cost = UIKit.Text("Cost", rt, d.cost.ToString(), 22f, UIPalette.Vitality, TextAlignmentOptions.MidlineLeft, true);
            UIKit.Place(cost.rectTransform, new Vector2(0f, 0f), new Vector2(40f, 30f), new Vector2(60f, 28f));
            MenuKit.MasteryPips(rt, MasteryRules.LevelOf(Save, d.id), 18f, new Vector2(0.5f, 0f), new Vector2(0f, 6f));
            if (state == CardState.Chosen)
            {
                Image check = UIKit.Image("InDeck", rt, MenuKit.Circle, UIPalette.Leaf);
                UIKit.Place(check.rectTransform, new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(40f, 40f));
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
