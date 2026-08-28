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
            if (titleScreen == null) return;          // no menu authored: go straight to the harbour
            ConfigureDirectTitleArt();
            titleScreen.gameObject.SetActive(true);
            ShowWorld(false);                         // menu is up — the harbour must not sit behind it
            RefreshTitle();
        }

        void ConfigureDirectTitleArt()
        {
            var logo = titleScreen.Find("Logo")?.GetComponent<Image>();
            if (logo != null)
            {
                var directLogo = DirectReskinSprites.Load("rusty-fishing-title-logo");
                if (directLogo != null) logo.sprite = directLogo;
                logo.preserveAspect = true;
                logo.rectTransform.anchoredPosition = new Vector2(0f, 590f);
                logo.rectTransform.sizeDelta = new Vector2(360f, 430f);
            }

            if (titleScreen.Find("RuntimeTitle") != null) return;
            var titleObject = new GameObject("RuntimeTitle", typeof(RectTransform), typeof(CanvasRenderer),
                                             typeof(TextMeshProUGUI), typeof(Shadow));
            var titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.SetParent(titleScreen, false);
            titleRect.anchorMin = titleRect.anchorMax = titleRect.pivot = new Vector2(.5f, .5f);
            titleRect.anchoredPosition = new Vector2(0f, 300f);
            titleRect.sizeDelta = new Vector2(900f, 190f);

            var title = titleObject.GetComponent<TextMeshProUGUI>();
            title.text = "RUSTY FISHING";
            title.alignment = TextAlignmentOptions.Center;
            title.color = new Color32(255, 244, 208, 255);
            title.enableAutoSizing = true;
            title.fontSizeMin = 54f;
            title.fontSizeMax = 104f;
            title.fontStyle = FontStyles.Bold;
            title.raycastTarget = false;
            var buttonLabel = newGameButton != null ? newGameButton.GetComponentInChildren<TMP_Text>(true) : null;
            if (buttonLabel != null) title.font = buttonLabel.font;

            var shadow = titleObject.GetComponent<Shadow>();
            shadow.effectColor = new Color32(61, 29, 29, 230);
            shadow.effectDistance = new Vector2(8f, -8f);
            titleObject.transform.SetSiblingIndex(logo != null ? logo.transform.GetSiblingIndex() + 1 : 1);
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
            // A new game lands IN THE HARBOUR, not at sea. Continue still sails, because a returning
            // player was mid-voyage -- but the opening tutorial talks about the dock, the SET SAIL button
            // and steering out of the harbour, and none of that can be pointed at from open water.
            if (titleScreen != null) titleScreen.gameObject.SetActive(false);
            ShowWorld(true);
        }
    }
}
