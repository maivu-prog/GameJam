using System;
using System.Collections.Generic;

namespace RustyFishing
{
    /// <summary>
    /// What a single line of a mission asks for. Kept as ONE flat row with a kind tag rather than a class
    /// per objective: every objective in the design is "count N of <kind>, filtered by target/harbour/
    /// threshold", and a flat row stays readable next to the mission it belongs to.
    /// </summary>
    [Serializable] public sealed class ObjectiveDef
    {
        /// <summary>Which event advances this line. See <see cref="ObjectiveKind"/>.</summary>
        public ObjectiveKind kind;

        /// <summary>Fish id, port id, obstacle id or upgrade branch — depending on kind. Empty = any.</summary>
        public string target = "";

        /// <summary>Port the event has to happen AT (selling, earning). Empty = anywhere.</summary>
        public string harbor = "";

        /// <summary>How many. For EarnCoinsFromSale this is coins; for UpgradePart it is the level to reach.</summary>
        public int count = 1;

        /// <summary>Kilograms for CatchWeight, percent (0..100) for ReturnWithHullAbove. Unused otherwise.</summary>
        public float threshold;

        /// <summary>Line shown on the card and the sea tracker. Written by hand — it is player-facing text.</summary>
        public string label = "";

        public ObjectiveDef() { }
        public ObjectiveDef(ObjectiveKind kind, string label, int count = 1,
                            string target = "", string harbor = "", float threshold = 0f)
        { this.kind = kind; this.label = label; this.count = count;
          this.target = target; this.harbor = harbor; this.threshold = threshold; }
    }

    /// <summary>
    /// The objective vocabulary. Deliberately small: these nine cover the whole twelve-mission story line,
    /// and every one of them maps onto an event the game ALREADY fires, so nothing has to poll.
    /// </summary>
    public enum ObjectiveKind
    {
        CatchSpecies,          // target = fish id (empty = any fish)
        CatchWeight,           // threshold = kg, target = fish id or empty
        SellFreshFish,         // sell fish still graded Fresh
        SellSpeciesAtHarbor,   // target = fish id, harbor = port id
        EarnCoinsFromSale,     // count = coins in ONE sale, harbor = port id or empty
        DockAtHarbor,          // target = port id
        CrossObstacleSafely,   // target = obstacle id, passed at or under its safe speed
        ReturnWithHullAbove,   // threshold = percent, checked on docking
        StayOffshoreAtNight,   // out of every port radius after dark, then dock again
        UpgradePart,           // target = branch, count = level to reach
    }

    /// <summary>One story mission: who hands it out, what it asks, what it pays, and what it says.</summary>
    [Serializable] public sealed class MissionDef
    {
        public string id, title, description;

        /// <summary>NPC id who hands it out — their portrait and start dialogue open the Ledger.</summary>
        public string giver = "";

        /// <summary>
        /// Port the mission is CLAIMED at. The player can finish the objectives anywhere, but the reward
        /// only lands when they dock here — that is what keeps the story tied to the map instead of
        /// firing off in open water.
        /// </summary>
        public string completionHarbor = "";

        public List<ObjectiveDef> objectives = new();
        public int rewardCoins;

        public string[] startDialogue = Array.Empty<string>();
        public string[] completeDialogue = Array.Empty<string>();

        /// <summary>Next mission in the chain. Empty = end of the line.</summary>
        public string nextId = "";
    }

    /// <summary>A speaking part. Portrait path is under Resources/Art/Characters/Narrative.</summary>
    [Serializable] public sealed class NpcDef
    {
        public string id, name, role, portrait;
        /// <summary>Port this NPC keeps. They speak the completion dialogue for missions claimed here.</summary>
        public string harbor;
        public NpcDef(string id, string name, string role, string portrait, string harbor)
        { this.id = id; this.name = name; this.role = role; this.portrait = portrait; this.harbor = harbor; }
    }

