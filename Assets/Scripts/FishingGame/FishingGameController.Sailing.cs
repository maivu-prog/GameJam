using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    // Day/night clock, boat steering, port/obstacle scrolling, collisions and the screen-shake + flash FX.
    public sealed partial class FishingGameController
    {
        void TickClock(float dt){phaseTime+=dt;if(phaseTime<GameCatalog.DaySeconds){worldHour=6+phaseTime/GameCatalog.DaySeconds*12;nightShade.gameObject.SetActive(false);monster.gameObject.SetActive(false);}else{mode=mode==Mode.Fishing?mode:Mode.Night;worldHour=18+(phaseTime-GameCatalog.DaySeconds)/GameCatalog.NightSeconds*12;nightShade.gameObject.SetActive(true);monster.gameObject.SetActive(GameCatalog.AtPort(boatX)==null);if(phaseTime>=GameCatalog.DaySeconds+GameCatalog.NightSeconds){phaseTime=0;worldHour=6;save.Data.day++;save.Store();}restButton.gameObject.SetActive(GameCatalog.AtPort(boatX)!=null);}}
        void TickBoat(float dt){float dir=mode==Mode.Fishing?0:(left.Held?-1:0)+(right.Held?1:0);float target=dir*GameCatalog.MaxSpeed*save.SpeedMultiplier;boatSpeed=Mathf.MoveTowards(boatSpeed,target,(dir!=0?GameCatalog.BoatAccel:GameCatalog.BoatDecel)*dt);boatX=Mathf.Clamp(boatX+boatSpeed*dt,0,GameCatalog.SeaLength);boat.rectTransform.localScale=new Vector3(boatSpeed<-.01f?-1:1,1,1);boat.rectTransform.anchoredPosition=new Vector2(0,340+Mathf.Sin(Time.time*3)*8);
            bool atPort=GameCatalog.AtPort(boatX)!=null;
            // Utility button above the joystick: shows DOCK at a port (and never while fishing);
            // falls back to the old DOCK button if the utility button isn't in the scene.
            if(utilityButton!=null)utilityButton.gameObject.SetActive(atPort&&mode!=Mode.Fishing);
            else if(dockButton!=null)dockButton.gameObject.SetActive(atPort);
            for(int i=0;i<portArt.Count;i++){float sx=(GameCatalog.Ports[i].x-boatX)*GameCatalog.WorldScrollPpu;portArt[i].rectTransform.anchoredPosition=new Vector2(sx,430);portArt[i].gameObject.SetActive(Mathf.Abs(sx)<GameCatalog.PortCullPx);}
            foreach(var o in GameCatalog.Obstacles){int oi=GameCatalog.Obstacles.IndexOf(o);float sx=(o.x-boatX)*GameCatalog.WorldScrollPpu;obstacleArt[oi].rectTransform.anchoredPosition=new Vector2(sx,315);obstacleArt[oi].gameObject.SetActive(Mathf.Abs(sx)<GameCatalog.ObstacleCullPx);float d=Mathf.Abs(boatX-o.x);obstacleArt[oi].color=d<3?new Color(1f,.55f,.28f,1):Color.white;if(d>1.3f){hitObstacles.Remove(o.id);continue;}if(hitObstacles.Contains(o.id))continue;hitObstacles.Add(o.id);if(Mathf.Abs(boatSpeed)>o.safeSpeed){float ratio=Mathf.Clamp((Mathf.Abs(boatSpeed)-o.safeSpeed)/Mathf.Max(.1f,GameCatalog.MaxSpeed-o.safeSpeed),.3f,1.5f);save.Data.hullHp=Mathf.Max(0,save.Data.hullHp-Mathf.CeilToInt(o.damage*ratio));boatSpeed*=.2f;save.Store();TriggerHitFx();if(save.Data.hullHp<=0)Wreck();}}}
        void Wreck(){save.Data.cargo.Clear();save.Data.hullHp=0;save.Store();phaseTime=0;worldHour=6;OpenHarbor(GameCatalog.Ports[0]);message.text="The ship sank. All fish were lost. Pay for repairs.";}

        // Full-screen red flash overlay for collision feedback (built at runtime; BuildSea is skipped on a baked canvas).
        void SetupHitFx(){var rt=RuntimeUI.Rect(sea,"HitFlash",Vector2.zero,new Vector2(1080,1920));hitFlash=rt.gameObject.AddComponent<Image>();hitFlash.color=new Color(1f,.27f,.24f,0);hitFlash.raycastTarget=false;rt.SetAsLastSibling();}
        void TriggerHitFx(){shakeTime=.28f;if(hitFlash!=null){var c=hitFlash.color;c.a=.5f;hitFlash.color=c;}}
        void TickHitFx(float dt){
            if(shakeTime>0){shakeTime-=dt;float k=Mathf.Max(0,shakeTime)/.28f;world.anchoredPosition=shakeTime>0?(Vector2)Random.insideUnitCircle*18f*k:Vector2.zero;}
            else if(world.anchoredPosition!=Vector2.zero)world.anchoredPosition=Vector2.zero;
            if(hitFlash!=null&&hitFlash.color.a>0){var c=hitFlash.color;c.a=Mathf.Max(0,c.a-dt*3f);hitFlash.color=c;}}
    }
}
