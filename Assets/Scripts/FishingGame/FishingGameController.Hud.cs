using UnityEngine;

namespace RustyFishing
{
    // Sea HUD: clock hand, zone/danger label, speedometer, cargo/coins/HP readouts.
    public sealed partial class FishingGameController
    {
        void UpdateSeaUI(){
            var port=GameCatalog.AtPort(boatX);
            // Clock is shown ONLY by the rotating needle now (no numeric text):
            // full day+night cycle sweeps 180° (half circle); each phase (day / night) = 90° (quarter).
            float cycle=phaseTime<GameCatalog.DaySeconds?phaseTime/GameCatalog.DaySeconds*.5f:.5f+(phaseTime-GameCatalog.DaySeconds)/GameCatalog.NightSeconds*.5f;
            // Sweep left→right across the day/night arc (reversed from before, per design).
            if(clockNeedle!=null)clockNeedle.localEulerAngles=new Vector3(0,0,180*Mathf.Clamp01(cycle)-90);
            // Safe/danger is shown by the zone board (safe = at a port, danger = open water).
            UpdateZoneBoard(port!=null);
            // Speedometer + hazard (safe-speed) needle.
            // Nearest obstacle by a plain linear scan. This used to be OrderBy(...).FirstOrDefault(), which
            // SORTED the whole field every frame — O(n log n) plus an iterator and a buffer allocated per
            // frame — to read one element. Finding a minimum needs neither.
            ObstacleInstance nearby=null;float nearestDist=float.MaxValue;
            var obstacles=GameCatalog.ObstacleField;
            for(int i=0;i<obstacles.Count;i++){
                float d=Mathf.Abs(obstacles[i].x-boatX);
                if(d<nearestDist){nearestDist=d;nearby=obstacles[i];}
            }
            bool inHazard=nearby!=null&&Mathf.Abs(nearby.x-boatX)<3;
            if(speedNeedle!=null)speedNeedle.localEulerAngles=new Vector3(0,0,SpeedNeedleAngle(Mathf.Abs(boatSpeed)));
            if(safeNeedle!=null){safeNeedle.gameObject.SetActive(inHazard);if(inHazard)safeNeedle.localEulerAngles=new Vector3(0,0,SpeedNeedleAngle(nearby.safeSpeed));}
            // Shared readouts (same labels used at harbor via RefreshHarbor).
            Set(seaCargo,$"{save.Data.cargo.Count}/{save.Capacity}");
            SetCoins();
            UpdateShipReadouts();
            UpdateArrivalBanner(port);}
        // HP + speed + HP bar. Called from the sailing loop AND from RefreshHarbor, so these read
        // out correctly on both screens instead of being stuck on their design-time placeholders
        // (the sailing loop never runs while you're in the harbor).
        void UpdateShipReadouts(){
            Set(hp,$"{save.Data.hullHp}/{save.MaxHp}");
            Set(speed,$"{Mathf.Abs(boatSpeed):0.0} kn");
            UpdateHpBar();}
        // Harbor-arrival banner. The name FADES IN as the port comes into reach, holds, then fades out —
        // it used to snap to full opacity the instant the boat crossed the port radius, which read as a
        // popup rather than as the harbour resolving out of the rain.
        void UpdateArrivalBanner(PortDef port){
            if(harborZone==null)return;

            // Arriving raises the name and PINS it: while the boat sits in harbour water the sign stays up,
            // because it is a label for where you are, not an announcement that you got here.
            if(port!=null&&port!=bannerPort){
                bannerPort=port;bannerTimer=0f;bannerPinned=true;
                harborZone.text=port.name.ToUpperInvariant();
                harborZone.alpha=0f;
                harborZone.gameObject.SetActive(true);
            }
            // Casting off releases the pin — jump straight to the fade-out leg so the name drops astern
            // with the harbour instead of lingering for a fixed count first.
            if(port==null&&bannerPinned){
                bannerPinned=false;bannerPort=null;
                bannerTimer=BannerIn+BannerHold;
            }
            if(!harborZone.gameObject.activeSelf)return;

            bannerTimer+=Time.deltaTime;
            float t=bannerTimer;
            float alpha=t<BannerIn        ? t/BannerIn
                       :bannerPinned      ? 1f
                       :t<BannerIn+BannerHold ? 1f
                       :1f-(t-BannerIn-BannerHold)/BannerOut;
            harborZone.alpha=Mathf.Clamp01(alpha);
            // Ride up a little on the way in and settle — the drift is what makes the fade read as motion.
            float rise=t<BannerIn?(1f-Mathf.SmoothStep(0f,1f,t/BannerIn))*-BannerRisePx:0f;
            var rt=harborZone.rectTransform;
            rt.anchoredPosition=new Vector2(rt.anchoredPosition.x,bannerBaseY+rise);
            if(!bannerPinned&&t>=BannerIn+BannerHold+BannerOut)harborZone.gameObject.SetActive(false);}
        // HP fill bar: fillAmount = hullHp / maxHp, with an optional green→yellow→red tint.
        void UpdateHpBar(){if(hpFill==null)return;float frac=save.MaxHp>0?Mathf.Clamp01((float)save.Data.hullHp/save.MaxHp):0f;hpFill.fillAmount=frac;if(hpFillTint)hpFill.color=frac>.5f?new Color(.42f,.82f,.44f):frac>.25f?new Color(.9f,.77f,.3f):new Color(.85f,.32f,.3f);}
        // Swap the board sprite + text between safe/danger. Safe = docked at a port; danger = out in open water.
        void UpdateZoneBoard(bool safe){
            if(zoneBoard!=null){var s=safe?zoneSafeSprite:zoneDangerSprite;if(s!=null&&zoneBoard.sprite!=s)zoneBoard.sprite=s;}
            // Out on the water the board doubles as the stock readout: a thinning region has to be visible
            // or the drop in catches just reads as the game being stingy.
            if(zoneBoardText!=null){
                var t=safe?zoneSafeText:FishStock.Label(FishStock.BestReachable(save.Data,boatX,save.Tier));
                if(zoneBoardText.text!=t)zoneBoardText.text=t;}}
        string ClockLabel(){float h=worldHour%24;return $"{Mathf.FloorToInt(h):00}:{Mathf.FloorToInt((h-Mathf.Floor(h))*60):00}";}
        // The speed + safe-speed needles must pivot at the speedometer's centre (their own parent's
        // origin). Force it so they rotate around the dial hub regardless of how the scene was laid out.
        // Speedometer needle Z angle for a given speed. Start angle (at 0) and total sweep are tunable
        // in GameCatalog (live via the Tuning panel).
        static float SpeedNeedleAngle(float speed)=>GameCatalog.SpeedNeedleStart-GameCatalog.SpeedNeedleSweep*Mathf.Clamp01(speed/GameCatalog.MaxSpeed);
        // Single source of truth for every coin readout — the sea/harbor HUD coin AND the upgrade
        // panel coin always show the same balance ("dùng chung Coin giữa tất cả scene").
        void SetCoins(){var c=save.Data.coins.ToString();Set(seaCoins,c);Set(upgradeCoins,c);}
        void SetupNeedles(){CenterNeedle(speedNeedle);CenterNeedle(safeNeedle);}
        // Hub is at the TOP of the needle rect (pivot.y = 1): the needle hangs down from the dial centre.
        void CenterNeedle(RectTransform n){if(n==null)return;n.anchorMin=n.anchorMax=new Vector2(.5f,.5f);n.pivot=new Vector2(.5f,1f);n.anchoredPosition=Vector2.zero;}
    }
}
