using System;
using System.Collections.Generic;
using UnityEngine;

namespace RustyFishing
{
    [Serializable] public sealed class FishDef
    {
        public string id, name, art;
        public float hp, speed, size, aspect, minDepth, maxDepth, value, weight;
        // 1 = common everywhere, 5 = only worth hunting in the deep far zones. Drives SeaMap.SpawnWeight.
        public float rarity = 1f;
        // Night creatures. atk > 0 is what makes a species "evil": it only spawns after dark, it hunts the
        // boat, and it is worth a great deal at market. Everything else about it is an ordinary fish.
        public float atk;              // hull damage per strike
        public float chasePx;          // how close the boat must be before it comes for you (screen px)
        public float attackEvery = 4f; // seconds between its own strikes
        public int maxAlive;           // 0 = no limit; 1 = there is only ever one of these in the sea
        // Which stretch of the map this species lives in, 1..9. Depth already gates by band; this gates
        // by DISTANCE, so a creature can be locked to the far water even once its band is reachable.
        public int minZone = 1, maxZone = 9;
        public bool Evil => atk > 0f;

        public FishDef(string id, string name, string art, float hp, float speed, float size,
            float aspect, float minDepth, float maxDepth, float value, float weight, float rarity = 1f,
            float atk = 0f, float chasePx = 0f, float attackEvery = 4f, int maxAlive = 0,
            int minZone = 1, int maxZone = 9)
        { this.id=id; this.name=name; this.art=art; this.hp=hp; this.speed=speed; this.size=size;
          this.aspect=aspect; this.minDepth=minDepth; this.maxDepth=maxDepth; this.value=value; this.weight=weight;
          this.rarity=rarity; this.atk=atk; this.chasePx=chasePx; this.attackEvery=attackEvery; this.maxAlive=maxAlive;
          this.minZone=minZone; this.maxZone=maxZone; }
    }

    [Serializable] public sealed class PortDef
    {
        public string id, name, art;
        public float x, radius;
        // Every port buys fish. These two say which of the other two services it also offers.
        public bool upgrades, repair;
        public Dictionary<string,float> prices;
        public Dictionary<string,int> obstacleCounts = new();   // how many of each obstacle id spawn in this port's approach
        public PortDef(string id,string name,float x,bool upgrades,bool repair,string art,float[] multipliers)
        { this.id=id; this.name=name; this.x=x; radius=GameCatalog.PortRadius; this.upgrades=upgrades; this.repair=repair; this.art=art;
          prices=new Dictionary<string,float>(); for(int i=0;i<GameCatalog.Fish.Count&&i<multipliers.Length;i++) prices[GameCatalog.Fish[i].id]=multipliers[i]; }
        // Used by GameDataLoader: build a port with an empty price table, then fill prices from game-data.json.
        public PortDef(string id,string name,float x,bool upgrades,bool repair,string art)
        { this.id=id; this.name=name; this.x=x; radius=GameCatalog.PortRadius; this.upgrades=upgrades; this.repair=repair; this.art=art; prices=new Dictionary<string,float>(); }
        public string ServiceSummary => "Market" + (upgrades?" · Upgrade":"") + (repair?" · Repair":"");
        public int ObstacleCount(string id)=>obstacleCounts.TryGetValue(id,out var c)?c:0;
    }

    // Obstacle TYPE (no fixed position): ports say how many, safe speed is rolled in [min,max] per instance.
    [Serializable] public sealed class ObstacleDef
    {
        public string id,name,art; public float damage,safeSpeedMin,safeSpeedMax;
        public ObstacleDef(string id,string name,float damage,float safeSpeedMin,float safeSpeedMax,string art)
        { this.id=id;this.name=name;this.damage=damage;this.safeSpeedMin=safeSpeedMin;this.safeSpeedMax=safeSpeedMax;this.art=art; }
    }

    // A placed obstacle in the sea (generated from ports × obstacle counts).
    public sealed class ObstacleInstance { public ObstacleDef def; public float x, safeSpeed; public bool hit; }

