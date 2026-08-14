using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RustyFishing
{
    // Canvas / UI construction (used when baking the editable scene) and runtime button binding.
    public sealed partial class FishingGameController
    {
        void Build(){canvas=RuntimeUI.BuildCanvas();harbor=RuntimeUI.Rect(canvas.transform,"HarborScreen",Vector2.zero,new Vector2(1080,1920));sea=RuntimeUI.Rect(canvas.transform,"SeaScreen",Vector2.zero,new Vector2(1080,1920));BuildHarbor();BuildSea();}
        public void BuildEditableHierarchy(){Build();}
        public void RepairArtReferences()
        {
            for(int i=0;i<portArt.Count&&i<GameCatalog.Ports.Count;i++)portArt[i].sprite=RuntimeUI.Sprite("progression/"+GameCatalog.Ports[i].art);
            for(int i=0;i<obstacleArt.Count&&i<GameCatalog.Obstacles.Count;i++)obstacleArt[i].sprite=RuntimeUI.Sprite("progression/"+GameCatalog.Obstacles[i].art);
        }
        void EnsureUIInput(){var input=Object.FindFirstObjectByType<InputSystemUIInputModule>();if(input!=null&&input.actionsAsset==null)input.AssignDefaultActions();}
        void BuildHarbor(){RuntimeUI.Image(harbor,"Backdrop","whispering-harbor",Vector2.zero,new Vector2(1080,1920)).GetComponent<Image>().preserveAspect=false;
            RuntimeUI.Image(harbor,"Sign","UI/Harbor/harbor-sign",new Vector2(0,605),new Vector2(710,260));harborName=RuntimeUI.Text(harbor,"HOME HARBOR",new Vector2(0,590),new Vector2(620,110),56,TextAnchor.MiddleCenter,new Color(.9f,.84f,.65f));
            RuntimeUI.Image(harbor,"Market","UI/Harbor/market-card",new Vector2(0,50),new Vector2(980,850));RuntimeUI.Text(harbor,"FISH MARKET",new Vector2(0,370),new Vector2(760,90),54);market=RuntimeUI.Text(harbor,"",new Vector2(0,65),new Vector2(800,470),29,TextAnchor.UpperLeft);
            RuntimeUI.Button(harbor,"UI/Harbor/action-button","SELL FISH",new Vector2(-265,-270),new Vector2(260,95),Sell,26);RuntimeUI.Button(harbor,"UI/Harbor/action-button","TOSS ROTTEN",new Vector2(0,-270),new Vector2(260,95),Toss,24);RuntimeUI.Button(harbor,"UI/Harbor/action-button","REPAIR",new Vector2(265,-270),new Vector2(260,95),Repair,26);
            RuntimeUI.Button(harbor,"UI/Harbor/action-button","HOOK +",new Vector2(-300,-460),new Vector2(210,80),()=>Upgrade("hook"),22);RuntimeUI.Button(harbor,"UI/Harbor/action-button","HOLD +",new Vector2(-100,-460),new Vector2(210,80),()=>Upgrade("hold"),22);RuntimeUI.Button(harbor,"UI/Harbor/action-button","ENGINE +",new Vector2(100,-460),new Vector2(210,80),()=>Upgrade("engine"),22);RuntimeUI.Button(harbor,"UI/Harbor/action-button","HULL +",new Vector2(300,-460),new Vector2(210,80),()=>Upgrade("hull"),22);
            message=RuntimeUI.Text(harbor,"",new Vector2(0,-575),new Vector2(820,80),24);RuntimeUI.Button(harbor,"UI/Harbor/primary-button","SET SAIL",new Vector2(0,-790),new Vector2(780,150),SetSail,50);}
        void BuildSea(){var bg=RuntimeUI.Image(sea,"Sea","fishing-world-backdrop",Vector2.zero,new Vector2(1080,1920));bg.preserveAspect=false;world=RuntimeUI.Rect(sea,"World",Vector2.zero,new Vector2(1080,1920));
            for(int i=0;i<GameCatalog.Ports.Count;i++){var p=GameCatalog.Ports[i];portArt.Add(RuntimeUI.Image(world,"Port-"+p.id,"progression/"+p.art,new Vector2(0,430),new Vector2(700,350)));}
            for(int i=0;i<GameCatalog.Obstacles.Count;i++){var o=GameCatalog.Obstacles[i];var img=RuntimeUI.Image(world,"Obstacle-"+o.id,"progression/"+o.art,new Vector2(0,315),new Vector2(260,170));img.material=null;obstacleArt.Add(img);}
            boat=RuntimeUI.Image(world,"Boat","progression/boat-0",new Vector2(0,340),new Vector2(430,260));fishLayer=RuntimeUI.Rect(world,"FishLayer",new Vector2(0,-30),new Vector2(1080,1150));
            RuntimeUI.Image(sea,"ClockPanel","UI/Gameplay/clock-panel",new Vector2(-405,690),new Vector2(250,380));RuntimeUI.Image(sea,"ClockFace","UI/Harbor/clock-face",new Vector2(-405,755),new Vector2(155,155));clockNeedle=Needle(sea,"ClockNeedle",new Vector2(-405,755),new Vector2(6,58),new Color(.34f,.15f,.1f));
            RuntimeUI.Image(sea,"Counter","UI/Gameplay/counter-panel",new Vector2(360,760),new Vector2(300,150));seaCargo=RuntimeUI.Text(sea,"0/20",new Vector2(385,760),new Vector2(180,90),38);RuntimeUI.Image(sea,"CoinCounter","UI/Gameplay/counter-panel",new Vector2(360,610),new Vector2(300,150));seaCoins=RuntimeUI.Text(sea,"0",new Vector2(385,610),new Vector2(180,90),38);
            // Arrival banner (centre-top of the sea screen), hidden until you reach a dock.
            harborZone=RuntimeUI.Text(sea,"HARBOR",new Vector2(0,300),new Vector2(820,130),64,TextAnchor.MiddleCenter,new Color(.94f,.86f,.62f));harborZone.gameObject.SetActive(false);
            RuntimeUI.Image(sea,"Speedometer","UI/Gameplay/speedometer",new Vector2(400,320),new Vector2(260,260));safeNeedle=Needle(sea,"SafeNeedle",new Vector2(400,320),new Vector2(7,82),new Color(.15f,.48f,.45f));speedNeedle=Needle(sea,"SpeedNeedle",new Vector2(400,320),new Vector2(9,75),new Color(.67f,.22f,.14f));safeNeedle.gameObject.SetActive(false);speed=RuntimeUI.Text(sea,"0.0 kn",new Vector2(400,285),new Vector2(170,80),32);hp=RuntimeUI.Text(sea,"HP 100",new Vector2(0,235),new Vector2(300,65),30,TextAnchor.MiddleCenter,new Color(.93f,.86f,.68f));
            RuntimeUI.Image(sea,"Depth","UI/Gameplay/depth-ruler",new Vector2(-485,-180),new Vector2(85,760));
            left=MakeHold("UI/Gameplay/left-control",new Vector2(-385,-720),new Vector2(270,270));right=MakeHold("UI/Gameplay/right-control",new Vector2(385,-720),new Vector2(270,270));
            fishButton=RuntimeUI.Button(sea,"UI/Gameplay/fishing-dial","FISH",new Vector2(0,-715),new Vector2(300,300),()=>{},42);dockButton=RuntimeUI.Button(sea,"UI/Harbor/primary-button","DOCK",new Vector2(0,-470),new Vector2(330,100),Dock,30);restButton=RuntimeUI.Button(sea,"UI/Harbor/primary-button","REST UNTIL DAWN",new Vector2(0,-350),new Vector2(450,110),Rest,28);restButton.gameObject.SetActive(false);
            hook=RuntimeUI.Image(world,"Hook","UI/Gameplay/hook-icon",new Vector2(0,180),new Vector2(60,110)).rectTransform;line=RuntimeUI.Image(world,"Line","UI/Gameplay/rope-rivet",Vector2.zero,new Vector2(1,1));line.sprite=null;line.preserveAspect=false;line.color=new Color(.94f,.88f,.70f,.96f);line.transform.SetSiblingIndex(hook.transform.GetSiblingIndex());hook.gameObject.SetActive(false);joystick=fishButton.gameObject.AddComponent<HoldControl>();
            monster=RuntimeUI.Image(world,"SeaMonster","sea-monster-encounter",new Vector2(370,100),new Vector2(620,1000));monster.gameObject.SetActive(false);nightShade=RuntimeUI.Image(sea,"NightShade","UI/Harbor/small-card",Vector2.zero,new Vector2(1080,1920));nightShade.sprite=null;nightShade.color=new Color(.01f,.03f,.08f,.55f);nightShade.raycastTarget=false;nightShade.gameObject.SetActive(false);nightShade.transform.SetSiblingIndex(1);}
        HoldControl MakeHold(string sprite,Vector2 pos,Vector2 size){var i=RuntimeUI.Image(sea,"Hold",sprite,pos,size);return i.gameObject.AddComponent<HoldControl>();}
        RectTransform Needle(Transform parent,string name,Vector2 pos,Vector2 size,Color color){var r=RuntimeUI.Rect(parent,name,pos,size);r.pivot=new Vector2(.5f,0);var i=r.gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=false;return r;}
        void BindButtons(){
            // Everything is wired by dragging refs onto the controller (see the "Action Buttons"
            // header). No GameObject-name matching — the hand-built scene renames and even duplicates
            // names (both the repair and upgrade buttons are called "FixBtn"), so names are unreliable.
            BindClick(sailButton,SetSail);
            BindClick(repairButton,Repair);
            BindClick(upgradeButton,OpenUpgradePanel);
            BindClick(settingsButton,OpenSettingsPanel);
            BindClick(restButton,Rest);
            BindClick(dockButton,Dock);
            // Upgrade panel carousel.
            BindClick(upgradeBackButton,CloseUpgradePanel);
            BindClick(upgradePrevButton,()=>CycleUpgrade(-1));
            BindClick(upgradeNextButton,()=>CycleUpgrade(1));
            BindClick(upgradeBuyButton,BuySelectedUpgrade);
            if(upgradeParts!=null)
                foreach(var p in upgradeParts){if(p==null||p.button==null)continue;int idx=(int)p.part;BindClick(p.button,()=>SelectUpgrade(idx));}
            if(upgradePanel!=null)upgradePanel.gameObject.SetActive(false); // hidden until UpgradeButton opens it
            // Utility button above the joystick: docks at a port, hidden otherwise (driven in TickBoat).
            if(utilityButton!=null){
                BindClick(utilityButton,Dock);
                var ul=utilityButton.GetComponentInChildren<TMP_Text>(true);if(ul!=null)ul.text="DOCK";
                utilityButton.gameObject.SetActive(false);
                if(dockButton!=null)dockButton.gameObject.SetActive(false); // merged into the utility button
            }
            // The repair-fee field (ResourceCount) is disabled in the scene — force it (and its parents)
            // active so the fee is visible once we write to it.
            if(repairFeeText!=null)
                for(var t=repairFeeText.transform;t!=null&&t!=(canvas!=null?canvas.transform:null);t=t.parent)
                    if(!t.gameObject.activeSelf)t.gameObject.SetActive(true);
            // FISH is the hold-to-cast dial (see StartFishing/TickHook); Sell/Toss live on the market rows.
        }
        static void BindClick(Button b,System.Action action){
            if(b==null)return;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(()=>action());
        }
    }
}
