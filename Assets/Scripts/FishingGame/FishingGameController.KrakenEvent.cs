using System.Collections.Generic;
using UnityEngine;

namespace RustyFishing
{
    // The Drowned One is not weather. The other two night hunters drift in and out of the field on their
    // own, but this one arrives as an event: the water warns you first, then six tentacles come up at
    // once and everything else in the sea leaves.
    //
    // Each tentacle is an ordinary FishActor, which is the whole point of building it this way -- hooking,
    // health bars, biting the hull, fleeing, selling and the 12-hour spoilage all work already. Severing
    // one is worth 1100 coins, the richest catch in the game.
    //
    // The warning exists so the fight is a CHOICE. Deep water at night is where the money is; the arm is
    // what you accept in exchange, and you get a few seconds to decide you would rather not.
    public sealed partial class FishingGameController
    {
        const string KrakenId = "kraken";

        // How long the water churns before the arm breaks the surface. Long enough to turn and run at
        // full speed out of the zone, and no longer -- the threat has to feel like it is arriving.
        const float KrakenWarningSeconds = 7f;

        // After the arm leaves (severed or dawn), the deep is quiet for this long.
        const float KrakenCooldownSeconds = 150f;

        // How long the arm hangs on once the boat is out of its water before sinking back.
        // Short, because breaking off is no longer a reset: the wounds carry (see krakenWounds), so
        // retreating and coming back is a real tactic rather than throwing the fight away.
        const float KrakenGiveUpSeconds = 12f;

        enum KrakenState { Away, Warning, Present }

        KrakenState krakenState = KrakenState.Away;
        float krakenTimer;                 // counts the warning down, then the cooldown
        bool krakenPresent;                // read by Eligible: while true, nothing else hunts
        bool krakenWarned;                 // so the warning line is written once, not every frame
        float krakenGiveUp;                // counts down only while the boat is outside its water

        /// <summary>
        /// Health of the tentacles still attached when the arm last let go, so a hunt survives being
        /// broken off. Running is meant to be a retreat, not a reset -- without this, fleeing handed the
        /// arm all six tentacles back at full health and every encounter started from nothing.
        /// Cleared when the arm is finished off, and at dawn, so each night begins whole.
        /// </summary>
        readonly List<float> krakenWounds = new();

        /// <summary>Is this water where the Drowned One lives, right now?</summary>
        bool InKrakenWater()
        {
            var def = GameCatalog.GetFish(KrakenId);
            if (def == null || !IsNight) return false;
            // Band C has to be genuinely reachable, not merely unlocked on paper: over the shelf the
            // hook stops short of its depth and the fight could never happen.
            if (SeaMap.UnlockedBands(save.Tier) < 3) return false;
            if (SeaMap.PlayableDepthU(boatX, save.Tier) <= def.minDepth) return false;
            int zi = SeaMap.ZoneIndexAt(boatX);
            if (zi < def.minZone || zi > def.maxZone) return false;
            return !GameCatalog.InSafeZone(boatX);   // harbour water is a truce, even for this
        }

        void TickKrakenEvent(float dt)
        {
            switch (krakenState)
            {
                case KrakenState.Away:
                    krakenTimer -= dt;
                    if (krakenTimer > 0f) return;
                    if (!InKrakenWater()) return;
                    krakenState = KrakenState.Warning;
                    krakenTimer = KrakenWarningSeconds;
                    krakenWarned = false;
                    break;

                case KrakenState.Warning:
                    // Leaving the water calls it off. That is the point of warning at all.
                    if (!InKrakenWater()) { EndKraken(false); return; }
                    if (!krakenWarned)
                    {
                        krakenWarned = true;
                        Set(message, "The water is turning. Something is coming up.");
                    }
                    krakenTimer -= dt;
                    if (krakenTimer <= 0f) SummonKraken();
                    break;

                case KrakenState.Present:
                    // Dawn, or the last tentacle severed, ends it.
                    if (!IsNight || CountSpecies(KrakenId) == 0) { EndKraken(true); return; }

                    // Running does not save the hold -- the arm keeps hold of the sea while you are in
                    // its water. But it cannot follow you out of the deep for ever, and while it is up
                    // NOTHING else hunts anywhere on the map. Left unbounded, fleeing bought the player
                    // an empty ocean for the rest of the night, which punishes the sane reaction.
                    if (InKrakenWater()) { krakenGiveUp = KrakenGiveUpSeconds; break; }
                    krakenGiveUp -= dt;
                    if (krakenGiveUp <= 0f) EndKraken(false);
                    break;
            }
        }