    // A sea zone: fish allowed to spawn, the spawn-rate multiplier and the HUD label — all data-driven.
    public static class GameCatalog
    {
        // NOTE: every value below is `static` (not const) so the Tuning Tool AND game-data.json (via
        // GameDataLoader reflection) can override it at runtime. Field names must match the JSON 'tuning' keys.
        public static float DaySeconds=180, NightSeconds=105, MaxSpeed=6.5f;   // night length is set per hull tier
        // Speedometer needle mapping: angle(z) = SpeedNeedleStart - SpeedNeedleSweep * (speed/MaxSpeed).
        public static float SpeedNeedleStart=-40, SpeedNeedleSweep=240;
        public static float DisplaySpeedKn=8f;       // cosmetic base speed shown on the ENGINE upgrade card (kn at level 0)
        public static float SeaLength=120;           // recomputed from DockGap in LayoutDocks()
        // Port BASE line (waterline) in Y. Ports use a bottom-centre pivot (0.5, 0), so anchoredPosition.y
        // is their bottom edge and every port — whatever its height — rests its base on this line. Tune via
        // the "portY" slider to slide all ports up/down together.
        public static float PortY=247.289f;
        public static float DockGap=78f;             // units between consecutive docks — tuned for a 10-port map
        // Half-width of a harbour, in sea-units. Drives THREE things at once: when the DOCK button appears,
        // how wide the night safe zone is, and how much water around a port stays free of fish + obstacles.
        public static float PortRadius=12f;
        public static float BoatAccel=2.3f, BoatDecel=3.0f;
        public static float HookSink=3.5f, HookSinkMax=5, HookUpForce=11, HookUpDrag=.5f;
        public static float HookRiseMax=5, HookHorizontal=5, HookRetract=30;
        public static float HookDamage=10, MaxDepth=50;
        // Body-based catch: a fish is bitten when the hook shank comes within (its body radius +
        // HookCatchRadius) of its centre, where body radius = FishWidthPx * FishHitFraction. This makes the
        // visible hook/barb touching the fish BODY register damage, instead of only a hit dead-on its centre
        // (the old point-distance test needed the shank to reach the middle of a ~250px fish, so it felt like
        // only the line-end connected). HookCatchRadius is just a small flat reach beyond the body for feel.
        public static float HookCatchRadius=10, FishHitFraction=.4f;
        // The hook must be MOVING at least this fast (px/s) to deal damage. Kills the "sink to the floor and
        // hold" exploit: a parked hook is harmless, so you must actively rub it across a fish to catch.
        public static float HookBiteMinSpeed=55f;
        // Floor under a species' size so the tiniest fish don't render/hit too small. 2x the smallest (sardine 0.48).
        public static float MinFishSize=.96f;

        /// <summary>
        /// Minimum gap between two fish when they spawn, as a multiple of their combined half-widths.
        /// 1 = just touching; above that leaves clear water between them.
        ///
        /// Nothing used to stop two fish landing on the same spot, and a stack of them read as a bug: the
        /// hook kills a sardine in about half a second, so a pile in one place emptied itself into the hold
        /// in a couple of seconds and looked like one catch worth three fish.
        /// </summary>
        public static float fishSpawnSpacing=1.25f;