    /// <summary>
    /// "The Bell Below" — the story line, its cast, and the lookups the controller needs.
    ///
    /// Shape of the thing: ONE chain, one live mission at a time, claimed face to face at a named port.
    /// That is what the old questline was missing — it advanced silently wherever you happened to be, so
    /// the map never became a place you travel to for a reason. Here every mission ends at a person.
    ///
    /// SCOPE NOTE: missions 1-6 are authored. The remaining six of the twelve-mission design are left out
    /// on purpose — they need objective kinds (StayOffshoreAtNight, ReturnWithHullAbove) whose events are
    /// wired but unproven, and the first six already cover catching, freshness, market prices, obstacles,
    /// depth and the long haul. Adding the rest is pure data: append rows, set nextId.
    ///
    /// The full cast is registered even though only Mara, Elias and Nell hand out missions 1-6, because
    /// mission 6 is CLAIMED at Merchant Harbor and Silas has to be there to stamp it. Portraits for all
    /// six already exist in Resources.
    /// </summary>
    public static class MissionBook
    {
        // ── cast ─────────────────────────────────────────────────────────────────────────────────────
        public static readonly List<NpcDef> Npcs = new()
        {
            new("mara",  "Mara Vale",        "Home Harbormaster",         "mara-vale-portrait",     "home"),
            new("elias", "Elias Rook",       "Shipwright",                "elias-rook-portrait",    "home"),
            new("nell",  "Nell Sallow",      "Coral Fishmonger",          "nell-sallow-portrait",   "coral"),
            new("silas", "Silas Vane",       "Merchant Harbor Trader",    "silas-vane-portrait",    "trade"),
            new("ivo",   "Keeper Ivo",       "Frontier Lighthouse Keeper","keeper-ivo-portrait",    "frontier"),
            new("caller","The Drowned Caller","",                         "drowned-caller-portrait",""),
        };

        public static NpcDef Npc(string id)
        {
            foreach (var n in Npcs) if (n.id == id) return n;
            return null;
        }

        /// <summary>Who stamps a mission claimed at this port. Mara outranks Elias at Home.</summary>
        public static NpcDef NpcAtHarbor(string portId)
        {
            foreach (var n in Npcs) if (n.harbor == portId) return n;
            return null;
        }

