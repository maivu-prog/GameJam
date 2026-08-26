using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    /// <summary>
    /// Bubble burst for the hook. Fires whenever the hook LOSES speed against its own recent average —
    /// swinging the joystick from sinking to rising, bottoming out at max depth, hitting the surface
    /// clamp, or the line snapping into retract. Those are the moments the water "grabs" the hook, and a
    /// burst of bubbles is what sells that resistance. Accelerating smoothly never fizzes.
    ///
    /// Two details make it fire when it should. It reads the hook's ACTUAL movement, not the velocity the
    /// joystick asked for, so a stop caused by a clamp counts as much as one the player asked for. And it
    /// compares against a LAGGED velocity, not last frame's: a finger takes several frames to swing the
    /// stick across, so a per-frame delta stays tiny for exactly the move that should throw the most
    /// bubbles, and only instant snaps would ever qualify.
    ///
    /// Bubbles are pooled: the pool is allocated once and entries are recycled, so a burst never
    /// allocates and never spikes the frame.
    /// </summary>
    public sealed class HookBubbles : MonoBehaviour
    {
        [Header("Khi nào nổ bọt")]
        [Tooltip("Lưỡi câu phải MẤT ít nhất bằng này (px/giây) tốc độ thì mới sinh bọt. " +
                 "Hạ xuống = nhạy hơn, ghì nhẹ cũng ra bọt.")]
        [SerializeField] float triggerDelta = 35f;

        [Tooltip("Mức mất tốc coi là mạnh nhất — tới đây thì ra số bọt tối đa.")]
        [SerializeField] float fullDelta = 260f;

        [Tooltip("Độ trễ (giây) của vận tốc tham chiếu. Ngón tay gạt cần từ chìm sang nổi mất vài frame " +
                 "chứ không tức thì, nên phải so với vận tốc trễ này. So với đúng frame trước thì mỗi frame " +
                 "chỉ lệch tí xíu, và chỉ những cú SNAP (giật dây, chạm đáy) mới đủ ngưỡng.")]
        [SerializeField] float velLagSeconds = .16f;

        [Tooltip("Nghỉ tối thiểu giữa 2 lần nổ (giây), để giữ gạt liên tục không phun bọt thành dòng.")]
        [SerializeField] float cooldown = .12f;

        [Tooltip("Bật để in ra Console mức GIẢM TỐC lớn nhất mỗi giây. Dùng khi bọt không ra để biết " +
                 "là do ngưỡng đặt cao hay do không có ai gọi tới.")]
        [SerializeField] bool logBraking;

        /// <summary>Driven by FishingGameController.debugHookBubbles, which IS editable in the scene.</summary>
        public void SetDebug(bool on) => logBraking = on;

        [Header("Số lượng & kích thước")]
        [SerializeField] Vector2Int countRange = new(5, 18);
        [Tooltip("Đường kính bọt (px) — mỗi bọt random trong khoảng này.")]
        [SerializeField] Vector2 sizeRange = new(24f, 78f);
        [SerializeField] int poolSize = 48;

        [Header("Chuyển động")]
        [Tooltip("Lực nổi (px/giây^2) đẩy bọt lên.")]
        [SerializeField] float buoyancy = 220f;
        [Tooltip("Tốc độ nổi tối đa (px/giây).")]
        [SerializeField] float riseMax = 190f;
        [Tooltip("Bọt văng ra bao nhanh lúc mới sinh (px/giây).")]
        [SerializeField] float spreadSpeed = 130f;
        [Tooltip("Độ cản nước — càng cao thì cú văng ban đầu tắt càng nhanh.")]
        [SerializeField] float drag = 2.6f;
        [Tooltip("Biên độ lắc trái phải khi nổi lên (px).")]
        [SerializeField] float wobblePx = 16f;
        [SerializeField] Vector2 lifeRange = new(.8f, 1.8f);

        struct Bubble
        {
            public RectTransform rt;
            public Image img;
            public Vector2 vel, origin;
            public float age, life, size, phase, wobble;
            public bool alive;
        }

        Bubble[] pool;
        int next;
        float lastBurst = -99f;
        Vector2 lagVel;
        float peakBraking, lastLog; int samples, burstsThisSecond;
        int AliveCount(){ int n = 0; foreach (var b in pool) if (b.alive) n++; return n; }

        // The logic is proven; what is left is whether the quad is on screen, sized, opaque and enabled.
        // Report all four for one live bubble rather than guessing which of them is wrong.
        string FirstAliveReport()
        {
            foreach (var b in pool)
            {
                if (!b.alive || b.rt == null) continue;
                var c = b.img != null ? b.img.color : Color.clear;
                return $"pos={b.rt.anchoredPosition} size={b.rt.sizeDelta.x:0} alpha={c.a:0.00} "
                     + $"active={b.rt.gameObject.activeInHierarchy} enabled={(b.img != null && b.img.enabled)} "
                     + $"sprite={(b.img != null && b.img.sprite != null ? b.img.sprite.name : "NULL")} "
                     + $"lossyScale={b.rt.lossyScale.x:0.00}";
            }
            return "khong co bot nao song";
        }
        Color baseColour = Color.white;

        void Awake()
        {
            var sprite = RuntimeUI.Sprite("UI/Gameplay/bubble");
            pool = new Bubble[Mathf.Max(1, poolSize)];
            for (int i = 0; i < pool.Length; i++)
            {
                var rt = RuntimeUI.Rect(transform, "Bubble", Vector2.zero, Vector2.one * 16f);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = sprite;
                img.raycastTarget = false;
                img.preserveAspect = true;
                rt.gameObject.SetActive(false);
                pool[i] = new Bubble { rt = rt, img = img };
            }
        }

        /// <summary>
        /// Hand over the hook's velocity every frame. Bubbles fire when it is LOSING speed against its own
        /// recent average — the water checking it. Calls that do not qualify are ignored, so the caller can
        /// report unconditionally.
        /// </summary>
        public void ReportVelocity(Vector2 at, Vector2 velocity, float dt)
        {
            Vector2 lag = lagVel;
            lagVel = Vector2.Lerp(lagVel, velocity, Mathf.Clamp01(dt / Mathf.Max(.0001f, velLagSeconds)));

            // Project the change onto the direction the hook WAS travelling: positive means it slowed down
            // or turned back, negative means it is still accelerating the same way. Only the former fizzes —
            // sinking faster and faster should not throw bubbles, being stopped by the water should.
            Vector2 change = lag - velocity;
            float braking = lag.sqrMagnitude > 1f ? Vector2.Dot(change, lag.normalized) : change.magnitude;

            peakBraking = Mathf.Max(peakBraking, braking);
            samples++;
            // Left over from tracking down why bursts fired but nothing was visible. Behind the shared
            // debug flag now: it printed every second of every cast, in builds as well as the editor.
            if (Time.unscaledTime - lastLog > 1f)
            {
                if (GameCatalog.debugStorage)
                    Debug.Log($"[HookBubbles] bursts {burstsThisSecond}/s | alive {AliveCount()} | {FirstAliveReport()}");
                lastLog = Time.unscaledTime; peakBraking = 0f; samples = 0; burstsThisSecond = 0;
            }

            if (braking < triggerDelta || Time.unscaledTime - lastBurst < cooldown) return;

            lastBurst = Time.unscaledTime;
            burstsThisSecond++;
            // At a dead stop (depth clamp) the current velocity carries no direction — fall back to the
            // heading the hook was on, so the bubbles still stream off the correct side.
            Vector2 heading = velocity.sqrMagnitude > 1f ? velocity : lag;
            Burst(at, Mathf.InverseLerp(triggerDelta, fullDelta, braking), heading);
        }

        /// <summary>Forget the velocity history (call when a new cast starts).</summary>
        public void ResetVelocity() => lagVel = Vector2.zero;

        /// <summary>Spawn a burst directly. <paramref name="strength"/> is 0..1.</summary>
        public void Burst(Vector2 at, float strength, Vector2 direction)
        {
            int count = Mathf.RoundToInt(Mathf.Lerp(countRange.x, countRange.y, Mathf.Clamp01(strength)));
            // Bubbles are torn off the hook, so they scatter roughly OPPOSITE its travel.
            Vector2 away = direction.sqrMagnitude > .001f ? -direction.normalized : Vector2.down;
            for (int i = 0; i < count; i++) Spawn(at, strength, away);
        }

        void Spawn(Vector2 at, float strength, Vector2 away)
        {
            ref var b = ref pool[next];
            next = (next + 1) % pool.Length;   // oldest bubble is recycled when the pool runs dry

            float spread = Random.Range(-.9f, .9f);
            Vector2 dir = (away + new Vector2(-away.y, away.x) * spread).normalized;

            b.origin = at + Random.insideUnitCircle * 12f;
            b.vel = dir * spreadSpeed * Random.Range(.35f, 1f) * Mathf.Lerp(.6f, 1.25f, strength);
            b.age = 0f;
            b.life = Random.Range(lifeRange.x, lifeRange.y);
            b.size = Random.Range(sizeRange.x, sizeRange.y) * Mathf.Lerp(.75f, 1.15f, strength);
            b.phase = Random.value * 6.28f;
            b.wobble = Random.Range(.4f, 1f) * wobblePx;
            b.alive = true;

            b.rt.anchoredPosition = b.origin;
            b.rt.sizeDelta = Vector2.one * b.size;
            b.rt.gameObject.SetActive(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            for (int i = 0; i < pool.Length; i++)
            {
                ref var b = ref pool[i];
                if (!b.alive) continue;
                b.age += dt;
                if (b.age >= b.life)
                {
                    b.alive = false;
                    b.rt.gameObject.SetActive(false);
                    continue;
                }

                // Water drag kills the initial scatter, then buoyancy takes over and it climbs.
                b.vel -= b.vel * Mathf.Min(1f, drag * dt);
                b.vel.y = Mathf.Min(b.vel.y + buoyancy * dt, riseMax);
                b.origin += b.vel * dt;

                float t = b.age / b.life;
                float sway = Mathf.Sin(b.phase + b.age * 6f) * b.wobble * t;
                b.rt.anchoredPosition = b.origin + new Vector2(sway, 0f);
                b.rt.sizeDelta = Vector2.one * b.size * Mathf.Lerp(.7f, 1.15f, t);   // bubbles expand as they rise

                var c = baseColour;
                c.a = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 6f)) * (1f - t * t);  // quick in, slow out
                b.img.color = c;
            }
        }

        /// <summary>Hide every live bubble at once (used when a cast ends).</summary>
        public void Clear()
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i].alive) continue;
                pool[i].alive = false;
                pool[i].rt.gameObject.SetActive(false);
            }
        }
    }
}
