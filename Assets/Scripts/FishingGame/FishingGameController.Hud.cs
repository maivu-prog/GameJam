using System.Linq;
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
            var nearby=GameCatalog.Obstacles.OrderBy(o=>Mathf.Abs(o.x-boatX)).FirstOrDefault();
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
        // Harbor-arrival banner: when the boat enters a port zone, flash the port name for ~2s then fade.
        void UpdateArrivalBanner(PortDef port){
            if(port!=null&&port!=bannerPort){bannerPort=port;bannerTimer=BannerHold+BannerFade;if(harborZone!=null){harborZone.text=port.name.ToUpperInvariant();harborZone.alpha=1;harborZone.gameObject.SetActive(true);}}
            else if(port==null)bannerPort=null;
            if(bannerTimer>0){bannerTimer-=Time.deltaTime;if(harborZone!=null)harborZone.alpha=bannerTimer>BannerFade?1f:Mathf.Clamp01(bannerTimer/BannerFade);if(bannerTimer<=0&&harborZone!=null)harborZone.gameObject.SetActive(false);}}
        // HP fill bar: fillAmount = hullHp / maxHp, with an optional green→yellow→red tint.
        void UpdateHpBar(){if(hpFill==null)return;float frac=save.MaxHp>0?Mathf.Clamp01((float)save.Data.hullHp/save.MaxHp):0f;hpFill.fillAmount=frac;if(hpFillTint)hpFill.color=frac>.5f?new Color(.42f,.82f,.44f):frac>.25f?new Color(.9f,.77f,.3f):new Color(.85f,.32f,.3f);}
        // Swap the board sprite + text between safe/danger. Safe = docked at a port; danger = out in open water.
        void UpdateZoneBoard(bool safe){
            if(zoneBoard!=null){var s=safe?zoneSafeSprite:zoneDangerSprite;if(s!=null&&zoneBoard.sprite!=s)zoneBoard.sprite=s;}
            if(zoneBoardText!=null){var t=safe?zoneSafeText:zoneDangerText;if(zoneBoardText.text!=t)zoneBoardText.text=t;}}
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