        // ── the chain ────────────────────────────────────────────────────────────────────────────────
        // TEXT BUDGET — the panel is a phone-width parchment, so every string here is written to fit:
        //   description  ≤ 42 chars, ONE line
        //   objective    ≤ 24 chars (the tally "  0/3" is appended after it)
        //   dialogue     ≤ 52 chars per line, at most 3 lines (a 4th is DROPPED, not shown)
        // Longer than that and the block below it gets shoved into whatever sits underneath.
        // Dialogue is rendered with no quote marks and no blank line between lines, so write each entry
        // as a finished sentence — the panel stacks them straight on top of each other.
        public static readonly List<MissionDef> Missions = new()
        {
            new MissionDef {
                id = "fishers_morning", title = "A Fisher's Morning", giver = "mara",
                description = "Three bream, then home.",
                completionHarbor = "home", rewardCoins = 60, nextId = "before_it_spoils",
                objectives = {
                    new(ObjectiveKind.CatchSpecies, "Catch Coastal Bream", 3, target:"bream"),
                    new(ObjectiveKind.DockAtHarbor, "Dock at Home Harbor", 1, target:"home"),
                },
                startDialogue = new[] {
                    "Bream. Three of them. No wandering.",
                    "Back before dark — the sea remembers stragglers.",
                },
                completeDialogue = new[] {
                    "Three. You count better than the last one.",
                    "Rest. Tomorrow you learn what ice is for.",
                },
            },

            new MissionDef {
                id = "before_it_spoils", title = "Before It Spoils", giver = "mara",
                description = "Sell three fish still Fresh.",
                completionHarbor = "home", rewardCoins = 90, nextId = "coral_route",
                objectives = {
                    new(ObjectiveKind.SellFreshFish, "Sell Fresh fish", 3),
                },
                startDialogue = new[] {
                    "A fish is worth what it was an hour ago.",
                    "Sell three before they turn.",
                },
                completeDialogue = new[] {
                    "Fresh. That's a wage, not a beggar's cup.",
                    "Go south. Ask Nell why she overpays.",
                },
            },

            new MissionDef {
                id = "coral_route", title = "The Coral Route", giver = "mara",
                description = "Four sardine, sold at Coral Harbor.",
                completionHarbor = "coral", rewardCoins = 140, nextId = "wood_in_the_current",
                objectives = {
                    new(ObjectiveKind.SellSpeciesAtHarbor, "Sell sardine at Coral",
                        4, target:"sardine", harbor:"coral"),
                },
                startDialogue = new[] {
                    "Same fish, other water, other price.",
                    "Four sardine to Coral. Don't sell them here.",
                },
                completeDialogue = new[] {
                    "Sardine. Still cold from the shelf.",
                    "This one saw the light below. Look at its eyes.",
                },
            },

            new MissionDef {
                id = "wood_in_the_current", title = "Wood in the Current", giver = "elias",
                description = "Ease past driftwood, then home.",
                completionHarbor = "home", rewardCoins = 120, nextId = "a_light_beneath",
                objectives = {
                    new(ObjectiveKind.CrossObstacleSafely, "Pass driftwood safely", 1, target:"driftwood"),
                    new(ObjectiveKind.DockAtHarbor, "Dock at Home Harbor", 1, target:"home"),
                },
                startDialogue = new[] {
                    "Speed is cheap to buy and dear to spend.",
                    "Through the driftwood slow. Watch the needle.",
                },
                completeDialogue = new[] {
                    "Not a scratch. You listened.",
                    "Wood breaks. Steel bends. That wreck did neither.",
                },
            },

            new MissionDef {
                id = "a_light_beneath", title = "A Light Beneath", giver = "nell",
                description = "One Lanternfish from the deep.",
                completionHarbor = "coral", rewardCoins = 200, nextId = "merchants_price",
                objectives = {
                    new(ObjectiveKind.CatchSpecies, "Catch a Lanternfish", 1, target:"lanternfish"),
                },
                startDialogue = new[] {
                    "A fish down there carries its own lamp.",
                    "Ask what it needs to see. Bring me one.",
                },
                completeDialogue = new[] {
                    "Still lit. They stay lit for hours.",
                    "It swam toward the bell. They all do.",
                },
            },

            new MissionDef {
                id = "merchants_price", title = "The Merchant's Price", giver = "nell",
                description = "One 120c haul at Merchant Harbor.",
                completionHarbor = "trade", rewardCoins = 260, nextId = "",
                objectives = {
                    new(ObjectiveKind.EarnCoinsFromSale, "Earn 120c in one sale",
                        120, harbor:"trade"),
                },
                startDialogue = new[] {
                    "You fish close. Coin lives further out.",
                    "Make Silas pay 120 in a single sale.",
                },
                completeDialogue = new[] {
                    "A hundred and twenty. From a boat that size.",
                    "Fresh, stale, sacred or cursed — all have a price.",
                },
            },
        };

        public static MissionDef Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var m in Missions) if (m.id == id) return m;
            return null;
        }

        /// <summary>The mission a brand-new save starts on.</summary>
        public static string FirstId => Missions.Count > 0 ? Missions[0].id : "";

        /// <summary>1-based position in the chain, for "3 / 6" style readouts. 0 when unknown.</summary>
        public static int IndexOf(string id)
        {
            for (int i = 0; i < Missions.Count; i++) if (Missions[i].id == id) return i + 1;
            return 0;
        }

        public static int Count => Missions.Count;
    }
}
