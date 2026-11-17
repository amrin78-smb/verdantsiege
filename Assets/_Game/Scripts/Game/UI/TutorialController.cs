using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VerdantSiege.Battle;
using VerdantSiege.Grid;

namespace VerdantSiege.UI
{
    /// <summary>
    /// First-play guided tutorial (First Roots, until the player wins once). Each step points at what to do and
    /// advances on the actual game state, in any order the player chooses. The first wave is held until the
    /// steps are done, with a Skip button and a 45 s safety release (20 s of actual waiting) so it can never block play.
    /// </summary>
    public sealed class TutorialController : MonoBehaviour
    {
        private BattleController _battle;
        private BattleSimulation _sim;
        private RectTransform _panel, _pointer;
        private TextMeshProUGUI _text, _stepLabel;
        private int _step;
        private float _elapsed, _waited;
        private bool _done;
        private int _glowSlot, _shootSlot;
        private Button _chip;

        private void SetMinimised(bool min)
        {
            if (_done) return;
            _panel.gameObject.SetActive(!min);
            _chip.gameObject.SetActive(min);
        }
        private const int Steps = 4;
        private readonly Vector3[] _corners = new Vector3[4];

        public static TutorialController Create(BattleController battle, BattleHud hud)
        {
            var go = new GameObject("Tutorial");
            go.transform.SetParent(hud.transform, false);
            var t = go.AddComponent<TutorialController>();
            t.Build(battle, hud);
            return t;
        }

        private void Build(BattleController battle, BattleHud hud)
        {
            _battle = battle;
            _sim = battle.Sim;
            _sim.HoldFirstWave = true;
            _glowSlot = Slot("glowcap");
            _shootSlot = Slot("sunspore");

            Image p = UIKit.Panel("TutorialPanel", hud.Root, new Color(0.1f, 0.2f, 0.15f, 0.95f), true, true);
            _panel = p.rectTransform;
            UIKit.Place(_panel, new Vector2(0.5f, 0f), new Vector2(BattleHud.RailWidth * 0.5f, 16f), new Vector2(1080f, 130f));
            p.raycastTarget = false;
            Image sprout = UIKit.Image("Icon", p.transform, UIKit.Lib != null ? UIKit.Lib.iconLeaf : null, UIPalette.Leaf);
            UIKit.Place(sprout.rectTransform, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(60f, 60f));
            _text = UIKit.Text("Text", p.transform, "", 38f, UIPalette.Parchment, TextAlignmentOptions.MidlineLeft, true);
            UIKit.Stretch(_text.rectTransform);
            _text.enableAutoSizing = true;
            _text.fontSizeMin = MenuKit.TextMin;
            _text.fontSizeMax = 38f;
            _text.rectTransform.offsetMin = new Vector2(90f, 6f);
            _text.rectTransform.offsetMax = new Vector2(-280f, -6f);
            _stepLabel = UIKit.Text("Step", p.transform, "", MenuKit.TextMin, UIPalette.TextDim, TextAlignmentOptions.TopRight);
            UIKit.Place(_stepLabel.rectTransform, new Vector2(1f, 1f), new Vector2(-106f, -4f), new Vector2(160f, 44f));
            _stepLabel.fontSize = MenuKit.TextMin;
            Button skip = UIKit.Button("Skip", p.transform, "Skip", UIPalette.ButtonSecondary, Finish, null, 38f);
            UIKit.Place((RectTransform)skip.transform, new Vector2(1f, 0f), new Vector2(-100f, 8f), new Vector2(160f, 68f));
            Button min = UIKit.Button("Minimise", p.transform, "-", UIPalette.ButtonSecondary, () => SetMinimised(true), null, 44f);
            UIKit.Place((RectTransform)min.transform, new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(84f, 84f));
            // Collapsed chip: tap to bring the tip back. The pointer keeps guiding while minimised.
            _chip = UIKit.Button("TipChip", hud.Root, "Tip", new Color(0.1f, 0.2f, 0.15f, 0.95f), () => SetMinimised(false), UIKit.Lib != null ? UIKit.Lib.iconLeaf : null, 38f);
            UIKit.Place((RectTransform)_chip.transform, new Vector2(0.5f, 0f), new Vector2(BattleHud.RailWidth * 0.5f, 16f), new Vector2(230f, 84f));
            _chip.gameObject.SetActive(false);

            Image arrow = UIKit.Image("Pointer", hud.Root, UIKit.Lib != null ? UIKit.Lib.iconPlay : null, UIPalette.Gold);
            _pointer = arrow.rectTransform;
            _pointer.sizeDelta = new Vector2(84f, 84f);
            var sh = arrow.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
            sh.effectDistance = new Vector2(3f, -4f);
            ShowStep();
        }

