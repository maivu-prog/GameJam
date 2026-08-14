using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace RustyFishing.Editor
{
    public static class FishingEditableSceneBuilder
    {
        [MenuItem("Rusty Fishing/Rebuild Editable UI")]
        public static void RebuildEditableUI()
        {
            var scene=SceneManager.GetActiveScene();
            foreach(var old in Object.FindObjectsByType<FishingGameController>(FindObjectsSortMode.None))Object.DestroyImmediate(old.gameObject);
            foreach(var old in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))if(old.name=="GameCanvas")Object.DestroyImmediate(old.gameObject);
            foreach(var old in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))Object.DestroyImmediate(old.gameObject);
            var root=new GameObject("FishingGameController");
            var controller=root.AddComponent<FishingGameController>();
            controller.BuildEditableHierarchy();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/SampleScene.unity");
            Selection.activeGameObject=root;
            Debug.Log("Editable GameCanvas baked into SampleScene. All RectTransforms are now editable in the Inspector.");
        }

        public static void RebuildFromBatch()=>RebuildEditableUI();

        [MenuItem("Rusty Fishing/Repair Art References %#j")]
        public static void RepairArtReferences()
        {
            var scene=SceneManager.GetActiveScene();
            var controller=Object.FindFirstObjectByType<FishingGameController>();
            if(controller==null)return;
            controller.RepairArtReferences();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Rusty Fishing art references repaired.");
        }
    }
}
