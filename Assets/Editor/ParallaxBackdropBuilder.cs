using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace RustyFishing.Editor
{
    /// <summary>
    /// Puts a real "Parallax" GameObject into the scene under SeaScreen so the backdrop layers can be
    /// tuned in Edit mode and the values survive leaving Play mode. Without it FishingGameController
    /// creates the same object at run time from SeaParallax.DefaultLayers(), which works but throws away
    /// every edit when you press Stop.
    /// </summary>
    public static class ParallaxBackdropBuilder
    {
        const string ObjectName = "Parallax";

        [MenuItem("Rusty Fishing/Create Parallax Backdrop In Scene")]
        public static void CreateInScene()
        {
            var controller = Object.FindFirstObjectByType<FishingGameController>(FindObjectsInactive.Include);
            var sea = FindSeaScreen();
            if (sea == null)
            {
                EditorUtility.DisplayDialog("Parallax", "No 'SeaScreen' found under a Canvas in this scene.", "OK");
                return;
            }

            var existing = sea.Find(ObjectName);
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Parallax",
                        "SeaScreen already has a 'Parallax' object. Replace it with a fresh one at default values?",
                        "Replace", "Cancel"))
                {
                    Selection.activeGameObject = existing.gameObject;
                    return;
                }
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            var go = new GameObject(ObjectName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create Parallax Backdrop");
            var rt = (RectTransform)go.transform;
            rt.SetParent(sea, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // Sit exactly where the flat painted backdrop was: backmost, behind World and NightShade.
            var flat = sea.Find("Sea");
            rt.SetSiblingIndex(flat != null ? flat.GetSiblingIndex() : 0);
            if (flat != null && flat.gameObject.activeSelf)
            {
                Undo.RecordObject(flat.gameObject, "Disable flat backdrop");
                flat.gameObject.SetActive(false);   // the layers replace it; kept around as a fallback
            }

            var parallax = Undo.AddComponent<SeaParallax>(go);
            parallax.Build();

            var scene = SceneManager.GetActiveScene();
            EditorUtility.SetDirty(parallax);
            if (controller != null) EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = go;
            Debug.Log("Parallax backdrop added to SeaScreen. Tune it in the Inspector — values are now saved " +
                      "with the scene. Save the scene to keep them.", parallax);
        }

        [MenuItem("Rusty Fishing/Create Sea Band Overlays In Scene")]
        public static void CreateBandOverlays()
        {
            var sea = FindSeaScreen();
            if (sea == null)
            {
                EditorUtility.DisplayDialog("Band overlays", "No 'SeaScreen' found under a Canvas in this scene.", "OK");
                return;
            }
            var existing = sea.Find("BandOverlays");
            if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }

            var go = new GameObject("BandOverlays", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create Sea Band Overlays");
            var rt = (RectTransform)go.transform;
            rt.SetParent(sea, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var parallax = sea.Find("Parallax");
            rt.SetSiblingIndex(parallax != null ? parallax.GetSiblingIndex() + 1 : 0);

            var comp = Undo.AddComponent<SeaBandOverlays>(go);
            comp.Build();
            EditorUtility.SetDirty(comp);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = go;
            Debug.Log("Band overlays added to SeaScreen. Drop a sprite into each band's slot in the Inspector.", comp);
        }

        [MenuItem("Rusty Fishing/Create Sleep Button In Scene")]
        public static void CreateSleepButton()
        {
            var harbor = FindNamed("HarborScreen");
            var controller = Object.FindFirstObjectByType<FishingGameController>(FindObjectsInactive.Include);
            if (harbor == null || controller == null)
            {
                EditorUtility.DisplayDialog("Sleep button",
                    "Need a 'HarborScreen' under a Canvas and a FishingGameController in the scene.", "OK");
                return;
            }

            var existing = harbor.Find("SleepBtn");
            if (existing == null)
            {
                var go = new GameObject("SleepBtn", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Create Sleep Button");
                var rt = (RectTransform)go.transform;
                rt.SetParent(harbor, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
                rt.anchoredPosition = new Vector2(0, -655);
                rt.sizeDelta = new Vector2(660, 120);

                var img = go.AddComponent<Image>();
                img.sprite = DirectReskinSprites.Load("UI/Harbor/primary-button");
                img.type = Image.Type.Simple;
                var btn = go.AddComponent<Button>();
                btn.targetGraphic = img;

                var labelGO = new GameObject("Text", typeof(RectTransform));
                var lrt = (RectTransform)labelGO.transform;
                lrt.SetParent(rt, false);
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = lrt.offsetMax = Vector2.zero;
                var label = labelGO.AddComponent<TMPro.TextMeshProUGUI>();
                label.text = "SLEEP UNTIL DAWN";
                label.fontSize = 44;
                label.alignment = TMPro.TextAlignmentOptions.Center;
                label.color = new Color(.94f, .9f, .76f);
                label.raycastTarget = false;

                existing = rt;
            }

            // Assign it to the controller's serialized slot so no manual dragging is needed.
            var so = new SerializedObject(controller);
            var prop = so.FindProperty("sleepButton");
            if (prop != null)
            {
                prop.objectReferenceValue = existing.GetComponent<Button>();
                so.ApplyModifiedProperties();
            }
            existing.gameObject.SetActive(false);   // the controller shows it at night

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("SleepBtn created under HarborScreen and wired to FishingGameController.sleepButton. " +
                      "Restyle it however you like — the code only binds the click.", existing.gameObject);
        }

        [MenuItem("Rusty Fishing/Create Message Banner In Scene")]
        public static void CreateMessageBanner()
        {
            var controller = Object.FindFirstObjectByType<FishingGameController>(FindObjectsInactive.Include);
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (controller == null || canvas == null)
            {
                EditorUtility.DisplayDialog("Message banner",
                    "Need a Canvas and a FishingGameController in the scene.", "OK");
                return;
            }

            // On the CANVAS, not inside HarborScreen or SeaScreen: the wreck notice fires at sea and the
            // shipyard notices fire in port, and a banner parented to either would be hidden for half of
            // what it has to say.
            // Two objects, not one: the plate is the parent and the label is its child. The game switches
            // the PARENT off when there is nothing to say, so the backing art leaves with the words --
            // clearing the text on a single object would leave an empty plate sitting there.
            var banner = FindNamed("MessageBanner");
            TMPro.TMP_Text label;
            if (banner == null)
            {
                var go = new GameObject("MessageBanner", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Create Message Banner");
                var rt = (RectTransform)go.transform;
                rt.SetParent(canvas.transform, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
                rt.anchoredPosition = new Vector2(0, 250);
                rt.sizeDelta = new Vector2(900, 130);

                var plate = go.AddComponent<Image>();
                plate.sprite = DirectReskinSprites.Load("UI/Harbor/small-card");
                plate.type = Image.Type.Simple;
                plate.preserveAspect = false;
                plate.raycastTarget = false;   // it floats over the harbour buttons; it must not eat taps

                var labelGO = new GameObject("Text", typeof(RectTransform));
                var lrt = (RectTransform)labelGO.transform;
                lrt.SetParent(rt, false);
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                // Inset so long lines do not run into the edge of the plate art.
                lrt.offsetMin = new Vector2(46, 22);
                lrt.offsetMax = new Vector2(-46, -22);

                var tmp = labelGO.AddComponent<TMPro.TextMeshProUGUI>();
                tmp.text = "";
                tmp.enableAutoSizing = true;   // "Sold for 40 coins." and "No shipyard here. Try Midway
                tmp.fontSizeMin = 22;          // Anchorage." are wildly different lengths
                tmp.fontSizeMax = 42;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.color = new Color(.96f, .91f, .78f);
                tmp.raycastTarget = false;

                banner = rt;
                label = tmp;
            }
            else
            {
                label = banner.GetComponentInChildren<TMPro.TMP_Text>(true);
            }

            // One slot: the controller finds the label under the banner itself.
            var so = new SerializedObject(controller);
            var prop = so.FindProperty("messageBanner");
            if (prop != null) prop.objectReferenceValue = banner.gameObject;
            so.ApplyModifiedProperties();

            banner.gameObject.SetActive(false);   // the game shows it when there is something to say

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = banner.gameObject;
            Debug.Log("MessageBanner created on the Canvas: plate + child label, wired to "
                    + "FishingGameController.messageBanner. Restyle it freely — the code finds the text "
                    + "underneath, and hides the whole banner a few seconds later.", banner.gameObject);
        }

        static Transform FindNamed(string name)
        {
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas) continue;
                foreach (var rt in c.GetComponentsInChildren<RectTransform>(true))
                    if (rt.name == name) return rt;
            }
            return null;
        }

        static Transform FindSeaScreen() => FindNamed("SeaScreen");
    }
}
