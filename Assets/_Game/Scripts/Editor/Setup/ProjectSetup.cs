using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VerdantSiege.App;
using VerdantSiege.Audio;
using VerdantSiege.Battle;
using VerdantSiege.Battle.View;
using VerdantSiege.Data;
using VerdantSiege.Visual;

namespace VerdantSiege.EditorTools
{
    /// <summary>
    /// One-button regeneration of everything derived: player/Android/quality settings, materials, fonts, UI
    /// sprites, prefabs from the Blender models, audio, content data, game config and scenes.
    /// Menu: Tools ▸ Verdant Siege ▸ Setup Project. Batch: Tools/setup.sh.
    /// </summary>
    public static class ProjectSetup
    {
        private const string ConfigPath = EditorAssets.Root + "Resources/VS_GameConfig.asset";
        private const string VisualPath = EditorAssets.Root + "Data/Visual/VisualLibrary.asset";

        public static readonly string[] PropNames =
        {
            "RockA", "RockB", "RockC", "BushA", "BushB", "FlowerCluster", "GrassTuft", "MushroomCluster",
            "TreeA", "TreeB", "RuinWall", "FencePost", "StonePath", "RuinArch", "Cottage"
        };

        [MenuItem("Tools/Verdant Siege/Setup Project", priority = 1)]
        public static void RunMenu() => Run();

