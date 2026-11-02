using UnityEditor;
using UnityEngine;

namespace TY.FXFacialAssistant
{
    internal static class ExpressionMenuIconImportSettings
    {
        public static void Apply(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.streamingMipmaps = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 0;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 100;
            importer.crunchedCompression = false;
            var defaults = importer.GetDefaultPlatformTextureSettings();
            defaults.maxTextureSize = 256;
            defaults.format = TextureImporterFormat.Automatic;
            defaults.textureCompression = TextureImporterCompression.CompressedHQ;
            defaults.compressionQuality = 100;
            defaults.crunchedCompression = false;
            importer.SetPlatformTextureSettings(defaults);
            foreach (var platform in new[] { "Standalone", "Android", "iPhone" })
                importer.ClearPlatformTextureSettings(platform);
        }
    }
}
