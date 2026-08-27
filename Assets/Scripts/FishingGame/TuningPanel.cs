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
            public string name, desc; public float min, max; public Func<float> get; public Action<float> set;
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
            Add("sinkBase", .2f, 5, () => GameCatalog.HookSink, v => GameCatalog.HookSink = v, "Tốc độ chìm cơ bản của lưỡi câu");
            Add("sinkMax", 2, 8, () => GameCatalog.HookSinkMax, v => GameCatalog.HookSinkMax = v, "Tốc độ chìm tối đa khi giữ gạt xuống");
            Add("upForce", 2, 16, () => GameCatalog.HookUpForce, v => GameCatalog.HookUpForce = v, "Lực kéo lưỡi câu lên khi gạt lên");
            Add("riseMax", .4f, 5, () => GameCatalog.HookRiseMax, v => GameCatalog.HookRiseMax = v, "Tốc độ nổi lên tối đa của lưỡi câu");
            Add("upDrag", .1f, 1, () => GameCatalog.HookUpDrag, v => GameCatalog.HookUpDrag = v, "Độ cản khi kéo lên (cao = chậm hơn)");
            Add("horizontal", 1, 14, () => GameCatalog.HookHorizontal, v => GameCatalog.HookHorizontal = v, "Tốc độ di chuyển ngang của lưỡi câu");
            Add("retract", 2, 24, () => GameCatalog.HookRetract, v => GameCatalog.HookRetract = v, "Tốc độ thu lưỡi câu về thuyền");
            Add("fishHit", .15f, .6f, () => GameCatalog.FishHitFraction, v => GameCatalog.FishHitFraction = v, "Vùng ăn damage theo thân cá (tỉ lệ bề ngang). Cao = chạm mép cá đã dính; thấp = phải trúng gần giữa");
            Add("catchReach", 0, 40, () => GameCatalog.HookCatchRadius, v => GameCatalog.HookCatchRadius = v, "Tầm với thêm ngoài thân cá (px). 0 = phải chạm đúng thân");
            Add("hookDamage", 2, 40, () => GameCatalog.HookDamage, v => GameCatalog.HookDamage = v, "Sát thương lưỡi câu mỗi giây (thấp = cá lâu chết, cần cọ lâu hơn)");
            Add("biteMinSpeed", 0, 200, () => GameCatalog.HookBiteMinSpeed, v => GameCatalog.HookBiteMinSpeed = v, "Hook phải di chuyển nhanh hơn mức này (px/s) mới gây damage. 0 = tắt (hook đứng yên cũng ăn); cao = phải cọ mạnh");
            Add("minFishSize", .3f, 1.6f, () => GameCatalog.MinFishSize, v => GameCatalog.MinFishSize = v, "Cỡ nhỏ nhất của cá (species nhỏ hơn sẽ được kéo lên mức này)");
            Add("lineSeconds", 3, 30, () => GameCatalog.HookLineSeconds, v => GameCatalog.HookLineSeconds = v, "Thời gian tối đa mỗi lần thả câu (giây)");
            Add("maxDepthU", 10, 40, () => GameCatalog.HookMaxDepthUnits, v => GameCatalog.HookMaxDepthUnits = v, "Độ sâu tối đa lưỡi câu xuống được");
            Add("worldScroll", 16, 64, () => GameCatalog.WorldScrollPpu, v => GameCatalog.WorldScrollPpu = v, "Tốc độ cuộn cảnh biển theo thuyền (px/đơn vị)");
            Add("fishSwim", 10, 64, () => GameCatalog.FishSwimPpu, v => GameCatalog.FishSwimPpu = v, "Tốc độ bơi ngang của cá");
            Add("fishWander", 0, 60, () => GameCatalog.FishWanderPx, v => GameCatalog.FishWanderPx = v, "Biên độ nhấp nhô lên xuống của cá");
            Add("fishRoam", 40, 500, () => GameCatalog.FishRoamHalfWidthPx, v => GameCatalog.FishRoamHalfWidthPx = v, "Khoảng cá bơi quanh chỗ ở của nó (px)");
            Add("fishCull", 400, 1200, () => GameCatalog.FishCullPx, v => GameCatalog.FishCullPx = v, "Cá ẩn đi khi trôi ra ngoài khoảng này (px)");
            Add("fishDensity", .1f, 1.5f, () => GameCatalog.FishFieldDensity, v => GameCatalog.FishFieldDensity = v, "Mật độ cá trên mỗi đơn vị biển (theo vùng)");
            Add("fishFieldMax", 10, 120, () => GameCatalog.FishFieldMax, v => GameCatalog.FishFieldMax = Mathf.RoundToInt(v), "Số cá tối đa trong cả biển");
            Add("fishSize", .4f, 2f, () => GameCatalog.FishSizeScale, v => GameCatalog.FishSizeScale = v, "Hệ số phóng to/thu nhỏ toàn bộ cá");
            Add("dockGap", 18, 70, () => GameCatalog.DockGap, v => { GameCatalog.DockGap = v; GameCatalog.LayoutDocks(); }, "Khoảng cách nhỏ nhất giữa các cảng");
            Add("dockGapVar", 1, 3.5f, () => GameCatalog.DockGapVarMax, v => { GameCatalog.DockGapVarMax = v; GameCatalog.ReseedDockGaps(); GameCatalog.LayoutDocks(); }, "Độ biến động khoảng cách cảng (1 = đều nhau)");
            Add("portSparse", 0, 40, () => GameCatalog.FishPortSparseRadius, v => GameCatalog.FishPortSparseRadius = v, "Bán kính quanh cảng ít cá (dụ đi xa)");
            Add("portY", -80, 420, () => GameCatalog.PortY, v => GameCatalog.PortY = v, "Đường nước của cảng — kéo để trượt TẤT CẢ cảng lên/xuống (căn theo đáy nên cảng cao/thấp đều nằm một hàng)");
            Add("lineWidth", 1, 60, () => GameCatalog.LineWidthPx, v => GameCatalog.LineWidthPx = v, "Độ dày của dây câu");
            Add("lineSag", 0, 180, () => GameCatalog.LineSagPx, v => GameCatalog.LineSagPx = v, "Fishing-line curve sag");
            Add("hookScale", .5f, 5f, () => GameCatalog.HookScale, v => GameCatalog.HookScale = v, "Kích thước hiển thị của lưỡi câu");
            Add("spdNeedleStart", -180, 180, () => GameCatalog.SpeedNeedleStart, v => GameCatalog.SpeedNeedleStart = v, "Góc bắt đầu của kim đồng hồ tốc độ");
            Add("spdNeedleSweep", 0, 360, () => GameCatalog.SpeedNeedleSweep, v => GameCatalog.SpeedNeedleSweep = v, "Góc quét của kim đồng hồ tốc độ");
            Add("trailMinDist", 4, 40, () => GameCatalog.LineTrailMinDist, v => GameCatalog.LineTrailMinDist = v, "Khoảng cách giữa các điểm vẽ dây câu");
            // One density slider per depth band, then one per zone (data-driven — follows SeaMap).
            for (int bi = 0; bi < SeaMap.Bands.Count; bi++)
            {
                int idx = bi;
                Add("density tầng " + SeaMap.Bands[bi].id, 0f, 2f,
                    () => SeaMap.Bands[idx].densityMul, v => SeaMap.Bands[idx].densityMul = v,
                    "Mật độ cá tầng sâu " + SeaMap.Bands[bi].id);
            }
            for (int zi = 0; zi < SeaMap.Zones.Count; zi++)
            {
                int idx = zi;
                Add("density vùng " + SeaMap.Zones[zi].index, 0f, 2f,
                    () => SeaMap.Zones[idx].densityMul, v => SeaMap.Zones[idx].densityMul = v,
                    "Mật độ cá vùng " + SeaMap.Zones[zi].index);
            }
        }

        void Add(string name, float min, float max, Func<float> get, Action<float> set, string desc = "")
            => specs.Add(new Spec { name = name, desc = desc, min = min, max = max, get = get, set = set });

        public Canvas host; // set by FishingGameController to the live GameCanvas (guaranteed to render)

        void BuildUi()
        {
            Transform parent;
            // Prefer an existing canvas (the explicit host, else whatever the scene already renders with)
            // so the panel never adds a second canvas to a scene that has one.
            if (host == null)
                foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) { host = c; break; }
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

            MakeLabel(panel.transform, "GAME FIELD TUNING", new Vector2(0, 806), 34, TextAnchor.MiddleCenter);

            // The list grows past what fits, and each row now carries a Vietnamese description line, so the
            // rows live in a scroll view between the title and the fixed buttons at the bottom.
            const float rowH = 104f;
            float viewTop = 762f, viewBottom = -716f;      // panel-local y bounds of the scroll region
            float viewH = viewTop - viewBottom;
            var viewport = NewRect(panel.transform, "Viewport", new Vector2(0, (viewTop + viewBottom) / 2), new Vector2(576, viewH));
            viewport.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, .001f); // catches drags, near-invisible
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();

            float contentH = specs.Count * rowH;
            var content = NewRect(viewport, "Content", Vector2.zero, new Vector2(560, contentH));
            content.anchorMin = content.anchorMax = new Vector2(.5f, 1);
            content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            scroll.content = content; scroll.viewport = viewport;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 34;

            for (int i = 0; i < specs.Count; i++)
                MakeRow(content, specs[i], contentH / 2 - rowH / 2 - i * rowH); // yCenter of row i in content-local coords

            // Fixed action buttons pinned to the panel bottom (outside the scroll view).
            MakeButton(panel.transform, "RESET", new Vector2(-140, -762), new Vector2(240, 72), ResetAll);
            MakeButton(panel.transform, "LOG", new Vector2(140, -762), new Vector2(240, 72), LogValues);
            // Wipe saved progression (coins/day/upgrades/cargo/hull) and restart at Home Harbor.
            var wipe = MakeButton(panel.transform, "RESET SAVE", new Vector2(0, -838), new Vector2(300, 72), ResetSave);
            wipe.image.color = new Color(.62f, .2f, .2f, .96f);

            panel.SetActive(false);
            trt.SetAsLastSibling(); // keep the toggle clickable on top of the panel
        }

        // One row: variable name + live value on top, a short Vietnamese description under it, slider below.
        void MakeRow(Transform parent, Spec s, float yCenter)
        {
            var label = MakeLabel(parent, s.name, new Vector2(-165, yCenter + 32), 25, TextAnchor.MiddleLeft);
            label.rectTransform.sizeDelta = new Vector2(320, 36);
            s.valueLabel = MakeLabel(parent, s.get().ToString("0.##"), new Vector2(205, yCenter + 32), 25, TextAnchor.MiddleRight);
            s.valueLabel.rectTransform.sizeDelta = new Vector2(150, 36);
            if (!string.IsNullOrEmpty(s.desc))
            {
                var d = MakeLabel(parent, s.desc, new Vector2(-165, yCenter + 5), 17, TextAnchor.MiddleLeft);
                d.rectTransform.sizeDelta = new Vector2(520, 28);
                d.color = new Color(.62f, .78f, .82f); // dim teal so it reads as a caption
            }
            s.slider = MakeSlider(parent, new Vector2(0, yCenter - 32), new Vector2(540, 36), s.min, s.max, s.get());
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
