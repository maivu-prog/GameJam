using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RustyFishing
{
    /// <summary>
    /// In-game tuning tool for the gameplay field — a live slider panel modelled on the
    /// joystick-demo mockup. Builds its own top-most overlay canvas, so it is independent of
    /// the baked game canvas. Sliders mutate the static tuning fields on GameCatalog live.
    /// Tap the gear button (top-left) to show/hide. "LOG" prints the current values to the
    /// Console so you can paste them back into GameCatalog as new defaults.
    /// </summary>
    public sealed class TuningPanel : MonoBehaviour
    {
        sealed class Spec
        {
            public string name; public float min, max; public Func<float> get; public Action<float> set;
            public Text valueLabel; public Slider slider;
        }

        readonly List<Spec> specs = new();
        readonly Dictionary<Spec, float> defaults = new();
        GameObject panel;
        Font Ui; // set in Start — GetBuiltinResource must NOT be called from a field initializer

        void Start()
        {
            Ui = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildSpecs();
            foreach (var s in specs) defaults[s] = s.get();
            BuildUi();
        }

        void Update()
        {
            // Keyboard fallback (Editor / desktop): press T to toggle the panel.
            if (panel != null && Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
                panel.SetActive(!panel.activeSelf);
        }

        void BuildSpecs()
        {
            Add("sinkBase", .2f, 5, () => GameCatalog.HookSink, v => GameCatalog.HookSink = v);
            Add("sinkMax", 2, 8, () => GameCatalog.HookSinkMax, v => GameCatalog.HookSinkMax = v);
            Add("upForce", 2, 16, () => GameCatalog.HookUpForce, v => GameCatalog.HookUpForce = v);
            Add("riseMax", .4f, 5, () => GameCatalog.HookRiseMax, v => GameCatalog.HookRiseMax = v);
            Add("upDrag", .1f, 1, () => GameCatalog.HookUpDrag, v => GameCatalog.HookUpDrag = v);
            Add("horizontal", 1, 14, () => GameCatalog.HookHorizontal, v => GameCatalog.HookHorizontal = v);
            Add("retract", 2, 24, () => GameCatalog.HookRetract, v => GameCatalog.HookRetract = v);
            Add("lineSeconds", 3, 30, () => GameCatalog.HookLineSeconds, v => GameCatalog.HookLineSeconds = v);
            Add("maxDepthU", 10, 40, () => GameCatalog.HookMaxDepthUnits, v => GameCatalog.HookMaxDepthUnits = v);
            Add("worldScroll", 16, 64, () => GameCatalog.WorldScrollPpu, v => GameCatalog.WorldScrollPpu = v);
            Add("fishSwim", 10, 64, () => GameCatalog.FishSwimPpu, v => GameCatalog.FishSwimPpu = v);
            Add("fishWander", 0, 60, () => GameCatalog.FishWanderPx, v => GameCatalog.FishWanderPx = v);
            Add("fishSurfaceY", -200, 500, () => GameCatalog.FishSurfaceY, v => GameCatalog.FishSurfaceY = v);
            Add("fishDepthPx", 5, 45, () => GameCatalog.FishDepthPx, v => GameCatalog.FishDepthPx = v);
            Add("fishSize", .4f, 2f, () => GameCatalog.FishSizeScale, v => GameCatalog.FishSizeScale = v);
            Add("dockGap", 18, 70, () => GameCatalog.DockGap, v => { GameCatalog.DockGap = v; GameCatalog.LayoutDocks(); });
            Add("lineWidth", 1, 16, () => GameCatalog.LineWidthPx, v => GameCatalog.LineWidthPx = v);
            Add("spdNeedleStart", -180, 180, () => GameCatalog.SpeedNeedleStart, v => GameCatalog.SpeedNeedleStart = v);
            Add("spdNeedleSweep", 0, 360, () => GameCatalog.SpeedNeedleSweep, v => GameCatalog.SpeedNeedleSweep = v);
            Add("trailMinDist", 4, 40, () => GameCatalog.LineTrailMinDist, v => GameCatalog.LineTrailMinDist = v);
        }

        void Add(string name, float min, float max, Func<float> get, Action<float> set)
            => specs.Add(new Spec { name = name, min = min, max = max, get = get, set = set });

        public Canvas host; // set by FishingGameController to the live GameCanvas (guaranteed to render)

        void BuildUi()
        {
            Transform parent;
            if (host != null)
            {
                parent = host.transform; // attach to the working game canvas
            }
            else
            {
                var canvas = new GameObject("TuningCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5000; // always on top
                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = .5f;
                parent = canvas.transform;
            }
            Debug.Log("[TuningPanel] BuildUi — host=" + (host != null ? host.name : "own canvas"));

            // Toggle button — pinned to the top-left CORNER (anchor 0,1) so it shows on any aspect ratio.
            var toggle=MakeButton(parent, "⚙ TUNE", Vector2.zero, new Vector2(240, 104), () => panel.SetActive(!panel.activeSelf));
            toggle.image.color=new Color(.95f,.62f,.18f,1f);
            var trt=(RectTransform)toggle.transform;
            trt.anchorMin=trt.anchorMax=new Vector2(0,1);trt.pivot=new Vector2(0,1);trt.anchoredPosition=new Vector2(24,-24);
            trt.SetAsLastSibling();

            // Panel background.
            panel = NewRect(parent, "Panel", new Vector2(-250, -40), new Vector2(600, 1740)).gameObject;
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(.05f, .09f, .11f, .92f);

            float y = 830;
            MakeLabel(panel.transform, "GAME FIELD TUNING", new Vector2(0, y), 34, TextAnchor.MiddleCenter);
            y -= 70;
            foreach (var s in specs)
            {
                MakeRow(panel.transform, s, y);
                y -= 78; // tightened so the full (growing) list + buttons stay on-screen
            }
            MakeButton(panel.transform, "RESET", new Vector2(-140, y - 6), new Vector2(240, 74), ResetAll);
            MakeButton(panel.transform, "LOG", new Vector2(140, y - 6), new Vector2(240, 74), LogValues);
            // Wipe saved progression (coins/day/upgrades/cargo/hull) and restart at Home Harbor.
            var wipe = MakeButton(panel.transform, "RESET SAVE", new Vector2(0, y - 92), new Vector2(300, 74), ResetSave);
            wipe.image.color = new Color(.62f, .2f, .2f, .96f);

            panel.SetActive(false);
            trt.SetAsLastSibling(); // keep the toggle clickable on top of the panel
        }

        void MakeRow(Transform parent, Spec s, float y)
        {
            var label = MakeLabel(parent, s.name, new Vector2(-160, y + 26), 26, TextAnchor.MiddleLeft);
            label.rectTransform.sizeDelta = new Vector2(300, 40);
            s.valueLabel = MakeLabel(parent, s.get().ToString("0.##"), new Vector2(190, y + 26), 26, TextAnchor.MiddleRight);
            s.valueLabel.rectTransform.sizeDelta = new Vector2(180, 40);
            s.slider = MakeSlider(parent, new Vector2(0, y - 16), new Vector2(540, 40), s.min, s.max, s.get());
            s.slider.onValueChanged.AddListener(v => { s.set(v); s.valueLabel.text = v.ToString("0.##"); });
        }

        void ResetAll()
        {
            // Just move the sliders back to defaults (onValueChanged applies the value + label). No destroy.
            foreach (var s in specs)
                if (s.slider) s.slider.value = defaults[s];
        }

        void ResetSave()
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<FishingGameController>();
            if (controller != null) controller.ResetProgression();
            else Debug.LogWarning("[TuningPanel] No FishingGameController found — cannot reset progression.");
        }

        void LogValues()
        {
            var sb = new StringBuilder("=== Game field tuning ===\n");
            foreach (var s in specs) sb.Append(s.name).Append(" = ").Append(s.get().ToString("0.###")).Append('\n');
            Debug.Log(sb.ToString());
        }

        // ---- tiny uGUI builders ----------------------------------------------------------
        RectTransform NewRect(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        Text MakeLabel(Transform parent, string value, Vector2 pos, int size, TextAnchor align)
        {
            var rt = NewRect(parent, "Label", pos, new Vector2(560, 44));
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Ui; t.text = value; t.fontSize = size; t.alignment = align;
            t.color = new Color(.93f, .9f, .78f);
            return t;
        }

        Image NewImage(Transform parent, string name, Color color)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        Button MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, Action onClick)
        {
            var rt = NewRect(parent, label, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(.72f, .34f, .2f, .96f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            var t = MakeLabel(rt, label, Vector2.zero, 28, TextAnchor.MiddleCenter);
            t.rectTransform.sizeDelta = size;
            return btn;
        }

        Slider MakeSlider(Transform parent, Vector2 pos, Vector2 size, float min, float max, float val)
        {
            var rt = NewRect(parent, "Slider", pos, size);
            var slider = rt.gameObject.AddComponent<Slider>();

            var bg = NewImage(rt, "BG", new Color(1, 1, 1, .16f));
            StretchH(bg.rectTransform, 0, 12);

            var fillArea = NewRect(rt, "Fill Area", Vector2.zero, size);
            StretchH(fillArea, 8, 14);
            var fill = NewImage(fillArea, "Fill", new Color(.94f, .78f, .4f, .9f));
            fill.rectTransform.anchorMin = new Vector2(0, 0);
            fill.rectTransform.anchorMax = new Vector2(1, 1);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

            var handleArea = NewRect(rt, "Handle Slide Area", Vector2.zero, size);
            StretchH(handleArea, 14, size.y);
            var handle = NewImage(handleArea, "Handle", new Color(1, 1, 1, .96f));
            handle.rectTransform.anchorMin = new Vector2(0, 0);
            handle.rectTransform.anchorMax = new Vector2(0, 1);
            handle.rectTransform.sizeDelta = new Vector2(28, 0);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min; slider.maxValue = max; slider.value = val;
            return slider;
        }

        // Stretch horizontally inside the parent with a left/right inset, vertically centred at a fixed height.
        void StretchH(RectTransform rt, float padX, float height)
        {
            rt.anchorMin = new Vector2(0, .5f); rt.anchorMax = new Vector2(1, .5f); rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = new Vector2(padX, -height / 2);
            rt.offsetMax = new Vector2(-padX, height / 2);
        }
    }
}
