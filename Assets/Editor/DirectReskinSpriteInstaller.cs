using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RustyFishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DirectReskinSpriteInstaller
{
    const string SourceRoot = "Assets/Resources/Art/ReskinArt";
    const string LegacyRoot = "Assets/Resources/Art/";

    [MenuItem("Rusty Fishing/Art/Apply Direct Reskin Sprites")]
    public static void ApplyFromMenu() => Apply();

    public static void ApplyBatch()
    {
        Apply();
        EditorApplication.Exit(0);
    }

    static void Apply()
    {
        var importerChanges = PrepareSourceImporters();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var missingMappings = ValidateLegacyMappings();
        if (missingMappings.Count > 0)
            throw new InvalidOperationException("Missing direct reskin mappings:\n" + string.Join("\n", missingMappings));

        var prefabChanges = RemapPrefabs();
        var sceneChanges = RemapScenes();
        var assetChanges = RemapScriptableObjects();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var remainingLegacyReferences = FindRemainingLegacyReferences();
        if (remainingLegacyReferences.Count > 0)
            throw new InvalidOperationException("Legacy sprite references remain:\n" + string.Join("\n", remainingLegacyReferences));

        var reportPath = Path.GetFullPath("Logs/direct-reskin-install-report.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllLines(reportPath, new[]
        {
            $"Source importers updated: {importerChanges}",
            $"Prefab sprite references updated: {prefabChanges}",
            $"Scene sprite references updated: {sceneChanges}",
            $"ScriptableObject sprite references updated: {assetChanges}",
            "Missing direct mappings: 0",
            "Remaining legacy sprite references: 0",
        });
        Debug.Log($"Direct reskin sprites applied successfully. Report: {reportPath}");
    }

    static int PrepareSourceImporters()
    {
        var changed = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SourceRoot }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

            var serializedImporter = new SerializedObject(importer);
            var sprites = serializedImporter.FindProperty("m_SpriteSheet.m_Sprites");
            if (sprites == null || sprites.arraySize == 0) continue;

            var dirty = false;
            if (importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
                dirty = true;
            }
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                dirty = true;
            }
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }
            if (!dirty) continue;
            importer.SaveAndReimport();
            changed++;
        }
        return changed;
    }

    static List<string> ValidateLegacyMappings()
    {
        var missing = new List<string>();
        var root = Path.GetFullPath(LegacyRoot);
        foreach (var file in Directory.EnumerateFiles(root, "*.png", SearchOption.AllDirectories))
        {
            var normalized = file.Replace('\\', '/');
            if (normalized.Contains("/ReskinArt/", StringComparison.OrdinalIgnoreCase)) continue;
            var relative = normalized.Substring(root.Replace('\\', '/').Length).TrimStart('/');
            var key = relative.Substring(0, relative.Length - ".png".Length);
            if (!DirectReskinSprites.HasMapping(key) || DirectReskinSprites.Load(key) == null)
                missing.Add(key);
        }
        return missing;
    }

    static int RemapPrefabs()
    {
        var changes = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Prefab" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var localChanges = RemapHierarchy(root);
                if (localChanges == 0) continue;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                changes += localChanges;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        return changes;
    }

    static int RemapScenes()
    {
        var changes = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var localChanges = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                localChanges += RemapHierarchy(root);
                localChanges += NormalizeDirectSceneArt(root);
            }
            if (localChanges > 0) EditorSceneManager.SaveScene(scene);
            changes += localChanges;
        }
        return changes;
    }

    static int NormalizeDirectSceneArt(GameObject root)
    {
        var changes = 0;
        var titleSprite = DirectReskinSprites.Load("rusty-fishing-title-logo");
        foreach (var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {
            if (image.name != "Logo" || image.transform.parent == null ||
                image.transform.parent.name != "TitleScene" || image.sprite == titleSprite) continue;
            image.sprite = titleSprite;
            EditorUtility.SetDirty(image);
            changes++;
        }
        return changes;
    }

    static int RemapScriptableObjects()
    {
        var changes = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Resources" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset == null || asset is GameObject || asset is Component) continue;
                changes += RemapObject(asset);
            }
        }
        return changes;
    }

    static int RemapHierarchy(GameObject root)
    {
        var changes = 0;
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component != null) changes += RemapObject(component);
        }
        return changes;
    }

    static int RemapObject(UnityEngine.Object target)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.GetIterator();
        var enterChildren = true;
        var changes = 0;
        while (property.Next(enterChildren))
        {
            enterChildren = false;
            if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue is not Sprite sprite)
                continue;
            if (!TryLegacyKey(sprite, out var key)) continue;
            var replacement = DirectReskinSprites.Load(key);
            if (replacement == null || replacement == sprite) continue;
            property.objectReferenceValue = replacement;
            changes++;
        }
        if (changes > 0) serialized.ApplyModifiedPropertiesWithoutUndo();
        return changes;
    }

    static bool TryLegacyKey(Sprite sprite, out string key)
    {
        var path = AssetDatabase.GetAssetPath(sprite).Replace('\\', '/');
        if (!path.StartsWith(LegacyRoot, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(SourceRoot + "/", StringComparison.OrdinalIgnoreCase) ||
            !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            key = null;
            return false;
        }
        key = path.Substring(LegacyRoot.Length, path.Length - LegacyRoot.Length - ".png".Length);
        return true;
    }

    static List<string> FindRemainingLegacyReferences()
    {
        var remaining = new HashSet<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Prefab" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                CollectLegacyReferences(root.GetComponentsInChildren<Component>(true), path, remaining);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                CollectLegacyReferences(root.GetComponentsInChildren<Component>(true), path, remaining);
        }
        foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Resources" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            CollectLegacyReferences(AssetDatabase.LoadAllAssetsAtPath(path), path, remaining);
        }
        return remaining.OrderBy(value => value).ToList();
    }

    static void CollectLegacyReferences(IEnumerable<UnityEngine.Object> objects, string ownerPath, ISet<string> remaining)
    {
        foreach (var asset in objects)
        {
            if (asset == null) continue;
            var serialized = new SerializedObject(asset);
            var property = serialized.GetIterator();
            var enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = false;
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue is Sprite sprite && TryLegacyKey(sprite, out var key))
                    remaining.Add(ownerPath + " -> " + key);
            }
        }
    }
}
