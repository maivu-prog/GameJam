using UnityEditor;

namespace RustyFishing.Editor
{
    public sealed class FishingAssetImporter:AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.Contains("Assets/Resources/Art/"))return;
            var t=(TextureImporter)assetImporter;
            // Only apply defaults on the FIRST import (no .meta yet). After that, respect manual changes
            // (e.g. switching Sprite Mode to Multiple to slice a spritesheet) instead of forcing them back.
            if(!t.importSettingsMissing)return;
            t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.alphaIsTransparency=true;t.mipmapEnabled=false;t.filterMode=UnityEngine.FilterMode.Bilinear;t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=4096;
        }
    }
}