        public static void RunBatch()
        {
            bool ok;
            try { ok = Run(); }
            catch (Exception e) { Debug.LogException(e); ok = false; }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Run()
        {
            DateTime start = DateTime.Now;
            ArtBuilder.Placeholders.Clear();
            ProjectConfigurator.ConfigurePlayerSettings();
            ProjectConfigurator.ConfigureQuality();

            var lib = EditorAssets.LoadOrCreateSO<VisualLibrary>(VisualPath);
            ArtBuilder.BuildMaterials(lib);
            ArtBuilder.BuildFonts(lib);
            UISpriteBuilder.BuildAll(lib);
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();

            ArtBuilder.SyncSourceArt();
            ArtBuilder.DetectFacing();
            LogModelDiagnostics();

            var prefabs = new Dictionary<string, GameObject>
            {
                ["Sunspore"] = ArtBuilder.UnitPrefab<DefenderView>("Defenders", "Sunspore", lib, 0.8f, true,
                    () => ArtBuilder.PlaceholderRig(new Color(1f, 0.65f, 0.2f), "Stem", "Head", "LeafL", "LeafR")),
                ["BulwarkBloom"] = ArtBuilder.UnitPrefab<DefenderView>("Defenders", "BulwarkBloom", lib, 0.95f, true,
                    () => ArtBuilder.PlaceholderRig(new Color(0.55f, 0.45f, 0.3f), "Core", "PlatesOuter", "PlatesInner", "Eyes")),
                ["Glowcap"] = ArtBuilder.UnitPrefab<DefenderView>("Defenders", "Glowcap", lib, 0.75f, true,
                    () => ArtBuilder.PlaceholderRig(new Color(0.3f, 0.9f, 0.85f), "Stem", "Cap", "Babies")),
                ["Drifter"] = ArtBuilder.UnitPrefab<EnemyView>("Enemies", "Drifter", lib, 0.85f, true,
                    () => ArtBuilder.PlaceholderRig(new Color(0.45f, 0.25f, 0.5f), "Body", "Head", "ArmL", "ArmR", "LegL", "LegR")),
                ["Skitter"] = ArtBuilder.UnitPrefab<EnemyView>("Enemies", "Skitter", lib, 0.75f, true,
                    () => ArtBuilder.PlaceholderRig(new Color(0.6f, 0.2f, 0.4f), "Body", "Leg0", "Leg1", "Leg2", "Leg3", "Leg4", "Leg5")),
                ["PlatedBrute"] = ArtBuilder.UnitPrefab<EnemyView>("Enemies", "PlatedBrute", lib, 1.2f, true,
                    () => ArtBuilder.PlaceholderRig(new Color(0.4f, 0.22f, 0.35f), "Body", "Head", "ArmL", "ArmR", "LegL", "LegR", "Armor")),
                ["SeedBolt"] = ArtBuilder.ProjectilePrefab(lib),
            };
            M3Assets.AddPrefabs(prefabs, lib); // Milestone 3 defenders, enemies, boss, projectiles (M3Assets.cs)
            lib.sunmote = ArtBuilder.PickupPrefab(lib);
            EvolutionAssets.Build(lib); // Living Evolution accessories (optional art; missing models fall back to a glow ring)
            lib.groundTileA = ArtBuilder.TilePrefab(lib, "GroundTileA", new Color(0.5f, 0.72f, 0.3f));
            lib.groundTileB = ArtBuilder.TilePrefab(lib, "GroundTileB", new Color(0.42f, 0.64f, 0.26f));
            lib.heartroot = ArtBuilder.HeartrootPrefab(lib);

            var props = new Dictionary<string, GameObject>();
            foreach (string p in PropNames)
            {
                bool foliage = p.StartsWith("Bush") || p.StartsWith("Tree") || p == "GrassTuft" || p == "FlowerCluster";
                Color c = foliage ? new Color(0.3f, 0.55f, 0.25f) : new Color(0.6f, 0.58f, 0.52f);
                Vector3 s = p.StartsWith("Tree") ? new Vector3(1.6f, 4f, 1.6f) : p == "Cottage" ? new Vector3(3f, 3f, 3f) : p == "RuinArch" ? new Vector3(0.6f, 3f, 2.5f) : new Vector3(0.6f, 0.5f, 0.6f);
                props[p] = ArtBuilder.PropPrefab(lib, p, foliage, c, s, p.StartsWith("Tree") ? PrimitiveType.Capsule : PrimitiveType.Cube);
            }
            if (ArtBuilder.Model("Battlefield", "LaneEdge") != null)
            {
                var edge = new GameObject("LaneEdge");
                ArtBuilder.InstantiateModel("Battlefield", "LaneEdge", lib, null, edge.transform, 0f, false).name = "Model";
                props["LaneEdge"] = EditorAssets.SavePrefab(edge, EditorAssets.Root + "Prefabs/Environment/LaneEdge.prefab");
            }
            EditorUtility.SetDirty(lib);

            var icons = new Dictionary<string, Sprite>();
            foreach (string n in new[] { "Sunspore", "BulwarkBloom", "Glowcap", "Drifter", "Skitter", "PlatedBrute" }) icons[n] = ArtBuilder.Icon(n);
            M3Assets.AddIcons(icons); // Milestone 3 (missing icons are allowed: cards show a monogram)

            BrandingBuilder.Build();
            AudioLibrary audio = AudioSynth.BuildAll();
            ContentDatabase db = ContentBuilder.Build(prefabs, icons);

            var cfg = EditorAssets.LoadOrCreateSO<GameConfig>(ConfigPath);
            cfg.content = db;
            cfg.visuals = lib;
            cfg.audio = audio;
            cfg.firstEncounterId = "vh_01";
            cfg.versionLabel = ProjectConfigurator.AppVersion + " prototype (M4 campaign)";
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();

            // Validate before scene generation: NewScene unloads unreferenced in-memory assets.
            bool ok = Validate(db);
            SceneBuilder.BuildAll(lib, props, lib.heartroot, db.encounters.Count > 0 ? db.encounters[0] : null);
            ProjectConfigurator.ConfigureBuildScenes(SceneBuilder.MainMenuPath, SceneBuilder.BattlePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[VS Setup] Done in {(DateTime.Now - start).TotalSeconds:0}s. Placeholders ({ArtBuilder.Placeholders.Count}): " +
                      (ArtBuilder.Placeholders.Count == 0 ? "none" : string.Join(", ", ArtBuilder.Placeholders)) + "\n" + ProjectConfigurator.Describe());
            return ok;
        }

        private static bool Validate(ContentDatabase db)
        {
            bool ok = true;
            foreach (EncounterDefinition e in db.encounters)
            {
                List<string> errors = EncounterValidator.Validate(e);
                foreach (string err in errors) { Debug.LogError("[VS Setup] Content: " + err); ok = false; }
            }
            foreach (DefenderDefinition d in db.defenders)
            {
                if (d.prefab == null) { Debug.LogError($"[VS Setup] {d.id} has no prefab"); ok = false; }
                if (d.icon == null) Debug.LogWarning($"[VS Setup] {d.id} has no icon (card shows no portrait)");
            }
            foreach (EnemyDefinition e in db.enemies) if (e.prefab == null) { Debug.LogError($"[VS Setup] {e.id} has no prefab"); ok = false; }
            return ok;
        }

        /// <summary>Logs bounds and a vertex-colour sample per model (scale/axis/colour-space sanity).</summary>
        private static void LogModelDiagnostics()
        {
            foreach (string cat in new[] { "Defenders", "Enemies", "Bosses", "Battlefield", "FX" })
            {
                string[] guids = AssetDatabase.FindAssets("t:Model", new[] { EditorAssets.Root + "Art/Models/" + cat });
                foreach (string g in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(g);
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null) continue;
                    var b = new Bounds();
                    bool first = true;
                    int tris = 0;
                    Color sample = Color.clear;
                    foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
                    {
                        Mesh m = mf.sharedMesh;
                        if (m == null) continue;
                        tris += (int)(m.triangles.Length / 3);
                        Bounds wb = TransformBounds(mf.transform, m.bounds, go.transform);
                        if (first) { b = wb; first = false; } else b.Encapsulate(wb);
                        if (sample == Color.clear && m.colors.Length > 0) sample = m.colors[m.colors.Length / 2];
                    }
                    Debug.Log($"[VS Art] {System.IO.Path.GetFileNameWithoutExtension(path)}: size={b.size.x:0.00}x{b.size.y:0.00}x{b.size.z:0.00} " +
                              $"min=({b.min.x:0.00},{b.min.y:0.00},{b.min.z:0.00}) tris={tris} colorSample={sample}");
                }
            }
        }

        private static Bounds TransformBounds(Transform t, Bounds local, Transform root)
        {
            Matrix4x4 m = root.worldToLocalMatrix * t.localToWorldMatrix;
            Vector3 c = m.MultiplyPoint3x4(local.center);
            Vector3 e = local.extents;
            Vector3 ax = m.MultiplyVector(new Vector3(e.x, 0, 0)), ay = m.MultiplyVector(new Vector3(0, e.y, 0)), az = m.MultiplyVector(new Vector3(0, 0, e.z));
            var ext = new Vector3(Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x), Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y), Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
            return new Bounds(c, ext * 2f);
        }
    }
}
