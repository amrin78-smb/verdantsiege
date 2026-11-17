using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VerdantSiege.Audio;
using VerdantSiege.Battle;
using VerdantSiege.Battle.View;
using VerdantSiege.Data;

namespace VerdantSiege.UI
{
    /// <summary>
    /// Living Evolution HUD: the compact chooser card (2-3 branch rows: icon, name, one-line description, cost),
    /// the growth strip on top of the defender inspect panel (XP bar, "Evolve" button, chosen branch, Capacitor
    /// charge) and the tappable world badges (<see cref="EvolutionBadges"/>). Rules live in the testable
    /// <see cref="EvolutionChooserState"/>; battle time keeps running. The card docks on the right edge, above
    /// or below depending on where the defender is, so it never hides the plant being evolved; tapping the
    /// battlefield elsewhere or picking a card closes it.
    /// </summary>
    public sealed class EvolutionChooser : MonoBehaviour
    {
        // TD-36: descriptions 36 (min 34) across the full row width (cost pill beside the name), bold 48 costs.
        private const float CardWidth = 780f;
        private const float RowHeight = 150f;
        private const float HeaderHeight = 112f;

        private BattleController _ctrl;
        private BattleSimulation _sim;
        private BattleHud _hud;
        private Camera _cam;
        private EvolutionBadges _badges;

        private RectTransform _card;
        private TextMeshProUGUI _title, _subtitle;
        private readonly List<Row> _rows = new List<Row>();
        private float _shake;

        // inspect strip
        private RectTransform _strip;
        private Image _stripFill, _stripDisc;
        private RectTransform _stripTrack;
        private TextMeshProUGUI _stripLabel, _stripValue;
        private Button _stripEvolve;

        private sealed class Row
        {
            public RectTransform root;
            public Button button;
            public Image bg, disc;
            public TextMeshProUGUI glyph, name, desc, cost;
            public Image costIcon;
            public CanvasGroup group;
        }

        public EvolutionChooserState State { get; private set; }
        public EvolutionBadges Badges => _badges;
        public RectTransform Card => _card;
        public bool IsOpen => State != null && State.IsOpen;

        /// <summary>Builds the chooser, the inspect strip and the badges for a battle (one call from BattleController).</summary>
        public static EvolutionChooser Create(BattleController ctrl, BattleHud hud, Camera cam, BattleLayout layout)
        {
            var go = new GameObject("EvolutionUI");
            go.transform.SetParent(hud.transform, false);
            var c = go.AddComponent<EvolutionChooser>();
            c.Build(ctrl, hud, cam, layout);
            return c;
        }

        private void Build(BattleController ctrl, BattleHud hud, Camera cam, BattleLayout layout)
        {
            _ctrl = ctrl;
            _sim = ctrl.Sim;
            _hud = hud;
            _cam = cam;
            State = new EvolutionChooserState(_sim);
            State.Changed += Refresh;
            State.Denied += OnDenied;

            _badges = gameObject.AddComponent<EvolutionBadges>();
            _badges.Init(_sim, layout, cam, hud.Canvas.transform);
            _badges.EvolveTapped += d => { if (State.Open(d)) _hud.ShowInspect(null); };
            _badges.ReleaseTapped += d => _sim.TryReleaseCapacitor(d);

            BuildCard();
            BuildStrip();
            Refresh();
        }

        // ================================================================ input from the battlefield

        /// <summary>
        /// A tap on the battlefield resolved to this defender (or null). A charged Capacitor releases; a ready
        /// defender opens the chooser (returns true: the caller skips the inspect panel); anything else closes it.
        /// </summary>
        public bool OnDefenderTapped(DefenderRuntime d)
        {
            if (d != null && _sim.IsCapacitor(d) && d.storedVitality > 0) _sim.TryReleaseCapacitor(d);
            bool open = State.OnBattlefieldTap(d);
            if (open) _hud.ShowInspect(null);
            return open;
        }

        /// <summary>Same as tapping option <paramref name="i"/> on the card (also used by the autoplay harness).</summary>
        public EvolveResult Choose(int i)
        {
            EvolveResult r = State.Choose(i);
            if (r == EvolveResult.Ok && AudioService.Instance != null) AudioService.Instance.Play(Sfx.UiTap, 0.6f);
            return r;
        }