        /// <summary>
        /// Bring the arm up: clear the sea of everything else that hunts, then place six tentacles across
        /// the deep band around the boat.
        /// </summary>
        void SummonKraken()
        {
            var def = GameCatalog.GetFish(KrakenId);
            if (def == null) { EndKraken(false); return; }

            krakenState = KrakenState.Present;
            krakenPresent = true;
            krakenGiveUp = KrakenGiveUpSeconds;

            // Everything else that hunts leaves. Not deleted outright -- they fade, so it reads as the
            // sea emptying ahead of something worse rather than as a bug.
            for (int i = 0; i < fish.Count; i++)
            {
                var f = fish[i];
                if (f != null && !f.Leaving && f.Def.Evil && f.Def.id != KrakenId)
                    f.Dismiss(GameCatalog.PhaseFadeSeconds);
            }

            var zone = SeaMap.ZoneAt(boatX);
            float seabed = SeaMap.SeabedDepthU(boatX);
            var band = SeaMap.Bands[SeaMap.Bands.Count - 1];        // the deep one it lives in
            bool resuming = krakenWounds.Count > 0;
            int want = resuming ? krakenWounds.Count : Mathf.Max(1, def.maxAlive);
            int made = 0;

            for (int i = 0; i < want; i++)
            {
                // Spread them either side of the boat so the arm surrounds rather than queues up. The
                // spacing check still applies, so two never land on the same spot.
                float side = (i % 2 == 0) ? -1f : 1f;
                float spread = 1.2f + (i / 2) * 1.6f;
                float x = boatX + side * spread;
                float depth = SeaMap.RollDepth(def, band, seabed);
                if (!FishSpotClear(x, depth, FishWidthPx(def))) { x += side * .8f; }
                SpawnFishAt(def, x, depth, zone.evilHpMul, band.fleeMul * zone.fleeMul);
                // The one just added is the last in the list -- give it back the wound it carried.
                if (resuming && fish.Count > 0)
                {
                    var t = fish[fish.Count - 1];
                    if (t != null && t.Def.id == KrakenId) t.SetWoundedHp(krakenWounds[i]);
                }
                made++;
            }
            krakenWounds.Clear();

            Set(message, resuming
                ? $"IT FOUND YOU AGAIN. {made} tentacles left, and they remember."
                : $"THE DROWNED ONE. {made} tentacles. Cut them loose or run.");
            if (GameCatalog.debugStorage)
                Debug.Log($"[KRAKEN] surfaced at zone {zone.index}, {made} tentacles, " +
                          $"{def.hp * zone.evilHpMul:0} hp each, {def.atk} atk");
        }

        /// <summary>The arm leaves. `fought` distinguishes a finished fight from a warning called off.</summary>
        void EndKraken(bool fought)
        {
            bool cleared = CountSpecies(KrakenId) == 0;
            if (krakenState == KrakenState.Present && fought && cleared)
                Set(message, "The arm sinks back. The sea is yours again.");

            // Broken off rather than finished: remember the wounds so the next encounter carries on.
            // A finished fight and a new dawn both start the arm whole again.
            krakenWounds.Clear();
            bool remember = krakenState == KrakenState.Present && !fought && !cleared;

            for (int i = 0; i < fish.Count; i++)
            {
                var f = fish[i];
                if (f == null || f.Leaving || f.Def.id != KrakenId) continue;
                if (remember) krakenWounds.Add(f.Hp);
                f.Dismiss(GameCatalog.PhaseFadeSeconds);
            }

            if (remember && GameCatalog.debugStorage)
                Debug.Log($"[KRAKEN] broke off with {krakenWounds.Count} tentacles left, " +
                          $"hp {string.Join("/", krakenWounds.ConvertAll(h => Mathf.RoundToInt(h)))}");

            krakenState = KrakenState.Away;
            krakenPresent = false;
            krakenWarned = false;
            // A called-off warning may retry soon; a finished fight buys real quiet.
            krakenTimer = fought ? KrakenCooldownSeconds : 20f;
        }
    }
}
