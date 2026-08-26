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
    /// The selected look is driven by the Button's OWN configured transition — no extra sprite or colour
    /// fields to keep in sync. Set Button ▸ Transition to Sprite Swap, Color Tint or Animation and fill in
    /// its "Selected" slot; whichever you pick is what shows while this part is the one on the card. With
    /// Transition = None, use the optional `highlight` object instead.
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

        [NonSerialized] Sprite normalSprite;
        [NonSerialized] Color normalColor;
        [NonSerialized] bool cached;

        /// <summary>
        /// Show or clear this part's "I am the chosen one" look, using whatever the Button itself was
        /// configured with — Sprite Swap, Color Tint or Animation.
        ///
        /// It used to read spriteState.selectedSprite and nothing else, so a button left on the default
        /// Color Tint transition had a null there and simply never changed. Which state to read has to
        /// follow the transition the designer actually picked.
        ///
        /// Note this does NOT use Unity's own selection: a Selectable's "Selected" state means keyboard
        /// or controller focus, and it is lost the moment anything else on screen is clicked. Selection
        /// here means "this is the part the panel is showing", which has to survive that.
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (button == null) return;
            var img = button.image;

            switch (button.transition)
            {
                case Selectable.Transition.SpriteSwap:
                    if (img == null) return;
                    if (!cached) { normalSprite = img.sprite; cached = true; }
                    var sel = button.spriteState.selectedSprite;
                    img.sprite = selected && sel != null ? sel : normalSprite;
                    break;

                case Selectable.Transition.ColorTint:
                    if (img == null) return;
                    // Tint the Image's BASE colour, not the CanvasRenderer: Selectable owns the renderer
                    // colour and rewrites it on every hover, which would wipe this out on the next mouse move.
                    if (!cached) { normalColor = img.color; cached = true; }
                    img.color = selected ? button.colors.selectedColor : normalColor;
                    break;

                case Selectable.Transition.Animation:
                    var anim = button.GetComponent<Animator>();
                    if (anim == null || !anim.isActiveAndEnabled) return;
                    anim.SetTrigger(selected ? button.animationTriggers.selectedTrigger
                                             : button.animationTriggers.normalTrigger);
                    break;

                // Transition = None: nothing on the Button to read. Use the optional `highlight`
                // GameObject for the selected look instead — RefreshUpgradeSelector drives it either way.
            }
        }
    }
}