        /// <summary>Temporary: log every catch and every storage repaint. Turn off when the count is trusted.</summary>
        public static bool debugStorage=false;
        // Verbose fishing log: prints, ~5x/sec while a fish is in reach, the hook speed / gate / depth-floor
        // state and the fish HP, so a phantom catch can be traced. Turn off once the bug is understood.
        public static bool debugFishing=false;
        public static float PixelsPerUnit=40;        // hook physics run in units then scale to px (demo uses 40)
        public static float HookLineSeconds=12, HookMaxDepthUnits=46;   // reach cap; the band gate is usually stricter
        public static float WorldScrollPpu=42;       // px per sea-unit for scrolling ports/obstacles past the boat
        public static float PortCullPx=1600, ObstacleCullPx=760; // hide art beyond this screen offset from the boat
        // (760px ≈ 18 units: keeps obstacles just past the safe start hidden until you sail toward them.)
        public static float FishSwimPpu=40;          // px per unit for fish horizontal swim
        public static float FishWanderPx=26;         // vertical bob amplitude while swimming
        public static float FishTurnChance=.4f;      // chance/sec a non-fleeing fish reverses direction
        public static float WeightMin=.6f, WeightMax=1.7f;   // per-fish weight multiplier range (drives HP & value)
        public static float WeightSizeToKg=4f;       // display kg = species.size * this * weightMul
        public static float FishSpawnHalfWidthPx=520;// (legacy) fish spawn within +/- this (px) of centre
        public static float FishRoamHalfWidthPx=220; // how far a fish wanders left/right from its home spot (px)
        public static float FishCullPx=780;          // hide a fish once it scrolls beyond +/- this screen offset
        public static float FishFieldDensity=.16f;   // fish per sea-unit PER REGION (x the zone and band multipliers)
        public static int FishFieldMax=220;          // hard cap on total fish alive across all 24 regions
        // Px per depth unit — the ONE vertical scale, shared by the hook and the fish so a fish at depth d
        // and a hook at depth d land on the same screen line. Recomputed from SeaMap on every hull tier, so
        // ascending zooms the water column out. NOT a game-data.json key: it is derived, not authored.
        public static float DepthPx=52f;
        public static float FishSizeScale=1f;        // global live multiplier on fish sprite size
        public static float LineWidthPx=13.353f;     // fishing-line thickness
        public static float HookScale=1.261f;        // hook sprite scale multiplier
        public static float LineTrailMinDist=14;     // desired px length of each rendered line segment
        public static int LineTrailMaxPoints=90;     // maximum rendered segments
        public static float LineSagPx=0f;            // gravity/sag strength on the trailing fishing line
        public static float CollectSeconds=.5f;      // caught-fish tween-to-basket duration
        // Night is meant to be a threat you manage, not a wall of fish. The hunters spawn at a fraction
        // of the daytime density on top of the usual zone/band multipliers.
        public static float EvilDensityMul=.15f;

        /// <summary>
        /// How many of one night species may be alive at once, by zone. Zone 1 gets exactly one -- the
        /// first night out is a single silhouette, not a swarm -- and the pack thickens with distance.
        ///
        /// This is a head COUNT, not a density: density alone could never hold the early game down,
        /// because whatever the field held, the player simply fished it out and the field refilled. A cap
        /// bounds what a whole night can produce, which is the only thing that stopped night income
        /// compounding away the first half of the run.
        ///
        /// A species with its own maxAlive (the kraken is meant to be unique) keeps the lower of the two.
        /// </summary>
        public static float evilAliveZone1=1f, evilAlivePerZone=.25f;

        /// <summary>
        /// Seconds before a night species may spawn again after one appears. The head count alone was not
        /// enough: "only one alive at a time" still lets a whole night be farmed if the replacement arrives
        /// the moment you reel the last one in. This is what makes the pack SLOW to come back, and it is
        /// the number that keeps night income from compounding away the first half of the run.
        ///
        /// Roughly one night length, so home waters yield about one hunter a night and the far zones --
        /// which are allowed more alive at once -- yield two or three.
        /// </summary>
        public static float evilRespawnSeconds=120f;

        public static int EvilAliveAt(FishDef def,int zoneIndex){
            int ramp=Mathf.Max(1,Mathf.RoundToInt(evilAliveZone1+(zoneIndex-1)*evilAlivePerZone));
            return def!=null&&def.maxAlive>0?Mathf.Min(def.maxAlive,ramp):ramp;
        }
        public static float PhaseFadeSeconds=1.4f;   // dusk/dawn crossfade for the whole fish field
        public static float BiteMinGap=.55f;         // minimum seconds between two hull bites landing
        // --- Night hunter attack run (see FishActor) ---
        public static float HuntRiseSpeedMul=6f;     // x normal swim speed while charging the hull
        public static float HuntDiveSpeedMul=.8f;    // x normal swim speed while retreating to depth
        public static float HuntStrikeRangeX=150f;   // horizontal window it must be inside to bite
        public static float HuntStrikeRangeY=48f;    // vertical window — keep tight or it bites from below
        public static float HuntStandoffPx=55f;      // how far under the hull it stops; raise if it overlaps
        public static float SpawnBaseInterval=1.1f;  // base seconds between spawn attempts (divided by zone rate)
        public static int SpawnMaxActive=18;         // max fish alive at once
        // --- Economy (data-driven; read by PlayerSave; keys match game-data.json 'economy') ---
        public static int basketBaseCapacity=18, repairCostPerMissingHp=2, startHullHp=100;
        // Per LEVEL, and there are 12 of them per branch now. The end of the tree lands close to where the
        // old 6/7-level curve did — the same total power, just handed out in twice as many, smaller steps.
        public static float engineSpeedPerLevel=.045f, hookDamagePerLevel=.30f, hookSpeedPerLevel=.08f;
        public static float holdCapacityPerLevel=1.8f, hullHpPerLevel=5f;
        public static float freshHours=24, staleHours=48, staleSellFactor=.5f;
        // Night catches spoil on their own, much shorter clock. A day is 12 in-game hours and a night is
        // 12 more, so 12 hours means a night haul must be sold before the NEXT dusk -- you have exactly
        // one day to get it to a port. That is the cost of the night prices: they stay high as the reward
        // for going out, and the spoilage is what stops a player banking a hold of kraken indefinitely.
        public static float nightFreshHours=6, nightStaleHours=12;
        public static float contractRewardMul=2.2f;   // daily-order reward = fish.value * target * this

