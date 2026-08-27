using System.Collections.Generic;
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
            if(hook!=null)hook.localScale=Vector3.one*GameCatalog.HookScale;
            if(line!=null)line.enabled=false;
            Transform parent=hook!=null?hook.transform.parent:(line!=null?line.transform.parent:canvas.transform);
            lineRoot=RuntimeUI.Rect(parent,"LineRoot",Vector2.zero,new Vector2(1080,1920));
            if(hook!=null)lineRoot.SetSiblingIndex(hook.transform.GetSiblingIndex());
            lineSegs=new Image[GameCatalog.LineTrailMaxPoints+2];
            for(int i=0;i<lineSegs.Length;i++){var im=RuntimeUI.Image(lineRoot,"Seg","UI/Gameplay/rope-rivet",Vector2.zero,new Vector2(1,1));im.preserveAspect=false;im.color=new Color(.95f,.89f,.72f,.98f);im.raycastTarget=false;im.gameObject.SetActive(false);lineSegs[i]=im;}}
        void HideLine(){if(lineSegs!=null)foreach(var im in lineSegs)if(im&&im.gameObject.activeSelf)im.gameObject.SetActive(false);}

        void StartFishing(){HintsOnCast();mode=Mode.Fishing;hookRetracting=false;hookOffset=Vector2.zero;hookPrevOffset=Vector2.zero;if(bubbles!=null)bubbles.ResetVelocity();hookTime=0;boatSpeed=0;trail.Clear();hook.localScale=Vector3.one*GameCatalog.HookScale;hook.localEulerAngles=Vector3.zero;hook.anchoredPosition=new Vector2(0,SeaMap.HookRestY);hook.gameObject.SetActive(true);
            // Hide the left/right steering controls while casting (you can't steer with a line out).
            if(left!=null)left.gameObject.SetActive(false);if(right!=null)right.gameObject.SetActive(false);
            ShowCastTimer(true);UpdateCastTimer();
            if(GameCatalog.debugStorage)Debug.Log($"[Hook] cast — bubbles={(bubbles!=null?"CO":"NULL")}, hook={(hook!=null?"CO":"NULL")}");}
        void EndFishing(){mode=phaseTime>=GameCatalog.DaySeconds?Mode.Night:Mode.Sailing;hookRetracting=false;ShowCastTimer(false);hook.gameObject.SetActive(false);trail.Clear();linePts.Clear();HideLine();
            if(left!=null)left.gameObject.SetActive(true);if(right!=null)right.gameObject.SetActive(true);}
        void TickHook(float dt){
            UpdateCastTimer();   // also runs while reeling in, where hookTime is frozen and the ring holds
            if(hookRetracting){RetractHook(dt);TrackHookImpact(dt);return;}
            hookTime+=dt;Vector2 j=joystick.Value;float vx=j.x*GameCatalog.HookHorizontal;float vy=j.y>=0?GameCatalog.HookSink+j.y*(GameCatalog.HookSinkMax-GameCatalog.HookSink):Mathf.Clamp(GameCatalog.HookSink-(-j.y*GameCatalog.HookUpForce)*GameCatalog.HookUpDrag,-GameCatalog.HookRiseMax,GameCatalog.HookSinkMax);
            // Horizontal keeps its own scale; vertical uses DepthPx so the hook and the fish share one depth
            // ruler. The floor is the unlocked band (or the sea bed, whichever is shallower) — that IS the
            // progression gate: the line simply will not pay out past the edge of water you have earned.
            // Hook upgrades move the hook faster through the water as well as hitting harder. Descent eats
            // into the same 12s the fight needs, so reach speed is what actually opens up the deep bands.
            float hookSpeed=save.HookSpeedMultiplier;
            Vector2 preMove=hookOffset;
            hookOffset+=new Vector2(vx*hookSpeed*GameCatalog.PixelsPerUnit,-vy*hookSpeed*GameCatalog.DepthPx*DepthSpeedGain())*dt;
            hook.anchoredPosition=new Vector2(0,SeaMap.HookRestY)+hookOffset;
            // Stop the BODY at the limit, not the origin: the barb hangs ~50px*scale below the hook's own
            // pivot, so clamping the pivot let the whole business end sink past the band edge.
            float floorPx=MaxCastDepthU()*GameCatalog.DepthPx-HookReachBelowOrigin();
            hookOffset.y=Mathf.Clamp(hookOffset.y,-Mathf.Max(0f,floorPx),0f);
            // How far the hook actually moved on screen this frame. A parked hook (e.g. resting at the depth
            // floor while you hold DOWN) reads ~0 and deals NO damage — you must actively work it across a
            // fish to bite, instead of sinking to the bottom and letting it cook whatever drifts by.
            float hookMovePx=(hookOffset-preMove).magnitude/Mathf.Max(dt,1e-4f);
            hook.anchoredPosition=new Vector2(0,SeaMap.HookRestY)+hookOffset;
            // Point the hook in its direction of travel — the BARB (bottom of the sprite) leads. Sinking straight
            // down gives 0° (natural, barb down); pushing up rotates toward 180° (barb up); moving sideways tilts.
            // Smoothed so it turns rather than snaps. HookTip() reads this angle, so the hitbox follows the barb.
            Vector2 move=new Vector2(vx,-vy);
            if(move.sqrMagnitude>0.02f){HintsOnHookMoved();float target=Mathf.Atan2(move.y,move.x)*Mathf.Rad2Deg+90f;hook.localEulerAngles=new Vector3(0,0,Mathf.LerpAngle(hook.localEulerAngles.z,target,0.3f));}
            DrawLine();
            TrackHookImpact(dt);
            // The hit radius is in PIXELS while the fish sprite shrinks with the zoom, so without DepthZoom
            // a zoomed-out tier 3 gave a hitbox wider than the fish itself — deeper water became EASIER to
            // hit, the exact opposite of the intent.
            float zoom=DepthZoom();
            if(GameCatalog.InSafeZone(boatX))return;   // truce: the line goes out but nothing takes it
            // ONE fish at a time — the nearest one touching the shank. This used to damage every fish in
            // range on the same frame, so a shoal was whittled down in parallel and two or three died
            // within a few frames of each other: it read as a single catch that dropped three fish into
            // the hold. A hook catches what it is stuck in, not everything swimming past it.
            FishActor bite=null;float best=float.MaxValue;int nInRange=0;
            foreach(var f in fish){
                // Skip anything that cannot be caught, or it would stand in front of a fish that can:
                // Hit() refuses locked and leaving fish, so as the nearest one it would simply block.
                // MUST also skip non-visible fish: off-screen/parked fish early-return in Tick() before
                // repositioning, so they sit at their spawn x=0 (right under the boat) with the sprite
                // hidden — and the sinking hook was "catching" those invisible ghosts. Only a drawn fish
                // is really in the water.
                if(!f||f.Leaving||f.Locked||!f.Visible)continue;
                float d=DistanceToHookBody(f.Rect.anchoredPosition+fishLayer.anchoredPosition);
                // Reach = the fish's actual body radius (half its on-screen width * a fraction) plus a small
                // flat margin. Touching the fish body with any part of the shank counts, not just its centre.
                float reach=FishWidthPx(f.Def)*GameCatalog.FishHitFraction+GameCatalog.HookCatchRadius*zoom;
                if(d>=reach)continue;
                nInRange++;
                if(d>=best)continue;
                best=d;bite=f;
            }
            bool biteGate=bite!=null&&hookMovePx>=GameCatalog.HookBiteMinSpeed;
            if(GameCatalog.debugFishing&&bite!=null){
                _fishDbgT-=dt;
                if(_fishDbgT<=0f){
                    _fishDbgT=0.2f;
                    float thr=FishWidthPx(bite.Def)*GameCatalog.FishHitFraction+GameCatalog.HookCatchRadius*zoom;
                    Vector2 fpos=bite.Rect.anchoredPosition+fishLayer.anchoredPosition;
                    Vector2 hpos=hook.anchoredPosition;
                    Debug.Log($"[FISHDBG] near={bite.Def.id} vis={bite.Visible} hp={bite.Hp:0.0} d={best:0} thr={thr:0} nInRange={nInRange} "
                             +$"fishRect={bite.Rect.sizeDelta.x:0}x{bite.Rect.sizeDelta.y:0} fishPos=({fpos.x:0},{fpos.y:0}) hookPos=({hpos.x:0},{hpos.y:0}) "
                             +$"hookMove={hookMovePx:0} gate={biteGate} joy=({joystick.Value.x:0.00},{joystick.Value.y:0.00}) frame={Time.frameCount}");
                }
            }
            if(biteGate&&bite.Hit(GameCatalog.HookDamage*save.DamageMultiplier*dt)){
                // Always catch — even when the basket is full. If it overflows, open the toss modal.
                var f=bite;
                if(GameCatalog.debugFishing){
                    bool atFloorC=hookOffset.y<=-Mathf.Max(0f,floorPx)+1.5f;
                    string how=atFloorC?"PARKED-AT-FLOOR":(Mathf.Abs(joystick.Value.x)<.12f?"VERTICAL-DESCENT":"STEERED");
                    Debug.Log($"[FISHDBG] CAUGHT {f.Def.id} via {how} — hookMove={hookMovePx:0} "
                             +$"hookY={hookOffset.y:0}/floor={-Mathf.Max(0f,floorPx):0} joy=({joystick.Value.x:0.00},{joystick.Value.y:0.00})");
                }
                fish.Remove(f);f.Collect(new Vector2(95,300)-fishLayer.anchoredPosition);
                int before=save.Data.cargo.Count;
                save.AddForced(f.Def.id,AbsHour,f.WeightMul);
                if(GameCatalog.debugStorage)
                    Debug.Log($"[BAT] {f.Def.id}  khoang {before} -> {save.Data.cargo.Count}  "
                             +$"(frame {Time.frameCount}, con ca con lai duoi bien {fish.Count})");
                int caughtZone=SeaMap.ZoneIndexAt(f.HomeX);
                FishStock.Take(save.Data,caughtZone,SeaMap.BandIndexAt(f.DepthU));
                MissionOnCatch(f.Def,f.DepthU,caughtZone,f.Def.size*GameCatalog.WeightSizeToKg*f.WeightMul);
                if(save.OverCapacity&&!inventoryOpen)ForceStorageOpen();}
            // Release the dial (or run out the line) -> reel the hook straight back to the boat.
            if(!joystick.Held||hookTime>GameCatalog.HookLineSeconds)hookRetracting=true;}
        // The hook sprite is 110px tall before scale: the barb sits near the bottom, the eye near the top.
        // Both ends are needed — the whole shank blocks and catches, not just the point.
        const float HookBarbLocalY=-50f, HookEyeLocalY=48f;

        Vector2 HookPoint(float localY){
            float s=GameCatalog.HookScale;
            Vector2 local=new Vector2(0f,localY)*s;
            float th=hook.localEulerAngles.z*Mathf.Deg2Rad,c=Mathf.Cos(th),si=Mathf.Sin(th);
            return (Vector2)hook.anchoredPosition+new Vector2(local.x*c-local.y*si,local.x*si+local.y*c);
        }
        /// <summary>World-local position of the barb (the leading point).</summary>
        Vector2 HookTip()=>HookPoint(HookBarbLocalY);
        /// <summary>World-local position of the eye (where the line ties on).</summary>
        Vector2 HookEye()=>HookPoint(HookEyeLocalY);

        // Shortest distance from a point to the hook's shank, treated as a capsule between eye and barb.
        // Testing the barb alone let a fish sit against the middle of the hook untouched.
        float DistanceToHookBody(Vector2 p){
            Vector2 a=HookEye(),b=HookTip(),ab=b-a;
            float len2=ab.sqrMagnitude;
            if(len2<.0001f)return Vector2.Distance(p,a);
            float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/len2);
            return Vector2.Distance(p,a+ab*t);
        }

        /// <summary>How far the lowest part of the hook body hangs below the hook's own origin, in px.</summary>
        float HookReachBelowOrigin(){
            float originY=hook.anchoredPosition.y;
            return Mathf.Max(0f,originY-Mathf.Min(HookTip().y,HookEye().y));
        }
        // Bubbles fire on a sharp change in the hook's REAL movement, not in the movement the joystick
        // asked for — so bottoming out at max depth or hitting the surface clamp counts exactly like a
        // hard flick does. Both are the water resisting, which is what the bubbles are there to show.
        void TrackHookImpact(float dt){
            // Report WHICH guard bailed rather than silently doing nothing — that is what made the missing
            // bubbles impossible to diagnose from the outside.
            // Leftovers from the bubble hunt. Behind the shared flag: this one fired every second of
            // every cast, in shipped builds as much as in the editor.
            if(GameCatalog.debugStorage&&Time.unscaledTime-lastHookDebug>1f){
                lastHookDebug=Time.unscaledTime;
                Debug.Log($"[Hook] dt={dt:0.000} bubbles={(bubbles!=null)} hook={(hook!=null)} "
                          +$"hookActive={(hook!=null&&hook.gameObject.activeSelf)} mode={mode} "
                          +$"vel={(dt>0f?(hookOffset-hookPrevOffset).magnitude/dt:0f):0} px/s");
            }
            if(dt<=0f||bubbles==null||hook==null||!hook.gameObject.activeSelf)return;
            Vector2 vel=(hookOffset-hookPrevOffset)/dt;
            hookPrevOffset=hookOffset;
            bubbles.ReportVelocity(HookTip(),vel,dt);   // HookBubbles decides what counts as being checked
        }
        float lastHookDebug;
        float _fishDbgT;   // throttle for the [FISHDBG] catch trace
        void RetractHook(float dt){float step=GameCatalog.HookRetract*save.HookSpeedMultiplier*GameCatalog.PixelsPerUnit*dt;float dist=hookOffset.magnitude;if(dist<=step){hookOffset=Vector2.zero;hook.anchoredPosition=new Vector2(0,SeaMap.HookRestY);DrawLine();EndFishing();return;}hookOffset-=hookOffset/dist*step;hook.anchoredPosition=new Vector2(0,SeaMap.HookRestY)+hookOffset;DrawLine();}
        // The fishing line is a little Verlet rope pinned at the rod tip and the hook. It TRAILS/LAGS behind
        // the hook as it moves (inertia in the interior nodes) instead of snapping to a straight segment, and
        // it never keeps the old sharp movement history (each frame relaxes toward a clean chain). LineSagPx
        // adds gravity + slack so the line can bow; at 0 it hangs taut and straight when the hook is still.
        void DrawLine(){Vector2 h=hook.anchoredPosition;
            // Record the hook's ACTUAL path: the line trails along where the hook has travelled (rod tip ->
            // logged path points -> current hook), so it follows the hook's movement instead of a straight arc.
            if(trail.Count==0||Vector2.Distance(trail[trail.Count-1],h)>GameCatalog.LineTrailMinDist){trail.Add(h);if(trail.Count>GameCatalog.LineTrailMaxPoints)trail.RemoveAt(0);}
            linePts.Clear();linePts.Add(new Vector2(95,300));linePts.AddRange(trail);linePts.Add(h);
            if(lineSegs==null)return;int s=0;
            for(int i=0;i<linePts.Count-1&&s<lineSegs.Length;i++){Vector2 a=linePts[i],b=linePts[i+1];float len=Vector2.Distance(a,b);if(len<.5f)continue;var rt=lineSegs[s].rectTransform;rt.anchoredPosition=(a+b)*.5f;rt.sizeDelta=new Vector2(len+1.5f,GameCatalog.LineWidthPx);rt.localEulerAngles=new Vector3(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);if(!lineSegs[s].gameObject.activeSelf)lineSegs[s].gameObject.SetActive(true);s++;}
            for(int i=s;i<lineSegs.Length;i++)if(lineSegs[i].gameObject.activeSelf)lineSegs[i].gameObject.SetActive(false);}

        void TickFish(float dt){
            // Fish are NOT spawned in front of the player. They live at fixed world spots (their HomeX)
            // and scroll past as the boat sails. We only slowly top up the field with fish placed OFF-SCREEN,
            // so depletion recovers over time but nothing ever pops into view next to the player.
            FishStock.Tick(save.Data,dt);   // every region recovers, including the ones you are not in
            spawnTimer-=dt;if(spawnTimer<=0){spawnTimer=GameCatalog.SpawnBaseInterval;ReplenishFishField(2,true);}
            // While actively casting, hand fish the hook position so they flee from it (demo/real-game parity).
            // Inside harbour water the hook is invisible to the fish as well — they neither flee it nor
            // get caught by it, so the truce runs both ways.
            bool safeWater=GameCatalog.InSafeZone(boatX);
            Vector2? hookLocal=(mode==Mode.Fishing&&!hookRetracting&&!safeWater)?(Vector2?)(hook.anchoredPosition-fishLayer.anchoredPosition):null;
            float reach=SeaMap.UnlockedDepthU(save.Tier);
            // Docked is safe: Update never runs in Harbor mode, so simply being on the sea screen at night
            // is what puts you in reach. Sail out again and the hunt resumes exactly where it left off.
            bool hostile=IsNight&&mode!=Mode.Harbor&&!safeWater;
            // Hull height in fishLayer-local space — hunters climb to this before they can bite.
            float boatLocalY=(boat!=null?boat.rectTransform.anchoredPosition.y:340f)
                             -(fishLayer!=null?fishLayer.anchoredPosition.y:0f);
            attackGap-=dt;
            for(int i=fish.Count-1;i>=0;i--){
                if(fish[i]==null){fish.RemoveAt(i);continue;}
                var f=fish[i];
                f.SetLocked(f.DepthU>reach);   // below the earned water = silhouette, uncatchable
                f.Tick(dt,Time.time,hookLocal,boatX,hostile,boatLocalY);
                if(f.ConsumeAttack())TakeBite(f);
            }}

        // One bite at a time. Each creature already staggers itself, but a pack can still land together by
        // chance, and losing a third of the hull in one frame reads as a bug rather than as an ambush.
        float attackGap;
        void TakeBite(FishActor attacker){
            if(attackGap>0f)return;
            attackGap=GameCatalog.BiteMinGap;
            if(attacker.Def.id=="kraken")PlayKrakenAttack();
            int dmg=Mathf.Max(1,Mathf.RoundToInt(attacker.Def.atk));
            save.Data.hullHp=Mathf.Max(0,save.Data.hullHp-save.DamageAfterArmor(dmg));
            save.Store();
            TriggerHitFx();   // shake + red flash is the feedback; the harbour text is not on screen here
            if(save.Data.hullHp<=0)Wreck();
        }

        // The hull tier decides how much water is unlocked AND how far the view pulls back to show it, so
        // both are applied together. Existing fish are re-placed and re-scaled against the new ruler,
        // otherwise an ascension would leave the whole field sitting at the old depths.
        public void ApplyDepthScale(){
            GameCatalog.DepthPx=SeaMap.DepthPx(save.Tier);
            // More water unlocked also means a longer night out in it.
            GameCatalog.NightSeconds=SeaMap.NightSecondsFor(save.Tier);
            lastHullTier=save.Tier;
            float reach=SeaMap.UnlockedDepthU(save.Tier);
            for(int i=0;i<fish.Count;i++){
                var f=fish[i];
                if(f==null||f.Rect==null)continue;
                f.Reposition(DepthToLocalY(f.DepthU),FishWidthPx(f.Def));
                f.SetLocked(f.DepthU>reach);
            }
        }

        // Fill every region up to its target count in one shot (used at start / new day / reset).
        void PopulateFishField()=>ReplenishFishField(GameCatalog.FishFieldMax,false);

        /// <summary>True once the clock has passed dusk. Drives which half of the species list spawns.</summary>
        public bool IsNight=>phaseTime>=GameCatalog.DaySeconds;

        // Dusk and dawn swap the entire field. The outgoing shoal fades rather than vanishing, and the
        // incoming one fades up from nothing, so the changeover reads as the sea turning over.
        void SwapFishField(){
            for(int i=0;i<fish.Count;i++)if(fish[i]!=null)fish[i].Dismiss(GameCatalog.PhaseFadeSeconds);
            fish.Clear();          // the actors delete themselves once faded; they are no longer ours
            PopulateFishField();
        }

        /// <summary>Deepest the line may go right here: the unlocked band, the sea bed, and the line's own
        /// reach, whichever runs out first.</summary>
        public float MaxCastDepthU()
            => Mathf.Min(SeaMap.PlayableDepthU(boatX,save.Tier),GameCatalog.HookMaxDepthUnits);

        /// <summary>fishLayer-local y for a depth, on the same ruler the hook uses.</summary>
        float DepthToLocalY(float depthU)
            => SeaMap.HookRestY-depthU*GameCatalog.DepthPx-(fishLayer!=null?fishLayer.anchoredPosition.y:0f);

        // Every (zone x band) pair is its own region with its own target population. Locked bands are
        // populated too — the player is meant to SEE that the deep is busy before earning the right to fish
        // it — they just render as silhouettes (see TickFish).
        void ReplenishFishField(int maxAdd,bool requireOffscreen){
            int added=0;
            // Start scanning at the boat's own zone and wrap. Scanning 1..9 in order would hand every spare
            // slot to the home shallows whenever the global cap binds, leaving the far sea empty exactly
            // where the payoff is supposed to be.
            int start=Mathf.Clamp(SeaMap.ZoneIndexAt(boatX)-1,0,SeaMap.Zones.Count-1);
            for(int k=0;k<SeaMap.Zones.Count&&fish.Count<GameCatalog.FishFieldMax&&added<maxAdd;k++){
                int zi=(start+k)%SeaMap.Zones.Count;
                var zone=SeaMap.Zones[zi];
                SeaMap.ZoneBounds(zone.index,out float lo,out float hi);
                lo=Mathf.Clamp(lo,0f,GameCatalog.SeaLength);
                hi=Mathf.Clamp(hi,0f,GameCatalog.SeaLength);
                if(hi<=lo)continue;
                for(int bi=0;bi<SeaMap.Bands.Count&&fish.Count<GameCatalog.FishFieldMax&&added<maxAdd;bi++){
                    var band=SeaMap.Bands[bi];
                    // Zones 1-3 have no band C at all: the shelf reaches the floor of band B there.
                    float seabed=Mathf.Min(SeaMap.Zones[zi].shelfDepthU,SeaMap.DeepestU);
                    if(band.topU>=seabed)continue;
                    float density=GameCatalog.FishFieldDensity*zone.densityMul*band.densityMul;
                    if(IsNight)density*=GameCatalog.EvilDensityMul;   // the sea empties out after dark
                    density*=FishStock.Of(save.Data,zone.index,bi);   // ...and where you have been fishing
                    int target=Mathf.RoundToInt((hi-lo)*density);
                    int have=CountFishIn(lo,hi,band);
                    while(have<target&&fish.Count<GameCatalog.FishFieldMax&&added<maxAdd){
                        float x=PickFishX(lo,hi,requireOffscreen);
                        if(float.IsNaN(x))break;   // no valid spot this call — try again next tick
                        if(!SpawnFishIn(band,zone,x))break;
                        have++;added++;
                    }
                }
            }
        }

        int CountFishIn(float lo,float hi,BandDef band){
            int n=0;
            for(int i=0;i<fish.Count;i++){
                var f=fish[i];
                if(f==null||f.HomeX<lo||f.HomeX>=hi)continue;
                if(f.DepthU>=band.topU&&f.DepthU<band.bottomU)n++;
            }
            return n;
        }

        // Roll a world x inside [lo,hi] that isn't inside a port's safe radius (and, if asked, is off-screen).
        float PickFishX(float lo,float hi,bool requireOffscreen){
            for(int t=0;t<12;t++){
                float x=Random.Range(lo,hi);
                if(GameCatalog.AtPort(x)!=null)continue;
                if(requireOffscreen&&Mathf.Abs(x-boatX)*GameCatalog.WorldScrollPpu<GameCatalog.FishCullPx)continue;
                if(Random.value>GameCatalog.PortDensityFactor(x))continue; // thin fish out near any harbor
                return x;
            }
            return float.NaN;
        }

        // Weighted pick across the species that can live in this region. Rarity is punished near the surface
        // and near home, and rewarded deep and far — so the same roll produces bream at A1 and ghost tuna at C9.
        // Day and night draw from the same species list, split by whether the species hunts. A candidate
        // also has to have room left under its own maxAlive cap — that is what keeps the kraken unique.
        // When each night species is next allowed to appear. Keyed by species, cleared at dawn.
        readonly Dictionary<string,float> evilNextSpawn=new();

        bool Eligible(FishDef def,BandDef band,float seabed,SeaZoneDef zone){
            if(def.Evil!=IsNight)return false;
            // The tentacles are a set piece, not weather: KrakenEvent decides when the arm arrives and
            // places all six itself, so the ambient field must never hand one out.
            if(def.id==KrakenId)return false;
            // Distance gate, separate from the depth gate. Band C opens the water; this keeps the deep
            // hunters out of the home stretch even once you can reach their depth.
            int zi=zone!=null?zone.index:1;
            if(zi<def.minZone||zi>def.maxZone)return false;
            // While the arm is up, nothing else hunts. The sea belongs to one thing at a time.
            if(def.Evil&&krakenPresent)return false;
            if(def.Evil&&evilNextSpawn.TryGetValue(def.id,out var t)&&Time.time<t)return false;
            if(!SeaMap.Inhabits(def,band,seabed))return false;
            // Night hunters are capped by head count per zone, not just by maxAlive: zone 1 holds one.
            int cap=def.Evil?GameCatalog.EvilAliveAt(def,zone!=null?zone.index:1)
                            :def.maxAlive;
            if(cap>0&&CountSpecies(def.id)>=cap)return false;
            return true;
        }
        int CountSpecies(string id){
            int n=0;
            for(int i=0;i<fish.Count;i++)if(fish[i]!=null&&!fish[i].Leaving&&fish[i].Def.id==id)n++;
            return n;
        }

        bool SpawnFishIn(BandDef band,SeaZoneDef zone,float homeX){
            float seabed=SeaMap.SeabedDepthU(homeX);
            float total=0f;
            for(int i=0;i<GameCatalog.Fish.Count;i++){
                var def=GameCatalog.Fish[i];
                if(!Eligible(def,band,seabed,zone))continue;
                total+=SeaMap.SpawnWeight(def,band,zone);
            }
            if(total<=0f)return false;   // nothing lives in this region right now — stop asking for this band
            float roll=Random.value*total;
            for(int i=0;i<GameCatalog.Fish.Count;i++){
                var def=GameCatalog.Fish[i];
                if(!Eligible(def,band,seabed,zone))continue;
                roll-=SeaMap.SpawnWeight(def,band,zone);
                if(roll>0f)continue;
                // Day fish take the depth x distance ramp. Night hunters deliberately do NOT: their own
                // base HP (34 / 136 / 408) already encodes the depth axis, so band.difficultyMul on top
                // counted depth twice, and zone.evilHpMul is the entire distance ramp by itself.
                float difficulty=def.Evil?zone.evilHpMul:band.difficultyMul*zone.difficultyMul;
                // Keep rolling for a spot with clear water around it. Depth alone usually resolves it, so
                // the x is only nudged after a few failures -- moving it every time would fight the density
                // ramp that decides where in the zone this fish belongs.
                float w=FishWidthPx(def);
                for(int attempt=0;attempt<10;attempt++){
                    float x=attempt<4?homeX:homeX+Random.Range(-1f,1f)*(attempt-3)*.6f;
                    float depth=SeaMap.RollDepth(def,band,seabed);
                    if(!FishSpotClear(x,depth,w))continue;
                    SpawnFishAt(def,x,depth,difficulty,band.fleeMul*zone.fleeMul);
                    if(def.Evil)evilNextSpawn[def.id]=Time.time+GameCatalog.evilRespawnSeconds;
                    return true;
                }
                return false;   // this pocket of water is already full
            }
            return false;
        }

        /// <summary>On-screen width of this species at the current zoom — also the spacing yardstick.</summary>
        float FishWidthPx(FishDef def)=>
            (fishSizeBase+Mathf.Max(GameCatalog.MinFishSize,def.size)*fishSizePer)*fishScale*GameCatalog.FishSizeScale*DepthZoom();

        /// <summary>
        /// Is there room for a fish of this width at this spot? Two fish are never allowed to overlap:
        /// a stack in one place is caught almost instantly (a sardine dies in about half a second), so it
        /// reads as a single cast pulling up three fish.
        ///
        /// Measured in SCREEN pixels, not world units, because that is what "overlapping" means to the
        /// player and because the vertical axis is depth-scaled while the horizontal is not.
        /// </summary>
        bool FishSpotClear(float homeX,float depthU,float widthPx){
            float y=DepthToLocalY(depthU);
            for(int i=0;i<fish.Count;i++){
                var o=fish[i];
                if(!o||o.Leaving)continue;
                float dx=(homeX-o.HomeX)*GameCatalog.WorldScrollPpu;
                float dy=y-DepthToLocalY(o.DepthU);
                float need=(widthPx+FishWidthPx(o.Def))*.5f*GameCatalog.fishSpawnSpacing;
                if(dx*dx+dy*dy<need*need)return false;
            }
            return true;
        }

        void SpawnFishAt(FishDef def,float homeX,float depthU,float difficultyMul,float fleeMul){
            float w=FishWidthPx(def);
            var rect=RuntimeUI.Rect(fishLayer,"Fish",Vector2.zero,Vector2.one);
            var actor=rect.gameObject.AddComponent<FishActor>();
            actor.Init(def,homeX,DepthToLocalY(depthU),w,depthU,difficultyMul,fleeMul);
            actor.SetLocked(depthU>SeaMap.UnlockedDepthU(save.Tier));
            fish.Add(actor);
        }

        // Fish shrink as the view pulls back, so a zoomed-out sea reads as deeper rather than as bigger fish.
        float DepthZoom()=>GameCatalog.DepthPx/Mathf.Max(1f,SeaMap.DepthPx(0));

        /// <summary>
        /// Cancels the zoom out of the hook's vertical speed, so the hook always covers the same number of
        /// SCREEN pixels per second whatever tier you are on.
        ///
        /// Without it, unlocking band C halves DepthPx and the hook visibly crawls — and worse, the line
        /// still only pays out for 12 seconds while the water it has to cross has doubled to 46 units, so
        /// the deep becomes unreachable by simple arithmetic. Multiplying it back means each tier descends
        /// its own band in roughly the same time.
        /// </summary>
        float DepthSpeedGain()=>SeaMap.DepthPx(0)/Mathf.Max(1f,GameCatalog.DepthPx);
    }
}
