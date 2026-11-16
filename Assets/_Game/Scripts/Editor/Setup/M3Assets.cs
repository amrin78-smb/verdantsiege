using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VerdantSiege.Battle.View;
using VerdantSiege.Visual;

namespace VerdantSiege.EditorTools
{
    /// <summary>
    /// Milestone 3 prefabs (9 defenders, 9 enemies, the Root Maw boss, 5 projectiles) and card icons. Every unit is
    /// built with <see cref="ArtBuilder.UnitPrefab{TView}"/> and a placeholder rig carrying the Blender part-name
    /// contract, so a missing model shows as a labelled placeholder and real art drops in with no code change.
    /// Called from <see cref="ProjectSetup.Run"/>.
    /// </summary>
    public static class M3Assets
    {
        public static readonly string[] DefenderArt =
            { "Thorncaster", "SnapjawVine", "FrostFern", "EmberLily", "Whirlseed", "MenderMoss", "Burrowroot", "StormOrchid", "Mirrorleaf" };
        public static readonly string[] EnemyArt =
            { "Vaultling", "SporeCarrier", "Burrower", "Howler", "Sapper", "Glider", "Mimic", "Warden", "BlightHerald" };

        public static void AddPrefabs(Dictionary<string, GameObject> prefabs, VisualLibrary lib)
        {
            // ---- defenders (face +X)
            prefabs["Thorncaster"] = D(lib, "Thorncaster", 0.8f, new Color(0.45f, 0.7f, 0.25f), "Stem", "Head", "LeafL", "LeafR", "Muzzle");
            prefabs["SnapjawVine"] = D(lib, "SnapjawVine", 0.85f, new Color(0.75f, 0.25f, 0.3f), "Stem", "Jaw", "JawLower", "LeafL", "LeafR");
            prefabs["FrostFern"] = D(lib, "FrostFern", 0.8f, new Color(0.5f, 0.8f, 0.95f), "Stem", "Fronds", "Crystal", "Muzzle");
            prefabs["EmberLily"] = D(lib, "EmberLily", 0.8f, new Color(1f, 0.45f, 0.15f), "Stem", "Bloom", "Core", "Muzzle");
            prefabs["Whirlseed"] = D(lib, "Whirlseed", 0.75f, new Color(0.9f, 0.8f, 0.4f), "Stem", "Rotor", "Pod", "Muzzle");
            prefabs["MenderMoss"] = D(lib, "MenderMoss", 0.85f, new Color(0.35f, 0.8f, 0.45f), "Mound", "Orbs", "Bloom");
            prefabs["Burrowroot"] = D(lib, "Burrowroot", 0.7f, new Color(0.55f, 0.4f, 0.25f), "Ring", "Sprout", "Spikes");
            prefabs["StormOrchid"] = D(lib, "StormOrchid", 0.8f, new Color(0.6f, 0.55f, 0.95f), "Stem", "Petals", "Rod", "Muzzle");
            prefabs["Mirrorleaf"] = D(lib, "Mirrorleaf", 0.9f, new Color(0.75f, 0.9f, 0.9f), "Stem", "Mirror", "LeafL", "LeafR");

            // ---- enemies (face -X)
            prefabs["Vaultling"] = E(lib, "Vaultling", 0.7f, new Color(0.85f, 0.5f, 0.25f), 0f, "Body", "Head", "LegL", "LegR", "ArmL", "ArmR");
            prefabs["SporeCarrier"] = E(lib, "SporeCarrier", 0.9f, new Color(0.5f, 0.7f, 0.25f), 0f, "Body", "Sac", "LegL", "LegR", "Head");
            prefabs["Burrower"] = E(lib, "Burrower", 0.8f, new Color(0.5f, 0.38f, 0.25f), 0f, "Body", "Head", "ArmL", "ArmR");
            prefabs["Howler"] = E(lib, "Howler", 0.85f, new Color(0.75f, 0.3f, 0.3f), 0f, "Body", "Head", "Sac", "ArmL", "ArmR", "LegL", "LegR");
            prefabs["Sapper"] = E(lib, "Sapper", 0.8f, new Color(0.4f, 0.6f, 0.3f), 0f, "Body", "Head", "Canister", "ArmL", "ArmR", "LegL", "LegR");
            prefabs["Glider"] = E(lib, "Glider", 0.6f, new Color(0.5f, 0.4f, 0.8f), 0.6f, "Body", "WingL", "WingR", "Head");
            prefabs["Mimic"] = E(lib, "Mimic", 0.8f, new Color(0.65f, 0.45f, 0.75f), 0f, "Body", "Eyes", "Tendrils");
            prefabs["Warden"] = E(lib, "Warden", 1.1f, new Color(0.45f, 0.55f, 0.7f), 0f, "Body", "Head", "ArmL", "ArmR", "LegL", "LegR", "Shield", "Aura");
            prefabs["BlightHerald"] = E(lib, "BlightHerald", 1.0f, new Color(0.65f, 0.25f, 0.45f), 0f, "Body", "Head", "ArmL", "ArmR", "LegL", "LegR", "Banner");

            // ---- boss (faces -X, spans all lanes at the entry edge)
            prefabs["RootMaw"] = ArtBuilder.UnitPrefab<BossView>("Bosses", "RootMaw", lib, 4f, true, RootMawPlaceholder);

            // ---- projectiles
            prefabs["Thorn"] = Projectile(lib, "Thorn", PrimitiveType.Capsule, new Vector3(0.07f, 0.2f, 0.07f), new Vector3(0f, 0f, 90f), new Color(0.6f, 0.85f, 0.35f), new Color(0.8f, 1f, 0.55f));
            prefabs["FrostShard"] = Projectile(lib, "FrostShard", PrimitiveType.Cube, new Vector3(0.2f, 0.09f, 0.09f), new Vector3(45f, 0f, 0f), new Color(0.7f, 0.92f, 1f), new Color(0.6f, 0.9f, 1f));
            prefabs["EmberSeed"] = Projectile(lib, "EmberSeed", PrimitiveType.Sphere, new Vector3(0.22f, 0.22f, 0.22f), Vector3.zero, new Color(1f, 0.5f, 0.15f), new Color(1f, 0.6f, 0.2f));
            prefabs["WhirlSeed"] = Projectile(lib, "WhirlSeed", PrimitiveType.Cylinder, new Vector3(0.26f, 0.02f, 0.26f), new Vector3(90f, 0f, 0f), new Color(0.95f, 0.85f, 0.45f), new Color(1f, 0.95f, 0.6f));
            prefabs["SapperSpit"] = Projectile(lib, "SapperSpit", PrimitiveType.Sphere, new Vector3(0.18f, 0.15f, 0.15f), Vector3.zero, new Color(0.55f, 0.95f, 0.25f), new Color(0.6f, 1f, 0.3f));
        }