        // ---- Ship tiers ----------------------------------------------------------------------------
        // Four branch levels per tier. Max every branch inside a tier and the UPGRADE button becomes NEW
        // SHIP: new hull art, a flat bump to every stat, the four dots reset, and the next band of water
        // opens. Tiers are lettered A/B/C to match the depth band each one unlocks.
        public const int LevelsPerTier=4, MaxShipTier=3;
        public static readonly string[] ShipTierLetters={"A","B","C","D"};
        public static int[] NewShipCosts={290,2080,14925};

        // What one New Ship adds on top of the branch levels — "a little of everything".
        // Hull as ARMOUR, in the divide form (NOTE-game-balance-math.md 2.2): damage/(1+armor), never
        // damage-armor. Two reasons. The subtractive form silently kills fast light hits -- a swarm of
        // small night bites would round to nothing while one big obstacle still lands -- and, more
        // pressingly, raw +MaxHp did NOTHING for the hull branch: repairs are billed per missing HP, so
        // taking 40 damage cost 80 coins whether the hull held 100 or 220. Armour is what actually makes
        // the branch pay for itself. Each point is roughly +6% effective HP, as in Warcraft III.
        public static float hullArmorPerLevel=.06f, tierArmorBonus=.20f;
        public static float tierDamageBonus=.25f, tierHookSpeedBonus=.10f, tierBoatSpeedBonus=.08f;
        public static float tierCapacityBonus=3.5f, tierHullHpBonus=20f;

        /// <summary>Highest branch level allowed at this ship tier: 4, 8, then 12.</summary>
        public static int LevelCapFor(int shipTier)=>Mathf.Clamp(shipTier+1,1,MaxShipTier)*LevelsPerTier;

