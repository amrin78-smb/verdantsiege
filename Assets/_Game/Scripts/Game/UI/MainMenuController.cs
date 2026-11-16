using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VerdantSiege.App;
using VerdantSiege.Data;
using VerdantSiege.Save;

namespace VerdantSiege.UI
{
    /// <summary>
    /// Main menu over a living 3D diorama: title, Play (next uncompleted encounter), encounter list, settings
    /// (audio, auto-collect, reduced flashes, graphics quality) and a confirmed progress reset.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public Camera menuCamera;
        public Transform cameraPivot;

        private AppRoot _app;
        private RectTransform _root;
        private GameObject _settings, _confirm, _levels;
        private TextMeshProUGUI _seedsText, _qualityText;
        private VerdantSiege.Battle.View.DefenderView[] _guards;
        private Vector3 _camBasePos;
        private Quaternion _camBaseRot;

        private void Start()
        {
            _app = AppRoot.Ensure();
            UIKit.Lib = _app.Config != null ? _app.Config.visuals : null;
            Time.timeScale = 1f;
            _guards = FindObjectsByType<VerdantSiege.Battle.View.DefenderView>(FindObjectsSortMode.None);
            foreach (var h in FindObjectsByType<VerdantSiege.Battle.View.HeartrootView>(FindObjectsSortMode.None)) h.Init(null);
            if (menuCamera != null) { _camBasePos = menuCamera.transform.position; _camBaseRot = menuCamera.transform.rotation; }
            Build();
            if (_app.Audio != null && _app.Config.audio != null)
            {
                _app.Audio.SetPaused(false);
                _app.Audio.PlayMusic(_app.Config.audio.menuMusic);
                _app.Audio.PlayAmbience(_app.Config.audio.ambienceLoop);
            }
        }

        private void Update()
        {
            if (_guards != null) foreach (var g in _guards) if (g != null) g.Tick(Time.deltaTime, Time.time);
            if (menuCamera == null) return;
            float t = Time.time;
            menuCamera.transform.position = _camBasePos + new Vector3(Mathf.Sin(t * 0.13f) * 0.35f, Mathf.Sin(t * 0.21f) * 0.08f, 0f);
            menuCamera.transform.rotation = _camBaseRot * Quaternion.Euler(0f, Mathf.Sin(t * 0.13f) * 1.2f, 0f);
        }

        private ContentDatabase Content => _app.Config != null ? _app.Config.content : null;

        private EncounterDefinition NextEncounter()
        {
            if (Content == null || Content.encounters.Count == 0) return null;
            foreach (EncounterDefinition e in Content.encounters)
                if (e != null && !_app.Save.Data.IsCompleted(e.id)) return e;
            return Content.encounters[Content.encounters.Count - 1];
        }

        private void Build()
        {
            Canvas canvas = UIKit.CreateCanvas("MenuUI", 10);
            _root = UIKit.SafeArea(canvas.transform);

            // Title block (left), leaving the right side for the diorama's Heartroot.
            RectTransform title = UIKit.Rect("Title", _root);
            UIKit.Place(title, new Vector2(0f, 1f), new Vector2(90f, -90f), new Vector2(900f, 320f));
            TextMeshProUGUI t1 = UIKit.Text("Name", title, "Verdant Siege", 150f, UIPalette.Gold, TextAlignmentOptions.TopLeft, true);
            UIKit.Stretch(t1.rectTransform);
            t1.textWrappingMode = TextWrappingModes.NoWrap;
            t1.overflowMode = TextOverflowModes.Overflow;
            var outline = t1.gameObject.AddComponent<Shadow>();
            outline.effectColor = new Color(0.05f, 0.12f, 0.06f, 0.85f);
            outline.effectDistance = new Vector2(4f, -6f);
            TextMeshProUGUI t2 = UIKit.Text("Tag", title, "Cultivate. Defend. Restore the world.", 40f, UIPalette.Parchment, TextAlignmentOptions.TopLeft);
            UIKit.Stretch(t2.rectTransform);
            t2.rectTransform.offsetMax = new Vector2(0f, -175f);

            EncounterDefinition next = NextEncounter();
            bool anyDone = Content != null && Content.encounters.Exists(e => e != null && _app.Save.Data.IsCompleted(e.id));
            float y = 120f;
            Button play = UIKit.Button("Play", _root, anyDone ? "Continue" : "Play", UIPalette.ButtonPrimary,
                () => { if (next != null) AppRoot.LoadBattle(next.id); }, UIKit.Lib != null ? UIKit.Lib.iconPlay : null, 64f);
            UIKit.Place((RectTransform)play.transform, new Vector2(0f, 0f), new Vector2(90f, y + 330f), new Vector2(520f, 140f));
            if (next != null)
            {
                TextMeshProUGUI nl = UIKit.Text("NextLabel", play.transform, next.displayName, 28f, UIPalette.Parchment, TextAlignmentOptions.Bottom);
                UIKit.Place(nl.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -44f), new Vector2(520f, 40f));
            }
            Button levels = UIKit.Button("Levels", _root, "Encounters", UIPalette.ButtonSecondary, () => ShowLevels(true), UIKit.Lib != null ? UIKit.Lib.iconFlag : null, 44f);
            UIKit.Place((RectTransform)levels.transform, new Vector2(0f, 0f), new Vector2(90f, y + 170f), new Vector2(520f, 104f));
            Button settings = UIKit.Button("Settings", _root, "Settings", UIPalette.ButtonSecondary, () => ShowSettings(true), UIKit.Lib != null ? UIKit.Lib.iconGear : null, 44f);
            UIKit.Place((RectTransform)settings.transform, new Vector2(0f, 0f), new Vector2(90f, y + 40f), new Vector2(520f, 104f));

