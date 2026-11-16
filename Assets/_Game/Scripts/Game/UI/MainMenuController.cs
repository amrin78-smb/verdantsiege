using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VerdantSiege.App;
using VerdantSiege.Data;
using VerdantSiege.Progression;
using VerdantSiege.Save;
using VerdantSiege.Visual;

namespace VerdantSiege.UI
{
    /// <summary>
    /// The Sanctuary (Milestone 4): the home hub over the living 3D diorama, which is restored (greener, brighter,
    /// more defenders) as missions are won. Entries: Continue (next mission), World Map, Seed Conservatory and
    /// Settings (audio, auto-collect, reduced flashes, graphics quality, confirmed progress reset). Screens are built
    /// on demand on top of the hub (<see cref="WorldMapScreen"/>, <see cref="LoadoutScreen"/>, <see cref="ConservatoryScreen"/>).
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public Camera menuCamera;
        public Transform cameraPivot;

        private AppRoot _app;
        private Canvas _canvas;
        private RectTransform _root;
        private GameObject _settings, _confirm, _hub;
        private TextMeshProUGUI _qualityText;
        private readonly List<VerdantSiege.Battle.View.DefenderView> _guards = new List<VerdantSiege.Battle.View.DefenderView>();
        private SanctuaryRestoration _restoration;
        private Vector3 _camBasePos;
        private Quaternion _camBaseRot;
        private MonoBehaviour _screen;

        /// <summary>The screen on top of the hub (null = the Sanctuary itself). Used by the autoplay harness.</summary>
        public MonoBehaviour CurrentScreen => _screen;
        public static MainMenuController Instance { get; private set; }