        /// <summary>Cost of the next hull, or -1 when the final ship is already afloat.</summary>
        public static int NewShipCost(int shipTier)
            => shipTier>=0 && shipTier<NewShipCosts.Length ? NewShipCosts[shipTier] : -1;
        // Upgrade cost per branch, one entry per level (data-driven; keys = branch id, from game-data.json 'upgrades').
        // Twelve steps per branch, and the four branches are deliberately NOT priced alike.
        // A new ship needs every branch at its cap, so the total is fixed however you order the
        // buys -- with one shared curve there was no decision left at all. Price tracks payback:
        // hook turns straight into income, engine into fishing time, hold saves return trips, and
        // hull only pays off at night, so it is the cheap one you can defer. Sums to the same 8200
        // the session balance was tuned against.
        // Fitted by tools/fit_costs.py against measured income, not hand-picked. The rule from
        // NOTE-game-balance-math.md 1.1 is that cost must outrun income or no wall ever forms -- but the
        // note's r = 1.07..1.15 assumes ADDITIVE income, and this game's income MULTIPLIES: deeper bands
        // and further zones raise the value of every catch, about x18 end to end. At the old r = 1.256
        // income won outright and a 90-minute run finished with millions of unspendable coins.
        // r = 1.26 with these bases is the gentlest curve that still forms a wall: measured at 88-94% of
        // cycles buying something, longest dry spell one cycle, so the climb never stalls.
        // Branch bases differ by payback: hook turns into income, engine into fishing time, hold saves
        // trips, hull only pays at night.
        // Fitted by tools/fit_costs.py, not hand-picked -- see NOTE-game-balance-math.md 1.1.
        // The note's r = 1.07..1.15 assumes ADDITIVE income; this game's income MULTIPLIES (deeper bands
        // and further zones raise the value of every catch, about x18 end to end), so copying that number
        // would let income outrun cost and no wall would form at all. At the old flat r = 1.256 a
        // 90-minute run ended holding 3.7 MILLION unspendable coins.
        //
        // r = 1.29 is the gentlest curve that still forms one. Measured across 75% / 60% / 50% play:
        // every skill level keeps buying (longest dry spell 3 cycles, and only for the weakest player),
        // and even a 60% player finishes all twelve levels inside the session.
        // Branch bases differ by payback: hook becomes income, engine buys fishing time, hold saves
        // return trips, hull only pays off at night -- so hull is the cheap one you can defer.
        public static Dictionary<string,int[]> UpgradeCosts = new(){
            {"hook",new[]{55,75,95,120,155,200,260,335,430,555,720,925}},
            {"hold",new[]{45,55,70,90,120,155,195,255,330,425,545,705}},
            {"engine",new[]{45,60,80,100,130,170,220,280,360,465,605,780}},
            {"hull",new[]{35,45,55,70,95,120,155,200,260,335,430,555}},
        };
        /// <summary>
        /// Per-branch milestone levels. Reaching one multiplies THAT branch's accumulated bonus by
        /// <see cref="MilestoneGain"/>.
        ///
        /// The point is the stagger, not the bonus (NOTE-game-balance-math.md 1.5): value-for-money decays
        /// as costs climb, so without these every branch slides downhill forever and the player stops
        /// choosing. A milestone makes a branch worth buying again -- but only if the four lists DISAGREE.
        /// Put every milestone on the same level and all four branches jump together, the ranking never
        /// changes, and the mechanic may as well not exist.
        ///
        /// Deliberately no milestone at 4, 8 or 12: those are the New Ship steps, which already jump every
        /// stat at once. Landing a branch milestone there would hide it inside the bigger jump.
        /// </summary>
        public static Dictionary<string,int[]> UpgradeMilestones = new(){
            {"hook",new[]{3,9}},
            {"hold",new[]{2,7}},
            {"engine",new[]{5,10}},
            {"hull",new[]{6,11}},
        };
        public static float MilestoneGain=1.5f;

        /// <summary>Multiplier on a branch's bonus at this level, from the milestones already passed.</summary>
        public static float MilestoneMul(string branch,int level){
            if(!UpgradeMilestones.TryGetValue(branch,out var marks))return 1f;
            float m=1f;
            for(int i=0;i<marks.Length;i++)if(level>=marks[i])m*=MilestoneGain;
            return m;
        }