        private void OnDenied(EvolveResult r)
        {
            _shake = 0.35f;
            _hud.DenyVitality();
            if (AudioService.Instance != null) AudioService.Instance.Play(Sfx.CardDenied);
        }

        // ================================================================ card
        private void BuildCard()
        {
            Image panel = UIKit.Panel("EvolutionChooser", _hud.Root, UIPalette.Panel, true, true);
            _card = panel.rectTransform;
            _title = UIKit.Text("Title", _card, "", 48f, UIPalette.Gold, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -10f), new Vector2(CardWidth - 130f, 58f));
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            MenuKit.Fit(_title, 48f, 38f);
            _subtitle = UIKit.Text("Subtitle", _card, "Choose one. The battle keeps going.", MenuKit.TextMin, UIPalette.TextDim, TextAlignmentOptions.TopLeft);
            UIKit.Place(_subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -64f), new Vector2(CardWidth - 130f, 44f));
            Button close = UIKit.Button("Close", _card, null, UIPalette.ButtonSecondary, () => State.Close(), UIKit.Lib != null ? UIKit.Lib.iconClose : null);
            UIKit.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(96f, 96f));
            for (int i = 0; i < 3; i++) _rows.Add(BuildRow(i));
            _card.gameObject.SetActive(false);
        }

        private Row BuildRow(int index)
        {
            var r = new Row();
            r.button = UIKit.Button("Option" + index, _card, null, UIPalette.PanelLight, () => Choose(index));
            r.bg = (Image)r.button.targetGraphic;
            r.root = (RectTransform)r.button.transform;
            r.group = UIKit.Group(r.root.gameObject);
            Sprite circle = UIKit.Lib != null ? UIKit.Lib.uiCircle : null;
            r.disc = UIKit.Image("Disc", r.root, circle, Color.white);
            UIKit.Place(r.disc.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(84f, 84f));
            r.glyph = UIKit.Text("Glyph", r.disc.transform, "", 38f, new Color(0.1f, 0.14f, 0.1f), TextAlignmentOptions.Center, true);
            UIKit.Stretch(r.glyph.rectTransform);
            r.name = UIKit.Text("Name", r.root, "", 42f, UIPalette.Parchment, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(r.name.rectTransform, new Vector2(0f, 1f), new Vector2(110f, -8f), new Vector2(CardWidth - 28f - 110f - 190f, 56f));
            r.name.textWrappingMode = TextWrappingModes.NoWrap;
            MenuKit.Fit(r.name, 42f, 34f);
            r.desc = UIKit.Text("Desc", r.root, "", 36f, UIPalette.Parchment, TextAlignmentOptions.TopLeft);
            UIKit.Place(r.desc.rectTransform, new Vector2(0f, 1f), new Vector2(110f, -66f), new Vector2(CardWidth - 28f - 110f - 14f, 80f));
            r.desc.lineSpacing = -10f;
            MenuKit.Fit(r.desc, 36f);
            Image pill = UIKit.Image("Cost", r.root, UIKit.Lib != null ? UIKit.Lib.uiRound : null, new Color(0f, 0f, 0f, 0.35f));
            UIKit.Place(pill.rectTransform, new Vector2(1f, 1f), new Vector2(-10f, -8f), new Vector2(176f, 62f));
            r.costIcon = UIKit.Image("Icon", pill.transform, UIKit.Lib != null ? UIKit.Lib.iconVitality : null, Color.white);
            r.costIcon.preserveAspect = true;
            UIKit.Place(r.costIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(52f, 52f));
            r.cost = UIKit.Text("Value", pill.transform, "", 48f, UIPalette.Vitality, TextAlignmentOptions.MidlineRight, true);
            UIKit.Place(r.cost.rectTransform, new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(104f, 60f));
            return r;
        }

        private void Refresh()
        {
            _badges.HighlightId = State.IsOpen ? State.Target.Id : 0;
            if (!State.IsOpen)
            {
                if (_card.gameObject.activeSelf) _card.gameObject.SetActive(false);
                return;
            }
            DefenderRuntime d = State.Target;
            List<EvolutionDefinition> evos = d.Definition.evolutions;
            _title.text = d.Definition.displayName + " can evolve!";
            int n = Mathf.Min(evos.Count, _rows.Count);
            float y = -HeaderHeight;
            for (int i = 0; i < _rows.Count; i++)
            {
                Row r = _rows[i];
                bool on = i < n && evos[i] != null;
                r.root.gameObject.SetActive(on);
                if (!on) continue;
                EvolutionDefinition e = evos[i];
                UIKit.Place(r.root, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(CardWidth - 28f, RowHeight));
                y -= RowHeight + 10f;
                r.disc.color = e.accentColor;
                r.glyph.text = string.IsNullOrEmpty(e.glyph) ? e.displayName.Substring(0, 1) : e.glyph;
                bool unlocked = _sim.BranchUnlocked(d, i);
                r.name.text = unlocked ? e.displayName : e.displayName + "  <size=85%><color=#C9D8C4>LOCKED</color></size>";
                r.name.color = unlocked ? Color.Lerp(UIPalette.Parchment, e.accentColor, 0.35f) : UIPalette.TextDim;
                // Milestone 4: specialist branches open with defender mastery (earned by using it in battle).
                r.desc.text = unlocked ? e.description : $"Reach mastery {e.masteryRequired} with {d.Definition.displayName} to unlock.";
                r.cost.text = _sim.EvolutionCost(d, i).ToString();
                r.disc.color = unlocked ? e.accentColor : new Color(0.35f, 0.37f, 0.35f);
            }
            float height = HeaderHeight + n * (RowHeight + 10f) + 6f;
            Vector3 sp = _cam.WorldToScreenPoint(_ctrl.Layout.CellCenter(d.Coord) + Vector3.up * 0.6f);
            bool defenderLow = sp.y < Screen.height * 0.5f;
            float top = BattleHud.TopBarHeight + 20f + (_sim.Boss != null ? EncounterAlertsHud.BarHeight + 8f : 0f);
            if (defenderLow) UIKit.Place(_card, new Vector2(1f, 1f), new Vector2(-16f, -top), new Vector2(CardWidth, height));
            else UIKit.Place(_card, new Vector2(1f, 0f), new Vector2(-16f, 16f), new Vector2(CardWidth, height));
            _card.SetAsLastSibling();
            if (!_card.gameObject.activeSelf)
            {
                _card.gameObject.SetActive(true);
                PopIn.Play(_card.gameObject);
            }
        }

        // ================================================================ inspect strip
        private void BuildStrip()
        {
            RectTransform inspect = _hud.InspectPanel;
            if (inspect == null) return;
            Image bg = UIKit.Panel("GrowthStrip", inspect, UIPalette.Panel, true, false);
            _strip = bg.rectTransform;
            _strip.anchorMin = new Vector2(0f, 1f);
            _strip.anchorMax = new Vector2(1f, 1f);
            _strip.pivot = new Vector2(0.5f, 0f);
            _strip.offsetMin = new Vector2(0f, 8f);
            _strip.offsetMax = new Vector2(0f, 8f + 84f);
            Sprite circle = UIKit.Lib != null ? UIKit.Lib.uiCircle : null;
            _stripDisc = UIKit.Image("Disc", _strip, circle, UIPalette.Gold);
            UIKit.Place(_stripDisc.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(48f, 48f));
            _stripLabel = UIKit.Text("Label", _strip, "Growth", 36f, UIPalette.Parchment, TextAlignmentOptions.MidlineLeft, true);
            UIKit.Place(_stripLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(70f, 0f), new Vector2(360f, 60f));
            Image track = UIKit.Image("Track", _strip, UIKit.Lib != null ? UIKit.Lib.uiRound : null, new Color(0f, 0f, 0f, 0.5f));
            _stripTrack = track.rectTransform;
            UIKit.Place(_stripTrack, new Vector2(0f, 0.5f), new Vector2(210f, 0f), new Vector2(260f, 24f));
            _stripFill = UIKit.Image("Fill", _stripTrack, UIKit.Lib != null ? UIKit.Lib.uiRound : null, UIPalette.Gold);
            _stripFill.type = Image.Type.Filled;
            _stripFill.fillMethod = Image.FillMethod.Horizontal;
            UIKit.Stretch(_stripFill.rectTransform, 2f);
            _stripValue = UIKit.Text("Value", _strip, "", 34f, UIPalette.TextDim, TextAlignmentOptions.MidlineRight, true);
            UIKit.Place(_stripValue.rectTransform, new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(130f, 60f));
            _stripEvolve = UIKit.Button("Evolve", _strip, "Evolve!", UIPalette.ButtonPrimary, () =>
            {
                DefenderRuntime d = _hud.Inspected;
                if (d != null && State.Open(d)) _hud.ShowInspect(null);
            }, null, 38f);
            UIKit.Place((RectTransform)_stripEvolve.transform, new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(220f, 72f));
            _strip.gameObject.SetActive(false);
        }

        private void UpdateStrip()
        {
            if (_strip == null) return;
            DefenderRuntime d = _hud.Inspected;
            bool show = d != null && d.alive && d.Definition.CanEvolve && _hud.InspectPanel.gameObject.activeSelf;
            if (_strip.gameObject.activeSelf != show) _strip.gameObject.SetActive(show);
            if (!show) return;
            EvolutionDefinition evo = d.evolution;
            bool ready = d.EvolutionReady;
            _stripEvolve.gameObject.SetActive(ready);
            if (evo != null)
            {
                _stripDisc.color = evo.accentColor;
                _stripTrack.gameObject.SetActive(false);
                if (_sim.IsCapacitor(d))
                {
                    _stripLabel.text = $"{evo.displayName}: {d.storedPulses}/{evo.capacitorPulses}";
                    _stripValue.text = d.storedPulses > 0 ? "Tap to release" : "";
                    _stripValue.rectTransform.sizeDelta = new Vector2(240f, 60f);
                }
                else
                {
                    _stripLabel.text = "Evolved: " + evo.displayName;
                    _stripValue.text = "";
                }
                _stripLabel.rectTransform.sizeDelta = new Vector2(560f, 60f);
                _stripLabel.color = Color.Lerp(UIPalette.Parchment, evo.accentColor, 0.5f);
                return;
            }
            _stripDisc.color = ready ? UIPalette.Gold : new Color(0.6f, 0.55f, 0.35f);
            _stripLabel.text = ready ? "Ready to evolve" : "Growth";
            _stripLabel.color = ready ? UIPalette.Gold : UIPalette.Parchment;
            _stripLabel.rectTransform.sizeDelta = new Vector2(ready ? 380f : 140f, 60f);
            _stripTrack.gameObject.SetActive(!ready);
            _stripFill.fillAmount = d.XpFraction;
            _stripValue.text = ready ? "" : Mathf.FloorToInt(d.XpFraction * 100f) + "%";
            _stripValue.rectTransform.sizeDelta = new Vector2(130f, 60f);
        }

        // ================================================================ frame
        private void Update()
        {
            if (_sim == null) return;
            // Close when the target is gone/evolved, the battle ended, or the player picked up a card.
            if (State.IsOpen && (_ctrl.SelectedSlot >= 0 || !State.Validate())) State.Close();
            UpdateStrip();
            if (!State.IsOpen) return;
            int vit = _sim.Vitality.Amount;
            List<EvolutionDefinition> evos = State.Target.Definition.evolutions;
            for (int i = 0; i < _rows.Count && i < evos.Count; i++)
            {
                if (evos[i] == null) continue;
                bool afford = vit >= _sim.EvolutionCost(State.Target, i);
                bool unlocked = _sim.BranchUnlocked(State.Target, i);
                _rows[i].cost.color = afford ? UIPalette.Vitality : UIPalette.Danger;
                _rows[i].group.alpha = !unlocked ? 0.45f : afford ? 1f : 0.7f;
            }
            if (_shake > 0f)
            {
                _shake -= Time.unscaledDeltaTime;
                float s = Mathf.Sin(Time.unscaledTime * 60f) * 10f * Mathf.Clamp01(_shake / 0.35f);
                Vector2 p = _card.anchoredPosition;
                _card.anchoredPosition = new Vector2(-16f + s, p.y);
            }
        }
    }
}
