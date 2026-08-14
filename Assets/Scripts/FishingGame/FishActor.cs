using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    public sealed class FishActor:MonoBehaviour
    {
        public FishDef Def {get;private set;} public float Hp {get;private set;} public RectTransform Rect {get;private set;}
        public float WeightMul {get;private set;}  // random per-fish; HP, sale value and visual size scale with it
        // Demo/real-game parity: after being hit a fish flees for FleeSeconds at FleeSpeedMul,
        // swimming away from the hook horizontally.
        const float FleeSeconds=2.5f, FleeSpeedMul=2.2f;
        float direction,baseY,phase,fleeUntil,lastNow,turnCooldown,maxHp,hpW;
        RectTransform hpBar,hpFillRt; Image hpFill; // HP bar above the fish, shown once it takes damage
        // Collect animation: once caught, tween into the basket (rod tip) then self-destroy (real-game parity).
        bool collecting; Vector2 collectStart,collectTarget; float collectT;
        public void Collect(Vector2 targetLocal){collecting=true;collectStart=Rect.anchoredPosition;collectTarget=targetLocal;collectT=0;if(hpBar!=null)hpBar.gameObject.SetActive(false);}
        void Update(){if(!collecting)return;collectT+=Time.deltaTime;float k=Mathf.Clamp01(collectT/GameCatalog.CollectSeconds);Rect.anchoredPosition=Vector2.LerpUnclamped(collectStart,collectTarget,k*k);Rect.localScale=new Vector3(-direction,1,1)*(1-k);if(k>=1)Destroy(gameObject);}
        public void Init(FishDef def,Vector2 pos,float width)
        {Def=def;WeightMul=Random.Range(GameCatalog.WeightMin,GameCatalog.WeightMax);Hp=def.hp*WeightMul;Rect=(RectTransform)transform;Rect.name="Fish-"+def.id;Rect.anchoredPosition=pos;
         float w=width*Mathf.Lerp(.9f,1.18f,Mathf.InverseLerp(GameCatalog.WeightMin,GameCatalog.WeightMax,WeightMul));Rect.sizeDelta=new Vector2(w,w/def.aspect);
         var image=Rect.gameObject.AddComponent<Image>();image.sprite=RuntimeUI.Sprite("fish/species/"+def.art);image.preserveAspect=true;direction=Random.value<.5f?-1:1;baseY=pos.y;phase=Random.value*6.28f;
         maxHp=Hp;BuildHpBar(w/def.aspect);}
        void BuildHpBar(float fishH){
            hpW=Rect.sizeDelta.x*.6f;
            var barGO=new GameObject("HpBar",typeof(RectTransform));hpBar=barGO.GetComponent<RectTransform>();hpBar.SetParent(Rect,false);hpBar.anchorMin=hpBar.anchorMax=hpBar.pivot=new Vector2(.5f,.5f);hpBar.anchoredPosition=new Vector2(0,fishH*.5f+22);hpBar.sizeDelta=new Vector2(hpW,14);
            var bg=barGO.AddComponent<Image>();bg.color=new Color(0,0,0,.6f);bg.raycastTarget=false;
            var fillGO=new GameObject("Fill",typeof(RectTransform));hpFillRt=fillGO.GetComponent<RectTransform>();hpFillRt.SetParent(hpBar,false);hpFillRt.anchorMin=new Vector2(0,0);hpFillRt.anchorMax=new Vector2(0,1);hpFillRt.pivot=new Vector2(0,.5f);hpFillRt.anchoredPosition=Vector2.zero;hpFillRt.sizeDelta=new Vector2(hpW,0);
            hpFill=fillGO.AddComponent<Image>();hpFill.raycastTarget=false;
            hpBar.gameObject.SetActive(false);}
        // sprite art faces the -x direction, so flip by -direction to face the way it swims.
        public void Tick(float dt,float now,Vector2? hook){lastNow=now;phase+=dt*2;bool fleeing=now<fleeUntil;float speed=Def.speed*GameCatalog.FishSwimPpu*(fleeing?FleeSpeedMul:1f);
            if(fleeing&&hook.HasValue)direction=Rect.anchoredPosition.x<hook.Value.x?-1:1;
            else{turnCooldown-=dt;if(turnCooldown<=0&&Random.value<GameCatalog.FishTurnChance*dt*10f){direction*=-1;turnCooldown=.5f;}}
            Rect.anchoredPosition+=new Vector2(direction*speed*dt,0);var p=Rect.anchoredPosition;p.y=baseY+Mathf.Sin(phase)*GameCatalog.FishWanderPx;Rect.anchoredPosition=p;if(Mathf.Abs(p.x)>GameCatalog.FishSpawnHalfWidthPx+120)direction*=-1;Rect.localScale=new Vector3(-direction,1,1);
            if(hpBar!=null){bool show=Hp<maxHp-.01f;if(hpBar.gameObject.activeSelf!=show)hpBar.gameObject.SetActive(show);if(show){float frac=Mathf.Clamp01(Hp/maxHp);hpFillRt.sizeDelta=new Vector2(hpW*frac,0);hpFill.color=frac>.5f?new Color(.48f,.86f,.46f):frac>.25f?new Color(.9f,.77f,.3f):new Color(.88f,.36f,.34f);hpBar.localScale=new Vector3(-direction,1,1);}}}
        public bool Hit(float amount){Hp-=amount;if(amount>0)fleeUntil=lastNow+FleeSeconds;return Hp<=0;}
    }
}
