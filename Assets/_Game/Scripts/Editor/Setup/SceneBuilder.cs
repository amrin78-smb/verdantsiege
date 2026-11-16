using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VerdantSiege.Battle;
using VerdantSiege.Battle.View;
using VerdantSiege.Data;
using VerdantSiege.UI;
using VerdantSiege.Visual;

namespace VerdantSiege.EditorTools
{
    /// <summary>
    /// Generates the MainMenu and Battle scenes (Verdant Heartlands dressing). Scenery placement is seeded, so
    /// regeneration is deterministic. Battlefield cells stay clear: dressing lives outside the 5x9 field.
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenesDir = EditorAssets.Root + "Scenes/";
        public const string MainMenuPath = ScenesDir + "MainMenu/MainMenu.unity";
        public const string BattlePath = ScenesDir + "Battle/Battle.unity";

        private static readonly Color SkyColor = new Color(0.62f, 0.78f, 0.86f);
        private static readonly Color FogColor = new Color(0.66f, 0.8f, 0.78f);

        public static void BuildAll(VisualLibrary lib, Dictionary<string, GameObject> props, GameObject heartroot, EncounterDefinition fallback)
        {
            BuildBattle(lib, props, heartroot, fallback);
            BuildMainMenu(lib, props, heartroot);
        }