            Image seeds = UIKit.Panel("Seeds", _root, UIPalette.Panel, true, true);
            UIKit.Place(seeds.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(300f, 96f));
            Image si = UIKit.Image("Icon", seeds.transform, UIKit.Lib != null ? UIKit.Lib.iconSeed : null, UIPalette.Gold);
            UIKit.Place(si.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(70f, 70f));
            _seedsText = UIKit.Text("Amount", seeds.transform, _app.Save.Data.seeds.ToString(), 54f, UIPalette.Gold, TextAlignmentOptions.MidlineRight, true);
            UIKit.Stretch(_seedsText.rectTransform);
            _seedsText.rectTransform.offsetMax = new Vector2(-24f, 0f);

            TextMeshProUGUI ver = UIKit.Text("Version", _root, _app.Config != null ? _app.Config.versionLabel : "", 24f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.BottomRight);
            UIKit.Place(ver.rectTransform, new Vector2(1f, 0f), new Vector2(-30f, 20f), new Vector2(600f, 40f));

            BuildSettings(canvas.transform);
            BuildConfirm(canvas.transform);
            BuildLevels(canvas.transform);
        }

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
            y -= 120f;
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
            if (show) PopIn.Play(_settings);
            else _app.SaveNow();
        }

        private void BuildConfirm(Transform canvas)
        {
            _confirm = Overlay(canvas, "Confirm", new Vector2(860f, 480f), "Start Over?", out RectTransform p);
            TextMeshProUGUI msg = UIKit.Text("Msg", p, "All seeds and encounter progress will be erased. Settings are kept.", 36f, UIPalette.TextDim);
            UIKit.Place(msg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(760f, 140f));
            Button no = UIKit.Button("Cancel", p, "Keep Progress", UIPalette.ButtonPrimary, () => _confirm.SetActive(false), null, 34f);
            UIKit.Place((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(-200f, 36f), new Vector2(360f, 96f));
            Button yes = UIKit.Button("Erase", p, "Erase", new Color(0.55f, 0.16f, 0.16f, 1f), () =>
            {
                _app.Save.ResetProgress();
                _confirm.SetActive(false);
                AppRoot.LoadMainMenu();
            }, null, 34f);
            UIKit.Place((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(200f, 36f), new Vector2(360f, 96f));
        }

        private void BuildLevels(Transform canvas)
        {
            _levels = Overlay(canvas, "Levels", new Vector2(1100f, 860f), "Verdant Heartlands", out RectTransform p);
            float y = -150f;
            if (Content != null)
            {
                bool unlocked = true;
                foreach (EncounterDefinition e in Content.encounters)
                {
                    if (e == null) continue;
                    bool done = _app.Save.Data.IsCompleted(e.id);
                    EncounterDefinition captured = e;
                    string label = e.displayName + (done ? "  - Cleared" : unlocked ? "" : "  (locked)");
                    Button b = UIKit.Button(e.id, p, label, done ? UIPalette.ButtonSecondary : unlocked ? UIPalette.ButtonPrimary : new Color(0.2f, 0.22f, 0.2f, 1f),
                        unlocked ? () => AppRoot.LoadBattle(captured.id) : (System.Action)null, null, 38f);
                    b.interactable = unlocked;
                    UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(900f, 100f));
                    y -= 118f;
                    unlocked = done;
                }
            }
            Button close = UIKit.Button("Back", p, "Back", UIPalette.ButtonSecondary, () => ShowLevels(false), UIKit.Lib != null ? UIKit.Lib.iconHome : null, 40f);
            UIKit.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(340f, 96f));
        }

        private void ShowLevels(bool show)
        {
            _levels.SetActive(show);
            if (show) PopIn.Play(_levels);
        }
    }
}
