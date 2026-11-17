using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VerdantSiege.Battle;
using VerdantSiege.Data;

namespace VerdantSiege.UI
{
    /// <summary>
    /// Battle HUD, built in code. Left rail: Vitality + defender cards (thumb-reachable in landscape, keeps
    /// lanes clear). Top: Heartroot health, wave progress with major-wave flags, speed and pause. Overlays:
    /// toast, wave banner, defender inspect card, pause menu, results, developer debug panel.
    /// The HUD only displays state and raises intents; the BattleController decides.
    /// </summary>
    public sealed class BattleHud : MonoBehaviour
    {
        /// <summary>Width of the left card rail for the current battle: one column of wide cards for up to
        /// <see cref="SingleColumnMax"/> cards, two columns of tall cards above that (TD-36). Set by <see cref="Build"/>
        /// before anything else reads it (camera fit, centred strips).</summary>
        public static float RailWidth { get; private set; } = SingleRailWidth;
        public const float SingleRailWidth = 300f;
        public const int SingleColumnMax = 5;
        public const float TallCardWidth = 204f, RailColumnGap = 10f;

        /// <summary>The rail width a deck of <paramref name="cards"/> cards needs.</summary>
        public static float RailWidthFor(int cards) => cards > SingleColumnMax ? 16f + 2f * TallCardWidth + RailColumnGap + 12f : SingleRailWidth;
        public const float TopBarHeight = 128f;

        // intents
        public event Action<int> CardClicked;
        public event Action<int, Vector2> CardDragBegan, CardDragged, CardDragEnded;
        public event Action PauseClicked, ResumeClicked, RestartClicked, MenuClicked, SpeedClicked, UndoClicked, InspectClosed;
        public event Action<bool> AutoCollectChanged;
        public event Action<float, float> VolumesChanged;
        public event Action<string> DebugCommand;

        public BattleTapSurface TapSurface { get; private set; }
        public RectTransform Root { get; private set; }
        public Canvas Canvas { get; private set; }

        private readonly List<DefenderCardView> _cards = new List<DefenderCardView>();
        private TextMeshProUGUI _vitalityText, _waveText, _speedText, _toastText;
        private RectTransform _vitalityBadge, _waveFill, _waveTrack, _toast;
        private NotificationFeed _feed;
        private CanvasGroup _toastGroup;
        private readonly List<Image> _heartPips = new List<Image>();
        private float _displayVitality, _targetVitality, _vitalityPunch;
        private float _toastTimer;
        private BattleSimulation _sim;

        // inspect
        private RectTransform _inspect;
        private TextMeshProUGUI _inspectName, _inspectRole, _inspectDesc, _inspectHp;
        private Image _inspectHpFill, _inspectPortrait;
        private Button _undoButton;
        private TextMeshProUGUI _undoLabel;
        private DefenderRuntime _inspected;

        // overlays
        private GameObject _pauseOverlay, _resultOverlay, _debugPanel;
        private Toggle _autoToggle;
        private Slider _musicSlider, _sfxSlider;

        // vitality fly-ins
        private readonly List<RectTransform> _flyPool = new List<RectTransform>();
        private readonly List<(RectTransform rt, Vector2 from, float t)> _flying = new List<(RectTransform, Vector2, float)>();

        public void Build(BattleSimulation sim, bool autoCollect, float music, float sfx, bool debugTools)
        {
            _sim = sim;
            RailWidth = RailWidthFor(sim.Deck.Count);
            Canvas = UIKit.CreateCanvas("BattleHUD", 10);
            Canvas.transform.SetParent(transform, false);
            TapSurface = BattleTapSurface.Create(Canvas.transform);
            Root = UIKit.SafeArea(Canvas.transform);

            BuildRail(sim);
            BuildTopBar(sim);
            BuildToastAndBanner();
            BuildInspect();
            BuildPause(autoCollect, music, sfx);
            if (debugTools) BuildDebug(sim);
            _displayVitality = _targetVitality = sim.Vitality.Amount;
            _vitalityText.text = sim.Vitality.Amount.ToString();
            SetHeartroot(sim.HeartrootHealth, sim.HeartrootMaxHealth);
        }