        private void Start()
        {
            Instance = this;
            _app = AppRoot.Ensure();
            UIKit.Lib = _app.Config != null ? _app.Config.visuals : null;
            Time.timeScale = 1f;
            SetUpDiorama();
            if (menuCamera != null) { _camBasePos = menuCamera.transform.position; _camBaseRot = menuCamera.transform.rotation; }
            _canvas = UIKit.CreateCanvas("MenuUI", 10);
            BuildHub();
            BuildSettings(_canvas.transform);
            BuildConfirm(_canvas.transform);
            if (_app.Audio != null && _app.Config.audio != null)
            {
                _app.Audio.SetPaused(false);
                _app.Audio.PlayMusic(_app.Config.audio.menuMusic);
                _app.Audio.PlayAmbience(_app.Config.audio.ambienceLoop);
            }
            OpenRoute();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private ContentDatabase Content => _app.Config != null ? _app.Config.content : null;
        private SaveData Save => _app.Save.Data;

        // ================================================================ diorama (restoration + guardians)
        private void SetUpDiorama()
        {
            foreach (var h in FindObjectsByType<VerdantSiege.Battle.View.HeartrootView>(FindObjectsSortMode.None)) h.Init(null);
            foreach (SanctuaryGuardian g in FindObjectsByType<SanctuaryGuardian>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                g.gameObject.SetActive(Save.IsUnlocked(g.defenderId));
            _guards.AddRange(FindObjectsByType<VerdantSiege.Battle.View.DefenderView>(FindObjectsSortMode.None));
            _restoration = FindAnyObjectByType<SanctuaryRestoration>();
            if (_restoration == null || _app.Progression.Campaign == null) return;
            int tier = CampaignRules.SanctuaryTier(Save, _app.Progression.Campaign);
            int seen = Mathf.Clamp(Save.sanctuaryTierSeen, 0, tier);
            if (tier > seen)
            {
                _restoration.Apply(seen);
                Invoke(nameof(CelebrateRestoration), 1.2f);
            }
            else _restoration.Apply(tier);
        }

        private void CelebrateRestoration()
        {
            int tier = CampaignRules.SanctuaryTier(Save, _app.Progression.Campaign);
            int seen = Mathf.Clamp(Save.sanctuaryTierSeen, 0, tier);
            _restoration.Celebrate(seen, tier);
            Save.sanctuaryTierSeen = tier;
            _app.SaveNow();
            if (_app.Audio != null) _app.Audio.Play(Audio.Sfx.Evolve);
            if (_screen == null) MenuKit.Toast(_root, tier >= CampaignRules.MaxSanctuaryTier ? "The Sanctuary is fully restored!" : "The Sanctuary blooms again!", UIPalette.Leaf);
            RefreshHub();
        }

        private void Update()
        {
            foreach (var g in _guards) if (g != null && g.isActiveAndEnabled) g.Tick(Time.deltaTime, Time.time);
            if (menuCamera == null) return;
            float t = Time.time;
            menuCamera.transform.position = _camBasePos + new Vector3(Mathf.Sin(t * 0.13f) * 0.35f, Mathf.Sin(t * 0.21f) * 0.08f, 0f);
            menuCamera.transform.rotation = _camBaseRot * Quaternion.Euler(0f, Mathf.Sin(t * 0.13f) * 1.2f, 0f);
        }

        // ================================================================ routing
        private void OpenRoute()
        {
            MenuRoute route = _app.PendingRoute;
            string id = _app.PendingRouteEncounterId;
            _app.PendingRoute = MenuRoute.Sanctuary;
            _app.PendingRouteEncounterId = null;
            EncounterDefinition e = Content != null && !string.IsNullOrEmpty(id) ? Content.FindEncounter(id) : null;
            switch (route)
            {
                case MenuRoute.WorldMap: ShowWorldMap(); break;
                case MenuRoute.Conservatory: ShowConservatory(ConservatoryScreen.Tab.Research); break;
                case MenuRoute.Loadout:
                    if (e != null && _app.Progression.IsMissionUnlocked(e)) ShowLoadout(e);
                    else ShowWorldMap();
                    break;
            }
        }

        private void SetScreen(MonoBehaviour s)
        {
            if (_screen != null) Destroy(_screen.gameObject);
            _screen = s;
            if (_hub != null) _hub.SetActive(s == null);
            if (s != null) s.transform.SetSiblingIndex(_hub != null ? _hub.transform.GetSiblingIndex() + 1 : 0);
        }

        public void ShowSanctuary()
        {
            SetScreen(null);
            RefreshHub();
        }

        public void ShowWorldMap() => SetScreen(WorldMapScreen.Open(this, _canvas.transform));
        public void ShowLoadout(EncounterDefinition e) => SetScreen(LoadoutScreen.Open(this, _canvas.transform, e));
        public void ShowConservatory(ConservatoryScreen.Tab tab) => SetScreen(ConservatoryScreen.Open(this, _canvas.transform, tab));

        /// <summary>Continue: the next open mission's loadout (straight into battle on the very first play).</summary>
        public void Continue()
        {
            EncounterDefinition next = _app.Progression.NextMission();
            if (next == null) { ShowWorldMap(); return; }
            bool firstEver = !Save.settings.tutorialSeen && Save.encounters.Count == 0;
            if (firstEver) AppRoot.LoadBattle(next.id);
            else ShowLoadout(next);
        }

        // ================================================================ hub
        private TextMeshProUGUI _continueSub, _restText;
        private Image[] _restPips;
        private GameObject _researchBadge;
        private CurrencyBar _currency;

        private void BuildHub()
        {
            _hub = new GameObject("Sanctuary", typeof(RectTransform));
            _hub.transform.SetParent(_canvas.transform, false);
            UIKit.Stretch((RectTransform)_hub.transform);
            _root = UIKit.SafeArea(_hub.transform);

            RectTransform title = UIKit.Rect("Title", _root);
            UIKit.Place(title, new Vector2(0f, 1f), new Vector2(80f, -64f), new Vector2(1000f, 280f));
            TextMeshProUGUI t1 = UIKit.Text("Name", title, "Verdant Siege", 132f, UIPalette.Gold, TextAlignmentOptions.TopLeft, true);
            UIKit.Stretch(t1.rectTransform);
            t1.textWrappingMode = TextWrappingModes.NoWrap;
            t1.overflowMode = TextOverflowModes.Overflow;
            var outline = t1.gameObject.AddComponent<Shadow>();
            outline.effectColor = new Color(0.05f, 0.12f, 0.06f, 0.85f);
            outline.effectDistance = new Vector2(4f, -6f);
            TextMeshProUGUI t2 = UIKit.Text("Tag", title, "The Sanctuary", 46f, UIPalette.Parchment, TextAlignmentOptions.TopLeft, true);
            UIKit.Stretch(t2.rectTransform);
            t2.rectTransform.offsetMax = new Vector2(0f, -150f);
            MenuKit.Shadowed(t2);

            const float x = 80f, w = 560f;
            Button cont = UIKit.Button("Continue", _root, "Continue", UIPalette.ButtonPrimary, Continue, UIKit.Lib != null ? UIKit.Lib.iconPlay : null, 60f);
            UIKit.Place((RectTransform)cont.transform, new Vector2(0f, 0f), new Vector2(x, 446f), new Vector2(w, 150f));
            ((RectTransform)cont.transform.Find("Label")).offsetMin = new Vector2(96f, 46f);
            _continueSub = UIKit.Text("Next", cont.transform, "", 28f, UIPalette.Parchment, TextAlignmentOptions.Bottom);
            UIKit.Stretch(_continueSub.rectTransform);
            _continueSub.rectTransform.offsetMin = new Vector2(96f, 14f);
            _continueSub.rectTransform.offsetMax = new Vector2(-12f, 0f);
            cont.gameObject.AddComponent<Pulse>().amount = 0.025f;

            Button map = UIKit.Button("WorldMap", _root, "World Map", UIPalette.ButtonSecondary, ShowWorldMap, UIKit.Lib != null ? UIKit.Lib.iconMapPin : null, 46f);
            UIKit.Place((RectTransform)map.transform, new Vector2(0f, 0f), new Vector2(x, 318f), new Vector2(w, 112f));
            Button cons = UIKit.Button("Conservatory", _root, "Conservatory", UIPalette.ButtonSecondary, () => ShowConservatory(ConservatoryScreen.Tab.Research),
                UIKit.Lib != null ? UIKit.Lib.iconSprout : null, 46f);
            UIKit.Place((RectTransform)cons.transform, new Vector2(0f, 0f), new Vector2(x, 190f), new Vector2(w, 112f));
            Image badge = UIKit.Image("Badge", cons.transform, MenuKit.Circle, UIPalette.Danger);
            UIKit.Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(10f, 10f), new Vector2(40f, 40f));
            TextMeshProUGUI bang = UIKit.Text("Bang", badge.transform, "!", 30f, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Stretch(bang.rectTransform);
            badge.gameObject.AddComponent<Pulse>().amount = 0.12f;
            _researchBadge = badge.gameObject;
            Button settings = UIKit.Button("Settings", _root, "Settings", UIPalette.ButtonSecondary, () => ShowSettings(true), UIKit.Lib != null ? UIKit.Lib.iconGear : null, 46f);
            UIKit.Place((RectTransform)settings.transform, new Vector2(0f, 0f), new Vector2(x, 62f), new Vector2(w, 112f));

            _currency = CurrencyBar.Create(_root);

            // Restoration card (bottom right).
            Image rest = UIKit.Panel("Restoration", _root, UIPalette.Panel, true, true);
            UIKit.Place(rest.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 62f), new Vector2(560f, 168f));
            TextMeshProUGUI rh = UIKit.Text("Head", rest.transform, "Sanctuary restored", 34f, UIPalette.Gold, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(rh.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -14f), new Vector2(340f, 44f));
            _restPips = new Image[CampaignRules.MaxSanctuaryTier];
            for (int i = 0; i < _restPips.Length; i++)
            {
                _restPips[i] = UIKit.Image("Pip" + i, rest.transform, UIKit.Lib != null ? UIKit.Lib.iconLeaf : null, Color.white);
                _restPips[i].preserveAspect = true;
                UIKit.Place(_restPips[i].rectTransform, new Vector2(1f, 1f), new Vector2(-22f - (3 - i) * 46f, -14f), new Vector2(42f, 42f));
            }
            _restText = UIKit.Text("Text", rest.transform, "", 25f, UIPalette.TextDim, TextAlignmentOptions.TopLeft);
            UIKit.Place(_restText.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -66f), new Vector2(520f, 90f));

            TextMeshProUGUI ver = UIKit.Text("Version", _root, _app.Config != null ? _app.Config.versionLabel : "", 22f, new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.BottomRight);
            UIKit.Place(ver.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 18f), new Vector2(600f, 34f));
            RefreshHub();
        }

        private void RefreshHub()
        {
            if (_continueSub == null) return;
            EncounterDefinition next = _app.Progression.NextMission();
            _continueSub.text = next != null ? next.displayName : "Campaign complete: replay for stars";
            _researchBadge.SetActive(_app.Progression.AnyResearchAffordable());
            _currency.Refresh();
            int tier = _restoration != null ? _restoration.Tier : (_app.Progression.Campaign != null ? CampaignRules.SanctuaryTier(Save, _app.Progression.Campaign) : 0);
            for (int i = 0; i < _restPips.Length; i++) _restPips[i].color = i < tier ? UIPalette.Leaf : new Color(1f, 1f, 1f, 0.16f);
            string[] lines =
            {
                "The Blight has crept into the grove. Win missions to restore it.",
                "Flowers return to the Heartroot. Keep going!",
                "Hedges and blossoms are back. The rot is gone.",
                "The old grove stirs: arches and glowing caps return.",
                "The Verdant Heartlands are restored. Thank you, gardener.",
            };
            _restText.text = lines[Mathf.Clamp(tier, 0, lines.Length - 1)];
        }

        // ================================================================ overlays (settings, confirm)
        private GameObject Overlay(Transform canvas, string name, Vector2 size, string title, out RectTransform panel)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            UIKit.Stretch((RectTransform)go.transform);
            Image dim = go.AddComponent<Image>();
            dim.color = UIPalette.Dim;
            dim.raycastTarget = true;
            Image p = UIKit.Panel("Panel", go.transform, UIPalette.Panel, true, true);
            panel = p.rectTransform;
            UIKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            TextMeshProUGUI t = UIKit.Text("Title", panel, title, 72f, UIPalette.Parchment, TextAlignmentOptions.Center, true);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(size.x - 40f, 90f));
            go.SetActive(false);
            return go;
        }

        private void BuildSettings(Transform canvas)
        {
            SettingsData s = _app.Settings;
            _settings = Overlay(canvas, "Settings", new Vector2(900f, 1020f), "Settings", out RectTransform p);
            float y = -140f;
            void SliderRow(string label, float value, System.Action<float> set)
            {
                TextMeshProUGUI l = UIKit.Text(label, p, label, 36f, UIPalette.TextDim, TextAlignmentOptions.MidlineLeft);
                UIKit.Place(l.rectTransform, new Vector2(0.5f, 1f), new Vector2(-230f, y), new Vector2(300f, 60f));
                Slider sl = UIKit.Slider(label + "Slider", p, value, v => { set(v); _app.ApplySettings(); });
                UIKit.Place((RectTransform)sl.transform, new Vector2(0.5f, 1f), new Vector2(150f, y), new Vector2(440f, 60f));
                y -= 84f;
            }
            SliderRow("Music", s.musicVolume, v => s.musicVolume = v);
            SliderRow("Sound", s.sfxVolume, v => s.sfxVolume = v);
            SliderRow("Ambience", s.ambienceVolume, v => s.ambienceVolume = v);
            void ToggleRow(string label, bool value, System.Action<bool> set)
            {
                Toggle tg = UIKit.Toggle(label, p, label, value, v => set(v));
                UIKit.Place((RectTransform)tg.transform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(740f, 70f));
                y -= 84f;
            }
            ToggleRow("Auto-collect Vitality", s.autoCollect, v => s.autoCollect = v);
            ToggleRow("Reduce flashes and shake", s.reducedFlashes, v => { s.reducedFlashes = v; s.reducedShake = v; });
            ToggleRow("Show damage numbers", s.showDamageNumbers, v => s.showDamageNumbers = v);

            Button q = UIKit.Button("Quality", p, "", UIPalette.ButtonSecondary, CycleQuality, null, 36f);
            UIKit.Place((RectTransform)q.transform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(740f, 90f));
            _qualityText = q.GetComponentInChildren<TextMeshProUGUI>();
            if (_qualityText == null)
            {
                _qualityText = UIKit.Text("Label", q.transform, "", 36f, UIPalette.Parchment, TextAlignmentOptions.Center, true);
                UIKit.Stretch(_qualityText.rectTransform);
            }
            RefreshQuality();
            Button reset = UIKit.Button("Reset", p, "Reset Progress", new Color(0.45f, 0.16f, 0.16f, 1f), () => _confirm.SetActive(true), null, 34f);
            UIKit.Place((RectTransform)reset.transform, new Vector2(0.5f, 0f), new Vector2(-200f, 36f), new Vector2(340f, 96f));
            Button done = UIKit.Button("Done", p, "Done", UIPalette.ButtonPrimary, () => ShowSettings(false), null, 40f);
            UIKit.Place((RectTransform)done.transform, new Vector2(0.5f, 0f), new Vector2(200f, 36f), new Vector2(340f, 96f));
        }

        private void CycleQuality()
        {
            SettingsData s = _app.Settings;
            s.qualityLevel = s.qualityLevel >= 2 ? -1 : s.qualityLevel + 1;
            QualityManager.Apply(s.qualityLevel);
            RefreshQuality();
        }

        private void RefreshQuality()
        {
            int q = _app.Settings.qualityLevel;
            string name = q < 0 ? $"Auto ({QualityManager.Names[QualityManager.CurrentLevel]})" : QualityManager.Names[q];
            _qualityText.text = "Graphics: " + name;
        }

        private void ShowSettings(bool show)
        {
            _settings.SetActive(show);
            _settings.transform.SetAsLastSibling();
            if (show) PopIn.Play(_settings);
            else _app.SaveNow();
        }

        private void BuildConfirm(Transform canvas)
        {
            _confirm = Overlay(canvas, "Confirm", new Vector2(860f, 480f), "Start Over?", out RectTransform p);
            TextMeshProUGUI msg = UIKit.Text("Msg", p, "All Seeds, Essence, research, mastery and mission progress will be erased. Settings are kept.", 34f, UIPalette.TextDim);
            UIKit.Place(msg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(760f, 160f));
            Button no = UIKit.Button("Cancel", p, "Keep Progress", UIPalette.ButtonPrimary, () => _confirm.SetActive(false), null, 34f);
            UIKit.Place((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(-200f, 36f), new Vector2(360f, 96f));
            Button yes = UIKit.Button("Erase", p, "Erase", new Color(0.55f, 0.16f, 0.16f, 1f), () =>
            {
                _app.ResetProgress();
                _confirm.SetActive(false);
                AppRoot.LoadMainMenu();
            }, null, 34f);
            UIKit.Place((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(200f, 36f), new Vector2(360f, 96f));
        }
    }
}
