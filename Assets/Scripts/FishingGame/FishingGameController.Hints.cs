using System;
using System.Collections.Generic;
using UnityEngine;

namespace RustyFishing
{
    // Onboarding. Two kinds of line, in one list:
    //
    //   SEQUENCE steps run strictly in order and cover the opening loop -- the dock, casting off, steering
    //   out, fishing, coming home, selling. Each waits until the player is actually in its situation, so
    //   the pace belongs to them and not to a timer.
    //
    //   ANYTIME lines are situational and fire whenever they apply, but never while a sequence step is
    //   still pending: being warned about spoilage before you have cast once is noise.
    //
    // Nothing blocks, nothing has a Next button, nothing must be dismissed. A line leaves the moment the
    // player does the thing and never returns -- including when they worked it out unprompted.
    public sealed partial class FishingGameController
    {
        sealed class Hint
        {
            public string id, text;
            public bool sequence;        // part of the ordered opening, rather than situational
            public Func<bool> Show;      // the player is in this situation right now
            public Func<bool> Learned;   // ...and this is what proves they got it

            /// <summary>
            /// For steps that only TELL the player something, with nothing to do about it. They retire
            /// after this many seconds on screen.
            ///
            /// Without it such a step is a dead end: "this is the dock" has no action to wait for, so it
            /// would have to borrow the next step's completion -- and then the next step never gets its
            /// turn, because the thing that ended the first one already ended it too.
            /// </summary>
            public float dwellSeconds;
        }

        List<Hint> hints;
        bool hookEverMoved, everCast, everSoldFresh, everCrossedSafely, everDocked, everSailed;
        float hintHoldUntil;             // keep the current line up briefly so it cannot flicker
        bool hintWarned;                 // the "not wired" warning prints once, not every frame
        string hintLast;                 // last line handed to the view, for the debug trace
        string dwellId;                  // informational step currently counting down
        float dwellLeft;                 // ...and how long it still has
        float firstSaleAt = -1f;         // when the player first sold something, so a rule can follow it

        void SetupHints()
        {
            hints = new List<Hint>
            {
                // ---- the opening loop, in order ----
                new Hint {
                    id = "dock_intro", sequence = true, dwellSeconds = 4f,
                    text = "This is the dock. Sell your catch, repair and upgrade here "
                         + "when you have fish and coin.",
                    // Nothing to do -- it just names the place, then stands aside for SET SAIL.
                    Show = () => mode == Mode.Harbor,
                    Learned = () => everSailed,
                },
                new Hint {
                    id = "set_sail", sequence = true,
                    text = "Tap SET SAIL to leave the dock.",
                    Show = () => mode == Mode.Harbor,
                    Learned = () => everSailed,
                },
                new Hint {
                    id = "steer_out", sequence = true,
                    text = "Tap the Left and Right arrows to take the ship out of the harbour.",
                    // Harbour water is a truce -- nothing bites inside it, so the first job is to leave.
                    Show = () => AtSea && GameCatalog.InSafeZone(boatX),
                    Learned = () => everSailed && !GameCatalog.InSafeZone(boatX),
                },
                new Hint {
                    id = "cast", sequence = true,
                    text = "Hold and move the FISH dial to control your hook.",
                    Show = () => AtSea && !GameCatalog.InSafeZone(boatX),
                    Learned = () => everCast,
                },
                new Hint {
                    id = "steer_hook", sequence = true,
                    text = "Hook onto a fish to drain it, until the fish is yours.",
                    // Not instantly: firing during the cast animation reads as a complaint, not a hint.
                    Show = () => mode == Mode.Fishing && hookTime > 1.5f,
                    Learned = () => hookEverMoved,
                },
                new Hint {
                    id = "dock_back", sequence = true,
                    text = "Sail back to a port and tap DOCK to go ashore.",
                    Show = () => AtSea && save.Data.cargo.Count > 0,
                    Learned = () => everDocked,
                },
                new Hint {
                    id = "sell", sequence = true,
                    text = "Tap SELL on a fish to sell it.",
                    Show = () => mode == Mode.Harbor && save.Data.cargo.Count > 0,
                    Learned = () => everSoldFresh,
                },
                new Hint {
                    id = "freshness", sequence = true, dwellSeconds = 5f,
                    text = "Only fresh fish fetch a good price. Once a fish rots it is worth nothing.",
                    // A beat after the first sale, not on top of it: the coins and the row sliding away
                    // are their own piece of feedback, and a rule landing in the middle of that is lost.
                    Show = () => mode == Mode.Harbor
                                 && firstSaleAt > 0f && Time.time >= firstSaleAt + 2f,
                    // Purely informational -- dwellSeconds retires it. Nothing to demonstrate.
                    Learned = () => false,
                },

                // ---- situational, once the opening loop is behind them ----
                new Hint {
                    id = "slow_obstacle",
                    text = "Slow down. Your speed has to be below the blue needle to pass without a scratch.",
                    // Fires on the approach to the FIRST obstacle, whatever the current speed. Waiting
                    // until the boat is already too fast teaches the rule at the moment it is too late to
                    // use it -- and obstacles are cleared by SLOWING, never by steering around them, which
                    // nothing on screen says.
                    Show = ApproachingObstacle,
                    Learned = () => everCrossedSafely,
                },
                new Hint {
                    id = "spoil", dwellSeconds = 4f,
                    // A WARNING, not the rule -- the rule is taught by the "freshness" step above. This
                    // one fires while it is actually happening, so it says what to do about it.
                    // It needs a dwell: with no action to wait for it would never retire, and a hint that
                    // never retires permanently starves every situational line below it.
                    text = "Your catch is turning. Make for a port before it spoils.",
                    Show = HasStaleFish,
                    Learned = () => false,
                },
                new Hint {
                    id = "depth_locked",
                    text = "That is as deep as this ship reaches. A new hull will take you further down.",
                    Show = () => mode == Mode.Fishing && save.Tier < GameCatalog.MaxShipTier
                                 && HookAtDepthLimit(),
                    Learned = () => save.Tier > 0,
                },
            };
            LogHintState();
        }

