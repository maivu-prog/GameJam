using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    // Fishing: cast/retract lifecycle, hook physics, the fishing-line trail, the joystick knob,
    // and fish spawning/ticking.
    public sealed partial class FishingGameController
    {
        // Split the static FISH dial into base (the existing dial art) + a movable knob that follows the finger.
        void SetupJoystickKnob(){
            if(joystick==null)return;
            var baseRt=(RectTransform)joystick.transform;
            joystick.knobRadius=baseRt.rect.width*.28f;
            // Prefer the knob wired in the Inspector; only spawn one if none is assigned.
            if(joystickKnob!=null){
                joystick.knob=joystickKnob;
                var img=joystickKnob.GetComponent<Image>();if(img!=null)img.raycastTarget=false; // must not eat the drag
                return;
            }
            var lbl=baseRt.GetComponentInChildren<TMP_Text>(true);if(lbl!=null)lbl.enabled=false; // hide baked "FISH" label
            var knob=RuntimeUI.Image(baseRt,"Knob","UI/Gameplay/fishing-dial",Vector2.zero,new Vector2(150,150));
            knob.raycastTarget=false;knob.color=new Color(.82f,.78f,.66f,1f);knob.transform.SetAsLastSibling();
            joystick.knob=knob.rectTransform;}

        // Draw the fishing line as a pool of thin rotated Image quads between consecutive trail points
        // (rod tip -> hook path -> hook). Uses a real sprite so it always renders. Built at runtime
        // because BuildSea() is skipped on a pre-baked canvas.
        void StyleFishingLine(){
            if(line!=null)line.enabled=false;
            Transform parent=hook!=null?hook.transform.parent:(line!=null?line.transform.parent:canvas.transform);
            lineRoot=RuntimeUI.Rect(parent,"LineRoot",Vector2.zero,new Vector2(1080,1920));
            if(hook!=null)lineRoot.SetSiblingIndex(hook.transform.GetSiblingIndex());
            lineSegs=new Image[GameCatalog.LineTrailMaxPoints+2];
            for(int i=0;i<lineSegs.Length;i++){var im=RuntimeUI.Image(lineRoot,"Seg","UI/Gameplay/rope-rivet",Vector2.zero,new Vector2(1,1));im.preserveAspect=false;im.color=new Color(.95f,.89f,.72f,.98f);im.raycastTarget=false;im.gameObject.SetActive(false);lineSegs[i]=im;}}
        void HideLine(){if(lineSegs!=null)foreach(var im in lineSegs)if(im&&im.gameObject.activeSelf)im.gameObject.SetActive(false);}

        void StartFishing(){mode=Mode.Fishing;hookRetracting=false;hookOffset=Vector2.zero;hookTime=0;boatSpeed=0;trail.Clear();hook.anchoredPosition=new Vector2(0,180);hook.gameObject.SetActive(true);}
        void EndFishing(){mode=phaseTime>=GameCatalog.DaySeconds?Mode.Night:Mode.Sailing;hookRetracting=false;hook.gameObject.SetActive(false);trail.Clear();linePts.Clear();HideLine();}
        void TickHook(float dt){
            if(hookRetracting){RetractHook(dt);return;}
            hookTime+=dt;Vector2 j=joystick.Value;float vx=j.x*GameCatalog.HookHorizontal;float vy=j.y>=0?GameCatalog.HookSink+j.y*(GameCatalog.HookSinkMax-GameCatalog.HookSink):Mathf.Clamp(GameCatalog.HookSink-(-j.y*GameCatalog.HookUpForce)*GameCatalog.HookUpDrag,-GameCatalog.HookRiseMax,GameCatalog.HookSinkMax);
            hookOffset+=new Vector2(vx,-vy)*GameCatalog.PixelsPerUnit*dt;hookOffset.y=Mathf.Clamp(hookOffset.y,-GameCatalog.HookMaxDepthUnits*GameCatalog.PixelsPerUnit,0);hook.anchoredPosition=new Vector2(0,180)+hookOffset;if(new Vector2(vx,-vy).sqrMagnitude>.01f)hook.localEulerAngles=new Vector3(0,0,Mathf.Atan2(-vy,vx)*Mathf.Rad2Deg-90);DrawLine();
            foreach(var f in fish.ToArray())if(f&&Vector2.Distance(hook.anchoredPosition,f.Rect.anchoredPosition+fishLayer.anchoredPosition)<55+f.Def.size*25&&f.Hit(GameCatalog.HookDamage*save.DamageMultiplier*dt)){
                // Always catch — even when the basket is full. If it overflows, open the toss modal.
                fish.Remove(f);f.Collect(new Vector2(95,300)-fishLayer.anchoredPosition);save.AddForced(f.Def.id,AbsHour,f.WeightMul);
                if(save.OverCapacity&&!inventoryOpen)OpenInventoryToss();}
            // Release the dial (or run out the line) -> reel the hook straight back to the boat.
            if(!joystick.Held||hookTime>GameCatalog.HookLineSeconds)hookRetracting=true;}
        void RetractHook(float dt){float step=GameCatalog.HookRetract*GameCatalog.PixelsPerUnit*dt;float dist=hookOffset.magnitude;if(dist<=step){hookOffset=Vector2.zero;hook.anchoredPosition=new Vector2(0,180);DrawLine();EndFishing();return;}hookOffset-=hookOffset/dist*step;hook.anchoredPosition=new Vector2(0,180)+hookOffset;DrawLine();}
        void DrawLine(){Vector2 h=hook.anchoredPosition;
            if(trail.Count==0||Vector2.Distance(trail[trail.Count-1],h)>GameCatalog.LineTrailMinDist){trail.Add(h);if(trail.Count>GameCatalog.LineTrailMaxPoints)trail.RemoveAt(0);}
            linePts.Clear();linePts.Add(new Vector2(95,300));linePts.AddRange(trail);linePts.Add(h);
            if(lineSegs==null)return;int s=0;
            for(int i=0;i<linePts.Count-1&&s<lineSegs.Length;i++){Vector2 a=linePts[i],b=linePts[i+1];float len=Vector2.Distance(a,b);if(len<.5f)continue;var rt=lineSegs[s].rectTransform;rt.anchoredPosition=(a+b)*.5f;rt.sizeDelta=new Vector2(len+1.5f,GameCatalog.LineWidthPx);rt.localEulerAngles=new Vector3(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);if(!lineSegs[s].gameObject.activeSelf)lineSegs[s].gameObject.SetActive(true);s++;}
            for(int i=s;i<lineSegs.Length;i++)if(lineSegs[i].gameObject.activeSelf)lineSegs[i].gameObject.SetActive(false);}

        void TickFish(float dt){spawnTimer-=dt;if(spawnTimer<=0&&fish.Count<GameCatalog.SpawnMaxActive&&GameCatalog.AtPort(boatX)==null){spawnTimer=GameCatalog.SpawnBaseInterval/GameCatalog.ZoneAt(boatX).spawnRateMul;SpawnFish();}
            // While actively casting, hand fish the hook position so they flee from it (demo/real-game parity).
            Vector2? hookLocal=(mode==Mode.Fishing&&!hookRetracting)?(Vector2?)(hook.anchoredPosition-fishLayer.anchoredPosition):null;
            for(int i=fish.Count-1;i>=0;i--){if(fish[i]==null){fish.RemoveAt(i);continue;}fish[i].Tick(dt,Time.time,hookLocal);}}
        void SpawnFish(){var zone=GameCatalog.ZoneAt(boatX);var candidates=GameCatalog.Fish.Where(f=>zone.allows(f.id)).ToList();var def=candidates[Random.Range(0,candidates.Count)];float depth=Random.Range(def.minDepth,def.maxDepth);var rect=RuntimeUI.Rect(fishLayer,"Fish",Vector2.zero,Vector2.one);var actor=rect.gameObject.AddComponent<FishActor>();float w=(fishSizeBase+def.size*fishSizePer)*fishScale*GameCatalog.FishSizeScale;float fy=Mathf.Max(GameCatalog.FishSurfaceY-depth*GameCatalog.FishDepthPx,-540f);actor.Init(def,new Vector2(Random.Range(-GameCatalog.FishSpawnHalfWidthPx,GameCatalog.FishSpawnHalfWidthPx),fy),w);fish.Add(actor);}
    }
}