        public static void AddIcons(Dictionary<string, Sprite> icons)
        {
            foreach (string n in DefenderArt) icons[n] = ArtBuilder.Icon(n);
            foreach (string n in EnemyArt) icons[n] = ArtBuilder.Icon(n);
        }

        private static GameObject D(VisualLibrary lib, string name, float shadow, Color c, params string[] parts) =>
            ArtBuilder.UnitPrefab<DefenderView>("Defenders", name, lib, shadow, true, () => ArtBuilder.PlaceholderRig(c, parts));

        private static GameObject E(VisualLibrary lib, string name, float shadow, Color c, float lift, params string[] parts) =>
            ArtBuilder.UnitPrefab<EnemyView>("Enemies", name, lib, shadow, true, () =>
            {
                GameObject rig = ArtBuilder.PlaceholderRig(c, parts);
                if (lift > 0f) foreach (Transform t in rig.transform) t.localPosition += Vector3.up * lift; // Glider hovers
                return rig;
            });

        /// <summary>Big labelled stand-in with the boss part contract (front faces -X).</summary>
        private static GameObject RootMawPlaceholder()
        {
            var root = new GameObject("Rig");
            Color bark = new Color(0.35f, 0.22f, 0.25f);
            void Part(PrimitiveType t, string n, Vector3 pos, Vector3 scale, Color col) =>
                ArtBuilder.Primitive(t, pos, scale, col, n).transform.SetParent(root.transform, false);
            Part(PrimitiveType.Sphere, "Body", new Vector3(0.6f, 1.0f, 0f), new Vector3(2.2f, 2.0f, 4.6f), bark);
            Part(PrimitiveType.Cube, "Jaw", new Vector3(-0.45f, 0.35f, 0f), new Vector3(0.9f, 0.4f, 2.6f), bark * 0.8f);
            Part(PrimitiveType.Capsule, "RootL", new Vector3(-0.2f, 0.4f, 2.2f), new Vector3(0.5f, 1.2f, 0.5f), bark * 0.9f);
            Part(PrimitiveType.Capsule, "RootR", new Vector3(-0.2f, 0.4f, -2.2f), new Vector3(0.5f, 1.2f, 0.5f), bark * 0.9f);
            Part(PrimitiveType.Sphere, "Eye", new Vector3(-0.35f, 1.7f, 0f), new Vector3(0.45f, 0.45f, 0.45f), new Color(1f, 0.85f, 0.3f));
            Part(PrimitiveType.Sphere, "Core", new Vector3(-0.4f, 0.95f, 0f), new Vector3(0.7f, 0.7f, 0.7f), new Color(0.95f, 0.35f, 0.75f));
            return root;
        }

        private static GameObject Projectile(VisualLibrary lib, string name, PrimitiveType shape, Vector3 scale, Vector3 euler, Color color, Color trailColor)
        {
            var root = new GameObject(name);
            GameObject model = ArtBuilder.InstantiateModel("FX", name, lib, () =>
            {
                GameObject g = ArtBuilder.Primitive(shape, Vector3.zero, scale, color, "Mesh");
                g.transform.localRotation = Quaternion.Euler(euler);
                var holder = new GameObject("Rig");
                g.transform.SetParent(holder.transform, false);
                return holder;
            }, root.transform, ArtBuilder.FacingYaw, false);
            model.name = "Model";
            var trailGo = new GameObject("Trail");
            trailGo.transform.SetParent(root.transform, false);
            var trail = trailGo.AddComponent<TrailRenderer>();
            trail.time = 0.16f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.13f), new Keyframe(1f, 0f));
            var g2 = new Gradient();
            g2.SetKeys(new[] { new GradientColorKey(trailColor, 0f), new GradientColorKey(trailColor * 0.8f, 1f) },
                       new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g2;
            trail.sharedMaterial = ArtBuilder.GlowTinted("VS_Trail", Color.white, 0.6f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.numCapVertices = 2;
            root.AddComponent<ProjectileView>();
            return EditorAssets.SavePrefab(root, EditorAssets.Root + "Prefabs/FX/" + name + ".prefab");
        }
    }
}