        /// <summary>One line at startup listing what is still to be taught, and what is already retired.</summary>
        void LogHintState()
        {
            if (!GameCatalog.debugStorage || hints == null) return;
            var todo = new List<string>();
            var done = new List<string>();
            for (int i = 0; i < hints.Count; i++)
                (Learned(hints[i].id) ? done : todo).Add(hints[i].id);
            Debug.Log($"[HINT] view wired={hintView != null}  mode={mode}"
                    + $"  |  still to teach ({todo.Count}): {string.Join(", ", todo)}"
                    + $"  |  already learned ({done.Count}): {string.Join(", ", done)}");
        }

        bool AtSea => mode == Mode.Sailing || mode == Mode.Night;

        bool Learned(string id) => save.Data.hintsSeen.Contains(id);

        void MarkLearned(string id)
        {
            if (Learned(id)) return;
            save.Data.hintsSeen.Add(id);
            save.Store();
        }

        /// <summary>
        /// Run the read-timer for a purely informational line. Returns false the moment it expires, so the
        /// caller can hand that frame to whatever comes next instead of leaving a retired line on screen.
        ///
        /// The clock only advances while the line is actually being shown -- a player who covers it with a
        /// panel does not lose the sentence they never read -- and it is unscaled, because the x3 speed
        /// cheat would otherwise flick a four-second line past in barely one.
        ///
        /// Lines that ask for an ACTION have no dwell and wait indefinitely.
        /// </summary>
        bool Dwell(Hint h)
        {
            if (h.dwellSeconds <= 0f) return true;
            if (dwellId != h.id) { dwellId = h.id; dwellLeft = h.dwellSeconds; }
            dwellLeft -= Time.unscaledDeltaTime;
            if (dwellLeft > 0f) return true;
            MarkLearned(h.id);
            return false;
        }

