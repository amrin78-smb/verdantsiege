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
    /// World map (Milestone 4): the Verdant Heartlands as a stylised painted map with its missions as nodes along a
    /// winding path (the Root Maw as the finale), stars under each node, and the future regions as locked teasers.
    /// Tapping a node opens its briefing (threats, objectives, rewards, unlocks) and then the loadout.
    /// </summary>
    public sealed class WorldMapScreen : MonoBehaviour
    {
        public const float RegionColumnWidth = 430f;

        private MainMenuController _hub;
        private AppRoot _app;
        private RectTransform _safe;
        private GameObject _brief;

        /// <summary>The mission whose briefing is open (autoplay).</summary>
        public EncounterDefinition BriefMission { get; private set; }

        public static WorldMapScreen Open(MainMenuController hub, Transform canvas)
        {
            GameObject go = MenuKit.Frame(canvas, "WorldMap", "World Map", hub.ShowSanctuary, out RectTransform safe, out _);
            var s = go.AddComponent<WorldMapScreen>();
            s._hub = hub;
            s._app = AppRoot.Ensure();
            s._safe = safe;
            s.Build();
            PopIn.Play(go);
            return s;
        }

        private SaveData Save => _app.Save.Data;
        private CampaignDefinition Campaign => _app.Progression.Campaign;

        private void Build()
        {
            CampaignDefinition c = Campaign;
            if (c == null) return;
            RegionDefinition region = null;
            foreach (RegionDefinition r in c.regions) if (r != null && CampaignRules.RegionUnlocked(Save, c, r)) region = r; // furthest open region
            if (region == null && c.regions.Count > 0) region = c.regions[0];
            BuildMap(region);
            BuildRegions(c, region);
        }

        // ================================================================ map
        private void BuildMap(RegionDefinition region)
        {
            Image map = UIKit.Panel("Map", _safe, region.groundColor, true, true);
            RectTransform m = map.rectTransform;
            m.anchorMin = Vector2.zero;
            m.anchorMax = Vector2.one;
            m.offsetMin = new Vector2(28f, 28f);
            m.offsetMax = new Vector2(-(RegionColumnWidth + 48f), -(MenuKit.TopBarHeight + 12f));
            Decorate(m, region);

            // Region title + progress.
            TextMeshProUGUI name = UIKit.Text("RegionName", m, region.displayName, 52f, UIPalette.Parchment, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -20f), new Vector2(900f, 64f));
            MenuKit.Shadowed(name);
            TextMeshProUGUI tag = UIKit.Text("Tagline", m, region.tagline, 26f, new Color(0.95f, 0.95f, 0.85f, 0.85f), TextAlignmentOptions.TopLeft);
            UIKit.Place(tag.rectTransform, new Vector2(0f, 1f), new Vector2(32f, -84f), new Vector2(900f, 36f));
            int stars = CampaignRules.StarsIn(Save, region), max = region.missions.Count * 3;
            Image starIcon = UIKit.Image("StarIcon", m, UIKit.Lib != null ? UIKit.Lib.iconStar : null, UIPalette.Gold);
            starIcon.preserveAspect = true;
            UIKit.Place(starIcon.rectTransform, new Vector2(1f, 1f), new Vector2(-150f, -24f), new Vector2(52f, 52f));
            TextMeshProUGUI st = UIKit.Text("Stars", m, $"{stars}/{max}", 40f, UIPalette.Gold, TextAlignmentOptions.MidlineLeft, true);
            UIKit.Place(st.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(120f, 52f));

            // Path between consecutive missions (dots; gold where already walked).
            List<EncounterDefinition> missions = region.missions;
            for (int i = 0; i + 1 < missions.Count; i++)
            {
                Vector2 a = missions[i].mapPosition, b = missions[i + 1].mapPosition;
                bool walked = Save.IsCompleted(missions[i].id);
                const int dots = 13;
                for (int k = 1; k < dots; k++)
                {
                    float t = k / (float)dots;
                    Vector2 p = Vector2.Lerp(a, b, t) + new Vector2(0f, Mathf.Sin(t * Mathf.PI) * 0.05f * (i % 2 == 0 ? 1f : -1f));
                    Image dot = UIKit.Image("Path" + i + "_" + k, m, MenuKit.Circle,
                        walked ? new Color(1f, 0.85f, 0.4f, 0.95f) : new Color(0.98f, 0.95f, 0.85f, 0.4f));
                    dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = p;
                    dot.rectTransform.sizeDelta = new Vector2(walked ? 16f : 13f, walked ? 16f : 13f);
                }
            }
            for (int i = 0; i < missions.Count; i++) BuildNode(m, missions[i], i);
        }

        /// <summary>Painted-map dressing: meadow blobs, groves (leaf clusters), a stream and stones. Seeded, so stable.</summary>
        private void Decorate(RectTransform m, RegionDefinition region)
        {
            var rng = new System.Random(region.id.GetHashCode() & 0x7fffffff);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Color light = Color.Lerp(region.groundColor, Color.white, 0.18f), dark = Color.Lerp(region.groundColor, Color.black, 0.25f);
            void Blob(Vector2 at, float size, Color c)
            {
                Image b = UIKit.Image("Blob", m, MenuKit.Circle, c);
                b.rectTransform.anchorMin = b.rectTransform.anchorMax = at;
                b.rectTransform.sizeDelta = new Vector2(size * R(1f, 1.6f), size);
            }
            for (int i = 0; i < 14; i++) Blob(new Vector2(R(0.02f, 0.98f), R(0.05f, 0.9f)), R(140f, 300f), new Color(light.r, light.g, light.b, 0.35f));
            for (int i = 0; i < 8; i++) Blob(new Vector2(R(0.02f, 0.98f), R(0.05f, 0.9f)), R(100f, 220f), new Color(dark.r, dark.g, dark.b, 0.3f));
            // Stream across the lower third.
            for (int i = 0; i <= 40; i++)
            {
                float t = i / 40f;
                Blob(new Vector2(t, 0.1f + 0.06f * Mathf.Sin(t * 7f)), 38f, new Color(0.45f, 0.72f, 0.85f, 0.55f));
            }
            // Groves: leaf clusters near the edges, away from the path.
            Sprite leaf = UIKit.Lib != null ? UIKit.Lib.iconLeaf : null;
            for (int i = 0; i < 26; i++)
            {
                Vector2 at = new Vector2(R(0.02f, 0.98f), i % 2 == 0 ? R(0.8f, 0.86f) : R(0.16f, 0.2f));
                Image l = UIKit.Image("Grove", m, leaf, new Color(dark.r * 0.8f, dark.g * 0.9f, dark.b * 0.7f, 0.85f));
                l.rectTransform.anchorMin = l.rectTransform.anchorMax = at;
                l.rectTransform.sizeDelta = Vector2.one * R(34f, 60f);
                l.rectTransform.localRotation = Quaternion.Euler(0f, 0f, R(-40f, 40f));
            }
        }

        private void BuildNode(RectTransform map, EncounterDefinition e, int index)
        {
            bool done = Save.IsCompleted(e.id);
            bool open = _app.Progression.IsMissionUnlocked(e);
            bool boss = e.boss != null;
            float size = boss ? 160f : 128f;
            Color fill = done ? new Color(0.85f, 0.62f, 0.2f) : open ? UIPalette.ButtonPrimary : MenuKit.LockedGrey;
            if (boss && open && !done) fill = new Color(0.55f, 0.18f, 0.32f);
            RectTransform node = UIKit.Rect("Node_" + e.id, map);
            node.anchorMin = node.anchorMax = e.mapPosition;
            node.sizeDelta = new Vector2(size, size);
            Image halo = UIKit.Image("Halo", node, MenuKit.Circle, new Color(0f, 0f, 0f, 0.35f));
            UIKit.Place(halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(size + 14f, size + 10f));
            Image ring = UIKit.Image("Ring", node, MenuKit.Circle, done ? UIPalette.Gold : open ? UIPalette.Parchment : new Color(0.55f, 0.58f, 0.55f), true);
            UIKit.Stretch(ring.rectTransform);
            var button = ring.gameObject.AddComponent<Button>();
            button.targetGraphic = ring;
            EncounterDefinition captured = e;
            button.onClick.AddListener(() => ShowBrief(captured));
            ring.gameObject.AddComponent<ButtonJuice>();
            Image disc = UIKit.Image("Disc", ring.transform, MenuKit.Circle, fill);
            UIKit.Stretch(disc.rectTransform, size * 0.08f);
            if (!open)
            {
                Image lk = UIKit.Image("Lock", ring.transform, UIKit.Lib != null ? UIKit.Lib.iconLock : null, new Color(1f, 1f, 1f, 0.75f));
                lk.preserveAspect = true;
                UIKit.Stretch(lk.rectTransform, size * 0.28f);
            }
            else if (boss)
            {
                Image sk = UIKit.Image("Boss", ring.transform, UIKit.Lib != null ? UIKit.Lib.iconSkull : null, UIPalette.Parchment);
                sk.preserveAspect = true;
                UIKit.Stretch(sk.rectTransform, size * 0.24f);
            }
            else
            {
                TextMeshProUGUI n = UIKit.Text("Number", ring.transform, (index + 1).ToString(), size * 0.46f, UIPalette.Parchment, TextAlignmentOptions.Center, true);
                UIKit.Stretch(n.rectTransform);
                MenuKit.Shadowed(n);
            }
            if (open && !done) ring.gameObject.AddComponent<Pulse>().amount = 0.05f;

            // Name pill + stars below.
            Image pill = UIKit.Panel("NamePill", node, new Color(0.06f, 0.12f, 0.08f, 0.85f), false, false);
            pill.raycastTarget = false;
            UIKit.Place(pill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -54f), new Vector2(290f, 48f));
            TextMeshProUGUI label = UIKit.Text("Name", pill.transform, e.displayName, 28f, open ? UIPalette.Parchment : UIPalette.TextDim, TextAlignmentOptions.Center, true);
            UIKit.Stretch(label.rectTransform, 4f);
            label.enableAutoSizing = true; label.fontSizeMin = 20f; label.fontSizeMax = 28f;
            if (open)
            {
                EncounterRecord rec = Save.Find(e.id);
                MenuKit.Stars(node, rec != null ? rec.stars : 0, 34f, 4f, new Vector2(0.5f, 0f), new Vector2(0f, -96f));
            }
        }

        // ================================================================ regions column
        private void BuildRegions(CampaignDefinition c, RegionDefinition current)
        {
            RectTransform col = UIKit.Rect("Regions", _safe);
            col.anchorMin = new Vector2(1f, 0f);
            col.anchorMax = new Vector2(1f, 1f);
            col.pivot = new Vector2(1f, 1f);
            col.offsetMin = new Vector2(-(RegionColumnWidth + 24f), 28f);
            col.offsetMax = new Vector2(-24f, -(MenuKit.TopBarHeight + 12f));
            TextMeshProUGUI head = UIKit.Text("Head", col, "Regions", 40f, UIPalette.Gold, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(head.rectTransform, new Vector2(0f, 1f), new Vector2(6f, 0f), new Vector2(RegionColumnWidth, 50f));
            float y = -60f;
            foreach (RegionDefinition r in c.regions)
            {
                if (r == null) continue;
                bool open = CampaignRules.RegionUnlocked(Save, c, r);
                bool isCurrent = r == current;
                Image card = UIKit.Panel("Region_" + r.id, col, open ? Color.Lerp(r.groundColor, UIPalette.Panel, 0.45f) : new Color(0.12f, 0.12f, 0.13f, 0.95f), isCurrent, true);
                UIKit.Place(card.rectTransform, new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(RegionColumnWidth, open ? 200f : 190f));
                Image swatch = UIKit.Image("Swatch", card.transform, MenuKit.Round, open ? r.accentColor : new Color(r.accentColor.r * 0.45f, r.accentColor.g * 0.45f, r.accentColor.b * 0.45f));
                UIKit.Place(swatch.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(14f, (open ? 200f : 190f) - 36f));
                TextMeshProUGUI n = UIKit.Text("Name", card.transform, r.displayName, 36f, open ? UIPalette.Parchment : new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.TopLeft, true);
                UIKit.Place(n.rectTransform, new Vector2(0f, 1f), new Vector2(44f, -14f), new Vector2(RegionColumnWidth - 60f, 46f));
                string body;
                if (open)
                {
                    int won = 0;
                    foreach (EncounterDefinition e in r.missions) if (e != null && Save.IsCompleted(e.id)) won++;
                    body = $"{won}/{r.missions.Count} missions won\n<color=#FFD35C>{CampaignRules.StarsIn(Save, r)}/{r.missions.Count * 3} stars</color>" +
                           (CampaignRules.RegionComplete(Save, r) ? "  <color=#7CD65A>Restored</color>" : "");
                }
                else body = r.lockedHint;
                TextMeshProUGUI b = UIKit.Text("Body", card.transform, body, 25f, open ? UIPalette.TextDim : new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.TopLeft);
                UIKit.Place(b.rectTransform, new Vector2(0f, 1f), new Vector2(46f, -64f), new Vector2(RegionColumnWidth - 66f, 120f));
                if (!open)
                {
                    Image lk = UIKit.Image("Lock", card.transform, UIKit.Lib != null ? UIKit.Lib.iconLock : null, new Color(1f, 1f, 1f, 0.45f));
                    lk.preserveAspect = true;
                    UIKit.Place(lk.rectTransform, new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(44f, 44f));
                    TextMeshProUGUI soon = UIKit.Text("Soon", card.transform, r.comingSoon ? "COMING SOON" : "LOCKED", 20f, r.accentColor, TextAlignmentOptions.TopRight, true);
                    UIKit.Place(soon.rectTransform, new Vector2(1f, 1f), new Vector2(-70f, -24f), new Vector2(200f, 30f));
                }
                y -= (open ? 200f : 190f) + 16f;
            }
        }

        // ================================================================ briefing
        public void ShowBrief(EncounterDefinition e)
        {
            if (_brief != null) Destroy(_brief);
            if (!_app.Progression.IsMissionUnlocked(e))
            {
                MenuKit.Toast(_safe, "Win the previous mission to open " + e.displayName + ".", UIPalette.Parchment);
                return;
            }
            BriefMission = e;
            _brief = new GameObject("Brief", typeof(RectTransform));
            _brief.transform.SetParent(_safe, false);
            UIKit.Stretch((RectTransform)_brief.transform);
            Image dim = _brief.AddComponent<Image>();
            dim.color = UIPalette.Dim;
            dim.raycastTarget = true;
            Image panel = UIKit.Panel("Panel", _brief.transform, UIPalette.Panel, true, true);
            RectTransform p = panel.rectTransform;
            UIKit.Place(p, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1360f, 860f));
            EncounterRecord rec = Save.Find(e.id);
            bool done = rec != null && rec.completed;

            TextMeshProUGUI title = UIKit.Text("Title", p, e.displayName, 64f, e.boss != null ? new Color(1f, 0.6f, 0.65f) : UIPalette.Gold, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -26f), new Vector2(900f, 76f));
            MenuKit.Stars(p, rec != null ? rec.stars : 0, 52f, 6f, new Vector2(1f, 1f), new Vector2(-40f, -34f));
            TextMeshProUGUI brief = UIKit.Text("Briefing", p, e.briefing, 30f, UIPalette.Parchment, TextAlignmentOptions.TopLeft);
            UIKit.Place(brief.rectTransform, new Vector2(0f, 1f), new Vector2(42f, -108f), new Vector2(1270f, 96f));

            // Threat intel (left).
            TextMeshProUGUI th = UIKit.Text("ThreatsHead", p, "Threats", 36f, UIPalette.Gold, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(th.rectTransform, new Vector2(0f, 1f), new Vector2(42f, -214f), new Vector2(600f, 46f));
            List<EnemyDefinition> threats = LoadoutRules.Threats(e);
            float y = -266f;
            int shown = 0;
            foreach (EnemyDefinition en in threats)
            {
                if (shown++ >= 7) break;
                Image dot = UIKit.Image("Dot", p, MenuKit.Circle, en.accentColor);
                UIKit.Place(dot.rectTransform, new Vector2(0f, 1f), new Vector2(46f, y - 8f), new Vector2(22f, 22f));
                string hint = string.IsNullOrEmpty(en.counterHint) ? "" : $"  <color=#C9D8C4><size=80%>{en.counterHint}</size></color>";
                TextMeshProUGUI t = UIKit.Text("Threat", p, $"<b>{en.displayName}</b>" + (en.elite ? " <color=#FF5E6C>ELITE</color>" : "") + hint, 26f,
                    UIPalette.Parchment, TextAlignmentOptions.TopLeft);
                UIKit.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(80f, y), new Vector2(620f, 60f));
                t.enableAutoSizing = true; t.fontSizeMin = 18f; t.fontSizeMax = 26f;
                y -= 62f;
            }
            if (e.boss != null)
            {
                TextMeshProUGUI bt = UIKit.Text("Boss", p, $"<color=#FF8FA0><b>Boss: {e.boss.displayName}</b></color>  <size=80%>Break its pods to stagger it; strike the Core when it opens.</size>",
                    26f, UIPalette.Parchment, TextAlignmentOptions.TopLeft);
                UIKit.Place(bt.rectTransform, new Vector2(0f, 1f), new Vector2(46f, y), new Vector2(660f, 60f));
                bt.enableAutoSizing = true; bt.fontSizeMin = 18f; bt.fontSizeMax = 26f;
            }

            // Objectives and rewards (right).
            float rx = 740f;
            TextMeshProUGUI oh = UIKit.Text("ObjHead", p, "Objectives", 36f, UIPalette.Gold, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(oh.rectTransform, new Vector2(0f, 1f), new Vector2(rx, -214f), new Vector2(560f, 46f));
            float replay = Campaign != null ? Campaign.replaySeedFraction : 0.35f;
            string[] names = { "Win the battle", "Keep the Heartroot unhurt", e.challenge.IsSet ? e.challenge.text : "-" };
            string[] rewards =
            {
                done ? $"Replay: {RewardRules.Replay(e.seedReward, replay)} Seeds" : $"{e.seedReward} Seeds",
                $"{e.flawlessBonus} Seeds",
                $"{e.challengeSeeds} Seeds + {e.challengeEssence} Essence",
            };
            int[] mask = { EncounterRecord.StarWin, EncounterRecord.StarFlawless, EncounterRecord.StarChallenge };
            for (int i = 0; i < 3; i++)
            {
                bool got = rec != null && rec.HasStar(mask[i]);
                Image s = UIKit.Image("ObjStar" + i, p, UIKit.Lib != null ? UIKit.Lib.iconStar : null, got ? UIPalette.Gold : MenuKit.StarOff);
                s.preserveAspect = true;
                UIKit.Place(s.rectTransform, new Vector2(0f, 1f), new Vector2(rx, -268f - i * 86f), new Vector2(54f, 54f));
                TextMeshProUGUI t = UIKit.Text("Obj" + i, p, names[i], 28f, UIPalette.Parchment, TextAlignmentOptions.TopLeft, true);
                UIKit.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(rx + 70f, -266f - i * 86f), new Vector2(520f, 36f));
                TextMeshProUGUI rw = UIKit.Text("Reward" + i, p, got && i > 0 ? "Earned" : rewards[i], 23f, got ? UIPalette.Leaf : UIPalette.TextDim, TextAlignmentOptions.TopLeft);
                UIKit.Place(rw.rectTransform, new Vector2(0f, 1f), new Vector2(rx + 72f, -302f - i * 86f), new Vector2(520f, 30f));
            }
            float uy = -540f;
            if (e.boss != null && (rec == null || rec.bossDefeats == 0))
            {
                TextMeshProUGUI ess = UIKit.Text("BossReward", p, $"First defeat: +{e.boss.bonusSeeds} Seeds, +{e.boss.essenceFirstDefeat} Essence", 25f, MenuKit.EssenceColor, TextAlignmentOptions.TopLeft, true);
                UIKit.Place(ess.rectTransform, new Vector2(0f, 1f), new Vector2(rx, uy), new Vector2(580f, 34f));
                uy -= 44f;
            }
            if (!done && e.unlocksDefenders.Count > 0)
            {
                TextMeshProUGUI ut = UIKit.Text("UnlockHead", p, "First win adds to your garden:", 26f, UIPalette.TextDim, TextAlignmentOptions.TopLeft);
                UIKit.Place(ut.rectTransform, new Vector2(0f, 1f), new Vector2(rx, uy), new Vector2(580f, 34f));
                float x = rx;
                foreach (DefenderDefinition d in e.unlocksDefenders)
                {
                    RectTransform pt = MenuKit.Portrait(p, d, 70f, false);
                    UIKit.Place(pt, new Vector2(0f, 1f), new Vector2(x, uy - 44f), new Vector2(70f, 70f));
                    TextMeshProUGUI dn = UIKit.Text("Name", pt, d.displayName, 20f, UIPalette.Parchment, TextAlignmentOptions.Top, true);
                    UIKit.Place(dn.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -30f), new Vector2(170f, 28f));
                    x += 180f;
                }
            }

            Button close = UIKit.Button("Close", p, "Back", UIPalette.ButtonSecondary, CloseBrief, UIKit.Lib != null ? UIKit.Lib.iconClose : null, 38f);
            UIKit.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(-220f, 30f), new Vector2(300f, 100f));
            Button go = UIKit.Button("Prepare", p, "Prepare", UIPalette.ButtonPrimary, () => _hub.ShowLoadout(e), UIKit.Lib != null ? UIKit.Lib.iconPlay : null, 44f);
            UIKit.Place((RectTransform)go.transform, new Vector2(0.5f, 0f), new Vector2(160f, 30f), new Vector2(380f, 100f));
            PopIn.Play(_brief);
        }

        public void CloseBrief()
        {
            if (_brief != null) Destroy(_brief);
            BriefMission = null;
        }
    }
}
