using System;
using System.Collections.Generic;
using UnityEngine;

namespace RustyFishing
{
    /// <summary>One horizontal depth band. A is the surface water, C the deep.</summary>
    [Serializable] public sealed class BandDef
    {
        public string id;
        public float topU, bottomU;      // depth in sea-units, measured down from the water line
        public float densityMul;         // fish per unit of sea, relative to the global density
        public float rarityBias;         // pushes species choice toward rarer fish — rises FAST with depth
        public float difficultyMul;      // multiplies spawned fish HP
        public float fleeMul;            // multiplies flee speed AND flee duration — deep fish bolt harder

        public BandDef(string id, float topU, float bottomU, float densityMul, float rarityBias, float difficultyMul, float fleeMul = 1f)
        { this.id=id; this.topU=topU; this.bottomU=bottomU; this.densityMul=densityMul; this.rarityBias=rarityBias; this.difficultyMul=difficultyMul; this.fleeMul=fleeMul; }

        public float Height => Mathf.Max(0f, bottomU - topU);
    }

    /// <summary>One vertical slice of sea, sitting between two consecutive ports.</summary>
    [Serializable] public sealed class SeaZoneDef
    {
        public int index;                // 1..9, matching the design map
        public float densityMul;         // more fish the further out you sail
        public float rarityBias;         // rises SLIGHTLY with distance, unlike depth
        public float difficultyMul;
        public float fleeMul;
        // Night creatures get their OWN hp curve. The day-fish difficulty ramp starts at 1.0, which made
        // the very first piranha you ever meet as tough as a full-grown day fish; this starts at half.
        public float evilHpMul = 1f;
        // How WIDE this zone is, as a multiple of GameCatalog.DockGap. Zone width is a zone property, not
        // a dice roll: the map is meant to open out toward zone 7 and close back in, and that shape has to
        // survive a re-roll of the dock spacing.
        public float gapMul = 1f;
        public float shelfDepthU;        // the sea floor here — nothing lives below it

        public SeaZoneDef(int index, float densityMul, float rarityBias, float difficultyMul, float shelfDepthU, float fleeMul = 1f, float evilHpMul = 1f, float gapMul = 1f)
        { this.index=index; this.densityMul=densityMul; this.rarityBias=rarityBias; this.difficultyMul=difficultyMul; this.shelfDepthU=shelfDepthU; this.fleeMul=fleeMul; this.evilHpMul=evilHpMul; this.gapMul=gapMul; }
    }

    /// <summary>
    /// The progression map: 9 zones across (bounded by the 10 ports) x 3 depth bands down, minus the
    /// continental shelf that fills the deep water near shore. That gives A1..A9, B1..B9 and C4..C9 —
    /// C1..C3 do not exist because the shelf there reaches the bottom of band B.
    ///
    /// Two separate difficulty axes, deliberately weighted differently:
    ///   ACROSS (zone 1 -> 9): more fish, slightly rarer, slightly tougher. Sailing far is a soft grind.
    ///   DOWN   (band A -> C): sharply rarer and tougher. Depth is the real progression gate, and it is
    ///                         locked behind the ship's hull tier rather than behind travel.
    ///
    /// Zone boundaries are NOT stored — they are read from the live port positions, so re-rolling the dock
    /// spacing (LayoutDocks) moves the zones with the ports instead of leaving them stale.
    /// </summary>
    public static class SeaMap
    {
        // How deep the visible water column reaches at each unlock tier, in sea-units. Tier 1 shows band A
        // and half of B — enough to see that there is more down there. Each ascension pulls the view back.
        public static float[] ViewDepthU = { 22f, 34f, 46f };

        // Night gets longer as the deep opens up: 1m45 to start, +45s with band B, +30s more with band C.
        // More water to work also means more time exposed to whatever lives in it.
        public static float[] NightSecondsByTier = { 105f, 150f, 180f };

        public static float NightSecondsFor(int shipTier)
            => NightSecondsByTier[Mathf.Clamp(UnlockedBands(shipTier) - 1, 0, NightSecondsByTier.Length - 1)];

        // Screen geometry the depth scale is derived from. HookRestY is where the rod tip sits, and depth 0
        // is measured from there so the hook and the fish share one scale (they did not, historically).
        public static float HookRestY = 180f;
        public static float ViewBottomY = -960f;