        private int Slot(string id)
        {
            for (int i = 0; i < _sim.Deck.Count; i++) if (_sim.Deck[i].Definition.id == id) return i;
            return -1;
        }

        private int Count(string id)
        {
            int n = 0;
            foreach (DefenderRuntime d in _sim.Defenders) if (d.alive && d.Definition.id == id) n++;
            return n;
        }

        private void ShowStep()
        {
            _stepLabel.text = $"{Mathf.Min(_step + 1, Steps)} / {Steps}";
            switch (_step)
            {
                case 0: _text.text = "Tap the <color=#7CD65A>Glowcap</color> card. Glowcaps grow Vitality."; break;
                case 1: _text.text = "Now tap a lit square on the lawn to plant it."; break;
                case 2: _text.text = "Tap the glowing <color=#FFE06B>Sunmotes</color> to gather Vitality. Defeated Blight drop them too!"; break;
                case 3: _text.text = "Plant a <color=#FF9E33>Sunspore</color>. It fires down its lane at the Blight."; break;
            }
        }

        private void Update()
        {
            if (_done || _sim == null) return;
            _elapsed += Time.unscaledDeltaTime;
            bool advanced = false;
            if (_step == 0 && (_battle.SelectedSlot == _glowSlot || Count("glowcap") > 0)) { _step = 1; advanced = true; }
            if (_step == 1 && Count("glowcap") > 0) { _step = 2; advanced = true; }
            if (_step == 2 && (_sim.Stats.vitalityCollected > 0 || _sim.Options.autoCollect)) { _step = 3; advanced = true; }
            if (_step == 3 && Count("sunspore") > 0) { Finish(); return; }
            if (advanced) ShowStep();
            // Safety release: never hold the wave long. Counts only while the wave is actually waiting.
            if (_sim.FirstWaveWaiting) _waited += Time.unscaledDeltaTime;
            if (_elapsed > 45f || _waited > 20f) { Finish(); return; }
            PlacePointer();
        }

        private void PlacePointer()
        {
            Vector2 target;
            float rot = 0f;
            float bob = Mathf.Sin(Time.unscaledTime * 6f) * 14f;
            switch (_step)
            {
                case 0:
                case 3:
                {
                    int slot = _step == 0 ? _glowSlot : _shootSlot;
                    if (_battle.SelectedSlot == slot) goto case 1;
                    Transform card = _battle.Hud.CardTransform(slot);
                    if (card == null) { _pointer.gameObject.SetActive(false); return; }
                    Vector3[] c = _corners;
                    ((RectTransform)card).GetWorldCorners(c);
                    target = new Vector2(c[2].x + 50f + bob, (c[0].y + c[1].y) * 0.5f);
                    rot = 180f; // points left at the card
                    break;
                }
                case 1:
                {
                    target = _battle.CoordToScreen(new GridCoord(2, _step == 1 ? 0 : 1)) + new Vector2(0f, 90f + bob);
                    rot = -90f; // points down at the cell
                    break;
                }
                default:
                {
                    PickupRuntime best = null;
                    foreach (PickupRuntime pk in _sim.Pickups) if (pk.alive && (best == null || pk.age > best.age)) best = pk;
                    if (best == null || !_battle.View.PickupViews.TryGetValue(best.Id, out var v)) { _pointer.gameObject.SetActive(false); return; }
                    target = (Vector2)_battle.battleCamera.WorldToScreenPoint(v.transform.position) + new Vector2(0f, 80f + bob);
                    rot = -90f;
                    break;
                }
            }
            if (!_pointer.gameObject.activeSelf) _pointer.gameObject.SetActive(true);
            _pointer.position = target;
            _pointer.localRotation = Quaternion.Euler(0f, 0f, rot);
        }

        private void Finish()
        {
            if (_done) return;
            _done = true;
            if (_sim != null) _sim.HoldFirstWave = false;
            _panel.gameObject.SetActive(false);
            _pointer.gameObject.SetActive(false);
            if (_chip != null) _chip.gameObject.SetActive(false);
            bool completed = Count("sunspore") > 0;
            _battle.Hud.ShowBanner(completed ? "Well done!" : "Here they come!", "The Blight approaches from the right", UIPalette.Leaf, 3f);
        }
    }
}
