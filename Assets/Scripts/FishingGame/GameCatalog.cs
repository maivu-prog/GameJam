using System;
using System.Collections.Generic;
using UnityEngine;

namespace RustyFishing
{
    [Serializable] public sealed class FishDef
    {
        public string id, name, art;
        public float hp, speed, size, aspect, minDepth, maxDepth, value, weight;
        public FishDef(string id, string name, string art, float hp, float speed, float size,
            float aspect, float minDepth, float maxDepth, float value, float weight)
        { this.id=id; this.name=name; this.art=art; this.hp=hp; this.speed=speed; this.size=size;
          this.aspect=aspect; this.minDepth=minDepth; this.maxDepth=maxDepth; this.value=value; this.weight=weight; }
    }

    [Serializable] public sealed class PortDef
    {
        public string id, name, art;
        public float x, radius;
        public bool upgrades;
        public Dictionary<string,float> prices;
        public PortDef(string id,string name,float x,bool upgrades,string art,float[] multipliers)
        { this.id=id; this.name=name; this.x=x; radius=6; this.upgrades=upgrades; this.art=art;
          prices=new Dictionary<string,float>(); for(int i=0;i<GameCatalog.Fish.Count;i++) prices[GameCatalog.Fish[i].id]=multipliers[i]; }
        // Used by GameDataLoader: build a port with an empty price table, then fill prices from game-data.json.
        public PortDef(string id,string name,float x,bool upgrades,string art)
        { this.id=id; this.name=name; this.x=x; radius=6; this.upgrades=upgrades; this.art=art; prices=new Dictionary<string,float>(); }
    }

    [Serializable] public sealed class ObstacleDef
    {
        public string id,name,art; public float x,safeSpeed,damage;
        public ObstacleDef(string id,string name,float x,float safeSpeed,float damage,string art)
        { this.id=id;this.name=name;this.x=x;this.safeSpeed=safeSpeed;this.damage=damage;this.art=art; }
    }

    // A sea zone: fish allowed to spawn, the spawn-rate multiplier and the HUD label — all data-driven.
    public sealed class ZoneDef
    {
        public readonly string name; public readonly float maxX, spawnRateMul; public readonly System.Func<string,bool> allows;
        public ZoneDef(string name,float maxX,float spawnRateMul,System.Func<string,bool> allows)
        { this.name=name; this.maxX=maxX; this.spawnRateMul=spawnRateMul; this.allows=allows; }
    }