        // ---------------------------------------------------------------- rail
        private void BuildRail(BattleSimulation sim)
        {
            RectTransform rail = UIKit.Rect("Rail", Root);
            rail.anchorMin = new Vector2(0f, 0f);
            rail.anchorMax = new Vector2(0f, 1f);
            rail.pivot = new Vector2(0f, 0.5f);
            rail.sizeDelta = new Vector2(RailWidth, 0f);
            rail.anchoredPosition = Vector2.zero;

            Image badge = UIKit.Panel("Vitality", rail, UIPalette.Panel, true, true);
            _vitalityBadge = badge.rectTransform;
            UIKit.Place(_vitalityBadge, new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(RailWidth - 28f, 104f));
            Image vIcon = UIKit.Image("Icon", badge.transform, UIKit.Lib != null ? UIKit.Lib.iconVitality : null, Color.white);
            UIKit.Place(vIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(92f, 92f));
            _vitalityText = UIKit.Text("Amount", badge.transform, "0", 64f, UIPalette.Vitality, TextAlignmentOptions.MidlineRight, true);
            UIKit.Stretch(_vitalityText.rectTransform);
            _vitalityText.rectTransform.offsetMax = new Vector2(-22f, 0f);
            _vitalityText.rectTransform.offsetMin = new Vector2(104f, 0f);
            _vitalityText.enableAutoSizing = true;
            _vitalityText.fontSizeMax = 64f;
            _vitalityText.fontSizeMin = 36f;
            _vitalityText.textWrappingMode = TextWrappingModes.NoWrap;

            // Up to 5 cards: one column of wide cards. 6-8 (Seed Satchel): two columns of tall cards, so every card keeps
            // a big portrait, a >=28 name and a bold cost (TD-36) instead of shrinking to fit one column.
            int n = Mathf.Max(1, sim.Deck.Count);
            bool two = n > SingleColumnMax;
            const float avail = 1080f - 136f - 16f;
            float gap = two ? RailColumnGap : 14f;
            int rows = two ? (n + 1) / 2 : n;
            float cardW = two ? TallCardWidth : RailWidth - 28f;
            float cardH = two ? Mathf.Min(232f, Mathf.Floor((avail - (rows - 1) * gap) / rows)) : (n <= 4 ? 172f : 160f);
            for (int i = 0; i < sim.Deck.Count; i++)
            {
                DefenderCardView card = DefenderCardView.Create(rail, i, sim.Deck[i], new Vector2(cardW, cardH));
                int col = two ? i % 2 : 0, row = two ? i / 2 : i;
                UIKit.Place((RectTransform)card.transform, new Vector2(0f, 1f), new Vector2(16f + col * (cardW + gap), -136f - row * (cardH + gap)), new Vector2(cardW, cardH));
                card.Clicked += s => CardClicked?.Invoke(s);
                card.DragBegan += (s, p) => CardDragBegan?.Invoke(s, p);
                card.Dragged += (s, p) => CardDragged?.Invoke(s, p);
                card.DragEnded += (s, p) => CardDragEnded?.Invoke(s, p);
                _cards.Add(card);
            }
        }

