using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RustyFishing
{
    [Serializable] public sealed class CaughtFish { public string id; public float caughtHour; public float wmul=1f; public CaughtFish(string id,float h,float w){this.id=id;caughtHour=h;wmul=w;} }
    [Serializable] public sealed class SaveData
    {
        public int version=1, coins=0, day=1, hullHp=100;
        public int hookLevel=0, holdLevel=0, engineLevel=0, hullLevel=0;
        // Which hull you are sailing: 0=A, 1=B, 2=C, 3=final. Gates the depth bands and caps how far the
        // four branches may be levelled, and every New Ship adds a flat bump on top of them.
        public int shipTier=0;
        // Which port the player was last at, so Continue resumes there instead of Home Harbor.
        public string lastPortId="home";
        // Seconds into the current day/night cycle, so Continue resumes the same time of day, not dawn.
        public float phaseTime=0f;
        // Boat position along the sea, so Continue resumes exactly where you were, not the last port.
        public float boatX=6f;
        // JSON snapshot of this whole save as it was at the current day's dawn. Sinking restores it, so a
        // lost run costs the day rather than your progress. Persisted so it survives Continue.
        public string dayStart="";
        // Story missions ("The Bell Below"). One live mission at a time; missionProgress has one tally per
        // objective of that mission, so a two-line mission carries two counters. missionId "" on a fresh
        // save means "not handed out yet" — the first harbour visit gives it.
        public string missionId="";
        // A mission is OFFERED when handed out but only ACTIVE (tracks progress, shows the sea note) once the
        // player presses ACCEPT in the Ledger. Kept per-save so an offered-not-accepted mission survives a quit.
        public bool missionAccepted=false;
        // Whether the player has OPENED the Ledger for the current offer. New+unseen → the MISSIONS button
        // breathes; seen-but-not-accepted → it shows a warning icon.
        public bool missionSeen=false;
        public List<int> missionProgress=new();
        public List<string> missionsDone=new();
        // Set the moment every objective is met; cleared on claim. Stored rather than derived so the
        // READY stamp survives a quit and the player does not have to re-trigger the final objective.
        public bool missionReady=false;
        // One 0..1 stock per (zone x band) region — see FishStock. Persisted so quitting mid-session
        // does not hand back a freshly stocked sea.
        public List<float> fishStock=new();
        public List<CaughtFish> cargo=new();

        // Tutorial hints already learned. Stored by id rather than as a bitmask so hints can be added,
        // removed or reordered later without silently re-teaching things the player already knows.
        public List<string> hintsSeen=new();
    }
    public sealed class PlayerSave
    {
        const string FileName="rusty-fishing-save.json";
        public SaveData Data { get; private set; }
        string PathName => Path.Combine(Application.persistentDataPath,FileName);
        public PlayerSave(){ Load(); }
        public void Load(){ bool fresh=!File.Exists(PathName); try{ Data=fresh?new SaveData():JsonUtility.FromJson<SaveData>(File.ReadAllText(PathName)); }catch{Data=new SaveData();fresh=true;} Data??=new SaveData(); Data.cargo??=new(); Data.missionProgress??=new(); Data.missionsDone??=new(); Data.hintsSeen??=new(); if(string.IsNullOrEmpty(Data.lastPortId))Data.lastPortId="home"; if(fresh)Data.hullHp=GameCatalog.startHullHp; }
        public void Store(){ try{ File.WriteAllText(PathName,JsonUtility.ToJson(Data,true)); }catch(Exception e){Debug.LogWarning(e.Message);} }
        // Wipe progression back to a fresh save (deletes the file, then re-creates a default one).
        public void Reset(){ try{ if(File.Exists(PathName))File.Delete(PathName); }catch(Exception e){Debug.LogWarning(e.Message);} Data=new SaveData(); Data.cargo=new(); Data.hintsSeen=new(); Data.hullHp=GameCatalog.startHullHp; Store(); }
        // Delete just the save file on disk (used by the editor menu when no game is running).
        public static void DeleteFile(){ try{ var p=Path.Combine(Application.persistentDataPath,FileName); if(File.Exists(p))File.Delete(p); }catch(Exception e){Debug.LogWarning(e.Message);} }
        // Economy values come from GameCatalog (which game-data.json can override).
        // Every stat is branch levels PLUS a flat per-tier bump, so a New Ship is felt immediately even
        // before its four fresh levels are bought.
        public int Tier=>Mathf.Clamp(Data.shipTier,0,GameCatalog.MaxShipTier);
        // Each branch's bonus is scaled by the milestones it has passed -- the levels differ per branch
        // on purpose, so the best buy keeps changing hands. See GameCatalog.UpgradeMilestones.
        float HookMul=>GameCatalog.MilestoneMul("hook",Data.hookLevel);
        float HoldMul=>GameCatalog.MilestoneMul("hold",Data.holdLevel);
        float EngineMul=>GameCatalog.MilestoneMul("engine",Data.engineLevel);
        float HullMul=>GameCatalog.MilestoneMul("hull",Data.hullLevel);

        public int MaxHp=>Mathf.RoundToInt(GameCatalog.startHullHp
                                           +Data.hullLevel*GameCatalog.hullHpPerLevel*HullMul
                                           +Tier*GameCatalog.tierHullHpBonus);
        public int Capacity=>Mathf.RoundToInt(GameCatalog.basketBaseCapacity
                                              +Data.holdLevel*GameCatalog.holdCapacityPerLevel*HoldMul
                                              +Tier*GameCatalog.tierCapacityBonus);
        public float SpeedMultiplier=>1+Data.engineLevel*GameCatalog.engineSpeedPerLevel*EngineMul
                                       +Tier*GameCatalog.tierBoatSpeedBonus;
        public float DamageMultiplier=>1+Data.hookLevel*GameCatalog.hookDamagePerLevel*HookMul
                                        +Tier*GameCatalog.tierDamageBonus;
        // Same upgrade drives how fast the hook swims: sink, rise, sideways and reel-in all scale.
        public float HookSpeedMultiplier=>1+Data.hookLevel*GameCatalog.hookSpeedPerLevel*HookMul
                                           +Tier*GameCatalog.tierHookSpeedBonus;

        /// <summary>Armour rating. Hull levels and the ship tier both feed it.</summary>
        public float Armor=>Data.hullLevel*GameCatalog.hullArmorPerLevel*HullMul
                            +Tier*GameCatalog.tierArmorBonus;

        /// <summary>
        /// Incoming damage after armour, in the divide form: raw/(1+armor). Never raw-armor -- that
        /// scales with how HARD a hit lands rather than how much total damage arrives, so it would wipe
        /// out the many-small-bites night fish while leaving big obstacles almost untouched.
        /// Always at least 1, so nothing becomes literally harmless.
        /// </summary>
        public int DamageAfterArmor(int raw)=>raw<=0?0:Mathf.Max(1,Mathf.RoundToInt(raw/(1f+Armor)));
        public int RepairCost=>Mathf.Max(0,(MaxHp-Data.hullHp)*GameCatalog.repairCostPerMissingHp);
        public void AddForced(string id,float hour,float wmul){Data.cargo.Add(new CaughtFish(id,hour,wmul));Store();}   // may exceed Capacity; caller resolves via toss
        public bool OverCapacity => Data.cargo.Count>Capacity;
        public bool TossAt(int index){if(index<0||index>=Data.cargo.Count)return false;Data.cargo.RemoveAt(index);Store();return true;}
        public static float W(CaughtFish c)=>c.wmul>0?c.wmul:1f;   // guard old saves where wmul defaults to 0
        /// <summary>
        /// Fresh / Stale / Rotten. Night catches run on their own, far shorter clock -- see
        /// GameCatalog.nightFreshHours. They keep their high prices as the reward for fishing after dark;
        /// the short fuse is what stops that reward being hoarded.
        /// </summary>
        public string Freshness(CaughtFish fish,float hour){
            float age=Mathf.Max(0,hour-fish.caughtHour);
            var def=GameCatalog.GetFish(fish.id);
            bool night=def!=null&&def.Evil;
            float fresh=night?GameCatalog.nightFreshHours:GameCatalog.freshHours;
            float stale=night?GameCatalog.nightStaleHours:GameCatalog.staleHours;
            return age<fresh?"Fresh":age<stale?"Stale":"Rotten";
        }
        public int Sell(PortDef port,float hour){int earned=0,delivered=0;for(int i=Data.cargo.Count-1;i>=0;i--){var c=Data.cargo[i];var state=Freshness(c,hour);if(state=="Rotten")continue;var f=GameCatalog.GetFish(c.id);earned+=Mathf.RoundToInt(f.value*W(c)*port.prices[c.id]*(state=="Fresh"?1:GameCatalog.staleSellFactor));Data.cargo.RemoveAt(i);}Data.coins+=earned;Store();return earned;}
        // Sell value of ONE fish (0 if rotten).
        public int PriceOf(CaughtFish c,PortDef port,float hour){var st=Freshness(c,hour);if(st=="Rotten")return 0;var f=GameCatalog.GetFish(c.id);return Mathf.RoundToInt(f.value*W(c)*port.prices[c.id]*(st=="Fresh"?1:GameCatalog.staleSellFactor));}
        // Sell a single fish by index; returns coins earned (0 = rotten/invalid, nothing removed).
        public int SellAt(int index,PortDef port,float hour){if(index<0||index>=Data.cargo.Count)return 0;int p=PriceOf(Data.cargo[index],port,hour);if(p<=0)return 0;Data.cargo.RemoveAt(index);Data.coins+=p;Store();return p;}
        public int Toss(float hour){int n=Data.cargo.RemoveAll(c=>Freshness(c,hour)=="Rotten");Store();return n;}
        public bool Repair(){int cost=RepairCost;if(cost==0)return true;if(Data.coins<cost)return false;Data.coins-=cost;Data.hullHp=MaxHp;Store();return true;}

        /// <summary>Snapshot the whole save as "the start of today" (call at each dawn).</summary>
        public void CaptureDayStart(){
            Data.dayStart="";                       // blank it first so the snapshot doesn't nest a prior one
            Data.dayStart=JsonUtility.ToJson(Data);
            Store();
        }
        /// <summary>Roll the save back to this day's dawn snapshot. Returns false if there is none.</summary>
        public bool RestoreDayStart(){
            if(string.IsNullOrEmpty(Data.dayStart))return false;
            string snap=Data.dayStart;
            var restored=JsonUtility.FromJson<SaveData>(snap);
            if(restored==null)return false;
            restored.dayStart=snap;                 // keep it armed so sinking again the same day rewinds again
            restored.cargo??=new();restored.missionProgress??=new();restored.missionsDone??=new();restored.hintsSeen??=new();restored.fishStock??=new();
            Data=restored;
            Store();
            return true;
        }
        /// <summary>Current level of an upgrade branch by id ("hook"/"hold"/"engine"/"hull").</summary>
        public int LevelOf(string id)=>id=="hook"?Data.hookLevel:id=="hold"?Data.holdLevel:id=="engine"?Data.engineLevel:Data.hullLevel;
        /// <summary>Branch levels are capped by the ship tier: 4 on hull A, 8 on B, 12 on C.</summary>
        public int LevelCap=>GameCatalog.LevelCapFor(Tier);
        public bool AtTierCap(string id)=>LevelOf(id)>=LevelCap;

        public bool Upgrade(string id){
            int level=LevelOf(id);
            if(level>=LevelCap)return false;                      // needs a New Ship before it can go on
            if(!GameCatalog.UpgradeCosts.TryGetValue(id,out var costs)||level>=costs.Length)return false;
            if(Data.coins<costs[level])return false;
            Data.coins-=costs[level];
            if(id=="hook")Data.hookLevel++;
            else if(id=="hold")Data.holdLevel++;
            else if(id=="engine")Data.engineLevel++;
            // Hull: bump current HP by the max just gained so upgrading actually raises your health
            // (and a full ship stays full) instead of only widening the max and shrinking the bar.
            else{int beforeMax=MaxHp;Data.hullLevel++;Data.hullHp+=Mathf.Max(0,MaxHp-beforeMax);}
            Store();return true;}

        /// <summary>Every branch maxed for this tier, and there is still a hull left to build.</summary>
        public bool NewShipReady=>Tier<GameCatalog.MaxShipTier
                                  &&AtTierCap("hook")&&AtTierCap("hold")
                                  &&AtTierCap("engine")&&AtTierCap("hull");
        public int NewShipCost=>GameCatalog.NewShipCost(Tier);

        /// <summary>Lay down the next hull. Raises the tier, which lifts the level cap and opens the band.</summary>
        public bool BuyNewShip(){
            if(!NewShipReady)return false;
            int cost=NewShipCost;
            if(cost<0||Data.coins<cost)return false;
            Data.coins-=cost;Data.shipTier++;Store();return true;}
    }
}
