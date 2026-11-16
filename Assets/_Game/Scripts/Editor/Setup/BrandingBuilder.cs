using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace VerdantSiege.EditorTools
{
    /// <summary>
    /// App icon (adaptive + round + legacy) and splash logo from the Blender-rendered branding art in
    /// Art/Branding. Splash: the Verdant Siege logo on deep moss, followed by the Unity logo.
    /// </summary>
    public static class BrandingBuilder
    {
        private const string Src = "Art/Branding/";
        private const string Dst = EditorAssets.Root + "Art/Branding/";
        public static readonly Color SplashBackground = new Color32(0x14, 0x2A, 0x1E, 0xFF);

        public static void Build()
        {
            string[] files = { "icon_foreground.png", "icon_background.png", "icon_legacy.png", "logo.png" };
            foreach (string f in files)
                if (!File.Exists(Src + f)) { Debug.LogWarning($"[VS Branding] {Src}{f} missing; icon/splash left unchanged."); return; }
            EditorAssets.EnsureFolder(Dst.TrimEnd('/'));
            foreach (string f in files) File.Copy(Src + f, Dst + f, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Texture2D fg = Tex("icon_foreground.png", false);
            Texture2D bg = Tex("icon_background.png", false);
            Texture2D legacy = Tex("icon_legacy.png", false);
            Sprite logo = LogoSprite();

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { legacy }, IconKind.Application);
            NamedBuildTarget android = NamedBuildTarget.Android;
            PlatformIcon[] adaptive = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Adaptive);
            foreach (PlatformIcon icon in adaptive) icon.SetTextures(bg, fg);
            PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Adaptive, adaptive);
            foreach (PlatformIconKind kind in new[] { AndroidPlatformIconKind.Round, AndroidPlatformIconKind.Legacy })
            {
                PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(android, kind);
                foreach (PlatformIcon icon in icons) icon.SetTexture(legacy);
                PlayerSettings.SetPlatformIcons(android, kind, icons);
            }

            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = true;
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
            PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Dolly;
            PlayerSettings.SplashScreen.backgroundColor = SplashBackground;
            PlayerSettings.SplashScreen.background = null;
            PlayerSettings.SplashScreen.overlayOpacity = 0f;
            PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2.5f, logo) };
            AssetDatabase.SaveAssets();
            Debug.Log("[VS Branding] App icon (adaptive/round/legacy) and splash logo applied.");
        }

        private static Texture2D Tex(string name, bool sprite)
        {
            string path = Dst + name;
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Sprite LogoSprite()
        {
            string path = Dst + "logo.png";
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
