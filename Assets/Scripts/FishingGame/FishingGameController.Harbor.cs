using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    // Harbor screen: market list, sell/toss/repair/upgrade, set sail / dock, and the basket-full toss modal.
    public sealed partial class FishingGameController
    {
        void OpenHarbor(PortDef port){mode=Mode.Harbor;currentPort=port;boatX=port.x;boatSpeed=0;shakeTime=0;if(world!=null)world.anchoredPosition=Vector2.zero;harbor.gameObject.SetActive(true);sea.gameObject.SetActive(false);RefreshHarbor();}
        void RefreshHarbor(){
            Set(harborName,currentPort.name.ToUpperInvariant());
            // Cargo/coins are shared with the sea HUD (make those labels persistent in the scene).
            Set(seaCargo,$"{save.Data.cargo.Count}/{save.Capacity}");SetCoins();
            UpdateShipReadouts();          // keep HP/speed/HP-bar current on the harbor screen too
            UpdateRepairFee();             // show the live repair fee on the "Fix Ship" panel
            EnsureMarketListLayout();
            foreach(var go in marketRows)Destroy(go);marketRows.Clear();
            var cargo=save.Data.cargo;
            if(market!=null){market.enabled=cargo.Count==0;if(cargo.Count==0)market.text="Fish hold empty";}
            if(marketRowPrefab==null||marketList==null)return; // assign the row prefab + list container
            // One row per individual fish (each keeps its own weight/freshness/price).
            for(int i=0;i<cargo.Count;i++){
                int idx=i;var c=cargo[i];var f=GameCatalog.GetFish(c.id);
                float kg=f.size*GameCatalog.WeightSizeToKg*PlayerSave.W(c);
                string fr=save.Freshness(c,AbsHour);int fi=fr=="Fresh"?0:fr=="Stale"?1:2;
                var sprite=RuntimeUI.Sprite("fish/species/"+f.art+(fi==2?"-rotten":""));
                var row=Instantiate(marketRowPrefab,marketList);
                row.Set(sprite,kg,fi,save.PriceOf(c,currentPort,AbsHour),()=>SellFish(idx));
                marketRows.Add(row.gameObject);
            }}
        void SellFish(int index){int earned=save.SellAt(index,currentPort,AbsHour);Set(message,earned>0?$"Sold for {earned} coins.":"Rotten fish can't be sold — toss it.");RefreshHarbor();}
        // Content must be top-anchored and its height driven by a ContentSizeFitter, or the ScrollRect
        // thinks there's nothing to scroll and elastic-snaps back to the top.
        void EnsureMarketListLayout(){
            if(marketList==null)return;
            marketList.anchorMin=new Vector2(0,1);marketList.anchorMax=new Vector2(1,1);marketList.pivot=new Vector2(.5f,1);
            if(marketList.GetComponent<VerticalLayoutGroup>()==null){var v=marketList.gameObject.AddComponent<VerticalLayoutGroup>();v.childControlHeight=false;v.childForceExpandHeight=false;v.childAlignment=TextAnchor.UpperCenter;}
            var fit=marketList.GetComponent<ContentSizeFitter>();if(fit==null)fit=marketList.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;}
        void SetSail(){if(save.Data.hullHp<=0){message.text="Repair the ship before sailing.";return;}harbor.gameObject.SetActive(false);sea.gameObject.SetActive(true);mode=phaseTime>=GameCatalog.DaySeconds?Mode.Night:Mode.Sailing;UpdateSeaUI();}
        void Dock(){var p=GameCatalog.AtPort(boatX);if(p!=null)OpenHarbor(p);}
        // Basket-full flow: still catch the fish, then force the player to toss one to get back under capacity.
        void OpenInventoryToss(){inventoryOpen=true;BuildInventoryPanel();}
        void CloseInventory(){inventoryOpen=false;if(inventoryPanel!=null){Destroy(inventoryPanel);inventoryPanel=null;}}
        void TossAt(int index){save.TossAt(index);if(save.OverCapacity)BuildInventoryPanel();else CloseInventory();}
        // Modal listing EVERY caught fish individually (scrollable) — tap one to toss it.
        void BuildInventoryPanel(){
            if(inventoryPanel!=null)Destroy(inventoryPanel);
            var root=RuntimeUI.Rect(canvas.transform,"InventoryModal",Vector2.zero,new Vector2(1080,1920));inventoryPanel=root.gameObject;
            root.gameObject.AddComponent<Image>().color=new Color(0,0,0,.62f); // dim + blocks background clicks
            var card=RuntimeUI.Image(root,"Card","UI/Harbor/market-card",Vector2.zero,new Vector2(980,1500));card.preserveAspect=false;
            RuntimeUI.Text(card.transform,"BASKET FULL",new Vector2(0,650),new Vector2(860,100),54,TextAnchor.MiddleCenter,new Color(.85f,.4f,.32f));
            RuntimeUI.Text(card.transform,$"Storage {save.Data.cargo.Count}/{save.Capacity} — tap a fish to toss it",new Vector2(0,562),new Vector2(900,60),28);
            // Scroll view (viewport masks, content holds one row per fish).
            var scrollGO=RuntimeUI.Rect(card.transform,"Scroll",new Vector2(0,-60),new Vector2(900,1120));
            var scroll=scrollGO.gameObject.AddComponent<ScrollRect>();
            var viewport=RuntimeUI.Rect(scrollGO,"Viewport",Vector2.zero,new Vector2(900,1120));
            viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;viewport.offsetMin=Vector2.zero;viewport.offsetMax=Vector2.zero;
            viewport.gameObject.AddComponent<Image>().color=new Color(1,1,1,.001f);viewport.gameObject.AddComponent<RectMask2D>();
            var content=RuntimeUI.Rect(viewport,"Content",Vector2.zero,new Vector2(900,10));
            content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;
            const float rowH=104;int n=save.Data.cargo.Count;content.sizeDelta=new Vector2(0,n*rowH);
            for(int i=0;i<n;i++){
                int idx=i;var c=save.Data.cargo[i];var fdef=GameCatalog.GetFish(c.id);
                float wmul=PlayerSave.W(c);float kg=fdef.size*GameCatalog.WeightSizeToKg*wmul;string fr=save.Freshness(c,AbsHour);int val=Mathf.RoundToInt(fdef.value*wmul);
                var btn=RuntimeUI.Button(content,"UI/Harbor/action-button",$"{fdef.name.ToUpperInvariant()}    {kg:0.0} kg · {fr} · ~{val}c",Vector2.zero,new Vector2(860,92),()=>TossAt(idx),22);
                btn.image.preserveAspect=false;
                var brt=(RectTransform)btn.transform;brt.anchorMin=brt.anchorMax=new Vector2(.5f,1);brt.pivot=new Vector2(.5f,.5f);brt.anchoredPosition=new Vector2(0,-rowH/2-i*rowH);
                RuntimeUI.Image(btn.transform,"Icon","fish/species/"+fdef.art+(fr=="Rotten"?"-rotten":""),new Vector2(-370,0),new Vector2(80,60));
            }
            scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            root.SetAsLastSibling();}
        void Sell(){message.text=$"Sold fish for {save.Sell(currentPort,AbsHour)} coins.";RefreshHarbor();}
        void Toss(){message.text=$"Tossed {save.Toss(AbsHour)} rotten fish.";RefreshHarbor();}
        void Repair(){int cost=save.RepairCost;message.text=cost==0?"Hull already full.":save.Repair()?$"Ship repaired for {cost} coins.":$"Not enough coins (need {cost}).";RefreshHarbor();}
        // Write the live repair fee into the panel's ResourceCount number (was a static placeholder).
        void UpdateRepairFee(){if(repairFeeText==null)return;repairFeeText.text=save.RepairCost.ToString();}
        void Upgrade(string id){if(!currentPort.upgrades){message.text="Upgrades are only available at Home Harbor.";return;}bool ok=save.Upgrade(id);message.text=ok?$"{id.ToUpperInvariant()} upgraded.":"Upgrade unavailable or too expensive.";if(ok)SyncUpgradeArt();RefreshHarbor();}
        // Swap boat/hook sprites to match upgrade levels (art frames 0..3), like the real game.
        void SyncUpgradeArt(){
            if(boat!=null){var s=RuntimeUI.Sprite("progression/boat-"+Mathf.Clamp(save.Data.hullLevel,0,3));if(s!=null)boat.sprite=s;}
            var hookImg=hook!=null?hook.GetComponent<Image>():null;
            if(hookImg!=null){var s=RuntimeUI.Sprite("progression/hook-"+Mathf.Clamp(save.Data.hookLevel,0,3));if(s!=null)hookImg.sprite=s;}}
        void Rest(){if(phaseTime<GameCatalog.DaySeconds)return;phaseTime=0;worldHour=6;save.Data.day++;save.Store();OpenHarbor(GameCatalog.AtPort(boatX)??GameCatalog.Ports[0]);}
        // Wipe ALL saved progress (coins, day, upgrades, cargo, hull) back to a fresh start and return
        // to Home Harbor. Exposed for the in-game Tuning panel ("RESET SAVE") and the editor menu.
        public void ResetProgression(){
            save.Reset();
            hitObstacles.Clear();
            foreach(var f in fish)if(f!=null)Destroy(f.gameObject);
            fish.Clear();
            phaseTime=0;worldHour=6;boatSpeed=0;
            if(nightShade!=null)nightShade.gameObject.SetActive(false);
            if(monster!=null)monster.gameObject.SetActive(false);
            SyncUpgradeArt();
            OpenHarbor(GameCatalog.Ports[0]);
            Set(message,"Progression reset.");
        }
        void CloseModal(){if(modalPanel!=null){Destroy(modalPanel);modalPanel=null;}}
        // Upgrade panel = a one-part detail card you page through with the ‹ › arrows (order below).
        static readonly string[] UpgradePartIds={"engine","hull","hook","hold"};
        static readonly string[] UpgradePartNames={"ENGINE","HULL","HOOK","FISH HOLD"};
        static readonly string[] UpgradePartStats={"SPEED","MAX HP","DAMAGE","CAPACITY"};
        void OpenUpgradePanel(){
            if(!currentPort.upgrades){Set(message,"Upgrades are only available at Home Harbor.");return;}
            if(upgradePanel!=null)upgradePanel.gameObject.SetActive(true);
            ShowUpgradePart();
        }
        void CloseUpgradePanel(){if(upgradePanel!=null)upgradePanel.gameObject.SetActive(false);}
        void CycleUpgrade(int dir){upgradeIndex=(upgradeIndex+dir+UpgradePartIds.Length)%UpgradePartIds.Length;ShowUpgradePart();}
        void SelectUpgrade(int i){if(i<0||i>=UpgradePartIds.Length)return;upgradeIndex=i;ShowUpgradePart();}
        void BuySelectedUpgrade(){Upgrade(UpgradePartIds[upgradeIndex]);ShowUpgradePart();}
        void ShowUpgradePart(){
            string id=UpgradePartIds[upgradeIndex];
            int level=UpgradeLevel(id),maxLevel=GameCatalog.UpgradeCosts.Length;bool maxed=level>=maxLevel;
            int cost=maxed?0:GameCatalog.UpgradeCosts[level];
            SetCoins();
            Set(upgradePartName,$"{UpgradePartNames[upgradeIndex]} {Roman(level+1)}");
            Set(upgradeStatLabel,UpgradePartStats[upgradeIndex]);
            Set(upgradeCurrentValue,PartValueText(id,level));
            Set(upgradeNextValue,maxed?"MAX":PartValueText(id,level+1));
            Set(upgradeBuyLabel,maxed?"MAX":$"UPGRADE {cost}");
            if(upgradeBuyButton!=null)upgradeBuyButton.interactable=!maxed&&save.Data.coins>=cost;
            var curUI=PartUI(upgradeIndex);
            if(upgradePartIcon!=null&&curUI!=null&&curUI.bigSprite!=null)upgradePartIcon.sprite=curUI.bigSprite;
            if(upgradeLevelDots!=null)
                for(int i=0;i<upgradeLevelDots.Length;i++)
                    if(upgradeLevelDots[i]!=null)upgradeLevelDots[i].color=i<=level?new Color(.16f,.65f,.6f):new Color(1,1,1,.2f);
            RefreshUpgradeSelector();
        }
        // The round part-icons on the ship: selected-sprite swap, glow ring, level label, and a
        // "can upgrade now" indicator. Each block names its own part, so array order doesn't matter.
        void RefreshUpgradeSelector(){
            if(upgradeParts==null)return;
            var costs=GameCatalog.UpgradeCosts;
            foreach(var p in upgradeParts){
                if(p==null)continue;
                int k=(int)p.part;if(k<0||k>=UpgradePartIds.Length)continue;
                bool sel=k==upgradeIndex;
                int lvl=UpgradeLevel(UpgradePartIds[k]);
                bool canUpgrade=lvl<costs.Length&&save.Data.coins>=costs[lvl];
                p.SetSelected(sel); // icon swaps using the button's own Sprite-Swap states
                if(p.highlight!=null)p.highlight.SetActive(sel);
                if(p.label!=null)p.label.text=$"{UpgradePartNames[k]} {Roman(lvl+1)}";
                if(p.readyIndicator!=null)p.readyIndicator.SetActive(canUpgrade);
            }
        }
        UpgradePartUI PartUI(int index){if(upgradeParts!=null)foreach(var p in upgradeParts)if(p!=null&&(int)p.part==index)return p;return null;}
        // Cosmetic "before/after" value shown per part at a given level.
        string PartValueText(string id,int level){
            switch(id){
                case "engine": return $"{GameCatalog.DisplaySpeedKn*(1+level*GameCatalog.engineSpeedPerLevel):0.0} kn";
                case "hull":   return $"{GameCatalog.startHullHp+level*GameCatalog.hullHpPerLevel} HP";
                case "hook":   return $"x{1+level*GameCatalog.hookDamagePerLevel:0.00}";
                default:       return $"{GameCatalog.basketBaseCapacity+level*GameCatalog.holdCapacityPerLevel}";
            }
        }
        static string Roman(int n)=>n switch{1=>"I",2=>"II",3=>"III",4=>"IV",_=>n.ToString()};
        int UpgradeLevel(string id)=>id=="hook"?save.Data.hookLevel:id=="hold"?save.Data.holdLevel:id=="engine"?save.Data.engineLevel:save.Data.hullLevel;
        // Settings modal (stub — full options to come).
        void OpenSettingsPanel(){
            CloseModal();
            var root=RuntimeUI.Rect(canvas.transform,"SettingsModal",Vector2.zero,new Vector2(1080,1920));modalPanel=root.gameObject;
            root.gameObject.AddComponent<Image>().color=new Color(0,0,0,.62f);
            var card=RuntimeUI.Image(root,"Card","UI/Harbor/market-card",Vector2.zero,new Vector2(840,700));card.preserveAspect=false;
            RuntimeUI.Text(card.transform,"SETTINGS",new Vector2(0,220),new Vector2(640,90),52);
            RuntimeUI.Text(card.transform,"Coming soon.",new Vector2(0,30),new Vector2(640,80),34);
            RuntimeUI.Button(card.transform,"UI/Harbor/action-button","CLOSE",new Vector2(0,-220),new Vector2(320,100),CloseModal,30);
            root.SetAsLastSibling();
        }
    }
}
