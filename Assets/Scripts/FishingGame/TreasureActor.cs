using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    /// <summary>
    /// A treasure resting on the seabed. Unlike a fish it does not swim — it sits at a fixed world spot and
    /// scrolls past with the boat, and is caught with the SAME hook-drain mechanic (the controller drives the
    /// hit test). Three types (0 bag / 1 pearl / 2 chest) differ in HP and reward, set by the controller.
    /// </summary>
    public sealed class TreasureActor : MonoBehaviour
    {
        public int Type { get; private set; }
        public float Hp { get; private set; }
        public float HomeX { get; private set; }
        public float DepthU { get; private set; }
        public RectTransform Rect { get; private set; }
        public bool Visible => image != null && image.enabled;

        float maxHp, hitFlashT;
        Image image;
        RectTransform hpBar, hpFillRt; Image hpFill;
        bool collecting; Vector2 collectStart, collectTarget; float collectT;

        public void Init(int type, string art, float homeX, float y, float width, float hp, float depthU)
        {
            Type = type; HomeX = homeX; DepthU = depthU; Hp = maxHp = hp;
            Rect = (RectTransform)transform; Rect.name = "Treasure-" + type;
            Rect.anchoredPosition = new Vector2(0, y);
            Rect.sizeDelta = new Vector2(width, width);
            image = Rect.gameObject.AddComponent<Image>();
            image.sprite = RuntimeUI.Sprite(art);
            image.preserveAspect = true; image.raycastTarget = false;
            BuildHpBar(width);
        }

        void BuildHpBar(float w)
        {
            float hw = w * .7f, hh = Mathf.Clamp(hw * .16f, 12f, 20f);
            var barGO = new GameObject("HpBar", typeof(RectTransform));
            hpBar = barGO.GetComponent<RectTransform>(); hpBar.SetParent(Rect, false);
            hpBar.anchorMin = hpBar.anchorMax = hpBar.pivot = new Vector2(.5f, .5f);
            hpBar.anchoredPosition = new Vector2(0, w * .5f + hh); hpBar.sizeDelta = new Vector2(hw, hh);
            var bg = barGO.AddComponent<Image>(); bg.color = new Color(0, 0, 0, .6f); bg.raycastTarget = false;
            var fillGO = new GameObject("Fill", typeof(RectTransform));
            hpFillRt = fillGO.GetComponent<RectTransform>(); hpFillRt.SetParent(hpBar, false);
            hpFillRt.anchorMin = new Vector2(0, 0); hpFillRt.anchorMax = new Vector2(1, 1);
            hpFillRt.offsetMin = hpFillRt.offsetMax = Vector2.zero;
            hpFill = fillGO.AddComponent<Image>(); hpFill.color = new Color(1f, .84f, .35f); hpFill.raycastTarget = false;
            hpFill.type = Image.Type.Filled; hpFill.fillMethod = Image.FillMethod.Horizontal; hpFill.fillOrigin = 0; hpFill.fillAmount = 1f;
            hpBar.gameObject.SetActive(false);
        }

        // localY = the on-screen depth line for this treasure's DepthU (the controller recomputes it so a hull
        // upgrade that rescales depth keeps the treasure on the seabed).
        public void Tick(float boatX, float localY, Vector2? hook)
        {
            if (collecting) return;
            float sx = (HomeX - boatX) * GameCatalog.WorldScrollPpu;
            Rect.anchoredPosition = new Vector2(sx, localY);
            bool vis = Mathf.Abs(sx) < GameCatalog.FishCullPx;
            if (image != null && image.enabled != vis) image.enabled = vis;
            if (hitFlashT > 0f)
            {
                hitFlashT -= Time.deltaTime;
                float k = Mathf.Clamp01(hitFlashT / 0.16f);
                if (image != null) image.color = Color.Lerp(Color.white, new Color(1f, .95f, .55f), k);
            }
            if (hpBar != null)
            {
                bool hookNear = hook.HasValue && Vector2.Distance(new Vector2(sx, localY), hook.Value) < Rect.sizeDelta.x * .6f + 90f;
                bool show = vis && (Hp < maxHp - .01f || hookNear);
                if (hpBar.gameObject.activeSelf != show) hpBar.gameObject.SetActive(show);
                if (show) hpFill.fillAmount = Mathf.Clamp01(Hp / maxHp);
            }
        }

        public bool Hit(float amount) { if (collecting) return false; Hp -= amount; if (amount > 0) hitFlashT = 0.16f; return Hp <= 0; }

        public void Collect(Vector2 targetLocal)
        {
            collecting = true; collectStart = Rect.anchoredPosition; collectTarget = targetLocal; collectT = 0f;
            if (hpBar != null) hpBar.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!collecting) return;
            collectT += Time.deltaTime;
            float k = Mathf.Clamp01(collectT / GameCatalog.CollectSeconds);
            Rect.anchoredPosition = Vector2.LerpUnclamped(collectStart, collectTarget, k * k);
            Rect.localScale = Vector3.one * (1f - k);
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
