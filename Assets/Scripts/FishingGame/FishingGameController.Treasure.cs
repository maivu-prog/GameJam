using System.Collections.Generic;
using UnityEngine;

namespace RustyFishing
{
    // Treasure system: in the far/deep sea (past the shallow home area), a treasure may rest on the seabed
    // near the midpoint between two adjacent ports. The whole field re-rolls every few days; each spot rolls a
    // spawn chance (higher the deeper/farther it is) and, on success, a type (normal 70% / high 20% / highest
    // 10%). Treasures are drained with the SAME hook mechanic as fish and pay out coins.
    public sealed partial class FishingGameController
    {
        readonly List<TreasureActor> treasures = new();
        int treasureResetDay = -999;                 // last day the field was re-rolled

        // Tunables (kept here so treasure balance sits in one place).
        const int TreasureResetDays = 3;             // re-roll the whole field this often
        const float TreasureSpawnBase = 0.35f;       // base chance per eligible spot...
        const float TreasureSpawnPerLevel = 0.08f;   // ...+ this per depth/distance level...
        const float TreasureSpawnMax = 0.85f;        // ...capped here
        const float TreasureXJitter = 3f;            // world-units of scatter around the midpoint
        static readonly int[] TreasureCoin = { 30, 70, 150 };      // normal / high / highest
        static readonly float[] TreasureBaseHp = { 40f, 70f, 110f };
        static readonly string[] TreasureArt =
        {
            "World/Treasures/treasure-normal-bag",
            "World/Treasures/treasure-higher-pearl",
            "World/Treasures/treasure-highest-chest",
        };

        /// <summary>Re-roll the field if enough days have passed (or it has never been built).</summary>
        void MaybeResetTreasures()
        {
            if (save.Data.day - treasureResetDay < TreasureResetDays) return;
            treasureResetDay = save.Data.day;
            ResetTreasureField();
        }

        void ResetTreasureField()
        {
            for (int i = 0; i < treasures.Count; i++) if (treasures[i] != null) Destroy(treasures[i].gameObject);
            treasures.Clear();

            var ports = GameCatalog.Ports;
            for (int i = 0; i < ports.Count - 1; i++)
            {
                float mid = (ports[i].x + ports[i + 1].x) * 0.5f;   // the farthest point between two adjacent ports
                float seabed = SeaMap.SeabedDepthU(mid);
                // "Past A1/A2": skip spots whose seabed is still in the shallow first band.
                if (seabed <= SeaMap.Bands[0].bottomU + 0.5f) continue;
                int level = Mathf.Max(0, SeaMap.ZoneIndexAt(mid) - 2) + SeaMap.BandIndexAt(seabed);
                float pSpawn = Mathf.Clamp(TreasureSpawnBase + TreasureSpawnPerLevel * level, TreasureSpawnBase, TreasureSpawnMax);
                if (Random.value > pSpawn) continue;   // missed — no treasure in this stretch this cycle
                int type = RollTreasureType();
                float x = GameCatalog.PushOutOfPorts(mid + Random.Range(-TreasureXJitter, TreasureXJitter));
                SpawnTreasure(type, x, seabed);
            }
            if (GameCatalog.debugStorage) Debug.Log($"[Treasure] reset day {save.Data.day}: {treasures.Count} spawned");
        }

        static int RollTreasureType()
        {
            float r = Random.value;
            return r < 0.70f ? 0 : (r < 0.90f ? 1 : 2);   // 70 / 20 / 10
        }

        void SpawnTreasure(int type, float homeX, float depthU)
        {
            float hp = TreasureBaseHp[type] * (1f + save.Tier * 0.15f);   // grows slightly with hull tier
            float w = (130f + type * 22f) * DepthZoom();
            var rect = RuntimeUI.Rect(fishLayer, "Treasure", Vector2.zero, Vector2.one);
            var a = rect.gameObject.AddComponent<TreasureActor>();
            a.Init(type, TreasureArt[type], homeX, DepthToLocalY(depthU), w, hp, depthU);
            treasures.Add(a);
        }

        void TickTreasures(float dt, Vector2? hookLocal)
        {
            for (int i = treasures.Count - 1; i >= 0; i--)
            {
                var t = treasures[i];
                if (t == null) { treasures.RemoveAt(i); continue; }
                t.Tick(boatX, DepthToLocalY(t.DepthU), hookLocal);   // recompute y so a depth rescale keeps it on the seabed
            }
        }

        // Called from the hook tick: drain the nearest treasure the shank is touching, same rules as fish.
        void TryCatchTreasure(float hookMovePx, float zoom, float dt)
        {
            if (treasures.Count == 0) return;
            TreasureActor best = null; float bestD = float.MaxValue;
            for (int i = 0; i < treasures.Count; i++)
            {
                var t = treasures[i];
                if (t == null || !t.Visible) continue;
                float d = DistanceToHookBody(t.Rect.anchoredPosition + fishLayer.anchoredPosition);
                float reach = t.Rect.sizeDelta.x * 0.5f * GameCatalog.FishHitFraction + GameCatalog.HookCatchRadius * zoom;
                if (d >= reach || d >= bestD) continue;
                bestD = d; best = t;
            }
            if (best == null || hookMovePx < GameCatalog.HookBiteMinSpeed) return;
            if (best.Hit(GameCatalog.HookDamage * save.DamageMultiplier * dt))
            {
                int coins = TreasureCoin[Mathf.Clamp(best.Type, 0, TreasureCoin.Length - 1)];
                save.Data.coins += coins; save.Store(); SetCoins();
                Set(message, $"Treasure hauled up!  +{coins}c");
                treasures.Remove(best);
                best.Collect(new Vector2(95, 300) - fishLayer.anchoredPosition);
            }
        }
    }
}
