using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    // FishingGameController is split across several partial files by feature (same class, so every
    // serialized reference stays intact and the baked scene needs no changes):
    //   .cs (this)   — serialized fields, runtime state, Awake/Update
    //   .Build.cs    — canvas/UI construction (editor bake) + runtime button binding
    //   .Harbor.cs   — harbor screen, market, economy, upgrades, basket-full inventory toss
    //   .Sailing.cs  — day/night clock, boat movement, obstacle collisions, hit FX
    //   .Fishing.cs  — hook cast/retract, fishing line, joystick knob, fish spawn/AI
    //   .Hud.cs      — sea HUD readouts
    [RequireComponent(typeof(TuningInspector))]   // keeps the Inspector tuning component on this GameObject (Edit mode too)
    public sealed partial class FishingGameController : MonoBehaviour
    {
        enum Mode { Harbor, Sailing, Fishing, Night }

        // ────────────────────────────────────────────────────────────────────────────────
        //  Inspector references. Everything below "Canvas Roots" through "Controls" is
        //  AUTO-WIRED by  Menu ▸ Rusty Fishing ▸ Rebuild Editable UI  — you normally don't
        //  touch those. Only "Fish Size" and "Zone Board" are meant for hand-tuning.
        //  NOTE on headers: keep each [Header] on a SINGLE-field line; a [Header] placed on a
        //  comma list (a, b, c) repeats itself before every field and clutters the Inspector.
        // ────────────────────────────────────────────────────────────────────────────────
        [Header("Canvas Roots  (auto-wired — don't edit)")]
        [SerializeField] Canvas canvas;
        [SerializeField] RectTransform harbor, sea, world, fishLayer, hook, speedNeedle, safeNeedle, clockNeedle;

        [Header("Art  (auto-wired — don't edit)")]
        [SerializeField] Image boat;
        [SerializeField] Image line, nightShade, monster;
        [SerializeField] List<Image> portArt = new(), obstacleArt = new();
        readonly List<Image> portHalos = new();   // night safe-zone glow, one per harbour


        [Header("HUD Text  (auto-wired — don't edit)")]
        [Tooltip("Arrival banner: shows the port name on the sea screen ~2s then fades when you reach a dock.")]
        [SerializeField] TMP_Text harborZone;
        [SerializeField] TMP_Text seaCargo, seaCoins, hp, speed, market, harborName; // clock is needle-only; safe/danger is the board

        [Tooltip("Dải thông báo: kéo cả GameObject vào đây (nền + chữ). Game bật/tắt CẢ CỤM này, " +
                 "nên nền không nằm trơ lại khi hết chữ, và tự tìm TMP_Text bên trong — không cần " +
                 "kéo riêng dòng chữ. Menu Rusty Fishing ▸ Create Message Banner In Scene tạo sẵn một cái.")]
        [SerializeField] GameObject messageBanner;

        [Header("Controls  (auto-wired — don't edit)")]
        [SerializeField] HoldControl left;
        [SerializeField] HoldControl right, joystick;
        [SerializeField] Button fishButton, dockButton, restButton;
        [Tooltip("The joystick's moving knob (drag your 'knob' GameObject). If empty, a knob is spawned at runtime.")]
        [SerializeField] RectTransform joystickKnob;

        [Header("Action Buttons  (drag refs — no name matching)")]
        [Tooltip("Drag each button here. SetSail / Repair / Upgrade / Settings / Utility. Leave any you don't have empty.")]
        [SerializeField] Button sailButton;
        [SerializeField] Button repairButton;      // the REPAIR button (FixBtn) on the Fix-Ship panel
        [SerializeField] Button upgradeButton;     // the button on the Upgrade panel → opens the upgrade-parts modal
        [SerializeField] Button settingsButton;    // the Menu button → opens the settings modal
        [SerializeField] Button utilityButton;     // context button above the joystick (docks at a port)
        [Tooltip("The number text on the Fix-Ship panel (ResourceCount) — shows the live repair fee.")]
        [SerializeField] TMP_Text repairFeeText;
        [Tooltip("Nút SLEEP trên màn cảng, chỉ hiện vào ban đêm. Kéo GameObject vào đây " +
                 "(Rusty Fishing ▸ Create Sleep Button In Scene tạo sẵn một cái).")]
        [SerializeField] Button sleepButton;

        [Header("Storage  (dựng tay trong scene, kéo vào đây)")]
        [Tooltip("Component StoragePanelView — dựng bảng storage trong scene rồi kéo vào đây. " +
                 "Để TRỐNG thì game dùng lại bảng cũ tự sinh lúc chạy, nên tràn khoang vẫn xử lý được " +
                 "trong khi bảng mới còn dang dở.")]
        [SerializeField] StoragePanelView storagePanel;
        [Tooltip("Nút mở khoang cá. Tạo GameObject nút ở đâu tùy bạn rồi kéo vào đây — code chỉ gán sự " +
                 "kiện click. Để TRỐNG thì chỉ mở được khi khoang tràn.")]
        [SerializeField] Button storageButton;
        [Tooltip("Số cá / sức chứa hiện trên nút mở khoang, ví dụ '19/18'. Không bắt buộc.")]
        [SerializeField] TMP_Text storageButtonCount;

        [Tooltip("Component TutorialHintView — dải chữ gợi ý một dòng, dựng tay trong scene. " +
                 "Để TRỐNG thì không có gợi ý nào hiện ra, game vẫn chạy bình thường.")]
        [SerializeField] TutorialHintView hintView;

        [Header("Missions  (dựng tay trong scene, kéo vào đây)")]
        [Tooltip("Component MissionLedgerView — dựng bảng LEDGER trong scene rồi kéo vào đây. " +
                 "Để TRỐNG thì nhiệm vụ vẫn chạy và vẫn trả thưởng, chỉ là không có giao diện nào hiện ra.")]
        [SerializeField] MissionLedgerView missionView;
        [Tooltip("Nút MISSIONS trên màn cảng, mở bảng LEDGER.")]
        [SerializeField] Button missionButton;

        [Header("Title Screen  (dựng tay trong scene, kéo vào đây)")]
        [Tooltip("Cả panel màn hình vào game. Để TRỐNG thì game vào thẳng màn cảng, không có menu.")]
        [SerializeField] RectTransform titleScreen;
        [Tooltip("Nút CONTINUE. Tự ẩn khi save chưa có tiến độ gì.")]
        [SerializeField] Button continueButton;
        [Tooltip("Nút NEW GAME. Xoá save và dựng lại toàn bộ thế giới.")]
        [SerializeField] Button newGameButton;
        [Tooltip("Dòng tóm tắt save (tuỳ chọn) — hiện 'Day 4 · 1250c · Into the Blue'.")]
        [SerializeField] TMP_Text titleSaveLine;

        [Tooltip("Panel Fix-Ship. Ẩn cả panel này ở cảng không có thợ sửa, không chỉ ẩn mỗi nút.")]
        [SerializeField] RectTransform repairPanel;
        [Tooltip("Panel/khối chứa nút UPGRADE trên màn cảng. Ẩn ở cảng không có xưởng đóng tàu.")]
        [SerializeField] RectTransform upgradeEntryPanel;

        [Header("Upgrade Panel  (drag refs)")]
        [Tooltip("The upgrade panel to show/hide. UpgradeButton opens it.")]
        [SerializeField] RectTransform upgradePanel;
        [Tooltip("Optional. The upgrade panel's own coin text. Leave empty to reuse the shared HUD coin instead — both always show the same balance either way.")]
        [SerializeField] TMP_Text upgradeCoins;
        [SerializeField] Button upgradeBackButton;      // "BACK TO HARBOR" (and/or the top-left arrow) → closes the panel
        [Header("Upgrade — part detail card")]
        [SerializeField] TMP_Text upgradePartName;      // "ENGINE II"
        [SerializeField] TMP_Text upgradeStatLabel;     // "SPEED"
        [SerializeField] TMP_Text upgradeCurrentValue;  // "8.0 kn"
        [SerializeField] TMP_Text upgradeNextValue;     // "9.2 kn"
        [SerializeField] Image upgradePartIcon;         // big part art on the detail card (optional)
        [SerializeField] Image[] upgradeLevelDots;      // 4 dots (level 0..3): dot i lit when i <= level
        [SerializeField] Button upgradePrevButton;      // ‹ previous part
        [SerializeField] Button upgradeNextButton;      // › next part
        [SerializeField] Button upgradeBuyButton;       // the "UPGRADE ###" button
        [SerializeField] TMP_Text upgradeBuyLabel;      // its label — becomes "UPGRADE 340" / "MAX"
        [Header("Upgrade — parts on the ship (one block per part; pick which via the 'Part' dropdown — order-independent)")]
        [SerializeField] UpgradePartUI[] upgradeParts;

        [Header("Fish Size  (hand-tuned)")]
        [Tooltip("Fish sprite width (px) = (Base + species.size * Per) * Scale. Lower 'Per' to shrink the big/small gap.")]
        [SerializeField] float fishSizeBase = 200f;
        [SerializeField] float fishSizePer = 80f, fishScale = 1f;

        [Header("Dock Camera  (hand-tuned)")]
        [Tooltip("Zoom lúc cập cảng. 1 = không zoom, 1.9 = phóng to gần gấp đôi.")]
        [SerializeField] float dockZoomScale = 1.9f;
        [Tooltip("Thời gian (giây) cho một lượt zoom vào hoặc lùi ra.")]
        [SerializeField] float dockZoomSeconds = .55f;
        [Range(0f, 1f)]
        [Tooltip("Nền parallax ăn theo bao nhiêu phần của cú zoom. Thấp hơn 1 = nền lùi lại, tạo chiều sâu.")]
        [SerializeField] float dockParallaxShare = .35f;

        [Header("Port Halo  (hand-tuned)")]
        [Tooltip("Tâm quầng sáng theo trục y trên màn hình. Mặt nước = 250, đỉnh = 960, đáy = -960.")]
        [SerializeField] float haloOffsetY = 250f;
        [Tooltip("Nhân bề ngang. 1 = khớp đúng bán kính vùng an toàn thật (6 đơn vị biển mỗi bên).")]
        [SerializeField] float haloWidthMul = 1f;
        [Tooltip("Nhân chiều cao so với bề ngang. Nhỏ hơn 1 = bẹt xuống như vũng sáng trên mặt nước.")]
        [SerializeField] float haloHeightMul = .85f;
        [Range(0f, 1f)]
        [Tooltip("Độ sáng nền của quầng.")]
        [SerializeField] float haloAlpha = .72f;
        [Range(0f, .5f)]
        [Tooltip("Biên độ thở của quầng. 0 = đứng yên.")]
        [SerializeField] float haloPulse = .10f;
        [Tooltip("Màu quầng sáng.")]
        [SerializeField] Color haloTint = Color.white;

        [Header("Cast Timer  (hand-tuned)")]
        [Tooltip("Đường kính vòng đếm giờ quanh cần câu (px). Mặt dial FISH rộng ~384 nên để lớn hơn một chút.")]
        [SerializeField] float castTimerSize = 430f;

        [Header("Debug")]
        [Tooltip("In ra Console trạng thái của hệ bọt khí mỗi lần thả câu, và mức giảm tốc mỗi giây. " +
                 "Bật được từ Edit mode vì nó nằm trên component có sẵn trong scene.")]
        [SerializeField] bool debugHookBubbles;


        [Header("World Layout  (hand-tuned)")]
        [Tooltip("Chiều cao (y) của obstacle trên màn hình. Obstacle được sinh ra lúc chạy nên không kéo trong scene được — chỉnh ở đây. Port thì kéo thẳng từng cái trong scene.")]
        [SerializeField] float obstacleY = 315f;

        [Header("Zone Board  (hand-assigned)")]
        [Tooltip("Board Image; its sprite swaps between Safe/Danger. Safe = docked at a port, Danger = open water.")]
        [SerializeField] Image zoneBoard;
        [SerializeField] Sprite zoneSafeSprite, zoneDangerSprite;
        [Tooltip("Optional TextMeshPro label on the board; its text swaps between the two strings below.")]
        [SerializeField] TMP_Text zoneBoardText;
        [SerializeField] string zoneSafeText = "SAFE", zoneDangerText = "DANGER";

        [Header("Market List  (hand-assigned)")]
        [Tooltip("Row prefab (needs a MarketRow component) instantiated per fish species into 'Market List'. Put a Vertical Layout Group on the list container.")]
        [SerializeField] MarketRow marketRowPrefab;
        [SerializeField] RectTransform marketList;

        [Header("HP Bar  (hand-assigned)")]
        [Tooltip("Fill Image (Image Type = Filled, Horizontal). Its fillAmount tracks ship HP / max HP.")]
        [SerializeField] Image hpFill;
        [Tooltip("Tint the fill green → yellow → red by HP fraction.")]
        [SerializeField] bool hpFillTint = true;

        readonly List<FishActor> fish = new();
        readonly HashSet<string> hitObstacles = new();
        PlayerSave save;
        PortDef currentPort;
        Mode mode;
        float boatX = 6, boatSpeed, phaseTime, worldHour = 6, spawnTimer, hookTime;
        Vector2 hookOffset, hookPrevOffset;   // the delta between them is the hook velocity the bubbles read
        int lastHullTier=-1;            // ascending re-derives the depth scale (see ApplyDepthScale)
        bool wasNight;                  // edge-detect for the dusk/dawn field swap
        PortDef dockingPort;            // set while the dock-in zoom plays; OpenHarbor fires when it lands
        float dockZoom01, dockZoomTarget;   // 0 = sailing view, 1 = pulled right in on the quay
        Vector3 worldBaseScale=Vector3.one, parallaxBaseScale=Vector3.one;   // the scene's own scale, kept intact
        bool baseScalesCaptured;
        float bannerBaseY;              // the banner's resting y, captured before it is ever animated
        bool hookRetracting, joyHeldPrev;
        Image[] lineSegs;
        RectTransform lineRoot;
        readonly List<Vector2> trail = new(), linePts = new();
        readonly List<GameObject> marketRows = new();
        int upgradeIndex;               // which of the 4 parts the upgrade card is showing
        float shakeTime;
        Image hitFlash;                 // collision feedback: screen shake + red flash
        SeaParallax parallax;            // layered scrolling backdrop (replaces the flat Sea image)
        SeaBandOverlays bandOverlays;   // the A/B/C depth-band wash over the backdrop
        Image krakenAttackFx;            // foreground tentacle strike, driven by the unique night hunter
        Sprite[] krakenAttackSprites;
        float krakenLayerBlend, krakenAttackTime;
        int krakenAttackPose;
        bool krakenAttackFlip;
        HookBubbles bubbles;            // bubble burst FX when the hook is checked by the water
        Image castTimerFill, castTimerTrack;   // ring around the joystick: time left on the cast
        bool inventoryOpen;
        bool forcedStorage;             // hold opened by overflow, not by the player
        GameObject inventoryPanel;      // basket-full toss modal
        GameObject modalPanel;          // settings modal (runtime-built stub)
        PortDef bannerPort;             // last port announced by the arrival banner
        float bannerTimer;              // seconds ELAPSED since the arrival banner appeared
        bool bannerPinned;              // in harbour water: the name stays up until you cast off
        const float BannerIn = .5f, BannerHold = 1.9f, BannerOut = .7f;
        const float BannerRisePx = 26f;   // the name drifts up slightly as it fades in, then settles
        // Null-safe text setter, so HUD labels you delete/rearrange in the scene never NRE.
        void Set(TMP_Text t, string s)
        {
            if (t == null) return;
            t.text = s;
            // The message banner is the one label that reports EVENTS rather than state, so it has to
            // expire. Nothing ever cleared it, which left "Sold for 40 coins." sitting over the harbour
            // for the rest of the run.
            if (t != message) return;
            messageUntil = string.IsNullOrEmpty(s) ? 0f : Time.unscaledTime + MessageSeconds;
            ShowMessage(!string.IsNullOrEmpty(s));
        }

        const float MessageSeconds = 4f;
        float messageUntil;
        TMP_Text messageLabel;
        bool messageResolved;

        /// <summary>
        /// The label inside the banner. Only the banner GameObject is wired in the Inspector, and the
        /// text is found underneath it -- one slot to drag instead of two that can disagree.
        ///
        /// Searched with includeInactive, because the banner starts switched off.
        /// </summary>
        TMP_Text message
        {
            get
            {
                if (messageResolved) return messageLabel;
                messageResolved = true;
                if (messageBanner != null) messageLabel = messageBanner.GetComponentInChildren<TMP_Text>(true);
                return messageLabel;
            }
        }

        /// <summary>
        /// Show or hide the whole banner, backing art included. Clearing the text alone is not enough
        /// once there is a plate behind it: an empty panel would sit on screen saying nothing.
        /// </summary>
        void ShowMessage(bool on)
        {
            if (messageBanner != null && messageBanner.activeSelf != on) messageBanner.SetActive(on);
        }

        void TickMessage()
        {
            if (message == null || messageUntil <= 0f || Time.unscaledTime < messageUntil) return;
            messageUntil = 0f;
            message.text = "";
            ShowMessage(false);
        }

        // Absolute cumulative game-hours since day 1, so fish freshness is correct across day rollovers
        // (worldHour resets to 6 each morning and must NOT be used for ageing). Matches the real game.
        float AbsHour => (save.Data.day - 1) * 24 + (phaseTime < GameCatalog.DaySeconds ? phaseTime / GameCatalog.DaySeconds * 12 : 12 + (phaseTime - GameCatalog.DaySeconds) / GameCatalog.NightSeconds * 12);

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            save = new PlayerSave();
            // Space the docks with randomised gaps (each >= DockGap) so the voyage length varies run to run,
            // then everything downstream (obstacle field, sea length, fish field) is built from those positions.
            GameCatalog.LayoutDocks();
            AcquireCanvas();
            if(harborZone!=null)bannerBaseY=harborZone.rectTransform.anchoredPosition.y;
            SetupPorts();        // match the baked port art to the 10-port map BEFORE art is assigned
            SetupPortHalos();
            RepairArtReferences();
            StyleFishingLine();
            EnsureUIInput();
            BindButtons();
            SyncUpgradeArt();
            SetupHitFx();
            SetupJoystickKnob();
            SetupNeedles();
            SetupParallax();    // build the layered backdrop BEFORE the first obstacle placement
            SetupKrakenVisuals();
            HideMonster();
            SetupBandOverlays();
            SetupBubbles();
            SetupCastTimer();
            SetupSleepButton();
            SetupObstacles();   // obstacle field was generated by LayoutDocks() above
            SetupNavArrow();    // edge chevron that points toward fish / port when the water is empty
            SetupTutorialSpotlight();   // dark mask + highlight + gesture pointer over the current tutorial control
            ApplyDepthScale();  // depth ruler + camera zoom for the current hull tier, BEFORE any fish exist
            // Restore the time of day so Continue resumes where the clock was, not at dawn. worldHour is
            // derived from phaseTime the same way TickClock does; wasNight is seeded so no spurious dusk/dawn
            // swap fires on the first sea frame. (NewGame resets these back to morning via save.Reset().)
            phaseTime=save.Data.phaseTime;
            worldHour=phaseTime<GameCatalog.DaySeconds
                ?6+phaseTime/GameCatalog.DaySeconds*12
                :18+(phaseTime-GameCatalog.DaySeconds)/GameCatalog.NightSeconds*12;
            wasNight=phaseTime>=GameCatalog.DaySeconds;
            // Boot into the port the player was last at, so Continue resumes there (NewGame overrides this
            // back to Home Harbor). Falls back to Home if the id is unknown / an old save.
            float savedBoatX=save.Data.boatX;   // OpenHarbor snaps boatX to the port's x — restore the real position after
            OpenHarbor(GameCatalog.PortById(save.Data.lastPortId));
            boatX=savedBoatX;
            save.Data.boatX=savedBoatX;
            // Old saves (and fresh ones) have no dawn snapshot yet — arm one so a sinking has somewhere to
            // rewind to. A save that already carries one (mid-day Continue) keeps its real day-start point.
            if(string.IsNullOrEmpty(save.Data.dayStart))save.CaptureDayStart();
            PopulateFishField();   // pre-spawn fish across every zone BEFORE the player sails out to meet them
            SetupMissions();       // hands out mission 1 on a fresh save and wires the Ledger view
            SetupStorageRefs();    // storage panel + its open button; harmless if neither is wired
            SetupHints();          // contextual one-liners; harmless if hintView is unwired
            SetupTitleRefs();      // last: it decides whether the harbour is reachable yet
            MissionsWorldReady();  // from here on, a docking is the player arriving — not Awake building

#if UNITY_EDITOR
            // In-game tuning overlay (⚙ TUNE top-left / press T) — EDITOR ONLY. Wrapped in UNITY_EDITOR so it
            // is compiled out of EVERY build (web + apk): no cheat/tune panel ever ships. Spawned here
            // because it isn't in the hand-built scene.
            if (FindFirstObjectByType<TuningPanel>() == null)
                new GameObject("TuningPanel").AddComponent<TuningPanel>();
#endif

            // Tuning lives on the TuningInspector component of this GameObject (added via RequireComponent, so
            // it's editable in Edit mode). It re-applies its serialized values in its own Start(), after this.
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, .05f * Mathf.Max(1f, Time.timeScale));
            // Mission feedback runs BEFORE the harbour early-out: the claim stamp is pressed while the
            // player is standing in port, so it would never animate if it sat below this line.
            TickMissionUI(dt);
            // Above the harbour early-out, same as the mission UI: the "how to sell" hint only makes sense
            // while standing in port, which is exactly where this method used to stop running.
            TickHints();
            TickTutorialSpotlight();   // spotlight the control the current hint is talking about
            TickMessage();   // most messages are written in the harbour, so this must run there too
            if (inventoryOpen || mode == Mode.Harbor) return;
            TickClock(dt);
            MissionNoteOffshore(!GameCatalog.InSafeZone(boatX), mode == Mode.Night);
            TickBoat(dt);
            TickNavArrow();   // point the way to fish / port across the (now long) sea
            TickFish(dt);
            TickKrakenEvent(dt);   // warning -> six tentacles -> quiet; drives the visuals below
            TickKrakenVisuals(dt);
            // Demo parity: press-and-hold the FISH dial to cast (rising edge), release to retract.
            bool pressed = joystick != null && joystick.Held && !joyHeldPrev;
            if (joystick != null) joyHeldPrev = joystick.Held;
            if ((mode == Mode.Sailing || mode == Mode.Night) && pressed) StartFishing();
            if (mode == Mode.Fishing) TickHook(dt);
            TickDockCamera(dt);
            TickHitFx(dt);
            UpdateSeaUI();
        }
    }
}
