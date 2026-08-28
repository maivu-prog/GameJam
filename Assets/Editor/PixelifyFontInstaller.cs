using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace RustyFishing.Editor
{
    /// <summary>Creates the project's Pixelify Sans TMP asset from the bundled OFL font.</summary>
    public static class PixelifyFontInstaller
    {
        public const string SourceFontPath = "Assets/Resources/Fonts/PixelifySans/PixelifySans-Bold.ttf";
        public const string FontAssetPath = "Assets/Resources/Fonts/PixelifySans/Pixelify Sans Bold SDF.asset";

        [InitializeOnLoadMethod]
        static void InstallOnceWhenEditorRefreshes()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath) == null)
                EditorApplication.delayCall += Install;
        }

        [MenuItem("Rusty Fishing/Art/Rebuild Pixelify Sans TMP Font")]
        public static void Install()
        {
            AssetDatabase.ImportAsset(SourceFontPath, ImportAssetOptions.ForceSynchronousImport);
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
                throw new System.InvalidOperationException($"Could not import source font: {SourceFontPath}");

            // Rebuilding is intentional and idempotent: the source TTF remains the canonical input.
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath) != null)
                AssetDatabase.DeleteAsset(FontAssetPath);

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                source,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
                throw new System.InvalidOperationException("TMP failed to create the Pixelify Sans font asset.");

            fontAsset.name = "Pixelify Sans Bold SDF";
            fontAsset.atlasTextures[0].name = "Pixelify Sans Bold SDF Atlas";
            fontAsset.material.name = "Pixelify Sans Bold SDF Material";

            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(
                "Assets/TextMesh Pro/Resources/TMP Settings.asset");
            if (settings != null)
            {
                var serializedSettings = new SerializedObject(settings);
                serializedSettings.FindProperty("m_defaultFontAsset").objectReferenceValue = fontAsset;
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Pixelify Sans TMP font created: {FontAssetPath} (guid {AssetDatabase.AssetPathToGUID(FontAssetPath)})");
        }
    }
}
