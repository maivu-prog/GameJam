using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    // Harbor screen: market list, sell/toss/repair/upgrade, set sail / dock, and the basket-full toss modal.
    public sealed partial class FishingGameController
    {
        void OpenHarbor(PortDef port){mode=Mode.Harbor;currentPort=port;boatX=port.x;boatSpeed=0;save.Data.lastPortId=port.id;save.Data.boatX=boatX;save.Store();shakeTime=0;if(harbor!=null)harbor.gameObject.SetActive(true);if(sea!=null)sea.gameObject.SetActive(false);UpdateUpgradeAvailability();UpdateSleepButton();RefreshHarbor();MissionOnDock(port);}
        // Ports without upgrades (everywhere except Home Harbor) shouldn't show the UPGRADE button or its panel.
        void UpdateUpgradeAvailability(){
            bool has=currentPort!=null&&currentPort.upgrades;
            if(upgradeButton!=null)upgradeButton.gameObject.SetActive(has);
            if(!has&&upgradePanel!=null)upgradePanel.gameObject.SetActive(false);
            bool fix=currentPort!=null&&currentPort.repair;
            if(repairButton!=null)repairButton.gameObject.SetActive(fix);
            // Hiding only the button left the panel behind it on screen at ports with no such service.
            if(repairPanel!=null)repairPanel.gameObject.SetActive(fix);
            if(upgradeEntryPanel!=null)upgradeEntryPanel.gameObject.SetActive(has);
        }
        void RefreshHarbor(){
            UpdateMissionUI();
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
                row.Set(sprite,f.name.ToUpperInvariant(),kg,fi,save.PriceOf(c,currentPort,AbsHour),()=>SellFish(idx));
                marketRows.Add(row.gameObject);
            }}
        void SellFish(int index){
            // Snapshot BEFORE the sale: PlayerSave removes the rows as it sells, so afterwards there is
            // nothing left to tell the missions what species left the hold or how fresh it was.
            var sold=new List<CaughtFish>();var fresh=new List<string>();
            if(index>=0&&index<save.Data.cargo.Count){var c=save.Data.cargo[index];if(save.Freshness(c,AbsHour)!="Rotten"){sold.Add(c);fresh.Add(save.Freshness(c,AbsHour));}}
            int earned=save.SellAt(index,currentPort,AbsHour);HintsOnSale(fresh);MissionOnSale(currentPort,earned,sold,fresh);
            Set(message,earned>0?$"Sold for {earned} coins.":"Rotten fish can't be sold — toss it.");RefreshHarbor();}
        // Content must be top-anchored and its height driven by a ContentSizeFitter, or the ScrollRect
        // thinks there's nothing to scroll and elastic-snaps back to the top.
        void EnsureMarketListLayout(){
            if(marketList==null)return;
            marketList.anchorMin=new Vector2(0,1);marketList.anchorMax=new Vector2(1,1);marketList.pivot=new Vector2(.5f,1);
            if(marketList.GetComponent<VerticalLayoutGroup>()==null){var v=marketList.gameObject.AddComponent<VerticalLayoutGroup>();v.childControlHeight=false;v.childForceExpandHeight=false;v.childAlignment=TextAnchor.UpperCenter;}
            var fit=marketList.GetComponent<ContentSizeFitter>();if(fit==null)fit=marketList.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;}
        void SetSail(){if(save.Data.hullHp<=0){Set(message,"Repair the ship before sailing.");return;}HintsOnSail();CloseLedger();if(harbor!=null)harbor.gameObject.SetActive(false);if(sea!=null)sea.gameObject.SetActive(true);mode=phaseTime>=GameCatalog.DaySeconds?Mode.Night:Mode.Sailing;if(left!=null)left.gameObject.SetActive(true);if(right!=null)right.gameObject.SetActive(true);
            // Leaving: the sea screen comes back still zoomed in on the quay and eases out as you pull away.
            dockingPort=null;dockZoomTarget=0f;ApplyDockCamera();
            PlaceWorldArt();UpdateSeaUI();}
        // Dock no longer cuts straight to the harbour screen: it starts the zoom-in and OpenHarbor is
        // fired by TickDockCamera once the camera has actually arrived.
        void Dock(){
            if(dockingPort!=null)return;                       // already gliding in
            var p=GameCatalog.AtPort(boatX);
            if(p==null)return;
            HintsOnDock();
            dockingPort=p;dockZoomTarget=1f;boatSpeed=0f;
            if(utilityButton!=null)utilityButton.gameObject.SetActive(false);
            if(dockButton!=null)dockButton.gameObject.SetActive(false);
        }
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
        void Sell(){
            // Same snapshot rule as SellFish — everything not Rotten is about to go.
            var sold=new List<CaughtFish>();var fresh=new List<string>();
            foreach(var c in save.Data.cargo){var st=save.Freshness(c,AbsHour);if(st=="Rotten")continue;sold.Add(c);fresh.Add(st);}
            int earned=save.Sell(currentPort,AbsHour);HintsOnSale(fresh);MissionOnSale(currentPort,earned,sold,fresh);
            Set(message,$"Sold fish for {earned} coins.");RefreshHarbor();}
        void Toss(){Set(message,$"Tossed {save.Toss(AbsHour)} rotten fish.");RefreshHarbor();}
        void Repair(){
            if(currentPort!=null&&!currentPort.repair){Set(message,"No shipwright here. Try "+NearestWithRepair()+".");return;}
            int cost=save.RepairCost;
            // Set() is null-safe. Writing message.text directly threw a NullReferenceException the moment
            // the scene left that label unassigned — the repair itself had already run inside the ternary,
            // but RefreshHarbor() below never did, so the UI only caught up when you left and came back.
            Set(message,cost==0?"Hull already full.":save.Repair()?$"Ship repaired for {cost} coins.":$"Not enough coins (need {cost}).");
            RefreshHarbor();}
        // Name the closest port that does offer the service, so a refusal always points somewhere.
        string NearestWithRepair()=>NearestWith(p=>p.repair);
        string NearestWithUpgrades()=>NearestWith(p=>p.upgrades);
        string NearestWith(System.Func<PortDef,bool> has){
            PortDef best=null;float bestD=float.MaxValue;
            foreach(var p in GameCatalog.Ports){if(!has(p))continue;float d=Mathf.Abs(p.x-boatX);if(d<bestD){bestD=d;best=p;}}
            return best!=null?best.name:"another harbor";}
        // Write the live repair fee into the panel's ResourceCount number (was a static placeholder).
        void UpdateRepairFee(){if(repairFeeText==null)return;repairFeeText.text=save.RepairCost.ToString();}
        void Upgrade(string id){if(!currentPort.upgrades){Set(message,"No shipyard here. Try "+NearestWithUpgrades()+".");return;}bool ok=save.Upgrade(id);Set(message,ok?$"{id.ToUpperInvariant()} upgraded.":"Upgrade unavailable or too expensive.");if(ok){SyncUpgradeArt();MissionOnUpgrade(id,save.LevelOf(id));}RefreshHarbor();}
        // Hook upgrade tiers → rarity sprites (one per hook level, 0..7). Each hook upgrade promotes the hook
        // to the next rarity; the cast-hook art on the sea screen changes to match.
        static readonly string[] HookRaritySprites={
            "progression/hook-rarity-options/01-common-bone",
            "progression/hook-rarity-options/02-common-rusted-iron",
            "progression/hook-rarity-options/03-uncommon-bronze",
            "progression/hook-rarity-options/04-uncommon-tempered-steel",
            "progression/hook-rarity-options/05-rare-moon-silver",
            "progression/hook-rarity-options/06-epic-obsidian",
            "progression/hook-rarity-options/07-legendary-leviathan-fang",
            "progression/hook-rarity-options/08-mythic-abyssal-crown",
        };
        // Swap boat sprite (hull level 0..3) and the cast-hook sprite (hook level → rarity), like the real game.
        void SyncUpgradeArt(){
            if(save.Tier!=lastHullTier)ApplyDepthScale();   // new ship: another band open, the view pulls back
            if(boat!=null){var s=RuntimeUI.Sprite("progression/boat-"+Mathf.Clamp(save.Tier,0,3));if(s!=null)boat.sprite=s;}
            var hookImg=hook!=null?hook.GetComponent<Image>():null;
            // 8 hook sprites now have to cover 12 levels, so the index is scaled rather than clamped —
            // clamping would freeze the art at level 7 and leave the last five upgrades looking identical.
            if(hookImg!=null){
                int n=HookRaritySprites.Length, top=GameCatalog.MaxShipTier*GameCatalog.LevelsPerTier;
                int t=Mathf.Clamp(save.Data.hookLevel*(n-1)/Mathf.Max(1,top-1),0,n-1);
                var s=RuntimeUI.Sprite(HookRaritySprites[t]);
                if(s!=null){hookImg.sprite=s;hookImg.preserveAspect=true;}}}
        // Skip the rest of the night. Called from the sea-screen REST button and the harbour SLEEP button.
        void Rest(){
            if(!IsNight)return;
            phaseTime=0;worldHour=6;save.Data.phaseTime=0;save.Data.day++;save.Store();
            FishStock.Restore(save.Data,FishStock.SleepRegen);   // a night at anchor is what the sea needed
            wasNight=false;SwapFishField();        // dawn now, so the hunters leave and the shoals return
            HideKrakenVisualsImmediate();
            OpenHarbor(GameCatalog.AtPort(boatX)??GameCatalog.Ports[0]);
            save.CaptureDayStart();   // new dawn: this harbour state is where a sinking today rewinds to
            UpdateSleepButton();
        }
        // Wipe ALL saved progress (coins, day, upgrades, cargo, hull) back to a fresh start and return
        // to Home Harbor. Exposed for the in-game Tuning panel ("RESET SAVE") and the editor menu.
        public void ResetProgression(){
            save.Reset();
            hitObstacles.Clear();
            foreach(var f in fish)if(f!=null)Destroy(f.gameObject);
            fish.Clear();
            PopulateFishField();   // re-seed the sea after a wipe
            phaseTime=0;worldHour=6;boatSpeed=0;
            if(nightShade!=null)nightShade.gameObject.SetActive(false);
            if(monster!=null)monster.gameObject.SetActive(false);
            HideKrakenVisualsImmediate();
            SyncUpgradeArt();
            OpenHarbor(GameCatalog.Ports[0]);
            Set(message,"Progression reset.");
        }
        /// <summary>Cheat: refill hull HP to full (test tool only).</summary>
        public void CheatHealHull(){ save.Data.hullHp=save.MaxHp; save.Store(); UpdateShipReadouts(); }
        // Hooks for the Inspector tuning tool (TuningInspector) to rebuild structural bits live.
        public void RebuildObstacleArt(){SetupObstacles();}
        public void RepopulateFish(){foreach(var f in fish)if(f!=null)Destroy(f.gameObject);fish.Clear();PopulateFishField();}
        void CloseModal(){if(modalPanel!=null){Destroy(modalPanel);modalPanel=null;}}
        // Upgrade panel = a one-part detail card you page through with the ‹ › arrows (order below).
        static readonly string[] UpgradePartIds={"engine","hull","hook","hold"};
        static readonly string[] UpgradePartNames={"ENGINE","HULL","HOOK","FISH HOLD"};
        static readonly string[] UpgradePartStats={"SPEED  (KN)","MAX HP","DMG & REACH","CAPACITY"};
        void OpenUpgradePanel(){
            if(!currentPort.upgrades){Set(message,"No shipyard here. Try "+NearestWithUpgrades()+".");return;}
            if(upgradePanel!=null)upgradePanel.gameObject.SetActive(true);
            ShowUpgradePart();
        }
        void CloseUpgradePanel(){if(upgradePanel!=null)upgradePanel.gameObject.SetActive(false);}
        void CycleUpgrade(int dir){upgradeIndex=(upgradeIndex+dir+UpgradePartIds.Length)%UpgradePartIds.Length;ShowUpgradePart();}
        void SelectUpgrade(int i){if(i<0||i>=UpgradePartIds.Length)return;upgradeIndex=i;ShowUpgradePart();}
        // One button, two jobs: buy the next level, or — once all four branches are capped — lay down the
        // next hull, which is what actually opens the water below.
        void BuySelectedUpgrade(){
            if(save.NewShipReady)BuyNewShip();
            else Upgrade(UpgradePartIds[upgradeIndex]);
            ShowUpgradePart();
        }

        void BuyNewShip(){
            int cost=save.NewShipCost;
            if(!save.BuyNewShip()){Set(message,$"Not enough coins for the new ship (need {cost}).");return;}
            ApplyDepthScale();      // another band unlocked: depth ruler, camera zoom and night length
            SyncUpgradeArt();       // new hull art
            Set(message,$"A new ship! Tier {GameCatalog.ShipTierLetters[Mathf.Clamp(save.Tier,0,3)]} — "
                        +"every stat improved, and deeper water is open.");
            RefreshHarbor();
        }

        /// <summary>
        /// "A-1" .. "C-4" for the level you are ON. Level 0 is the first step, so the display is level+1
        /// spelled as tier-letter and step; the top level clamps to the last step rather than rolling over
        /// into a tier that does not exist.
        /// </summary>
        /// <summary>
        /// Tier letter plus the step inside that tier: A-1 .. A-4, then B-1, then C-1. Two spellings of the
        /// same value — the small part buttons take the arabic form, the big card the roman one — so the
        /// card reads as a title and the buttons as a count.
        /// </summary>
        static string TierLabel(int level,bool roman=false){
            int lp=GameCatalog.LevelsPerTier, tiers=GameCatalog.MaxShipTier;
            int idx=Mathf.Clamp(level,0,tiers*lp-1);
            int step=idx%lp+1;
            return $"{GameCatalog.ShipTierLetters[idx/lp]}-{(roman?Roman(step):step.ToString())}";
        }
        // Per-branch upgrade cost array (one entry per level); empty if unknown.
        static int[] CostsOf(string id)=>GameCatalog.UpgradeCosts.TryGetValue(id,out var c)?c:System.Array.Empty<int>();
        void ShowUpgradePart(){
            string id=UpgradePartIds[upgradeIndex];
            var costs=CostsOf(id);
            int level=UpgradeLevel(id);
            int cap=save.LevelCap;                       // 4 / 8 / 12, whichever hull is under you
            bool atCap=level>=cap;
            bool newShip=save.NewShipReady;              // every branch capped AND a hull left to build
            int cost=newShip?save.NewShipCost:(atCap||level>=costs.Length?0:costs[level]);
            SetCoins();

            // Name carries the tier letter and the step inside it: ENGINE A-1 .. A-4, then B-1, then C-1.
            // The absolute level (1..12) is never shown — four dots and a letter say it more clearly.
            Set(upgradePartName,$"{UpgradePartNames[upgradeIndex]} {TierLabel(level,true)}");
            Set(upgradeStatLabel,UpgradePartStats[upgradeIndex]);
            Set(upgradeCurrentValue,PartValueText(id,level));
            Set(upgradeNextValue,atCap?(newShip?"NEW":"MAX"):PartValueText(id,level+1));

            Set(upgradeBuyLabel,newShip?$"NEW SHIP {cost}"
                                :atCap?"MAX"
                                :$"UPGRADE {cost}");
            if(upgradeBuyButton!=null)
                upgradeBuyButton.interactable=(newShip||!atCap)&&cost>0&&save.Data.coins>=cost;

            var curUI=PartUI(upgradeIndex);
            if(upgradePartIcon!=null&&curUI!=null&&curUI.bigSprite!=null)upgradePartIcon.sprite=curUI.bigSprite;

            // Four dots per tier, counted from the bottom of the CURRENT tier — they reset on every ship.
            int inTier=level-save.Tier*GameCatalog.LevelsPerTier;
            if(upgradeLevelDots!=null)
                for(int i=0;i<upgradeLevelDots.Length;i++)
                    if(upgradeLevelDots[i]!=null){
                        bool lit=i<inTier;
                        if(upgradeLevelDots[i].gameObject.activeSelf!=lit)upgradeLevelDots[i].gameObject.SetActive(lit);
                        if(lit)upgradeLevelDots[i].color=Color.white;
                    }
            RefreshUpgradeSelector();
        }
        // The round part-icons on the ship: selected-sprite swap, glow ring, level label, and a
        // "can upgrade now" indicator. Each block names its own part, so array order doesn't matter.
        void RefreshUpgradeSelector(){
            if(upgradeParts==null)return;
            foreach(var p in upgradeParts){
                if(p==null)continue;
                int k=(int)p.part;if(k<0||k>=UpgradePartIds.Length)continue;
                bool sel=k==upgradeIndex;
                int lvl=UpgradeLevel(UpgradePartIds[k]);
                var costs=CostsOf(UpgradePartIds[k]);
                bool canUpgrade=lvl<costs.Length&&save.Data.coins>=costs[lvl];
                p.SetSelected(sel); // icon swaps using the button's own Sprite-Swap states
                if(p.highlight!=null)p.highlight.SetActive(sel);
                if(p.label!=null)p.label.text=$"{UpgradePartNames[k]} {TierLabel(lvl)}";
                if(p.readyIndicator!=null)p.readyIndicator.SetActive(canUpgrade);
            }
        }
        UpgradePartUI PartUI(int index){if(upgradeParts!=null)foreach(var p in upgradeParts)if(p!=null&&(int)p.part==index)return p;return null;}
        // Cosmetic "before/after" value shown per part at a given level.
        /// <summary>
        /// One SHORT value per part — the unit lives in the stat label above. The current and next values
        /// are two separate boxes with an arrow between them, so anything longer than a few characters
        /// runs straight over its neighbour ("8.0 kn8.8 kn").
        ///
        /// Hook shows damage only even though the upgrade also buys reach: two numbers never fit, and the
        /// label says both improve.
        /// </summary>
        string PartValueText(string id,int level){
            // Must include the per-tier bonus, or the card advertises a number the ship does not have:
            // PlayerSave adds it to every stat, so a tier-B hull really is 140 HP, not the 120 the branch
            // levels alone would give. Rounded because the per-level rates are tunable floats.
            int tier=save.Tier;
            switch(id){
                case "engine": return $"{GameCatalog.DisplaySpeedKn*(1+level*GameCatalog.engineSpeedPerLevel+tier*GameCatalog.tierBoatSpeedBonus):0.0}";
                case "hull":   return $"{Mathf.RoundToInt(GameCatalog.startHullHp+level*GameCatalog.hullHpPerLevel+tier*GameCatalog.tierHullHpBonus)}";
                case "hook":   return $"x{1+level*GameCatalog.hookDamagePerLevel+tier*GameCatalog.tierDamageBonus:0.00}";
                default:       return $"{Mathf.RoundToInt(GameCatalog.basketBaseCapacity+level*GameCatalog.holdCapacityPerLevel+tier*GameCatalog.tierCapacityBonus)}";
            }
        }
        // Absolute level 1..12 on the small part buttons, while the big card shows the tier name
        // (ENGINE A-1). Two readings of the same number: the buttons rank the whole ship, the card
        // says where you are inside the current hull. Table goes to XII because that is the cap.
        static readonly string[] RomanNumerals=
            {"I","II","III","IV","V","VI","VII","VIII","IX","X","XI","XII"};
        static string Roman(int n)=>n>=1&&n<=RomanNumerals.Length?RomanNumerals[n-1]:n.ToString();
        int UpgradeLevel(string id)=>id=="hook"?save.Data.hookLevel:id=="hold"?save.Data.holdLevel:id=="engine"?save.Data.engineLevel:save.Data.hullLevel;
        // Settings modal (stub — full options to come).
        void OpenSettingsPanel(){
            CloseModal();
            var root=RuntimeUI.Rect(canvas.transform,"SettingsModal",Vector2.zero,new Vector2(1080,1920));modalPanel=root.gameObject;
            root.gameObject.AddComponent<Image>().color=new Color(0,0,0,.62f);
            var card=RuntimeUI.Image(root,"Card","UI/Harbor/market-card",Vector2.zero,new Vector2(840,700));card.preserveAspect=false;
            RuntimeUI.Text(card.transform,"SETTINGS",new Vector2(0,220),new Vector2(640,90),52);
            RuntimeUI.Button(card.transform,"UI/Harbor/action-button","MAIN MENU",new Vector2(0,60),new Vector2(360,100),ShowTitle,30);
            RuntimeUI.Button(card.transform,"UI/Harbor/action-button","QUIT GAME",new Vector2(0,-60),new Vector2(360,100),QuitGame,30);
            RuntimeUI.Button(card.transform,"UI/Harbor/action-button","CLOSE",new Vector2(0,-200),new Vector2(320,100),CloseModal,30);
            root.SetAsLastSibling();
        }

        /// <summary>Save and return to the title screen. Continue from there resumes this same save.</summary>
        void ShowTitle(){
            if(mode==Mode.Fishing)EndFishing();   // don't leave a live line/hook behind the menu
            save?.Store();
            CloseModal();
            if(titleScreen==null){ShowWorld(true);return;}   // no menu authored — just close the modal
            titleScreen.gameObject.SetActive(true);
            ShowWorld(false);
            RefreshTitle();
        }

        /// <summary>Save and exit. In the editor this just stops Play; a real build quits (WebGL unloads).</summary>
        void QuitGame(){
            save?.Store();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
    }
}