        void TickHints()
        {
            if (hints == null) return;
            if (hintView == null)
            {
                // Say so ONCE. Silence here is indistinguishable from "no hint applies right now", which
                // is exactly the confusion an unwired field produces.
                if (GameCatalog.debugStorage && !hintWarned)
                {
                    hintWarned = true;
                    Debug.LogWarning("[HINT] The hintView field on FishingGameController is EMPTY, so no "
                                   + "hint can appear. Drag the GameObject holding TutorialHintView into it.");
                }
                return;
            }

            // Retire anything the player has demonstrated, whether or not its line was ever on screen.
            for (int i = 0; i < hints.Count; i++)
                if (!Learned(hints[i].id) && hints[i].Learned()) MarkLearned(hints[i].id);

            string show = null;

            // The opening sequence has right of way, and only its FIRST unfinished step may speak.
            bool sequencePending = false;
            for (int i = 0; i < hints.Count; i++)
            {
                var h = hints[i];
                if (!h.sequence || Learned(h.id)) continue;
                sequencePending = true;
                if (h.Show() && Dwell(h)) { show = h.text; break; }
                if (Learned(h.id)) continue;   // its dwell just ran out -- give this frame to the next step
                break;
            }

            if (show == null && !sequencePending)
                for (int i = 0; i < hints.Count && show == null; i++)
                {
                    var h = hints[i];
                    if (h.sequence || Learned(h.id) || !h.Show()) continue;
                    if (Dwell(h)) show = h.text;
                }

            // A hazard slides past in a fraction of a second, so a line that vanished the instant its
            // condition lapsed would strobe. Once shown, hold it.
            if (show != null) hintHoldUntil = Time.time + 2.5f;
            else if (Time.time < hintHoldUntil) return;

            if (GameCatalog.debugStorage && show != hintLast)
            {
                hintLast = show;
                Debug.Log($"[HINT] {(show == null ? "(nothing to say)" : show)}   "
                        + $"| learned {save.Data.hintsSeen.Count}/{hints.Count}  mode={mode}");
            }
            hintView.Show(show);
        }

        /// <summary>
        /// Bearing down on an obstacle, at any speed.
        ///
        /// Deliberately NOT gated on already being too fast: the lesson is "slow down", and a player who
        /// only hears it once they are over the limit has no time left to act on it. The range is a shade
        /// wider than the one that reveals the safe-speed needle, so the words arrive with the marker they
        /// are talking about rather than after it.
        /// </summary>
        bool ApproachingObstacle()
        {
            if (!AtSea) return false;
            var field = GameCatalog.ObstacleField;
            for (int i = 0; i < field.Count; i++)
            {
                var o = field[i];
                if (o.hit) continue;                                   // already behind us this pass
                float gap = o.x - boatX;
                if (Mathf.Abs(gap) > 4f) continue;                     // not close enough to matter yet
                if (Mathf.Abs(boatSpeed) < .05f) continue;             // drifting: nothing is about to happen
                if (Mathf.Sign(gap) != Mathf.Sign(boatSpeed)) continue; // behind us, or we are backing off
                return true;
            }
            return false;
        }

        bool HasStaleFish()
        {
            for (int i = 0; i < save.Data.cargo.Count; i++)
                if (save.Freshness(save.Data.cargo[i], AbsHour) != "Fresh") return true;
            return false;
        }

        /// <summary>
        /// Hook resting on the floor, AND the floor is the band lock rather than the seabed. The
        /// distinction matters: over the continental shelf the hook also stops dead, but there a new ship
        /// would change nothing and the line would be a lie.
        /// </summary>
        bool HookAtDepthLimit()
        {
            float floorPx = MaxCastDepthU() * GameCatalog.DepthPx - HookReachBelowOrigin();
            if (hookOffset.y > -Mathf.Max(0f, floorPx) + 4f) return false;
            return SeaMap.UnlockedDepthU(save.Tier) <= SeaMap.SeabedDepthU(boatX) + .01f;
        }

        // --- called from the game loop when the player demonstrates something ---
        void HintsOnSail() { everSailed = true; }
        void HintsOnCast() { everCast = true; }
        void HintsOnHookMoved() { hookEverMoved = true; }
        void HintsOnDock() { everDocked = true; }
        void HintsOnSafeCrossing() { everCrossedSafely = true; }
        void HintsOnSale(IReadOnlyList<string> freshness)
        {
            if (freshness == null) return;
            if (firstSaleAt < 0f) firstSaleAt = Time.time;
            for (int i = 0; i < freshness.Count; i++)
                if (freshness[i] == "Fresh") { everSoldFresh = true; return; }
        }
    }
}
