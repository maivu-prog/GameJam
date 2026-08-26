using System.Collections.Generic;
using UnityEngine;

namespace RustyFishing
{
    /// <summary>
    /// Per-region fish stock. Every (zone x band) region holds a 0..1 stock that the spawner multiplies
    /// into its population target: catching draws it down, time puts it back.
    ///
    /// Without this the sea is a tap, not a resource — the field tops up two fish every 1.1s, so a region
    /// emptied of sixteen fish is full again nine seconds later and standing still is optimal.
    ///
    /// The stock is tracked PER BAND, not per zone. That is the point of the whole mechanic: fishing out
    /// the surface of a zone leaves the water below it untouched, so unlocking depth does not only buy
    /// more valuable fish, it buys somewhere to go when the shallows run thin.
    /// </summary>
    public static class FishStock
    {
        /// <summary>How much of a region one catch removes (1.0 = the whole region).</summary>
        public static float DepletionPerCatch = .04f;

        /// <summary>Stock recovered per second. Tuned so a region fished hard for one day phase needs
        /// about TWO full cycles to come back. At one cycle the regen exactly cancelled the depletion and
        /// the mechanic did nothing at all.</summary>
        public static float RegenPerSecond = .001f;

        /// <summary>A region never dies completely — there is always something left, just not worth staying for.</summary>
        public static float MinStock = .15f;

        /// <summary>Sleeping through the night at anchor puts this much back everywhere.</summary>
        public static float SleepRegen = .55f;

        static int Slots => Mathf.Max(1, SeaMap.Zones.Count * SeaMap.Bands.Count);

        static int Index(int zone, int band) =>
            Mathf.Clamp(zone - 1, 0, SeaMap.Zones.Count - 1) * SeaMap.Bands.Count
            + Mathf.Clamp(band, 0, SeaMap.Bands.Count - 1);

        static void EnsureSized(SaveData d)
        {
            d.fishStock ??= new List<float>();
            while (d.fishStock.Count < Slots) d.fishStock.Add(1f);
        }

        public static float Of(SaveData d, int zone, int band)
        {
            EnsureSized(d);
            return Mathf.Clamp(d.fishStock[Index(zone, band)], MinStock, 1f);
        }

        /// <summary>Draw one fish out of a region.</summary>
        public static void Take(SaveData d, int zone, int band)
        {
            EnsureSized(d);
            int i = Index(zone, band);
            d.fishStock[i] = Mathf.Max(MinStock, d.fishStock[i] - DepletionPerCatch);
        }

        /// <summary>Time passing. Every region recovers, including the ones you are not in.</summary>
        public static void Tick(SaveData d, float dt)
        {
            EnsureSized(d);
            float add = RegenPerSecond * dt;
            if (add <= 0f) return;
            for (int i = 0; i < d.fishStock.Count; i++)
                if (d.fishStock[i] < 1f) d.fishStock[i] = Mathf.Min(1f, d.fishStock[i] + add);
        }

        /// <summary>Bulk recovery — sleeping off a night at anchor.</summary>
        public static void Restore(SaveData d, float amount)
        {
            EnsureSized(d);
            for (int i = 0; i < d.fishStock.Count; i++)
                d.fishStock[i] = Mathf.Min(1f, d.fishStock[i] + amount);
        }

        /// <summary>Best stock across the bands this hull tier can actually reach at this x.</summary>
        public static float BestReachable(SaveData d, float worldX, int shipTier)
        {
            int zone = SeaMap.ZoneIndexAt(worldX);
            float best = 0f;
            int bands = SeaMap.UnlockedBands(shipTier);
            for (int b = 0; b < bands; b++) best = Mathf.Max(best, Of(d, zone, b));
            return best;
        }

        // The player has to be able to SEE a region thinning, or it just reads as the game being stingy.
        public static string Label(float stock) =>
            stock > .72f ? "RICH" : stock > .38f ? "THINNING" : "FISHED OUT";
    }
}
