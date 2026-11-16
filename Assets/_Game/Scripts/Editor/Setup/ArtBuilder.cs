using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using VerdantSiege.Battle.View;
using VerdantSiege.Visual;

namespace VerdantSiege.EditorTools
{
    /// <summary>
    /// Imports the Blender models (Art/Models → Assets/_Game/Art/Models), builds shared materials, fonts and
    /// the game prefabs. Missing models get clearly-labelled primitive placeholders so the game stays playable.
    /// </summary>
    public static class ArtBuilder
    {
        public const string SourceArt = "Art/";
        private const string Mat = EditorAssets.Root + "Art/Materials/";
        private const string Prefabs = EditorAssets.Root + "Prefabs/";
        private const string Fonts = EditorAssets.Root + "UI/Fonts/";
        public static readonly List<string> Placeholders = new List<string>();

        /// <summary>Yaw (degrees) applied to directional models so defenders face +X (determined from Sunspore's Muzzle).</summary>
        public static float FacingYaw { get; private set; }

        // ------------------------------------------------------------------ source sync
        /// <summary>Copies FBX/PNG outputs of Tools/Blender into the Unity project (only when changed).</summary>
        public static void SyncSourceArt()
        {
            int copied = 0;
            foreach (string cat in new[] { "Defenders", "Enemies", "Battlefield", "Props", "FX" })
            {
                string src = Path.Combine(SourceArt, "Models", cat);
                if (!Directory.Exists(src)) continue;
                string dst = EditorAssets.Root + "Art/Models/" + cat;
                Directory.CreateDirectory(dst);
                foreach (string f in Directory.GetFiles(src, "*.fbx")) copied += CopyIfChanged(f, Path.Combine(dst, Path.GetFileName(f)));
            }
            if (Directory.Exists(SourceArt + "Icons"))
            {
                string dst = EditorAssets.Root + "Art/Icons";
                Directory.CreateDirectory(dst);
                foreach (string f in Directory.GetFiles(SourceArt + "Icons", "*.png")) copied += CopyIfChanged(f, Path.Combine(dst, Path.GetFileName(f)));
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[VS Art] Synced Blender outputs ({copied} changed files).");
        }

        private static int CopyIfChanged(string src, string dst)
        {
            if (File.Exists(dst))
            {
                var a = new FileInfo(src);
                var b = new FileInfo(dst);
                if (a.Length == b.Length && BytesEqual(File.ReadAllBytes(src), File.ReadAllBytes(dst))) return 0;
            }
            File.Copy(src, dst, true);
            return 1;
        }

        private static bool BytesEqual(byte[] x, byte[] y)
        {
            if (x.Length != y.Length) return false;
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
            return true;
        }

        public static GameObject Model(string category, string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{EditorAssets.Root}Art/Models/{category}/{name}.fbx");

        public static Sprite Icon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{EditorAssets.Root}Art/Icons/{name}_icon.png");

        // ------------------------------------------------------------------ materials & meshes
        public static void BuildMaterials(VisualLibrary lib)
        {
            Shader lit = Shader.Find("VerdantSiege/VertexLit");
            Shader glow = Shader.Find("VerdantSiege/Glow");
            Shader flat = Shader.Find("VerdantSiege/FlatAlpha");
            if (lit == null || glow == null || flat == null) throw new System.Exception("Verdant Siege shaders not found (compile errors?)");
            lib.vertexLit = EditorAssets.LoadOrCreateMaterial(Mat + "VS_VertexLit.mat", lit);
            lib.vertexLit.SetFloat("_WindStrength", 0f);
            lib.vertexLit.enableInstancing = true;
            lib.vertexLitWind = EditorAssets.LoadOrCreateMaterial(Mat + "VS_VertexLit_Foliage.mat", lit);
            lib.vertexLitWind.SetFloat("_WindStrength", 0.035f);
            lib.vertexLitWind.enableInstancing = true;
            lib.glowAdditive = EditorAssets.LoadOrCreateMaterial(Mat + "VS_Glow.mat", glow);
            lib.glowAdditive.SetFloat("_DstBlend", 1f);
            lib.glowAdditive.SetFloat("_Softness", 1.6f);
            lib.blobShadow = EditorAssets.LoadOrCreateMaterial(Mat + "VS_BlobShadow.mat", flat);
            lib.blobShadow.SetColor("_BaseColor", new Color(0.03f, 0.08f, 0.02f, 0.42f));
            lib.blobShadow.SetFloat("_Radial", 1f);
            lib.cellHighlight = EditorAssets.LoadOrCreateMaterial(Mat + "VS_CellHighlight.mat", flat);
            lib.cellHighlight.SetColor("_BaseColor", Color.white);
            lib.cellHighlight.SetFloat("_Radial", 0f);
            lib.cellHighlight.SetFloat("_Edge", 0.07f);
            foreach (Material m in new[] { lib.vertexLit, lib.vertexLitWind, lib.glowAdditive, lib.blobShadow, lib.cellHighlight }) EditorUtility.SetDirty(m);
            lib.quadMesh = QuadMesh();
        }

        public static Material GlowTinted(string name, Color c, float softness = 1.6f)
        {
            Material m = EditorAssets.LoadOrCreateMaterial(Mat + name + ".mat", Shader.Find("VerdantSiege/Glow"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Softness", softness);
            m.SetFloat("_DstBlend", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Mesh QuadMesh()
        {
            string path = EditorAssets.Root + "Art/Meshes/VS_Quad.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                EditorAssets.EnsureFolder(Path.GetDirectoryName(path));
                mesh = new Mesh { name = "VS_Quad" };
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.normals = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ------------------------------------------------------------------ fonts
        public static void BuildFonts(VisualLibrary lib)
        {
            lib.displayFont = FontAsset("LilitaOne-Regular.ttf", "LilitaOne SDF");
            lib.bodyFont = FontAsset("Inter-SemiBold.ttf", "Inter SemiBold SDF");
        }

        private static TMP_FontAsset FontAsset(string ttf, string name)
        {
            string path = Fonts + name + ".asset";
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fa != null) return fa;
            var font = AssetDatabase.LoadAssetAtPath<Font>(Fonts + ttf);
            if (font == null) throw new System.Exception("Font missing: " + Fonts + ttf);
            fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = name;
            AssetDatabase.CreateAsset(fa, path);
            fa.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            foreach (Texture2D t in fa.atlasTextures)
            {
                t.name = name + " Atlas";
                AssetDatabase.AddObjectToAsset(t, fa);
            }
            fa.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~•");
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        // ------------------------------------------------------------------ orientation
        /// <summary>Measures how the Blender→FBX→Unity conversion oriented the models (Sunspore's Muzzle must be +X).</summary>
        public static void DetectFacing()
        {
            FacingYaw = 0f;
            GameObject src = Model("Defenders", "Sunspore");
            if (src == null) { Debug.LogWarning("[VS Art] Sunspore model missing; assuming no facing correction."); return; }
            var go = (GameObject)Object.Instantiate(src);
            Transform muzzle = FindDeep(go.transform, "Muzzle");
            if (muzzle != null)
            {
                Vector3 local = go.transform.InverseTransformPoint(muzzle.position);
                if (local.x < 0f) FacingYaw = 180f;
                Debug.Log($"[VS Art] Sunspore Muzzle at {local} -> facing yaw {FacingYaw}");
            }
            else Debug.LogWarning("[VS Art] Sunspore has no Muzzle marker; assuming no facing correction.");
            Object.DestroyImmediate(go);
        }

        public static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        // ------------------------------------------------------------------ prefabs
        /// <summary>Unit prefab: root [UnitRig + view] → Model (FBX, child 0) + Shadow.</summary>
        public static GameObject UnitPrefab<TView>(string category, string name, VisualLibrary lib, float shadowSize, bool directional,
                                                   System.Func<GameObject> placeholder) where TView : Component
        {
            var root = new GameObject(name);
            GameObject model = InstantiateModel(category, name, lib, placeholder, root.transform, directional ? FacingYaw : 0f, false);
            model.name = "Model";
            AddShadow(root.transform, lib, shadowSize);
            root.AddComponent<UnitRig>();
            root.AddComponent<TView>();
            return EditorAssets.SavePrefab(root, $"{Prefabs}{category}/{name}.prefab");
        }

        public static GameObject InstantiateModel(string category, string name, VisualLibrary lib, System.Func<GameObject> placeholder,
                                                  Transform parent, float yaw, bool foliage)
        {
            GameObject src = Model(category, name);
            GameObject model;
            if (src != null)
            {
                model = (GameObject)PrefabUtility.InstantiatePrefab(src);
                model.transform.SetParent(parent, false);
                model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterials = new[] { foliage ? lib.vertexLitWind : lib.vertexLit };
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                }
            }
            else
            {
                Placeholders.Add($"{category}/{name}");
                model = placeholder != null ? placeholder() : Primitive(PrimitiveType.Cube, Vector3.zero, Vector3.one * 0.5f, Color.magenta, "Body");
                model.name = name + "_PLACEHOLDER";
                model.transform.SetParent(parent, false);
            }
            return model;
        }

        public static void AddShadow(Transform parent, VisualLibrary lib, float size)
        {
            var sh = new GameObject("Shadow");
            sh.transform.SetParent(parent, false);
            sh.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            sh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            sh.transform.localScale = new Vector3(size, size * 0.8f, 1f);
            sh.AddComponent<MeshFilter>().sharedMesh = lib.quadMesh;
            var mr = sh.AddComponent<MeshRenderer>();
            mr.sharedMaterial = lib.blobShadow;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        /// <summary>Clearly-marked placeholder primitive (separate simple-lit material, never shipped as final art).</summary>
        public static GameObject Primitive(PrimitiveType type, Vector3 pos, Vector3 scale, Color color, string name)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            string path = $"{Mat}Placeholder_{ColorUtility.ToHtmlStringRGB(color)}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { color = color };
                EditorAssets.EnsureFolder(Mat);
                AssetDatabase.CreateAsset(m, path);
            }
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        /// <summary>Placeholder rig with the contract's part names so animation code paths still run.</summary>
        public static GameObject PlaceholderRig(Color body, params string[] parts)
        {
            var root = new GameObject("Rig");
            GameObject b = Primitive(PrimitiveType.Capsule, new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.45f, 0.45f), body, parts.Length > 0 ? parts[0] : "Body");
            b.transform.SetParent(root.transform, false);
            for (int i = 1; i < parts.Length; i++)
            {
                GameObject p = Primitive(PrimitiveType.Sphere, new Vector3(-0.15f, 0.7f + 0.05f * i, 0f), Vector3.one * 0.22f, body * 0.85f, parts[i]);
                p.transform.SetParent(root.transform, false);
            }
            return root;
        }

        // ------------------------------------------------------------------ specific prefabs
        public static GameObject ProjectilePrefab(VisualLibrary lib)
        {
            var root = new GameObject("SeedBolt");
            GameObject model = InstantiateModel("FX", "SeedBolt", lib,
                () => Primitive(PrimitiveType.Sphere, Vector3.zero, new Vector3(0.22f, 0.16f, 0.16f), new Color(1f, 0.8f, 0.3f), "Mesh"),
                root.transform, FacingYaw, false);
            model.name = "Model";
            var trailGo = new GameObject("Trail");
            trailGo.transform.SetParent(root.transform, false);
            var trail = trailGo.AddComponent<TrailRenderer>();
            trail.time = 0.14f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.14f), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.2f), 1f) },
                      new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.sharedMaterial = GlowTinted("VS_Trail", Color.white, 0.6f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.numCapVertices = 2;
            root.AddComponent<ProjectileView>();
            return EditorAssets.SavePrefab(root, Prefabs + "FX/SeedBolt.prefab");
        }

        public static GameObject PickupPrefab(VisualLibrary lib)
        {
            var root = new GameObject("Sunmote");
            GameObject model = InstantiateModel("FX", "Sunmote", lib,
                () => Primitive(PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.3f, new Color(1f, 0.85f, 0.3f), "Orb"),
                root.transform, 0f, false);
            model.name = "Model";
            var halo = new GameObject("Halo");
            halo.transform.SetParent(model.transform, false);
            halo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            halo.transform.localPosition = new Vector3(0f, -0.28f, 0f);
            halo.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
            halo.AddComponent<MeshFilter>().sharedMesh = lib.quadMesh;
            var mr = halo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GlowTinted("VS_GlowGold", new Color(1f, 0.78f, 0.3f, 0.75f), 1.4f);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            root.AddComponent<PickupView>();
            return EditorAssets.SavePrefab(root, Prefabs + "FX/Sunmote.prefab");
        }

        public static GameObject TilePrefab(VisualLibrary lib, string name, Color fallback)
        {
            var root = new GameObject(name);
            GameObject model = InstantiateModel("Battlefield", name, lib,
                () => Primitive(PrimitiveType.Cube, new Vector3(0f, -0.02f, 0f), new Vector3(0.98f, 0.06f, 0.98f), fallback, "Tile"),
                root.transform, 0f, false);
            model.name = "Model";
            return EditorAssets.SavePrefab(root, Prefabs + "Environment/" + name + ".prefab");
        }

        public static GameObject HeartrootPrefab(VisualLibrary lib)
        {
            var root = new GameObject("Heartroot");
            GameObject model = InstantiateModel("Battlefield", "Heartroot", lib,
                () =>
                {
                    var r = new GameObject("Rig");
                    Primitive(PrimitiveType.Cylinder, new Vector3(0f, 1.2f, 0f), new Vector3(0.9f, 1.2f, 0.9f), new Color(0.45f, 0.3f, 0.2f), "Trunk").transform.SetParent(r.transform, false);
                    Primitive(PrimitiveType.Sphere, new Vector3(0f, 2.8f, 0f), new Vector3(2.6f, 1.6f, 3.4f), new Color(0.3f, 0.6f, 0.25f), "Canopy").transform.SetParent(r.transform, false);
                    Primitive(PrimitiveType.Sphere, new Vector3(0.45f, 1.1f, 0f), Vector3.one * 0.45f, new Color(1f, 0.7f, 0.3f), "HeartCore").transform.SetParent(r.transform, false);
                    return r;
                }, root.transform, FacingYaw, false);
            model.name = "Model";
            // Canopy sways in the wind.
            Transform canopy = FindDeep(model.transform, "Canopy");
            if (canopy != null) foreach (Renderer r in canopy.GetComponentsInChildren<Renderer>()) if (r.sharedMaterial == lib.vertexLit) r.sharedMaterial = lib.vertexLitWind;
            AddShadow(root.transform, lib, 3.2f);
            root.AddComponent<UnitRig>();
            root.AddComponent<HeartrootView>();
            return EditorAssets.SavePrefab(root, Prefabs + "Environment/Heartroot.prefab");
        }

        /// <summary>Static scenery prop (foliage props get the wind material).</summary>
        public static GameObject PropPrefab(VisualLibrary lib, string name, bool foliage, Color fallback, Vector3 fallbackScale, PrimitiveType fallbackType)
        {
            var root = new GameObject(name);
            GameObject model = InstantiateModel("Props", name, lib,
                () => Primitive(fallbackType, new Vector3(0f, fallbackScale.y * 0.5f, 0f), fallbackScale, fallback, "Mesh"),
                root.transform, 0f, foliage);
            model.name = "Model";
            return EditorAssets.SavePrefab(root, Prefabs + "Environment/Props/" + name + ".prefab");
        }
    }
}
