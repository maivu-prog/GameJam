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
            //
            // Compressed, not Uncompressed: at RGBA32 the art was 236 MB of a 239 MB build -- 98.6% of
            // everything a player downloads. DXT/BC cuts that about fourfold before Brotli runs. Because
            // this only fires on first import, art already in the project is unaffected; use
            // tools/set_texture_compression.py to change those.
            if(!t.importSettingsMissing)return;
            t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.alphaIsTransparency=true;t.mipmapEnabled=false;t.filterMode=UnityEngine.FilterMode.Point;t.textureCompression=TextureImporterCompression.Compressed;t.maxTextureSize=4096;
        }
    }
}
