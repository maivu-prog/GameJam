using UnityEngine;

namespace RustyFishing
{
    /// <summary>
    /// Idle float for the title logo — it drifts up and down on a sine so the front screen never reads as
    /// a still image. Drop it on the logo object itself; nothing else needs wiring.
    ///
    /// Works on a UI RectTransform (amplitude in anchored pixels) or a plain Transform / SpriteRenderer
    /// (amplitude in world units) — it picks whichever the object actually is.
    ///
    /// Two details that matter because the title screen is toggled with SetActive, not destroyed. The rest
    /// pose is captured ONCE in Awake, so hiding the menu mid-drift and showing it again cannot bake the
    /// current offset in as the new base and walk the logo off screen over a session. And OnDisable puts
    /// the pose back, so whatever the scene author placed is what a screenshot or an inactive prefab shows.
    ///
    /// Runs on unscaled time by default: a menu should keep breathing even if something has parked
    /// Time.timeScale at 0.
    ///
    /// ⚠️ A LayoutGroup or ContentSizeFitter on the SAME object will overwrite the position every layout
    /// pass and the logo will sit dead still. Put the layout on a parent and this on the child.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TitleLogoHover : MonoBehaviour
    {
        [Header("Nhịp trôi")]
        [Tooltip("Trôi lên/xuống bao xa tính từ vị trí gốc. UI thì đơn vị là pixel, sprite ngoài thế giới " +
                 "thì là world unit. Logo title cỡ 10–20 px là vừa — quá tay thành nhún nhảy chứ không phải lơ lửng.")]
        [SerializeField] float amplitude = 14f;

        [Tooltip("Một vòng lên-xuống-về-chỗ-cũ mất bao nhiêu giây. Càng lớn càng chậm rãi, nặng nề. " +
                 "Dưới 1s bắt đầu trông bồn chồn.")]
        [SerializeField] float period = 2.4f;

        [Tooltip("Lệch pha lúc bắt đầu (0..1 = một vòng). Chỉ cần khi có NHIỀU thứ cùng trôi — đặt mỗi cái " +
                 "một giá trị khác nhau để chúng không lên xuống đồng loạt trông như máy móc.")]
        [Range(0f, 1f)]
        [SerializeField] float phaseOffset;

        [Header("Thêm sức sống (tuỳ chọn — để 0 là tắt)")]
        [Tooltip("Nghiêng qua lại bao nhiêu độ. Rất nhỏ thôi: 1–2 độ là đủ để hết cảm giác cứng đờ. " +
                 "Cố ý lệch pha 1/4 vòng so với nhịp trôi, nên nó nghiêng mạnh nhất lúc đang trôi nhanh nhất — " +
                 "giống vật đang bồng bềnh, không phải bập bênh.")]
        [SerializeField] float tiltDegrees;

        [Tooltip("Phình/co theo nhịp, tính theo phần trăm (0.02 = ±2%). To nhất ở đỉnh, nhỏ nhất ở đáy — " +
                 "đọc ra như đang tiến lại gần rồi lùi ra xa.")]
        [SerializeField] float scalePulse;

        [Header("Khác")]
        [Tooltip("Bật: chạy theo thời gian thật, nên vẫn trôi kể cả khi game đang pause (timeScale = 0). " +
                 "Tắt: đứng yên theo game. Menu thì gần như luôn muốn bật.")]
        [SerializeField] bool useUnscaledTime = true;

        RectTransform rect;          // null when this is a world-space object rather than UI
        Vector2 baseAnchored;
        Vector3 basePosition, baseScale;
        Quaternion baseRotation;
        float time;

        void Awake()
        {
            rect = transform as RectTransform;
            if (rect != null) baseAnchored = rect.anchoredPosition;
            else basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            baseScale = transform.localScale;
        }

        // Start every showing from the rest pose. The logo then eases UP out of exactly where the scene
        // author put it, instead of snapping to some mid-drift offset the moment the menu opens.
        void OnEnable() => time = 0f;

        void OnDisable() => Restore();

        void Update()
        {
            if (period <= 0f) return;

            time += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float wave = Mathf.Sin((time / period + phaseOffset) * Mathf.PI * 2f);

            float offset = wave * amplitude;
            if (rect != null) rect.anchoredPosition = baseAnchored + new Vector2(0f, offset);
            else transform.localPosition = basePosition + new Vector3(0f, offset, 0f);

            // Quarter-cycle lead: cos peaks where sin is climbing fastest, so the tilt reads as the logo
            // leaning into the drift rather than rocking in lockstep with it.
            if (!Mathf.Approximately(tiltDegrees, 0f))
            {
                float lean = Mathf.Cos((time / period + phaseOffset) * Mathf.PI * 2f) * tiltDegrees;
                transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, lean);
            }

            if (!Mathf.Approximately(scalePulse, 0f))
                transform.localScale = baseScale * (1f + wave * scalePulse);
        }

        /// <summary>Snap back to the pose captured at Awake. Called on disable; also handy from a cutscene.</summary>
        public void Restore()
        {
            if (rect != null) rect.anchoredPosition = baseAnchored;
            else transform.localPosition = basePosition;
            transform.localRotation = baseRotation;
            transform.localScale = baseScale;
        }

        /// <summary>
        /// Re-read the current pose as the new rest pose. Only needed if something MOVES the logo on
        /// purpose after Awake — a layout pass, a tween that repositions the menu, a resolution change.
        /// </summary>
        public void Recapture()
        {
            if (rect != null) baseAnchored = rect.anchoredPosition;
            else basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            baseScale = transform.localScale;
        }
    }
}