    public static class GameCatalog
    {
        // NOTE: every value below is `static` (not const) so the Tuning Tool AND game-data.json (via
        // GameDataLoader reflection) can override it at runtime. Field names must match the JSON 'tuning' keys.
        public static float DaySeconds=180, NightSeconds=180, MaxSpeed=3.5f;
        // Speedometer needle mapping: angle(z) = SpeedNeedleStart - SpeedNeedleSweep * (speed/MaxSpeed).
        public static float SpeedNeedleStart=-40, SpeedNeedleSweep=240;
        public static float DisplaySpeedKn=8f;       // cosmetic base speed shown on the ENGINE upgrade card (kn at level 0)
        public static float SeaLength=120;           // recomputed from DockGap in LayoutDocks()
        public static float DockGap=37f;             // units between consecutive docks (config)
        public static float BoatAccel=1.25f, BoatDecel=1.6f;
        public static float HookSink=3.5f, HookSinkMax=5, HookUpForce=11, HookUpDrag=.5f;
        public static float HookRiseMax=5, HookHorizontal=5, HookRetract=20;
        public static float HookDamage=10, MaxDepth=50;
        public static float PixelsPerUnit=40;        // hook physics run in units then scale to px (demo uses 40)
        public static float HookLineSeconds=12, HookMaxDepthUnits=26;
        public static float WorldScrollPpu=42;       // px per sea-unit for scrolling ports/obstacles past the boat
        public static float PortCullPx=1600, ObstacleCullPx=1200; // hide art beyond this screen offset from the boat
        public static float FishSwimPpu=40;          // px per unit for fish horizontal swim
        public static float FishWanderPx=26;         // vertical bob amplitude while swimming
        public static float FishTurnChance=.4f;      // chance/sec a non-fleeing fish reverses direction
        public static float WeightMin=.6f, WeightMax=1.7f;   // per-fish weight multiplier range (drives HP & value)
        public static float WeightSizeToKg=4f;       // display kg = species.size * this * weightMul
        public static float FishSpawnHalfWidthPx=520;// fish spawn within +/- this (px) of centre
        public static float FishSurfaceY=250, FishDepthPx=26;// fishLayer-local y = FishSurfaceY - depthUnits*FishDepthPx
        public static float FishSizeScale=1f;        // global live multiplier on fish sprite size
        public static float LineWidthPx=7;           // fishing-line thickness
        public static float LineTrailMinDist=14;     // px between trail samples
        public static int LineTrailMaxPoints=90;     // cap on trail length (oldest dropped)
        public static float CollectSeconds=.5f;      // caught-fish tween-to-basket duration
        public static float SpawnBaseInterval=1.1f;  // base seconds between spawn attempts (divided by zone rate)
        public static int SpawnMaxActive=18;         // max fish alive at once
        // --- Economy (data-driven; read by PlayerSave; keys match game-data.json 'economy') ---
        public static int basketBaseCapacity=20, holdCapacityPerLevel=5, startHullHp=100, hullHpPerLevel=25, repairCostPerMissingHp=2;
        public static float engineSpeedPerLevel=.15f, hookDamagePerLevel=.25f, freshHours=24, staleHours=48, staleSellFactor=.5f;
        public static int[] UpgradeCosts={120,300,650};
        public static readonly List<FishDef> Fish = new()
        {
            new("bream","Coastal Bream","bream",8,1.2f,.6f,1.42f,2,14,10,1),
            new("sardine","Silver Sardine","sardine",6,2.4f,.48f,3.76f,2,12,8,1.3f),
            new("mackerel","Blue Mackerel","mackerel",16,2.1f,.82f,2.23f,4,20,22,1),
            new("barracuda","Barracuda","barracuda",25,1.8f,1,3.03f,8,28,35,1),
            new("red_snapper","Red Snapper","red_snapper",32,1.5f,1.05f,1.35f,10,30,48,.8f),
            new("lanternfish","Lanternfish","lanternfish",20,1.9f,.72f,1.74f,20,42,55,.75f),
            new("anglerfish","Anglerfish","anglerfish",60,2.4f,1.6f,1.56f,28,49,120,1),
            new("black_grouper","Black Grouper","black_grouper",85,1.15f,1.85f,1.65f,22,48,175,.5f),
            new("ghost_tuna","Ghost Tuna","ghost_tuna",110,2.8f,1.45f,1.91f,25,49,240,.25f),
        };
        public static readonly List<PortDef> Ports = new()
        {
            new("home","Home Harbor",6,true,"harbors-0",new[]{.75f,.7f,.8f,.8f,.9f,1.1f,.85f,1f,1.2f}),
            new("coral","Coral Harbor",38,false,"harbors-2",new[]{1.15f,1.25f,1.1f,1f,.8f,1.25f,.95f,1.1f,1.25f}),
            new("trade","Merchant Harbor",76,false,"harbors-3",new[]{1.4f,1.55f,1.45f,1.35f,1.25f,1.4f,1.2f,1.15f,1.35f}),
            new("frontier","Frontier Harbor",116,false,"harbors-1",new[]{1.8f,2f,1.85f,1.65f,1.7f,1.6f,1.5f,1.4f,1.25f}),
        };
        public static readonly List<ObstacleDef> Obstacles = new()
        {
            new("driftwood","Driftwood",25,2.4f,12,"obstacles-0"),
            new("shipwreck","Shipwreck",58,1.5f,30,"obstacles-1"),
            new("reef","Death Reef",96,.95f,48,"obstacles-2"),
        };
        public static readonly List<ZoneDef> Zones = new()
        {
            new("Coastal Waters",25,.2f,id=>id=="sardine"||id=="bream"||id=="mackerel"),
            new("Open Sea",65,.95f,id=>id!="black_grouper"&&id!="ghost_tuna"),
            new("Deep Sea",SeaLength+1,1.65f,id=>id!="bream"&&id!="sardine"),
        };
        public static ZoneDef ZoneAt(float x){foreach(var z in Zones)if(x<z.maxX)return z;return Zones[Zones.Count-1];}
        // Space the docks evenly by DockGap, drop obstacles between them, and resize the sea to fit.
        public static void LayoutDocks(){for(int i=0;i<Ports.Count;i++)Ports[i].x=6+i*DockGap;for(int i=0;i<Obstacles.Count&&i+1<Ports.Count;i++)Obstacles[i].x=(Ports[i].x+Ports[i+1].x)*.5f;SeaLength=Ports[Ports.Count-1].x+8;}
        public static FishDef GetFish(string id) => Fish.Find(f=>f.id==id);
        public static PortDef AtPort(float x) => Ports.Find(p=>Mathf.Abs(p.x-x)<=p.radius);
    }
}
