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

        [Header("HUD Text  (auto-wired — don't edit)")]
        [Tooltip("Arrival banner: shows the port name on the sea screen ~2s then fades when you reach a dock.")]
        [SerializeField] TMP_Text harborZone;
        [SerializeField] TMP_Text seaCargo, seaCoins, hp, speed, message, market, harborName; // clock is needle-only; safe/danger is the board

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
        Vector2 hookOffset;
        bool hookRetracting, joyHeldPrev;
        Image[] lineSegs;
        RectTransform lineRoot;
        readonly List<Vector2> trail = new(), linePts = new();
        readonly List<GameObject> marketRows = new();
        int upgradeIndex;               // which of the 4 parts the upgrade card is showing
        float shakeTime;
        Image hitFlash;                 // collision feedback: screen shake + red flash
        bool inventoryOpen;
        GameObject inventoryPanel;      // basket-full toss modal
        GameObject modalPanel;          // settings modal (runtime-built stub)
        PortDef bannerPort;             // last port announced by the arrival banner
        float bannerTimer;              // harbor-arrival name banner countdown
        const float BannerHold = 2f, BannerFade = .6f;
        // Null-safe text setter, so HUD labels you delete/rearrange in the scene never NRE.
        void Set(TMP_Text t, string s) { if (t != null) t.text = s; }

        // Absolute cumulative game-hours since day 1, so fish freshness is correct across day rollovers
        // (worldHour resets to 6 each morning and must NOT be used for ageing). Matches the real game.
        float AbsHour => (save.Data.day - 1) * 24 + (phaseTime < GameCatalog.DaySeconds ? phaseTime / GameCatalog.DaySeconds * 12 : 12 + (phaseTime - GameCatalog.DaySeconds) / GameCatalog.NightSeconds * 12);

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            save = new PlayerSave();
            // Port/obstacle positions come from game-data.json (or built-in defaults). DockGap remains an
            // optional live re-spacing tool via the Tuning Tool slider; it no longer overrides positions at start.
            if (canvas == null) Build();
            RepairArtReferences();
            StyleFishingLine();
            EnsureUIInput();
            BindButtons();
            SyncUpgradeArt();
            SetupHitFx();
            SetupJoystickKnob();
            SetupNeedles();
            OpenHarbor(GameCatalog.Ports[0]);
            try { if (Object.FindFirstObjectByType<TuningPanel>() == null) { var tp = new GameObject("TuningPanel").AddComponent<TuningPanel>(); tp.host = canvas; } }
            catch (System.Exception e) { Debug.LogWarning("TuningPanel init skipped: " + e.Message); }
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, .05f);
            if (inventoryOpen || mode == Mode.Harbor) return;
            TickClock(dt);
            TickBoat(dt);
            TickFish(dt);
            // Demo parity: press-and-hold the FISH dial to cast (rising edge), release to retract.
            bool pressed = joystick != null && joystick.Held && !joyHeldPrev;
            if (joystick != null) joyHeldPrev = joystick.Held;
            if ((mode == Mode.Sailing || mode == Mode.Night) && pressed) StartFishing();
            if (mode == Mode.Fishing) TickHook(dt);
            TickHitFx(dt);
            UpdateSeaUI();
        }
    }
}
