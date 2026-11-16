using UnityEngine;
using VerdantSiege.Visual;

namespace VerdantSiege.EditorTools
{
    /// <summary>
    /// Evolution accessory prefabs from the art team's FX models (EvoSolar, EvoPrism, EvoRoot, EvoBloom). They are
    /// optional: a missing model leaves the library slot empty and the evolved defender shows its accent glow ring
    /// and tint only (no placeholder primitive, nothing fails). Called from <see cref="ProjectSetup.Run"/>.
    /// </summary>
    public static class EvolutionAssets
    {
        public static void Build(VisualLibrary lib)
        {
            lib.evoSolar = Accessory(lib, "EvoSolar");
            lib.evoPrism = Accessory(lib, "EvoPrism");
            lib.evoRoot = Accessory(lib, "EvoRoot");
            lib.evoBloom = Accessory(lib, "EvoBloom");
        }

        private static GameObject Accessory(VisualLibrary lib, string name)
        {
            if (ArtBuilder.Model("FX", name) == null)
            {
                Debug.Log($"[VS Setup] Evolution accessory FX/{name} not found: evolved defenders use the glow ring only.");
                return null;
            }
            var root = new GameObject(name);
            GameObject model = ArtBuilder.InstantiateModel("FX", name, lib, null, root.transform, ArtBuilder.FacingYaw, false);
            model.name = "Model";
            return EditorAssets.SavePrefab(root, EditorAssets.Root + "Prefabs/FX/" + name + ".prefab");
        }
    }
}
