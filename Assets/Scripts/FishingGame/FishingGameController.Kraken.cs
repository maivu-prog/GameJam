using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    public sealed partial class FishingGameController
    {
        const float KrakenFadeSeconds = 1.25f;
        const float KrakenAttackSeconds = .72f;

        static readonly string[] KrakenAttackArt =
        {
            "World/Kraken/kraken-attack-thrust",
            "World/Kraken/kraken-attack-swipe",
            "World/Kraken/kraken-attack-wrap",
            "World/Kraken/kraken-attack-slam",
        };

        void SetupKrakenVisuals()
        {
            if (parallax != null)
            {
                parallax.EnsureKrakenLayers();
                parallax.SetKrakenIntensity(0f);
            }
            if (world == null) return;

            krakenAttackSprites = new Sprite[KrakenAttackArt.Length];
            for (int i = 0; i < KrakenAttackArt.Length; i++)
                krakenAttackSprites[i] = RuntimeUI.Sprite(KrakenAttackArt[i]);

            var rt = RuntimeUI.Rect(world, "KrakenAttackFx", Vector2.zero, new Vector2(1200, 1500));
            krakenAttackFx = rt.gameObject.AddComponent<Image>();
            krakenAttackFx.raycastTarget = false;
            krakenAttackFx.preserveAspect = true;
            krakenAttackFx.gameObject.SetActive(false);
            rt.SetAsLastSibling();       // over boat/fish art, still below the sea-screen HUD
        }

        void TickKrakenVisuals(float dt)
        {
            bool hunting = false;
            for (int i = 0; i < fish.Count; i++)
            {
                var f = fish[i];
                if (f != null && !f.Leaving && f.Def.id == "kraken" && f.Hunting)
                {
                    hunting = true;
                    break;
                }
            }

            float target = IsNight && hunting ? 1f : 0f;
            krakenLayerBlend = Mathf.MoveTowards(krakenLayerBlend, target, dt / KrakenFadeSeconds);
            if (parallax != null) parallax.SetKrakenIntensity(krakenLayerBlend);

            if (krakenAttackFx == null || !krakenAttackFx.gameObject.activeSelf) return;
            krakenAttackTime += dt;
            float t = Mathf.Clamp01(krakenAttackTime / KrakenAttackSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            float alpha = Mathf.Sin(t * Mathf.PI);
            var c = Color.white;
            c.a = alpha;
            krakenAttackFx.color = c;

            var rt = krakenAttackFx.rectTransform;
            Vector2 start, end, size;
            float rotation;
            switch (krakenAttackPose)
            {
                case 0: start = new Vector2(0, -620); end = new Vector2(0, -80); size = new Vector2(980, 1500); rotation = 0; break;
                case 1: start = new Vector2(-520, 80); end = new Vector2(280, 80); size = new Vector2(1550, 1050); rotation = 0; break;
                case 2: start = new Vector2(0, -40); end = new Vector2(0, 40); size = new Vector2(1220, 1220); rotation = 0; break;
                default:start = new Vector2(320, 620); end = new Vector2(-40, 80); size = new Vector2(1020, 1520); rotation = 0; break;
            }
            rt.anchoredPosition = Vector2.LerpUnclamped(start, end, eased);
            rt.sizeDelta = size;
            rt.localEulerAngles = new Vector3(0, 0, rotation);
            float sx = krakenAttackFlip ? -1f : 1f;
            float scale = Mathf.Lerp(.82f, 1.08f, eased);
            rt.localScale = new Vector3(sx * scale, scale, 1f);
            if (t >= 1f) krakenAttackFx.gameObject.SetActive(false);
        }

        void PlayKrakenAttack()
        {
            if (krakenAttackFx == null || krakenAttackSprites == null || krakenAttackSprites.Length == 0) return;
            krakenAttackPose = Random.Range(0, krakenAttackSprites.Length);
            krakenAttackFlip = Random.value < .5f;
            krakenAttackFx.sprite = krakenAttackSprites[krakenAttackPose];
            krakenAttackTime = 0f;
            krakenAttackFx.color = new Color(1, 1, 1, 0);
            krakenAttackFx.gameObject.SetActive(true);
            krakenAttackFx.rectTransform.SetAsLastSibling();
        }

        void HideKrakenVisualsImmediate()
        {
            krakenLayerBlend = 0f;
            if (parallax != null) parallax.SetKrakenIntensity(0f);
            if (krakenAttackFx != null) krakenAttackFx.gameObject.SetActive(false);
        }
    }
}
