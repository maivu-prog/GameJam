using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class DirectReskinPlayCapture
{
    const string PendingKey = "RustyFishing.DirectReskinCapture.Pending";
    static int frames;
    static int stage;
    static string logFolder;

    [InitializeOnLoadMethod]
    static void Initialize()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (SessionState.GetBool(PendingKey, false) && EditorApplication.isPlaying)
            EditorApplication.delayCall += BeginPlayCapture;
    }

    public static void CaptureBatch()
    {
        logFolder = Path.GetFullPath("Logs/direct-reskin-play");
        Directory.CreateDirectory(logFolder);
        SessionState.SetBool(PendingKey, true);
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.isPlaying = true;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            BeginPlayCapture();
        }
        else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(PendingKey, false))
        {
            SessionState.SetBool(PendingKey, false);
            EditorApplication.Exit(0);
        }
    }

    static void BeginPlayCapture()
    {
        if (!SessionState.GetBool(PendingKey, false) || !EditorApplication.isPlaying) return;
        logFolder = Path.GetFullPath("Logs/direct-reskin-play");
        Directory.CreateDirectory(logFolder);
        Screen.SetResolution(540, 960, false);
        Application.targetFrameRate = 60;
        frames = 0;
        stage = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        frames++;
        if (frames < 120) return;
        frames = 0;

        switch (stage)
        {
            case 0:
                Capture("01-title.png");
                stage = 1;
                break;
            case 1:
                InvokeButton("CONTINUE", "NEW GAME", "START");
                stage = 2;
                break;
            case 2:
                Capture("02-harbor.png");
                stage = 3;
                break;
            case 3:
                InvokeButton("SET SAIL");
                stage = 4;
                break;
            case 4:
                Capture("03-sea.png");
                stage = 5;
                break;
            default:
                EditorApplication.update -= Tick;
                EditorApplication.isPlaying = false;
                break;
        }
    }

    static void Capture(string fileName)
    {
        var path = Path.Combine(logFolder, fileName);
        const int width = 540;
        const int height = 960;
        var cameraObject = new GameObject("DirectReskinCaptureCamera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        var renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = renderTexture;
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var modes = canvases.Select(canvas => canvas.renderMode).ToArray();
        var cameras = canvases.Select(canvas => canvas.worldCamera).ToArray();
        var planeDistances = canvases.Select(canvas => canvas.planeDistance).ToArray();
        for (var i = 0; i < canvases.Length; i++)
        {
            canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
            canvases[i].worldCamera = camera;
            canvases[i].planeDistance = 1f;
        }

        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());

        for (var i = 0; i < canvases.Length; i++)
        {
            canvases[i].renderMode = modes[i];
            canvases[i].worldCamera = cameras[i];
            canvases[i].planeDistance = planeDistances[i];
        }
        RenderTexture.active = previous;
        camera.targetTexture = null;
        RenderTexture.ReleaseTemporary(renderTexture);
        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        Debug.Log("Captured direct-reskin play screen: " + path);
    }

    static void InvokeButton(params string[] labels)
    {
        foreach (var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var label = button.GetComponentInChildren<TMP_Text>(true)?.text?.Trim();
            if (label == null || !labels.Any(value => label.Contains(value, StringComparison.OrdinalIgnoreCase))) continue;
            button.onClick.Invoke();
            Debug.Log("Invoked UI button for capture: " + label);
            return;
        }
        Debug.LogWarning("Could not find capture button: " + string.Join(", ", labels));
    }
}
