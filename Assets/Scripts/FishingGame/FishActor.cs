using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    public sealed class FishActor:MonoBehaviour
    {
        public FishDef Def {get;private set;} public float Hp {get;private set;} public RectTransform Rect {get;private set;}
        public float WeightMul {get;private set;}  // random per-fish; HP, sale value and visual size scale with it
        // World x (sea-units) where this fish LIVES. The fish waits here and scrolls past like a port/obstacle
        // as the boat sails; it only wanders a little (FishRoamHalfWidthPx) around this spot. This is what makes
        // fish sit in their zone waiting to be caught instead of appearing in front of the player.
        public float HomeX {get;private set;}
        // Depth in sea-units. Decides which band the fish belongs to, so the controller can tell whether the
        // player's hull tier is allowed to reach it.
        public float DepthU {get;private set;}
        // A fish below the player's unlocked depth still lives its normal life — it just renders as a dark
        // shape and cannot be damaged, so the deep reads as "full of something" long before it is reachable.
        public bool Locked {get;private set;}
        // After being hit a fish bolts away from the hook. How fast and for how long is a REGION stat now,
        // not a constant: deep water fish run harder, so the same hp bar takes more chasing to whittle down.
        float fleeSeconds,fleeSpeedMul,fleeRoamPx;
        float direction,baseY,phase,fleeUntil,lastNow,turnCooldown,maxHp,hpW,hpH,wanderX;
        Image image; // own sprite — toggled off when the fish scrolls off-screen (culling)
        RectTransform hpBar,hpFillRt; Image hpFill; // HP bar above the fish, shown once it takes damage
        // Collect animation: once caught, tween into the basket (rod tip) then self-destroy (real-game parity).
        bool collecting; Vector2 collectStart,collectTarget; float collectT;
        // Dusk/dawn changeover: the whole field fades out rather than blinking away, so the shoals leaving
        // and the night things arriving reads as a shift change instead of a glitch.
        bool dismissing; float fadeT,fadeSeconds=1f,spawnFadeT;
        // Night hunters only. attackTimer is seeded RANDOMLY so a shoal that spawned together does not
        // strike in unison — the stagger is what makes a pack feel like a pack.
        float attackTimer; bool attackReady;
        // A strike used to be invisible: HP dropped and nothing on screen moved. The lunge is the tell.
        float lungeT; const float LungeSeconds=.45f;
        float dartUntil, dartMul=1f, dartCooldown;   // short random bursts while cruising
        public bool IsEvil => Def!=null&&Def.Evil;
        public bool Hunting {get;private set;}
        public void Collect(Vector2 targetLocal){collecting=true;collectStart=Rect.anchoredPosition;collectTarget=targetLocal;collectT=0;if(hpBar!=null)hpBar.gameObject.SetActive(false);}
        /// <summary>Fade out and delete — the field turning over at dusk or dawn.</summary>
        public void Dismiss(float seconds){
            if(dismissing)return;
            dismissing=true;fadeSeconds=Mathf.Max(.05f,seconds);fadeT=0f;
            if(hpBar!=null)hpBar.gameObject.SetActive(false);
        }
        /// <summary>True once this fish has started leaving; it can no longer be hit or counted.</summary>
        public bool Leaving=>dismissing;

        void Update(){
            if(dismissing){
                fadeT+=Time.deltaTime;
                ApplyColour();
                if(fadeT>=fadeSeconds)Destroy(gameObject);
                return;
            }
            if(spawnFadeT<1f&&image!=null){
                spawnFadeT=Mathf.Min(1f,spawnFadeT+Time.deltaTime/Mathf.Max(.05f,fadeSeconds));
                ApplyColour();
            }
            if(!collecting)return;collectT+=Time.deltaTime;float k=Mathf.Clamp01(collectT/GameCatalog.CollectSeconds);Rect.anchoredPosition=Vector2.LerpUnclamped(collectStart,collectTarget,k*k);Rect.localScale=new Vector3(-direction,1,1)*(1-k);if(k>=1)Destroy(gameObject);}
        // homeX = world position (sea-units) this fish belongs to; y = fishLayer-local depth line.
        public void Init(FishDef def,float homeX,float y,float width,float depthU,float difficultyMul,float fleeMul)
        {Def=def;HomeX=homeX;DepthU=depthU;
         spawnFadeT=0f;attackTimer=Random.Range(.35f,Mathf.Max(.5f,def.attackEvery));   // no two strike together
         fleeSeconds=SeaMap.FleeSecondsBase*fleeMul;fleeSpeedMul=SeaMap.FleeSpeedBase*fleeMul;
         fleeRoamPx=GameCatalog.FishRoamHalfWidthPx*SeaMap.FleeRoamMul*fleeMul;WeightMul=Random.Range(GameCatalog.WeightMin,GameCatalog.WeightMax);Hp=def.hp*WeightMul*Mathf.Max(.1f,difficultyMul);Rect=(RectTransform)transform;Rect.name="Fish-"+def.id;Rect.anchoredPosition=new Vector2(0,y);wanderX=Random.Range(-GameCatalog.FishRoamHalfWidthPx,GameCatalog.FishRoamHalfWidthPx);
         float w=width*Mathf.Lerp(.9f,1.18f,Mathf.InverseLerp(GameCatalog.WeightMin,GameCatalog.WeightMax,WeightMul));Rect.sizeDelta=new Vector2(w,w/def.aspect);
         image=Rect.gameObject.AddComponent<Image>();image.sprite=RuntimeUI.Sprite("fish/species/"+def.art);image.preserveAspect=true;direction=Random.value<.5f?-1:1;baseY=y;phase=Random.value*6.28f;
         maxHp=Hp;BuildHpBar(w/def.aspect);ApplyColour();}
        void BuildHpBar(float fishH){
            hpW=Rect.sizeDelta.x*.6f;
            hpH=Mathf.Clamp(hpW*.19f,12f,18f);
            var barGO=new GameObject("HpBar",typeof(RectTransform));hpBar=barGO.GetComponent<RectTransform>();hpBar.SetParent(Rect,false);hpBar.anchorMin=hpBar.anchorMax=hpBar.pivot=new Vector2(.5f,.5f);hpBar.anchoredPosition=new Vector2(0,fishH*.5f+hpH*.75f+4);hpBar.sizeDelta=new Vector2(hpW,hpH);
            var bg=barGO.AddComponent<Image>();bg.sprite=RuntimeUI.Sprite("UI/Gameplay/HealthBar/fish-health-bar-frame");bg.raycastTarget=false;
            var fillGO=new GameObject("Fill",typeof(RectTransform));hpFillRt=fillGO.GetComponent<RectTransform>();hpFillRt.SetParent(hpBar,false);hpFillRt.anchorMin=new Vector2(.075f,.23f);hpFillRt.anchorMax=new Vector2(.925f,.77f);hpFillRt.offsetMin=hpFillRt.offsetMax=Vector2.zero;
            hpFill=fillGO.AddComponent<Image>();hpFill.sprite=RuntimeUI.Sprite("UI/Gameplay/HealthBar/fish-health-bar-fill");hpFill.type=Image.Type.Filled;hpFill.fillMethod=Image.FillMethod.Horizontal;hpFill.fillOrigin=0;hpFill.fillAmount=1;hpFill.raycastTarget=false;
            hpBar.gameObject.SetActive(false);}
        // sprite art faces the -x direction, so flip by -direction to face the way it swims.
        // The fish's on-screen x is its HOME position scrolled by the boat, plus a small local wander,
        // so it stays put in its zone and drifts past as you sail — it does NOT follow the camera.
        // Strike windows and run speeds live on GameCatalog so they can be tuned from game-data.json.
        // Both axes are tested: checking only the horizontal gap let a fish on the sea floor bite a boat
        // on the surface.
        // A dive is finished once it is back within this much of its home depth.
        const float DiveSettlePx=14f;

        // The attack loop. Cruise at home depth -> Rise at the hull -> bite -> Dive back down -> repeat.
        enum Hunt{Cruise,Rise,Dive}
        Hunt huntPhase=Hunt.Cruise;
        float huntY;   // vertical offset from baseY, driven by the phase machine

        /// <summary>
        /// How far PAST its own cull range a fish has to be before it stops thinking. The margin exists so
        /// a fish hovering right on the edge does not flip between ticking and parked every frame as the
        /// boat rocks — it has to be clearly gone before it goes quiet, and clearly back before it wakes.
        /// </summary>
        const float SleepMarginPx=240f;

        public void Tick(float dt,float now,Vector2? hook,float boatX,bool hostile,float boatLocalY){lastNow=now;phase+=dt*2;bool fleeing=now<fleeUntil;
            // ── park anything far off-screen ──────────────────────────────────────────────────────────
            // The field holds ~220 fish spread over the whole sea while the visible window is only about
            // ±18 sea units, so roughly nine in ten are off-screen at any moment. The cull test used to sit
            // at the BOTTOM of this method, which meant every one of them still ran the full hunt state
            // machine, two random rolls, the harbour push-out and a transform write — and then had its
            // sprite switched off. This does the same test first and skips all of it.
            //
            // Position is DERIVED from HomeX/wanderX rather than integrated frame by frame, so a parked
            // fish resumes exactly where it should the moment it comes back into range: only wanderX stops
            // advancing, and nobody can see a fish that is not drawn.
            float parkScroll=(HomeX-boatX)*GameCatalog.WorldScrollPpu;
            float parkRange=Mathf.Max(GameCatalog.FishCullPx,IsEvil?Def.chasePx:0f)+SleepMarginPx;
            // A hunter mid-charge keeps thinking however far it has been left behind: parking it would
            // freeze it halfway up its arc, and it would resume from that pose whenever the boat returned.
            if(!collecting&&!dismissing&&lungeT<=0f&&huntPhase==Hunt.Cruise
               &&Mathf.Abs(parkScroll+wanderX)>parkRange){
                if(image!=null&&image.enabled)image.enabled=false;
                if(hpBar!=null&&hpBar.gameObject.activeSelf)hpBar.gameObject.SetActive(false);
                return;
            }

            // Idle dart: only while calmly cruising — a fleeing or hunting fish already has its own speed.
            if(!fleeing&&huntPhase==Hunt.Cruise){
                dartCooldown-=dt;
                if(now>dartUntil&&dartCooldown<=0f&&Random.value<SeaMap.DartChancePerSec*dt){
                    dartUntil=now+Random.Range(SeaMap.DartSeconds.x,SeaMap.DartSeconds.y);
                    dartMul=Random.Range(SeaMap.DartSpeedMul.x,SeaMap.DartSpeedMul.y);
                    dartCooldown=Random.Range(1.2f,3.5f);   // never two darts back to back
                }
            }
            float dart=(!fleeing&&now<dartUntil)?dartMul:1f;
            float speed=Def.speed*GameCatalog.FishSwimPpu*(fleeing?fleeSpeedMul:dart);
            float scroll=(HomeX-boatX)*GameCatalog.WorldScrollPpu;
            // Night hunters close on the boat by migrating their HOME position, so they scroll toward you
            // through the same world-space maths as everything else instead of being pinned to the camera.
            Hunting=false;
            if(IsEvil&&hostile&&!Locked&&!dismissing){
                bool inRange=Mathf.Abs(scroll+wanderX)<Def.chasePx;
                float rise=speed*GameCatalog.HuntRiseSpeedMul;   // the charge
                float dive=speed*GameCatalog.HuntDiveSpeedMul;   // the retreat, slower than the charge
                switch(huntPhase){
                    case Hunt.Cruise:
                        attackTimer-=dt;
                        if(inRange&&attackTimer<=0f)huntPhase=Hunt.Rise;
                        break;

                    case Hunt.Rise:{
                        Hunting=true;
                        // Close on the hull in both axes at once: swim across AND up.
                        float closeU=rise/Mathf.Max(1f,GameCatalog.WorldScrollPpu);
                        HomeX=GameCatalog.PushOutOfPorts(Mathf.MoveTowards(HomeX,boatX,closeU*dt));
                        scroll=(HomeX-boatX)*GameCatalog.WorldScrollPpu;
                        wanderX=Mathf.MoveTowards(wanderX,0f,rise*dt);
                        float wantY=boatLocalY-GameCatalog.HuntStandoffPx-baseY;
                        huntY=Mathf.MoveTowards(huntY,wantY,rise*dt);
                        if(Mathf.Abs(scroll+wanderX)<GameCatalog.HuntStrikeRangeX
                           &&Mathf.Abs(wantY-huntY)<GameCatalog.HuntStrikeRangeY){
                            attackReady=true;lungeT=LungeSeconds;
                            huntPhase=Hunt.Dive;   // bite, then get back down out of reach
                        }
                        break;
                    }

                    case Hunt.Dive:
                        Hunting=true;
                        huntY=Mathf.MoveTowards(huntY,0f,dive*dt);
                        if(Mathf.Abs(huntY)<DiveSettlePx){
                            huntY=0f;huntPhase=Hunt.Cruise;
                            attackTimer=Def.attackEvery;   // cooldown starts once it is home, not on the bite
                        }
                        break;
                }
            }
            else{huntY=Mathf.MoveTowards(huntY,0f,speed*dt);huntPhase=Hunt.Cruise;}
            if(Hunting)direction=(scroll+wanderX)<0f?1:-1;   // always face the boat while closing
            else if(fleeing&&hook.HasValue)direction=(scroll+wanderX)<hook.Value.x?-1:1;
            else{turnCooldown-=dt;if(turnCooldown<=0&&Random.value<GameCatalog.FishTurnChance*dt*10f){direction*=-1;turnCooldown=.5f;}}
            // A bolting fish gets a wider leash so it actually gets away; once it calms down it eases back
            // inside its normal roam instead of snapping, which would look like a teleport.
            float roam=fleeing?fleeRoamPx:GameCatalog.FishRoamHalfWidthPx*(dart>1f?1.5f:1f);
            if(!Hunting)wanderX+=direction*speed*dt;
            if(!Hunting&&Mathf.Abs(wanderX)>roam){
                if(fleeing)direction*=-1;
                wanderX=Mathf.MoveTowards(wanderX,Mathf.Sign(wanderX)*roam,Mathf.Max(speed,120f)*dt);
            }
            // Harbour water is off limits: anything that drifts or charges into it is turned back at the
            // edge. Without this a hunter would happily follow the boat right up to the quay.
            float worldX=HomeX+wanderX/Mathf.Max(1f,GameCatalog.WorldScrollPpu);
            float outside=GameCatalog.PushOutOfPorts(worldX);
            if(!Mathf.Approximately(outside,worldX)){
                wanderX+=(outside-worldX)*GameCatalog.WorldScrollPpu;
                direction=outside>worldX?1:-1;
            }
            float sx=scroll+wanderX;
            // The lunge: dart at the hull and recoil, over LungeSeconds. Purely visual — the damage has
            // already been applied by the time this plays.
            if(lungeT>0f){
                lungeT-=dt;
                float punch=Mathf.Sin(Mathf.Clamp01(lungeT/LungeSeconds)*Mathf.PI);
                sx=Mathf.Lerp(sx,0f,punch*.6f);
                ApplyColour();
            }
            float bob=Mathf.Sin(phase)*GameCatalog.FishWanderPx*(huntPhase==Hunt.Cruise?1f:.25f);
            Rect.anchoredPosition=new Vector2(sx,baseY+huntY+bob);Rect.localScale=new Vector3(-direction,1,1);
            bool vis=Mathf.Abs(sx)<Mathf.Max(GameCatalog.FishCullPx,IsEvil?Def.chasePx:0f);if(image!=null&&image.enabled!=vis)image.enabled=vis;
            if(hpBar!=null){bool show=vis&&!Locked&&Hp<maxHp-.01f;if(hpBar.gameObject.activeSelf!=show)hpBar.gameObject.SetActive(show);if(show){hpFill.fillAmount=Mathf.Clamp01(Hp/maxHp);hpBar.localScale=new Vector3(-direction,1,1);}}}
        /// <summary>Move this fish onto a new depth ruler (called when ascending changes the zoom).</summary>
        public void Reposition(float y,float width){
            baseY=y;
            float w=width*Mathf.Lerp(.9f,1.18f,Mathf.InverseLerp(GameCatalog.WeightMin,GameCatalog.WeightMax,WeightMul));
            Rect.sizeDelta=new Vector2(w,w/Def.aspect);
            Rect.anchoredPosition=new Vector2(Rect.anchoredPosition.x,y);
            if(hpBar!=null){hpW=Rect.sizeDelta.x*.6f;hpH=Mathf.Clamp(hpW*.19f,12f,18f);hpBar.anchoredPosition=new Vector2(0,Rect.sizeDelta.y*.5f+hpH*.75f+4);hpBar.sizeDelta=new Vector2(hpW,hpH);}
        }

        /// <summary>Read and clear a pending strike. The controller applies the damage.</summary>
        public bool ConsumeAttack(){if(!attackReady)return false;attackReady=false;return true;}

        public bool Hit(float amount){if(Locked||dismissing)return false;Hp-=amount;if(amount>0)fleeUntil=lastNow+fleeSeconds;return Hp<=0;}

        /// <summary>
        /// Carry a wound over from a previous encounter. Used when the Drowned One comes back: a
        /// tentacle you half severed and then fled from should still be half severed.
        /// Clamped above zero, because a creature that arrives already dead never gets to be caught.
        /// </summary>
        public void SetWoundedHp(float hp){Hp=Mathf.Max(1f,hp);}

        /// <summary>Show this fish as an unreachable silhouette (or restore it to normal).</summary>
        public void SetLocked(bool locked){
            if(Locked==locked)return;
            Locked=locked;
            ApplyColour();
            if(locked&&hpBar!=null)hpBar.gameObject.SetActive(false);
        }
        // The silhouette tint and the dusk fade both want to write image.color, so they go through here
        // instead of overwriting each other — a locked fish still has to fade out at dawn.
        void ApplyColour(){
            if(image==null)return;
            var c=Locked?LockedTint:Color.white;
            // Flare red through the lunge so the strike still reads when the creature is small or far.
            if(lungeT>0f)c=Color.Lerp(c,new Color(1f,.35f,.28f,c.a),Mathf.Sin(Mathf.Clamp01(lungeT/LungeSeconds)*Mathf.PI));
            c.a*=dismissing?Mathf.Clamp01(1f-fadeT/fadeSeconds):spawnFadeT;
            image.color=c;
        }
        static readonly Color LockedTint=new(.06f,.11f,.12f,.62f);
    }
}