        // Species weighting. weight = RarityFalloff^(rarity-1) * (1 + bias*RarityBiasGain)^(rarity-1):
        // with no bias a rarity-5 fish is ~1/25 as likely as a rarity-1; enough bias flips that around.
        public static float RarityFalloff = .45f;
        public static float RarityBiasGain = .55f;

        // Flee behaviour, scaled per region by band.fleeMul * zone.fleeMul. Deep fish run FASTER and for
        // LONGER, so the deep is hard to work rather than merely slow to chew through.
        public static float FleeSecondsBase = 2.5f;
        public static float FleeSpeedBase = 2.2f;
        // A bolting fish also gets a wider leash — otherwise it just rattles against the normal roam limit
        // and the extra speed reads as vibration instead of escape.
        public static float FleeRoamMul = 2.4f;

        // Idle darts. A fish cruising at a constant speed in a straight line reads as a sprite on a
        // conveyor; a short random burst every few seconds is what makes it read as alive.
        public static float DartChancePerSec = .22f;
        public static Vector2 DartSeconds = new(.25f, .65f);
        public static Vector2 DartSpeedMul = new(2.2f, 3.4f);

        public static readonly List<BandDef> Bands = new()
        {
            //          id  top  bottom  density  rarityBias  difficulty
            //          id  top  bottom  density  rarityBias  difficulty  flee
            new BandDef("A",  0f,   14f,    1.00f,      0f,      1.00f,   1.00f),
            new BandDef("B", 14f,   28f,     .85f,    1.2f,      1.45f,   1.25f),
            new BandDef("C", 28f,   44f,     .70f,    2.6f,      2.10f,   1.55f),
        };

        public static readonly List<SeaZoneDef> Zones = new()
        {
            //             idx density rarity  diff  shelf   flee  evilHp  gap
            new SeaZoneDef(1,   .45f,   0f,   1.00f,  28f, 1.000f, 0.50f, 0.65f),   // shelf sits on the floor of band B:
            new SeaZoneDef(2,   .60f, .10f,   1.04f,  28f, 1.025f, 0.62f, 0.85f),   // no band C at all in zones 1-3
            new SeaZoneDef(3,   .75f, .22f,   1.08f,  28f, 1.050f, 0.75f, 1.05f),
            new SeaZoneDef(4,   .90f, .34f,   1.12f,  32f, 1.075f, 0.88f, 1.25f),   // the shelf starts dropping away
            new SeaZoneDef(5,  1.05f, .46f,   1.16f,  36f, 1.100f, 1.00f, 1.45f),
            new SeaZoneDef(6,  1.20f, .58f,   1.20f,  40f, 1.125f, 1.12f, 1.70f),
            new SeaZoneDef(7,  1.35f, .70f,   1.25f,  44f, 1.150f, 1.25f, 1.95f),   // open ocean floor from here out
            new SeaZoneDef(8,  1.48f, .80f,   1.30f,  44f, 1.175f, 1.38f, 1.65f),
            new SeaZoneDef(9,  1.60f, .90f,   1.35f,  44f, 1.200f, 1.50f, 1.35f),
        };

        public static float DeepestU => Bands.Count > 0 ? Bands[Bands.Count - 1].bottomU : 0f;

        // ---- unlocking ------------------------------------------------------------------------------
        /// <summary>How many bands this SHIP may fish in (1..3). Driven by SaveData.shipTier, which the
/// New Ship purchase raises — not by the hull upgrade branch, which now runs 0..12.</summary>
        public static int UnlockedBands(int shipTier) => Mathf.Clamp(shipTier + 1, 1, Bands.Count);

        /// <summary>Bottom of the deepest band this hull tier may reach.</summary>
        public static float UnlockedDepthU(int shipTier) => Bands[UnlockedBands(shipTier) - 1].bottomU;

        /// <summary>Pixels per depth unit at this tier — this is the "camera zoom".</summary>
        public static float DepthPx(int shipTier)
        {
            int tier = Mathf.Clamp(UnlockedBands(shipTier) - 1, 0, ViewDepthU.Length - 1);
            return (HookRestY - ViewBottomY) / Mathf.Max(1f, ViewDepthU[tier]);
        }

        // ---- lookups --------------------------------------------------------------------------------
        /// <summary>Zone index (1..9) for a world x, read from the live port layout.</summary>
        public static int ZoneIndexAt(float x)
        {
            var ports = GameCatalog.Ports;
            if (ports.Count < 2) return 1;
            for (int i = 0; i < ports.Count - 1; i++)
                if (x < ports[i + 1].x) return Mathf.Clamp(i + 1, 1, Zones.Count);
            return Zones.Count;
        }