        public static readonly List<FishDef> Fish = new()
        {
            new("bream","Coastal Bream","bream",8,1.2f,.6f,1.42f,2,14,16,1,1f),
            new("sardine","Silver Sardine","sardine",6,2.4f,.48f,3.76f,2,12,14,1.3f,1f),
            new("mackerel","Blue Mackerel","mackerel",16,2.1f,.82f,2.23f,4,24,30,1,2f),
            new("barracuda","Barracuda","barracuda",25,1.8f,1,3.03f,10,28,46,1,2f),
            new("red_snapper","Red Snapper","red_snapper",32,1.5f,1.05f,1.35f,14,30,62,.8f,3f),
            new("lanternfish","Lanternfish","lanternfish",20,1.9f,.72f,1.74f,18,40,75,.75f,3f),
            new("anglerfish","Anglerfish","anglerfish",60,2.4f,1.6f,1.56f,28,44,140,1,4f),
            new("black_grouper","Black Grouper","black_grouper",85,1.15f,1.85f,1.65f,24,42,185,.5f,4f),
            new("ghost_tuna","Ghost Tuna","ghost_tuna",110,2.8f,1.45f,1.91f,32,44,250,.25f,5f),
            // ---- night creatures (atk > 0). One per depth band; they replace the shoals after dusk. ----
            // HP is 1.7x what a day fish of comparable size carries, and EvilDensityMul thins the field to
            // a third of what it was. The two trade against each other -- tougher fish or fewer of them buy
            // the same slowdown -- and this split favours fewer, because piling HP on lengthened every
            // fight for weaker players too, who were already the slowest to finish. Both exist to pay for the prices below, which are deliberately NOT on the day scale
            // -- a night fish is the reward for going out after dark, so it stays worth 7x its daylight
            // equivalent and is made expensive in TIME and RISK instead of in coins. Night catches also rot
            // in 12 hours (nightStaleHours), so the haul has to reach a port before the next dusk.
            //
            // Band A keeps its piranha, but ONE of them in zone 1 -- see EvilAliveAt. Night income arriving
            // in the first minutes is what used to compound through the whole run, and no amount of
            // thinning, tougher fish or repair cost could offset it. A hard head-count does: home waters
            // hold a single hunter, and the pack grows the further out you sail.
            //     id            name             art          hp  speed size  aspect  minD maxD value  wt rarity  atk chase  every alive
            //     id            name             art          hp  speed size  aspect  minD maxD value  wt rarity  atk chase  every alive  zones
            // Depth windows OVERLAP the band edges, the way five of the nine day species already do.
            // Cut to exactly one band each, the night read as three flat shelves with nothing between
            // them -- and it left band C empty after dark everywhere outside the kraken's zones 8-9.
            new("piranha","Blood Piranha","piranha",           34,2.9f,.80f,3.03f,  0, 20,110,  1f, 1f,   4f,  760f, 3.2f, 0, 1, 9),
            new("night_shark","Night Shark","night_shark",    136,2.3f,1.75f,1.91f,12, 34,320,  1f, 2f,  11f, 1250f, 4.5f, 0, 3, 9),
            // The tentacles. SIX of them, and each one is an ordinary catchable fish -- that is the whole
            // trick: hooking, health bars, biting and selling all already work, so the set piece needs no
            // machinery of its own. Severing one is worth more than anything else in the sea.
            // Not spawned by the ambient field like the other two; KrakenEvent brings the whole arm in at
            // once, after a warning. See FishingGameController.KrakenEvent.cs.
            //
            // 300 hp x 6 with 34 atk is not a guess: simulated against the real line timer and armour, it
            // is the point where a FINISHED ship wins with about 30% hull left and every lesser one sinks.
            // At 520 even a maxed ship went down after four tentacles, which is a locked door, not a boss.
            // Hook alone loses and hull alone loses -- the arm asks for both.
            new("kraken","The Drowned One","kraken",          300,1.7f,8.00f, .75f,28, 44,1100,  1f, 3f,  34f, 4000f, 5.0f, 6, 8, 9),
        };
        public static readonly List<PortDef> Ports = new()
        {
            new("home","Home Harbor",6,true,true,"harbors-0",new[]{0.88f,0.88f,0.88f,0.88f,0.88f,0.88f,0.88f,0.88f,0.88f,0.88f,0.88f,0.88f}),
            new("shoal","Shoal Landing",44,false,false,"harbors-2",new[]{0.94f,0.94f,0.95f,0.95f,0.95f,0.95f,0.95f,0.95f,0.95f,0.95f,0.95f,0.95f}),
            new("kelp","Kelp Quay",88,false,true,"harbors-1",new[]{1.01f,1.01f,1.01f,1.01f,1.01f,1.02f,1.02f,1.02f,1.02f,1.02f,1.03f,1.03f}),
            new("coral","Coral Harbor",130,false,false,"harbors-2",new[]{1.07f,1.07f,1.08f,1.08f,1.08f,1.09f,1.09f,1.09f,1.09f,1.1f,1.1f,1.1f}),
            new("midway","Midway Anchorage",176,true,true,"harbors-3",new[]{1.13f,1.14f,1.14f,1.15f,1.15f,1.15f,1.16f,1.16f,1.17f,1.17f,1.17f,1.18f}),
            new("reef","Reef Station",218,false,false,"harbors-2",new[]{1.2f,1.2f,1.21f,1.21f,1.22f,1.22f,1.23f,1.23f,1.24f,1.24f,1.25f,1.25f}),
            new("trade","Merchant Harbor",264,false,true,"harbors-1",new[]{1.26f,1.27f,1.27f,1.28f,1.28f,1.29f,1.3f,1.3f,1.31f,1.31f,1.32f,1.33f}),
            new("lantern","Lantern Wharf",306,false,false,"harbors-2",new[]{1.32f,1.33f,1.34f,1.34f,1.35f,1.36f,1.37f,1.37f,1.38f,1.39f,1.39f,1.4f}),
            new("abyss","Abyss Gate",352,true,true,"harbors-3",new[]{1.39f,1.39f,1.4f,1.41f,1.42f,1.43f,1.44f,1.44f,1.45f,1.46f,1.47f,1.48f}),
            new("frontier","Frontier Harbor",398,false,true,"harbors-1",new[]{1.45f,1.46f,1.47f,1.48f,1.49f,1.5f,1.5f,1.51f,1.52f,1.53f,1.54f,1.55f}),
        };
        public static List<ObstacleDef> Obstacles = new()
        {
            new("driftwood","Driftwood",12,3.7f,5.2f,"obstacles-0"),
            new("shipwreck","Shipwreck",30,2.2f,3.3f,"obstacles-1"),
            new("reef","Death Reef",48,1.3f,2.2f,"obstacles-2"),
        };
        // Obstacles actually placed in the sea, generated from ports × their obstacle counts.
        public static readonly List<ObstacleInstance> ObstacleField = new();
        // Fish thin out within this many sea-units beyond a port's dock radius (0 at the dock edge, full
        // density once you're this far past it) — so there are few fish right by any harbor.
        public static float FishPortSparseRadius=14f;
        // 0..1 chance a fish placed at world x is kept, based on how far x is from the NEAREST port.
        public static float PortDensityFactor(float x){
            float best=1f;
            foreach(var p in Ports){float f=Mathf.Clamp01((Mathf.Abs(x-p.x)-p.radius)/Mathf.Max(.01f,FishPortSparseRadius));if(f<best)best=f;}
            return best;
        }
        // Dock spacing is randomised: each gap is DockGap * a per-gap multiplier in [1, DockGapVarMax], so
        // gaps vary a lot but never fall below DockGap. Multipliers are rolled ONCE (stable across DockGap
        // slider drags) — call ReseedDockGaps() to roll a fresh pattern.
        public static float DockGapVarMax=1.25f;   // jitter only now — zone.gapMul carries the pattern
        static float[] dockGapMul;
        public static void ReseedDockGaps(){dockGapMul=new float[Ports.Count];for(int i=0;i<dockGapMul.Length;i++)dockGapMul[i]=UnityEngine.Random.Range(1f,Mathf.Max(1f,DockGapVarMax));}
        // Space the docks and resize the sea to fit (obstacles regenerate). Each gap is
        // DockGap x the zone's own gapMul x a small random jitter: the SHAPE of the map (tight at home,
        // widest at zone 7, easing back in by zone 9) is authored, and only the wobble is rolled.
        public static void LayoutDocks(){
            if(dockGapMul==null||dockGapMul.Length!=Ports.Count)ReseedDockGaps();
            for(int i=0;i<Ports.Count;i++){
                if(i==0){Ports[i].x=6f;continue;}
                float shape=i-1<SeaMap.Zones.Count?SeaMap.Zones[i-1].gapMul:1f;   // gap i spans zone i
                Ports[i].x=Ports[i-1].x+DockGap*shape*dockGapMul[i];
            }
            SeaLength=Ports[Ports.Count-1].x+8;GenerateObstacleField();
            LogLayout();
        }
        // One line per dock so the spacing that actually came out is readable, instead of being inferred
        // from how far apart two harbours look on a zoomed-out Scene view.
        public static bool LogDockLayout=true;
        static void LogLayout(){
            if(!LogDockLayout)return;
            var sb=new System.Text.StringBuilder("[Docks] gap=").Append(DockGap).Append(" var=").Append(DockGapVarMax)
                     .Append(" radius=").Append(PortRadius).AppendLine();
            for(int i=0;i<Ports.Count;i++){
                float gap=i==0?0f:Ports[i].x-Ports[i-1].x;
                sb.Append($"  D{i+1} {Ports[i].id,-9} x={Ports[i].x,7:0.0}");
                if(i>0)sb.Append($"  vung {i}: rong {gap,6:0.0}u, nuoc ngoai safe-zone {gap-2*PortRadius,6:0.0}u");
                sb.AppendLine();
            }
            sb.Append($"  SeaLength {SeaLength:0.0}u");
            UnityEngine.Debug.Log(sb.ToString());
        }