        // ---------------------------------------------------------------- top bar
        private void BuildTopBar(BattleSimulation sim)
        {
            // Heartroot health pips.
            Image heartPanel = UIKit.Panel("Heartroot", Root, UIPalette.Panel, true, true);
            int max = sim.HeartrootMaxHealth;
            float w = 96f + max * 46f;
            UIKit.Place(heartPanel.rectTransform, new Vector2(0f, 1f), new Vector2(RailWidth + 8f, -16f), new Vector2(w, 88f));
            Image heartIcon = UIKit.Image("Icon", heartPanel.transform, UIKit.Lib != null ? UIKit.Lib.iconHeart : null, UIPalette.Danger);
            UIKit.Place(heartIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(64f, 64f));
            for (int i = 0; i < max; i++)
            {
                Image pip = UIKit.Image("Pip" + i, heartPanel.transform, UIKit.Lib != null ? UIKit.Lib.iconLeaf : null, UIPalette.Leaf);
                UIKit.Place(pip.rectTransform, new Vector2(0f, 0.5f), new Vector2(86f + i * 46f, 0f), new Vector2(40f, 40f));
                _heartPips.Add(pip);
            }

            // Wave progress.
            Image wavePanel = UIKit.Panel("Waves", Root, UIPalette.Panel, true, true);
            // Left-anchored right after the Heartroot panel so the two never overlap (the wide rail on 4:3 tablets did).
            UIKit.Place(wavePanel.rectTransform, new Vector2(0f, 1f), new Vector2(RailWidth + 8f + w + 16f, -14f), new Vector2(620f, 96f));
            _waveText = UIKit.Text("Label", wavePanel.transform, "Prepare your garden", 34f, UIPalette.Parchment, TextAlignmentOptions.Top, true);
            UIKit.Stretch(_waveText.rectTransform, 4f);
            _waveText.rectTransform.offsetMin = new Vector2(6f, 40f);
            _waveText.textWrappingMode = TextWrappingModes.NoWrap;
            MenuKit.Fit(_waveText, 34f, 30f);
            Image track = UIKit.Image("Track", wavePanel.transform, UIKit.Lib != null ? UIKit.Lib.uiRound : null, new Color(0f, 0f, 0f, 0.5f));
            _waveTrack = track.rectTransform;
            _waveTrack.anchorMin = new Vector2(0f, 0f);
            _waveTrack.anchorMax = new Vector2(1f, 0f);
            _waveTrack.pivot = new Vector2(0.5f, 0f);
            _waveTrack.offsetMin = new Vector2(24f, 14f);
            _waveTrack.offsetMax = new Vector2(-24f, 36f);
            Image fill = UIKit.Image("Fill", track.transform, UIKit.Lib != null ? UIKit.Lib.uiRound : null, UIPalette.Blight);
            _waveFill = fill.rectTransform;
            _waveFill.anchorMin = Vector2.zero;
            _waveFill.anchorMax = new Vector2(0f, 1f);
            _waveFill.pivot = new Vector2(0f, 0.5f);
            _waveFill.offsetMin = _waveFill.offsetMax = Vector2.zero;
            int total = sim.Encounter.waves.Count;
            for (int i = 0; i < total; i++)
            {
                WaveDefinition wd = sim.Encounter.waves[i];
                if (wd == null || !wd.major) continue;
                Image flag = UIKit.Image("Flag" + i, track.transform, UIKit.Lib != null ? UIKit.Lib.iconFlag : null, UIPalette.Gold);
                float at = (i + 0.02f) / total;
                flag.rectTransform.anchorMin = flag.rectTransform.anchorMax = new Vector2(at, 0.5f);
                flag.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                flag.rectTransform.sizeDelta = new Vector2(34f, 34f);
                flag.rectTransform.anchoredPosition = Vector2.zero;
            }

            // Pause and speed (top right).
            Button pause = UIKit.Button("Pause", Root, null, UIPalette.ButtonSecondary, () => PauseClicked?.Invoke(), UIKit.Lib != null ? UIKit.Lib.iconPause : null);
            UIKit.Place((RectTransform)pause.transform, new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(110f, 110f));
            Button speed = UIKit.Button("Speed", Root, null, UIPalette.ButtonSecondary, () => SpeedClicked?.Invoke(), UIKit.Lib != null ? UIKit.Lib.iconSpeed : null);
            UIKit.Place((RectTransform)speed.transform, new Vector2(1f, 1f), new Vector2(-140f, -16f), new Vector2(110f, 110f));
            _speedText = UIKit.Text("Speed", speed.transform, "1x", 34f, UIPalette.Gold, TextAlignmentOptions.Bottom, true);
            UIKit.Stretch(_speedText.rectTransform, 4f);
        }

        // ---------------------------------------------------------------- toast & banner
        private void BuildToastAndBanner()
        {
            Image toast = UIKit.Panel("Toast", Root, new Color(0.08f, 0.12f, 0.1f, 0.92f), true, true);
            _toast = toast.rectTransform;
            UIKit.Place(_toast, new Vector2(0.5f, 0f), new Vector2(RailWidth * 0.5f, 22f), new Vector2(760f, 70f));
            toast.raycastTarget = false;
            _toastText = UIKit.Text("Text", toast.transform, "", 32f, UIPalette.Parchment, TextAlignmentOptions.Center, true);
            UIKit.Stretch(_toastText.rectTransform, 10f);
            _toastGroup = UIKit.Group(toast.gameObject);
            _toastGroup.alpha = 0f;
            _toastGroup.blocksRaycasts = false;

            _feed = NotificationFeed.Create(Root);
        }

