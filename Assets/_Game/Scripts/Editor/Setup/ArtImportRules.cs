using UnityEditor;

namespace VerdantSiege.EditorTools
{
    /// <summary>
    /// Import rules for Blender-generated models (Assets/_Game/Art/Models) and rendered icons
    /// (Assets/_Game/Art/Icons). Models: metres, axis conversion baked, hierarchy and pivots kept (parts are
    /// animated by name), Blender normals imported, vertex colours kept, no materials/animation/cameras/lights.
    /// </summary>
    public sealed class ArtImportRules : AssetPostprocessor
    {
        public const string ModelsFolder = "Assets/_Game/Art/Models/";
        public const string IconsFolder = "Assets/_Game/Art/Icons/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelsFolder)) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;
            mi.preserveHierarchy = true;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.importVisibility = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.meshCompression = ModelImporterMeshCompression.Low;
            mi.isReadable = false;
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            mi.weldVertices = true;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            mi.addCollider = false;
            mi.generateSecondaryUV = false;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(IconsFolder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = 256;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.crunchedCompression = false;
            ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        }
    }
}
