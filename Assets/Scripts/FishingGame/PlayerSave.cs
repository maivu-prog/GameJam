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
        public List<CaughtFish> cargo=new();
    }
    public sealed class PlayerSave
    {
        const string FileName="rusty-fishing-save.json";
        public SaveData Data { get; private set; }
        string PathName => Path.Combine(Application.persistentDataPath,FileName);
        public PlayerSave(){ Load(); }
        public void Load(){ bool fresh=!File.Exists(PathName); try{ Data=fresh?new SaveData():JsonUtility.FromJson<SaveData>(File.ReadAllText(PathName)); }catch{Data=new SaveData();fresh=true;} Data??=new SaveData(); Data.cargo??=new(); if(fresh)Data.hullHp=GameCatalog.startHullHp; }
        public void Store(){ try{ File.WriteAllText(PathName,JsonUtility.ToJson(Data,true)); }catch(Exception e){Debug.LogWarning(e.Message);} }
        // Wipe progression back to a fresh save (deletes the file, then re-creates a default one).
        public void Reset(){ try{ if(File.Exists(PathName))File.Delete(PathName); }catch(Exception e){Debug.LogWarning(e.Message);} Data=new SaveData(); Data.cargo=new(); Data.hullHp=GameCatalog.startHullHp; Store(); }
        // Delete just the save file on disk (used by the editor menu when no game is running).
        public static void DeleteFile(){ try{ var p=Path.Combine(Application.persistentDataPath,FileName); if(File.Exists(p))File.Delete(p); }catch(Exception e){Debug.LogWarning(e.Message);} }
        // Economy values come from GameCatalog (which game-data.json can override).
        public int MaxHp=>GameCatalog.startHullHp+Data.hullLevel*GameCatalog.hullHpPerLevel;
        public int Capacity=>GameCatalog.basketBaseCapacity+Data.holdLevel*GameCatalog.holdCapacityPerLevel;
        public float SpeedMultiplier=>1+Data.engineLevel*GameCatalog.engineSpeedPerLevel;
        public float DamageMultiplier=>1+Data.hookLevel*GameCatalog.hookDamagePerLevel;
        public int RepairCost=>Mathf.Max(0,(MaxHp-Data.hullHp)*GameCatalog.repairCostPerMissingHp);
        public void AddForced(string id,float hour,float wmul){Data.cargo.Add(new CaughtFish(id,hour,wmul));Store();}   // may exceed Capacity; caller resolves via toss
        public bool OverCapacity => Data.cargo.Count>Capacity;
        public bool TossAt(int index){if(index<0||index>=Data.cargo.Count)return false;Data.cargo.RemoveAt(index);Store();return true;}
        public static float W(CaughtFish c)=>c.wmul>0?c.wmul:1f;   // guard old saves where wmul defaults to 0
        public string Freshness(CaughtFish fish,float hour){float age=Mathf.Max(0,hour-fish.caughtHour);return age<GameCatalog.freshHours?"Fresh":age<GameCatalog.staleHours?"Stale":"Rotten";}
        public int Sell(PortDef port,float hour){int earned=0;for(int i=Data.cargo.Count-1;i>=0;i--){var c=Data.cargo[i];var state=Freshness(c,hour);if(state=="Rotten")continue;var f=GameCatalog.GetFish(c.id);earned+=Mathf.RoundToInt(f.value*W(c)*port.prices[c.id]*(state=="Fresh"?1:GameCatalog.staleSellFactor));Data.cargo.RemoveAt(i);}Data.coins+=earned;Store();return earned;}
        // Sell value of ONE fish (0 if rotten).
        public int PriceOf(CaughtFish c,PortDef port,float hour){var st=Freshness(c,hour);if(st=="Rotten")return 0;var f=GameCatalog.GetFish(c.id);return Mathf.RoundToInt(f.value*W(c)*port.prices[c.id]*(st=="Fresh"?1:GameCatalog.staleSellFactor));}
        // Sell a single fish by index; returns coins earned (0 = rotten/invalid, nothing removed).
        public int SellAt(int index,PortDef port,float hour){if(index<0||index>=Data.cargo.Count)return 0;int p=PriceOf(Data.cargo[index],port,hour);if(p<=0)return 0;Data.cargo.RemoveAt(index);Data.coins+=p;Store();return p;}
        public int Toss(float hour){int n=Data.cargo.RemoveAll(c=>Freshness(c,hour)=="Rotten");Store();return n;}
        public bool Repair(){int cost=RepairCost;if(cost==0)return true;if(Data.coins<cost)return false;Data.coins-=cost;Data.hullHp=MaxHp;Store();return true;}
        public bool Upgrade(string id){int level=id=="hook"?Data.hookLevel:id=="hold"?Data.holdLevel:id=="engine"?Data.engineLevel:Data.hullLevel;var baseCosts=GameCatalog.UpgradeCosts;if(level>=baseCosts.Length||Data.coins<baseCosts[level])return false;Data.coins-=baseCosts[level];if(id=="hook")Data.hookLevel++;else if(id=="hold")Data.holdLevel++;else if(id=="engine")Data.engineLevel++;else Data.hullLevel++;Store();return true;}
    }
}
