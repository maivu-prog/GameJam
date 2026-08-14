using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    // The four upgradeable parts. Order MUST match GameCatalog part handling (engine, hull, hook, hold).
    public enum UpgradePart { Engine, Hull, Hook, Hold }

    /// <summary>
    /// One block per part on the shipyard screen. Pick which part via the <see cref="part"/> dropdown,
    /// then wire only the pieces you use — every field is optional. Because each block names its own
    /// part, the order of the array does NOT matter.
    ///
    /// The icon's look is driven by the Button's OWN sprite states — no extra sprite fields to keep in
    /// sync: the button's Image sprite is the normal look, and Button ▸ Transition = Sprite Swap ▸
    /// "Selected Sprite" is shown while this part is the selected one.
    /// </summary>
    [Serializable]
    public sealed class UpgradePartUI
    {
        public UpgradePart part;          // which part this block controls (dropdown)
        public Button button;             // round icon on the ship — tap to select; supplies the sprite states
        public GameObject highlight;      // optional glow ring — shown only while selected
        public TMP_Text label;            // optional label → "ENGINE II" (updates with level)
        public GameObject readyIndicator; // optional badge — shown when this part can be upgraded & afforded

        [Tooltip("Optional big art shown on the detail card while this part is selected.")]
        public Sprite bigSprite;

        [NonSerialized] Sprite normalCache;
        [NonSerialized] bool cached;

        // Swap the button's icon between its normal sprite (the Image's own sprite) and its configured
        // Sprite-Swap "Selected Sprite". Setting the base sprite plays nicely with the Button's
        // transition (highlight/press still layer on top via overrideSprite).
        public void SetSelected(bool selected)
        {
            var img = button != null ? button.image : null;
            if (img == null) return;
            if (!cached) { normalCache = img.sprite; cached = true; }
            var sel = button.spriteState.selectedSprite;
            img.sprite = selected && sel != null ? sel : normalCache;
        }
    }
}