        public void ShowToast(string message, float seconds = 1.6f)
        {
            _toastText.text = message;
            _toastTimer = seconds;
        }

        /// <summary>Headline (wave, phase, boss): compact strip under the top bars.</summary>
        public void ShowBanner(string title, string subtitle, Color titleColor, float seconds = 2.4f) =>
            _feed.Push(NotifyKind.Headline, title, subtitle, titleColor, seconds);

        /// <summary>Two-line intel (new foe + counter hint) in the same compact strip.</summary>
        public void ShowIntel(string title, string body, float seconds = 5f) =>
            _feed.Push(NotifyKind.Intel, title, body, UIPalette.Gold, seconds);

        /// <summary>Urgent one-liner (boss telegraphs) in the strip, ahead of intel.</summary>
        public void ShowAlert(string message, Color color, float seconds = 2.5f) =>
            _feed.Push(NotifyKind.Alert, message, null, color, seconds);

        /// <summary>Moves the strip below an extra top bar (the boss bar).</summary>
        public void SetFeedTop(float y) => _feed.SetTop(y);

        /// <summary>The notification strip is showing an intel card (autoplay screenshot).</summary>
        public bool FeedShowingIntel => _feed != null && _feed.ShowingIntel;

        // ---------------------------------------------------------------- inspect
        private void BuildInspect()
        {
            Image p = UIKit.Panel("Inspect", Root, UIPalette.Panel, true, true);
            _inspect = p.rectTransform;
            UIKit.Place(_inspect, new Vector2(1f, 0f), new Vector2(-16f, 16f), new Vector2(600f, 330f));
            _inspectPortrait = UIKit.Image("Portrait", p.transform, null, Color.white);
            _inspectPortrait.preserveAspect = true;
            UIKit.Place(_inspectPortrait.rectTransform, new Vector2(0f, 1f), new Vector2(10f, -10f), new Vector2(150f, 150f));
            _inspectName = UIKit.Text("Name", p.transform, "", 46f, UIPalette.Parchment, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(_inspectName.rectTransform, new Vector2(0f, 1f), new Vector2(170f, -14f), new Vector2(320f, 56f));
            _inspectRole = UIKit.Text("Role", p.transform, "", 28f, UIPalette.Gold, TextAlignmentOptions.TopLeft);
            UIKit.Place(_inspectRole.rectTransform, new Vector2(0f, 1f), new Vector2(172f, -70f), new Vector2(330f, 36f));
            _inspectRole.textWrappingMode = TextWrappingModes.NoWrap;
            MenuKit.Fit(_inspectRole, 28f);
            Image hpTrack = UIKit.Image("HpTrack", p.transform, UIKit.Lib != null ? UIKit.Lib.uiRound : null, new Color(0f, 0f, 0f, 0.5f));
            UIKit.Place(hpTrack.rectTransform, new Vector2(0f, 1f), new Vector2(172f, -112f), new Vector2(360f, 36f));
            _inspectHpFill = UIKit.Image("Fill", hpTrack.transform, UIKit.Lib != null ? UIKit.Lib.uiRound : null, UIPalette.Leaf);
            _inspectHpFill.type = Image.Type.Filled;
            _inspectHpFill.fillMethod = Image.FillMethod.Horizontal;
            UIKit.Stretch(_inspectHpFill.rectTransform, 3f);
            _inspectHp = UIKit.Text("Hp", hpTrack.transform, "", 28f, UIPalette.Parchment, TextAlignmentOptions.Center, true);
            UIKit.Stretch(_inspectHp.rectTransform);
            _inspectDesc = UIKit.Text("Desc", p.transform, "", 30f, UIPalette.TextDim, TextAlignmentOptions.TopLeft);
            MenuKit.Fit(_inspectDesc, 30f);
            _inspectDesc.rectTransform.anchorMin = new Vector2(0f, 0f);
            _inspectDesc.rectTransform.anchorMax = new Vector2(1f, 1f);
            _inspectDesc.rectTransform.offsetMin = new Vector2(20f, 16f);
            _inspectDesc.rectTransform.offsetMax = new Vector2(-20f, -166f);
            Button close = UIKit.Button("Close", p.transform, null, UIPalette.ButtonSecondary, () => InspectClosed?.Invoke(), UIKit.Lib != null ? UIKit.Lib.iconClose : null);
            UIKit.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(76f, 76f));
            _undoButton = UIKit.Button("Undo", p.transform, "Undo", UIPalette.ButtonPrimary, () => UndoClicked?.Invoke(), UIKit.Lib != null ? UIKit.Lib.iconUndo : null, 34f);
            UIKit.Place((RectTransform)_undoButton.transform, new Vector2(1f, 1f), new Vector2(-10f, -96f), new Vector2(200f, 80f));
            _undoLabel = _undoButton.GetComponentInChildren<TextMeshProUGUI>();
            _inspect.gameObject.SetActive(false);
        }