        public static SeaZoneDef ZoneAt(float x) => Zones[Mathf.Clamp(ZoneIndexAt(x) - 1, 0, Zones.Count - 1)];

        /// <summary>World x range covered by zone <paramref name="index"/> (1-based).</summary>
        public static void ZoneBounds(int index, out float lo, out float hi)
        {
            var ports = GameCatalog.Ports;
            int i = Mathf.Clamp(index, 1, Zones.Count) - 1;
            lo = i < ports.Count ? ports[i].x : 0f;
            hi = i + 1 < ports.Count ? ports[i + 1].x : GameCatalog.SeaLength;
            if (index >= Zones.Count) hi = Mathf.Max(hi, GameCatalog.SeaLength);
        }

        /// <summary>
        /// Sea floor depth at a world x. Interpolated between neighbouring zones so the shelf reads as one
        /// continuous slope instead of stepping down at every dock.
        /// </summary>
        public static float SeabedDepthU(float x)
        {
            int idx = ZoneIndexAt(x);
            ZoneBounds(idx, out float lo, out float hi);
            var here = Zones[idx - 1];
            var next = idx < Zones.Count ? Zones[idx] : here;
            float t = hi > lo ? Mathf.Clamp01((x - lo) / (hi - lo)) : 0f;
            return Mathf.Lerp(here.shelfDepthU, next.shelfDepthU, t);
        }

        /// <summary>Deepest water the player may actually fish at this x with this hull tier.</summary>
        public static float PlayableDepthU(float x, int shipTier)
            => Mathf.Min(UnlockedDepthU(shipTier), SeabedDepthU(x));

        public static BandDef BandAt(float depthU)
        {
            for (int i = 0; i < Bands.Count; i++)
                if (depthU < Bands[i].bottomU) return Bands[i];
            return Bands[Bands.Count - 1];
        }

        public static int BandIndexAt(float depthU)
        {
            for (int i = 0; i < Bands.Count; i++)
                if (depthU < Bands[i].bottomU) return i;
            return Bands.Count - 1;
        }

        /// <summary>
        /// Relative chance of picking this species in the given region. Rarity is punished by default and
        /// rewarded as the bias climbs, so shallow home water is all bream and sardine while the deep far
        /// zones are where anglerfish and ghost tuna become the common catch.
        /// </summary>
        public static float SpawnWeight(FishDef fish, BandDef band, SeaZoneDef zone)
        {
            float steps = Mathf.Max(0f, fish.rarity - 1f);
            float bias = band.rarityBias + zone.rarityBias;
            return Mathf.Pow(RarityFalloff, steps) * Mathf.Pow(1f + bias * RarityBiasGain, steps);
        }

        /// <summary>Does this species live anywhere inside this band? (its depth range must overlap)</summary>
        public static bool Inhabits(FishDef fish, BandDef band, float seabedU)
        {
            float bottom = Mathf.Min(band.bottomU, seabedU);
            return fish.minDepth < bottom && fish.maxDepth > band.topU;
        }

        // Fish were hugging the top edge of their band, so B and C read as one crowded line just under A
        // instead of as separate water. Push the usable window down a little and weight the roll toward the
        // middle of what is left, so each band fills out its own space.
        public static float BandTopInsetU = 1.5f;
        // Kept low on purpose: at tier 1 only the TOP half of band B is on screen, so clustering fish at
        // the middle of their band is the same as hiding half of them below the bottom edge.
        public static float BandCentreBias = .3f;

        /// <summary>Roll a depth for this species inside this band, never below the sea floor.</summary>
        public static float RollDepth(FishDef fish, BandDef band, float seabedU)
        {
            float top = band.topU + (band.topU > 0f ? BandTopInsetU : 0f);   // band A still starts at the surface
            float lo = Mathf.Max(fish.minDepth, top);
            float hi = Mathf.Min(Mathf.Min(fish.maxDepth, band.bottomU), seabedU);
            if (hi <= lo) return Mathf.Min(lo, hi);
            // Average of two rolls = a triangular distribution peaking at the centre; lerped against a flat
            // roll so the edges never go completely empty.
            float flat = UnityEngine.Random.value;
            float centred = (UnityEngine.Random.value + UnityEngine.Random.value) * .5f;
            return Mathf.Lerp(lo, hi, Mathf.Lerp(flat, centred, Mathf.Clamp01(BandCentreBias)));
        }
    }
}
