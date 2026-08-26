using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    // Day/night clock, boat steering, port/obstacle scrolling, collisions and the screen-shake + flash FX.
    public sealed partial class FishingGameController
    {
        // The sea monster used to appear in open water at night; it is switched off (see HideMonster).
        void TickClock(float dt){
            phaseTime+=dt;
            // Dusk and dawn each swap the entire fish field once, on the frame the clock crosses over.
            if(IsNight!=wasNight){wasNight=IsNight;SwapFishField();}
            if(phaseTime<GameCatalog.DaySeconds){worldHour=6+phaseTime/GameCatalog.DaySeconds*12;nightShade.gameObject.SetActive(false);}else{mode=mode==Mode.Fishing?mode:Mode.Night;worldHour=18+(phaseTime-GameCatalog.DaySeconds)/GameCatalog.NightSeconds*12;nightShade.gameObject.SetActive(true);if(phaseTime>=GameCatalog.DaySeconds+GameCatalog.NightSeconds){phaseTime=0;worldHour=6;save.Data.day++;save.Store();ReplenishFishField(GameCatalog.FishFieldMax,true);}}}
        void TickBoat(float dt){float dir=(mode==Mode.Fishing||dockingPort!=null)?0:(left.Held?-1:0)+(right.Held?1:0);float target=dir*GameCatalog.MaxSpeed*save.SpeedMultiplier;boatSpeed=Mathf.MoveTowards(boatSpeed,target,(dir!=0?GameCatalog.BoatAccel:GameCatalog.BoatDecel)*dt);boatX=Mathf.Clamp(boatX+boatSpeed*dt,0,GameCatalog.SeaLength);boat.rectTransform.localScale=new Vector3(boatSpeed<-.01f?-1:1,1,1);boat.rectTransform.anchoredPosition=new Vector2(0,340+Mathf.Sin(Time.time*3)*8);
            bool atPort=GameCatalog.AtPort(boatX)!=null;
            // Utility button above the joystick: shows DOCK at a port (and never while fishing);
            // falls back to the old DOCK button if the utility button isn't in the scene.
            // Hidden mid-dock too, otherwise this would re-show the button Dock() just switched off and the
            // player could fire a second dock into the zoom that is already playing.
            bool canDock=atPort&&mode!=Mode.Fishing&&dockingPort==null;
            if(utilityButton!=null)utilityButton.gameObject.SetActive(canDock);
            else if(dockButton!=null)dockButton.gameObject.SetActive(canDock);
            PlaceWorldArt();
            TickObstacleHits();}

        // Screen placement + culling for the scrolling world art (ports + obstacles). Split out of
        // TickBoat so it can also run while the boat is parked — SetupObstacles spawns its Images at
        // x=0, i.e. right on top of the boat, and TickBoat is skipped in Harbor / while a modal is open,
        // so anything not placed here stays visually glued to the boat.
        void PlaceWorldArt(){
            if(parallax!=null)parallax.SetScroll(boatX);
            if(bandOverlays!=null)bandOverlays.SetScroll(boatX);   // the bands are water, they scroll too
            PlacePortHalos();
            // Only X is driven by the boat. Each port's Y is left exactly as the scene authored it, so a
            // harbour can be nudged onto the backdrop's water line in the Inspector and it STAYS there —
            // this used to be hard-set to 430 every frame, which silently undid any such edit.
            for(int i=0;i<portArt.Count&&i<GameCatalog.Ports.Count;i++){
                float sx=(GameCatalog.Ports[i].x-boatX)*GameCatalog.WorldScrollPpu;
                var prt=portArt[i].rectTransform;
                prt.anchoredPosition=new Vector2(sx,prt.anchoredPosition.y);
                portArt[i].gameObject.SetActive(Mathf.Abs(sx)<GameCatalog.PortCullPx);
            }
            var field=GameCatalog.ObstacleField;
            // The two lists MUST stay 1:1. They fall out of sync whenever the field is regenerated without
            // rebuilding the art (LayoutDocks from the tuning sliders / dock-gap edits). The old code just
            // clamped the loop to the shorter list, which left every surplus Image frozen at its last screen
            // position forever — the "obstacle stuck to the boat" bug. Resync instead of truncating.
            if(obstacleArt.Count!=field.Count){SetupObstacles();return;}
            for(int i=0;i<field.Count;i++){
                float sx=(field[i].x-boatX)*GameCatalog.WorldScrollPpu;
                obstacleArt[i].rectTransform.anchoredPosition=new Vector2(sx,obstacleY);
                obstacleArt[i].gameObject.SetActive(Mathf.Abs(sx)<GameCatalog.ObstacleCullPx);
                obstacleArt[i].color=Mathf.Abs(boatX-field[i].x)<3?new Color(1f,.55f,.28f,1):Color.white;
            }
        }

        // Collision pass. Reads the field directly (never the art list), so a mid-run art rebuild can't
        // silently disable damage for the obstacles that lost their Image.
        void TickObstacleHits(){
            var field=GameCatalog.ObstacleField;
            for(int i=0;i<field.Count;i++){
                var inst=field[i];
                float d=Mathf.Abs(boatX-inst.x);
                if(d>1.3f){inst.hit=false;continue;}
                if(inst.hit)continue;
                inst.hit=true;
                if(Mathf.Abs(boatSpeed)<=inst.safeSpeed){HintsOnSafeCrossing();MissionOnSafeCrossing(inst.def.id);continue;}
                float ratio=Mathf.Clamp((Mathf.Abs(boatSpeed)-inst.safeSpeed)/Mathf.Max(.1f,GameCatalog.MaxSpeed-inst.safeSpeed),.3f,1.5f);
                save.Data.hullHp=Mathf.Max(0,save.Data.hullHp-save.DamageAfterArmor(Mathf.CeilToInt(inst.def.damage*ratio)));
                boatSpeed*=.2f;save.Store();TriggerHitFx();
                if(save.Data.hullHp<=0){Wreck();return;}
            }
        }

        // Create one obstacle Image per generated instance (obstacle count is dynamic, so not baked).
        void SetupObstacles(){
            foreach(var im in obstacleArt)if(im!=null)Destroy(im.gameObject);
            obstacleArt.Clear();
            foreach(var inst in GameCatalog.ObstacleField){
                var img=RuntimeUI.Image(world,"Obstacle-"+inst.def.id,"progression/"+inst.def.art,new Vector2(0,obstacleY),new Vector2(260,170));
                img.material=null;
                img.raycastTarget=false;
                img.gameObject.SetActive(false);   // hidden until PlaceWorldArt gives it a real screen x
                // Runtime children land at the end of 'world' and would draw OVER the boat. Slot them in
                // front of the boat so the hull always occludes the hazard it is passing.
                if(boat!=null)img.transform.SetSiblingIndex(boat.transform.GetSiblingIndex());
                obstacleArt.Add(img);}
            PlaceWorldArt();}   // counts match now, so this places them instead of recursing
        void Wreck(){EndKraken(false);dockingPort=null;dockZoom01=dockZoomTarget=0f;ApplyDockCamera();save.Data.cargo.Clear();save.Data.hullHp=0;save.Store();phaseTime=0;worldHour=6;OpenHarbor(GameCatalog.Ports[0]);Set(message,"The ship sank. All fish were lost. Pay for repairs.");}

        // Full-screen red flash overlay for collision feedback (built at runtime; BuildSea is skipped on a baked canvas).
        void SetupHitFx(){var rt=RuntimeUI.Rect(sea,"HitFlash",Vector2.zero,new Vector2(1080,1920));hitFlash=rt.gameObject.AddComponent<Image>();hitFlash.color=new Color(1f,.27f,.24f,0);hitFlash.raycastTarget=false;rt.SetAsLastSibling();}
        void TriggerHitFx(){shakeTime=.28f;if(hitFlash!=null){var c=hitFlash.color;c.a=.5f;hitFlash.color=c;}}
        // Docking pulls the view right in on the quay, then hands over to the harbour screen; leaving eases
        // it back out. Everything sits on a screen-space canvas, so the "camera" is world's localScale, with
        // the position compensated so the zoom centres on the BOAT instead of on the middle of the screen.
        void TickDockCamera(float dt){
            dockZoom01=Mathf.MoveTowards(dockZoom01,dockZoomTarget,dt/Mathf.Max(.01f,dockZoomSeconds));
            ApplyDockCamera();
            if(dockingPort!=null&&dockZoom01>=1f){var p=dockingPort;dockingPort=null;OpenHarbor(p);}
        }

        // Captured once, so the zoom scales RELATIVE to whatever scale the scene authored. Writing
        // Vector3.one here instead silently reset any scale set by hand on World or Parallax every frame.
        void CaptureBaseScales(){
            if(baseScalesCaptured)return;
            if(world!=null)worldBaseScale=world.localScale;
            if(parallax!=null)parallaxBaseScale=parallax.transform.localScale;
            baseScalesCaptured=true;
        }

        public void ApplyDockCamera(){
            CaptureBaseScales();
            float k=Mathf.SmoothStep(0f,1f,dockZoom01);
            if(world!=null)world.localScale=worldBaseScale*Mathf.Lerp(1f,dockZoomScale,k);
            // The backdrop takes only a share of the zoom, so it falls behind the boat as you close in.
            if(parallax!=null)parallax.transform.localScale=parallaxBaseScale*Mathf.Lerp(1f,dockZoomScale,k*dockParallaxShare);
        }

        // Screen shift that keeps the boat still while world scales up around it.
        Vector2 WorldZoomOffset(){
            float s=world!=null&&worldBaseScale.y>.0001f?world.localScale.y/worldBaseScale.y:1f;
            float boatY=boat!=null?boat.rectTransform.anchoredPosition.y:340f;
            return new Vector2(0f,-boatY*(s-1f));
        }

        void TickHitFx(float dt){
            Vector2 shake=Vector2.zero;
            if(shakeTime>0){shakeTime-=dt;float k=Mathf.Max(0,shakeTime)/.28f;shake=(Vector2)Random.insideUnitCircle*18f*k;}
            // One writer for world.anchoredPosition: the zoom offset is the base, the shake rides on top.
            // They used to fight — the shake reset to Vector2.zero every frame it was idle.
            if(world!=null)world.anchoredPosition=WorldZoomOffset()+shake;
            if(hitFlash!=null&&hitFlash.color.a>0){var c=hitFlash.color;c.a=Mathf.Max(0,c.a-dt*3f);hitFlash.color=c;}}
    }
}