        // No obstacles spawn before this world x — the first stretch of sea (the coastal starting zone) is a
        // safe area with no hazards. Keep it in sync with the first zone's maxX.
        public static float ObstacleFreeUntilX=45f;
        // Minimum world-unit spacing between any two obstacles, so they never overlap on screen.
        public static float ObstacleMinGap=9f;
        static bool ObstacleSpotFree(float x){foreach(var o in ObstacleField)if(Mathf.Abs(o.x-x)<ObstacleMinGap)return false;return true;}
        // Scatter each port's obstacle counts randomly in the water approaching that port; roll safe speed in
        // [min,max]. Rejection-sample each x so obstacles keep at least ObstacleMinGap apart (no overlap).
        public static void GenerateObstacleField(){
            ObstacleField.Clear();
            for(int i=0;i<Ports.Count;i++){
                var p=Ports[i];
                float lo=i>0?Ports[i-1].x+Ports[i-1].radius:0f, hi=p.x-p.radius;
                lo=Mathf.Max(lo,ObstacleFreeUntilX);   // keep the starting zone obstacle-free
                if(hi<=lo)continue;
                foreach(var def in Obstacles){
                    int count=p.ObstacleCount(def.id);
                    for(int k=0;k<count;k++){
                        float x=float.NaN;
                        for(int t=0;t<24;t++){float cand=UnityEngine.Random.Range(lo,hi);if(ObstacleSpotFree(cand)){x=cand;break;}}
                        if(float.IsNaN(x))continue; // no room without overlapping — skip this obstacle
                        ObstacleField.Add(new ObstacleInstance{def=def,x=x,safeSpeed=UnityEngine.Random.Range(def.safeSpeedMin,def.safeSpeedMax)});
                    }
                }
            }
        }
        static void DefCount(string portId,params (string id,int n)[] counts){var p=Ports.Find(x=>x.id==portId);if(p==null)return;foreach(var c in counts)p.obstacleCounts[c.id]=c.n;}
        // Hazards thicken the further out you sail. Counts are per the water approaching that port.
        // Hazards ramp from a single log in zone 1 up to the full field at zone 9. Zones 1-6 were
        // front-loaded: a reef (48 dmg) in zone 3 against a 100 HP hull read as a wall, not a warning.
        // Totals stay between 1 and 6 across the whole map. The danger curve is carried by WHICH
        // hazards appear (driftwood 12 dmg -> shipwreck 30 -> reef 48), not by piling on more of them:
        // a wall of nine obstacles reads as noise, three reefs reads as a reason to slow down.
        static GameCatalog(){
            DefCount("shoal",("driftwood",1));
            DefCount("kelp",("driftwood",2));
            DefCount("coral",("driftwood",1),("shipwreck",1));
            DefCount("midway",("driftwood",2),("shipwreck",1));
            DefCount("reef",("driftwood",1),("shipwreck",1),("reef",1));
            DefCount("trade",("driftwood",2),("shipwreck",1),("reef",1));
            DefCount("lantern",("driftwood",1),("shipwreck",2),("reef",2));
            DefCount("abyss",("shipwreck",3),("reef",2));
            DefCount("frontier",("shipwreck",2),("reef",4));
        }
        public static FishDef GetFish(string id) => Fish.Find(f=>f.id==id);
        public static PortDef AtPort(float x) => Ports.Find(p=>Mathf.Abs(p.x-x)<=p.radius);
        public static PortDef PortById(string id) => Ports.Find(p=>p.id==id) ?? Ports[0];

        // Harbour water is a truce: nothing bites the hull, the hook catches nothing, and the hunters will
        // not cross in. Deliberately the SAME radius that raises the DOCK button, so the safe line is the
        // line the player can already see.
        public static bool InSafeZone(float x) => AtPort(x) != null;

        /// <summary>Nudge a world x out to the edge of any harbour it has strayed inside.</summary>
        public static float PushOutOfPorts(float x)
        {
            foreach (var p in Ports)
            {
                float d = x - p.x;
                if (Mathf.Abs(d) < p.radius) return p.x + (d >= 0f ? p.radius : -p.radius);
            }
            return x;
        }
    }
}