        public void ShowInspect(DefenderRuntime d)
        {
            _inspected = d;
            if (d == null) { _inspect.gameObject.SetActive(false); return; }
            DefenderDefinition def = d.Definition;
            _inspectName.text = def.displayName;
            _inspectRole.text = def.role.ToString().ToUpperInvariant() + (string.IsNullOrEmpty(def.strengths) ? "" : "  •  " + def.strengths);
            _inspectDesc.text = def.description;
            _inspectPortrait.sprite = def.icon;
            _inspectPortrait.enabled = def.icon != null;
            _inspect.gameObject.SetActive(true);
        }

        public DefenderRuntime Inspected => _inspected;
        public RectTransform InspectPanel => _inspect; // Living Evolution: EvolutionChooser docks its growth strip on this panel

        // ---------------------------------------------------------------- pause
        private void BuildPause(bool autoCollect, float music, float sfx)
        {
            _pauseOverlay = new GameObject("PauseOverlay", typeof(RectTransform));
            _pauseOverlay.transform.SetParent(Canvas.transform, false);
            Image dim = _pauseOverlay.AddComponent<Image>();
            dim.color = UIPalette.Dim;
            dim.raycastTarget = true;
            UIKit.Stretch((RectTransform)_pauseOverlay.transform);
            Image panel = UIKit.Panel("Panel", _pauseOverlay.transform, UIPalette.Panel, true, true);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 860f));
            TextMeshProUGUI title = UIKit.Text("Title", panel.transform, "Paused", 84f, UIPalette.Parchment, TextAlignmentOptions.Center, true);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(700f, 100f));
            float y = -150f;
            Button resume = UIKit.Button("Resume", panel.transform, "Resume", UIPalette.ButtonPrimary, () => ResumeClicked?.Invoke(), UIKit.Lib != null ? UIKit.Lib.iconPlay : null, 46f);
            UIKit.Place((RectTransform)resume.transform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(560f, 110f)); y -= 130f;
            Button restart = UIKit.Button("Restart", panel.transform, "Restart", UIPalette.ButtonSecondary, () => RestartClicked?.Invoke(), UIKit.Lib != null ? UIKit.Lib.iconRestart : null, 42f);
            UIKit.Place((RectTransform)restart.transform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(560f, 100f)); y -= 120f;
            Button menu = UIKit.Button("Menu", panel.transform, "Main Menu", UIPalette.ButtonSecondary, () => MenuClicked?.Invoke(), UIKit.Lib != null ? UIKit.Lib.iconHome : null, 42f);
            UIKit.Place((RectTransform)menu.transform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(560f, 100f)); y -= 140f;

            _autoToggle = UIKit.Toggle("AutoCollect", panel.transform, "Auto-collect Vitality", autoCollect, v => AutoCollectChanged?.Invoke(v));
            UIKit.Place((RectTransform)_autoToggle.transform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(560f, 70f)); y -= 100f;
            TextMeshProUGUI ml = UIKit.Text("MusicLabel", panel.transform, "Music", 34f, UIPalette.TextDim, TextAlignmentOptions.MidlineLeft);
            UIKit.Place(ml.rectTransform, new Vector2(0.5f, 1f), new Vector2(-180f, y), new Vector2(200f, 60f));
            _musicSlider = UIKit.Slider("Music", panel.transform, music, v => VolumesChanged?.Invoke(_musicSlider.value, _sfxSlider.value));
            UIKit.Place((RectTransform)_musicSlider.transform, new Vector2(0.5f, 1f), new Vector2(90f, y), new Vector2(360f, 60f)); y -= 80f;
            TextMeshProUGUI sl = UIKit.Text("SfxLabel", panel.transform, "Sound", 34f, UIPalette.TextDim, TextAlignmentOptions.MidlineLeft);
            UIKit.Place(sl.rectTransform, new Vector2(0.5f, 1f), new Vector2(-180f, y), new Vector2(200f, 60f));
            _sfxSlider = UIKit.Slider("Sfx", panel.transform, sfx, v => VolumesChanged?.Invoke(_musicSlider.value, _sfxSlider.value));
            UIKit.Place((RectTransform)_sfxSlider.transform, new Vector2(0.5f, 1f), new Vector2(90f, y), new Vector2(360f, 60f));
            _pauseOverlay.SetActive(false);
        }

        public void ShowPause(bool show) => _pauseOverlay.SetActive(show);
        public bool PauseVisible => _pauseOverlay.activeSelf;

        // ---------------------------------------------------------------- results
        public void ShowResult(bool won, BattleStats stats, int seeds, int heart, int heartMax, bool hasNext, Action next)
        {
            if (_resultOverlay != null) Destroy(_resultOverlay);
            _resultOverlay = new GameObject("ResultOverlay", typeof(RectTransform));
            _resultOverlay.transform.SetParent(Canvas.transform, false);
            Image dim = _resultOverlay.AddComponent<Image>();
            dim.color = UIPalette.Dim;
            dim.raycastTarget = true;
            UIKit.Stretch((RectTransform)_resultOverlay.transform);
            _resultOverlay.AddComponent<PopIn>();
            Image panel = UIKit.Panel("Panel", _resultOverlay.transform, UIPalette.Panel, true, true);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 820f));
            TextMeshProUGUI title = UIKit.Text("Title", panel.transform, won ? "Garden Defended!" : "The Heartroot Falls",
                94f, won ? UIPalette.Gold : UIPalette.Danger, TextAlignmentOptions.Center, true);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(860f, 120f));
            TextMeshProUGUI sub = UIKit.Text("Sub", panel.transform,
                won ? (heart >= heartMax ? "Flawless: not a single root was touched." : "The Blight retreats... for now.")
                    : "Every loss teaches. Try a new formation.", 34f, UIPalette.TextDim);
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(820f, 50f));

            string Row(string k, string v) => $"<color=#C9D8C4>{k}</color><pos=62%><color=#F6ECD2>{v}</color>";
            int m = Mathf.FloorToInt(stats.elapsed / 60f), s = Mathf.FloorToInt(stats.elapsed % 60f);
            string body = Row("Blight defeated", stats.enemiesDefeated.ToString()) + "\n" +
                          Row("Defenders planted", stats.defendersPlanted.ToString()) + "\n" +
                          Row("Defenders lost", stats.defendersLost.ToString()) + "\n" +
                          Row("Vitality gathered", stats.vitalityCollected.ToString()) + "\n" +
                          Row("Heartroot", $"{heart} / {heartMax}") + "\n" +
                          Row("Time", $"{m}:{s:00}");
            TextMeshProUGUI stat = UIKit.Text("Stats", panel.transform, body, 38f, UIPalette.Parchment, TextAlignmentOptions.TopLeft);
            UIKit.Place(stat.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(640f, 330f));
            stat.lineSpacing = 12f;

            if (won)
            {
                Image chip = UIKit.Panel("Seeds", panel.transform, UIPalette.PanelLight, true, false);
                UIKit.Place(chip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(380f, 90f));
                Image icon = UIKit.Image("Icon", chip.transform, UIKit.Lib != null ? UIKit.Lib.iconSeed : null, UIPalette.Gold);
                UIKit.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(70f, 70f));
                TextMeshProUGUI st = UIKit.Text("Amount", chip.transform, $"+{seeds} Seeds", 46f, UIPalette.Gold, TextAlignmentOptions.MidlineRight, true);
                UIKit.Stretch(st.rectTransform);
                st.rectTransform.offsetMax = new Vector2(-22f, 0f);
            }
            float bx = hasNext && won ? -280f : -150f;
            Button again = UIKit.Button("Again", panel.transform, won ? "Replay" : "Try Again", won ? UIPalette.ButtonSecondary : UIPalette.ButtonPrimary,
                () => RestartClicked?.Invoke(), UIKit.Lib != null ? UIKit.Lib.iconRestart : null, 40f);
            UIKit.Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(bx, 40f), new Vector2(270f, 100f));
            Button menu = UIKit.Button("Menu", panel.transform, "Menu", UIPalette.ButtonSecondary, () => MenuClicked?.Invoke(), UIKit.Lib != null ? UIKit.Lib.iconHome : null, 40f);
            UIKit.Place((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(bx + 290f, 40f), new Vector2(250f, 100f));
            if (hasNext && won)
            {
                Button nb = UIKit.Button("Next", panel.transform, "Next", UIPalette.ButtonPrimary, next, UIKit.Lib != null ? UIKit.Lib.iconPlay : null, 40f);
                UIKit.Place((RectTransform)nb.transform, new Vector2(0.5f, 0f), new Vector2(bx + 560f, 40f), new Vector2(250f, 100f));
            }
        }

        // ---------------------------------------------------------------- debug
        private void BuildDebug(BattleSimulation sim)
        {
            Button toggle = UIKit.Button("DebugToggle", Root, "DBG", new Color(0.3f, 0.1f, 0.35f, 0.85f), () => _debugPanel.SetActive(!_debugPanel.activeSelf), null, 26f);
            UIKit.Place((RectTransform)toggle.transform, new Vector2(1f, 0.5f), new Vector2(-16f, 60f), new Vector2(100f, 64f));
            Image panel = UIKit.Panel("DebugPanel", Root, new Color(0.12f, 0.05f, 0.14f, 0.92f), false, true);
            _debugPanel = panel.gameObject;
            UIKit.Place(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(-130f, 60f), new Vector2(360f, 860f)); // M3: taller for more commands
            string[] cmds = { "+100 Vitality", "-100 Vitality", "Next wave", "Reset cooldowns", "Spawn Drifter", "Spawn Skitter", "Spawn Brute",
                "Spawn Vaultling", "Spawn Glider", "Spawn Burrower", "Spawn Warden", "Boss now", "Defeat all", "Grid debug" }; // M3 entries
            for (int i = 0; i < cmds.Length; i++)
            {
                string c = cmds[i];
                Button b = UIKit.Button(c, panel.transform, c, new Color(0.35f, 0.18f, 0.4f, 1f), () => DebugCommand?.Invoke(c), null, 26f);
                UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, -12f - i * 60f), new Vector2(330f, 54f));
            }
            _debugPanel.SetActive(false);
        }

        // ---------------------------------------------------------------- state updates
        public Transform CardTransform(int slot) => slot >= 0 && slot < _cards.Count ? _cards[slot].transform : null;

        public void SetSelectedCard(int slot)
        {
            for (int i = 0; i < _cards.Count; i++) _cards[i].SetSelected(i == slot);
        }

        public void DenyCard(int slot)
        {
            if (slot >= 0 && slot < _cards.Count) _cards[slot].Deny();
        }

        public void SetVitality(int amount, int delta)
        {
            _targetVitality = amount;
            if (delta > 0) _vitalityPunch = 1f;
        }

        public void DenyVitality() => _vitalityPunch = -1f;

        public void SetHeartroot(int remaining, int max)
        {
            for (int i = 0; i < _heartPips.Count; i++)
                _heartPips[i].color = i < remaining ? UIPalette.Leaf : new Color(0.2f, 0.12f, 0.12f, 0.8f);
        }

        public void SetSpeed(float s) => _speedText.text = s > 1.5f ? "2x" : "1x";

        /// <summary>Flies a Vitality icon from a screen point into the counter.</summary>
        public void FlyVitality(Vector2 screenPos)
        {
            RectTransform rt = null;
            foreach (RectTransform r in _flyPool) if (!r.gameObject.activeSelf) { rt = r; break; }
            if (rt == null)
            {
                Image img = UIKit.Image("Fly", Canvas.transform, UIKit.Lib != null ? UIKit.Lib.iconVitality : null, Color.white);
                rt = img.rectTransform;
                rt.sizeDelta = new Vector2(72f, 72f);
                _flyPool.Add(rt);
            }
            rt.gameObject.SetActive(true);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            _flying.Add((rt, screenPos, 0f));
        }

        private void Update()
        {
            if (_sim == null) return;
            float dt = Time.unscaledDeltaTime;
            int vit = _sim.Vitality.Amount;
            foreach (DefenderCardView c in _cards) c.Refresh(vit, dt);

            // Vitality counter roll-up + punch.
            _displayVitality = Mathf.MoveTowards(_displayVitality, _targetVitality, Mathf.Max(60f, Mathf.Abs(_targetVitality - _displayVitality) * 8f) * dt);
            _vitalityText.text = Mathf.RoundToInt(_displayVitality).ToString();
            _vitalityPunch = Mathf.MoveTowards(_vitalityPunch, 0f, dt * 4f);
            float p = _vitalityPunch > 0f ? 1f + _vitalityPunch * 0.12f : 1f;
            float shake = _vitalityPunch < 0f ? Mathf.Sin(Time.unscaledTime * 60f) * 8f * -_vitalityPunch : 0f;
            _vitalityBadge.localScale = new Vector3(p, p, 1f);
            _vitalityText.rectTransform.anchoredPosition = new Vector2(shake, 0f);
            _vitalityText.color = _vitalityPunch < 0f ? Color.Lerp(UIPalette.Vitality, UIPalette.Danger, -_vitalityPunch) : UIPalette.Vitality;

            // Waves.
            WaveDirector w = _sim.Waves;
            _waveFill.anchorMax = new Vector2(Mathf.Clamp01(w.Progress), 1f);
            if (w.CurrentWave < 0 && _sim.FirstWaveWaiting) _waveText.text = "Wave waits for the tip";
            else if (w.CurrentWave < 0) _waveText.text = $"First wave in {Mathf.CeilToInt(Mathf.Max(0f, w.PrepRemaining))}";
            else _waveText.text = $"Wave {w.CurrentWave + 1} / {w.TotalWaves}";

            // Toast / banner fades.
            _toastTimer -= dt;
            _toastGroup.alpha = Mathf.Clamp01(_toastTimer / 0.3f);

            // Inspect live values.
            if (_inspected != null && _inspect.gameObject.activeSelf)
            {
                if (!_inspected.alive) { ShowInspect(null); InspectClosed?.Invoke(); }
                else
                {
                    float hf = _inspected.HealthFraction;
                    _inspectHpFill.fillAmount = hf;
                    _inspectHpFill.color = hf > 0.5f ? UIPalette.Leaf : hf > 0.25f ? UIPalette.Gold : UIPalette.Danger;
                    _inspectHp.text = $"{Mathf.CeilToInt(_inspected.health)} / {Mathf.CeilToInt(_inspected.MaxHealth)}";
                    bool canUndo = _sim.CanUndo(_inspected);
                    _undoButton.gameObject.SetActive(canUndo);
                    if (canUndo) _undoLabel.text = $"Undo {Mathf.Max(0f, _sim.Options.undoGraceSeconds - _inspected.age):0.0}";
                }
            }

            // Vitality fly-ins.
            Vector2 target = RectTransformUtility.WorldToScreenPoint(null, _vitalityBadge.position) + new Vector2(-60f, 0f);
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                (RectTransform rt, Vector2 from, float t) f = _flying[i];
                f.t += dt / 0.55f;
                float e = f.t * f.t * (3f - 2f * f.t);
                Vector2 mid = Vector2.Lerp(f.from, target, 0.5f) + new Vector2(0f, 120f);
                Vector2 a = Vector2.Lerp(f.from, mid, e), b = Vector2.Lerp(mid, target, e);
                f.rt.position = Vector2.Lerp(a, b, e);
                float sc = Mathf.Lerp(1.1f, 0.6f, e);
                f.rt.localScale = new Vector3(sc, sc, 1f);
                if (f.t >= 1f) { f.rt.gameObject.SetActive(false); _flying.RemoveAt(i); _vitalityPunch = 1f; }
                else _flying[i] = f;
            }
        }
    }

    /// <summary>Scale-and-fade entrance for overlays (unscaled time).</summary>
    public sealed class PopIn : MonoBehaviour
    {
        private float _t;
        private CanvasGroup _g;

        public static void Play(GameObject go)
        {
            PopIn p = go.GetComponent<PopIn>();
            if (p == null) p = go.AddComponent<PopIn>();
            p._t = 0f;
            p._g = UIKit.Group(go);
            p._g.alpha = 0f;
        }

        private void Awake()
        {
            _g = UIKit.Group(gameObject);
            _g.alpha = 0f;
        }

        private void Update()
        {
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / 0.35f);
            float e = 1f - Mathf.Pow(1f - _t, 3f);
            _g.alpha = e;
            float s = 0.9f + 0.1f * e;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
