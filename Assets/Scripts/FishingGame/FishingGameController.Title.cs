using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    // Front screen. The LAYOUT is authored in the scene, not generated here — build the panel however you
    // like and drag the pieces into the slots on FishingGameController. This file only supplies the
    // behaviour: which button does what, when Continue is offered, and what a new run has to rebuild.
    //
    // Leave Title Screen empty and the game simply starts straight into the harbour, so the build is never
    // blocked by an unfinished menu.
    public sealed partial class FishingGameController
    {
        void SetupTitleRefs()
        {
            BindClick(continueButton, HideTitle);
            BindClick(newGameButton, NewGame);
            if (titleScreen == null)                  // no menu authored: go straight to SEA (not the harbour)
            {
                if (save != null && save.Data.hullHp > 0) SetSail();
                return;
            }
            titleScreen.gameObject.SetActive(true);
            ShowWorld(false);                         // menu is up — the harbour must not sit behind it
            RefreshTitle();
        }

        /// <summary>
        /// Show or hide the world panels WITHOUT touching game state. Awake() has to run OpenHarbor before
        /// the first frame — the docks, market, upgrade availability and quest label are all derived from
        /// it — but that also switches the harbour art on, which is why a fresh launch used to land on the
        /// harbour instead of the menu. So the state stays built and only the panels are hidden.
        ///
        /// Turning the world back on asks the CURRENT mode which panel is the right one, rather than
        /// assuming the harbour: HideTitle is on the Continue button, and a save that is mid-voyage must
        /// come back to the sea.
        /// </summary>
        void ShowWorld(bool on)
        {
            if (harbor != null) harbor.gameObject.SetActive(on && mode == Mode.Harbor);
            if (sea != null) sea.gameObject.SetActive(on && mode != Mode.Harbor);
        }

        /// <summary>A save worth continuing — anything that is not a pristine day one.</summary>
        public bool HasProgress => save != null && (save.Data.day > 1 || save.Data.coins > 0
                                                    || save.Data.missionsDone.Count > 0 || save.Data.cargo.Count > 0
                                                    || save.Data.hookLevel + save.Data.holdLevel
                                                       + save.Data.engineLevel + save.Data.hullLevel + save.Data.shipTier > 0);

        void RefreshTitle()
        {
            if (continueButton != null) continueButton.gameObject.SetActive(HasProgress);
            if (titleSaveLine == null) return;
            titleSaveLine.gameObject.SetActive(HasProgress);
            if (!HasProgress) return;
            var m = MissionBook.Get(save.Data.missionId);
            titleSaveLine.text = $"Day {save.Data.day}   ·   {save.Data.coins}c   ·   "
                               + (m != null ? m.title : "all contracts fulfilled");
        }

        /// <summary>
        /// Dismiss the title and play on from the existing save. The menu hands straight to the SEA, not
        /// to the harbour screen: Awake() had to run OpenHarbor to build everything derived from the save,
        /// but that is bookkeeping, not where the player should land.
        ///
        /// SetSail does the whole handover — swaps the panels, picks Sailing or Night off the clock, turns
        /// the steering controls on, resets the dock zoom and repaints the world art — so this must not
        /// poke the panels itself or it would fight it.
        ///
        /// One case cannot sail: a save stored right after a wreck has hullHp at 0, and SetSail refuses.
        /// Land that one in the harbour instead, because repairs are the only way out of it — otherwise
        /// dismissing the menu would leave both panels hidden and the screen blank.
        /// </summary>
        public void HideTitle()
        {
            if (titleScreen != null) titleScreen.gameObject.SetActive(false);
            if (save != null && save.Data.hullHp > 0) SetSail();
            else ShowWorld(true);
        }

        /// <summary>
        /// Wipe and restart in place. Everything DERIVED from the save has to be rebuilt, not just the
        /// numbers: the depth ruler and camera zoom, the boat art, the dock layout, the obstacle field and
        /// the fish all read from it, so a bare save.Reset() would leave the world showing the old run.
        /// </summary>
        public void NewGame()
        {
            save.Reset();
            ApplyDepthScale();
            SyncUpgradeArt();
            GameCatalog.LayoutDocks();
            SetupObstacles();
            phaseTime = 0f; worldHour = 6f; wasNight = false;
            RepopulateFish();
            OpenHarbor(GameCatalog.Ports[0]);
            // AFTER the rebuild docking, never before: save.Reset() clears the mission, so the docking
            // above is a no-op for missions — and handing mission 1 out here means its "return to Home
            // Harbor" line is still unticked when the player actually sails back.
            EnsureMissionAssigned();
            UpdateMissionUI();
            save.CaptureDayStart();   // day 1 dawn — the rewind point for a sinking on the first day
            // A new game now lands AT SEA (in home harbour water). The reordered tutorial opens with
            // "steer out of the harbour", so the player starts on the water, not on the dock screen.
            if (titleScreen != null) titleScreen.gameObject.SetActive(false);
            if (save.Data.hullHp > 0) SetSail(); else ShowWorld(true);
        }
    }
}
