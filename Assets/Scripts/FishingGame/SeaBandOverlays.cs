using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    /// <summary>Art for one depth band. Drop a sprite in and it fills that band's slice of the water.</summary>
    [Serializable] public sealed class BandOverlay
    {
        [Tooltip("Id tầng trong SeaMap: A, B hoặc C.")]
        public string band = "A";

        [Tooltip("Kéo sprite vào đây. Để trống thì tầng này chỉ tô màu Tint (hoặc không vẽ gì nếu alpha = 0).")]
        public Sprite sprite;

        [Tooltip("Bật để lặp sprite theo lưới thay vì kéo giãn cho vừa dải.")]
        public bool tiled;

        [Tooltip("Multiply = tối đi theo màu nền, hợp để phân tầng nước. Tắt = vẽ đè bình thường.")]
        public bool multiply = true;

        public Color tint = Color.white;

        [Range(0f, 1f)] public float alpha = 1f;

        [Tooltip("Nới dải ra (px) ở mép trên / mép dưới, để hai tầng cạnh nhau chồng mép cho mềm.")]
        public float padTopPx = 0f, padBottomPx = 0f;

        [Tooltip("Bỏ tick để ẩn tầng này.")]
        public bool show = true;

        [Header("Cuộn ngang")]
        [Range(0f, 2f)]
        [Tooltip("Tốc độ cuộn so với thế giới. 1 = trôi đúng bằng thuyền, 0 = dán cứng vào màn hình, " +
                 "nhỏ hơn 1 = tầng nước như ở xa hơn.")]
        public float scrollFactor = 1f;

        [Tooltip("Bề ngang (px) của MỘT bản copy. Phải >= bề ngang canvas (1080) vì mỗi tầng chỉ vẽ 2 bản.")]
        public float tileWidth = 1080f;

        [Range(-40f, 40f)]
        [Tooltip("Trôi thêm px/giây kể cả khi thuyền đứng yên — dòng chảy ngầm.")]
        public float drift = 0f;
    }

    /// <summary>
    /// Draws one image per depth band (A / B / C), sized to that band's slice of the water column, so the
    /// three bands read as distinct water rather than as one gradient with fish at different heights.
    ///
    /// The slices track GameCatalog.DepthPx, which the hull tier drives — so when ascending pulls the view
    /// back, the bands re-lay themselves against the new ruler instead of staying where they were drawn.
    ///
    /// Multiply is the default blend: these sit ON the parallax backdrop, and darkening it keeps the painted
    /// water visible through the band. Plain alpha would cover it up.
    /// </summary>
    public sealed class SeaBandOverlays : MonoBehaviour
    {
        [Tooltip("Một dòng cho mỗi tầng. Thứ tự trong list = thứ tự vẽ.")]
        [SerializeField] List<BandOverlay> overlays = Defaults();

        [Tooltip("Trần độ sâu (unit) dùng cho tầng cuối, để tầng C không kéo dài vô tận xuống dưới màn hình.")]
        [SerializeField] float bottomClampU = 48f;

        sealed class Strip { public BandOverlay def; public RectTransform group; public Image[] tiles; }
        readonly List<Strip> strips = new();
        const int Copies = 2;   // two copies always cover +-tileWidth/2 around the boat
        float scrollX;
        Material multiplyMaterial;
        string builtSignature;

        public static List<BandOverlay> Defaults() => new()
        {
            new BandOverlay { band = "A", tint = new Color(1f, 1f, 1f), alpha = 0f },
            new BandOverlay { band = "B", tint = Color.white, alpha = .55f },
            new BandOverlay { band = "C", tint = Color.white, alpha = .8f },
        };

        static Sprite LoadDefaultSprite(string band)
        {
            if (string.Equals(band, "B", StringComparison.OrdinalIgnoreCase))
                return DirectReskinSprites.Load("World/BandOverlays/band-b-overlay");
            if (string.Equals(band, "C", StringComparison.OrdinalIgnoreCase))
                return DirectReskinSprites.Load("World/BandOverlays/band-c-overlay");
            return null;
        }

        string Signature()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var o in overlays) sb.Append(o?.band).Append(o?.multiply).Append(o?.tiled).Append('|');
            return sb.ToString();
        }

        void OnEnable() { Build(); }

        [ContextMenu("Rebuild band overlays")]
        public void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var go = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            }
            strips.Clear();
            foreach (var def in overlays)
            {
                if (def == null) continue;
                if (def.sprite == null)
                {
                    def.sprite = LoadDefaultSprite(def.band);
                    if (def.sprite != null) def.tint = Color.white;
                }
                // A band is a strip of WATER, not a decal on the lens: it has to scroll past with the sea.
                // Two copies laid end to end and wrapped by a modulo give an endless run, the same trick
                // SeaParallax uses for the backdrop.
                var group = RuntimeUI.Rect(transform, "Band-" + def.band, Vector2.zero, new Vector2(0, 100));
                group.gameObject.hideFlags = HideFlags.DontSave;
                var strip = new Strip { def = def, group = group, tiles = new Image[Copies] };
                for (int i = 0; i < Copies; i++)
                {
                    var rt = RuntimeUI.Rect(group, "Tile", new Vector2(i * def.tileWidth, 0),
                                            new Vector2(def.tileWidth, 100));
                    rt.gameObject.hideFlags = HideFlags.DontSave;
                    var img = rt.gameObject.AddComponent<Image>();
                    img.raycastTarget = false;
                    img.preserveAspect = false;
                    img.type = def.tiled ? Image.Type.Tiled : Image.Type.Simple;
                    if (def.multiply) img.material = MultiplyMaterial();
                    strip.tiles[i] = img;
                }
                strips.Add(strip);
            }
            builtSignature = Signature();
            Apply();
        }

        Material MultiplyMaterial()
        {
            if (multiplyMaterial != null) return multiplyMaterial;
            var shader = Resources.Load<Shader>("Shaders/UIMultiply");
            if (shader == null)
            {
                Debug.LogWarning("[SeaBandOverlays] Resources/Shaders/UIMultiply not found — falling back to normal blend.");
                return null;
            }
            multiplyMaterial = new Material(shader) { name = "UIMultiply (runtime)", hideFlags = HideFlags.DontSave };
            return multiplyMaterial;
        }

        void LateUpdate()
        {
            if (builtSignature == null) return;
            if (builtSignature != Signature()) { Build(); return; }
            Apply();
        }

        /// <summary>Tell the bands where the boat is, in sea units. Called from PlaceWorldArt.</summary>
        public void SetScroll(float worldX) => scrollX = worldX;

        void Apply()
        {
            float px = Mathf.Max(1f, GameCatalog.DepthPx);
            float world = scrollX * GameCatalog.WorldScrollPpu;
            float time = Application.isPlaying ? Time.time : 0f;

            for (int i = 0; i < strips.Count && i < overlays.Count; i++)
            {
                var strip = strips[i];
                var def = overlays[i];
                if (strip == null || def == null || strip.group == null) continue;

                var band = FindBand(def.band);
                bool draw = def.show && band != null && def.alpha > 0f;
                if (strip.group.gameObject.activeSelf != draw) strip.group.gameObject.SetActive(draw);
                if (!draw) continue;

                // The scene only stores band settings. B/C art is resolved from Resources so replacing an
                // overlay PNG does not require re-wiring the BandOverlays GameObject in every scene.
                if (def.sprite == null)
                {
                    def.sprite = LoadDefaultSprite(def.band);
                    if (def.sprite != null) def.tint = Color.white;
                }

                // Depth -> screen, on the same ruler the hook and the fish use.
                float bottomU = Mathf.Min(band.bottomU, bottomClampU);
                float topY = SeaMap.HookRestY - band.topU * px + def.padTopPx;
                float botY = SeaMap.HookRestY - bottomU * px - def.padBottomPx;
                float h = Mathf.Max(1f, topY - botY);
                strip.group.anchoredPosition = new Vector2(0, (topY + botY) * .5f);
                strip.group.sizeDelta = new Vector2(0, h);

                float tileW = Mathf.Max(1f, def.tileWidth);
                float span = tileW * Copies;
                float offset = world * def.scrollFactor - time * def.drift;
                var c = def.tint;
                c.a = def.alpha;

                for (int k = 0; k < strip.tiles.Length; k++)
                {
                    var img = strip.tiles[k];
                    if (img == null) continue;
                    // Same wrap as SeaParallax: the copies stay tileW apart modulo span, so together they
                    // always tile a contiguous span-wide window centred on the boat.
                    float x = Mathf.Repeat(k * tileW - offset + span * .5f, span) - span * .5f;
                    var rt = img.rectTransform;
                    rt.anchoredPosition = new Vector2(x, 0f);
                    rt.sizeDelta = new Vector2(tileW, h);
                    img.sprite = def.sprite;
                    // With no sprite an Image still fills its rect with a white quad, which is exactly the
                    // flat colour wash we want for a band the artist has not drawn yet.
                    if (img.color != c) img.color = c;
                    if (img.material != null) img.material.SetFloat("_Strength", 1f);
                }
            }
        }

        static BandDef FindBand(string id)
        {
            foreach (var b in SeaMap.Bands)
                if (string.Equals(b.id, id, StringComparison.OrdinalIgnoreCase)) return b;
            return null;
        }
    }
}
