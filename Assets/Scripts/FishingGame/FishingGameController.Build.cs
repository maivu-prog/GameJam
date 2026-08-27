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

        // Play-mode canvas resolution. Build() generates a whole GameCanvas from code and is a LAST
        // resort: it must only run for a scene that has no UI canvas at all. Calling it whenever the
        // serialized `canvas` reference happens to be empty is what used to stack a second, duplicate
        // GameCanvas on top of the baked scene UI on every Play (same bug that killed GameBootstrap).
        void AcquireCanvas()
        {
            if (canvas != null) return;
            canvas = FindSceneCanvas();
            if (canvas == null) { Build(); return; }   // genuinely empty scene — generate the UI
            // Adopt the scene's canvas and re-find the two screen roots by name, so a lost reference
            // degrades into "wire me up again" instead of a duplicated UI.
            if (harbor == null) harbor = FindChildRect(canvas.transform, "HarborScreen");
            if (sea == null) sea = FindChildRect(canvas.transform, "SeaScreen");
            Debug.LogError("[FishingGame] The 'canvas' reference on " + name + " was empty. Reusing the scene canvas '" +
                canvas.name + "' instead of spawning a duplicate — but the other auto-wired references are probably " +
                "missing too. Re-run  Rusty Fishing ▸ Rebuild Editable UI  or re-drag the Canvas Roots.", this);
        }

        // The scene's own UI canvas. Root canvases only, and never a tool overlay we spawn ourselves.
        static Canvas FindSceneCanvas()
        {
            Canvas fallback = null;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas || c.name == "TuningCanvas") continue;
                if (c.name == "GameCanvas") return c;
                if (fallback == null) fallback = c;
            }
            return fallback;
        }

        static RectTransform FindChildRect(Transform root, string childName)
        {
            foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
                if (rt.name == childName) return rt;
            return null;
        }
        public void RepairArtReferences()
        {
            for(int i=0;i<portArt.Count&&i<GameCatalog.Ports.Count;i++)portArt[i].sprite=RuntimeUI.Sprite("progression/"+GameCatalog.Ports[i].art);
        }
        // Swap the single painted "Sea" backdrop for the layered parallax version. The flat image stays
        // in the scene (disabled) so the old look is one toggle away if a layer needs re-cutting.
        void SetupParallax(){
            if(sea==null)return;
            var flat=sea.Find("Sea");
            // Prefer a Parallax object authored in the scene (Rusty Fishing > Create Parallax Backdrop In
            // Scene). Its layer list is serialized, so tuning done in Edit mode survives leaving Play mode;
            // only fall back to building one from SeaParallax.DefaultLayers() when the scene has none.
            var root=sea.Find("Parallax") as RectTransform;
            if(root==null){
                root=RuntimeUI.Rect(sea,"Parallax",Vector2.zero,Vector2.zero);
                root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
                root.SetSiblingIndex(flat!=null?flat.GetSiblingIndex():0);   // backmost, behind World and NightShade
            }
            if(flat!=null)flat.gameObject.SetActive(false);
            parallax=root.GetComponent<SeaParallax>();
            if(parallax==null)parallax=root.gameObject.AddComponent<SeaParallax>();
            parallax.SetScroll(boatX);
            parallax.Build();   // rebuilds from the component's own layer list, not from the defaults
        }
        // Countdown ring hugging the fishing dial: how much of HookLineSeconds is left on this cast.
        // Two copies of the same ring sprite — a dim full-circle track, and a Radial360 Filled copy on top
        // that empties clockwise from 12 o'clock.
        void SetupCastTimer(){
            var dial=joystick!=null?joystick.transform as RectTransform:fishButton!=null?fishButton.transform as RectTransform:null;
            if(dial==null||dial.parent==null)return;
            // Sibling of the dial, not its child: a child always draws OVER its parent, which put the ring
            // on top of the dial art. Placed before it instead, only the ring's outer band shows.
            var host=dial.parent;
            var size=new Vector2(castTimerSize,castTimerSize);
            castTimerTrack=RuntimeUI.Image(host,"CastTimerTrack","UI/Gameplay/timer-ring",Vector2.zero,size);
            castTimerTrack.raycastTarget=false;                       // must never steal the dial's input
            castTimerTrack.preserveAspect=false;
            castTimerTrack.color=new Color(.04f,.08f,.09f,.38f);
            castTimerFill=RuntimeUI.Image(host,"CastTimerFill","UI/Gameplay/timer-ring",Vector2.zero,size);
            castTimerFill.raycastTarget=false;
            castTimerFill.preserveAspect=false;   // Filled + preserveAspect can offset the fill origin
            castTimerFill.type=Image.Type.Filled;
            castTimerFill.fillMethod=Image.FillMethod.Radial360;
            castTimerFill.fillOrigin=(int)Image.Origin360.Top;
            castTimerFill.fillClockwise=true;
            castTimerFill.fillAmount=1f;
            // Copy the dial's own anchoring so the ring stays centred on it at any aspect ratio, then slot
            // both copies in front of the dial in sibling order (= drawn behind it).
            foreach(var rt in new[]{castTimerTrack.rectTransform,castTimerFill.rectTransform}){
                rt.anchorMin=dial.anchorMin;rt.anchorMax=dial.anchorMax;rt.pivot=dial.pivot;
                rt.anchoredPosition=dial.anchoredPosition;rt.sizeDelta=size;
                rt.SetSiblingIndex(dial.GetSiblingIndex());
            }
            ShowCastTimer(false);   // only visible while a line is actually out
        }

        void ShowCastTimer(bool on){
            if(castTimerFill!=null)castTimerFill.gameObject.SetActive(on);
            if(castTimerTrack!=null)castTimerTrack.gameObject.SetActive(on);
        }

        void UpdateCastTimer(){
            if(castTimerFill==null)return;
            float left=Mathf.Clamp01(1f-hookTime/Mathf.Max(.01f,GameCatalog.HookLineSeconds));
            castTimerFill.fillAmount=left;
            // Amber while there is room, red over the last third, and a pulse at the very end so it reads
            // without the player having to look away from the hook.
            var c=Color.Lerp(new Color(.94f,.78f,.40f,.95f),new Color(.87f,.27f,.21f,1f),Mathf.InverseLerp(.34f,0f,left));
            if(left<.2f)c.a*=.62f+.38f*Mathf.Abs(Mathf.Sin(Time.time*9f));
            castTimerFill.color=c;
        }

        // The scene bakes art for 4 harbours; the map now has 10, and PlaceWorldArt only walks as far as the
        // shorter of the two lists — so ports 5..10 would exist in data, raise the DOCK button, and show
        // absolutely nothing. Top the list up (and trim it) so it always matches GameCatalog.Ports 1:1.
        void SetupPorts(){
            if(world==null)return;
            // Clone the layout of the last hand-placed port, so the size and the Y a designer tuned in the
            // scene still govern every harbour that gets added here.
            var template=portArt.Count>0?portArt[portArt.Count-1]:null;
            Vector2 size=template!=null?template.rectTransform.sizeDelta:new Vector2(700,350);
            while(portArt.Count>GameCatalog.Ports.Count){
                int last=portArt.Count-1;var im=portArt[last];portArt.RemoveAt(last);
                if(im!=null)Destroy(im.gameObject);
            }
            for(int i=0;i<portArt.Count;i++)
                if(portArt[i]!=null)portArt[i].gameObject.name="Port-"+GameCatalog.Ports[i].id;   // names went stale
            for(int i=portArt.Count;i<GameCatalog.Ports.Count;i++){
                var def=GameCatalog.Ports[i];
                var img=RuntimeUI.Image(world,"Port-"+def.id,"progression/"+def.art,new Vector2(0,GameCatalog.PortY),size);
                img.rectTransform.pivot=new Vector2(0.5f,0f);
                img.raycastTarget=false;
                if(boat!=null)img.transform.SetSiblingIndex(boat.transform.GetSiblingIndex());   // behind the hull
                portArt.Add(img);
            }
        }

        // A lantern glow over each harbour's safe water, so the truce line is something the player can
        // see rather than something they have to infer from the DOCK button appearing.
        void SetupPortHalos(){
            if(world==null)return;
            foreach(var im in portHalos)if(im!=null)Destroy(im.gameObject);
            portHalos.Clear();
            foreach(var p in GameCatalog.Ports){
                var img=RuntimeUI.Image(world,"Halo-"+p.id,"UI/Gameplay/safe-halo",Vector2.zero,Vector2.one);
                img.raycastTarget=false;
                img.preserveAspect=false;
                img.gameObject.SetActive(false);
                img.transform.SetSiblingIndex(0);   // behind the harbour art and the boat
                portHalos.Add(img);
            }
        }

        // Only lit at night, and it breathes slightly so it reads as lantern light rather than a decal.
        void PlacePortHalos(){
            for(int i=0;i<portHalos.Count&&i<GameCatalog.Ports.Count;i++){
                var img=portHalos[i];
                if(img==null)continue;
                if(!IsNight){if(img.gameObject.activeSelf)img.gameObject.SetActive(false);continue;}
                float sx=(GameCatalog.Ports[i].x-boatX)*GameCatalog.WorldScrollPpu;
                bool vis=Mathf.Abs(sx)<GameCatalog.PortCullPx;
                if(img.gameObject.activeSelf!=vis)img.gameObject.SetActive(vis);
                if(!vis)continue;
                // Size and placement are re-applied every frame so the Inspector fields tune live, and so
                // the width stays locked to the REAL safe radius rather than to a baked-in number.
                float w=GameCatalog.Ports[i].radius*2f*GameCatalog.WorldScrollPpu*haloWidthMul;
                img.rectTransform.sizeDelta=new Vector2(w,w*haloHeightMul);
                img.rectTransform.anchoredPosition=new Vector2(sx,haloOffsetY);
                var c=haloTint;
                c.a=Mathf.Clamp01(haloAlpha+haloPulse*Mathf.Sin(Time.time*1.6f+i));
                img.color=c;
            }
        }

        // Depth-band wash, drawn between the backdrop and the world so it tints the painted water but never
        // the boat, the fish or the hook. Prefers an object authored in the scene (Rusty Fishing > Create
        // Sea Band Overlays) so the sprites dragged into it are saved with the scene.
        void SetupBandOverlays(){
            if(sea==null)return;
            var root=sea.Find("BandOverlays") as RectTransform;
            if(root==null){
                root=RuntimeUI.Rect(sea,"BandOverlays",Vector2.zero,Vector2.zero);
                root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
                var after=sea.Find("Parallax");
                root.SetSiblingIndex(after!=null?after.GetSiblingIndex()+1:0);   // just in front of the backdrop
            }
            bandOverlays=root.GetComponent<SeaBandOverlays>();
            if(bandOverlays==null)bandOverlays=root.gameObject.AddComponent<SeaBandOverlays>();
            bandOverlays.Build();
        }

        // Night-only "sleep" on the harbour screen: the sea-screen REST button only exists while you are
        // out on the water, and once you have docked to escape a hunt there is no way to skip to dawn.
        // The SLEEP button is an authored GameObject on the harbour screen, not a runtime one — art and
        // placement belong in the scene. All the code does is bind the click and decide when it shows.
        void SetupSleepButton(){
            BindClick(sleepButton,Rest);
            if(sleepButton!=null)sleepButton.gameObject.SetActive(false);
        }

        // Night only, and only once you have actually docked: OpenHarbor is the sole caller. Out on the
        // water there is deliberately no way to skip the night — you have to make it back to a port.
        void UpdateSleepButton(){
            if(sleepButton!=null)sleepButton.gameObject.SetActive(IsNight);
        }

        // The night sea-monster encounter is disabled. The art and its scene object are kept so it can be
        // switched back on later — only the activation was removed (it used to pop in every night in open
        // water, from TickClock).
        void HideMonster(){if(monster!=null)monster.gameObject.SetActive(false);}

        // Bubble emitter lives in 'world' alongside the hook, so its coordinates match HookTip() and the
        // bubbles scroll with the sea. Sits just in front of the hook so they read as coming off it.
        void SetupBubbles(){
            if(world==null)return;
            var root=RuntimeUI.Rect(world,"HookBubbles",Vector2.zero,Vector2.zero);
            if(hook!=null)root.SetSiblingIndex(hook.GetSiblingIndex());
            bubbles=root.gameObject.AddComponent<HookBubbles>();
            bubbles.SetDebug(debugHookBubbles);   // the emitter is created at run time, so it cannot be ticked in Edit mode
            if(GameCatalog.debugStorage)
            Debug.Log($"[Hook] setup: bubbles={(bubbles!=null?"CO":"NULL")}, world={(world!=null?"CO":"NULL")}, "
                      +$"hook={(hook!=null?"CO":"NULL")}, sprite={(RuntimeUI.Sprite("UI/Gameplay/bubble")!=null?"CO":"NULL")}");
        }
        // Test-only fast-forward, pinned to the top-right of the game canvas so it is reachable on both
        // the harbor and sea screens. Toggles Time.timeScale, which speeds the whole game up — the day
        // clock only ticks at sea (Update() returns early in Harbor), so sail out before using it.
        // Nothing changes Time.timeScale now that the cheats are gone, but Unity carries it across a
        // domain reload — so a stray value left by anything else would follow the next Play session in.
        void OnDisable(){Time.timeScale=1f;}

        void EnsureUIInput(){var input=Object.FindFirstObjectByType<InputSystemUIInputModule>();if(input!=null&&input.actionsAsset==null)input.AssignDefaultActions();}
        void BuildHarbor(){RuntimeUI.Image(harbor,"Backdrop","whispering-harbor",Vector2.zero,new Vector2(1080,1920)).GetComponent<Image>().preserveAspect=false;
            RuntimeUI.Image(harbor,"Sign","UI/Harbor/harbor-sign",new Vector2(0,605),new Vector2(710,260));harborName=RuntimeUI.Text(harbor,"HOME HARBOR",new Vector2(0,590),new Vector2(620,110),56,TextAnchor.MiddleCenter,new Color(.9f,.84f,.65f));
            RuntimeUI.Image(harbor,"Market","UI/Harbor/market-card",new Vector2(0,50),new Vector2(980,850));RuntimeUI.Text(harbor,"FISH MARKET",new Vector2(0,370),new Vector2(760,90),54);market=RuntimeUI.Text(harbor,"",new Vector2(0,65),new Vector2(800,470),29,TextAnchor.UpperLeft);
            RuntimeUI.Button(harbor,"UI/Harbor/action-button","SELL FISH",new Vector2(-265,-270),new Vector2(260,95),Sell,26);RuntimeUI.Button(harbor,"UI/Harbor/action-button","TOSS ROTTEN",new Vector2(0,-270),new Vector2(260,95),Toss,24);RuntimeUI.Button(harbor,"UI/Harbor/action-button","REPAIR",new Vector2(265,-270),new Vector2(260,95),Repair,26);
            RuntimeUI.Button(harbor,"UI/Harbor/action-button","HOOK +",new Vector2(-300,-460),new Vector2(210,80),()=>Upgrade("hook"),22);RuntimeUI.Button(harbor,"UI/Harbor/action-button","HOLD +",new Vector2(-100,-460),new Vector2(210,80),()=>Upgrade("hold"),22);RuntimeUI.Button(harbor,"UI/Harbor/action-button","ENGINE +",new Vector2(100,-460),new Vector2(210,80),()=>Upgrade("engine"),22);RuntimeUI.Button(harbor,"UI/Harbor/action-button","HULL +",new Vector2(300,-460),new Vector2(210,80),()=>Upgrade("hull"),22);
            // The legacy builder used to own the message label. It is a scene object now -- one
            // GameObject holding a plate and its text -- so build the same pair here and point
            // messageBanner at it, rather than assigning the read-only label.
            if(messageBanner==null){
                var banner=RuntimeUI.Image(harbor,"MessageBanner","UI/Harbor/small-card",new Vector2(0,-575),new Vector2(880,120));
                banner.raycastTarget=false;banner.preserveAspect=false;
                RuntimeUI.Text(banner.transform,"",Vector2.zero,new Vector2(790,80),24);
                messageBanner=banner.gameObject;
                messageBanner.SetActive(false);
            }
RuntimeUI.Button(harbor,"UI/Harbor/primary-button","SET SAIL",new Vector2(0,-790),new Vector2(780,150),SetSail,50);}
        void BuildSea(){var bg=RuntimeUI.Image(sea,"Sea","fishing-world-backdrop",Vector2.zero,new Vector2(1080,1920));bg.preserveAspect=false;world=RuntimeUI.Rect(sea,"World",Vector2.zero,new Vector2(1080,1920));
            for(int i=0;i<GameCatalog.Ports.Count;i++){var p=GameCatalog.Ports[i];var im=RuntimeUI.Image(world,"Port-"+p.id,"progression/"+p.art,new Vector2(0,GameCatalog.PortY),new Vector2(700,350));im.rectTransform.pivot=new Vector2(0.5f,0f);portArt.Add(im);}
            boat=RuntimeUI.Image(world,"Boat","progression/boat-0",new Vector2(0,340),new Vector2(430,260));fishLayer=RuntimeUI.Rect(world,"FishLayer",new Vector2(0,-30),new Vector2(1080,1150));
            RuntimeUI.Image(sea,"ClockPanel","UI/Gameplay/clock-panel",new Vector2(-405,690),new Vector2(250,380));RuntimeUI.Image(sea,"ClockFace","UI/Harbor/clock-face",new Vector2(-405,755),new Vector2(155,155));clockNeedle=Needle(sea,"ClockNeedle",new Vector2(-405,755),new Vector2(6,58),new Color(.34f,.15f,.1f));
            RuntimeUI.Image(sea,"Counter","UI/Gameplay/counter-panel",new Vector2(360,760),new Vector2(300,150));seaCargo=RuntimeUI.Text(sea,"0/20",new Vector2(385,760),new Vector2(180,90),38);RuntimeUI.Image(sea,"CoinCounter","UI/Gameplay/counter-panel",new Vector2(360,610),new Vector2(300,150));seaCoins=RuntimeUI.Text(sea,"0",new Vector2(385,610),new Vector2(180,90),38);
            // Arrival banner (centre-top of the sea screen), hidden until you reach a dock.
            harborZone=RuntimeUI.Text(sea,"HARBOR",new Vector2(0,300),new Vector2(820,130),64,TextAnchor.MiddleCenter,new Color(.94f,.86f,.62f));harborZone.gameObject.SetActive(false);
            RuntimeUI.Image(sea,"Speedometer","UI/Gameplay/speedometer",new Vector2(400,320),new Vector2(260,260));safeNeedle=Needle(sea,"SafeNeedle",new Vector2(400,320),new Vector2(7,82),new Color(.15f,.48f,.45f));speedNeedle=Needle(sea,"SpeedNeedle",new Vector2(400,320),new Vector2(9,75),new Color(.67f,.22f,.14f));safeNeedle.gameObject.SetActive(false);speed=RuntimeUI.Text(sea,"0.0 kn",new Vector2(400,285),new Vector2(170,80),32);hp=RuntimeUI.Text(sea,"HP 100",new Vector2(0,235),new Vector2(300,65),30,TextAnchor.MiddleCenter,new Color(.93f,.86f,.68f));
            RuntimeUI.Image(sea,"Depth","UI/Gameplay/depth-ruler",new Vector2(-485,-180),new Vector2(85,760));
            left=MakeHold("UI/Gameplay/left-control",new Vector2(-385,-720),new Vector2(270,270));right=MakeHold("UI/Gameplay/right-control",new Vector2(385,-720),new Vector2(270,270));
            fishButton=RuntimeUI.Button(sea,"UI/Gameplay/fishing-dial","FISH",new Vector2(0,-715),new Vector2(300,300),()=>{},42);dockButton=RuntimeUI.Button(sea,"UI/Harbor/primary-button","DOCK",new Vector2(0,-470),new Vector2(330,100),Dock,30);restButton=RuntimeUI.Button(sea,"UI/Harbor/primary-button","REST UNTIL DAWN",new Vector2(0,-350),new Vector2(450,110),Rest,28);restButton.gameObject.SetActive(false);
            hook=RuntimeUI.Image(world,"Hook","UI/Gameplay/hook-icon",new Vector2(0,180),new Vector2(60,110)).rectTransform;var lineRt=RuntimeUI.Rect(world,"Line",Vector2.zero,new Vector2(1,1));line=lineRt.gameObject.AddComponent<Image>();line.sprite=null;line.preserveAspect=false;line.color=new Color(.94f,.88f,.70f,.96f);line.transform.SetSiblingIndex(hook.transform.GetSiblingIndex());hook.gameObject.SetActive(false);joystick=fishButton.gameObject.AddComponent<HoldControl>();
            monster=RuntimeUI.Image(world,"SeaMonster","sea-monster-encounter",new Vector2(370,100),new Vector2(620,1000));monster.gameObject.SetActive(false);nightShade=RuntimeUI.Image(sea,"NightShade","UI/Harbor/small-card",Vector2.zero,new Vector2(1080,1920));nightShade.sprite=null;nightShade.color=new Color(.01f,.03f,.08f,.55f);nightShade.raycastTarget=false;nightShade.gameObject.SetActive(false);nightShade.transform.SetSiblingIndex(1);}
        HoldControl MakeHold(string sprite,Vector2 pos,Vector2 size){var i=RuntimeUI.Image(sea,"Hold",sprite,pos,size);return i.gameObject.AddComponent<HoldControl>();}
        RectTransform Needle(Transform parent,string name,Vector2 pos,Vector2 size,Color color){var r=RuntimeUI.Rect(parent,name,pos,size);r.pivot=new Vector2(.5f,0);var i=r.gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=false;return r;}
        void BindButtons(){
            // Everything is wired by dragging refs onto the controller (see the "Action Buttons"
            // header). No GameObject-name matching — the hand-built scene renames and even duplicates
            // names (both the repair and upgrade buttons are called "FixBtn"), so names are unreliable.
            // The BUTTON, not SetSail itself: HideTitle also sails, and routing the hint through the
            // method meant pressing Continue silently taught the player the first three steps.
            BindClick(sailButton,()=>{HintsOnSail();SetSail();});
            BindClick(repairButton,Repair);
            BindClick(upgradeButton,OpenUpgradePanel);
            BindClick(settingsButton,OpenSettingsPanel);
            // restButton (sea screen) is retired: skipping the night is a PORT service now.
            if(restButton!=null)restButton.gameObject.SetActive(false);
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
