using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    /// <summary>One scrolling backdrop layer. Every field is live-tunable from the Inspector.</summary>
    [Serializable]
    public sealed class ParallaxLayer
    {
        [Tooltip("Đường dẫn trong Resources/Art (không có .png). VD: World/Parallax/shore-near")]
        public string art = "World/Parallax/world-base";

        [Tooltip("Bỏ tick để tắt lớp này — cách nhanh nhất để soi xem từng lớp đang vẽ cái gì.")]
        public bool show = true;

        [Range(0f, 2f)]
        [Tooltip("Tốc độ trôi so với thế giới. 0 = dán vào màn hình (xa vô tận), 1 = trôi đúng bằng thuyền, " +
                 ">1 = nằm TRƯỚC thuyền nên vượt qua nhanh hơn.")]
        public float factor = .5f;

        [Range(0f, 1f)]
        [Tooltip("Độ mờ của lớp.")]
        public float alpha = 1f;

        [Tooltip("Màu nhuộm. Tối hơn / ngả xanh = cảm giác ở xa hơn. Kênh alpha của màu bị bỏ qua — " +
                 "dùng ô Alpha ở trên.")]
        public Color tint = Color.white;

        [Range(-40f, 40f)]
        [Tooltip("Trôi thêm bao nhiêu px/giây kể cả khi thuyền đứng yên (gió, dòng chảy). Âm = trôi ngược.")]
        public float drift = 0f;

        [Header("Căn khung hình")]
        [Range(-600f, 600f)]
        [Tooltip("Dịch CẢ lớp lên/xuống (px). Đây là ô để sửa khi mặt nước của lớp này không khớp với " +
                 "các lớp khác. Dương = lên, âm = xuống. Không đổi chiều cao.")]
        public float yOffset = 0f;

        [Tooltip("Mép TRÊN của lớp, theo y màn hình: đỉnh = 960, mặt nước = 250, đáy = -960. " +
                 "Chỉ đụng vào khi cần kéo giãn/thu gọn lớp theo chiều dọc.")]
        public float top = 960f;

        [Tooltip("Mép DƯỚI của lớp, theo y màn hình. Phải nhỏ hơn Top.")]
        public float bottom = -960f;

        public float Height => Mathf.Max(1f, top - bottom);
    }

    /// <summary>
    /// The sea screen's scrolling backdrop. Replaces the single painted `Sea` image with the layers in
    /// Resources/Art/World/Parallax, each scrolling at its own fraction of the world speed so the scene
    /// gains depth as the boat moves.
    ///
    /// Layers above the water line move slower the further away they are (sky ~ still, shore ~ a fifth
    /// speed); layers below it are ordered the same way, except the underwater foreground moves FASTER
    /// than the world (factor > 1) because it sits in front of the boat's depth plane.
    ///
    /// Every layer texture tiles horizontally, so each one is drawn as two copies laid end to end and
    /// wrapped with a modulo — no matter how far the boat sails, there is never an edge.
    ///
    /// TUNING: run  Rusty Fishing > Create Parallax Backdrop In Scene  once. That puts a real "Parallax"
    /// object under SeaScreen, and because this component runs with [ExecuteAlways] the layers render in
    /// the Scene/Game view straight away — edit the list in Edit mode and the values are saved with the
    /// scene. Speed, opacity, tint, framing and the per-layer show toggle apply on the next frame;
    /// changing an art path or adding/removing a layer rebuilds automatically.
    ///
    /// Without that scene object FishingGameController still creates one at run time from
    /// DefaultLayers(), but then Unity throws the edits away on Stop — use the "Log layers" context menu
    /// to print them in a form you can paste back into DefaultLayers().
    ///
    /// The per-layer tile GameObjects are generated, never authored: they carry HideFlags.DontSave so
    /// the scene file only ever stores the layer list, not the strips built from it.
    /// </summary>
    [ExecuteAlways]
    public sealed class SeaParallax : MonoBehaviour
    {
        // Screen y of the painted water line, shared by every source frame. GameCatalog.FishSurfaceY
        // starts the fish field at the same height — move one, move both.
        public const float WaterlineY = 250f;

        [Tooltip("Bề ngang (px màn hình) của MỘT bản copy. Mỗi lớp chỉ vẽ 2 bản rồi lặp vòng, nên giá trị " +
                 "này phải >= bề ngang canvas (1080). Nguồn là ảnh dọc 1080x1920 nên 1080 là tỉ lệ gốc.")]
        [SerializeField] float tileWidth = 1080f;

        [Tooltip("Xếp từ SAU ra TRƯỚC: lớp đầu danh sách vẽ trước và bị các lớp sau đè lên.")]
        [SerializeField] List<ParallaxLayer> layers = DefaultLayers();

        [Tooltip("Nới thêm bao nhiêu px ra ngoài mép trên/dưới màn hình. Overscan tự động đã lo phần màn " +
                 "hình cao hơn tỉ lệ chuẩn rồi; ô này chỉ để chèn thêm nếu vẫn thấy hở.")]
        [SerializeField] float extraOverscanPx = 0f;

        [Range(0f, 200f)]
        [Tooltip("CHỈ dùng khi KHÔNG Play: giả lập thuyền đang ở đâu (đơn vị biển) để xem các lớp lệch nhau " +
                 "thế nào ngay trong Edit mode. Vào Play là giá trị này bị bỏ qua, lấy vị trí thuyền thật.")]
        [SerializeField] float previewBoatX;

        const int Copies = 2;   // two copies always cover +-tileWidth/2 around the boat (see SetScroll)

        sealed class Strip
        {
            public ParallaxLayer def;
            public RectTransform group;
            public Image[] tiles;
        }

        readonly List<Strip> strips = new();
        string builtSignature;   // the art paths + tileWidth the current strips were built from
        float scrollX;

        // Back to front. All sources share the same waterline and full-frame coordinates, so they can be
        // overlaid without per-layer cropping or vertical stretching.
        public static List<ParallaxLayer> DefaultLayers() => new()
        {
            Make("World/Parallax/world-base",              0f),
            Make("World/Parallax/horizon-far",           .06f, drift: -2f),
            Make("World/Parallax/shore-near",            .22f),
            Make("World/Parallax/water-surface",         .45f, drift: 3f),
            Make("World/Parallax/underwater-mid",        .35f, alpha: .72f, drift: 5f),
            Make("World/Parallax/underwater-foreground",1.15f, drift: 8f),
        };

        static ParallaxLayer Make(string art, float factor, float alpha = 1f, float drift = 0f)
            => new() { art = art, factor = factor, alpha = alpha, drift = drift };

        const string KrakenFarArt = "World/Kraken/kraken-far";
        const string KrakenNearArt = "World/Kraken/kraken-near-tentacles";

        /// <summary>Add the two Kraken-only strips without requiring every authored scene to be rebuilt.</summary>
        public void EnsureKrakenLayers()
        {
            bool changed = false;
            if (FindLayer(KrakenFarArt) == null)
            {
                var far = Make(KrakenFarArt, .025f, alpha: 0f, drift: .35f);
                int foreground = layers.FindIndex(x => x != null && x.art == "World/Parallax/underwater-foreground");
                layers.Insert(foreground >= 0 ? foreground : layers.Count, far);
                changed = true;
            }
            if (FindLayer(KrakenNearArt) == null)
            {
                layers.Add(Make(KrakenNearArt, 1.2f, alpha: 0f, drift: 6f));
                changed = true;
            }
            if (changed) Build();
        }

        /// <summary>Fade the encounter layers without rebuilding their generated Image strips.</summary>
        public void SetKrakenIntensity(float amount)
        {
            amount = Mathf.Clamp01(amount);
            var far = FindLayer(KrakenFarArt);
            if (far != null) { far.show = amount > .001f; far.alpha = .62f * amount; }
            var near = FindLayer(KrakenNearArt);
            if (near != null) { near.show = amount > .001f; near.alpha = .72f * amount; }
        }

        ParallaxLayer FindLayer(string art)
            => layers.Find(x => x != null && string.Equals(x.art, art, StringComparison.Ordinal));

        // Rebuilding is only needed when the set of sprites changes; everything else is applied per frame.
        string Signature()
        {
            var sb = new StringBuilder().Append(tileWidth).Append('|');
            foreach (var l in layers) sb.Append(l?.art).Append(',');
            return sb.ToString();
        }

        void OnEnable()
        {
#if UNITY_EDITOR
            // Build() uses DestroyImmediate outside Play mode, which Unity refuses to run while it is
            // still loading/deserialising — defer a frame so scene load and recompiles stay quiet.
            if (!Application.isPlaying) { UnityEditor.EditorApplication.delayCall += EditorBuild; return; }
#endif
            Build();
        }

#if UNITY_EDITOR
        void EditorBuild() { if (this != null && !Application.isPlaying) Build(); }
#endif

        void OnValidate()
        {
            // A row added with the list's "+" button can serialise to an all-zero colour, which would
            // silently render the layer black. Treat that as "no tint".
            if (layers != null)
                foreach (var l in layers)
                    if (l != null && l.tint == default) l.tint = Color.white;
            // Fewer than two screen widths of tile cannot cover the screen at every scroll offset.
            tileWidth = Mathf.Max(1080f, tileWidth);
        }

        [ContextMenu("Rebuild layers")]
        public void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var go = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            }
            strips.Clear();
            foreach (var def in layers)
            {
                if (def == null || string.IsNullOrEmpty(def.art)) continue;
                var sprite = RuntimeUI.Sprite(def.art);
                if (sprite == null) continue;   // RuntimeUI already logged it; skip rather than NRE
                var group = RuntimeUI.Rect(transform, "Layer-" + def.art, Vector2.zero, new Vector2(0, def.Height));
                group.gameObject.hideFlags = HideFlags.DontSave;
                var strip = new Strip { def = def, group = group, tiles = new Image[Copies] };
                for (int i = 0; i < Copies; i++)
                {
                    var rt = RuntimeUI.Rect(group, "Tile", new Vector2(i * tileWidth, 0), new Vector2(tileWidth, def.Height));
                    rt.gameObject.hideFlags = HideFlags.DontSave;
                    var img = rt.gameObject.AddComponent<Image>();
                    img.sprite = sprite;
                    img.preserveAspect = false;   // the layers are deliberately stretched to their band
                    img.raycastTarget = false;
                    strip.tiles[i] = img;
                }
                strips.Add(strip);
            }
            builtSignature = Signature();
            Apply();
        }

        /// <summary>Tell the backdrop where the boat is, in sea units. Cheap — the layers are placed in
        /// LateUpdate, not here.</summary>
        public void SetScroll(float worldX) => scrollX = worldX;

        // Driving the placement from here rather than from the controller's TickBoat means the layers
        // keep updating on the harbor screen and while a modal is open (TickBoat is skipped in both), so
        // the drift animation never freezes and Inspector edits always show up straight away.
        void LateUpdate()
        {
            if (builtSignature == null) return;                        // Build() has not run yet
            if (builtSignature != Signature()) { Build(); return; }    // art path / layer set changed
            Apply();
        }

        // A layer whose band reaches the edge of the 1080x1920 reference frame is meant to be full-bleed.
        // On a screen taller than 16:9 the canvas hands us more than 1920 units of height, so those layers
        // have to be stretched to the REAL rect or they leave the empty bars at the top and bottom that a
        // hand-set localScale was being used to paper over.
        const float FullFrameEdgeY = 950f;

        void Apply()
        {
            float span = tileWidth * Copies;
            float halfH = Mathf.Max(960f, ((RectTransform)transform).rect.height * .5f) + extraOverscanPx;
            float world = (Application.isPlaying ? scrollX : previewBoatX) * GameCatalog.WorldScrollPpu;
            float time = Application.isPlaying ? Time.time : 0f;
            foreach (var strip in strips)
            {
                var def = strip.def;
                // Re-applied every frame, so dragging a field in the Inspector shows up immediately.
                if (strip.group.gameObject.activeSelf != def.show) strip.group.gameObject.SetActive(def.show);
                if (!def.show) continue;
                float top = def.top >= FullFrameEdgeY ? halfH : def.top;
                float bottom = def.bottom <= -FullFrameEdgeY ? -halfH : def.bottom;
                float h = Mathf.Max(1f, top - bottom);
                strip.group.anchoredPosition = new Vector2(0, (top + bottom) * .5f + def.yOffset);
                strip.group.sizeDelta = new Vector2(0, h);

                float offset = world * def.factor - time * def.drift;
                var colour = def.tint;
                colour.a = def.alpha;   // the tint's own alpha is ignored; Alpha is the single opacity knob
                for (int i = 0; i < strip.tiles.Length; i++)
                {
                    // Lay the copies end to end, then wrap the run into one span centred on the boat. The
                    // copies stay exactly tileWidth apart modulo span, so they always tile a contiguous
                    // span-wide window — which covers at least +-tileWidth/2 whatever the offset is.
                    float x = Mathf.Repeat(i * tileWidth - offset + span * .5f, span) - span * .5f;
                    var img = strip.tiles[i];
                    var rt = img.rectTransform;
                    rt.anchoredPosition = new Vector2(x, 0);
                    rt.sizeDelta = new Vector2(tileWidth, h);
                    if (img.color != colour) img.color = colour;
                }
            }
        }

        // Unity throws Play-mode edits away. Print them in the shape of DefaultLayers() so a setup you
        // liked can be pasted back in as the new defaults.
        [ContextMenu("Log layers (paste into DefaultLayers)")]
        public void LogLayers()
        {
            string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "f";
            var sb = new StringBuilder("=== SeaParallax (tileWidth ").Append(F(tileWidth)).Append(") ===\n");
            foreach (var l in layers)
            {
                if (l == null) continue;
                sb.Append($"Make(\"{l.art}\", {F(l.factor)}, alpha: {F(l.alpha)}, drift: {F(l.drift)}),");
                if (!l.show) sb.Append("   // đang tắt");
                if (l.yOffset != 0f) sb.Append($"   // yOffset {F(l.yOffset)}");
                if (l.top != 960f || l.bottom != -960f) sb.Append($"   // top {F(l.top)} bottom {F(l.bottom)}");
                sb.Append('\n');
            }
            Debug.Log(sb.ToString(), this);
        }

        [ContextMenu("Reset to default layers")]
        public void ResetLayers() { layers = DefaultLayers(); Build(); }
    }
}
