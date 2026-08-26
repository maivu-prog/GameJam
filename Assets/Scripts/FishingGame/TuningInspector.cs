using UnityEngine;

namespace RustyFishing
{
    /// <summary>
    /// Live game-field tuning, edited straight from the Unity Inspector (replaces the old on-screen slider
    /// overlay). Enter Play mode, select the GameObject that holds the FishingGameController (this component
    /// is auto-added there), and edit any field — the value is pushed to GameCatalog immediately.
    ///
    /// On Play the fields are seeded from the live (game-data.json-overridden) values via PullFromCatalog(),
    /// so what you see is what the game is actually using. Structural changes (dock spacing, obstacle spacing)
    /// rebuild the docks/obstacles automatically; changing fish density needs a "Repopulate fish" (context menu)
    /// to take full effect since it only tops the field up, never removes existing fish.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TuningInspector : MonoBehaviour
    {
        [Header("Lưỡi câu — vật lý")]
        [Tooltip("Tốc độ chìm cơ bản của lưỡi câu")] public float sinkBase = 3.5f;
        [Tooltip("Tốc độ chìm tối đa khi giữ gạt xuống")] public float sinkMax = 5f;
        [Tooltip("Lực kéo lưỡi câu lên khi gạt lên")] public float upForce = 11f;
        [Tooltip("Tốc độ nổi lên tối đa của lưỡi câu")] public float riseMax = 5f;
        [Tooltip("Độ cản khi kéo lên (cao = chậm hơn)")] public float upDrag = .5f;
        [Tooltip("Tốc độ di chuyển ngang của lưỡi câu")] public float horizontal = 5f;
        [Tooltip("Tốc độ thu lưỡi câu về thuyền")] public float retract = 20f;
        [Tooltip("Thời gian tối đa mỗi lần thả câu (giây)")] public float lineSeconds = 12f;
        [Tooltip("Trần tầm với của dây câu. Giới hạn thật thường là tầng đã mở khoá theo bậc thân tàu.")] public float maxDepthU = 56f;
        [Tooltip("Kích thước hiển thị của lưỡi câu")] public float hookScale = 1.261f;

        [Header("Dây câu")]
        [Tooltip("Độ dày của dây câu")] public float lineWidth = 13.353f;
        [Tooltip("Khoảng cách giữa các điểm ghi đường đi của dây (nhỏ = mượt hơn)")] public float trailMinDist = 14f;

        [Header("Cá")]
        [Tooltip("Tốc độ bơi ngang của cá")] public float fishSwim = 40f;
        [Tooltip("Biên độ nhấp nhô lên xuống của cá")] public float fishWander = 26f;
        [Tooltip("Khoảng cá bơi quanh chỗ ở của nó (px)")] public float fishRoam = 220f;
        [Tooltip("Cá ẩn đi khi trôi ra ngoài khoảng này (px)")] public float fishCull = 780f;
        [Tooltip("Mật độ cá toàn cục (nhân với density từng vùng)")] public float fishDensity = .5f;
        [Tooltip("Số cá tối đa trong cả biển")] public int fishFieldMax = 70;
        [Tooltip("Hệ số phóng to/thu nhỏ toàn bộ cá")] public float fishSize = 1f;
        [Tooltip("Bán kính quanh cảng ít cá (dụ đi xa)")] public float portSparse = 14f;

        [Header("Mật độ cá theo tầng sâu")]
        [Tooltip("Tầng A — nước mặt, vùng chơi mặc định")] public float densityBandA = 1f;
        [Tooltip("Tầng B — mở khoá ở thân tàu bậc 1")] public float densityBandB = .85f;
        [Tooltip("Tầng C — nước sâu, thân tàu bậc 2")] public float densityBandC = .7f;

        [Header("Thế giới / cảng / chướng ngại")]
        [Tooltip("Tốc độ cuộn cảnh biển theo thuyền (px/đơn vị)")] public float worldScroll = 42f;
        // DockGap / DockGapVarMax / ObstacleFreeUntilX / ObstacleMinGap are deliberately NOT here.
        // They shape the MAP, and the map is authored in game-data.json alongside the per-zone gapMul
        // and shelfDepth that have to agree with them. Mirroring them in this component gave two sources
        // of truth, and since this component re-pushes its values every frame it silently won — the whole
        // authored layout was being rebuilt at stale numbers right after Awake had built the correct one.

        [Header("Đồng hồ tốc độ")]
        [Tooltip("Góc bắt đầu của kim đồng hồ tốc độ")] public float spdNeedleStart = -40f;
        [Tooltip("Góc quét của kim đồng hồ tốc độ")] public float spdNeedleSweep = 240f;

        FishingGameController cachedController;
        FishingGameController Controller => cachedController != null ? cachedController : (cachedController = FindFirstObjectByType<FishingGameController>());

        // On Play, PUSH the Inspector's serialized values into the game so what you set in Edit mode takes
        // effect (this component is the tuning source of truth — it overrides game-data.json's "tuning" block),
        // then rebuild docks/obstacles/fish so structural values apply too.
        void Start()
        {
            ApplyScalars();
            RepopulateFish();   // density/size are ours; the dock layout stays exactly as Awake built it
        }

        // Copy the LIVE values out of GameCatalog into the Inspector fields (e.g. to grab what game-data.json
        // loaded, in Play mode). Available from the component's context menu.
        // This component overwrites GameCatalog every frame, so edits made in game-data.json are invisible
        // until its own serialized fields are brought up to date. This does that in one step.
        [ContextMenu("Load values from game-data.json")]
        public void LoadFromJson()
        {
            GameDataLoader.LoadNow();
            PullFromCatalog();
            Debug.Log("[Tuning] Inspector fields refreshed from game-data.json. Save the scene to keep them.", this);
        }

        [ContextMenu("Pull current values from GameCatalog")]
        public void PullFromCatalog()
        {
            sinkBase = GameCatalog.HookSink; sinkMax = GameCatalog.HookSinkMax; upForce = GameCatalog.HookUpForce; riseMax = GameCatalog.HookRiseMax; upDrag = GameCatalog.HookUpDrag;
            horizontal = GameCatalog.HookHorizontal; retract = GameCatalog.HookRetract; lineSeconds = GameCatalog.HookLineSeconds; maxDepthU = GameCatalog.HookMaxDepthUnits; hookScale = GameCatalog.HookScale;
            lineWidth = GameCatalog.LineWidthPx; trailMinDist = GameCatalog.LineTrailMinDist;
            fishSwim = GameCatalog.FishSwimPpu; fishWander = GameCatalog.FishWanderPx; fishRoam = GameCatalog.FishRoamHalfWidthPx; fishCull = GameCatalog.FishCullPx; fishDensity = GameCatalog.FishFieldDensity; fishFieldMax = GameCatalog.FishFieldMax;
            fishSize = GameCatalog.FishSizeScale; portSparse = GameCatalog.FishPortSparseRadius;
            worldScroll = GameCatalog.WorldScrollPpu;
            spdNeedleStart = GameCatalog.SpeedNeedleStart; spdNeedleSweep = GameCatalog.SpeedNeedleSweep;
            var b = SeaMap.Bands;
            if (b.Count > 0) densityBandA = b[0].densityMul;
            if (b.Count > 1) densityBandB = b[1].densityMul;
            if (b.Count > 2) densityBandC = b[2].densityMul;
        }

        // Push all cheap scalar values (and per-zone density) into GameCatalog. Idempotent.
        void ApplyScalars()
        {
            GameCatalog.HookSink = sinkBase; GameCatalog.HookSinkMax = sinkMax; GameCatalog.HookUpForce = upForce; GameCatalog.HookRiseMax = riseMax; GameCatalog.HookUpDrag = upDrag;
            GameCatalog.HookHorizontal = horizontal; GameCatalog.HookRetract = retract; GameCatalog.HookLineSeconds = lineSeconds; GameCatalog.HookMaxDepthUnits = maxDepthU; GameCatalog.HookScale = hookScale;
            GameCatalog.LineWidthPx = lineWidth; GameCatalog.LineTrailMinDist = trailMinDist;
            GameCatalog.FishSwimPpu = fishSwim; GameCatalog.FishWanderPx = fishWander; GameCatalog.FishRoamHalfWidthPx = fishRoam; GameCatalog.FishCullPx = fishCull; GameCatalog.FishFieldDensity = fishDensity; GameCatalog.FishFieldMax = fishFieldMax;
            GameCatalog.FishSizeScale = fishSize; GameCatalog.FishPortSparseRadius = portSparse;
            GameCatalog.WorldScrollPpu = worldScroll; GameCatalog.SpeedNeedleStart = spdNeedleStart; GameCatalog.SpeedNeedleSweep = spdNeedleSweep;
            var b = SeaMap.Bands;
            if (b.Count > 0) b[0].densityMul = densityBandA;
            if (b.Count > 1) b[1].densityMul = densityBandB;
            if (b.Count > 2) b[2].densityMul = densityBandC;
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            ApplyScalars(); // push every frame so Inspector edits show instantly

            // Map layout is not tuned from here any more — use the "Rebuild docks + obstacles" context
            // menu after editing game-data.json if you want to see a fresh roll.
        }

        [ContextMenu("Rebuild docks + obstacles")]
        public void RebuildDocks() { GameCatalog.LayoutDocks(); if (Controller != null) Controller.RebuildObstacleArt(); }

        [ContextMenu("Repopulate fish (apply density changes)")]
        public void RepopulateFish() { if (Controller != null) Controller.RepopulateFish(); }

        [ContextMenu("Reset save (wipe progress)")]
        public void ResetSave() { if (Controller != null) Controller.ResetProgression(); }

        // Play-mode edits are NOT saved by Unity. Use this to print the current values in game-data.json
        // format so you can paste them back into Assets/Resources/GameData/game-data.json to persist them.
        [ContextMenu("Log values (paste into game-data.json)")]
        public void LogValues()
        {
            string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== game-data.json \"tuning\" block ===");
            sb.AppendLine($"\"HookSink\": {F(sinkBase)}, \"HookSinkMax\": {F(sinkMax)}, \"HookUpForce\": {F(upForce)}, \"HookRiseMax\": {F(riseMax)}, \"HookUpDrag\": {F(upDrag)},");
            sb.AppendLine($"\"HookHorizontal\": {F(horizontal)}, \"HookRetract\": {F(retract)}, \"HookLineSeconds\": {F(lineSeconds)}, \"HookMaxDepthUnits\": {F(maxDepthU)}, \"HookScale\": {F(hookScale)},");
            sb.AppendLine($"\"LineWidthPx\": {F(lineWidth)}, \"LineTrailMinDist\": {F(trailMinDist)},");
            sb.AppendLine($"\"FishSwimPpu\": {F(fishSwim)}, \"FishWanderPx\": {F(fishWander)}, \"FishRoamHalfWidthPx\": {F(fishRoam)}, \"FishCullPx\": {F(fishCull)}, \"FishFieldDensity\": {F(fishDensity)}, \"FishFieldMax\": {fishFieldMax},");
            sb.AppendLine($"\"FishSizeScale\": {F(fishSize)}, \"FishPortSparseRadius\": {F(portSparse)},");
            sb.AppendLine($"\"WorldScrollPpu\": {F(worldScroll)},");
            sb.AppendLine($"\"SpeedNeedleStart\": {F(spdNeedleStart)}, \"SpeedNeedleSweep\": {F(spdNeedleSweep)}");
            sb.AppendLine("=== \"bands\" density (put into each band row) ===");
            sb.AppendLine($"A: {F(densityBandA)}   B: {F(densityBandB)}   C: {F(densityBandC)}");
            Debug.Log(sb.ToString());
        }
    }
}