        private static void Lighting(Scene scene)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.7f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.55f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.26f, 0.16f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = 16f;
            RenderSettings.fogEndDistance = 42f;
            RenderSettings.skybox = null;
            var sun = new GameObject("Sun");
            var l = sun.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(1f, 0.93f, 0.8f);
            l.intensity = 1.25f;
            l.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        }

        private static Camera MakeCamera(string name)
        {
            var go = new GameObject(name);
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = SkyColor;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 80f;
            cam.allowMSAA = true;
            cam.allowHDR = false;
            go.AddComponent<AudioListener>();
            return cam;
        }

        // ------------------------------------------------------------------ battle
        private static void BuildBattle(VisualLibrary lib, Dictionary<string, GameObject> props, GameObject heartrootPrefab, EncounterDefinition fallback)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Lighting(scene);
            Camera cam = MakeCamera("BattleCamera");
            cam.fieldOfView = 32f;
            cam.transform.SetPositionAndRotation(new Vector3(4f, 9f, -4.5f), Quaternion.Euler(54f, 0f, 0f));
            cam.gameObject.AddComponent<BattleCameraRig>();

            var env = new GameObject("Environment").transform;
            GroundPlane(env, lib, new Vector3(4.5f, -0.04f, 2.5f), 46f, 32f, 7);
            var heart = (GameObject)PrefabUtility.InstantiatePrefab(heartrootPrefab);
            heart.transform.position = new Vector3(-1.05f, 0f, 2.5f);

            DressBattlefield(env, props, 9, 5);
            MarkStatic(env.gameObject);

            var controllerGo = new GameObject("BattleController");
            var c = controllerGo.AddComponent<BattleController>();
            c.battleCamera = cam;
            c.heartroot = heart.GetComponent<HeartrootView>();
            c.battlefieldRoot = env;
            c.fallbackEncounter = fallback;
            EditorAssets.EnsureFolder(ScenesDir + "Battle");
            EditorSceneManager.SaveScene(scene, BattlePath);
        }

        private static void DressBattlefield(Transform env, Dictionary<string, GameObject> props, int cols, int lanes)
        {
            var rng = new System.Random(20261116);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            GameObject P(string n) => props.TryGetValue(n, out GameObject g) ? g : null;
            void Put(string name, Vector3 pos, float yaw, float scale)
            {
                GameObject prefab = P(name);
                if (prefab == null) return;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, env);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                go.transform.localScale = Vector3.one * scale;
            }

            // Lane borders (stone/root edging) front and back.
            for (int x = 0; x < cols; x++)
            {
                Put("LaneEdge", new Vector3(x + 0.5f, 0f, -0.08f), 0f, 1f);
                Put("LaneEdge", new Vector3(x + 0.5f, 0f, lanes + 0.08f), 180f, 1f);
            }
            // Back: woodland, ruins and a cottage (tall things only behind the field).
            for (int i = 0; i < 9; i++) Put(i % 2 == 0 ? "TreeA" : "TreeB", new Vector3(-3f + i * 1.9f + R(-0.4f, 0.4f), 0f, lanes + R(3.4f, 5.5f)), R(0f, 360f), R(0.9f, 1.25f));
            for (int i = 0; i < 7; i++) Put(i % 2 == 0 ? "BushA" : "BushB", new Vector3(-1f + i * 1.7f + R(-0.3f, 0.3f), 0f, lanes + R(0.9f, 1.8f)), R(0f, 360f), R(0.8f, 1.15f));
            Put("RuinWall", new Vector3(2.4f, 0f, lanes + 2.2f), R(-8f, 8f), 1f);
            Put("RuinWall", new Vector3(5.6f, 0f, lanes + 2.5f), 180f + R(-8f, 8f), 0.9f);
            Put("Cottage", new Vector3(8.2f, 0f, lanes + 3.4f), 200f, 1f);
            Put("RuinArch", new Vector3(-2.2f, 0f, lanes + 1.6f), 15f, 1f);
            for (int i = 0; i < 5; i++) Put(i % 3 == 0 ? "RockA" : i % 3 == 1 ? "RockB" : "RockC", new Vector3(R(-1f, 10f), 0f, lanes + R(0.5f, 2.8f)), R(0f, 360f), R(0.7f, 1.3f));
            for (int i = 0; i < 10; i++) Put("FlowerCluster", new Vector3(R(-0.8f, 9.8f), 0f, lanes + R(0.35f, 1.2f)), R(0f, 360f), R(0.8f, 1.2f));
            // Front: low fence, flowers and grass so lane 0 stays readable.
            for (int x = -1; x < cols + 1; x++) Put("FencePost", new Vector3(x + 0.5f, 0f, -0.95f), 0f, 1f);
            for (int i = 0; i < 12; i++) Put(i % 2 == 0 ? "GrassTuft" : "FlowerCluster", new Vector3(R(-1.5f, 10.5f), 0f, R(-0.75f, -0.35f)), R(0f, 360f), R(0.7f, 1.05f));
            // Left: stone path to the Heartroot.
            for (int i = 0; i < 4; i++) Put("StonePath", new Vector3(-2.6f - i * 0.95f, 0.0f, 2.5f + R(-0.1f, 0.1f)), R(-10f, 10f), 1f);
            Put("MushroomCluster", new Vector3(-0.6f, 0f, -0.45f), R(0f, 360f), 1f);
            Put("MushroomCluster", new Vector3(-0.5f, 0f, lanes + 0.5f), R(0f, 360f), 1f);
            // Right: where the Blight seeps in — rocks and mushrooms, kept beyond the spawn edge.
            for (int i = 0; i < 4; i++) Put(i % 2 == 0 ? "RockB" : "MushroomCluster", new Vector3(cols + R(1.6f, 3.2f), 0f, R(-0.5f, lanes + 0.5f)), R(0f, 360f), R(0.8f, 1.2f));
            for (int i = 0; i < 4; i++) Put(i % 2 == 0 ? "TreeB" : "TreeA", new Vector3(cols + R(3.5f, 6f), 0f, R(-1f, lanes + 2f)), R(0f, 360f), R(0.9f, 1.2f));
            for (int i = 0; i < 4; i++) Put("TreeA", new Vector3(R(-7f, -4.5f), 0f, R(-1f, lanes + 2f)), R(0f, 360f), R(0.9f, 1.2f));
        }

        private static void MarkStatic(GameObject root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        /// <summary>Large meadow ground with gentle vertex-colour variation (lit, alpha 0 = not emissive).</summary>
        private static void GroundPlane(Transform parent, VisualLibrary lib, Vector3 center, float w, float d, int seed)
        {
            string path = EditorAssets.Root + "Art/Meshes/VS_Meadow.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = "VS_Meadow" }; AssetDatabase.CreateAsset(mesh, path); }
            const int nx = 46, nz = 32;
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            var light = new Color(0.47f, 0.66f, 0.27f, 0f);
            var dark = new Color(0.27f, 0.45f, 0.18f, 0f);
            var dry = new Color(0.62f, 0.62f, 0.32f, 0f);
            for (int z = 0; z <= nz; z++)
            for (int x = 0; x <= nx; x++)
            {
                float px = (x / (float)nx - 0.5f) * w, pz = (z / (float)nz - 0.5f) * d;
                float n = Mathf.PerlinNoise(px * 0.18f + seed, pz * 0.18f + seed * 2f);
                float n2 = Mathf.PerlinNoise(px * 0.6f + 31f, pz * 0.6f + 17f);
                Color c = Color.Lerp(dark, light, n);
                c = Color.Lerp(c, dry, Mathf.Clamp01(n2 - 0.62f) * 1.6f);
                float edge = Mathf.Clamp01((Mathf.Abs(px) - w * 0.3f) / (w * 0.2f)) + Mathf.Clamp01((Mathf.Abs(pz) - d * 0.3f) / (d * 0.2f));
                c = Color.Lerp(c, dark * 0.8f, Mathf.Clamp01(edge) * 0.6f);
                c.a = 0f;
                verts.Add(new Vector3(px, (n2 - 0.5f) * 0.06f, pz));
                cols.Add(c);
                norms.Add(Vector3.up);
            }
            for (int z = 0; z < nz; z++)
            for (int x = 0; x < nx; x++)
            {
                int i = z * (nx + 1) + x;
                tris.AddRange(new[] { i, i + nx + 1, i + 1, i + 1, i + nx + 1, i + nx + 2 });
            }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var go = new GameObject("Meadow");
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = lib.vertexLit;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ main menu
        private static void BuildMainMenu(VisualLibrary lib, Dictionary<string, GameObject> props, GameObject heartrootPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Lighting(scene);
            Camera cam = MakeCamera("MenuCamera");
            cam.fieldOfView = 38f;
            cam.transform.SetPositionAndRotation(new Vector3(-1.6f, 3.1f, -6.8f), Quaternion.Euler(16f, 14f, 0f));

            var env = new GameObject("Diorama").transform;
            GroundPlane(env, lib, new Vector3(0f, -0.04f, 3f), 40f, 30f, 3);
            var heart = (GameObject)PrefabUtility.InstantiatePrefab(heartrootPrefab);
            heart.transform.position = new Vector3(2.4f, 0f, 2.2f);
            heart.transform.rotation = Quaternion.Euler(0f, -25f, 0f);

            var rng = new System.Random(77);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            void Put(string name, Vector3 pos, float yaw, float scale, Transform parent)
            {
                if (!props.TryGetValue(name, out GameObject prefab) || prefab == null) return;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                go.transform.localScale = Vector3.one * scale;
            }
            for (int i = 0; i < 8; i++) Put(i % 2 == 0 ? "TreeA" : "TreeB", new Vector3(-6f + i * 2.2f + R(-0.5f, 0.5f), 0f, R(7f, 10f)), R(0f, 360f), R(1f, 1.3f), env);
            Put("Cottage", new Vector3(-3.8f, 0f, 6.2f), 160f, 1f, env);
            Put("RuinArch", new Vector3(5.6f, 0f, 4.6f), -30f, 1.1f, env);
            Put("RuinWall", new Vector3(-0.8f, 0f, 5.2f), 10f, 1f, env);
            for (int i = 0; i < 6; i++) Put(i % 2 == 0 ? "BushA" : "BushB", new Vector3(R(-5f, 6f), 0f, R(3.5f, 5.5f)), R(0f, 360f), R(0.9f, 1.3f), env);
            for (int i = 0; i < 14; i++) Put(i % 3 == 0 ? "GrassTuft" : "FlowerCluster", new Vector3(R(-5f, 6f), 0f, R(-1f, 3.5f)), R(0f, 360f), R(0.8f, 1.2f), env);
            for (int i = 0; i < 3; i++) Put(i == 0 ? "RockA" : i == 1 ? "RockB" : "RockC", new Vector3(R(-3f, 5f), 0f, R(0f, 3f)), R(0f, 360f), R(0.8f, 1.2f), env);
            Put("MushroomCluster", new Vector3(3.9f, 0f, 1.2f), 0f, 1.1f, env);
            for (int i = 0; i < 5; i++) Put("StonePath", new Vector3(1.6f - i * 0.95f, 0f, 1.2f - i * 0.35f), R(-15f, 15f), 1f, env);
            MarkStatic(env.gameObject);

            // A few defenders guarding the Heartroot (animated by the menu controller).
            var guards = new GameObject("Guardians").transform;
            void Guard(string id, Vector3 pos, float yaw)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EditorAssets.Root}Prefabs/Defenders/{id}.prefab");
                if (prefab == null) return;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, guards);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            }
            Guard("Sunspore", new Vector3(3.2f, 0f, 0.4f), -20f);
            Guard("Glowcap", new Vector3(1.9f, 0f, 0.1f), -10f);
            Guard("BulwarkBloom", new Vector3(4.4f, 0f, 1.1f), -30f);
            Guard("Sunspore", new Vector3(0.9f, 0f, 0.9f), -5f);

            var controllerGo = new GameObject("MainMenu");
            var mm = controllerGo.AddComponent<MainMenuController>();
            mm.menuCamera = cam;
            EditorAssets.EnsureFolder(ScenesDir + "MainMenu");
            EditorSceneManager.SaveScene(scene, MainMenuPath);
        }
    }
}
